using Golferoo.Sim.Economy;
using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C18 lot 1 : propriété provisoire d'une nouvelle partie, difficulté en données, emplacements de sauvegarde.</summary>
public class NewGameTests
{
    private static World NewGame(ulong seed) => new(seed, BlankProperty.Create(), league: true, placeClubhouse: true);

    [Fact]
    public void BlankProperty_SameSeed_SameMap_AndNothingElseOnIt()
    {
        var a = NewGame(7);
        var b = NewGame(7);

        Assert.Equal(a.Checksum(), b.Checksum());
        var t = a.Terrain!;
        Assert.Equal((300, 200, 4.0), (t.CellsX, t.CellsZ, t.CellSize));
        Assert.Equal(0, a.Trees.Count);
        for (var z = 0; z < t.CellsZ; z++)
        {
            for (var x = 0; x < t.CellsX; x++)
            {
                Assert.NotEqual(Surface.Water, t.GetCellSurface(x, z));
                Assert.NotEqual(Surface.Trees, t.GetCellSurface(x, z));
            }
        }
    }

    [Fact]
    public void BlankProperty_OnHundredSeeds_ClubhouseIsPlacedNearAnEdge_AndReachesTheFirstTee()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var world = NewGame(seed);
            var terrain = world.Terrain!;

            Assert.False(terrain.Blank);
            Assert.Empty(HoleCheck.Check(terrain));
            Assert.Equal(1, world.OpenHoles);
            var clubhouse = world.Clubhouse;
            Assert.NotNull(clubhouse);
            Assert.True(clubhouse.Footprint.MinX < terrain.CellsX / 10, $"graine {seed} : le pavillon est loin du bord");
            var field = world.ClubhouseField!;
            Assert.True(float.IsFinite(field.DistanceAt(terrain.CellXOf(terrain.Tee.X), terrain.CellZOf(terrain.Tee.Z))), $"graine {seed} : le départ est hors d'atteinte du pavillon");
        }
    }

    [Fact]
    public void Facile_IsLoadedFromData_WithEveryParameter()
    {
        var path = Path.Combine(Path.GetDirectoryName(EconomyTests.FacilePath())!, "..", "difficulty", "facile.json");

        var fromFile = DifficultyParams.Parse(File.ReadAllText(path));

        Assert.Equal(1.0, fromFile.GolferDemand);
        Assert.Equal(1.0, fromFile.MoodDrain);
        Assert.Equal(DifficultyParams.Facile.ObjectiveRewards, fromFile.ObjectiveRewards);
        Assert.Equal(7, fromFile.ObjectiveRewards.Count);
    }

    [Theory]
    [InlineData("""{ "moodDrain": 1.0, "objectiveRewards": {} }""")]
    [InlineData("""{ "golferDemand": 1.0, "objectiveRewards": {} }""")]
    [InlineData("""{ "golferDemand": 1.0, "moodDrain": 1.0 }""")]
    [InlineData("""{ "golferDemand": -1.0, "moodDrain": 1.0, "objectiveRewards": {} }""")]
    [InlineData("""{ "golferDemand": 1.0, "moodDrain": -0.5, "objectiveRewards": {} }""")]
    [InlineData("""{ "golferDemand": 1.0, "moodDrain": 1.0, "objectiveRewards": { "holes3": -1 } }""")]
    [InlineData("""{ "golferDemand": 1.0, "moodDrain": 1.0, "objectiveRewards": {}, "inconnu": 1 }""")]
    public void Difficulty_IncompleteOrNegative_IsRefused(string json) =>
        Assert.Throws<InvalidDataException>(() => DifficultyParams.Parse(json));

    private static string TempDir() => Directory.CreateTempSubdirectory("golferoo-slots").FullName;

    private static (byte[] Bytes, SlotMeta Meta) Serialized(World world, string club)
    {
        using var memory = new MemoryStream();
        world.Save(memory);
        return (memory.ToArray(), SlotMeta.Of(world, club));
    }

    [Fact]
    public void Slot_WrittenThenRead_KeepsTheChecksum_AndMetadataNeedNoWorld()
    {
        var dir = TempDir();
        var world = NewGame(3);
        for (var i = 0; i < 40; i++)
        {
            world.Step();
        }

        var (bytes, meta) = Serialized(world, "Club des tests");
        SaveSlotStore.Write(dir, "slot2", bytes, meta, [1, 2, 3]);

        File.WriteAllText(SaveSlotStore.SavePath(dir, "slot2"), "pas une sauvegarde");
        Assert.Equal(meta, SaveSlotStore.ReadMeta(dir, "slot2"));
        Assert.Equal(world.Checksum(), meta.Checksum);
        Assert.Equal(15_000_000, meta.Money);
        Assert.Equal(1, meta.OpenHoles);

        SaveSlotStore.Write(dir, "slot2", bytes, meta, null);
        Assert.Equal(world.Checksum(), SaveSlotStore.Read(dir, "slot2").Checksum());
        Assert.Equal([1, 2, 3], File.ReadAllBytes(SaveSlotStore.ThumbPath(dir, "slot2")));
        Assert.Null(SaveSlotStore.ReadMeta(dir, "slot3"));
    }

    [Fact]
    public void Autosave_NeverOverwritesAPlayerSlot()
    {
        var dir = TempDir();
        var first = NewGame(1);
        var (bytes, meta) = Serialized(first, "Premier");
        foreach (var slot in SaveSlotStore.Manual)
        {
            SaveSlotStore.Write(dir, slot, bytes, meta, null);
        }

        var later = NewGame(2);
        later.Step();
        var (laterBytes, laterMeta) = Serialized(later, "Second");
        SaveSlotStore.WriteAuto(dir, laterBytes, laterMeta, null);

        foreach (var slot in SaveSlotStore.Manual)
        {
            Assert.Equal(first.Checksum(), SaveSlotStore.Read(dir, slot).Checksum());
            Assert.Equal(meta, SaveSlotStore.ReadMeta(dir, slot));
        }

        Assert.Equal(later.Checksum(), SaveSlotStore.Read(dir, SaveSlotStore.Auto).Checksum());
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveSlotStore.SavePath(dir, "slot4"));
    }
}
