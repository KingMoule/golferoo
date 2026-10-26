using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C09 lot 1 : le planificateur voit les arbres, le golfeur ressort de la forêt au lieu de retaper dedans.</summary>
public class ForestExitTests
{
    /// <summary>Lisière sud de la forêt du coude (tuile 18,5 / 26,5, dans le rough) : la ligne droite au trou traverse la forêt, le fairway est à côté.</summary>
    private static readonly Point2 Edge = new(18.5 * 8, 26.5 * 8);

    private static (TerrainGrid Terrain, PhysicalShotResolver Resolver) Setup()
    {
        var terrain = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);
        var world = new World(1, terrain);
        return (terrain, new PhysicalShotResolver { Trees = world.Trees, Season = world.Season });
    }

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>Joue des trous de niveau bogey depuis la lisière ; rend le nombre de coups maximal, les retapes dans les arbres et une empreinte.</summary>
    private static (int MaxStrokes, int Retaps, ulong Hash) PlayFromEdge(int games = 100)
    {
        var (terrain, resolver) = Setup();
        var worst = 0;
        var retaps = 0;
        var hash = 14695981039346656037UL;
        for (ulong seed = 1; seed <= (ulong)games; seed++)
        {
            var rng = new DeterministicRng(seed);
            var ball = Edge;
            var strokes = 0;
            var lastTrees = false;
            var lastStart = ball;
            while (strokes < World.MaxStrokes)
            {
                var r = resolver.Resolve(new ShotRequest(ball, terrain.Hole, strokes, SkillLevels.Bogey), terrain, rng);
                var inTrees = r.Outcome == ShotOutcome.Trees;
                if (inTrees && lastTrees && Dist(lastStart, ball) < 5.0)
                {
                    retaps++;
                }

                lastTrees = inTrees;
                lastStart = ball;
                strokes += 1 + r.PenaltyStrokes;
                ball = r.Ball;
                hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(ball.X)) * 1099511628211UL;
                hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(ball.Z)) * 1099511628211UL;
                if (r.Holed)
                {
                    break;
                }
            }

            worst = Math.Max(worst, strokes);
        }

        return (worst, retaps, hash);
    }

    [Fact]
    public void EdgeBall_TheStraightLineCrossesTheWoods()
    {
        var (terrain, _) = Setup();
        var woods = 0;
        for (var i = 1; i < 100; i++)
        {
            woods += terrain.SurfaceAt(Edge.X + ((terrain.Hole.X - Edge.X) * i / 100.0), Edge.Z + ((terrain.Hole.Z - Edge.Z) * i / 100.0)) == Surface.Trees ? 1 : 0;
        }

        Assert.NotEqual(Surface.Trees, terrain.SurfaceAt(Edge.X, Edge.Z));
        Assert.True(woods > 30, $"{woods} points sur 99 dans les bois");
    }

    [Fact]
    public void BogeyGolfersAtTheWoodsEdge_NeverRetapIntoTheTrees_NorPickUp()
    {
        var (worst, retaps, _) = PlayFromEdge();

        Assert.Equal(0, retaps);
        Assert.True(worst < World.MaxStrokes, $"coups maximaux : {worst}");
    }

    [Fact]
    public void Planning_IsDeterministic()
    {
        Assert.Equal(PlayFromEdge(10).Hash, PlayFromEdge(10).Hash);
    }
}
