using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C21 lot 2, critère 5 : des choix non optimaux, plus fréquents chez les faibles, toujours plausibles et déterministes.</summary>
public class MistakeTests(ITestOutputHelper output)
{
    private const int Holes = 270;
    private const int Chunks = 6;

    private static TerrainGrid Load() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    /// <summary>
    /// Joue <paramref name="holes"/> trous de « Le neuf » au handicap <paramref name="handicap"/>, comme <see cref="PhysicalShotResolver.Resolve"/>
    /// (planification avec le RNG du coup, puis exécution), et rend pour chaque coup planifié le rang du choix parmi les finalistes
    /// (0 : le meilleur) et son écart de score au meilleur.
    /// </summary>
    private static List<(int Rank, double Gap)> Choices(double handicap, int holes, int chunks)
    {
        var terrain = Load();
        var world = new World(1, terrain);
        var skill = SkillLevels.FromHandicap(handicap);
        var profile = GolferProfile.Of(Archetype.Neutral);
        var all = new List<(int, double)>[chunks];
        Parallel.For(0, chunks, chunk =>
        {
            var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season, MapCapacity = World.MapCapacityFor(terrain) };
            var list = all[chunk] = [];
            for (var n = chunk; n < holes; n += chunks)
            {
                var hole = terrain.Course.Holes[n % terrain.Course.Count];
                var rng = new DeterministicRng(0x3157A4EUL + ((ulong)n * 0x9E3779B97F4A7C15UL));
                var ball = hole.Tee;
                var count = 0;
                while (count < World.MaxStrokes)
                {
                    var request = new ShotRequest(ball, hole.Hole, count, skill);
                    ShotResult r;
                    if (terrain.SurfaceAt(ball.X, ball.Z) == Surface.Green)
                    {
                        r = resolver.Resolve(request, terrain, rng);
                    }
                    else
                    {
                        var job = new PlanJob();
                        resolver.Advance(job, request, terrain, int.MaxValue, rng);
                        var plan = job.Result!.Value;
                        if (job.Finals.Count > 0)
                        {
                            var scores = PhysicalShotResolver.Scores(job, profile);
                            var best = scores.Select((s, k) => (s.Neutral, k)).Min().k;
                            var chosen = Enumerable.Range(0, scores.Length).First(k => job.Candidates[k].Plan == plan);
                            var rank = scores.Count(s => s.Neutral < scores[chosen].Neutral);
                            list.Add((rank, scores[chosen].Neutral - scores[best].Neutral));
                        }

                        r = resolver.ExecutePlan(request, plan, terrain, rng);
                    }

                    count += 1 + r.PenaltyStrokes;
                    ball = r.Ball;
                    if (r.Holed)
                    {
                        break;
                    }
                }
            }
        });

        return [.. all.SelectMany(l => l)];
    }

    [Theory]
    [InlineData(36.0, 0.20, 0.03)]
    [InlineData(0.0, 0.05, 0.02)]
    public void NonOptimalChoices_FollowTheHandicap_AndStayWithinHalfAStroke(double handicap, double rate, double tolerance)
    {
        var choices = Choices(handicap, Holes, Chunks);
        var wrong = choices.Count(c => c.Rank > 0);
        var share = (double)wrong / choices.Count;
        output.WriteLine($"handicap {handicap} : {wrong} choix non optimaux sur {choices.Count} coups ({share:P1}), écart max {choices.Max(c => c.Gap):F1} m");
        Assert.InRange(share, rate - tolerance, rate + tolerance);
        Assert.All(choices, c => Assert.InRange(c.Gap, 0.0, PhysicalShotResolver.MaxMistakeCost));
        Assert.All(choices, c => Assert.InRange(c.Rank, 0, 2));
    }

    [Fact]
    public void NonOptimalChoices_SameSeed_SameChoices()
    {
        Assert.Equal(Choices(36.0, 18, 2), Choices(36.0, 18, 2));
    }

    [Fact]
    public void MistakeChance_IsFivePercentPlusFifteenTimesHandicapOver36()
    {
        Assert.Equal(0.05, SkillLevels.MistakeChance(SkillLevels.FromHandicap(0)), 12);
        Assert.Equal(0.125, SkillLevels.MistakeChance(SkillLevels.FromHandicap(18)), 12);
        Assert.Equal(0.20, SkillLevels.MistakeChance(SkillLevels.FromHandicap(36)), 12);
        Assert.Equal(18.0, SkillLevels.ToHandicap(SkillLevels.FromHandicap(18)), 12);
    }
}
