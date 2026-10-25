using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Weather;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

public class WeatherTests(ITestOutputHelper output)
{
    private const int Days = 12_000;

    private static TerrainGrid Fairway(int cells = 150)
    {
        const double cell = 4.0;
        var field = new HeightField(cells, cells, cell);
        var size = cells * cell;
        var hole = new Point2(230, size / 2);
        var grid = new TerrainGrid(field, "test", new Point2(30, size / 2), hole, hole);
        for (var cz = 0; cz < cells; cz++)
        {
            for (var cx = 0; cx < cells; cx++)
            {
                grid.PaintCell(cx, cz, Surface.Fairway);
            }
        }

        return grid;
    }

    private static TerrainGrid Proto() => HoleLoader.Load(TerrainTests.HolePath(), 2);

    [Fact]
    public void SameSeedAndDay_SameWeather_AndTerrainEditsDoNotChangeIt()
    {
        var a = new World(seed: 7, Proto());
        var b = new World(seed: 7, Proto());
        b.PaintSurface(new Point2(100, 100), 20, Surface.Bunker);
        for (var day = 0; day < 30; day++)
        {
            Assert.Equal(a.Weather, b.Weather);
            Assert.Equal(WeatherGenerator.For(7, Climate.Temperate, day), WeatherGenerator.For(7, Climate.Temperate, day));
            a.Step(Calendar.TicksPerDay);
            b.Step(Calendar.TicksPerDay);
        }

        Assert.NotEqual(Enumerable.Range(0, 30).Select(d => WeatherGenerator.For(7, Climate.Temperate, d)), Enumerable.Range(0, 30).Select(d => WeatherGenerator.For(8, Climate.Temperate, d)));
    }

    [Fact]
    public void RainFrequency_PerSeason_MatchesClimate()
    {
        var c = Climate.Temperate;
        var rain = new int[4];
        var heavy = new int[4];
        var total = new int[4];
        for (var day = 0; day < Days; day++)
        {
            var season = (int)Calendar.At((long)day * Calendar.TicksPerDay, 0).Season;
            var sky = WeatherGenerator.For(3, c, day).Sky;
            total[season]++;
            rain[season] += sky >= Sky.Rain ? 1 : 0;
            heavy[season] += sky == Sky.HeavyRain ? 1 : 0;
        }

        for (var s = 0; s < 4; s++)
        {
            output.WriteLine($"saison {s} : pluie {rain[s] / (double)total[s]:P1} (climat {c.RainChance[s]:P0}), forte {heavy[s] / (double)total[s]:P1}");
            Assert.InRange(rain[s] / (double)total[s], c.RainChance[s] - 0.05, c.RainChance[s] + 0.05);
            Assert.InRange(heavy[s] / (double)total[s], c.HeavyRainChance[s] - 0.03, c.HeavyRainChance[s] + 0.03);
        }
    }

    [Fact]
    public void ConsecutiveDays_LookMoreAlikeThanRandomDays()
    {
        int same = 0, far = 0;
        for (var day = 0; day < Days; day++)
        {
            var sky = WeatherGenerator.For(5, Climate.Temperate, day).Sky;
            same += sky == WeatherGenerator.For(5, Climate.Temperate, day + 1).Sky ? 1 : 0;
            far += sky == WeatherGenerator.For(5, Climate.Temperate, day + 40).Sky ? 1 : 0;
        }

        output.WriteLine($"même ciel le lendemain {same / (double)Days:P1}, 40 jours plus tard {far / (double)Days:P1}");
        Assert.True(same > far * 1.2);
    }

    [Fact]
    public void Forecast_IsRightAboutFourDaysInFive()
    {
        var right = Enumerable.Range(0, Days).Count(d => WeatherGenerator.Forecast(9, Climate.Temperate, d) == WeatherGenerator.For(9, Climate.Temperate, d).Sky);
        Assert.InRange(right / (double)Days, 0.77, 0.83);
    }

    [Fact]
    public void Calm_NeverRainsNorBlows()
    {
        for (var day = 0; day < 400; day++)
        {
            var w = WeatherGenerator.For(1, Climate.Calm, day);
            Assert.Equal(Sky.Sun, w.Sky);
            Assert.Equal(default, w.WindAt(0.5));
        }
    }

    [Fact]
    public void Wind_RisesDuringTheDay()
    {
        var w = new WeatherDay(Sky.Sun, 1.0, 0.0, 10.0);
        Assert.Equal(7.0, w.WindAt(0.0).X, 9);
        Assert.Equal(13.0, w.WindAt(1.0).X, 9);
    }

