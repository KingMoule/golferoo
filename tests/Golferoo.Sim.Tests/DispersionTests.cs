using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>
/// C21 lot 2, critère 4 : fairways et greens en régulation d'amateurs réels, sur « Le neuf », 2 000 trous par niveau
/// (graines fixes, vent nul, arbres du parcours).
/// </summary>
public class DispersionTests(ITestOutputHelper output)
{
    private const int Holes = 2000;
    private const int Chunks = 8;

    private sealed record Stats(double Fairways, double Greens, double Wide, double Strokes, string ByHole);

    private static TerrainGrid Load() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    /// <summary>
    /// Joue <see cref="Holes"/> trous (le parcours en boucle) au niveau du handicap <paramref name="handicap"/>. Fairway : drive d'un par 4 ou 5
    /// arrêté sur le fairway. Green en régulation : sur le green (ou rentré) en par − 2 coups au plus, pénalités comprises. Large : coup plein
    /// parti à plus de trois écarts types de direction (club et niveau) de la direction visée, mesuré au sommet du vol (avant tout rebond).
    /// </summary>
    private static Stats Play(double handicap)
    {
        var terrain = Load();
        var world = new World(1, terrain);
        var skill = SkillLevels.FromHandicap(handicap);
        int fairways = 0, drives = 0, greens = 0, full = 0, wide = 0, strokes = 0;
        var gate = new object();
        var byHole = new int[terrain.Course.Count, 3];
        Parallel.For(0, Chunks, chunk =>
        {
            var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
            int fw = 0, dr = 0, gr = 0, fu = 0, wi = 0, st = 0;
            for (var n = chunk; n < Holes; n += Chunks)
            {
                var hole = terrain.Course.Holes[n % terrain.Course.Count];
                var rng = new DeterministicRng(0xD15FE45EUL + ((ulong)n * 0x9E3779B97F4A7C15UL));
                var ball = hole.Tee;
                var count = 0;
                var onGreen = false;
                while (count < World.MaxStrokes)
                {
                    var from = ball;
                    var r = resolver.Resolve(new ShotRequest(ball, hole.Hole, count, skill), terrain, rng, out var plan);
                    if (plan is { Low: false } shot)
                    {
                        fu++;
                        var apex = r.Trajectory!.MaxBy(p => p.Y);
                        var (ax, az) = (apex.X - from.X, apex.Z - from.Z);
                        var angle = Math.Abs(Math.Atan2((ax * shot.DirZ) - (az * shot.DirX), (ax * shot.DirX) + (az * shot.DirZ)));
                        wi += angle > 3.0 * shot.Club.DirectionSigma * skill ? 1 : 0;
                    }

                    if (count == 0 && hole.Par >= 4)
                    {
                        dr++;
                        fw += r.Outcome == ShotOutcome.Fairway ? 1 : 0;
                        Interlocked.Add(ref byHole[n % terrain.Course.Count, 0], r.Outcome == ShotOutcome.Fairway ? 1 : 0);
                    }

                    count += 1 + r.PenaltyStrokes;
                    ball = r.Ball;
                    if (!onGreen && count <= hole.Par - 2 && (r.Holed || r.Outcome == ShotOutcome.Green))
                    {
                        onGreen = true;
                        gr++;
                        Interlocked.Increment(ref byHole[n % terrain.Course.Count, 1]);
                    }

                    if (r.Holed)
                    {
                        break;
                    }
                }

                st += Math.Min(count, World.MaxStrokes);
                Interlocked.Add(ref byHole[n % terrain.Course.Count, 2], Math.Min(count, World.MaxStrokes));
            }

            lock (gate)
            {
                (fairways, drives, greens, full, wide, strokes) = (fairways + fw, drives + dr, greens + gr, full + fu, wide + wi, strokes + st);
            }
        });

        return new Stats((double)fairways / drives, (double)greens / Holes, (double)wide / full, (double)strokes / Holes,
            string.Join(" ; ", Enumerable.Range(0, terrain.Course.Count).Select(h => $"{h + 1} (par {terrain.Course.Holes[h].Par}) : fairways {byHole[h, 0]}, greens {byHole[h, 1]}, coups {byHole[h, 2]}")));
    }

    [Theory]
    [InlineData(0.0, 0.65, 0.65)]
    [InlineData(18.0, 0.45, 0.25)]
    public void LeNeuf_FairwaysAndGreens_MatchRealAmateurs(double handicap, double fairways, double greens)
    {
        var s = Play(handicap);
        output.WriteLine($"handicap {handicap} : fairways {s.Fairways:P1}, greens {s.Greens:P1}, au-delà de 3 σ {s.Wide:P2}, coups par trou {s.Strokes:F2}");
        output.WriteLine(s.ByHole);
        Assert.InRange(s.Fairways, fairways - 0.05, fairways + 0.05);
        Assert.InRange(s.Greens, greens - 0.05, greens + 0.05);
        Assert.True(s.Wide < 0.01);
    }
}
