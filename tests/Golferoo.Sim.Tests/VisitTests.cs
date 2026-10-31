using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>C17 lot 2 : visites à pied (snack-bar, pro shop), revenus, humeur, bonus du pro shop, hangar, banc, sauvegarde v18.</summary>
public class VisitTests
{
    internal static Dictionary<Archetype, double> All(double p) => Enum.GetValues<Archetype>().ToDictionary(a => a, _ => p);

    /// <summary>Tout le monde visite à coup sûr : les tests ne dépendent pas du tirage.</summary>
    private static BuildingParams Sure => BuildingParams.Default with { SnackBarChance = All(1.0), ProShopChance = All(1.0) };

    private static World NewGame(BuildingParams? rules = null) =>
        new(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: false, placeClubhouse: true, buildings: rules ?? Sure);

    private static double CellsBetween(World world, (int X, int Z) a, Point2 p) =>
        Math.Sqrt(Math.Pow(a.X - world.Terrain!.CellXOf(p.X), 2) + Math.Pow(a.Z - world.Terrain.CellZOf(p.Z), 2));

    /// <summary>Les angles de la carte, rangés par distance en cellules de la porte à <paramref name="score"/> (le test de pose coûte un champ de flux : on ne le fait qu'au besoin).</summary>
    private static IEnumerable<(int X, int Z)> Corners(World world, BuildingKind kind, DoorSide side, Func<(int X, int Z), double> score, bool descending = false)
    {
        var t = world.Terrain!;
        var all = Enumerable.Range(0, t.CellsZ).SelectMany(z => Enumerable.Range(0, t.CellsX).Select(x => (X: x, Z: z)));
        var keyed = all.Select(c => (c, d: score(world.BuildingRules.At(kind, c.X, c.Z, side)!.DoorCell)));
        return (descending ? keyed.OrderByDescending(k => k.d) : keyed.OrderBy(k => k.d)).Select(k => k.c);
    }

    /// <summary>L'angle où la pose est acceptée et dont la porte est la plus proche de <paramref name="around"/>.</summary>
    internal static (int X, int Z) Nearest(World world, BuildingKind kind, Point2 around, DoorSide side = DoorSide.South) =>
        Corners(world, kind, side, d => CellsBetween(world, d, around)).First(c => world.CheckBuilding(kind, c.X, c.Z, side) == BuildingPlacement.Placed);

