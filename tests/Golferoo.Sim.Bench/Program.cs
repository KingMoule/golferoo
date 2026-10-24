using System.Diagnostics;
using System.Text.Json;
using Golferoo.Sim;

// Banc de mesure de la simulation, à lancer en Release :
//   dotnet run -c Release --project tests/Golferoo.Sim.Bench [-- --out bench.json]
// Le squelette (lot 1 de C01) ne mesure que le tick à vide ; les lots 2 et 3 ajoutent
// terrain, champs de flux et mode charge à 200 golfeurs.

var outPath = args.SkipWhile(a => a != "--out").Skip(1).FirstOrDefault();

const int warmupTicks = 1_000;
const int measuredTicks = 10_000;

var world = new World(seed: 1);
world.Step(warmupTicks);

var samples = new double[measuredTicks];
for (var i = 0; i < measuredTicks; i++)
{
    var start = Stopwatch.GetTimestamp();
    world.Step();
    samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
}

Array.Sort(samples);
var report = new
{
    benchmark = "tick-empty",
    ticks = measuredTicks,
    meanMs = samples.Average(),
    p99Ms = samples[(int)(measuredTicks * 0.99)],
    maxMs = samples[^1],
    checksum = world.Checksum().ToString("x16"),
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
};

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (outPath is not null)
{
    File.WriteAllText(outPath, json);
}
