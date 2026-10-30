using Golferoo.Sim.Buildings;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>
/// Arrivées au pavillon (C15 lot 2) : des groupes de visiteurs arrivent seuls selon l'attrait du club, attendent dans une file devant la porte,
/// partent dès que le premier départ est libre (règles de départ de C12, avant la dernière heure de départ), puis sortent par la porte.
/// Les arrivées n'ont lieu que s'il y a un pavillon et un trou 1 ouvert.
/// </summary>
public sealed partial class World
{
    private const ulong ArrivalSeedMask = 0x41525249564C5321UL;

    private readonly DeterministicRng _arrivalRng;
    private bool _groupLeft;

    /// <summary>Réglages des arrivées (<c>data/golfers/arrivals.json</c> par défaut ; non sauvés, comme les données embarquées).</summary>
    public ArrivalParams Arrivals { get; }

    /// <summary>Réputation du club, de 0 à 1 : moyenne glissante des sorties (bonne = 1, mauvaise = 0).</summary>
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

    /// <summary>Vrai tant que l'heure de jeu est avant la dernière heure de départ.</summary>
    private bool DeparturesOpen => ArrivalParams.HourOf(DayFractionNow) < Arrivals.LastDepartureHour;

    private double DayFractionNow => (double)(Tick % Calendar.TicksPerDay) / (Calendar.TicksPerDay - 1);

    /// <summary>Un groupe de visiteurs peut partir : avant la dernière heure de départ, et le parcours n'est pas déjà plein de groupes.</summary>
    private bool CanStartArrival(TerrainGrid terrain) =>
        DeparturesOpen && _groups.Count(g => g.Released && !g.Finished && !g.Leaving) < Arrivals.MaxGroupsPerHole * terrain.Course.Count;

    /// <summary>Attrait du club à cet instant (0 sans trou ouvert).</summary>
    public double Attraction => Arrivals.Attraction(_holesOpen.Count(o => o), CourseRating, Reputation, Weather.Sky);

    /// <summary>Un tirage par tick : la probabilité d'une arrivée est le taux de l'heure divisé par les ticks d'une heure.</summary>
    private void StepArrivals()
    {
        if (Clubhouse is null || Terrain is not { Blank: false } || !HoleOpen)
        {
            return;
        }

        var hour = (int)(Tick % Calendar.TicksPerDay * 12 / Calendar.TicksPerDay);
        var perTick = Arrivals.RatePerHour(hour, Attraction) * 12.0 / Calendar.TicksPerDay;
        if (_arrivalRng.NextDouble() < perTick)
        {
            Arrive();
        }
    }

    private void Arrive()
    {
        var day = DayIndex;
        var visitors = Compose(Arrivals.GroupSize(_arrivalRng.NextDouble()));
        if (QueueLength >= Arrivals.QueueCapacity)
        {
            // File pleine : le groupe repart, mauvaise sortie.
            foreach (var v in visitors)
            {
                v.InPlay = false;
                v.LastVisitDay = day;
                v.Desire = Math.Max(0.0, v.Desire - Arrivals.DesireRefused);
            }

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
            if (_arrivalRng.NextDouble() < Arrivals.ReturnShare && Visitors.Returning(day, Arrivals.ReturnThreshold) is { Count: > 0 } back)
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
            });
        }

        var group = new Group(_nextGroupId++, golfers);
        _groups.Add(group);
        return group;
    }

    /// <summary>Les golfeurs du groupe marchent vers la porte ; arrivés, le groupe quitte le monde et ses visiteurs retournent au vivier avec une bonne sortie.</summary>
    private void UpdateLeaving(TerrainGrid terrain, Group group)
    {
        var door = Clubhouse?.DoorPosition(terrain.CellSize) ?? ExitPoint;
        foreach (var g in group.Golfers)
        {
            if (g.State == GolferState.Leaving && Walk(terrain, g, door))
            {
                g.State = GolferState.Done;
            }
        }

        if (!group.Finished)
        {
            return;
        }

        var visitors = new List<Visitor>(group.Golfers.Count);
        foreach (var g in group.Golfers)
        {
            var v = Visitors.Visitors[g.VisitorId];
            v.InPlay = false;
            v.LastVisitDay = DayIndex;
            v.Desire = Math.Min(1.0, v.Desire + Arrivals.DesireGood);
            visitors.Add(v);
        }

        Reputation += (1.0 - Reputation) * Arrivals.ReputationWeight;
        group.Left = true;
        _groupLeft = true;
        GroupLeft?.Invoke(visitors);
    }

    /// <summary>La nuit tombe : les groupes encore en file, qui n'ont pas pu partir avant la dernière heure de départ, rentrent chez eux sans que rien ne change pour eux.</summary>
    private void DismissWaitingArrivals()
    {
        foreach (var g in _groups.Where(g => g.FromArrivals && !g.Released).ToList())
        {
            foreach (var golfer in g.Golfers)
            {
                Visitors.Visitors[golfer.VisitorId].InPlay = false;
            }

            _groups.Remove(g);
        }
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
