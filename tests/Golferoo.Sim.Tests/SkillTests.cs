using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class SkillTests
{
    private const int Games = 200;

    /// <summary>Moyenne des coups d'un golfeur seul sur proto-par4 (vent nul, sol de départ), du tee au trou, 12 coups au plus.</summary>
    private static double MeanStrokes(double skill, int games = Games)
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var total = 0;
        Parallel.For(1, games + 1, seed =>
        {
            var resolver = new PhysicalShotResolver();
            var rng = new DeterministicRng((ulong)seed);
            var ball = terrain.Tee;
            var strokes = 0;
            while (strokes < World.MaxStrokes)
            {
                var r = resolver.Resolve(new ShotRequest(ball, terrain.Hole, strokes, skill), terrain, rng);
                strokes += 1 + r.PenaltyStrokes;
                ball = r.Ball;
                if (r.Holed)
                {
                    break;
                }
            }

            Interlocked.Add(ref total, Math.Min(strokes, World.MaxStrokes));
        });

        return (double)total / games;
    }

    [Fact]
    public void Calibration_ScratchNearPar_BogeyAboutOneStrokeWorse()
    {
        var par = HoleLoader.Load(TerrainTests.HolePath(), 2).Par;
        var scratch = MeanStrokes(SkillLevels.FromHandicap(0));
        var bogey = MeanStrokes(SkillLevels.FromHandicap(20));

        Assert.InRange(scratch - par, 0.0, 0.4);
        Assert.InRange(bogey - scratch, 0.7, 1.3);
    }

    [Fact]
    public void SkillLevels_InterpolateBetweenReferencesAndClampTo54()
    {
        Assert.Equal(SkillLevels.Scratch, SkillLevels.FromHandicap(0));
        Assert.Equal(SkillLevels.Bogey, SkillLevels.FromHandicap(20));
        Assert.Equal((SkillLevels.Scratch + SkillLevels.Bogey) / 2.0, SkillLevels.FromHandicap(10), 12);
        Assert.True(SkillLevels.FromHandicap(36) > SkillLevels.Bogey);
        Assert.Equal(SkillLevels.FromHandicap(54), SkillLevels.FromHandicap(80));
        Assert.Equal(SkillLevels.Scratch, SkillLevels.FromHandicap(-3));
    }

    [Fact]
    public void WorseSkill_ScattersMore()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var resolver = new PhysicalShotResolver();
        double Spread(double skill)
        {
            var sum = 0.0;
            for (var seed = 1; seed <= 40; seed++)
            {
                var r = resolver.Execute(terrain.Tee, Clubs.Driver, 1.0, 0.0, 1.0, terrain, new DeterministicRng((ulong)seed), skill);
                sum += Math.Abs(r.Ball.Z - terrain.Tee.Z);
            }

            return sum / 40;
        }

        Assert.True(Spread(2.0) > Spread(1.0));
    }

    [Fact]
    public void SkillOne_IsTheDefault_BitForBit()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var resolver = new PhysicalShotResolver();
        var a = resolver.Resolve(new ShotRequest(terrain.Tee, terrain.Hole, 0), terrain, new DeterministicRng(9));
        var b = resolver.Resolve(new ShotRequest(terrain.Tee, terrain.Hole, 0, 1.0), terrain, new DeterministicRng(9));

        Assert.Equal(a.Ball, b.Ball);
        Assert.Equal(a.Trajectory, b.Trajectory);
    }
}
