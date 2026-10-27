using System.IO.Compression;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;

namespace Golferoo.Sim.Tests;

/// <summary>C11 lot 1 : sauvegarde du monde sans la ligue (rechargement à somme de contrôle égale, octets stables, fichiers faux).</summary>
public class SaveTests
{
    private static TerrainGrid Hole(string map) => map == "dogleg"
        ? HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2)
        : HoleLoader.Load(TerrainTests.HolePath(), 2);

    private static byte[] Save(World world)
    {
        using var stream = new MemoryStream();
        world.Save(stream);
        return stream.ToArray();
    }

    private static World Load(byte[] bytes) => World.Load(new MemoryStream(bytes));

    /// <summary>Un groupe en jeu, des marcheurs, une source de beauté ; le monde s'arrête au tick demandé par le scénario.</summary>
    private static World Build(ulong seed, string map, string scenario)
    {
        var world = new World(seed, Hole(map));
        world.AddBeautySource(new Point2(120, 60));
        world.SetLoadMode(6);
        world.SpawnGroup();
        switch (scenario)
        {
            case "start":
                break;
            case "flight":
                for (var i = 0; i < 6000 && !InFlight(world); i++)
                {
                    world.Step();
                }

                Assert.True(InFlight(world), "aucune balle en vol");
                break;
            default:
                world.Step(300);
                QueueEverything(world);
                break;
        }

        return world;
    }

    private static bool InFlight(World world) =>
        world.Groups.SelectMany(g => g.Golfers).Any(g => g.State == GolferState.BallInFlight);

    /// <summary>Une commande dans chacune des six files de commandes.</summary>
    private static void QueueEverything(World world)
    {
        var terrain = world.Terrain!;
        world.PaintSurface(new Point2(150, 70), 12, Surface.Bunker);
        world.RaiseTerrain(new Point2(100, 40), 15, 1.5);
        world.PlantTree(60, 30, TreeSpeciesId.Birch, TreePlantStage.Sapling);
        world.RazeTree(world.Trees.Trees[0].Id);
        world.SetTurfSettings(TurfSettings.Normal with { GreenSpeed = GreenSpeed.Fast });
        world.SetCourseRating(new CourseRating(1, terrain.Par, 3.4, 4.6));
        Assert.True(world.PlaceTee(terrain.Tee));
        Assert.True(world.PlaceHole(terrain.Hole));
        world.PlayProShot(new ProShot(Clubs.Iron7, new Point2(200, 60), ShotShape.Fade, 0.8));
        Assert.Equal(1, world.PendingEdits - 1);
        Assert.True(world.PendingTreeCommands >= 2);
        Assert.True(world.ProShotPending);
    }

    [Theory]
    [InlineData(1UL, "proto", "start")]
    [InlineData(2UL, "proto", "flight")]
    [InlineData(3UL, "proto", "queued")]
    [InlineData(1UL, "dogleg", "flight")]
    [InlineData(2UL, "dogleg", "queued")]
    [InlineData(3UL, "dogleg", "start")]
    public void Reloaded_AdvancedLikeTheOriginal_HasTheSameChecksum(ulong seed, string map, string scenario)
    {
        var original = Build(seed, map, scenario);
        var bytes = Save(original);

        var loaded = Load(bytes);
        Assert.Equal(original.Checksum(), loaded.Checksum());
        original.Step(2000);
        loaded.Step(2000);

        Assert.Equal(original.Checksum(), loaded.Checksum());
        Assert.Equal(original.Tick, loaded.Tick);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("flight")]
    [InlineData("queued")]
    public void SavingAReloadedWorld_GivesTheSameBytes(string scenario)
    {
        var bytes = Save(Build(2, "dogleg", scenario));

        Assert.Equal(bytes, Save(Load(bytes)));
    }

    [Fact]
    public void WorldWithoutTerrain_RoundTrips()
    {
        var world = new World(9);
        world.Trees.Add(0.3, 0.4, TreeSpeciesId.Fir, 120);
        world.Step(50);

        var loaded = Load(Save(world));

        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Null(loaded.Terrain);
    }

    [Fact]
    public void Save_WithALeague_IsNotSupportedYet()
    {
        var world = new World(1, Hole("proto"), league: true);

        Assert.Throws<NotSupportedException>(() => world.Save(new MemoryStream()));
    }

    private static bool Throws(Action load)
    {
        try
        {
            load();
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    [Fact]
    public void Load_WrongMagic_Throws()
    {
        var bytes = Save(Build(1, "proto", "start"));
        bytes[0] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => Load(bytes));
    }

    [Fact]
    public void Load_UnknownVersion_Throws()
    {
        var bytes = Save(Build(1, "proto", "start"));
        bytes[4] = 2;

        Assert.Throws<InvalidDataException>(() => Load(bytes));
    }

    [Fact]
    public void Load_Truncated_Throws()
    {
        var bytes = Save(Build(1, "proto", "flight"));

        foreach (var length in new[] { 0, 3, 4, 7, 8, 15, 16, 17, 30, bytes.Length / 2, bytes.Length - 8, bytes.Length - 1 })
        {
            Assert.True(Throws(() => Load(bytes[..length])), $"length {length} of {bytes.Length}");
        }
    }

    [Fact]
    public void Load_AChangedByteInTheBody_Throws()
    {
        var bytes = Save(Build(1, "proto", "flight"));

        foreach (var at in new[] { 16, 20, 28, 38, 40, bytes.Length / 3, bytes.Length / 2, bytes.Length - 20, bytes.Length - 5, bytes.Length - 1 })
        {
            var broken = (byte[])bytes.Clone();
            broken[at] ^= 0x5A;
            Assert.True(Throws(() => Load(broken)), $"at {at} of {bytes.Length}");
        }
    }

    [Fact]
    public void Load_AChangedChecksumInTheHeader_Throws()
    {
        var bytes = Save(Build(1, "proto", "start"));
        bytes[8] ^= 1;

        Assert.Throws<InvalidDataException>(() => Load(bytes));
    }

    [Fact]
    public void Load_ABodyChangedThenRecompressed_ThrowsOnTheChecksum()
    {
        var bytes = Save(Build(1, "proto", "start"));
        var body = new MemoryStream();
        using (var gzip = new GZipStream(new MemoryStream(bytes, 28, bytes.Length - 28), CompressionMode.Decompress))
        {
            gzip.CopyTo(body);
        }

        var raw = body.ToArray();
        raw[188] ^= 1; // le tick (8 + 5 tables de climat de 36 octets) : le gzip est valide, le monde diffère
        var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(raw);
        }

        var forged = new MemoryStream();
        forged.Write(bytes, 0, 16); // magique, version, somme de contrôle
        forged.Write(BitConverter.GetBytes((int)packed.Length));
        forged.Write(BitConverter.GetBytes(Golferoo.Sim.Save.SaveFormat.Hash(raw)));
        forged.Write(packed.ToArray());

        Assert.Throws<InvalidDataException>(() => Load(forged.ToArray()));
    }

    [Fact]
    public void EditedWorld_AfterPaintsPlantingsFellingsTurfAndAMonthChange_Reloads()
    {
        var world = new World(5, Hole("dogleg"));
        world.SpawnGroup();
        world.SetLoadMode(10);
        world.PaintSurface(new Point2(200, 80), 20, Surface.Trees);
        world.RaiseTerrain(new Point2(120, 60), 25, 2.0);
        world.PlantTree(70, 40, TreeSpeciesId.Oak, TreePlantStage.Mound);
        world.SetTurfSettings(TurfSettings.Normal with { RoughLength = RoughLength.Short, GreenSpeed = GreenSpeed.Slow });
        world.Step(3);
        var standing = world.Trees.Trees.Where(t => t.State == TreeState.Standing).Take(4).ToList();
        world.RazeTree(standing[0].Id);
        world.FellTree(standing[1].Id, 1.0);
        world.FellTree(standing[2].Id, -2.0);
        world.Step(2 * Calendar.TicksPerDay);
        world.SkipToNextMonth();
        world.CycleSky();
        world.Step(10);
        Assert.Contains(world.Trees.Trees, t => t.State != TreeState.Standing);

        var loaded = Load(Save(world));

        Assert.Equal(world.Checksum(), loaded.Checksum());
        AssertSameCaches(world, loaded);
        world.Step(2000);
        loaded.Step(2000);
        Assert.Equal(world.Checksum(), loaded.Checksum());
        AssertSameCaches(world, loaded);
    }

    /// <summary>Les caches recalculés au chargement (sol, cimes d'arbres) valent ceux du monde d'origine, bit à bit.</summary>
    private static void AssertSameCaches(World a, World b)
    {
        var t = a.Terrain!;
        for (var z = 0.5; z < t.CellsZ * t.CellSize; z += 7.3)
        {
            for (var x = 0.5; x < t.CellsX * t.CellSize; x += 7.3)
            {
                Assert.Equal(a.Trees.MaxTopAt(x, z), b.Trees.MaxTopAt(x, z));
                Assert.Equal(t.Soil.RollResistanceAt(x, z), b.Terrain!.Soil.RollResistanceAt(x, z));
                Assert.Equal(t.Soil.RestitutionAt(x, z), b.Terrain!.Soil.RestitutionAt(x, z));
            }
        }

        Assert.Equal(a.Trees.Version, b.Trees.Version);
        Assert.Equal(a.Trees.NextId, b.Trees.NextId);
        Assert.Equal(a.Date, b.Date);
        Assert.Equal(a.TreesPlanted, b.TreesPlanted);
    }
}
