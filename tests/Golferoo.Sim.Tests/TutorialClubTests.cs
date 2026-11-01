using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Objectives;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Tutorial;

namespace Golferoo.Sim.Tests;

/// <summary>C19 lot 1 : la suite du tutoriel, du trou valide au snack-bar offert (club activé, étapes, pensée lue, bon de pose).</summary>
public class TutorialClubTests
{
    /// <summary>La leçon jusqu'au trou valide, le monde de la leçon (terre vierge, pas de ligue).</summary>
    internal static World OpenLesson()
    {
        var world = TutorialTests.BlankWorld();
        TutorialTests.Raze(world);
        TutorialTests.Plant(world);
        TutorialTests.PaintTeeAndFairway(world);
        TutorialTests.PaintGreen(world);
        Assert.True(world.PlaceTee(1, TutorialTests.TeeSpot));
        Assert.True(world.PlaceHole(1, TutorialTests.GreenSpot));
        world.Step();
        return world;
    }

    private static bool HasFee(World w) => w.Ledger.Current(LedgerCategory.GreenFees) > 0 || w.Ledger.Months.Any(m => m.Total(LedgerCategory.GreenFees) > 0);

    private static Golfer? ThoughtfulGolfer(World w) => w.Groups.SelectMany(g => g.Golfers).FirstOrDefault(w.CarriesThought);

