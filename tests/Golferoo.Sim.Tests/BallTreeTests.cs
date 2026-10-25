using Golferoo.Sim.Golfers;
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
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        for (var seed = 1; seed <= 100; seed++)
        {
            // À 1,5 m : sous la couronne (base à 3 m), face au tronc.
            var sim = Fly(terrain, stand, Season.Summer, (ulong)seed, 1.5);
            Assert.True(sim.Landed);
            Assert.True(sim.FirstLanding.X < TreeX, $"graine {seed} : la balle a traversé le tronc (atterrit en {sim.FirstLanding.X}).");
        }
    }

    [Fact]
    public void Crown_BroadleafLetsMorePassInSpringThanSummer()
    {
        var terrain = Flat();
        var summer = PassRate(terrain, TreeSpeciesId.Oak, 100 * 12, Season.Summer, 10_000);
        var spring = PassRate(terrain, TreeSpeciesId.Oak, 100 * 12, Season.Spring, 10_000);
        var winter = PassRate(terrain, TreeSpeciesId.Oak, 100 * 12, Season.Winter, 10_000);
        Console.WriteLine($"Chêne, 10 000 tirages : passent été {summer:P1}, printemps {spring:P1}, hiver {winter:P1}");
        Assert.True(spring > summer + 0.15, $"été {summer:P1}, printemps {spring:P1}");
        Assert.True(winter > spring);
    }

    [Fact]
    public void Crown_PineBarelyChangesBetweenSeasons()
    {
        var terrain = Flat();
        var rates = new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter }
            .Select(s => PassRate(terrain, TreeSpeciesId.Fir, 100 * 12, s, 3_000)).ToArray();
        Console.WriteLine($"Sapin, 10 000 tirages : printemps {rates[0]:P1}, été {rates[1]:P1}, automne {rates[2]:P1}, hiver {rates[3]:P1}");
        Assert.True(rates.Max() - rates.Min() < 0.06, string.Join(" ; ", rates.Select(r => r.ToString("P1"))));
    }

    [Fact]
    public void Crown_SaplingStopsFewerBallsThanAdult()
    {
        var terrain = Flat();
        var adult = PassRate(terrain, TreeSpeciesId.Oak, 100 * 12, Season.Summer, 2_000);
        var sapling = PassRate(terrain, TreeSpeciesId.Oak, 0, Season.Summer, 2_000);
        Console.WriteLine($"Passent : adulte {adult:P1}, plant {sapling:P1}");
        Assert.True(sapling > adult + 0.5, $"adulte {adult:P1}, plant {sapling:P1}");
    }

    [Fact]
    public void SameSeed_GivesBitIdenticalTrajectory()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
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
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        Assert.Equal(17.5, stand.MaxTopAt(TreeX, TreeZ), 1e-9);
        var rng = new DeterministicRng(5);
        var before = rng.StateHash();
        // 30 m de hauteur : la balle passe au-dessus du chêne (17,5 m) puis atterrit loin derrière.
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
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        var rng = new DeterministicRng(5);
        var before = rng.StateHash();
        Fly(terrain, stand, Season.Summer, 5, 9.5, rng: rng);
        Assert.NotEqual(before, rng.StateHash());
    }

    [Fact]
    public void World_SeasonDefaultsToSummerAndIsSettable()
    {
        var w = new World(1, Flat());
        Assert.Equal(Season.Summer, w.Season);
        w.Season = Season.Winter;
        Assert.Equal(Season.Winter, w.Season);
    }

    // --- Étape directe de TreeCollision : un seul arbre, un seul pas ---

    private static readonly double TrunkR = Tree.TrunkRadius(TreeSpeciesTable.Get(TreeSpeciesId.Oak), 100 * 12) + 0.0214;

    /// <summary>Un pas de (px, py, pz) à (qx, py, qz) contre le feuillu adulte en (TreeX, TreeZ) ; rend position et vitesse après le pas.</summary>
    private static (double X, double Y, double Z, double Vx, double Vy, double Vz) TrunkStep(double px, double pz, double qx, double qz, double vx, double vz, double y = 1.5)
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        var x = qx;
        var yy = y;
        var z = qz;
        var vy = -1.0;
        var done = false;
        TreeCollision.Step(terrain, stand, Season.Summer, new DeterministicRng(1), new TreeCollision.Probe(), px, y, pz, ref x, ref yy, ref z, ref vx, ref vy, ref vz, ref done);
        return (x, yy, z, vx, vy, vz);
    }

    [Fact]
    public void Trunk_HeadOn_ReturnsThirtyPercentOfNormalSpeed()
    {
        var r = TrunkStep(TreeX - TrunkR - 0.1, TreeZ, TreeX - TrunkR + 0.2, TreeZ, 60.0, 0.0);
        Assert.Equal(-18.0, r.Vx, 1e-9);
        Assert.Equal(0.0, r.Vz, 1e-9);
        Assert.Equal(-1.0, r.Vy, 1e-12);
        Assert.Equal(TreeX - TrunkR - 1e-3, r.X, 1e-9);
    }

    [Fact]
    public void Trunk_Oblique_KeepsTangentAndScalesNormal()
    {
        // Choc en (TreeX − R, TreeZ) : normale (−1, 0). Vitesse (40, 30) : normale 40 → −12, tangente 30 inchangée.
        var r = TrunkStep(TreeX - TrunkR - 0.1, TreeZ - 0.075, TreeX - TrunkR + 0.2, TreeZ + 0.15, 40.0, 30.0);
        var nx = (r.X - TreeX) / Math.Sqrt(((r.X - TreeX) * (r.X - TreeX)) + ((r.Z - TreeZ) * (r.Z - TreeZ)));
        var nz = (r.Z - TreeZ) / Math.Sqrt(((r.X - TreeX) * (r.X - TreeX)) + ((r.Z - TreeZ) * (r.Z - TreeZ)));
        var vn = (r.Vx * nx) + (r.Vz * nz);
        var vt = (-r.Vx * nz) + (r.Vz * nx);
        var vn0 = (40.0 * nx) + (30.0 * nz);
        var vt0 = (-40.0 * nz) + (30.0 * nx);
        Assert.Equal(-0.3 * vn0, vn, 1e-6);
        Assert.Equal(vt0, vt, 1e-6);
    }

    [Fact]
    public void Trunk_Grazing_HitsInsideTangentMissesOutside()
    {
        // Le long de x = TreeX − R + δ : à δ > 0 le segment coupe le cylindre, à δ < 0 il le frôle sans le toucher.
        var miss = TrunkStep(TreeX - 0.4, TreeZ - (TrunkR + 0.01), TreeX + 0.4, TreeZ - (TrunkR + 0.01), 0.0, 60.0);
        _ = miss;
        var hit = TrunkStep(TreeX - 0.4, TreeZ - (TrunkR - 0.01), TreeX + 0.4, TreeZ - (TrunkR - 0.01), 60.0, 0.0);
        Assert.NotEqual(60.0, hit.Vx);
        var pass = TrunkStep(TreeX - 0.4, TreeZ - (TrunkR + 0.01), TreeX + 0.4, TreeZ - (TrunkR + 0.01), 60.0, 0.0);
        Assert.Equal(60.0, pass.Vx);
        Assert.Equal(TreeX + 0.4, pass.X);
    }

    [Fact]
    public void Trunk_StartInside_PushesOutWhenMovingInAndIgnoresWhenMovingOut()
    {
        var inward = TrunkStep(TreeX - (TrunkR * 0.5), TreeZ, TreeX, TreeZ, 60.0, 0.0);
        Assert.Equal(-18.0, inward.Vx, 1e-9);
        Assert.Equal(TreeX - TrunkR - 1e-3, inward.X, 1e-9);

        var outward = TrunkStep(TreeX - (TrunkR * 0.5), TreeZ, TreeX - (TrunkR * 0.9), TreeZ, -60.0, 0.0);
        Assert.Equal(-60.0, outward.Vx);
    }

    // --- Couronne : une seule fois par vol, réarmée au sol ---

    [Fact]
    public void Crown_AlreadyStopped_NoDrawNoChange()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        var rng = new DeterministicRng(3);
        var before = rng.StateHash();
        double x = TreeX + 0.3, y = 9.5, z = TreeZ, vx = 60.0, vy = 0.0, vz = 0.0;
        var done = true;
        TreeCollision.Step(terrain, stand, Season.Summer, rng, new TreeCollision.Probe(), TreeX - 0.3, 9.5, TreeZ, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref done);
        Assert.Equal(before, rng.StateHash());
        Assert.Equal(60.0, vx);
        Assert.True(done);
    }

    /// <summary>Nombre de tirages faits par le vol : on rejoue la suite de la graine jusqu'à retrouver l'état final.</summary>
    private static int Draws(ulong seed, Func<DeterministicRng, SimulationResult> flight)
    {
        var rng = new DeterministicRng(seed);
        flight(rng);
        var target = rng.StateHash();
        var replay = new DeterministicRng(seed);
        for (var n = 0; n < 100_000; n++)
        {
            if (replay.StateHash() == target)
            {
                return n;
            }

            replay.NextUInt64();
        }

        throw new InvalidOperationException("État final introuvable.");
    }

    private static SimulationResult Drop(TerrainGrid terrain, TreeStand stand, DeterministicRng rng, double height) =>
        BallSimulator.Run(terrain, new Point2(TreeX + 1.0, TreeZ), new Vec3(0, 0, 0), rolling: false, 0.0, false, startHeight: height, trees: stand, season: Season.Summer, rng: rng);

    [Fact]
    public void Crown_StopsOncePerFlight()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        // Chute de 12 m dans la couronne : une descente de 270 pas environ. Sans la règle « une couronne par vol », la balle
        // (arrêtée mais encore dans la couronne) tirerait à chacun d'eux ; avec elle, le tirage cesse au premier arrêt.
        var mean = Enumerable.Range(1, 60).Average(sd => Draws((ulong)sd, rng => Drop(terrain, stand, rng, 12.0)));
        Console.WriteLine($"Tirages moyens d'une descente de 270 pas : {mean:F1}");
        Assert.InRange(mean, 50, 200);
    }

    /// <summary>Hauteur maximale atteinte par la balle après son premier contact avec le sol (rebond), pour une chute de 60 m.</summary>
    private static double ReboundApex(TerrainGrid terrain, TreeStand? stand, ulong seed)
    {
        var sim = BallSimulator.Run(terrain, new Point2(TreeX + 1.0, TreeZ), new Vec3(0, 0, 0), false, 0.0, true, startHeight: 60.0, trees: stand, season: Season.Summer, rng: new DeterministicRng(seed));
        var path = sim.Shot.Trajectory!;
        var landing = path.ToList().FindIndex(v => v.Y < 0.5);
        return path.Skip(landing).Take(40).Max(v => v.Y);
    }

    [Fact]
    public void Crown_RearmsOnGround_SoTheReboundIsStoppedAgain()
    {
        var terrain = Flat();
        var stand = StandWith(TreeSpeciesId.Oak, 100 * 12);
        Assert.InRange(ReboundApex(terrain, null, 1), 6.0, 9.0); // sans arbre : apex d'environ 7,3 m, bien au-dessus de la base de la couronne (3 m)
        var apexes = Enumerable.Range(1, 30).Select(sd => ReboundApex(terrain, stand, (ulong)sd)).OrderBy(a => a).ToList();
        Console.WriteLine($"Apex du rebond avec couronne : médiane {apexes[15]:F1}, max {apexes[^1]:F1}");
        // Sans réarmement tous les rebonds monteraient à 7,3 m ; réarmée, la couronne en coupe la plupart en route.
        Assert.True(apexes.Count(a => a < 7.0) >= 15, $"médiane {apexes[15]}");
    }

    // --- Index : voisines et bord de carte ---

    [Fact]
    public void MaxTopNear_SeesNeighbourCellsAndClampsAtEdges()
    {
        var stand = new TreeStand(100, 100);
        stand.Add(15.9, 50, TreeSpeciesId.Fir, 100 * 12);
        Assert.Equal(double.NegativeInfinity, stand.MaxTopAt(16.5, 50));
        Assert.Equal(20.0, stand.MaxTopNear(16.5, 50, 1.0), 1e-9);
        Assert.Equal(20.0, stand.MaxTopNear(15.5, 50, 1.0), 1e-9);
        Assert.Equal(double.NegativeInfinity, stand.MaxTopNear(40, 50, 1.0));
        stand.Add(0.5, 0.5, TreeSpeciesId.Oak, 100 * 12);
        stand.Add(99.9, 99.9, TreeSpeciesId.Fir, 100 * 12);
        Assert.Equal(17.5, stand.MaxTopNear(-3, -3, 6.0), 1e-9);
        Assert.Equal(20.0, stand.MaxTopNear(104, 104, 6.0), 1e-9);
        Assert.Equal(20.0, stand.MaxTopNear(99.9, 99.9, 6.0), 1e-9);
    }

    // --- Pente : la porte de hauteur se compte en altitude ---

    private static TerrainGrid Cliff()
    {
        const int cells = 50;
        const double cell = 4.0;
        var field = new HeightField(cells, cells, cell);
        for (var j = 0; j <= cells; j++)
        {
            for (var i = 0; i <= cells; i++)
            {
                // Plateau à 30 m jusqu'à x = 112, falaise, puis le sol à 0 à partir de x = 116.
                field.SetCornerHeight(i, j, i * cell <= 112 ? 30.0 : (i * cell >= 116 ? 0.0 : 30.0 - (7.5 * ((i * cell) - 112))));
            }
        }

        var grid = new TerrainGrid(field, "falaise", new Point2(8, 100), new Point2(190, 8), new Point2(190, 8));
        for (var cz = 0; cz < cells; cz++)
        {
            for (var cx = 0; cx < cells; cx++)
            {
                grid.PaintCell(cx, cz, Surface.Fairway);
            }
        }

        return grid;
    }

    [Fact]
    public void Slope_BallFarAboveItsOwnGroundButInsideACrownOnTheHillIsStillTested()
    {
        var terrain = Cliff();
        var stand = new TreeStand(200, 200, terrain);
        stand.Add(110, TreeZ, TreeSpeciesId.Oak, 100 * 12); // au bord du plateau : la couronne déborde sur la falaise
        // La balle tombe à x = 114 où le sol est à 15 m : à 50 m d'altitude elle est à 35 m du sol, mais dans la couronne (35 à 44 m) dès 44 m.
        Assert.InRange(terrain.Sample(114, TreeZ), 14.9, 15.1);
        var draws = 0;
        for (var seed = 1; seed <= 20; seed++)
        {
            draws += Draws((ulong)seed, rng => BallSimulator.Run(terrain, new Point2(114, TreeZ), new Vec3(0, 0, 0), false, 0.0, false, startHeight: 35.0, trees: stand, season: Season.Summer, rng: rng));
        }

        Assert.True(draws > 20, $"tirages : {draws}");
    }

    // --- Intégration World → résolveur ---

    [Fact]
    public void World_TreesAndSeasonChangeTheGame()
    {
        static List<GolferView> Play(bool lattice, Season season)
        {
            var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
            var world = new World(seed: 9, terrain);
            world.Season = season;
            world.Trees.RemoveWhere(_ => true);
            if (lattice)
            {
                for (var x = 6.0; x < terrain.CellsX * terrain.CellSize; x += 6.0)
                {
                    for (var z = 6.0; z < terrain.CellsZ * terrain.CellSize; z += 6.0)
                    {
                        var tee = Math.Sqrt(((x - terrain.Tee.X) * (x - terrain.Tee.X)) + ((z - terrain.Tee.Z) * (z - terrain.Tee.Z)));
                        if (tee > 12.0)
                        {
                            world.Trees.Add(x, z, TreeSpeciesId.Oak, 100 * 12);
                        }
                    }
                }
            }

            world.SpawnGroup();
            world.Step(2400);
            return [.. world.Snapshot().Golfers];
        }

        var bare = Play(false, Season.Summer);
        var summer = Play(true, Season.Summer);
        var winter = Play(true, Season.Winter);
        Assert.NotEqual(bare, summer);
        Assert.NotEqual(summer, winter);
        Assert.Equal(summer, Play(true, Season.Summer));
    }

    [Fact]
    public void World_WiresItsTreesIntoThePhysicalResolver()
    {
        var resolver = new PhysicalShotResolver();
        var world = new World(1, Flat(), resolver);
        Assert.Same(world.Trees, resolver.Trees);
        world.Season = Season.Autumn;
        Assert.Equal(Season.Autumn, resolver.Season);
    }

    [Fact]
    public void StopProbability_IsExactOnZeroToEight()
    {
        for (var a = 0.0; a <= 8.0; a += 0.01)
        {
            var p = TreeCollision.StopProbability(a);
            Assert.InRange(p, 0.0, 1.0);
            Assert.Equal(1.0 - Math.Exp(-a), p, 1e-6);
        }

        Assert.Equal(0.0, TreeCollision.StopProbability(0.0));
    }

    // --- Croissance et essences (C02 lot 3) ---

    [Fact]
    public void Birch_AtTenYears_IsTallerThanOak()
    {
        var birch = Tree.Height(TreeSpeciesTable.Get(TreeSpeciesId.Birch), 10 * 12);
        var oak = Tree.Height(TreeSpeciesTable.Get(TreeSpeciesId.Oak), 10 * 12);
        Assert.True(birch > oak, $"bouleau {birch:0.0} m, chêne {oak:0.0} m");
    }

    [Fact]
    public void Species_AreDistinctAndHaveSaplingsBetweenOneAndThreeMeters()
    {
        foreach (var id in new[] { TreeSpeciesId.Oak, TreeSpeciesId.Birch, TreeSpeciesId.Fir })
        {
            var sp = TreeSpeciesTable.Get(id);
            Assert.InRange(sp.SaplingHeight, 1.0, 3.0);
            Assert.InRange(sp.AdultHeight, 15.0, 25.0);
        }

        Assert.True(TreeSpeciesTable.Get(TreeSpeciesId.Oak).AdultCrownDiameter > TreeSpeciesTable.Get(TreeSpeciesId.Birch).AdultCrownDiameter);
        Assert.True(TreeSpeciesTable.Get(TreeSpeciesId.Birch).CrownDensity(Season.Summer) < TreeSpeciesTable.Get(TreeSpeciesId.Oak).CrownDensity(Season.Summer));
        Assert.Equal(TreeSpeciesId.Fir, TreeSpeciesTable.FindByName("Sapin")!.Id);
        Assert.Null(TreeSpeciesTable.FindByName("Feuillu"));
    }

    [Fact]
    public void AdvanceYear_RaisesHeightsAndCellTops_AndBallThatClearedSaplingIsNowTested()
    {
        var terrain = Flat();
        var world = new World(3, terrain);
        world.Trees.Add(TreeX, TreeZ, TreeSpeciesId.Oak, 0);
        var h0 = world.Trees.Trees[0].Height();
        var top0 = world.Trees.MaxTopAt(TreeX, TreeZ);
        var version = world.Trees.Version;

        // Un plant de 2 m : la balle à 9,5 m passe sans aucun tirage.
        var rng = new DeterministicRng(5);
        var before = rng.StateHash();
        Fly(terrain, world.Trees, Season.Summer, 5, 9.5, rng: rng);
        Assert.Equal(before, rng.StateHash());

        for (var y = 0; y < 50; y++)
        {
            world.AdvanceYear();
        }

        Assert.Equal(version + 50, world.Trees.Version);
        Assert.True(world.Trees.Trees[0].Height() > h0);
        Assert.Equal(12 * 50, world.Trees.Trees[0].AgeMonths);
        Assert.True(world.Trees.MaxTopAt(TreeX, TreeZ) > top0);
        Assert.Equal(world.Trees.Trees[0].Height(), world.Trees.MaxTopAt(TreeX, TreeZ), 1e-9);
        rng = new DeterministicRng(5);
        before = rng.StateHash();
        Fly(terrain, world.Trees, Season.Summer, 5, 9.5, rng: rng);
        Assert.NotEqual(before, rng.StateHash());
    }

    [Fact]
    public void AdvanceTrees_BumpsVersionOncePerCall()
    {
        var world = new World(3, Flat());
        for (var i = 0; i < 20; i++)
        {
            world.Trees.Add(10 + (i * 5), 10, TreeSpeciesId.Birch, 0);
        }

        var v = world.Trees.Version;
        Assert.Equal(20, world.AdvanceTrees(1));
        Assert.Equal(1.0, world.Trees.MeanAgeMonths, 1e-9);
        Assert.Equal(v + 1, world.Trees.Version);
        Assert.Equal(v + 1, world.Trees.Version);
        Assert.Equal(0, world.AdvanceTrees(0));
        Assert.Equal(v + 1, world.Trees.Version);
    }

    [Fact]
    public void Gate_OnHillyTerrain_UsesTheCellTopNotTheMapMaximum()
    {
        var terrain = Cliff();
        var stand = new TreeStand(200, 200, terrain);
        stand.Add(60, TreeZ, TreeSpeciesId.Oak, 100 * 12); // plateau à 30 m : cime à 47,5 m
        stand.Add(180, TreeZ, TreeSpeciesId.Oak, 0); // plant en bas, sol à 0 : cime à 2 m
        Assert.Equal(47.5, stand.MaxTopAt(60, TreeZ), 1e-9);
        Assert.Equal(2.0, stand.MaxTopAt(180, TreeZ), 1e-9);
        Assert.Equal(double.NegativeInfinity, stand.MaxTopAt(150, 20));

        // Au-dessus du plant, à 10 m d'altitude, la balle est sous le maximum de la carte (47,5 m) mais au-dessus de la cime locale :
        // elle ne tire rien et le vol est celui d'un monde sans arbre.
        var rng = new DeterministicRng(7);
        var before = rng.StateHash();
        var withTrees = BallSimulator.Run(terrain, new Point2(175, TreeZ), new Vec3(10, 0, 0), false, 0.0, true, startHeight: 10.0, trees: stand, season: Season.Summer, rng: rng);
        var without = BallSimulator.Run(terrain, new Point2(175, TreeZ), new Vec3(10, 0, 0), false, 0.0, true, startHeight: 10.0);
        Assert.Equal(before, rng.StateHash());
        Assert.Equal(without.FirstLanding, withTrees.FirstLanding);
    }

    [Fact]
    public void GrowthIsDeterministic_SameSeedSameAdvances_SameChecksum()
    {
        static ulong Run(int years)
        {
            var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
            var world = new World(11, terrain);
            for (var y = 0; y < years; y++)
            {
                world.AdvanceYear();
            }

            world.AdvanceTrees(5);
            return world.Checksum();
        }

        Assert.Equal(Run(7), Run(7));
        Assert.NotEqual(Run(7), Run(8));
    }

    [Fact]
    public void HoleLoader_RejectsRetiredSpeciesWithAClearMessage()
    {
        var json = System.IO.File.ReadAllText(TerrainTests.HolePath());
        var withTrees = json.TrimEnd().TrimEnd('}').TrimEnd() + ", \"trees\": [{\"x\": 10, \"z\": 8, \"species\": \"Feuillu\", \"age\": 30}] }";
        var ex = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(withTrees, 2));
        Assert.Contains("n'existe plus", ex.Message);
        Assert.Contains("Chêne", ex.Message);
    }

    [Fact]
    public void MaxTopAt_FollowsRaisedGround()
    {
        var terrain = Flat();
        var world = new World(3, terrain);
        world.Trees.Add(100, 100, TreeSpeciesId.Fir, 100 * 12);
        Assert.Equal(20.0, world.Trees.MaxTopAt(100, 100), 1e-9);
        world.RaiseTerrain(new Point2(100, 100), 12.0, 3.0);
        world.Step(1);
        Assert.True(world.Trees.MaxTopAt(100, 100) > 20.0);
    }
}