    [Fact]
    public void Drive_CrossWind_DriftsDownwind_TailWindCarriesFurther()
    {
        var terrain = Fairway();
        var club = Clubs.Driver;
        var start = new Point2(30, 300);
        var launch = new Vec3(club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, 0);
        var calm = BallSimulator.Run(terrain, start, launch, false, BallSimulator.DefaultDrag, false);
        var cross = BallSimulator.Run(terrain, start, launch, false, BallSimulator.DefaultDrag, false, wind: new Vec3(0, 0, 10));
        var tail = BallSimulator.Run(terrain, start, launch, false, BallSimulator.DefaultDrag, false, wind: new Vec3(10, 0, 0));
        var head = BallSimulator.Run(terrain, start, launch, false, BallSimulator.DefaultDrag, false, wind: new Vec3(-10, 0, 0));

        // Dérive attendue au premier contact, petit vent : ½·k·|v|·g·w·T² (g : renfort du vent de travers) avec la vitesse et la durée de vol moyennes du coup sans vent
        // (k·|v|·w est l'accélération latérale). On accepte un écart de 40 % pour la vitesse qui varie en vol.
        var drift = cross.FirstLanding.Z - start.Z;
        var carry = calm.FirstLanding.X - start.X;
        var flight = carry / (club.LaunchCos * club.Speed * 0.8);
        var expected = 0.5 * BallSimulator.DefaultDrag * club.Speed * 0.8 * 10 * BallSimulator.WindGainAcross * flight * flight;
        output.WriteLine($"porté {carry:F1} m, vol ≈ {flight:F1} s, dérive {drift:F1} m (attendue ≈ {expected:F1} m), arrière {tail.Shot.Ball.X - calm.Shot.Ball.X:F1} m, face {head.Shot.Ball.X - calm.Shot.Ball.X:F1} m");
        Assert.InRange(drift, expected * 0.6, expected * 1.4);
        Assert.True(tail.Shot.Ball.X > calm.Shot.Ball.X + 10);
        Assert.True(head.Shot.Ball.X < calm.Shot.Ball.X - 10);
    }

    [Fact]
    public void NoWind_TrajectoryUnchangedToTheBit()
    {
        var terrain = Fairway();
        var launch = new Vec3(50, 12, 3);
        var a = BallSimulator.Run(terrain, new Point2(30, 300), launch, false, BallSimulator.DefaultDrag, true);
        var b = BallSimulator.Run(terrain, new Point2(30, 300), launch, false, BallSimulator.DefaultDrag, true, wind: default);
        Assert.Equal(a.Shot.Ball, b.Shot.Ball);
        Assert.Equal(a.Shot.Trajectory!, b.Shot.Trajectory!);
    }

    [Fact]
    public void Planner_CompensatesCrossWind()
    {
        var terrain = Fairway();
        var request = new ShotRequest(terrain.Tee, terrain.Hole, 0);

        double MeanMiss(Vec3 wind)
        {
            var resolver = new PhysicalShotResolver { Wind = wind };
            var sum = 0.0;
            for (ulong seed = 0; seed < 40; seed++)
            {
                var r = resolver.Resolve(request, terrain, new DeterministicRng(seed));
                sum += Math.Sqrt(((r.Ball.X - terrain.Hole.X) * (r.Ball.X - terrain.Hole.X)) + ((r.Ball.Z - terrain.Hole.Z) * (r.Ball.Z - terrain.Hole.Z)));
            }

            return sum / 40;
        }

        var calm = MeanMiss(default);
        var windy = MeanMiss(new Vec3(0, 0, 10));
        output.WriteLine($"écart moyen au trou : sans vent {calm:F1} m, vent de travers de 10 m/s {windy:F1} m");
        Assert.True(windy < (calm * 1.25) + 3.0);
    }

    [Fact]
    public void SameSeedAndCommands_WithCycleSky_SameChecksum()
    {
        ulong Run()
        {
            var w = new World(seed: 11, Proto());
            w.SpawnGroup();
            w.Step(300);
            w.CycleSky();
            w.Step(300);
            return w.Checksum();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void CycleSky_GoesThroughSkiesForTheDayOnly()
    {
        var w = new World(seed: 2, Proto());
        var first = w.Weather.Sky;
        w.CycleSky();
        Assert.Equal((Sky)(((int)first + 1) % 4), w.Weather.Sky);
        w.Step(Calendar.TicksPerDay);
        Assert.Equal(WeatherGenerator.For(2, w.Climate, w.DayIndex).Sky, w.Weather.Sky);
    }
}
