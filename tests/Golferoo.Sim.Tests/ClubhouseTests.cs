using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C15 lot 1 : le pavillon dans la sim (placement, déplacement payant, emprise bloquante, champ de flux vers la porte).</summary>
public class ClubhouseTests
{
    internal static TerrainGrid LeNeuf() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    private static World NewGame() => new(1, LeNeuf(), league: true, placeClubhouse: true);

    private static bool IsFreeAndFlat(World world, Building b)
    {
        var t = world.Terrain!;
        var heights = new List<double>();
        for (var z = b.Footprint.MinZ; z <= b.Footprint.MaxZ; z++)
        {
            for (var x = b.Footprint.MinX; x <= b.Footprint.MaxX; x++)
            {
                if (t.GetCellSurface(x, z) is not (Surface.Fairway or Surface.Rough or Surface.Path))
                {
                    return false;
                }

                heights.Add(t.Heights.Sample((x + 0.5) * t.CellSize, (z + 0.5) * t.CellSize));
            }
        }

        return heights.Max() - heights.Min() <= World.ClubhouseMaxRelief
            && !world.Trees.Trees.Any(tree => b.Footprint.Contains(t.CellXOf(tree.X), t.CellZOf(tree.Z)));
    }

    /// <summary>Un autre emplacement valide, au plus près de <paramref name="from"/> en parcourant la carte par pas de 4 cellules.</summary>
    private static Point2 AnotherValidSpot(World world, DoorSide side)
    {
        var t = world.Terrain!;
        for (var z = 0; z < t.CellsZ; z += 4)
        {
            for (var x = 0; x < t.CellsX; x += 4)
            {
                var center = new Point2((x + 0.5) * t.CellSize, (z + 0.5) * t.CellSize);
                var b = Clubhouse.At((int)Math.Round(center.X / t.CellSize - 6.0), (int)Math.Round(center.Z / t.CellSize - 4.0), side);
                if (b.Footprint != world.Clubhouse!.Footprint && Math.Abs(x - world.Clubhouse.Footprint.MinX) > 20 && world.CanPlaceClubhouse(b))
                {
                    return center;
                }
            }
        }

        throw new InvalidOperationException("Aucun autre emplacement valide.");
    }

    [Fact]
    public void WorldWithoutPlacement_HasNoClubhouse()
    {
        var world = new World(1, LeNeuf());
        Assert.Null(world.Clubhouse);
        Assert.Null(world.ClubhouseField);
    }

    [Fact]
    public void NewGame_PlacesClubhouse_OnFreeFlatCells_WithAPathFromDoorToTee()
    {
        var world = NewGame();
        var club = world.Clubhouse!;
        var t = world.Terrain!;

        Assert.NotNull(club);
        Assert.Equal(Clubhouse.Width, club.Footprint.MaxX - club.Footprint.MinX + 1);
        Assert.Equal(Clubhouse.Depth, club.Footprint.MaxZ - club.Footprint.MinZ + 1);
        Assert.True(IsFreeAndFlat(world, club), "emprise libre et plate");
        Assert.False(club.Footprint.Contains(club.DoorCell.X, club.DoorCell.Z));
        Assert.True(t.IsWalkable(club.DoorCell.X, club.DoorCell.Z));

        var field = world.ClubhouseField!;
        Assert.Equal(club.DoorCell, (field.DestX, field.DestZ));
        Assert.True(field.IsReachable(t.CellXOf(t.Tee.X), t.CellZOf(t.Tee.Z)), "chemin de la porte au départ du trou 1");
        Assert.Contains((club.DoorCell.Z * t.CellsX) + club.DoorCell.X, world.Flow!.CachedKeys);
    }

    [Fact]
    public void NewGame_PlacesClubhouse_NextToTheFirstTee()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var distance = Math.Sqrt(Math.Pow(world.Clubhouse!.Position(t.CellSize).X - t.Tee.X, 2) + Math.Pow(world.Clubhouse.Position(t.CellSize).Z - t.Tee.Z, 2));

