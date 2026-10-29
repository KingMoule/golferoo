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

    private sealed record Stats(double Fairways, double Greens, double Wide, int FullShots, string ByHole);

    private static TerrainGrid Load() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    /// <summary>
    /// Joue <see cref="Holes"/> trous (le parcours en boucle) au niveau du handicap <paramref name="handicap"/>, chacun avec sa graine (le
    /// résultat ne dépend pas du découpage entre fils). Fairway : drive d'un par 4 ou 5 arrêté sur le fairway. Green en régulation : sur le
    /// green (ou rentré) en par − 2 coups au plus, pénalités comprises ; le trou s'arrête dès que c'est joué, réussi ou non (le reste du trou
    /// ne compte pour aucun taux : c'est ce qui garde le test court). Large : coup plein parti à plus de trois écarts types de direction
    /// (club et niveau) de la direction visée, mesuré au sommet du vol (avant tout rebond), sur les coups pleins joués.
    /// </summary>
    private static Stats Play(double handicap)
    {
        var terrain = Load();
        var world = new World(1, terrain);
        var skill = SkillLevels.FromHandicap(handicap);
        var count = terrain.Course.Count;
        var drives = new int[Holes];
        var fairways = new int[Holes];
        var greens = new int[Holes];
        var full = new int[Holes];
        var wide = new int[Holes];
        Parallel.For(
            0,
            Holes,
            // Une carte de jeu par trou gardée, comme dans le monde : sans, chaque trou refait les siennes (4 par défaut pour 9 trous).
            () => new PhysicalShotResolver { Trees = world.Trees, Season = world.Season, MapCapacity = World.MapCapacityFor(terrain) },
            (n, _, resolver) =>
            {
                var hole = terrain.Course.Holes[n % count];
                var rng = new DeterministicRng(0xD15FE45EUL + ((ulong)n * 0x9E3779B97F4A7C15UL));
                var ball = hole.Tee;
                var strokes = 0;
                while (strokes <= hole.Par - 2 && greens[n] == 0)
                {
                    var from = ball;
                    var r = resolver.Resolve(new ShotRequest(ball, hole.Hole, strokes, skill), terrain, rng, out var plan);
                    if (plan is { Low: false } shot)
                    {
                        full[n]++;
                        var apex = r.Trajectory!.MaxBy(p => p.Y);
                        var (ax, az) = (apex.X - from.X, apex.Z - from.Z);
                        var angle = Math.Abs(Math.Atan2((ax * shot.DirZ) - (az * shot.DirX), (ax * shot.DirX) + (az * shot.DirZ)));
                        wide[n] += angle > 3.0 * shot.Club.DirectionSigma * skill ? 1 : 0;
                    }

                    if (strokes == 0 && hole.Par >= 4)
                    {
                        drives[n] = 1;
                        fairways[n] = r.Outcome == ShotOutcome.Fairway ? 1 : 0;
                    }

                    strokes += 1 + r.PenaltyStrokes;
                    ball = r.Ball;
                    greens[n] = strokes <= hole.Par - 2 && (r.Holed || r.Outcome == ShotOutcome.Green) ? 1 : 0;
                }

                return resolver;
            },
            _ => { });

        int Sum(int[] values, int h = -1) => Enumerable.Range(0, Holes).Where(n => h < 0 || n % count == h).Sum(n => values[n]);
        return new Stats((double)Sum(fairways) / Sum(drives), (double)Sum(greens) / Holes, (double)Sum(wide) / Sum(full), Sum(full),
            string.Join(" ; ", Enumerable.Range(0, count).Select(h => $"{h + 1} (par {terrain.Course.Holes[h].Par}) : fairways {Sum(fairways, h)}, greens {Sum(greens, h)}")));
    }

    [Theory]
    [InlineData(0.0, 0.65, 0.65)]
    [InlineData(18.0, 0.45, 0.25)]
    public void LeNeuf_FairwaysAndGreens_MatchRealAmateurs(double handicap, double fairways, double greens)
    {
        var s = Play(handicap);
        output.WriteLine($"handicap {handicap} : fairways {s.Fairways:P1}, greens {s.Greens:P1}, au-delà de 3 σ {s.Wide:P2} de {s.FullShots} coups pleins");
        output.WriteLine(s.ByHole);
        Assert.InRange(s.Fairways, fairways - 0.05, fairways + 0.05);
        Assert.InRange(s.Greens, greens - 0.05, greens + 0.05);
        Assert.True(s.Wide < 0.01);
    }
}
