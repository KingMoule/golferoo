using System.Reflection;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Weather;

namespace Golferoo.Sim.Tests;

/// <summary>C30 : les correctifs du code review (bâtiments, peinture et arbres, drapeau, par et note, échéance de la ligue, sauvegarde et somme de contrôle).</summary>
public class CodeReviewTests
{
    private static Point2 Spot(World world) => new(world.Terrain!.Course[1].Tee.X + 30, world.Terrain.Course[1].Tee.Z + 30);

    /// <summary>C1 : le pavillon ne se déplace sur aucune position qui chevauche un snack-bar posé, et le refus ne coûte rien.</summary>
    [Fact]
    public void Clubhouse_NeverMovesOntoAPlacedBuilding()
    {
        var world = new World(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, placeClubhouse: true);
        var t = world.Terrain!;
        var snack = Enumerable.Range(0, t.CellsZ).SelectMany(z => Enumerable.Range(0, t.CellsX).Select(x => (x, z)))
            .First(c => world.CheckBuilding(BuildingKind.SnackBar, c.x, c.z, DoorSide.South) == BuildingPlacement.Placed);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, snack.x, snack.z, DoorSide.South));
        var placed = Assert.Single(world.PlacedBuildings).Footprint;
        var balance = world.Ledger.Balance;

        for (var z = placed.MinZ - Clubhouse.Depth; z <= placed.MaxZ; z++)
        {
            for (var x = placed.MinX - Clubhouse.Width; x <= placed.MaxX; x++)
            {
                var b = Clubhouse.At(x, z, DoorSide.South);
                if (b.Footprint.MaxX >= placed.MinX && b.Footprint.MinX <= placed.MaxX && b.Footprint.MaxZ >= placed.MinZ && b.Footprint.MinZ <= placed.MaxZ)
                {
                    Assert.False(world.CanPlaceClubhouse(b), $"pavillon en ({x}, {z}) sur le snack-bar");
                }
            }
        }

        var center = new Point2((placed.MinX + placed.MaxX + 1) * t.CellSize / 2, (placed.MinZ + placed.MaxZ + 1) * t.CellSize / 2);
        Assert.False(world.TryMoveClubhouse(center, DoorSide.South));
        Assert.Equal(balance, world.Ledger.Balance);
    }

    /// <summary>H2 : peindre de l'eau sous un arbre l'abat au prix annoncé ; l'annulation rend l'arbre (même identifiant) et l'argent.</summary>
    [Fact]
    public void PaintingWaterOverATree_ChargesItsRemoval_AndUndoGivesBothBack()
    {
        var world = new World(2, CourseTests.ThreeHoles());
        var at = Spot(world);
        world.PaintSurface(at, 6, Surface.Rough);
        world.Step();
        world.PlantTree(at.X, at.Z, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.Step();
        var tree = world.Trees.Trees.Single(x => Math.Abs(x.X - at.X) < 1 && Math.Abs(x.Z - at.Z) < 1);
        var (balance, count, spent) = (world.Ledger.Balance, world.Trees.Count, world.Ledger.Current(LedgerCategory.Trees));
        var quote = world.QuotePaint(at, 3, Surface.Water);
        Assert.True(quote > world.Economy.RazeCost(tree), "le devis compte l'abattage");

        world.BeginStroke();
        world.PaintSurface(at, 3, Surface.Water);
        world.Step();
        Assert.False(world.Trees.TryGet(tree.Id, out _));
        Assert.Equal(balance - quote, world.Ledger.Balance);
        Assert.Equal(spent - world.Economy.RazeCost(tree), world.Ledger.Current(LedgerCategory.Trees));

        world.UndoStroke();
        world.Step();
        Assert.True(world.Trees.TryGet(tree.Id, out _));
        Assert.Equal(count, world.Trees.Count);
        Assert.Equal(balance, world.Ledger.Balance);
    }

    /// <summary>H2 : le bulldozer abat même un arbre sur du rough, au prix du devis, et s'annule comme une peinture.</summary>
    [Fact]
    public void Bulldozer_RemovesTreesOnRough_AtTheQuotedPrice_AndIsUndoable()
    {
        var world = new World(2, CourseTests.ThreeHoles());
        var at = Spot(world);
        world.PaintSurface(at, 6, Surface.Rough);
        world.Step();
        world.PlantTree(at.X, at.Z, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.Step();
        var (balance, count, sum) = (world.Ledger.Balance, world.Trees.Count, world.Terrain!.Checksum());
        var quote = world.QuoteBulldoze(at, 3);

        world.BeginStroke();
        world.Bulldoze(at, 3);
        world.Step();
        Assert.Equal(count - 1, world.Trees.Count);
        Assert.Equal(balance - quote, world.Ledger.Balance);
        Assert.True(world.CanUndoStroke);

        world.UndoStroke();
        world.Step();
        Assert.Equal(count, world.Trees.Count);
        Assert.Equal(balance, world.Ledger.Balance);
        Assert.Equal(sum, world.Terrain.Checksum());
    }

    /// <summary>M1 : le drapeau du jour change de place sur chaque trou du parcours, pas seulement sur le premier.</summary>
    [Fact]
    public void DailyPin_MovesOnEveryHole()
    {
        var world = new World(1, ClubhouseTests.LeNeuf());
        var holes = world.Terrain!.Course.Holes;
        var first = holes.Select(h => h.Hole).ToArray();
        var moved = new bool[holes.Count];
        for (var day = 0; day < 4; day++)
        {
            world.Step(Calendar.TicksPerDay);
            for (var i = 0; i < holes.Count; i++)
            {
                moved[i] |= holes[i].Hole != first[i];
            }
        }

        Assert.All(moved, Assert.True);
    }

    /// <summary>M4 : déplacer un départ ou un drapeau invalide la note ; sur une terre du joueur, le par suit la nouvelle longueur.</summary>
    [Fact]
    public void MovingATeeOrPin_DropsTheRating_AndRededucesTheParOnABlankProperty()
    {
        var terrain = CourseTests.ThreeHoles();
        terrain.Blank = true;
        var world = new World(1, terrain);
        world.SetCourseRating(new Golferoo.Sim.Rating.CourseRating(3, 12, 80.0, 100.0));
        world.Step();
        Assert.NotNull(world.CourseRating);

        var far = terrain.Course[3].Hole;
        Assert.True(world.PlaceHole(1, far));
        world.Step();

        Assert.Null(world.CourseRating);
        Assert.Equal(TerrainGrid.DeducedPar(terrain.Course[1].Tee, far), terrain.Course[1].Par);

        world.SetCourseRating(new Golferoo.Sim.Rating.CourseRating(3, 12, 80.0, 100.0));
        world.Step();
        Assert.True(world.PlaceTee(2, terrain.Course[2].Tee));
        world.Step();
        Assert.Null(world.CourseRating);
    }

    /// <summary>M7 : un trou ajouté en cours de saison périme la note ; si aucune note n'arrive, la journée de ligue se clôt quand même, cartes sans écart.</summary>
    [Fact]
    public void LeagueRound_Closes_EvenIfTheRatingNeverComes()
    {
        var world = new World(3, CourseTests.ThreeHoles(), league: true) { Climate = Climate.Calm };
        Assert.True(world.AddHole(world.Terrain!.Course[2].Tee, world.Terrain.Course[2].Hole));
        world.Step();
        Assert.Null(world.CourseRating);

        while (!world.League!.Rounds[0].Closed && world.DayIndex < 40)
        {
            world.Step(Calendar.TicksPerDay / 4);
        }

        Assert.True(world.League.Rounds[0].Closed, "la journée attend une note qui ne vient pas");
    }

    /// <summary>H1 : sur une longue partie de ligue, les groupes finis quittent la liste et le pro reste disponible pour un tour d'entraînement.</summary>
    [Fact]
    public void LongLeagueGame_KeepsGroupsBounded_AndTheProAvailable()
    {
        var world = new World(11, HoleLoader.Load(TerrainTests.HolePath(), 2), league: true) { Climate = Climate.Calm };
        var most = 0;
        for (var day = 0; day < 60; day++)
        {
            world.Step(Calendar.TicksPerDay);
            most = Math.Max(most, world.Groups.Count);
        }

        Assert.True(most <= world.League!.Members.Count, $"{most} groupes en mémoire");
        for (var i = 0; i < 10 * Calendar.TicksPerDay && !world.CanStartProRound; i += 600)
        {
            world.Step(600);
        }

        Assert.True(world.CanStartProRound);
        world.StartProRound();
    }

    /// <summary>
    /// Des commandes de toutes sortes, et toutes les 10 commandes une sauvegarde relue : le monde relu a la même somme et les mêmes octets,
    /// et la partie continue sur le monde relu. Attrape un état oublié par <c>Save</c> ou par <c>Checksum</c>.
    /// </summary>
    [Fact]
    public void RandomCommands_SavedAndReloadedEveryTenSteps_KeepTheSameWorld()
    {
        var world = new World(4, CourseTests.ThreeHoles(), league: true);
        var rng = new Random(4);
        var tee = world.Terrain!.Course[1].Tee;
        Surface[] surfaces = [Surface.Fairway, Surface.Rough, Surface.Bunker, Surface.Trees, Surface.Water];
        for (var step = 0; step < 300; step++)
        {
            var at = new Point2(tee.X + rng.Next(-60, 60), tee.Z + rng.Next(10, 120));
            switch (rng.Next(9))
            {
                case 0: world.BeginStroke(); break;
                case 1: world.PaintSurface(at, rng.Next(2, 8), surfaces[rng.Next(surfaces.Length)]); break;
                case 2: world.RaiseTerrain(at, rng.Next(4, 10), rng.NextDouble() - 0.5); break;
                case 3: world.Bulldoze(at, rng.Next(2, 6)); break;
                case 4: world.UndoStroke(); break;
                case 5: world.PlantTree(at.X, at.Z, TreeSpeciesId.Oak, TreePlantStage.Sapling); break;
                case 6: world.SetFastForward(rng.Next(2) == 0); break;
                case 7: world.SpawnGroup(); break;
                default: break;
            }

            world.Step(rng.Next(1, 200));
            if (step % 10 == 9)
            {
                var bytes = Bytes(world);
                var loaded = World.Load(new MemoryStream(bytes));
                Assert.Equal(world.Checksum(), loaded.Checksum());
                Assert.Equal(bytes, Bytes(loaded));
                world = loaded;
            }
        }
    }

    private static byte[] Bytes(World world)
    {
        using var stream = new MemoryStream();
        world.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Fil d'alarme de la règle « tout état de la sim entre dans Checksum et Save » : un nouveau champ de <see cref="World"/> fait échouer ce test
    /// tant qu'on n'a pas décidé s'il est un état (mêlé et sauvé, avec un cas de test) ou un cache ou un réglage (listé ici avec sa raison).
    /// </summary>
    [Fact]
    public void EveryWorldField_IsAKnownStateOrCache()
    {
        string[] notState =
        [
            "_resolver", // résolveur de coups : choisi par l'appelant, sans état propre
            "_beautyMap", "_exitPoints", "_exitPointVersion", "BenchAccent", // caches recalculés
            "_proMemberId", // déduit de la ligue au chargement
            "Arrivals", "Moods", "Memberships", "BuildingRules", "Difficulty", // paramètres des données, pas sauvés (handoff, « Reporté »)
        ];
        var known = KnownState.Concat(notState).ToHashSet();
        var fields = typeof(World).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType)) // événements
            .Select(f => f.Name.StartsWith('<') ? f.Name[1..f.Name.IndexOf('>')] : f.Name)
            .ToList();

        Assert.True(!fields.Any(f => !known.Contains(f)), string.Join(", ", fields.Where(f => !known.Contains(f)).Order()));
    }

    /// <summary>Les champs de <see cref="World"/> mêlés à la somme de contrôle et sauvés (relevés en C30).</summary>
    private static readonly string[] KnownState =
    [
        "Seed", "Tick", "_climate", "_dayOffset", "_skyOverride", "_skyOverrideDay", "_rainyDays", "_rng", "_arrivalRng", "Terrain", "Trees", "Flow",
        "_woodlandDraws", "_nextGolferId", "_nextGroupId", "_waypoints", "_beautySources", "_groups", "_roamers", "_placed", "Clubhouse",
        "_edits", "_strokeSurfaces", "_strokeHeights", "_strokeTrees", "_strokeSurfacesCost", "_strokeReliefCost", "_strokeTreesCost",
        "_treeCommands", "TreeCommandsRefused", "TreesPlanted", "_holeCommands", "_courseCommands", "HoleCommandsRefused", "_holeCheckedVersion", "_holesOpen",
        "_ratingStale", "_heldCards", "_ratingCommands", "CourseRating", "_turfCommands", "_joinCommands", "_proShots", "_manualPro", "Pro", "Progression",
        "League", "Ledger", "Economy", "State", "Reactions", "Visitors", "Reputation", "FastForward", "_fastForwardCommand",
    ];
}
