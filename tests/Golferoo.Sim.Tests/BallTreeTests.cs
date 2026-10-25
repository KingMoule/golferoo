using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>Balle en vol et arbres de la sim (C02 lot 2) : tronc, couronne selon l'essence, l'âge et la saison, déterminisme.</summary>
public class BallTreeTests
{
    private const double TreeX = 120.0;
    private const double TreeZ = 100.0;
    private const double StartX = 100.0;
    private const double Speed = 60.0;

    private static TerrainGrid Flat()
    {
        const int cells = 50;
        const double cell = 4.0;
        var field = new HeightField(cells, cells, cell);
        var size = cells * cell;
        var hole = new Point2(size - 8, 8);
        var grid = new TerrainGrid(field, "test", new Point2(8, size / 2), hole, hole);
        for (var cz = 0; cz < cells; cz++)
        {
            for (var cx = 0; cx < cells; cx++)
            {
                grid.PaintCell(cx, cz, Surface.Fairway);
            }
        }

        return grid;
    }

    private static TreeStand StandWith(TreeSpeciesId species, int ageMonths)
    {
        var stand = new TreeStand(600, 600);
        stand.Add(TreeX, TreeZ, species, ageMonths);
        return stand;
    }

    private static SimulationResult Fly(TerrainGrid terrain, TreeStand? stand, Season season, ulong seed, double height, bool path = false, DeterministicRng? rng = null) =>
        BallSimulator.Run(terrain, new Point2(StartX, TreeZ), new Vec3(Speed, 0, 0), rolling: false, drag: 0.0, recordPath: path, startHeight: height, trees: stand, season: season, rng: rng ?? new DeterministicRng(seed));

    /// <summary>Part des balles lancées au centre de la couronne qui la traversent (atterrissent loin derrière l'arbre).</summary>
    private static double PassRate(TerrainGrid terrain, TreeSpeciesId species, int ageMonths, Season season, int shots)
    {
        var spec = TreeSpeciesTable.Get(species);
        var stand = StandWith(species, ageMonths);
        var h = Tree.Height(spec, ageMonths);
        var b = Tree.CrownBase(spec, ageMonths);
        // Départ rehaussé de la chute en vol : la balle arrive au centre de la couronne.
        var fall = 0.5 * BallSimulator.Gravity * ((TreeX - StartX) / Speed) * ((TreeX - StartX) / Speed);
        var centre = ((b + h) * 0.5) + fall;
        var passed = 0;
        for (var seed = 1; seed <= shots; seed++)
        {
            if (Fly(terrain, stand, season, (ulong)seed, centre).FirstLanding.X > TreeX + 15)
            {
                passed++;
            }
        }

        return (double)passed / shots;
    }

