using System.Diagnostics;
using System.Text.Json;
using Golferoo.Sim;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Weather;

// Banc de mesure de la simulation, à lancer en Release :
//   dotnet run -c Release --project tests/Golferoo.Sim.Bench [-- --out bench.json]
// Mesure aux facteurs de subdivision 2, 3 et 4 les seuils du critère 5 de la spec C01.
// Code de sortie non nul si un seuil échoue.

var outPath = args.SkipWhile(a => a != "--out").Skip(1).FirstOrDefault();
var holePath = FindHole();

const double tickMeanLimitMs = 2.0;
const double tickP99LimitMs = 5.0;
const double holeFieldLimitMs = 5.0;
const double bigFieldLimitMs = 20.0;
const double retouchLimitMs = 16.0;
const double treeFlightRatioLimit = 1.2;
const double treeGrowLimitMs = 5.0;
const double windFlightRatioLimit = 1.05;
const double windPlanRatioLimit = 1.2;
const double dayChangeLimitMs = 5.0;
const double puddleFloodLimitMs = 20.0;

// Carte plate de 512 x 512 cellules, la même à tous les facteurs.
var big = new TerrainGrid(new HeightField(512, 512, 2.0), "plat", new Point2(2, 2), new Point2(1000, 1000), new Point2(1000, 1000));
for (var z = 0; z < 512; z++)
{
    for (var x = 0; x < 512; x++)
    {
        big.PaintCell(x, z, Surface.Fairway);
    }
}

// Tour à blanc : le JIT à paliers optimise le code avant la première mesure,
// sinon le premier facteur mesuré paie l'échauffement.
{
    var warmup = new World(seed: 1, HoleLoader.Load(holePath, 2));
    warmup.SetLoadMode(200);
    var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
    while (Stopwatch.GetTimestamp() < until)
    {
        warmup.Step();
    }

    // Même chose pour le champ 512 x 512 : sa boucle tourne longtemps par appel, et sans ce tour le facteur 2,
    // mesuré en premier, tournait encore en partie sur du code non optimisé (25,8 ms de médiane en CI contre 9,4 ms
    // au facteur 4, sur la même carte). La pause laisse le JIT finir la compilation optimisée en arrière-plan.
    for (var i = 0; i < 60; i++)
    {
        FlowField.Build(big, 256, 256);
    }

    Thread.Sleep(500);
}