    internal static Building Place(World world, BuildingKind kind, Point2 around, DoorSide side = DoorSide.South)
    {
        var (x, z) = Nearest(world, kind, around, side);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(kind, x, z, side));
        return world.PlacedBuildings[^1];
    }

    internal static Point2 ClubDoor(World world) => world.Clubhouse!.DoorPosition(world.Terrain!.CellSize);

    /// <summary>Un snack-bar près du trou du virage et un pro shop près du pavillon.</summary>
    private static void EquipBoth(World world, int extra = 0)
    {
        for (var i = 0; i <= extra; i++)
        {
            Place(world, BuildingKind.ProShop, ClubDoor(world));
            Place(world, BuildingKind.SnackBar, world.ExitPointOf(5));
        }
    }

    private sealed record Visit(Golfer Golfer, BuildingKind Kind, (int X, int Z) Cell, int Delta, Point2 At, long Cents);

    /// <summary>Joue <paramref name="days"/> jours en notant chaque visite (événement <see cref="MoodEvent.ServiceVisited"/>), l'argent reçu juste avant et le golfeur à ce moment.</summary>
    private static List<Visit> Play(World world, int days, Action<Group>? onArrival = null)
    {
        var visits = new List<Visit>();
        long previous = world.Ledger.Balance, lastGain = 0;
        world.Ledger.MoneyChanged += balance =>
        {
            lastGain = balance - previous;
            previous = balance;
        };
        if (onArrival is not null)
        {
            world.GroupArrived += onArrival;
        }

        world.MoodChanged += c =>
        {
            if (c.Event == MoodEvent.ServiceVisited)
            {
                var kind = world.PlacedBuildings.FirstOrDefault(b => b.DoorCell == c.Cell)?.Kind ?? BuildingKind.Clubhouse;
                visits.Add(new Visit(c.Golfer, kind, c.Cell, c.Delta, c.Golfer.Position, lastGain));
            }
        };
        world.Step(days * Calendar.TicksPerDay);
        return visits;
    }

    [Fact]
    public void Visit_SnackBarAndProShop_GolfersWalkToTheDoorPayAndRaiseServiceVisited()
    {
        var world = NewGame();
        EquipBoth(world);
        var cell = world.Terrain!.CellSize;

        var visits = Play(world, 6);

        Assert.Contains(visits, v => v.Kind == BuildingKind.ProShop);
        Assert.Contains(visits, v => v.Kind == BuildingKind.SnackBar);
        foreach (var v in visits)
        {
            var building = world.PlacedBuildings.Single(b => b.DoorCell == v.Cell);
            var door = building.DoorPosition(cell);
            Assert.True(Math.Abs(v.At.X - door.X) < cell && Math.Abs(v.At.Z - door.Z) < cell, "le golfeur est à la porte");
            Assert.Equal(1, v.Delta);
            Assert.Equal(world.BuildingRules.Spec(v.Kind)!.Revenue, v.Cents);
        }
    }

    [Fact]
    public void Visit_AGolferVisitsOnlyOneBuildingOfEachKindPerRound()
    {
        var world = NewGame();
        EquipBoth(world, extra: 1);
        Assert.Equal(2, world.PlacedBuildings.Count(b => b.Kind == BuildingKind.SnackBar));

        var visits = Play(world, 6);

        Assert.NotEmpty(visits);
        Assert.All(visits.GroupBy(v => (v.Golfer.Id, v.Kind)), g => Assert.Single(g));
    }

    [Fact]
    public void Visit_WithoutABuildingInRange_NobodyVisits_AndGolfersStillPlay()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var landmarks = new[] { ClubDoor(world) }.Concat(Enumerable.Range(1, t.Course.Count).Select(h => t.Course[h].Hole)).ToList();
        double Closest((int X, int Z) door) => landmarks.Min(l => CellsBetween(world, door, l));
        var far = Corners(world, BuildingKind.SnackBar, DoorSide.South, Closest, descending: true).First(c => world.CheckBuilding(BuildingKind.SnackBar, c.X, c.Z, DoorSide.South) == BuildingPlacement.Placed);
        var farthest = Closest(world.BuildingRules.At(BuildingKind.SnackBar, far.X, far.Z, DoorSide.South)!.DoorCell);

        Assert.True(farthest > world.BuildingRules.SnackBar.Range, "le snack-bar est hors de portée de tout");
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, far.X, far.Z, DoorSide.South));
        var left = 0;
        world.GroupLeft += _ => left++;

        var visits = Play(world, 6);

        Assert.Empty(visits);
        Assert.True(left > 0, "des groupes ont joué");
    }

    [Fact]
    public void Visit_AGroupAlreadyFlaggedSlow_DoesNotVisit()
    {
        var world = NewGame();
        EquipBoth(world);

        var visits = Play(world, 6, g =>
        {
            foreach (var o in g.Golfers)
            {
                o.Occurrences[(int)MoodEvent.SlowPlay] = 1;
            }
        });

        Assert.Empty(visits);
    }

    [Fact]
    public void Visit_Chance_ComesFromTheProfile()
    {
        var never = BuildingParams.Default with { SnackBarChance = All(0.0), ProShopChance = All(0.0) };
        var world = NewGame(never);
        EquipBoth(world);

        Assert.Empty(Play(world, 6));
    }

    [Fact]
    public void ProShop_VisitScalesTheGolfersDispersionByTheBoundedFactor_AndOnlyOnce()
    {
        var world = NewGame();
        EquipBoth(world, extra: 1);
        var start = new Dictionary<int, double>();

        var visits = Play(world, 6, g =>
        {
            foreach (var o in g.Golfers)
            {
                start[o.Id] = o.Skill;
            }
        });

        var shops = visits.Where(v => v.Kind == BuildingKind.ProShop).ToList();
        Assert.NotEmpty(shops);
        Assert.All(shops, v => Assert.Equal(start[v.Golfer.Id] * world.BuildingRules.ProShopSkillFactor, v.Golfer.Skill, 12));
        Assert.All(visits.Where(v => v.Kind == BuildingKind.SnackBar && shops.All(s => s.Golfer.Id != v.Golfer.Id)), v => Assert.Equal(start[v.Golfer.Id], v.Golfer.Skill));
    }

    [Fact]
    public void ServiceVisited_IsAFrenchThought_AndDoesNotCountAsALikedHole()
    {
        var def = MoodParams.Default.Definitions[(int)MoodEvent.ServiceVisited];
        Assert.Equal(1, def.Value);
        Assert.False(def.Minor);
        Assert.Contains(' ', def.Thought);
    }

    private static long FirstMonthMaintenance(World world)
    {
        world.Step((Calendar.DaysPerMonth * Calendar.TicksPerDay) + 1);
        return world.Ledger.Months[0].Total(LedgerCategory.Maintenance);
    }

    [Fact]
    public void Shed_CutsMonthlyMaintenanceByFifteenPercent_AndTwoShedsDoNotCutThirty()
    {
        var none = NewGame();
        var one = NewGame();
        Place(one, BuildingKind.MaintenanceShed, ClubDoor(one));
        var two = NewGame();
        Place(two, BuildingKind.MaintenanceShed, ClubDoor(two));
        Place(two, BuildingKind.MaintenanceShed, ClubDoor(two), DoorSide.West);
        Assert.Equal(2, two.PlacedBuildings.Count(b => b.Kind == BuildingKind.MaintenanceShed));

        var (m0, m1, m2) = (FirstMonthMaintenance(none), FirstMonthMaintenance(one), FirstMonthMaintenance(two));

        Assert.True(m0 < 0);
        Assert.Equal(m0 * 8500 / 10_000, m1);
        Assert.Equal(m1, m2);
    }

    [Fact]
    public void MowerDepot_IsTheNearestShedDoor_OtherwiseTheClubhouseDoor()
    {
        var world = NewGame();
        var t = world.Terrain!;
        Assert.Equal(ClubDoor(world), world.MowerDepot(t.Tee));

        var far = Place(world, BuildingKind.MaintenanceShed, t.Course[9].Hole);
        var near = Place(world, BuildingKind.MaintenanceShed, t.Tee, DoorSide.West);

        Assert.Equal(near.DoorPosition(t.CellSize), world.MowerDepot(t.Tee));
        Assert.Equal(far.DoorPosition(t.CellSize), world.MowerDepot(t.Course[9].Hole));
    }

    [Fact]
    public void Bench_AddsABoundedBeautyBonusNearby_AndSellingItTakesItAway()
    {
        var world = NewGame();
        Assert.Null(world.BenchAccent);
        var bench = Place(world, BuildingKind.Bench, world.Terrain!.Tee);
        var accent = world.BenchAccent!;
        var spot = bench.Position(world.Terrain.CellSize);
        var rules = world.BuildingRules.BenchBeauty;
        Assert.Equal((rules.Weight, rules.RadiusMeters), (accent.Weight, accent.RadiusMeters));

        var plain = BeautyMap.Build(world.Terrain, world.Trees, Season.Summer, []);
        var with = BeautyMap.Build(world.Terrain, world.Trees, Season.Summer, [], accent);

        Assert.Equal(Math.Min(1.0, plain.At(spot.X, spot.Z) + rules.Weight), with.At(spot.X, spot.Z), 5);
        var outside = new Point2(spot.X + rules.RadiusMeters + 2 * world.Terrain.CellSize, spot.Z);
        Assert.Equal(plain.At(outside.X, outside.Z), with.At(outside.X, outside.Z), 5);

        Assert.True(world.SellBuilding(bench.Footprint.MinX, bench.Footprint.MinZ));
        Assert.Null(world.BenchAccent);
    }

    [Fact]
    public void Place_CannotShutInTheDoorOfABuildingAlreadyThere()
    {
        var world = NewGame();
        var snack = Place(world, BuildingKind.SnackBar, ClubDoor(world));
        var door = snack.DoorCell;
        var ring = new[] { (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1), (0, 2), (-1, 2), (1, 2) }.Select(d => (X: door.X + d.Item1, Z: door.Z + d.Item2)).ToList();
        var results = new List<BuildingPlacement>();

        foreach (var (x, z) in ring.Concat(Enumerable.Range(-3, 7).SelectMany(dx => Enumerable.Range(0, 5).Select(dz => (X: door.X + dx, Z: door.Z + dz)))))
        {
            if (world.CheckBuilding(BuildingKind.Bench, x, z, DoorSide.South) is var r && r is BuildingPlacement.NoPath or BuildingPlacement.Placed)
            {
                results.Add(world.PlaceBuilding(BuildingKind.Bench, x, z, DoorSide.South));
            }
        }

        Assert.Contains(BuildingPlacement.NoPath, results);
        var field = world.Flow!.Get(world.Terrain!.Tee);
        Assert.True(field.IsReachable(door.X, door.Z), "la porte du snack-bar reste atteignable depuis le départ");
        Assert.True(field.IsReachable(world.Clubhouse!.DoorCell.X, world.Clubhouse.DoorCell.Z));
    }

    [Fact]
    public void Place_CannotShutInTheClubhouseDoor()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var moved = false;
        for (var z = 4; z < t.CellsZ - 14 && !moved; z += 2)
        {
            for (var x = 4; x < t.CellsX - 14 && !moved; x += 2)
            {
                var b = Clubhouse.At(x, z, DoorSide.South);
                var around = b.Footprint.Expand(3, t.CellsX, t.CellsZ);
                if (Enumerable.Range(around.MinZ, around.MaxZ - around.MinZ + 1).All(cz => Enumerable.Range(around.MinX, around.MaxX - around.MinX + 1).All(cx => t.GetCellSurface(cx, cz) == Surface.Rough)))
                {
                    moved = world.TryMoveClubhouse(new Point2((x + (Clubhouse.Width / 2.0)) * t.CellSize, (z + (Clubhouse.Depth / 2.0)) * t.CellSize), DoorSide.South);
                }
            }
        }

        Assert.True(moved, "un pavillon dans une clairière de rough");
        var door = world.Clubhouse!.DoorCell;
        var results = new List<BuildingPlacement>();
        for (var dz = -3; dz <= 3; dz++)
        {
            for (var dx = -3; dx <= 3; dx++)
            {
                if (world.CheckBuilding(BuildingKind.Bench, door.X + dx, door.Z + dz, DoorSide.South) is BuildingPlacement.NoPath or BuildingPlacement.Placed)
                {
                    results.Add(world.PlaceBuilding(BuildingKind.Bench, door.X + dx, door.Z + dz, DoorSide.South));
                }
            }
        }

        Assert.Contains(BuildingPlacement.NoPath, results);
        Assert.Contains(BuildingPlacement.Placed, results);
        Assert.True(world.Flow!.Get(t.Tee).IsReachable(door.X, door.Z), "la porte du pavillon reste atteignable depuis le départ");
    }

    private static World Played(int days)
    {
        var world = NewGame(BuildingParams.Default);
        EquipBoth(world);
        Place(world, BuildingKind.MaintenanceShed, ClubDoor(world), DoorSide.West);
        Place(world, BuildingKind.Bench, world.Terrain!.Tee);
        world.Step(days * Calendar.TicksPerDay);
        return world;
    }

    [Fact]
    public void SameSeedAndCommands_GiveTheSameChecksum_AfterTenDaysOfVisits()
    {
        var (a, b) = (Played(10), Played(10));

        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.Equal(4, a.PlacedBuildings.Count);
    }

    private static byte[] Save(World world)
    {
        var file = new MemoryStream();
        world.Save(file);
        return file.ToArray();
    }

    private static World Load(byte[] bytes) => World.Load(new MemoryStream(bytes), new StubShotResolver());

    /// <summary>La visite en cours et les genres déjà proposés se sauvent et entrent dans la somme de contrôle ; ce cas échoue si l'un des deux est oublié.</summary>
    [Fact]
    public void VisitState_IsSavedLoadedAndChecksummed()
    {
        var world = NewGame();
        EquipBoth(world);
        Golfer? walker = null;
        for (var i = 0; i < 6 * Calendar.TicksPerDay && walker is null; i++)
        {
            world.Step();
            walker = world.Groups.SelectMany(g => g.Golfers).FirstOrDefault(g => g.State == GolferState.Visiting);
        }

        Assert.NotNull(walker);
        Assert.NotEqual(0, walker.Offered);
        var loaded = Load(Save(world));
        var twin = loaded.Groups.SelectMany(g => g.Golfers).Single(g => g.Id == walker.Id);

        Assert.Equal((walker.State, walker.VisitKind, walker.VisitDoor, walker.Resume, walker.Offered), (twin.State, twin.VisitKind, twin.VisitDoor, twin.Resume, twin.Offered));
        Assert.Equal(world.Checksum(), loaded.Checksum());
        var before = world.Checksum();
        walker.Offered ^= 1 << (int)BuildingKind.Bench;
        Assert.NotEqual(before, world.Checksum());
        walker.Offered ^= 1 << (int)BuildingKind.Bench;
        walker.Resume = GolferState.ToNextTee;
        Assert.NotEqual(before, world.Checksum());
        walker.Resume = twin.Resume;
        walker.VisitKind = walker.VisitKind == BuildingKind.ProShop ? BuildingKind.SnackBar : BuildingKind.ProShop;
        Assert.NotEqual(before, world.Checksum());
        walker.VisitKind = twin.VisitKind;
        Assert.Equal(before, world.Checksum());

        world.Step(2000);
        loaded.Step(2000);
        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    /// <summary>Une vraie sauvegarde v17 produite par <c>main</c> (54bec42, graine 3, trois jours et demi, snack-bar, pro shop et banc posés) se charge : bâtiments, humeurs et compteurs intacts, aucune visite faite, et la partie continue.</summary>
    [Fact]
    public void Load_RealV17Save_KeepsBuildingsMoodsAndCounters_WithNoVisitYet()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var expected = File.ReadAllLines(Path.Combine(dir, "v17-buildings.txt"));
        var world = Load(File.ReadAllBytes(Path.Combine(dir, "v17-buildings.sav")));

        Assert.Equal($"tick {world.Tick}", expected[0]);
        Assert.Equal($"balance {world.Ledger.Balance}", expected[1]);
        Assert.Equal(expected.Where(l => l.StartsWith("building ")), world.PlacedBuildings.Select(b => $"building {(int)b.Kind} {b.Footprint.MinX} {b.Footprint.MinZ} {b.Footprint.MaxX} {b.Footprint.MaxZ} {(int)b.Side}"));
        var golfers = world.Groups.SelectMany(g => g.Golfers).Where(o => o.VisitorId >= 0).ToList();
        Assert.Equal(expected.Where(l => l.StartsWith("golfer ")), golfers.Select(o => $"golfer {o.Id} {o.VisitorId} {o.Mood}"));
        Assert.All(golfers, g => Assert.Equal(0, g.Offered));
        var counters = Enumerable.Range(1, 9).SelectMany(h => Enum.GetValues<MoodEvent>().Select(e => (h, e, c: world.Reactions.Count(h, e), p: world.Reactions.Previous(h, e)))).Where(x => x.c + x.p > 0);
        Assert.Equal(expected.Where(l => l.StartsWith("reaction ")), counters.Select(x => $"reaction {x.h} {(int)x.e} {x.c} {x.p}"));
        Assert.NotNull(world.BenchAccent);

        var again = Load(Save(world));
        Assert.Equal(world.Checksum(), again.Checksum());
        world.Step(3000);
        again.Step(3000);
        Assert.Equal(world.Checksum(), again.Checksum());
    }
}