    [Fact]
    public void Trunk_AlwaysStopsOrBouncesTheBall()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Deciduous, 100 * 12);
        for (var seed = 1; seed <= 100; seed++)
        {
            // À 1,5 m : sous la couronne (base à 3 m), face au tronc.
            var sim = Fly(terrain, stand, Season.Summer, (ulong)seed, 1.5);
            Assert.True(sim.Landed);
            Assert.True(sim.FirstLanding.X < TreeX, $"graine {seed} : la balle a traversé le tronc (atterrit en {sim.FirstLanding.X}).");
        }
    }

    [Fact]
    public void Trunk_ReflectsOnHorizontalNormalWithBarkRestitution()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Deciduous, 100 * 12);
        var sim = Fly(terrain, stand, Season.Summer, 1, 1.5, path: true);
        var path = sim.Shot.Trajectory!;
        // Sans arbre, la balle file à 60 m/s : avec le tronc, elle revient vers le tireur, bien plus lentement.
        Assert.True(path.Count > 3);
        var back = path.Zip(path.Skip(1), (a, b) => b.X - a.X).Where(d => d < 0).ToList();
        Assert.NotEmpty(back);
        // Échantillons toutes les 1/20 s : la vitesse de retour est celle d'une restitution de 0,3 (18 m/s au plus), pas 60.
        Assert.InRange(-back.Min() * 20.0, 1.0, 20.0);
        Assert.True(sim.FirstLanding.X < TreeX);
    }

    [Fact]
    public void Crown_BroadleafLetsMorePassInSpringThanSummer()
    {
        var terrain = Flat();
        var summer = PassRate(terrain, TreeSpeciesId.Deciduous, 100 * 12, Season.Summer, 10_000);
        var spring = PassRate(terrain, TreeSpeciesId.Deciduous, 100 * 12, Season.Spring, 10_000);
        var winter = PassRate(terrain, TreeSpeciesId.Deciduous, 100 * 12, Season.Winter, 10_000);
        Console.WriteLine($"Feuillu, 10 000 tirages : passent été {summer:P1}, printemps {spring:P1}, hiver {winter:P1}");
        Assert.True(spring > summer + 0.15, $"été {summer:P1}, printemps {spring:P1}");
        Assert.True(winter > spring);
    }

    [Fact]
    public void Crown_PineBarelyChangesBetweenSeasons()
    {
        var terrain = Flat();
        var rates = new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter }
            .Select(s => PassRate(terrain, TreeSpeciesId.Pine, 100 * 12, s, 3_000)).ToArray();
        Console.WriteLine($"Pin, 10 000 tirages : printemps {rates[0]:P1}, été {rates[1]:P1}, automne {rates[2]:P1}, hiver {rates[3]:P1}");
        Assert.True(rates.Max() - rates.Min() < 0.06, string.Join(" ; ", rates.Select(r => r.ToString("P1"))));
    }

    [Fact]
    public void Crown_SaplingStopsFewerBallsThanAdult()
    {
        var terrain = Flat();
        var adult = PassRate(terrain, TreeSpeciesId.Deciduous, 100 * 12, Season.Summer, 2_000);
        var sapling = PassRate(terrain, TreeSpeciesId.Deciduous, 0, Season.Summer, 2_000);
        Console.WriteLine($"Passent : adulte {adult:P1}, plant {sapling:P1}");
        Assert.True(sapling > adult + 0.5, $"adulte {adult:P1}, plant {sapling:P1}");
    }

    [Fact]
    public void SameSeed_GivesBitIdenticalTrajectory()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Deciduous, 100 * 12);
        var a = Fly(terrain, stand, Season.Spring, 77, 9.5, path: true).Shot.Trajectory!;
        var b = Fly(terrain, stand, Season.Spring, 77, 9.5, path: true).Shot.Trajectory!;
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].X), BitConverter.DoubleToInt64Bits(b[i].X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Y), BitConverter.DoubleToInt64Bits(b[i].Y));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Z), BitConverter.DoubleToInt64Bits(b[i].Z));
        }
    }

    [Fact]
    public void BallAboveMaxHeight_ConsumesNoDrawAndIsUnchanged()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Deciduous, 100 * 12);
        Assert.Equal(16.0, stand.MaxHeightAt(TreeX, TreeZ), 1e-9);
        var rng = new DeterministicRng(5);
        var before = rng.StateHash();
        // 30 m de hauteur : la balle passe au-dessus du feuillu (16 m) puis atterrit loin derrière.
        var withTrees = Fly(terrain, stand, Season.Summer, 5, 30.0, path: true, rng: rng);
        var without = Fly(terrain, null, Season.Summer, 5, 30.0, path: true);
        Assert.Equal(before, rng.StateHash());
        Assert.Equal(without.FirstLanding, withTrees.FirstLanding);
        Assert.Equal(without.Shot.Trajectory, withTrees.Shot.Trajectory);
    }

    [Fact]
    public void BallThroughCrown_ConsumesDraws()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Deciduous, 100 * 12);
        var rng = new DeterministicRng(5);
        var before = rng.StateHash();
        Fly(terrain, stand, Season.Summer, 5, 9.5, rng: rng);
        Assert.NotEqual(before, rng.StateHash());
    }

    [Fact]
    public void StopProbability_MatchesExponential()
    {
        foreach (var a in new[] { 0.0, 0.01, 0.1, 0.5, 1.0 })
        {
            Assert.Equal(1.0 - Math.Exp(-a), TreeCollision.StopProbability(a), 0.004);
        }
    }

    [Fact]
    public void World_SeasonDefaultsToSummerAndIsSettable()
    {
        var w = new World(1, Flat());
        Assert.Equal(Season.Summer, w.Season);
        w.Season = Season.Winter;
        Assert.Equal(Season.Winter, w.Season);
    }
}
