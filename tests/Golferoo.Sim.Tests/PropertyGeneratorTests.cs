using System.Diagnostics;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C24 lot 1 : propriété générée (relief, eau, clairière, pavillon central), sans arbres.</summary>
public class PropertyGeneratorTests
{
    private const int Seeds = 100;

    private static string ParklandPath() => Path.Combine(Path.GetDirectoryName(EconomyTests.FacilePath())!, "..", "property", "parkland.json");

    private static IEnumerable<PropertyGenerator.Result> Properties()
    {
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            yield return PropertyGenerator.Generate(seed);
        }
    }

    [Fact]
    public void SameSeed_SameTerrain_DifferentSeeds_DifferentTerrains()
    {
        Assert.Equal(PropertyGenerator.Create(7).Checksum(), PropertyGenerator.Create(7).Checksum());

        var sums = Properties().Select(r => r.Terrain.Checksum()).ToHashSet();

        Assert.Equal(Seeds, sums.Count);
    }

    [Fact]
    public void Terrain_HasTheFixedGrid_NoTreesAndNoHole()
    {
        foreach (var r in Properties())
        {
            var t = r.Terrain;
            Assert.Equal((300, 200, 4.0), (t.CellsX, t.CellsZ, t.CellSize));
            Assert.True(t.Blank);
            Assert.Empty(t.IsolatedTrees);
            for (var cz = 0; cz < t.CellsZ; cz++)
            {
                for (var cx = 0; cx < t.CellsX; cx++)
                {
                    Assert.True(t.GetCellSurface(cx, cz) is Surface.Rough or Surface.Water);
                }
            }
        }
    }

    [Fact]
    public void Clearing_IsFlat_WithoutWater()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in Properties())
        {
            var t = r.Terrain;
            double low = double.MaxValue, high = double.MinValue;
            for (var j = 0; j <= t.CellsZ; j++)
            {
                for (var i = 0; i <= t.CellsX; i++)
                {
                    var (dx, dz) = ((i * t.CellSize) - PropertyGenerator.Center.X, (j * t.CellSize) - PropertyGenerator.Center.Z);
                    if (Math.Sqrt((dx * dx) + (dz * dz)) <= p.ClearingRadius)
                    {
                        var h = t.Heights.GetCornerHeight(i, j);
                        (low, high) = (Math.Min(low, h), Math.Max(high, h));
                    }
                }
            }

            Assert.True(high - low <= World.ClubhouseMaxRelief, $"clairière de {high - low:F2} m de relief");
            for (var cz = 0; cz < t.CellsZ; cz++)
            {
                for (var cx = 0; cx < t.CellsX; cx++)
                {
                    var c = t.CellCenter(cx, cz);
                    var distance = Math.Sqrt(Math.Pow(c.X - PropertyGenerator.Center.X, 2) + Math.Pow(c.Z - PropertyGenerator.Center.Z, 2));
                    if (distance <= p.ClearingRadius)
                    {
                        Assert.NotEqual(Surface.Water, t.GetCellSurface(cx, cz));
                    }

                    if (t.GetCellSurface(cx, cz) == Surface.Water)
                    {
                        Assert.True(distance >= p.MinWaterDistance, $"eau à {distance:F0} m du pavillon");
                    }
                }
            }
        }
    }

    [Fact]
    public void EveryProperty_HasOneOrTwoPonds_AStream_Fords_AndGentleRelief()
    {
        var p = PropertyParams.Parkland;
        foreach (var r in Properties())
        {
            Assert.InRange(r.Ponds, p.PondsMin, p.PondsMax);
            Assert.True(r.StreamLength >= p.StreamMinLength);
            Assert.True(r.Fords >= p.FordsMin);
            Assert.True(PropertyGenerator.MeanSlope(r.Terrain) <= p.MaxMeanSlope);
            Assert.InRange(r.Hills, p.HillsMin, p.HillsMax);
            Assert.True(r.Terrain.Heights.MaxCornerHeight < TerrainGrid.MaxHeight);
        }
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(160.0)]
    public void NoWater_CloserToTheClubhouseThanMinWaterDistance(double minWaterDistance)
    {
        var p = PropertyParams.Parkland with { MinWaterDistance = minWaterDistance };
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var t = PropertyGenerator.Generate(seed, p).Terrain;
            for (var cz = 0; cz < t.CellsZ; cz++)
            {
                for (var cx = 0; cx < t.CellsX; cx++)
                {
                    var c = t.CellCenter(cx, cz);
                    var distance = Math.Sqrt(Math.Pow(c.X - PropertyGenerator.Center.X, 2) + Math.Pow(c.Z - PropertyGenerator.Center.Z, 2));
                    Assert.False(t.GetCellSurface(cx, cz) == Surface.Water && distance < minWaterDistance, $"graine {seed} : eau à {distance:F0} m");
                }
            }
        }
    }

    [Theory]
    [InlineData(4.0)]
    [InlineData(10.0)]
    public void ReliefAmplitude_IsTheSpanOfTheBaseRelief(double amplitude)
    {
        // Collines et creux réduits à presque rien : il reste le relief de fond, de hauteur crête à creux égale à l'amplitude (au-dessus de 5 m de base).
        var p = PropertyParams.Parkland with { ReliefAmplitudeMin = amplitude, ReliefAmplitudeMax = amplitude, BumpHeightMin = 0.01, BumpHeightMax = 0.01 };
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var heights = PropertyGenerator.Generate(seed, p).Terrain.Heights;
            double low = double.MaxValue, high = double.MinValue;
            for (var j = 0; j <= heights.CellsZ; j++)
            {
                for (var i = 0; i <= heights.CellsX; i++)
                {
                    var h = heights.GetCornerHeight(i, j);
                    (low, high) = (Math.Min(low, h), Math.Max(high, h));
                }
            }

            // Les étangs et le ruisseau creusent sous le fond : seul le sommet est comparable.
            Assert.InRange(high, 5 + (amplitude / 2) - 1.5, 5 + (amplitude / 2) + 0.5);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Ponds_FollowPondsMinAndMax(int ponds)
    {
        var p = PropertyParams.Parkland with { PondsMin = ponds, PondsMax = ponds };
        for (ulong seed = 1; seed <= 20; seed++)
        {
            Assert.Equal(ponds, PropertyGenerator.Generate(seed, p).Ponds);
        }
    }

    [Fact]
    public void Hills_BothCountsOccurAcrossSeeds()
    {
        Assert.Equal([1, 2], Properties().Select(r => r.Hills).Distinct().Order());
    }

    [Fact]
    public void Clubhouse_CanBePlacedAtTheCenter()
    {
        foreach (var r in Properties())
        {
            var world = new World(1, r.Terrain);
            var b = Clubhouse.At(((int)PropertyGenerator.Center.X / 4) - (Clubhouse.Width / 2), ((int)PropertyGenerator.Center.Z / 4) - (Clubhouse.Depth / 2), DoorSide.East);

            Assert.True(world.CanPlaceClubhouse(b));
        }
    }

    [Fact]
    public void Generation_TakesUnderOneSecondPerSeed_InRelease()
    {
        _ = PropertyGenerator.Create(0); // échauffement du JIT
        var slowest = TimeSpan.Zero;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var sw = Stopwatch.StartNew();
            _ = PropertyGenerator.Create(seed);
            slowest = sw.Elapsed > slowest ? sw.Elapsed : slowest;
        }

#if !DEBUG
        Assert.True(slowest < TimeSpan.FromSeconds(1), $"{slowest.TotalMilliseconds:F0} ms");
#endif
    }

    [Fact]
    public void Parkland_FileMatchesTheEmbeddedCopy()
    {
        Assert.Equal(PropertyParams.Parkland, PropertyParams.Parse(File.ReadAllText(ParklandPath())));
    }

    [Theory]
    [InlineData("pondsMax")]
    [InlineData("playableRadius")]
    [InlineData("reliefWavelength")]
    public void Parkland_MissingField_IsRefused(string field)
    {
        var json = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(ParklandPath()), $"\"{field}\":[^,\\n]*,?", "");

        Assert.Throws<InvalidDataException>(() => PropertyParams.Parse(json));
    }

    [Theory]
    [InlineData("{\n  \"unknownKey\": 1,", "{")]
    [InlineData("\"pondsMin\": 1", "\"pondsMin\": 3")]
    [InlineData("\"maxMeanSlope\": 0.04", "\"maxMeanSlope\": -0.04")]
    public void Parkland_UnknownKeyOrBadBounds_IsRefused(string from, string to)
    {
        var text = File.ReadAllText(ParklandPath());
        var json = from == "{\n  \"unknownKey\": 1," ? text.Replace("{\n  \"reliefAmplitudeMin\"", "{\n  \"unknownKey\": 1,\n  \"reliefAmplitudeMin\"") : text.Replace(from, to);

        Assert.NotEqual(text, json);
        Assert.Throws<InvalidDataException>(() => PropertyParams.Parse(json));
    }
}
