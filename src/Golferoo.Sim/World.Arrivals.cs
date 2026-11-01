using Golferoo.Sim.Buildings;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>
/// Arrivées au pavillon (C15 lot 2) : des groupes de visiteurs arrivent seuls selon l'attrait du club, attendent dans une file devant la porte,
/// partent dès que le premier départ est libre (règles de départ de C12), puis sortent par la porte.
/// Les arrivées n'ont lieu que s'il y a un pavillon et un trou 1 ouvert.
/// </summary>
public sealed partial class World
{
    private const ulong ArrivalSeedMask = 0x41525249564C5321UL;

    private readonly DeterministicRng _arrivalRng;
    private bool _groupLeft;

    /// <summary>Réglages des arrivées (<c>data/golfers/arrivals.json</c> par défaut ; non sauvés, comme les données embarquées).</summary>
    public ArrivalParams Arrivals { get; }

    /// <summary>Réputation du club, de 0 à 1 : moyenne glissante de l'humeur de sortie des groupes (<see cref="MoodParams.ExitScore"/>), un groupe refusé comptant 0.</summary>
    public double Reputation { get; private set; }

    /// <summary>Visiteurs du club, persistants.</summary>
    public VisitorPool Visitors { get; private set; } = new();

    /// <summary>Un groupe arrive et entre dans la file (levé au tick de l'arrivée).</summary>
    public event Action<Group>? GroupArrived;

    /// <summary>Un groupe arrive file pleine et repart : ses visiteurs, qui perdent l'envie de revenir (levé au tick de l'arrivée).</summary>
    public event Action<IReadOnlyList<Visitor>>? GroupTurnedAway;

    /// <summary>Un groupe sort par la porte et quitte le monde, ses visiteurs de retour au vivier (levé au tick de la sortie).</summary>
    public event Action<IReadOnlyList<Visitor>>? GroupLeft;

    /// <summary>Groupes de visiteurs qui attendent leur départ devant la porte.</summary>
    public int QueueLength => _groups.Count(g => g.FromArrivals && !g.Released);

    /// <summary>Un groupe de visiteurs peut partir : le parcours n'est pas déjà plein de groupes.</summary>
    private bool CanStartArrival(TerrainGrid terrain) =>
        _groups.Count(g => g.Released && !g.Finished && !g.Leaving) < Arrivals.MaxGroupsPerHole * terrain.Course.Count;

    /// <summary>Attrait du club à cet instant (0 sans trou ouvert).</summary>
    public double Attraction => Arrivals.Attraction(_holesOpen.Count(o => o), Reputation, Weather.Sky);

    /// <summary>Un tirage par tick, à taux constant sur la journée.</summary>
    private void StepArrivals()
    {
        if (Clubhouse is null || !State.ClubActive || Terrain is null || !HoleOpen)
        {
            return;
        }

        // Leçon : tant qu'aucun groupe n'est venu, l'attrait vaut au moins 1 (pluie et un seul trou la ramenaient à 0,03 : une semaine d'attente).
        var attraction = Terrain.Blank && Visitors.Next == 0 ? Math.Max(Attraction, 1.0) : Attraction;
        if (_arrivalRng.NextDouble() < Arrivals.RatePerTick(attraction))
        {
            Arrive();
        }
    }

    private void Arrive()
    {
        var day = DayIndex;
        var firstNew = Visitors.Next;
        var visitors = Compose(Arrivals.GroupSize(_arrivalRng.NextDouble()));
        if (QueueLength >= Arrivals.QueueCapacity)
        {
            // File pleine : le groupe repart, mauvaise sortie. Un nouveau visiteur refusé n'entre pas dans le vivier.
            foreach (var v in visitors)
            {
                v.InPlay = false;
                if (v.Id < firstNew)
                {
                    v.LastVisitDay = day;
                    v.Desire = Math.Max(0.0, v.Desire - Arrivals.DesireRefused);
                }
            }

            Visitors.TruncateTo(firstNew);

            Reputation += (0.0 - Reputation) * Arrivals.ReputationWeight;
            GroupTurnedAway?.Invoke(visitors);
            return;
        }

        var group = EnqueueGroup(visitors);
        GroupArrived?.Invoke(group);
    }

    /// <summary>D'abord des visiteurs du vivier qui veulent revenir (pour une part des places), puis de nouveaux, qui entrent dans le vivier.</summary>
    private List<Visitor> Compose(int size)
    {
        var day = DayIndex;
        var group = new List<Visitor>(size);
        for (var i = 0; i < size; i++)
        {
            Visitor? chosen = null;
            if (_arrivalRng.NextDouble() < Arrivals.ReturnShare && Visitors.Returning(day, Arrivals.ReturnThreshold).Where(v => !InLeagueGroup(v)).ToList() is { Count: > 0 } back)
            {
                chosen = back[(int)(_arrivalRng.NextUInt64() % (ulong)back.Count)];
            }

            chosen ??= NewVisitor();
            chosen.InPlay = true;
            group.Add(chosen);
        }

        return group;
    }

