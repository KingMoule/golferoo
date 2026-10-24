namespace Golferoo.Sim.Tests;

public class WorldTests
{
    [Fact]
    public void Step_AdvancesTick()
    {
        var world = new World(seed: 42);

        world.Step(10);

        Assert.Equal(10, world.Tick);
    }

    [Fact]
    public void SameSeed_SameChecksum()
    {
        var a = new World(seed: 1234);
        var b = new World(seed: 1234);

        a.Step(500);
        b.Step(500);

        Assert.Equal(a.Checksum(), b.Checksum());
    }

    [Fact]
    public void DifferentSeed_DifferentChecksum()
    {
        var a = new World(seed: 1);
        var b = new World(seed: 2);

        a.Step(100);
        b.Step(100);

        Assert.NotEqual(a.Checksum(), b.Checksum());
    }

    [Fact]
    public void Rng_IsReproducibleAndInUnitRange()
    {
        var a = new DeterministicRng(7);
        var b = new DeterministicRng(7);

        for (var i = 0; i < 1000; i++)
        {
            var x = a.NextDouble();
            Assert.Equal(x, b.NextDouble());
            Assert.InRange(x, 0.0, 1.0 - double.Epsilon);
        }
    }
}
