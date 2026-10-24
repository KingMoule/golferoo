using System.Reflection;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class TerrainTests
{
    private const double Eps = 1e-9;

    internal static string HolePath()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Golferoo.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("Golferoo.sln introuvable.");
        return Path.Combine(root, "data", "holes", "proto-par4.json");
    }

    private static HeightField Field(int nx, int nz, double cell, Func<double, double, double> f)
    {
        var field = new HeightField(nx, nz, cell);
        for (var j = 0; j <= nz; j++)
        {
            for (var i = 0; i <= nx; i++)
            {
                field.SetCornerHeight(i, j, f(i * cell, j * cell));
            }
        }

        return field;
    }

    private static HeightField Bumpy() =>
        Field(12, 10, 2.0, (x, z) => (3 * Math.Sin(x * 0.7)) + (2 * Math.Cos(z * 0.9)) + (0.1 * x * z));

    // Critère 1

    [Theory]
    [InlineData(2, 96, 32)]
    [InlineData(3, 144, 48)]
    [InlineData(4, 192, 64)]
    public void Load_GivesExpectedDimensions(int factor, int cx, int cz)
    {
        var t = HoleLoader.Load(HolePath(), factor);

        Assert.Equal(cx, t.CellsX);
        Assert.Equal(cz, t.CellsZ);
        Assert.Equal(8.0 / factor, t.CellSize, Eps);
        Assert.Equal(384.0, t.Heights.WidthMeters, Eps);
        Assert.Equal(128.0, t.Heights.DepthMeters, Eps);
    }

    [Fact]
    public void Load_SameSurfaceAtSameWorldPointForAllFactors()
    {
        var t2 = HoleLoader.Load(HolePath(), 2);
        var t3 = HoleLoader.Load(HolePath(), 3);
        var t4 = HoleLoader.Load(HolePath(), 4);

        for (var tz = 0; tz < 16; tz++)
        {
            for (var tx = 0; tx < 48; tx++)
            {
                foreach (var (fx, fz) in new[] { (0.13, 0.21), (0.5, 0.5), (0.87, 0.64) })
                {
                    var x = (tx + fx) * 8;
                    var z = (tz + fz) * 8;
                    var s = t2.SurfaceAt(x, z);
                    Assert.Equal(s, t3.SurfaceAt(x, z));
                    Assert.Equal(s, t4.SurfaceAt(x, z));
                }
            }
        }
    }

    [Fact]
    public void Load_ReliefIsSameAcrossFactors()
    {
        var t2 = HoleLoader.Load(HolePath(), 2);
        var t4 = HoleLoader.Load(HolePath(), 4);

        // Les coins de tuiles sont communs à tous les facteurs : mêmes hauteurs exactes.
        for (var tx = 0; tx <= 48; tx += 4)
        {
            Assert.Equal(t2.Sample(tx * 8, 64), t4.Sample(tx * 8, 64), 1e-6);
        }
    }

    [Fact]
    public void Load_HoleHasRequiredSurfacesAndFeatures()
    {
        var t = HoleLoader.Load(HolePath(), 4);

        Assert.Equal(Surface.Tee, t.SurfaceAt(t.Tee.X, t.Tee.Z));
        Assert.Equal(Surface.Green, t.SurfaceAt(t.Green.X, t.Green.Z));
        Assert.Equal(Surface.Green, t.SurfaceAt(t.Hole.X, t.Hole.Z));
        Assert.True(t.Tee.X < t.Green.X, "tee à l'ouest, green à l'est");
        Assert.True(t.Sample(t.Green.X, t.Green.Z) > t.Sample(t.Tee.X, t.Tee.Z) + 1, "green surélevé");
        var seen = new HashSet<Surface>();
        for (var cz = 0; cz < t.CellsZ; cz++)
        {
            for (var cx = 0; cx < t.CellsX; cx++)
            {
                seen.Add(t.GetCellSurface(cx, cz));
            }
        }

        foreach (var s in new[] { Surface.Tee, Surface.Green, Surface.Fairway, Surface.Rough, Surface.Bunker, Surface.Water, Surface.Trees })
        {
            Assert.Contains(s, seen);
        }
    }

    // Critère 2

    [Fact]
    public void Sample_PassesThroughCornerHeights()
    {
        var f = Bumpy();
        for (var j = 0; j <= f.CellsZ; j++)
        {
            for (var i = 0; i <= f.CellsX; i++)
            {
                Assert.Equal(f.GetCornerHeight(i, j), f.Sample(i * f.CellSize, j * f.CellSize), Eps);
            }
        }
    }

    [Fact]
    public void Sample_IsContinuousAcrossCellBoundaries()
    {
        var f = Bumpy();
        const double step = 1e-4;
        for (var row = 0.5; row < f.DepthMeters; row += 0.77)
        {
            var prev = f.Sample(0, row);
            for (var x = step; x <= f.WidthMeters; x += step)
            {
                var h = f.Sample(x, row);
                Assert.True(Math.Abs(h - prev) < 0.01, $"saut en x={x}, z={row}");
                prev = h;
            }
        }
    }

    [Fact]
    public void Sample_HasContinuousDerivativeAtCellBoundaries()
    {
        var f = Bumpy();
        const double e = 1e-6;
        for (var i = 1; i < f.CellsX; i++)
        {
            var x = i * f.CellSize;
            for (var z = 0.3; z < f.DepthMeters - 0.3; z += 0.61)
            {
                var left = (f.Sample(x - e, z) - f.Sample(x - (2 * e), z)) / e;
                var right = (f.Sample(x + (2 * e), z) - f.Sample(x + e, z)) / e;
                Assert.True(Math.Abs(left - right) < 1e-3, $"pente discontinue en x={x}, z={z}");
            }
        }

        for (var j = 1; j < f.CellsZ; j++)
        {
            var z = j * f.CellSize;
            for (var x = 0.3; x < f.WidthMeters - 0.3; x += 0.61)
            {
                var up = (f.Sample(x, z - e) - f.Sample(x, z - (2 * e))) / e;
                var down = (f.Sample(x, z + (2 * e)) - f.Sample(x, z + e)) / e;
                Assert.True(Math.Abs(up - down) < 1e-3, $"pente discontinue en x={x}, z={z}");
            }
        }
    }

    [Fact]
    public void SampleNormal_MatchesNumericalGradient()
    {
        var f = Bumpy();
        const double e = 1e-5;
        for (var x = 0.7; x < f.WidthMeters - 0.7; x += 1.93)
        {
            for (var z = 0.7; z < f.DepthMeters - 0.7; z += 1.71)
            {
                var dx = (f.Sample(x + e, z) - f.Sample(x - e, z)) / (2 * e);
                var dz = (f.Sample(x, z + e) - f.Sample(x, z - e)) / (2 * e);
                var n = f.SampleNormal(x, z);
                var len = Math.Sqrt((dx * dx) + 1 + (dz * dz));
                Assert.Equal(-dx / len, n.X, 1e-6);
                Assert.Equal(1 / len, n.Y, 1e-6);
                Assert.Equal(-dz / len, n.Z, 1e-6);
            }
        }
    }

    // Critère 3

    [Fact]
    public void FlatTerrain_GivesConstantAndUpNormal()
    {
        var f = Field(8, 8, 2.0, (_, _) => 5.0);
        foreach (var (x, z) in new[] { (0.0, 0.0), (3.3, 7.1), (16.0, 16.0), (9.99, 0.01) })
        {
            Assert.Equal(5.0, f.Sample(x, z), Eps);
            var n = f.SampleNormal(x, z);
            Assert.Equal(0.0, n.X, Eps);
            Assert.Equal(1.0, n.Y, Eps);
            Assert.Equal(0.0, n.Z, Eps);
        }
    }

    [Fact]
    public void InclinedPlane_GivesExpectedSlope()
    {
        const double sx = 0.2;
        const double sz = -0.1;
        var f = Field(8, 8, 2.0, (x, z) => 1 + (sx * x) + (sz * z));
        foreach (var (x, z) in new[] { (0.0, 0.0), (3.3, 7.1), (16.0, 16.0), (9.99, 0.01), (15.5, 1.2) })
        {
            Assert.Equal(1 + (sx * x) + (sz * z), f.Sample(x, z), 1e-9);
            var n = f.SampleNormal(x, z);
            var len = Math.Sqrt((sx * sx) + 1 + (sz * sz));
            Assert.Equal(-sx / len, n.X, 1e-9);
            Assert.Equal(1 / len, n.Y, 1e-9);
            Assert.Equal(-sz / len, n.Z, 1e-9);
        }
    }

    // Critère 4

    [Fact]
    public void OutsideMap_ClampsWithoutException()
    {
        var t = HoleLoader.Load(HolePath(), 2);

        Assert.Equal(t.Sample(0, 0), t.Sample(-50, -20), Eps);
        Assert.Equal(t.Sample(384, 128), t.Sample(1000, 900), Eps);
        Assert.Equal(t.Sample(0, 64), t.Sample(-1e9, 64), Eps);
        Assert.Equal(t.SurfaceAt(0, 0), t.SurfaceAt(-5, -5));
        Assert.Equal(t.SurfaceAt(383.9, 127.9), t.SurfaceAt(5000, 5000));
        var n = t.SampleNormal(-10, 5000);
        Assert.Equal(1.0, (n.X * n.X) + (n.Y * n.Y) + (n.Z * n.Z), 1e-9);
    }

    // Critère 5

    private static string Doc(
        string surfaces = "[\"TG\", \"FW\"]",
        string heights = "[[0,0,0],[0,0,0],[0,0,0]]",
        string tee = "[0.5, 0.5]",
        string hole = "[1.5, 0.5]",
        string green = "[1.5, 0.5]",
        string tilesX = "2",
        string legend = "{\"T\":\"Tee\",\"G\":\"Green\",\"F\":\"Fairway\",\"W\":\"Water\"}") =>
        $"{{\"tileSize\":8,\"tilesX\":{tilesX},\"tilesZ\":2,\"legend\":{legend},\"surfaces\":{surfaces},\"heights\":{heights},\"tee\":{tee},\"green\":{green},\"hole\":{hole}}}";

    [Fact]
    public void Parse_ValidMinimalDocument()
    {
        var t = HoleLoader.Parse(Doc(), 2);

        Assert.Equal(4, t.CellsX);
        Assert.Equal(Surface.Water, t.SurfaceAt(15, 15));
    }

    [Fact]
    public void Parse_RejectsInconsistentDimensions()
    {
        var e1 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(surfaces: "[\"TG\", \"F\"]"), 2));
        Assert.Contains("ligne de surfaces 1", e1.Message, StringComparison.Ordinal);
        var e2 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(heights: "[[0,0,0],[0,0],[0,0,0]]"), 2));
        Assert.Contains("ligne de hauteurs 1", e2.Message, StringComparison.Ordinal);
        var e3 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(heights: "[[0,0,0],[0,0,0]]"), 2));
        Assert.Contains("lignes de hauteurs", e3.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsUnknownLetter()
    {
        var e = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(surfaces: "[\"TG\", \"FX\"]"), 2));

        Assert.Contains("'X'", e.Message, StringComparison.Ordinal);
        Assert.Contains("(1, 1)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsUnknownSurfaceInLegend()
    {
        var e = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(legend: "{\"T\":\"Lave\"}"), 2));

        Assert.Contains("Lave", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("Tee, Green")]
    public void Parse_RejectsNumericOrCombinedSurfaceInLegend(string name)
    {
        var legend = $"{{\"T\":\"{name}\",\"G\":\"Green\",\"F\":\"Fairway\",\"W\":\"Water\"}}";

        var e = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(legend: legend), 2));

        Assert.Contains("surface inconnue", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsNullSurfaceRow()
    {
        var e = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(surfaces: "[\"TG\", null]"), 2));

        Assert.Contains("Ligne de surfaces 1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtoHole_IsAbout400YardsTeeToHole()
    {
        var t = HoleLoader.Load(HolePath(), 4);

        var yards = Math.Sqrt(Math.Pow(t.Hole.X - t.Tee.X, 2) + Math.Pow(t.Hole.Z - t.Tee.Z, 2)) / 0.9144;

        Assert.InRange(yards, 370, 420);
    }

    [Fact]
    public void Parse_RejectsTeeOrHoleOffMap()
    {
        var e1 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(tee: "[-1, 0.5]"), 2));
        Assert.Contains("tee", e1.Message, StringComparison.Ordinal);
        Assert.Contains("hors carte", e1.Message, StringComparison.Ordinal);
        var e2 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(hole: "[2.5, 0.5]"), 2));
        Assert.Contains("hole", e2.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsTeeOffTeeSurfaceAndHoleOffGreen()
    {
        var e1 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(tee: "[0.5, 1.5]"), 2));
        Assert.Contains("tee", e1.Message, StringComparison.Ordinal);
        Assert.Contains("Tee", e1.Message, StringComparison.Ordinal);
        var e2 = Assert.Throws<HoleDataException>(() => HoleLoader.Parse(Doc(hole: "[0.5, 1.5]"), 2));
        Assert.Contains("Green", e2.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsMalformedJson()
    {
        var e = Assert.Throws<HoleDataException>(() => HoleLoader.Parse("{ pas du json", 2));

        Assert.Contains("JSON invalide", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsBadFactor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HoleLoader.Parse(Doc(), 0));
    }

    // Critère 6

    [Fact]
    public void SetCornerHeight_ChangesOnlyLocally()
    {
        var t = HoleLoader.Load(HolePath(), 4);
        var before = Snapshot(t);
        const int ci = 100;
        const int cj = 30;

        t.SetCornerHeight(ci, cj, t.Heights.GetCornerHeight(ci, cj) + 3.0);
        var after = Snapshot(t);

        var changedNear = false;
        for (var k = 0; k < before.Points.Length; k++)
        {
            var (x, z) = before.Points[k];
            var dist = Math.Max(Math.Abs(x - (ci * t.CellSize)), Math.Abs(z - (cj * t.CellSize))) / t.CellSize;
            if (dist > 2.0)
            {
                Assert.Equal(before.Heights[k], after.Heights[k]);
            }
            else if (before.Heights[k] != after.Heights[k])
            {
                changedNear = true;
            }
        }

        Assert.True(changedNear);
        Assert.Equal(t.Heights.GetCornerHeight(ci, cj), t.Sample(ci * t.CellSize, cj * t.CellSize), Eps);
    }

    [Fact]
    public void PaintCell_ChangesOnlyThatCell()
    {
        var t = HoleLoader.Load(HolePath(), 4);
        var before = Snapshot(t);
        const int cx = 60;
        const int cz = 20;
        var original = t.GetCellSurface(cx, cz);
        var painted = original == Surface.Bunker ? Surface.Water : Surface.Bunker;

        t.PaintCell(cx, cz, painted);

        Assert.Equal(painted, t.GetCellSurface(cx, cz));
        Assert.Equal(painted, t.SurfaceAt((cx + 0.5) * t.CellSize, (cz + 0.5) * t.CellSize));
        var after = Snapshot(t);
        for (var k = 0; k < before.Points.Length; k++)
        {
            var (x, z) = before.Points[k];
            var dist = Math.Max(Math.Abs(x - ((cx + 0.5) * t.CellSize)), Math.Abs(z - ((cz + 0.5) * t.CellSize))) / t.CellSize;
            if (dist > 2.0)
            {
                Assert.Equal(before.Surfaces[k], after.Surfaces[k]);
            }
        }

        Assert.Equal(before.Heights, after.Heights);
    }

    private static (double[] Heights, Surface[] Surfaces, (double X, double Z)[] Points) Snapshot(TerrainGrid t)
    {
        var points = new List<(double, double)>();
        for (var z = 0.37; z < t.Heights.DepthMeters; z += 3.1)
        {
            for (var x = 0.41; x < t.Heights.WidthMeters; x += 3.3)
            {
                points.Add((x, z));
            }
        }

        var arr = points.ToArray();
        return (
            arr.Select(p => t.Sample(p.Item1, p.Item2)).ToArray(),
            arr.Select(p => t.SurfaceAt(p.Item1, p.Item2)).ToArray(),
            arr);
    }

    // Table de surfaces et déterminisme

    [Fact]
    public void SurfaceTable_CoversEverySurface()
    {
        foreach (var s in Enum.GetValues<Surface>())
        {
            var p = SurfaceTable.Get(s);
            Assert.InRange(p.Restitution, 0.0, 1.0);
            Assert.InRange(p.Friction, 0.0, 1.0);
            Assert.True(p.RollResistance > 0);
        }

        Assert.False(SurfaceTable.Get(Surface.Water).Walkable);
        Assert.Equal(1, SurfaceTable.Get(Surface.Water).PenaltyStrokes);
        Assert.True(SurfaceTable.Get(Surface.Green).RollResistance < SurfaceTable.Get(Surface.Rough).RollResistance);
    }

    [Fact]
    public void World_ChecksumIncludesTerrainAndStaysDeterministic()
    {
        var a = new World(7, HoleLoader.Load(HolePath(), 2));
        var b = new World(7, HoleLoader.Load(HolePath(), 2));
        var none = new World(7);
        a.Step(5);
        b.Step(5);
        none.Step(5);

        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.NotEqual(a.Checksum(), none.Checksum());

        b.Terrain!.SetCornerHeight(10, 10, 99);
        Assert.NotEqual(a.Checksum(), b.Checksum());
    }
}
