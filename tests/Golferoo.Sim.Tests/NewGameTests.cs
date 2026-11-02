using Golferoo.Sim.Economy;
using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim.Tests;

/// <summary>C18 lot 1 et C24 lot 3 : nouvelle partie sur la propriété générée, difficulté en données, emplacements de sauvegarde.</summary>
public class NewGameTests
{
    private static World NewGame(ulong seed)
    {
        var property = PropertyGenerator.CreateWithTrees(seed);
        return new World(seed, property.Terrain, league: true, placeClubhouse: true, plantedTrees: property.Trees);
    }

    private static readonly ulong[] Sample = [.. Enumerable.Range(1, 20).Select(i => (ulong)i * 37)];

    [Fact]
    public void GeneratedProperty_SameSeed_SameWorld_AndItsTreesAreThePlantedOnes()
    {
        var a = NewGame(7);
        var b = NewGame(7);
        var property = PropertyGenerator.CreateWithTrees(7);

        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.NotEqual(a.Checksum(), NewGame(8).Checksum());
        Assert.Equal(property.Trees.Count, a.Trees.Count);
        Assert.Equal(property.Trees.Count(t => t.Remarkable), a.Trees.Trees.Count(t => t.Remarkable));
    }

    [Fact]
    public void GeneratedProperty_ClubhouseIsAtTheCenter_ReachesTheDoor_AndTheClubLivesWithoutAnyHole()
    {
        foreach (var seed in Sample)
        {
            var world = NewGame(seed);
            var terrain = world.Terrain!;

            Assert.True(terrain.Blank);
            Assert.False(terrain.IsLesson);
            Assert.True(world.State.ClubActive);
            Assert.NotNull(world.League);
            Assert.False(world.HoleOpen);
            var clubhouse = world.Clubhouse;
            Assert.NotNull(clubhouse);
            var middle = clubhouse.Position(terrain.CellSize);
            Assert.True(Math.Abs(middle.X - PropertyGenerator.Center.X) < 20 && Math.Abs(middle.Z - PropertyGenerator.Center.Z) < 20, $"graine {seed} : pavillon hors du centre");
            Assert.True(float.IsFinite(world.ClubhouseField!.DistanceAt(terrain.CellXOf(PropertyGenerator.Center.X + 40), terrain.CellZOf(PropertyGenerator.Center.Z))));
        }
    }

    [Fact]
    public void GeneratedProperty_HoldsThirtyDays_WithoutException()
    {
        foreach (var seed in Sample)
        {
            var world = NewGame(seed);

            world.Step(30 * Calendar.TicksPerDay);

            Assert.NotNull(world.Clubhouse);
            Assert.NotNull(world.League);
        }
    }

    [Fact]
    public void GeneratedProperty_SavedAndReloaded_KeepsItsChecksum_TreesIncluded()
    {
        foreach (var seed in Sample.Take(5))
        {
            var world = NewGame(seed);
            world.Step(2 * Calendar.TicksPerDay);
            using var stream = new MemoryStream();
            world.Save(stream);
            stream.Position = 0;

            var loaded = World.Load(stream);

            Assert.Equal(world.Checksum(), loaded.Checksum());
            Assert.Equal(world.Trees.Count, loaded.Trees.Count);
            Assert.Equal(world.Trees.Trees.Count(t => t.Remarkable), loaded.Trees.Trees.Count(t => t.Remarkable));
            Assert.True(loaded.State.ClubActive);
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
        Assert.Equal(0, meta.OpenHoles);

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
