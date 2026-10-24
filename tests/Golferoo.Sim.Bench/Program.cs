using System.Diagnostics;
using System.Text.Json;
using Golferoo.Sim;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;

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

    // Un champ de flux sur une carte plate de 512 x 512 cellules.
    var big = new TerrainGrid(new HeightField(512, 512, 2.0), "plat", new Point2(2, 2), new Point2(1000, 1000), new Point2(1000, 1000));
    for (var z = 0; z < 512; z++)
    {
        for (var x = 0; x < 512; x++)
        {
            big.PaintCell(x, z, Surface.Fairway);
        }
    }

    var bigField = Measure(20, 5, () => FlowField.Build(big, 256, 256));

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
        && holeField.Mean < holeFieldLimitMs && bigField.Mean < bigFieldLimitMs && retouch.P99 < retouchLimitMs;
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
        flowField512 = new { meanMs = bigField.Mean, maxMs = bigField.Max, limitMs = bigFieldLimitMs, passed = bigField.Mean < bigFieldLimitMs },
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

var report = new
{
    benchmark = "c01-lot3",
    passed = allPassed,
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

static (double Mean, double P99, double Max) Measure(int runs, int warmup, Action action)
{
    for (var i = 0; i < warmup; i++)
    {
        action();
    }

    var samples = new double[runs];
    for (var i = 0; i < runs; i++)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    Array.Sort(samples);
    return (samples.Average(), samples[(int)(runs * 0.99)], samples[^1]);
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
