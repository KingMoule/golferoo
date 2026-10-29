using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C12 lot 1 : le modèle Course (un trou inchangé, parcours de trois trous, fichiers faux).</summary>
public class CourseTests
{
    internal static string CoursePath() => Path.Combine(HoleDir(), "..", "..", "tests", "Golferoo.Sim.Tests", "Courses", "trois-trous.json");

    private static string HoleDir() => Path.GetDirectoryName(TerrainTests.HolePath())!;

    internal static TerrainGrid ThreeHoles() => CourseLoader.Load(CoursePath(), 2);

    // Sommes relevées avant le chantier (commit d'origine de la spec), 2 000 ticks, charge de 6 golfeurs, un groupe si le trou est ouvert.
    [Theory]
    // Lot 4 : les six sommes de coups ont changé, les trois du trou « blank-coude » (aucun coup joué) non. Le coup se planifie désormais pendant le geste, avec un budget de pas de simulation
    // par tick (World.PlanBudgetPerTick) : un golfeur dont la planification dépasse le geste attend qu'elle finisse avant de frapper, et la planification voit le vent du début du geste.
    // Les trajectoires s'écartent donc de celles d'avant, et la somme est prise à un tick où une planification peut être en cours (son état entre dans la somme).
    // Contrôle (vérifié) : sans PlanJob, coup planifié d'un seul tenant au dernier tick du geste comme avant, les six cas redonnent exactement leurs anciennes sommes. Avec une planification
    // d'un tenant au dernier tick mais un PlanJob créé au début du geste, proto-par4 / graine 1 ne la redonne pas : au tick 2000 un geste est en cours et l'état du PlanJob entre dans la somme.
    // C23 lot 1 : les six sommes de coups changent, le geste dure 23 ticks (jusqu'à l'impact du swing animé) au lieu de 20.
    // Contrôle (vérifié) : avec SwingTicksCount = 20, les six cas redonnent exactement leurs sommes d'avant.
    // C21 lot 2 : les six sommes de coups changent (écarts types et niveaux recalés, vraie loi normale par Box-Muller, tirage d'un choix non
    // optimal par coup au RNG du monde) ; les trois de « blank-coude », sans coup joué, non.
    [InlineData("proto-par4", 1UL, 17606501731160446518UL)]
    [InlineData("proto-par4", 2UL, 10510913993954027195UL)]
    [InlineData("proto-par4", 3UL, 15959334313397627081UL)]
    [InlineData("dogleg-par4", 1UL, 10528660100828060831UL)]
    [InlineData("dogleg-par4", 2UL, 3081298981598650964UL)]
    [InlineData("dogleg-par4", 3UL, 12775752372418096707UL)]
    [InlineData("blank-coude", 1UL, 6977119086498316254UL)]
    [InlineData("blank-coude", 2UL, 6838184519777748364UL)]
    [InlineData("blank-coude", 3UL, 4217163305829179422UL)]
    public void SingleHoleChecksumIsUnchanged(string hole, ulong seed, ulong expected)
    {
        var terrain = CourseLoader.Load(Path.Combine(HoleDir(), hole + ".json"), 2);
        Assert.Equal(1, terrain.Course.Count);
        var world = new World(seed, terrain);
        world.SetLoadMode(6);
        if (world.HoleOpen)
        {
            world.SpawnGroup();
        }

        world.Step(2000);
        Assert.Equal(expected, world.Checksum());
    }

    [Fact]
    public void ThreeHolesHaveTheirMarkersAndPar()
    {
        var terrain = ThreeHoles();
        Assert.Equal(70 * 2, terrain.CellsX);
        Assert.Equal(56 * 2, terrain.CellsZ);
        Assert.Equal([1, 2, 3], terrain.Course.Holes.Select(h => h.Number));
        Assert.All(terrain.Course.Holes, h =>
        {
            Assert.Equal(Surface.Tee, terrain.SurfaceAt(h.Tee.X, h.Tee.Z));
            Assert.Equal(Surface.Green, terrain.SurfaceAt(h.Hole.X, h.Hole.Z));
            Assert.Equal(Surface.Green, terrain.SurfaceAt(h.Green.X, h.Green.Z));
            Assert.Equal(4, h.Par);
            Assert.Empty(HoleCheck.Check(terrain, h));
        });

        // Hole 1 sits at the origin and is the one the groups play: the shortcuts read it.
        Assert.Equal(terrain.Course[1].Tee, terrain.Tee);
        Assert.Equal(terrain.Course[1].Hole, terrain.Hole);

        // Hole 3 is proto-par4 turned a quarter turn: same tee-to-pin distance, now running south.
        var source = CourseLoader.Load(Path.Combine(HoleDir(), "proto-par4.json"), 2);
        var h3 = terrain.Course[3];
        Assert.Equal(Dist(source.Tee, source.Hole), Dist(h3.Tee, h3.Hole), 6);
        Assert.True(h3.Hole.Z > h3.Tee.Z + 300);
        Assert.InRange(Math.Abs(h3.Hole.X - h3.Tee.X), 0, 30);
    }

