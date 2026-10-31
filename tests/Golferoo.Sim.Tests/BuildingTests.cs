using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>C17 lot 1 : poser et vendre snack-bar, pro shop, hangar et banc (déblocage, surfaces, emprises, défrichage payé, grand livre, sauvegarde v17).</summary>
public class BuildingTests
{
    private static World NewGame(EconomyParams? economy = null, BuildingParams? rules = null) =>
        new(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, economy: economy, placeClubhouse: true, buildings: rules);

    /// <summary>Première cellule d'angle (par lignes) où la commande est acceptée pour ce genre, hors de la zone <paramref name="skip"/>.</summary>
    private static (int X, int Z) FreeSpot(World world, BuildingKind kind, DoorSide side = DoorSide.South, int fromX = 0)
    {
        var t = world.Terrain!;
        for (var z = 0; z < t.CellsZ; z++)
        {
            for (var x = fromX; x < t.CellsX; x++)
            {
                if (world.CheckBuilding(kind, x, z, side) == BuildingPlacement.Placed && !world.Trees.Trees.Any(tree => Inside(world, kind, x, z, side, tree)))
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException($"Aucun emplacement libre pour {kind}.");
    }

    private static bool Inside(World world, BuildingKind kind, int x, int z, DoorSide side, Tree tree) =>
        world.BuildingRules.At(kind, x, z, side)!.Footprint.Contains(world.Terrain!.CellXOf(tree.X), world.Terrain.CellZOf(tree.Z));

    /// <summary>Un angle où l'emprise du genre contient au moins une cellule de <paramref name="surface"/> (et reste dans la carte).</summary>
    private static (int X, int Z) SpotOn(World world, BuildingKind kind, Surface surface)
    {
        var t = world.Terrain!;
        for (var z = 1; z < t.CellsZ - 8; z++)
        {
            for (var x = 1; x < t.CellsX - 8; x++)
            {
                var rect = world.BuildingRules.At(kind, x, z, DoorSide.South)!.Footprint;
                if (t.GetCellSurface((rect.MinX + rect.MaxX) / 2, (rect.MinZ + rect.MaxZ) / 2) == surface)
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException($"Aucune cellule de {surface}.");
    }

    [Fact]
    public void Rules_ComeFromData_AndAQuarterTurnSwapsTheFootprintAndMovesTheDoor()
    {
        var rules = BuildingParams.Default;
        Assert.Equal(2_000_000, rules.SnackBar.Cost);
        Assert.Equal(1, rules.Bench.Width * rules.Bench.Depth);
        var south = rules.At(BuildingKind.ProShop, 10, 10, DoorSide.South)!;
        var east = rules.At(BuildingKind.ProShop, 10, 10, DoorSide.East)!;
        Assert.Equal((8, 6), (south.Footprint.MaxX - south.Footprint.MinX + 1, south.Footprint.MaxZ - south.Footprint.MinZ + 1));
        Assert.Equal((6, 8), (east.Footprint.MaxX - east.Footprint.MinX + 1, east.Footprint.MaxZ - east.Footprint.MinZ + 1));
        Assert.Equal((13, 16), south.DoorCell);
        Assert.Equal((16, 13), east.DoorCell);
        Assert.Null(rules.At(BuildingKind.Clubhouse, 0, 0, DoorSide.South));
        Assert.Throws<InvalidDataException>(() => BuildingParams.Parse("""{ "snackBar": { "width": 6, "depth": 5, "cost": 1, "unlockHoles": 1 } }"""));
    }

    [Fact]
    public void Place_OnRough_IsAccepted_ChargesTheLedgerAndBlocksTheCells()
    {
        var world = NewGame();
        var (x, z) = FreeSpot(world, BuildingKind.SnackBar);
        var before = world.Ledger.Balance;

        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));

        var placed = Assert.Single(world.PlacedBuildings);
        Assert.Equal(BuildingKind.SnackBar, placed.Kind);
        Assert.Equal(before - world.BuildingRules.SnackBar.Cost, world.Ledger.Balance);
        Assert.Equal(-world.BuildingRules.SnackBar.Cost, world.Ledger.Current(LedgerCategory.Buildings));
        var field = world.Flow!.Get(placed.DoorPosition(world.Terrain!.CellSize));
        Assert.False(field.IsReachable(placed.Footprint.MinX, placed.Footprint.MinZ), "l'emprise bloque le passage");
        Assert.True(field.IsReachable(placed.DoorCell.X, placed.DoorCell.Z));
    }

    [Theory]
    [InlineData(Surface.Fairway)]
    [InlineData(Surface.Green)]
    [InlineData(Surface.Tee)]
    [InlineData(Surface.Bunker)]
    [InlineData(Surface.Water)]
    public void Place_OnAPlayingSurface_IsRefused_WithoutDebit(Surface surface)
    {
        var world = NewGame();
        var (x, z) = SpotOn(world, BuildingKind.SnackBar, surface);
        var before = world.Ledger.Balance;

        Assert.Equal(BuildingPlacement.BadSurface, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));

        Assert.Empty(world.PlacedBuildings);
        Assert.Equal(before, world.Ledger.Balance);
    }

    [Fact]
    public void Place_OnTheClubhouse_OrOnAnotherBuilding_OrOnItsDoor_IsRefused()
    {
        var world = NewGame();
        var club = world.Clubhouse!.Footprint;
        Assert.Equal(BuildingPlacement.Occupied, world.PlaceBuilding(BuildingKind.Bench, club.MinX + 2, club.MinZ + 2, DoorSide.South));

        var (x, z) = FreeSpot(world, BuildingKind.SnackBar);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        var balance = world.Ledger.Balance;
        Assert.Equal(BuildingPlacement.Occupied, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.Equal(BuildingPlacement.Occupied, world.PlaceBuilding(BuildingKind.Bench, x + 1, z + 1, DoorSide.South));
        var door = world.PlacedBuildings[0].DoorCell;
        Assert.Equal(BuildingPlacement.Occupied, world.PlaceBuilding(BuildingKind.Bench, door.X, door.Z, DoorSide.South));
        Assert.Equal(balance, world.Ledger.Balance);
        Assert.Single(world.PlacedBuildings);
    }

    [Fact]
    public void Place_ManyOfTheSameKind_IsAllowed()
    {
        var world = NewGame();
        var first = FreeSpot(world, BuildingKind.SnackBar);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, first.X, first.Z, DoorSide.South));
        var second = FreeSpot(world, BuildingKind.SnackBar, DoorSide.West);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, second.X, second.Z, DoorSide.West));

        Assert.Equal(2, world.PlacedBuildings.Count(b => b.Kind == BuildingKind.SnackBar));
    }

    [Fact]
    public void Place_OutsideTheMap_IsRefused()
    {
        var world = NewGame();
        Assert.Equal(BuildingPlacement.OutOfMap, world.PlaceBuilding(BuildingKind.Bench, -1, 5, DoorSide.South));
        Assert.Equal(BuildingPlacement.OutOfMap, world.PlaceBuilding(BuildingKind.SnackBar, world.Terrain!.CellsX - 2, 5, DoorSide.South));
    }

    [Fact]
    public void Place_ALockedKind_IsRefused_ThenAccepted_OnceEnoughHolesAreOpen()
    {
        var locked = BuildingParams.Default with { SnackBar = BuildingParams.Default.SnackBar with { UnlockHoles = 10 } };
        var world = NewGame(rules: locked);
        Assert.Equal(9, world.OpenHoles);
        var (x, z) = FreeSpot(NewGame(), BuildingKind.SnackBar);
        var before = world.Ledger.Balance;

        Assert.False(world.IsUnlocked(BuildingKind.SnackBar));
        Assert.Equal(BuildingPlacement.Locked, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.Empty(world.PlacedBuildings);
        Assert.Equal(before, world.Ledger.Balance);

        Assert.True(world.IsUnlocked(BuildingKind.ProShop));
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.ProShop, x, z, DoorSide.South));
    }

    [Fact]
    public void Place_WithAnInsufficientBalance_FollowsTheOverdraftRules()
    {
        var cost = BuildingParams.Default.SnackBar.Cost;
        var broke = NewGame(EconomyParams.Facile with { StartingMoney = cost - 1, OverdraftLimit = 0 });
        var (x, z) = FreeSpot(broke, BuildingKind.SnackBar);
        Assert.Equal(BuildingPlacement.Funds, broke.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.Empty(broke.PlacedBuildings);
        Assert.Equal(cost - 1, broke.Ledger.Balance);

        var overdraft = NewGame(EconomyParams.Facile with { StartingMoney = cost - 1, OverdraftLimit = 1_000_000 });
        Assert.Equal(BuildingPlacement.Placed, overdraft.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        Assert.Equal(-1, overdraft.Ledger.Balance);
    }

    [Fact]
    public void Place_OverTrees_CutsThemAndBillsTheClearing()
    {
        var world = NewGame();
        var (x, z) = FreeSpot(world, BuildingKind.SnackBar);
        var t = world.Terrain!;
        var tree = world.Trees.Add((x + 2.5) * t.CellSize, (z + 2.5) * t.CellSize, TreeSpeciesId.Oak, 50 * 12);
        var fallen = world.Trees.Add((x + 4.5) * t.CellSize, (z + 1.5) * t.CellSize, TreeSpeciesId.Oak, 50 * 12);
        world.Trees.SetState(fallen.Id, TreeState.Fallen, 0f);
        var expected = world.BuildingRules.SnackBar.Cost + world.Economy.RazeCost(tree) + world.Economy.TreeClearFallen;
        Assert.Equal(BuildingPlacement.Placed, world.CheckBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));

        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));

        Assert.False(world.Trees.TryGet(tree.Id, out _));
        Assert.False(world.Trees.TryGet(fallen.Id, out _));
        Assert.Equal(-expected, world.Ledger.Current(LedgerCategory.Buildings));
    }

    [Fact]
    public void Place_OverUndergrowth_ClearsItAtTheClearingRate()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var spot = default((int X, int Z)?);
        for (var z = 1; z < t.CellsZ - 8 && spot is null; z++)
        {
            for (var x = 1; x < t.CellsX - 8 && spot is null; x++)
            {
                if (t.GetCellSurface(x, z) == Surface.Trees && world.CheckBuilding(BuildingKind.Bench, x, z, DoorSide.South) == BuildingPlacement.Placed)
                {
                    spot = (x, z);
                }
            }
        }

        var (cx, cz) = spot ?? throw new InvalidOperationException("Aucun sous-bois.");
        var trees = world.Trees.Trees.Where(tree => t.CellXOf(tree.X) == cx && t.CellZOf(tree.Z) == cz).ToList();
        var expected = world.BuildingRules.Bench.Cost + trees.Sum(tree => tree.State == TreeState.Standing ? world.Economy.RazeCost(tree) : tree.State == TreeState.Fallen ? world.Economy.TreeClearFallen : 0)
            + (long)Math.Round(t.CellSize * t.CellSize * world.Economy.TreeClearPerM2);

        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.Bench, cx, cz, DoorSide.South));

        Assert.Equal(-expected, world.Ledger.Current(LedgerCategory.Buildings));
        Assert.Equal(Surface.Rough, t.GetCellSurface(cx, cz));
    }

    [Fact]
    public void Plant_OnABuildingFootprint_IsRefused()
    {
        var world = NewGame();
        var (x, z) = FreeSpot(world, BuildingKind.Bench);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.Bench, x, z, DoorSide.South));
        var t = world.Terrain!;
        var count = world.Trees.Trees.Count;
        world.PlantTree((x + 0.5) * t.CellSize, (z + 0.5) * t.CellSize, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.Step(1);
        Assert.Equal(count, world.Trees.Trees.Count);
    }

    [Fact]
    public void Sell_RefundsHalf_FreesTheFootprintAndTheWay()
    {
        var world = NewGame();
        var (x, z) = FreeSpot(world, BuildingKind.ProShop);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.ProShop, x, z, DoorSide.South));
        var balance = world.Ledger.Balance;
        var placed = world.PlacedBuildings[0];

        Assert.True(world.SellBuilding(placed.Footprint.MaxX, placed.Footprint.MaxZ));

        Assert.Empty(world.PlacedBuildings);
        Assert.Equal(balance + (world.BuildingRules.ProShop.Cost / 2), world.Ledger.Balance);
        Assert.Equal(-(world.BuildingRules.ProShop.Cost - (world.BuildingRules.ProShop.Cost / 2)), world.Ledger.Current(LedgerCategory.Buildings));
        var field = world.Flow!.Get(world.Clubhouse!.DoorPosition(world.Terrain!.CellSize));
        Assert.True(field.IsReachable(placed.Footprint.MinX, placed.Footprint.MinZ), "le passage est rendu");
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.ProShop, x, z, DoorSide.South));
    }

    [Fact]
    public void Sell_NothingThere_OrTheClubhouse_IsRefused()
    {
        var world = NewGame();
        var balance = world.Ledger.Balance;
        var club = world.Clubhouse!.Footprint;

        Assert.False(world.SellBuilding(club.MinX + 1, club.MinZ + 1));
        Assert.False(world.SellBuilding(0, 0));
        Assert.NotNull(world.Clubhouse);
        Assert.Equal(balance, world.Ledger.Balance);
    }

    [Fact]
    public void Paint_OnABuildingFootprint_IsIgnored()
    {
        var world = NewGame();
        var (x, z) = FreeSpot(world, BuildingKind.SnackBar);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.SnackBar, x, z, DoorSide.South));
        var t = world.Terrain!;
        var balance = world.Ledger.Balance;

        world.PaintSurface(new Point2((x + 2) * t.CellSize, (z + 2) * t.CellSize), t.CellSize, Surface.Fairway);
        world.Step(1);

        Assert.Equal(balance, world.Ledger.Balance);
        Assert.NotEqual(Surface.Fairway, t.GetCellSurface(x + 2, z + 2));
    }

    private static World PlayedTenDays()
    {
        var world = NewGame();
        var first = FreeSpot(world, BuildingKind.SnackBar);
        world.PlaceBuilding(BuildingKind.SnackBar, first.X, first.Z, DoorSide.South);
        var second = FreeSpot(world, BuildingKind.ProShop, DoorSide.East);
        world.PlaceBuilding(BuildingKind.ProShop, second.X, second.Z, DoorSide.East);
        var third = FreeSpot(world, BuildingKind.Bench, DoorSide.North);
        world.PlaceBuilding(BuildingKind.Bench, third.X, third.Z, DoorSide.North);
        world.SellBuilding(first.X, first.Z);
        world.Step(10 * Calendar.TicksPerDay);
        return world;
    }

    [Fact]
    public void SameSeedAndSameCommands_GiveTheSameChecksum_AfterTenDays()
    {
        var (a, b) = (PlayedTenDays(), PlayedTenDays());

        Assert.Equal(2, a.PlacedBuildings.Count);
        Assert.Equal(a.Checksum(), b.Checksum());
        var other = NewGame();
        other.Step(10 * Calendar.TicksPerDay);
        Assert.NotEqual(a.Checksum(), other.Checksum());
    }

    [Fact]
    public void PlacedBuildings_AreInTheChecksum_AndSurviveASaveLoad()
    {
        var world = NewGame();
        var (x, z) = FreeSpot(world, BuildingKind.MaintenanceShed, DoorSide.West);
        var without = world.Checksum();
        world.PlaceBuilding(BuildingKind.MaintenanceShed, x, z, DoorSide.West);
        Assert.NotEqual(without, world.Checksum());

        var file = new MemoryStream();
        world.Save(file);
        var loaded = World.Load(new MemoryStream(file.ToArray()));

        Assert.Equal(world.PlacedBuildings, loaded.PlacedBuildings);
        Assert.Equal(world.Checksum(), loaded.Checksum());
        var placed = loaded.PlacedBuildings[0];
        Assert.False(loaded.Flow!.Get(placed.DoorPosition(loaded.Terrain!.CellSize)).IsReachable(placed.Footprint.MinX, placed.Footprint.MinZ), "l'emprise bloque encore après le chargement");
        Assert.Equal(BuildingPlacement.Occupied, loaded.PlaceBuilding(BuildingKind.Bench, placed.Footprint.MinX, placed.Footprint.MinZ, DoorSide.South));
        Assert.True(loaded.SellBuilding(placed.Footprint.MinX, placed.Footprint.MinZ));
    }

    /// <summary>Une vraie sauvegarde v16 de <c>main</c> (abffa10, graine 3, trois jours et demi, pavillon posé) se charge sans bâtiment autre que le pavillon, et on peut y poser.</summary>
    [Fact]
    public void Load_RealV16Save_HasOnlyTheClubhouse_AndAcceptsBuildings()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var expected = File.ReadAllLines(Path.Combine(dir, "v16-clubhouse.txt"));
        var world = World.Load(new MemoryStream(File.ReadAllBytes(Path.Combine(dir, "v16-clubhouse.sav"))), new StubShotResolver());

        Assert.Equal($"tick {world.Tick}", expected[0]);
        Assert.Equal($"balance {world.Ledger.Balance}", expected[1]);
        var c = world.Clubhouse!;
        Assert.Equal($"clubhouse {c.Footprint.MinX} {c.Footprint.MinZ} {c.Footprint.MaxX} {c.Footprint.MaxZ} {(int)c.Side}", expected[2]);
        Assert.Equal($"openHoles {world.OpenHoles}", expected[3]);
        Assert.Empty(world.PlacedBuildings);

        var (x, z) = FreeSpot(world, BuildingKind.Bench);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.Bench, x, z, DoorSide.South));
        Assert.Equal(world.Checksum(), World.Load(new MemoryStream(Resave(world)), new StubShotResolver()).Checksum());
    }

    private static byte[] Resave(World world)
    {
        var file = new MemoryStream();
        world.Save(file);
        return file.ToArray();
    }
}