    /// <summary>Un angle libre où le snack-bar se pose (le premier par lignes).</summary>
    private static (int X, int Z) Spot(World w)
    {
        // Le terrain de la leçon est le même d'un monde à l'autre : on cherche sur une sonde qui tient un bon (sans bon, la pose est refusée partout).
        var probe = OpenLesson();
        probe.State.AddVoucher(BuildingKind.SnackBar);
        var t = w.Terrain!;
        for (var z = 0; z < t.CellsZ; z++)
        {
            for (var x = 0; x < t.CellsX; x++)
            {
                if (probe.CheckBuilding(BuildingKind.SnackBar, x, z, DoorSide.South) == BuildingPlacement.Placed)
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException("Aucun emplacement pour le snack-bar.");
    }

    /// <summary>Joue la leçon jusqu'à ce que <paramref name="stop"/> soit vrai (au plus <paramref name="limit"/> ticks).</summary>
    private static void StepUntil(World w, Func<bool> stop, int limit = 400_000)
    {
        for (var i = 0; i < limit && !stop(); i++)
        {
            w.Step();
        }

        Assert.True(stop(), "l'évènement attendu n'est pas arrivé");
    }

    // ---- Critère 1 : ClubActive ----

    [Fact]
    public void ClubActive_IsFalseOnTheBlankLandWhileTheHoleIsClosed_AndTrueElsewhere()
    {
        var lesson = TutorialTests.BlankWorld();
        lesson.Step(20_000);

        Assert.False(lesson.State.ClubActive);
        Assert.Null(lesson.Clubhouse);
        Assert.Empty(lesson.Groups);
        Assert.Equal(0, lesson.Visitors.Next);
        Assert.True(new World(1, ClubhouseTests.LeNeuf()).State.ClubActive);
        Assert.True(new World(1, null).State.ClubActive);
    }

    [Fact]
    public void ClubActive_TurnsOnAtTheFirstOpening_PlacesTheClubhouseOnce_AndSurvivesUndoingTheGreen()
    {
        var world = OpenLesson();

        Assert.True(world.HoleOpen);
        Assert.True(world.State.ClubActive);
        var club = world.Clubhouse;
        Assert.NotNull(club);

        world.PaintSurface(TutorialTests.GreenSpot, 15, Surface.Rough);
        world.Step();
        Assert.False(world.HoleOpen);
        Assert.True(world.State.ClubActive);
        Assert.Same(club, world.Clubhouse);

        TutorialTests.PaintGreen(world);
        world.PlaceHole(1, TutorialTests.GreenSpot);
        world.Step();
        Assert.True(world.HoleOpen);
        Assert.Same(club, world.Clubhouse);
    }

    [Fact]
    public void ClubActive_LetsVisitorsArrive_OnlyOnceTheHoleIsOpen()
    {
        var world = OpenLesson();
        StepUntil(world, () => world.Visitors.Next > 0);

        Assert.True(world.Groups.Count > 0 || world.QueueLength > 0 || world.Visitors.Next > 0);
        Assert.All(world.Groups, g => Assert.True(g.FromArrivals));
    }

    [Fact]
    public void Lesson_FirstGroupArrivesWithinTwoDays()
    {
        var world = OpenLesson();
        StepUntil(world, () => world.Visitors.Next > 0, 2 * Golferoo.Sim.Time.Calendar.TicksPerDay);

        Assert.True(world.Visitors.Next > 0);
    }

    // ---- Critère 2 : les étapes ----

    [Fact]
    public void Steps_FollowTheirEvents_InOrder_ToTheSnackBar()
    {
        var world = OpenLesson();
        Assert.Equal(TutorialStep.OpenHole, TutorialSteps.Current(world));

        var seen = new List<TutorialStep> { TutorialStep.OpenHole };
        void Watch()
        {
            var step = TutorialSteps.Current(world);
            if (step != seen[^1])
            {
                seen.Add(step);
            }
        }

        // Une première arrivée sort de l'accueil ; tant qu'aucun green fee n'est tombé, on ne passe pas la caisse.
        StepUntil(world, () => { Watch(); return world.Visitors.Next > 0; });
        Assert.NotEqual(TutorialStep.OpenHole, TutorialSteps.Current(world));
        Assert.False(HasFee(world));

        StepUntil(world, () => { Watch(); return HasFee(world); });
        Assert.Equal(TutorialStep.ReadThought, TutorialSteps.Current(world));
        Assert.Equal(1, world.State.Vouchers(BuildingKind.SnackBar));
        Assert.Contains(TutorialStep.FirstGreenFee, seen);

        // La pensée lue, le bon à poser ; le bon posé, c'est fini.
        StepUntil(world, () => ThoughtfulGolfer(world) is not null);
        Assert.True(world.ReadThought(ThoughtfulGolfer(world)!.Id));
        Assert.Equal(TutorialStep.PlaceSnackBar, TutorialSteps.Current(world));

        var (x, z) = Spot(world);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.Equal(TutorialStep.Done, TutorialSteps.Current(world));
        Watch();

        Assert.Equal(seen.OrderBy(s => (int)s).ToList(), seen);
        Assert.Equal(TutorialStep.OpenHole, seen[0]);
        Assert.Equal(TutorialStep.WatchPair, seen[1]);
    }

    [Fact]
    public void Steps_UndoingTheGreenAfterTheOpening_BringsBackTheGreenStep()
    {
        var world = OpenLesson();
        world.PaintSurface(TutorialTests.GreenSpot, 15, Surface.Rough);
        world.Step();

        Assert.Equal(TutorialStep.PaintGreen, TutorialSteps.Current(world));
    }

    // ---- Critère 3 : ThoughtRead ----

    [Fact]
    public void ReadThought_OnlyCountsOnAGolferWithAThought()
    {
        var world = OpenLesson();
        StepUntil(world, () => world.Groups.Count > 0);
        var fresh = world.Groups.SelectMany(g => g.Golfers).First(g => !world.CarriesThought(g));

        Assert.False(world.ReadThought(fresh.Id));
        Assert.False(world.ReadThought(-5));
        Assert.False(world.State.ThoughtRead);

        StepUntil(world, () => ThoughtfulGolfer(world) is not null);
        Assert.False(world.State.ThoughtRead);
        Assert.True(world.ReadThought(ThoughtfulGolfer(world)!.Id));
        Assert.True(world.State.ThoughtRead);
    }

    // ---- Critère 4 : le bon du tutoriel ----

    /// <summary>Mesure de C19 lot 1 : un bon de pose ne levait PAS le seuil de déblocage (CheckBuilding rendait Locked). La règle est écrite ici : un bon lève le seuil, pour son seul genre.</summary>
    [Fact]
    public void Voucher_LiftsTheUnlockThreshold_ForItsKindOnly()
    {
        var world = OpenLesson();
        Assert.Equal(1, world.OpenHoles);
        var (x, z) = Spot(world);

        Assert.Equal(BuildingPlacement.Locked, world.CheckBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        world.State.AddVoucher(BuildingKind.SnackBar);
        Assert.NotEqual(BuildingPlacement.Locked, world.CheckBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.True(world.IsUnlocked(BuildingKind.SnackBar));
        Assert.False(world.IsUnlocked(BuildingKind.ProShop));
    }

    [Fact]
    public void TutorialVoucher_PlacesTheSnackBarWithOneOpenHole_OnceAndWithoutChargingIt()
    {
        var world = OpenLesson();
        StepUntil(world, () => HasFee(world));
        Assert.Equal(1, world.State.Vouchers(BuildingKind.SnackBar));
        var (x, z) = Spot(world);

        var before = world.Ledger.Balance;
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.True(before - world.Ledger.Balance < world.BuildingCost(BuildingKind.SnackBar)!.Value);
        Assert.Equal(0, world.State.Vouchers(BuildingKind.SnackBar));

        var (x2, z2) = (x + 20, z);
        Assert.Equal(BuildingPlacement.Locked, world.PlaceBuilding(BuildingKind.SnackBar, x2, z2, DoorSide.South));
        Assert.Single(world.PlacedBuildings);
    }

    [Fact]
    public void TutorialVoucher_IsGivenOnlyOnce_AndNotInARealGame()
    {
        var world = OpenLesson();
        StepUntil(world, () => HasFee(world));
        world.Step(30_000);
        Assert.Equal(1, world.State.Vouchers(BuildingKind.SnackBar));

        var career = new World(3, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, placeClubhouse: true);
        career.Step(30_000);
        Assert.Equal(0, career.State.Vouchers(BuildingKind.SnackBar));
    }

    // ---- Critère 6 (partie) : sauvegarde ----

    [Fact]
    public void Lesson_RoundTripsThroughASave_WithTheSameChecksum()
    {
        var closed = TutorialTests.BlankWorld();
        Assert.Equal(closed.Checksum(), Reload(closed).Checksum());

        var world = OpenLesson();
        StepUntil(world, () => HasFee(world));
        world.Step(500);
        var loaded = Reload(world);

        Assert.True(loaded.State.ClubActive);
        Assert.Equal(world.Checksum(), loaded.Checksum());
        world.Step(2000);
        loaded.Step(2000);
        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void ClubActive_AndThoughtRead_ChangeTheChecksumInALesson()
    {
        var open = OpenLesson();
        var closed = TutorialTests.BlankWorld();
        Assert.NotEqual(closed.Checksum(), open.Checksum());

        var before = open.Checksum();
        open.State.ThoughtRead = true;
        Assert.NotEqual(before, open.Checksum());
        Assert.True(Reload(open).State.ThoughtRead);
    }

    private static World Reload(World world)
    {
        using var stream = new MemoryStream();
        world.Save(stream);
        return World.Load(new MemoryStream(stream.ToArray()));
    }

    // ---- Critère 7 : déterminisme ----

    [Fact]
    public void Lesson_IsDeterministic_OverTenDays()
    {
        static ulong Play()
        {
            var world = OpenLesson();
            world.Step(10 * Time.Calendar.TicksPerDay);
            return world.Checksum();
        }

        Assert.Equal(Play(), Play());
    }
}