    [Fact]
    public void WorldOpensEachHoleAndRoutesPlacements()
    {
        var world = new World(1, ThreeHoles());
        Assert.True(world.HoleOpen);
        Assert.True(world.IsHoleOpen(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.IsHoleOpen(4));

        var terrain = world.Terrain!;
        var h2 = terrain.Course[2];
        var other = Enumerable.Range(0, terrain.CellsX * terrain.CellsZ)
            .Select(i => terrain.CellCenter(i % terrain.CellsX, i / terrain.CellsX))
            .First(c => c.Z > 144 && terrain.SurfaceAt(c.X, c.Z) == Surface.Tee && (c.X != h2.Tee.X || c.Z != h2.Tee.Z));
        Assert.False(world.PlaceTee(4, other));
        Assert.True(world.PlaceTee(2, other));
        Assert.False(world.PlaceHole(2, other));
        var tee1 = terrain.Course[1].Tee;
        world.Step();
        Assert.Equal(other, h2.Tee);
        Assert.Equal(tee1, terrain.Course[1].Tee);
    }

    [Fact]
    public void SingleHoleFileLoadsAsOneHoleCourse()
    {
        var path = Path.Combine(HoleDir(), "dogleg-par4.json");
        var viaCourse = CourseLoader.Load(path, 2);
        var viaHole = HoleLoader.Load(path, 2);
        Assert.Equal(viaHole.Checksum(), viaCourse.Checksum());
        Assert.Equal(1, viaCourse.Course.Count);
        Assert.Equal(viaHole.Par, viaCourse.Course[1].Par);
    }

    [Fact]
    public void OverlappingHolesNameTheHole()
    {
        var json = CourseJson(70, 56, (1, 0, 0, 0), (2, 5, 5, 0));
        var e = Assert.Throws<HoleDataException>(() => CourseLoader.Parse(json, HoleDir(), 2));
        Assert.Contains("Trou 2", e.Message);
        Assert.Contains("chevauche", e.Message);
    }

    [Fact]
    public void HoleOutsideTheMapNamesTheHole()
    {
        var json = CourseJson(70, 56, (1, 0, 0, 0), (2, 40, 30, 0));
        var e = Assert.Throws<HoleDataException>(() => CourseLoader.Parse(json, HoleDir(), 2));
        Assert.Contains("Trou 2", e.Message);
        Assert.Contains("sort de la carte", e.Message);
    }

    [Fact]
    public void HoleNumbersMustRunFromOneWithoutGap()
    {
        var json = CourseJson(70, 56, (1, 0, 0, 0), (3, 0, 18, 0));
        Assert.Throws<HoleDataException>(() => CourseLoader.Parse(json, HoleDir(), 2));
    }

    [Fact]
    public void RoughAndWoodsMayOverlap()
    {
        // The two holes' bounding boxes cross but only on rough or woods cells: accepted.
        var json = CourseJson(70, 56, (1, 0, 0, 0), (2, 0, 15, 0));
        var terrain = CourseLoader.Parse(json, HoleDir(), 2);
        Assert.Equal(2, terrain.Course.Count);
    }

    [Theory]
    [InlineData(100000)]
    [InlineData(30000)]
    [InlineData(4097)]
    public void HugeMapIsRefusedBeforeAllocating(int tiles)
    {
        var json = CourseJson(tiles, tiles, (1, 0, 0, 0));
        Assert.Throws<HoleDataException>(() => CourseLoader.Parse(json, HoleDir(), 2));
    }

    private static string CourseJson(int tilesX, int tilesZ, params (int Number, int X, int Z, int Rotation)[] holes)
    {
        var entries = holes.Select(h => $$"""{ "file": "{{(h.Number == 1 ? "proto-par4" : "dogleg-par4")}}.json", "x": {{h.X}}, "z": {{h.Z}}, "rotation": {{h.Rotation}}, "number": {{h.Number}} }""");
        return $$"""{ "tilesX": {{tilesX}}, "tilesZ": {{tilesZ}}, "ground": "Rough", "holes": [ {{string.Join(", ", entries)}} ] }""";
    }

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
