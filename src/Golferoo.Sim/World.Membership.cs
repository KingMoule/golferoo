using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Adhésion (C16 lot 2) : après une partie finie, un visiteur gagne des points (trous aimés, humeur de sortie, pro) et monte de niveau ; un membre qui abandonne
/// démissionne pour toujours. Les membres sont ceux de la ligue. Le pro peut se joindre à un groupe de la file (<see cref="JoinQueuedGroup"/>).
/// </summary>
public sealed partial class World
{
    private readonly Queue<int> _joinCommands = new();

    /// <summary>Règle d'adhésion (<c>data/golfers/membership.json</c> par défaut ; non sauvée, comme les données embarquées).</summary>
    public MembershipParams Memberships { get; }

    /// <summary>Un visiteur devient membre (levé quand sa partie finie le fait passer de Visitor à Member).</summary>
    public event Action<Visitor>? MemberJoined;

    /// <summary>Un membre monte d'un niveau ou plus (<see cref="Visitor.Level"/> est le nouveau).</summary>
    public event Action<Visitor>? MemberPromoted;

    /// <summary>Un membre a abandonné une partie : il démissionne pour de bon.</summary>
    public event Action<Visitor>? MemberResigned;

    /// <summary>Le pro s'est joint à un groupe de la file (levé au tick où la commande s'applique).</summary>
    public event Action<Group>? ProJoinedGroup;

    private bool IsPro(Golfer g) => g.MemberId >= 0 && g.MemberId == _proMemberId;

    /// <summary>Le pro fait partie d'un groupe (partie d'entraînement, journée de ligue ou groupe de la file).</summary>
    private bool ProPlays => _groups.Any(g => g.Golfers.Any(IsPro));

    /// <summary>Le club pré-rempli : chaque membre de la ligue qui n'a pas de visiteur en reçoit un, au niveau Member, qui a envie de revenir comme les autres.</summary>
    private void AdoptClub()
    {
        foreach (var m in League!.Members.Where(m => !m.IsPro && m.VisitorId < 0))
        {
            m.VisitorId = Visitors.Add(m.Name, m.StartHandicap, m.Profile, Arrivals.DesireStart, MemberLevel.Member, Memberships.Thresholds[0]).Id;
        }
    }

    private Member? MemberOf(Visitor v) => League?.Members.FirstOrDefault(m => m.VisitorId == v.Id);

    /// <summary>La partie du groupe est finie : chaque golfeur qui ne l'a pas abandonnée gagne ses points ; un niveau franchi lève <see cref="MemberJoined"/> ou <see cref="MemberPromoted"/>.</summary>
    private void AwardPoints(Group group)
    {
        var withPro = group.Golfers.Any(IsPro);
        foreach (var g in group.Golfers)
        {
            if (!HasMood(g) || g.Quit)
            {
                continue;
            }

            var v = Visitors.Visitors[g.VisitorId];
            var from = v.Level;
            v.Points += Memberships.PointsFor(g.LikedHoles, g.Mood, withPro && from != MemberLevel.Visitor);
            var to = Memberships.LevelOf(v.Points);
            if (to <= from)
            {
                continue;
            }

            v.Level = to;
            if (from != MemberLevel.Visitor)
            {
                MemberPromoted?.Invoke(v);
                continue;
            }

            League?.Enroll(new Member(League.Members.Count, v.Name, v.Handicap, false, v.Profile, v.Id));
            MemberJoined?.Invoke(v);
        }
    }

    /// <summary>Le golfeur abandonne : s'il est membre, il démissionne (définitivement) et quitte la ligue.</summary>
    private void ResignIfMember(Golfer g)
    {
        var v = Visitors.Visitors[g.VisitorId];
        if (v.Level == MemberLevel.Visitor)
        {
            return;
        }

        v.Level = MemberLevel.Visitor;
        v.Resigned = true;
        if (MemberOf(v) is { } member)
        {
            League!.Resign(member);
        }

        MemberResigned?.Invoke(v);
    }

    /// <summary>Tarif du green fee (cents par trou) de ce niveau d'adhésion.</summary>
    private long LevelFee(MemberLevel level) => level switch
    {
        MemberLevel.Member => Economy.GreenFeeMember,
        MemberLevel.Silver => Economy.GreenFeeSilver,
        MemberLevel.Gold => Economy.GreenFeeGold,
        MemberLevel.Platinum => Economy.GreenFeePlatinum,
        _ => Economy.GreenFeeVisitor,
    };

    /// <summary>Cotisation annuelle (cents) de ce niveau d'adhésion.</summary>
    private long LevelDues(MemberLevel level) => level switch
    {
        MemberLevel.Member => Economy.DuesMember,
        MemberLevel.Silver => Economy.DuesSilver,
        MemberLevel.Gold => Economy.DuesGold,
        MemberLevel.Platinum => Economy.DuesPlatinum,
        _ => 0,
    };

    /// <summary>Le groupe de la file <paramref name="groupId"/> peut recevoir le pro : il attend encore son départ, le pro est libre, et aucun autre groupe ne l'attend déjà.</summary>
    public bool CanJoinQueuedGroup(int groupId) =>
        Pro is not null && Terrain is not null && _joinCommands.Count == 0 && !ProPlays && _groups.Any(g => g.Id == groupId && g.FromArrivals && !g.Released);

    /// <summary>
    /// Commande (en file comme les autres, appliquée au début du tick suivant) : le groupe de la file passe en tête et le pro s'y joint, pour partir avec lui au prochain départ libre.
    /// Le groupe reçoit <see cref="MoodEvent.ProPlaysWithUs"/>, un membre accompagné gagne un point de plus. Rend faux, sans rien faire, si <see cref="CanJoinQueuedGroup"/> l'est.
    /// </summary>
    public bool JoinQueuedGroup(int groupId)
    {
        if (!CanJoinQueuedGroup(groupId))
        {
            return false;
        }

        _joinCommands.Enqueue(groupId);
        return true;
    }

    private void ApplyJoins()
    {
        while (_joinCommands.Count > 0)
        {
            var id = _joinCommands.Dequeue();
            if (Pro is null || ProPlays || _groups.FirstOrDefault(g => g.Id == id && g.FromArrivals && !g.Released) is not { } group)
            {
                continue;
            }

            var terrain = RequireTerrain();
            var pro = League!.Members.First(m => m.IsPro);
            var lead = group.Golfers[0];
            var golfer = new Golfer(_nextGolferId++, GridQueries.NearestWalkablePoint(terrain, new Point2(lead.Position.X, lead.Position.Z + 1.2)), GolferState.Arriving)
            {
                Ball = terrain.Tee,
                Card = new ScoreCard([.. terrain.Course.Holes.Select(h => h.Par)]),
                MemberId = pro.Id,
                Skill = pro.Skill,
                Profile = pro.Profile,
            };
            group.Golfers = [.. group.Golfers, golfer];

            var first = _groups.FindIndex(g => !g.Released);
            _groups.Remove(group);
            _groups.Insert(first, group);

            foreach (var g in group.Golfers.Where(HasMood))
            {
                Raise(g, MoodEvent.ProPlaysWithUs, 1, g.Position);
            }

            ProJoinedGroup?.Invoke(group);
        }
    }

    private void MixMembership(ref ulong hash)
    {
        foreach (var id in _joinCommands)
        {
            Mix(ref hash, 0x4A4F494EUL);
            Mix(ref hash, (ulong)id);
        }
    }
}