    private Visitor NewVisitor()
    {
        var names = Arrivals.FirstNames;
        var name = $"{names[(int)(_arrivalRng.NextUInt64() % (ulong)names.Length)]} {(char)('A' + (int)(_arrivalRng.NextUInt64() % 26))}.";
        var handicap = Arrivals.HandicapMin + ((Arrivals.HandicapMax - Arrivals.HandicapMin) * _arrivalRng.NextDouble());
        return Visitors.Add(name, handicap, GolferProfile.Pick(_arrivalRng), Arrivals.DesireStart);
    }

    /// <summary>Le groupe prend la fin de la file : ses golfeurs attendent devant la porte, en retrait d'un rang par groupe déjà en file.</summary>
    private Group EnqueueGroup(List<Visitor> visitors)
    {
        var terrain = RequireTerrain();
        var club = Clubhouse!;
        var door = club.DoorPosition(terrain.CellSize);
        var (ox, oz) = club.Side switch
        {
            DoorSide.North => (0.0, -1.0),
            DoorSide.East => (1.0, 0.0),
            DoorSide.South => (0.0, 1.0),
            _ => (-1.0, 0.0),
        };
        var rank = QueueLength;
        var golfers = new List<Golfer>(visitors.Count);
        for (var i = 0; i < visitors.Count; i++)
        {
            var across = (i - ((visitors.Count - 1) / 2.0)) * 1.2;
            var wanted = new Point2(door.X + (ox * (1.5 + (rank * 2.5))) - (oz * across), door.Z + (oz * (1.5 + (rank * 2.5))) + (ox * across));
            var p = GridQueries.NearestWalkablePoint(terrain, wanted);
            var v = visitors[i];
            golfers.Add(new Golfer(_nextGolferId++, club.Footprint.Contains(terrain.CellXOf(p.X), terrain.CellZOf(p.Z)) ? door : p, GolferState.Arriving)
            {
                Ball = terrain.Tee,
                Card = new ScoreCard([.. terrain.Course.Holes.Select(h => h.Par)]),
                Skill = SkillLevels.FromHandicap(v.Handicap),
                Profile = v.Profile,
                VisitorId = v.Id,
                Mood = Moods.Start,
            });
        }

        var group = new Group(_nextGroupId++, golfers) { ArrivedTick = Tick };
        _groups.Add(group);
        return group;
    }

    /// <summary>
    /// Les golfeurs du groupe marchent vers la porte ; arrivés, le groupe quitte le monde et ses visiteurs retournent au vivier. Leur humeur de sortie
    /// décide de leur envie de revenir (<c>desireGood</c> si elle n'est pas négative, sinon <c>desireRefused</c> en moins) et fait la réputation du club.
    /// </summary>
    private void UpdateLeaving(TerrainGrid terrain, Group group)
    {
        var door = Clubhouse?.DoorPosition(terrain.CellSize) ?? ExitPoint;
        foreach (var g in group.Golfers)
        {
            if (g.State is GolferState.Leaving or GolferState.Quitting && Walk(terrain, g, door))
            {
                g.State = GolferState.Done;
            }
        }

        if (!group.Finished)
        {
            return;
        }

        AwardPoints(group);
        var visitors = new List<Visitor>(group.Golfers.Count);
        foreach (var g in group.Golfers.Where(HasMood))
        {
            var v = Visitors.Visitors[g.VisitorId];
            v.InPlay = false;
            v.LastVisitDay = DayIndex;
            v.Desire = Math.Clamp(v.Desire + (g.Mood >= 0 ? Arrivals.DesireGood : -Arrivals.DesireRefused), 0.0, 1.0);
            visitors.Add(v);
        }

        State.RecordExit(visitors.Count, group.Golfers.Where(HasMood).Sum(g => (long)g.Mood));
        Reputation += (ExitScore(group) - Reputation) * Arrivals.ReputationWeight;
        group.Left = true;
        _groupLeft = true;
        GroupLeft?.Invoke(visitors);
    }

    internal void WriteArrivals(Save.SaveWriter w)
    {
        _arrivalRng.Write(w);
        w.Double(Reputation);
        Visitors.Write(w);
        w.Int(_nextGroupId);
    }

    private void MixArrivals(ref ulong hash)
    {
        if (Clubhouse is null)
        {
            return;
        }

        Mix(ref hash, _arrivalRng.StateHash());
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(Reputation));
        Mix(ref hash, (ulong)_nextGroupId);
        Visitors.Mix(ref hash);
    }
}
