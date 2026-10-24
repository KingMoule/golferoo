namespace Golferoo.Sim.Tests;

// DÉMO du critère 3 de C01 : test volontairement faux, retiré au commit suivant.
public class DemoFailingTest
{
    [Fact]
    public void Demo_CiMustFail() => Assert.Equal(1, 2);
}