        Assert.True(distance < 60.0, $"pavillon à {distance:0} m du départ");
    }

    [Fact]
    public void Footprint_BlocksWalkers_AndPlanting()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var club = world.Clubhouse!;
        var field = world.ClubhouseField!;

        // Aucune cellule de l'emprise n'est atteinte, et le chemin du départ à la porte la contourne.
        Assert.False(field.IsReachable(club.Footprint.MinX + 3, club.Footprint.MinZ + 3));
        var (cx, cz) = (t.CellXOf(t.Tee.X), t.CellZOf(t.Tee.Z));
        for (var n = 0; field.TryNextCell(cx, cz, out var nx, out var nz) && n < 100_000; n++)
        {
            Assert.False(club.Footprint.Contains(nx, nz));
            (cx, cz) = (nx, nz);
        }

        Assert.Equal(club.DoorCell, (cx, cz));

        var middle = club.Position(t.CellSize);
        world.PlantTree(middle.X, middle.Z, Trees.TreeSpeciesId.Oak, Trees.TreePlantStage.Sapling);
        world.Step();
        Assert.Equal(1, world.TreeCommandsRefused);
        Assert.Equal(0, world.TreesPlanted);
    }

    [Fact]
    public void Move_ToAValidSpot_DebitsTheCost_AndChangesPosition()
    {
        var world = NewGame();
        var before = world.Ledger.Balance;
        var spot = AnotherValidSpot(world, DoorSide.South);

        Assert.True(world.TryMoveClubhouse(spot, DoorSide.South));

        Assert.Equal(before - world.Economy.ClubhouseMove, world.Ledger.Balance);
        Assert.Equal(-world.Economy.ClubhouseMove, world.Ledger.Current(LedgerCategory.Buildings));
        Assert.Equal(DoorSide.South, world.Clubhouse!.Side);
        Assert.True(IsFreeAndFlat(world, world.Clubhouse));
        Assert.True(world.ClubhouseField!.IsReachable(world.Terrain!.CellXOf(world.Terrain.Tee.X), world.Terrain.CellZOf(world.Terrain.Tee.Z)));
    }

    [Fact]
    public void Move_ToAnInvalidSpot_IsRefusedWithoutDebit()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var original = world.Clubhouse;
        var before = world.Ledger.Balance;
        var checksum = world.Checksum();

        Assert.False(world.TryMoveClubhouse(new Point2(0, 0), DoorSide.South)); // hors de la carte
        Assert.False(world.TryMoveClubhouse(t.Tee, DoorSide.South)); // sur le départ
        Assert.False(world.TryMoveClubhouse(t.Green, DoorSide.North)); // sur un green
        Assert.False(world.TryMoveClubhouse(original!.Position(t.CellSize), original.Side)); // ne bouge pas

        Assert.Equal(original, world.Clubhouse);
        Assert.Equal(before, world.Ledger.Balance);
        Assert.Equal(checksum, world.Checksum());
    }

    [Fact]
    public void Move_OntoATree_OrAGolfer_IsRefused()
    {
        var world = NewGame();
        var spot = AnotherValidSpot(world, DoorSide.South);
        world.Trees.Add(spot.X, spot.Z, Trees.TreeSpeciesId.Oak, 120);
        var before = world.Ledger.Balance;

        Assert.False(world.TryMoveClubhouse(spot, DoorSide.South));
        Assert.Equal(before, world.Ledger.Balance);
    }

    [Fact]
    public void Move_ChangesTheChecksum_AndKeepsCachedFieldsExact()
    {
        var world = NewGame();
        var t = world.Terrain!;
        var other = NewGame();
        Assert.Equal(other.Checksum(), world.Checksum());

        // Quelques champs en cache : vers les trous, loin du pavillon.
        foreach (var hole in t.Course.Holes)
        {
            world.Flow!.Get(hole.Hole);
        }

        var spot = AnotherValidSpot(world, DoorSide.West);
        Assert.True(world.TryMoveClubhouse(spot, DoorSide.West));
        Assert.NotEqual(other.Checksum(), world.Checksum());

        // Tout champ resté en cache est identique à un champ refait à neuf avec la nouvelle emprise.
        var costs = world.Flow!.CostsWith(null, null);
        Assert.NotEmpty(world.Flow.CachedKeys);
        foreach (var key in world.Flow.CachedKeys)
        {
            var cached = world.Flow.Get(key % t.CellsX, key / t.CellsX);
            var fresh = FlowField.Build(t, costs, key % t.CellsX, key / t.CellsX);
            AssertSameField(cached, fresh, t, world.Clubhouse!.Footprint);
        }
    }

    /// <summary>Mêmes distances et mêmes pas hors de <paramref name="inside"/> (l'emprise elle-même, que personne ne traverse : un champ gardé y garde ses anciennes valeurs).</summary>
    private static void AssertSameField(FlowField a, FlowField b, TerrainGrid t, CellRect? inside = null)
    {
        for (var z = 0; z < t.CellsZ; z++)
        {
            for (var x = 0; x < t.CellsX; x++)
            {
                if (inside is { } skip && skip.Contains(x, z))
                {
                    continue;
                }

                Assert.Equal(b.DistanceAt(x, z), a.DistanceAt(x, z));
                Assert.Equal(b.TryNextCell(x, z, out var bx, out var bz), a.TryNextCell(x, z, out var ax, out var az));
                Assert.Equal((bx, bz), (ax, az));
            }
        }
    }

    /// <summary>Deux régions séparées par un mur d'eau : un champ de la région où ne se passe rien reste en cache, le champ touché passe en attente.</summary>
    [Fact]
    public void ObstacleChange_InvalidatesOnlyTheFieldsItTouches()
    {
        var t = PathingTests.Flat(40, 20);
        for (var z = 0; z < 20; z++)
        {
            t.PaintCell(20, z, Surface.Water);
        }

        var cache = new FlowFieldCache(t);
        var left = cache.Get(2, 10);
        var right = cache.Get(37, 2);
        var rightNear = cache.Get(25, 10);

        // Un obstacle dans la région de gauche : le champ de droite ne le voit pas.
        cache.ChangeObstacle(null, new CellRect(8, 8, 11, 12));
        Assert.Equal(2, cache.Count);
        Assert.Same(right, cache.Get(37, 2));
        Assert.Same(rightNear, cache.Get(25, 10));
        Assert.Single(cache.Pending);
        Assert.Equal((10 * 40) + 2, cache.Pending[0]);

        // Le champ de gauche, refait, contourne l'obstacle ; celui de droite reste identique à un calcul neuf.
        cache.Refresh();
        var costs = cache.CostsWith(null, null);
        AssertSameField(cache.Get(2, 10), FlowField.Build(t, costs, 2, 10), t);
        AssertSameField(cache.Get(37, 2), FlowField.Build(t, costs, 37, 2), t, new CellRect(8, 8, 11, 12));
        Assert.False(cache.Get(2, 10).IsReachable(9, 10));
        Assert.NotSame(left, cache.Get(2, 10));

        // Libérer la même emprise : seul le champ de gauche (qui atteint l'anneau) est touché.
        cache.ChangeObstacle(new CellRect(8, 8, 11, 12), null);
        Assert.Same(right, cache.Get(37, 2));
        Assert.Contains((10 * 40) + 2, cache.Pending);
    }

    /// <summary>Une emprise ajoutée loin des chemins d'un champ ne le touche pas, même dans sa région.</summary>
    [Fact]
    public void AddedObstacle_OffThePathsOfAField_KeepsIt()
    {
        var t = PathingTests.Flat(40, 20);
        var cache = new FlowFieldCache(t);
        var corner = cache.Get(1, 1);
        var nearDest = cache.Get(3, 3);

        cache.ChangeObstacle(null, new CellRect(35, 15, 39, 19)); // coin opposé, collé au bord : rien derrière

        Assert.Same(corner, cache.Get(1, 1));
        Assert.Same(nearDest, cache.Get(3, 3));
        Assert.Empty(cache.Pending);
        var costs = cache.CostsWith(null, null);
        AssertSameField(cache.Get(1, 1), FlowField.Build(t, costs, 1, 1), t, new CellRect(35, 15, 39, 19));
    }
}