var factors = new List<object>();
var allPassed = true;
foreach (var factor in new[] { 2, 3, 4 })
{
    var terrain = HoleLoader.Load(holePath, factor);

    // Tick à 200 golfeurs en mode charge.
    var world = new World(seed: 1, terrain);
    world.SetLoadMode(200);
    world.Step(1_000);
    const int measuredTicks = 5_000;
    var ticks = new double[measuredTicks];
    for (var i = 0; i < measuredTicks; i++)
    {
        var start = Stopwatch.GetTimestamp();
        world.Step();
        ticks[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    Array.Sort(ticks);
    var tickMean = ticks.Average();
    var tickP99 = ticks[(int)(measuredTicks * 0.99)];

    // Un champ de flux sur la carte du trou.
    var hx = terrain.CellXOf(terrain.Hole.X);
    var hz = terrain.CellZOf(terrain.Hole.Z);
    var holeField = Measure(200, 20, () => FlowField.Build(terrain, hx, hz));

    // Un champ de flux sur la carte plate de 512 x 512 cellules, jugé sur la médiane : sur un runner partagé,
    // la moyenne est tirée par les pauses du ramasse-miettes (chaque champ alloue environ 2 Mo).
    var bigField = Measure(50, 30, () => FlowField.Build(big, 256, 256));

    // Retouche de 5 x 5 cellules, champs de flux compris (cache : trou, tee, sortie et dix autres destinations).
    var retouchWorld = new World(seed: 2, terrain);
    var cache = retouchWorld.Flow!;
    cache.Get(terrain.Hole);
    cache.Get(terrain.Tee);
    cache.Get(retouchWorld.ExitPoint);
    for (var i = 0; i < 10; i++)
    {
        cache.Get((terrain.CellsX * (i + 1) / 12), terrain.CellsZ / 2);
    }

    var flip = false;
    var retouch = Measure(300, 20, () =>
    {
        flip = !flip;
        for (var dz = 0; dz < 5; dz++)
        {
            for (var dx = 0; dx < 5; dx++)
            {
                terrain.PaintCell((terrain.CellsX / 3) + dx, (terrain.CellsZ / 3) + dz, flip ? Surface.Bunker : Surface.Fairway);
            }
        }

        cache.Refresh();
    });

    var passed = tickMean < tickMeanLimitMs && tickP99 < tickP99LimitMs
        && holeField.Mean < holeFieldLimitMs && bigField.Median < bigFieldLimitMs && retouch.P99 < retouchLimitMs;
    allPassed &= passed;
    factors.Add(new
    {
        factor,
        cells = new { x = terrain.CellsX, z = terrain.CellsZ },
        passed,
        tick200 = new
        {
            golfers = 200,
            ticks = measuredTicks,
            meanMs = tickMean,
            p99Ms = tickP99,
            maxMs = ticks[^1],
            limitMeanMs = tickMeanLimitMs,
            limitP99Ms = tickP99LimitMs,
            passed = tickMean < tickMeanLimitMs && tickP99 < tickP99LimitMs,
        },
        flowFieldHole = new { meanMs = holeField.Mean, maxMs = holeField.Max, limitMs = holeFieldLimitMs, passed = holeField.Mean < holeFieldLimitMs },
        flowField512 = new { meanMs = bigField.Mean, medianMs = bigField.Median, maxMs = bigField.Max, limitMs = bigFieldLimitMs, passed = bigField.Median < bigFieldLimitMs },
        retouch5x5 = new
        {
            cachedFields = cache.Count,
            meanMs = retouch.Mean,
            p99Ms = retouch.P99,
            maxMs = retouch.Max,
            limitMs = retouchLimitMs,
            passed = retouch.P99 < retouchLimitMs,
        },
        checksum = world.Checksum().ToString("x16"),
    });
}

// Vol d'une balle sur la carte plate de 512 x 512 avec 20 000 arbres (C02 lot 2), comparé au même vol sans arbres.
// Les mêmes 400 coups de driver (départs et directions tirés d'un RNG fixe) sont joués des deux côtés ; avec les arbres,
// la balle est arrêtée bien plus tôt (forêt dense partout) : le critère porte sur le coût d'un coup, et le coût par seconde
// simulée est donné pour information (un pas dans la canopée coûte plus qu'un pas de roulement sur le sol).
var treeFlight = MeasureTreeFlight(big);
allPassed &= treeFlight.Ratio <= treeFlightRatioLimit;

// Vieillir 20 000 arbres d'un mois (C02 lot 3) : âge, index par case et version. Médiane de 200 passes.
var grow = MeasureTreeGrowth(big);
allPassed &= grow.Median < treeGrowLimitMs;

// Vent (C04 lot 2) : les mêmes 400 drives sur la carte plate avec et sans vent de 14 m/s, coût par seconde simulée
// (le vent change la durée du vol) ; et planifier un drive sur le trou avec et sans ce vent.
var wind = MeasureWind(big, HoleLoader.Load(holePath, 2));
allPassed &= wind.FlightRatio <= windFlightRatioLimit && wind.PlanRatio <= windPlanRatioLimit;

// Changement de jour (C04 lot 3) sur une carte de 512 x 512 avec 20 000 arbres adultes, en tempête : météo, sol mouillé
// recalculé (humidité qui change chaque jour) et chutes. Médiane de 30 changements de jour.
var dayChange = MeasureDayChange();
allPassed &= dayChange.Median < dayChangeLimitMs;

// Cuvettes des flaques (C04 lot 4) : remplissage depuis les bords sur 320 x 320 cellules (102 400) après une retouche du relief,
// médiane de 11 passes. Critère sur un relief de collines et de creux comme en peint le pinceau ; le bruit pur à chaque coin
// (pire cas, des creux partout) est donné pour information. Au sec, rien n'est calculé.
var flood = MeasurePuddleFlood(noise: false);
var floodNoise = MeasurePuddleFlood(noise: true);
allPassed &= flood.Median < puddleFloodLimitMs;

var report = new
{
    benchmark = "c04-lot4",
    puddleFlood = new { cells = 320 * 320, medianMs = flood.Median, maxMs = flood.Max, noiseMedianMs = floodNoise.Median, limitMs = puddleFloodLimitMs, passed = flood.Median < puddleFloodLimitMs },
    dayChange = new { trees = dayChange.Trees, medianMs = dayChange.Median, maxMs = dayChange.Max, fallen = dayChange.Fallen, limitMs = dayChangeLimitMs, passed = dayChange.Median < dayChangeLimitMs },
    wind = new
    {
        speed = 14.0,
        flightRatioPerSimulatedSecond = wind.FlightRatio,
        flightLimit = windFlightRatioLimit,
        planCalmMs = wind.PlanCalmMs,
        planWindyMs = wind.PlanWindyMs,
        planRatio = wind.PlanRatio,
        planLimit = windPlanRatioLimit,
        passed = wind.FlightRatio <= windFlightRatioLimit && wind.PlanRatio <= windPlanRatioLimit,
    },
    passed = allPassed,
    treeGrowth = new { trees = grow.Trees, monthMeanMs = grow.Mean, monthMedianMs = grow.Median, monthMaxMs = grow.Max, limitMs = treeGrowLimitMs, passed = grow.Median < treeGrowLimitMs },
    treeFlight = new
    {
        trees = treeFlight.Trees,
        flights = treeFlight.Flights,
        noTreesMsPerFlight = treeFlight.NoTreesMs,
        withTreesMsPerFlight = treeFlight.WithTreesMs,
        ratio = treeFlight.Ratio,
        simulatedSecondsPerFlightNoTrees = treeFlight.NoTreesSeconds,
        simulatedSecondsPerFlightWithTrees = treeFlight.WithTreesSeconds,
        ratioPerSimulatedSecond = treeFlight.RatioPerSimulatedSecond,
        limit = treeFlightRatioLimit,
        passed = treeFlight.Ratio <= treeFlightRatioLimit,
    },
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    factors,
};

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (outPath is not null)
{
    File.WriteAllText(outPath, json);
}

return allPassed ? 0 : 1;

static (int Trees, double Mean, double Median, double Max) MeasureTreeGrowth(TerrainGrid map)
{
    const int treeCount = 20_000;
    var size = map.CellsX * map.CellSize;
    var stand = new TreeStand(size, size, map);
    var placer = new DeterministicRng(13);
    for (var i = 0; i < treeCount; i++)
    {
        stand.Add(2 + (placer.NextDouble() * (size - 4)), 2 + (placer.NextDouble() * (size - 4)), (TreeSpeciesId)(i % TreeSpeciesTable.Count), (int)(placer.NextDouble() * 30 * 12));
    }

    // Un mois de jeu : les arbres vieillissent et le sol est recalculé (saison changée à chaque passe, lecture qui force le recalcul).
    var summer = true;
    var m = Measure(200, 20, () =>
    {
        stand.Grow(Golferoo.Sim.Time.Calendar.TreeMonthsPerMonth);
        summer = !summer;
        map.Soil.Season = summer ? Season.Summer : Season.Autumn;
        map.Soil.RollResistanceAt(0, 0);
    });
    return (treeCount, m.Mean, m.Median, m.Max);
}

static (int Trees, int Flights, double NoTreesMs, double WithTreesMs, double Ratio, double RatioPerSimulatedSecond, double NoTreesSeconds, double WithTreesSeconds) MeasureTreeFlight(TerrainGrid map)
{
    const int treeCount = 20_000;
    const int flights = 400;
    var size = map.CellsX * map.CellSize;
    var stand = new TreeStand(size, size, map);
    var placer = new DeterministicRng(11);
    for (var i = 0; i < treeCount; i++)
    {
        var species = (TreeSpeciesId)(i % TreeSpeciesTable.Count);
        stand.Add(2 + (placer.NextDouble() * (size - 4)), 2 + (placer.NextDouble() * (size - 4)), species, (int)((20 + (placer.NextDouble() * 40)) * 12));
    }

    var aim = new DeterministicRng(12);
    var starts = new (Point2 Start, Vec3 Velocity)[flights];
    for (var i = 0; i < flights; i++)
    {
        var angle = aim.NextDouble() * 2 * Math.PI;
        var club = Clubs.Driver;
        starts[i] = (
            new Point2(150 + (aim.NextDouble() * (size - 300)), 150 + (aim.NextDouble() * (size - 300))),
            new Vec3(Math.Cos(angle) * club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, Math.Sin(angle) * club.LaunchCos * club.Speed));
    }

    double Round(TreeStand? trees, out double simulatedSeconds)
    {
        var rng = new DeterministicRng(13);
        var seconds = 0.0;
        var t0 = Stopwatch.GetTimestamp();
        foreach (var (start, velocity) in starts)
        {
            seconds += BallSimulator.Run(map, start, velocity, rolling: false, BallSimulator.DefaultDrag, recordPath: false, trees: trees, rng: rng).Duration;
        }

        simulatedSeconds = seconds;
        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    for (var i = 0; i < 5; i++)
    {
        Round(null, out _);
        Round(stand, out _);
    }

    // Médiane de 15 tours de chaque côté, en alternance.
    var none = new double[15];
    var with = new double[15];
    double secNone = 0, secWith = 0;
    for (var i = 0; i < 15; i++)
    {
        none[i] = Round(null, out secNone);
        with[i] = Round(stand, out secWith);
    }

    Array.Sort(none);
    Array.Sort(with);
    var noTrees = none[7] / flights;
    var withTrees = with[7] / flights;
    return (treeCount, flights, noTrees, withTrees, withTrees / noTrees, (with[7] / secWith) / (none[7] / secNone), secNone / flights, secWith / flights);
}

static (double FlightRatio, double PlanCalmMs, double PlanWindyMs, double PlanRatio) MeasureWind(TerrainGrid map, TerrainGrid hole)
{
    const int flights = 400;
    var windy = new Vec3(14.0 * 0.6, 0, 14.0 * 0.8);
    var aim = new DeterministicRng(21);
    var size = map.CellsX * map.CellSize;
    var starts = new (Point2 Start, Vec3 Velocity)[flights];
    for (var i = 0; i < flights; i++)
    {
        var angle = aim.NextDouble() * 2 * Math.PI;
        var club = Clubs.Driver;
        starts[i] = (
            new Point2(300 + (aim.NextDouble() * (size - 600)), 300 + (aim.NextDouble() * (size - 600))),
            new Vec3(Math.Cos(angle) * club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, Math.Sin(angle) * club.LaunchCos * club.Speed));
    }

    double Round(Vec3 w)
    {
        var seconds = 0.0;
        var t0 = Stopwatch.GetTimestamp();
        foreach (var (start, velocity) in starts)
        {
            seconds += BallSimulator.Run(map, start, velocity, rolling: false, BallSimulator.DefaultDrag, recordPath: false, wind: w).Duration;
        }

        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds / seconds;
    }

    var request = new ShotRequest(hole.Tee, hole.Hole, 0);
    double Plan(Vec3 w)
    {
        var resolver = new PhysicalShotResolver { Wind = w };
        var t0 = Stopwatch.GetTimestamp();
        resolver.Resolve(request, hole, new DeterministicRng(3));
        return Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    for (var i = 0; i < 5; i++)
    {
        Round(default);
        Round(windy);
        Plan(default);
        Plan(windy);
    }

    // Médianes de passes alternées.
    var calm = new double[15];
    var blow = new double[15];
    var planCalm = new double[31];
    var planWindy = new double[31];
    for (var i = 0; i < 15; i++)
    {
        calm[i] = Round(default);
        blow[i] = Round(windy);
    }

    for (var i = 0; i < 31; i++)
    {
        planCalm[i] = Plan(default);
        planWindy[i] = Plan(windy);
    }

    Array.Sort(calm);
    Array.Sort(blow);
    Array.Sort(planCalm);
    Array.Sort(planWindy);
    return (blow[7] / calm[7], planCalm[15], planWindy[15], planWindy[15] / planCalm[15]);
}

static (double Median, double Max) MeasurePuddleFlood(bool noise)
{
    var map = new TerrainGrid(new HeightField(320, 320, 2.0), "relief", new Point2(2, 2), new Point2(600, 600), new Point2(600, 600));
    var rng = new DeterministicRng(41);
    for (var j = 0; j <= 320; j++)
    {
        for (var i = 0; i <= 320; i++)
        {
            // Collines de 60 à 100 m et creux de 1 à 2 m, ou bruit pur.
            map.SetCornerHeight(i, j, noise ? rng.NextDouble() * 2.0 : (3.0 * Math.Sin(i * 0.1) * Math.Cos(j * 0.07)) + (1.5 * Math.Sin((i + j) * 0.23)));
        }
    }

    map.Soil.Wetness = 1.0;
    var samples = new double[11];
    for (var k = -2; k < samples.Length; k++)
    {
        map.SetCornerHeight(160, 160, rng.NextDouble() * 2.0);
        var t0 = Stopwatch.GetTimestamp();
        map.Puddles.DepthAt(0, 0);
        if (k >= 0)
        {
            samples[k] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        }
    }

    Array.Sort(samples);
    return (samples[samples.Length / 2], samples[^1]);
}

static (int Trees, double Median, double Max, int Fallen) MeasureDayChange()
{
    const int treeCount = 20_000;
    var map = new TerrainGrid(new HeightField(512, 512, 2.0), "plat", new Point2(2, 2), new Point2(1000, 1000), new Point2(1000, 1000));
    for (var z = 0; z < 512; z++)
    {
        for (var x = 0; x < 512; x++)
        {
            map.PaintCell(x, z, Surface.Rough);
        }
    }

    var world = new World(seed: 31, map) { Climate = Climate.Calm };
    var placer = new DeterministicRng(32);
    for (var i = 0; i < treeCount; i++)
    {
        world.Trees.Add(2 + (placer.NextDouble() * 1020), 2 + (placer.NextDouble() * 1020), (TreeSpeciesId)(i % TreeSpeciesTable.Count), 40 * 12);
    }

    var samples = new double[30];
    for (var i = -5; i < samples.Length; i++)
    {
        // Pluie un jour sur deux, forte : l'humidité du sol change à chaque changement de jour, et il y a des chutes.
        while (world.Weather.Sky != (i % 2 == 0 ? Sky.HeavyRain : Sky.Sun))
        {
            world.CycleSky();
        }

        world.Step(Calendar.TicksPerDay - 1 - (int)(world.Tick % Calendar.TicksPerDay));
        map.Soil.RollResistanceAt(0, 0);
        var t0 = Stopwatch.GetTimestamp();
        world.Step();
        map.Soil.RollResistanceAt(0, 0); // le sol se recalcule à la première lecture : on la compte
        if (i >= 0)
        {
            samples[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        }
    }

    var fallen = world.Trees.Trees.Count(t => t.State == TreeState.Fallen);
    Array.Sort(samples);
    return (treeCount, samples[samples.Length / 2], samples[^1], fallen);
}

static (double Mean, double P99, double Max, double Median) Measure(int runs, int warmup, Action action)
{
    for (var i = 0; i < warmup; i++)
    {
        action();
    }

    // Ramasse-miettes avant la mesure : une collecte héritée des étapes précédentes ne doit pas tomber dans l'échantillon.
    GC.Collect();
    GC.WaitForPendingFinalizers();

    var samples = new double[runs];
    for (var i = 0; i < runs; i++)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    Array.Sort(samples);
    return (samples.Average(), samples[(int)(runs * 0.99)], samples[^1], samples[runs / 2]);
}

static string FindHole()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Golferoo.sln")))
    {
        dir = dir.Parent;
    }

    return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Golferoo.sln introuvable."), "data", "holes", "proto-par4.json");
}
