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
        Assert.True(world.PlaceTee(1, terrain.Tee));
        Assert.True(world.PlaceHole(1, terrain.Hole));
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

    /// <summary>Monde avec ligue arrêté au moment demandé : journée en cours, finale en cours, saison ajustée, ou après une partie d'entraînement du pro.</summary>
    private static World LeagueWorld(ulong seed, string map, string scenario)
    {
        var terrain = Hole(map);
        var world = new World(seed, terrain, league: true) { ManualPro = scenario == "training" };
        world.SetCourseRating(CourseRater.Rate(terrain, seed, 15));
        var league = world.League!;
        if (scenario == "training")
        {
            var group = world.StartProRound();
            for (var i = 0; i < 60_000 && !group.Finished; i++)
            {
                if (world.AwaitingPro is { } pro)
                {
                    world.PlayProShot(ProShotFor(terrain, pro.Ball));
                }

                world.Step();
            }

            Assert.True(group.Finished);
            Assert.True(world.Pro!.Experience(ProSkill.Putting) > 0);
        }
        else
        {
            Func<bool> reached = scenario switch
            {
                "round" => () => league.Rounds[0].Cards.Count > 0 && !league.Rounds[0].Closed,
                "flight" => () => world.Groups.Any(g => g.LeagueRound >= 0 && g.Golfers.Any(x => x.State == GolferState.BallInFlight)),
                "final" => () => league.Final.Cards.Count > 0 && !league.Final.Closed,
                _ => () => league.Adjusted,
            };
            for (var i = 0; i < 40_000 && !reached(); i++)
            {
                // "flight" : une fois la première journée lancée, tick par tick jusqu'à une balle de ligue en vol.
                world.Step(scenario == "flight" && league.Rounds[0].StartDay >= 0 ? 1 : 100);
            }

            Assert.True(reached(), "scénario non atteint");
        }

        league.Members[3].Opinion = 17; // hors Checksum : seul AssertSameLeague le voit
        return world;
    }

    private static ProShot ProShotFor(TerrainGrid t, Point2 ball)
    {
        if (t.SurfaceAt(ball.X, ball.Z) == Surface.Green)
        {
            return new ProShot(Clubs.Putter, t.Hole, ShotShape.Straight, 1.0);
        }

        var d = Math.Sqrt(Math.Pow(t.Hole.X - ball.X, 2) + Math.Pow(t.Hole.Z - ball.Z, 2));
        var club = Clubs.Full.LastOrDefault(c => c.TargetRange >= d) ?? Clubs.Driver;
        return new ProShot(club, t.Hole, ShotShape.Straight, Math.Min(1.0, Math.Sqrt(d / club.TargetRange)));
    }

    /// <summary>L'état de ligue que ni <c>League.MixInto</c> ni <c>MixPro</c> ne couvrent : cartes et points des journées, classement, champion de la saison, avis des membres.</summary>
    private static void AssertSameLeague(World a, World b)
    {
        var (x, y) = (a.League!, b.League!);
        Assert.Equal(x.Members.Select(m => m.Opinion), y.Members.Select(m => m.Opinion));
        Assert.Equal(x.FinalRanking, y.FinalRanking);
        Assert.Equal(x.Champion, y.Champion);
        foreach (var (r, q) in x.Rounds.Append(x.Final).Zip(y.Rounds.Append(y.Final)))
        {
            Assert.Equal(r.Format, q.Format);
            Assert.Equal(r.NominalDay, q.NominalDay);
            Assert.Equal(r.Points, q.Points);
            Assert.Equal(r.Cards.Keys, q.Cards.Keys);
            foreach (var id in r.Cards.Keys)
            {
                var (c, d) = (r.Cards[id], q.Cards[id]);
                Assert.Equal(Enumerable.Range(0, c.HoleCount).Select(h => (c.Par(h), c.Strokes(h))), Enumerable.Range(0, d.HoleCount).Select(h => (d.Par(h), d.Strokes(h))));
            }
        }
    }

    [Theory]
    [InlineData(1UL, "proto", "round")]
    [InlineData(2UL, "dogleg", "round")]
    [InlineData(3UL, "proto", "final")]
    [InlineData(2UL, "proto", "adjusted")]
    [InlineData(3UL, "proto", "training")]
    [InlineData(1UL, "proto", "flight")]
    public void LeagueWorld_ReloadedAndAdvanced_HasTheSameChecksum(ulong seed, string map, string scenario)
    {
        var original = LeagueWorld(seed, map, scenario);
        var bytes = Save(original);

        var loaded = Load(bytes);
        Assert.Equal(bytes, Save(loaded));
        Assert.Equal(original.Checksum(), loaded.Checksum());
        AssertSameLeague(original, loaded);
        Assert.Equal(original.ManualPro, loaded.ManualPro);
        original.Step(2000);
        loaded.Step(2000);

        Assert.Equal(original.Checksum(), loaded.Checksum());
        AssertSameLeague(original, loaded);
    }

    [Fact]
    public void Load_ManualProWithoutALeague_Throws()
    {
        var bytes = Save(Build(1, "proto", "start"));
        var raw = Unpack(bytes);
        raw[^1] = 1; // _manualPro, dernier octet d'un monde sans ligue

        Assert.Throws<InvalidDataException>(() => Load(Pack(bytes, raw, Golferoo.Sim.Save.SaveFormat.Hash(raw))));
    }

    [Fact]
    public void AdjustedLeague_ReloadedThenIntoTheNextSeason_KeepsTheOpinions()
    {
        var original = LeagueWorld(4, "proto", "adjusted");
        var loaded = Load(Save(original));

        foreach (var w in new[] { original, loaded })
        {
            for (var i = 0; i < 200 && w.League!.Year == 0; i++)
            {
                w.Step(Calendar.TicksPerDay);
            }

            Assert.Equal(1, w.League!.Year);
        }

        Assert.Equal(original.Checksum(), loaded.Checksum());
        AssertSameLeague(original, loaded);
    }

    /// <summary>Fichier forgé : l'en-tête (magique, version, somme de contrôle) de <paramref name="original"/>, puis ce corps compressé avec cette empreinte.</summary>
    private static byte[] Pack(byte[] original, byte[] raw, ulong hash)
    {
        var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(raw);
        }

        var file = new MemoryStream();
        file.Write(original, 0, 16);
        file.Write(BitConverter.GetBytes((int)packed.Length));
        file.Write(BitConverter.GetBytes(hash));
        file.Write(packed.ToArray());
        return file.ToArray();
    }

    private static byte[] Unpack(byte[] file)
    {
        var raw = new MemoryStream();
        using var gzip = new GZipStream(new MemoryStream(file, 28, file.Length - 28), CompressionMode.Decompress);
        gzip.CopyTo(raw);
        return raw.ToArray();
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
        bytes[4] = 9;

        Assert.Throws<InvalidDataException>(() => Load(bytes));
    }

    [Fact]
    public void Load_VersionOne_SaysItIsAnOlderVersion()
    {
        var bytes = Save(Build(1, "proto", "start"));
        bytes[4] = 1;

        var e = Assert.Throws<InvalidDataException>(() => Load(bytes));
        Assert.Contains("ancienne version", e.Message);
    }

    /// <summary>C12 lot 1 : parcours de trois trous, groupe en jeu, une pose de départ du trou 3 en file.</summary>
    private static World ThreeHoleWorld(ulong seed, bool queued)
    {
        var world = new World(seed, CourseTests.ThreeHoles());
        world.SetLoadMode(6);
        world.SpawnGroup();
        world.Step(300);
        if (queued)
        {
            Assert.True(world.PlaceTee(3, world.Terrain!.Course[3].Tee));
            Assert.True(world.PlaceHole(2, world.Terrain.Course[2].Hole));
        }

        return world;
    }

    [Theory]
    [InlineData(1UL, false)]
    [InlineData(2UL, true)]
    public void ThreeHoleCourse_ReloadedAndAdvanced_HasTheSameChecksum(ulong seed, bool queued)
    {
        var original = ThreeHoleWorld(seed, queued);
        var bytes = Save(original);

        var loaded = Load(bytes);
        Assert.Equal(original.Checksum(), loaded.Checksum());
        Assert.Equal(3, loaded.Terrain!.Course.Count);
        Assert.Equal(original.Terrain!.Course[3].Tee, loaded.Terrain.Course[3].Tee);
        Assert.Equal(original.Terrain.Course[2].Par, loaded.Terrain.Course[2].Par);
        Assert.Equal(bytes, Save(loaded));
        original.Step(2000);
        loaded.Step(2000);

        Assert.Equal(original.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void BlankTerrain_ReloadedAfterOpening_KeepsItsHoleState()
    {
        var blank = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "blank-coude.json"), 2);
        var world = new World(1, blank);
        world.Step();
        Assert.False(world.HoleOpen);

        var loaded = Load(Save(world));

        Assert.False(loaded.HoleOpen);
        Assert.Equal(world.Checksum(), loaded.Checksum());
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
        var forged = Pack(bytes, raw, Golferoo.Sim.Save.SaveFormat.Hash(raw));

        Assert.Throws<InvalidDataException>(() => Load(forged));
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

    [Fact]
    public void Load_ImpossibleTerrainSize_Throws()
    {
        var world = Build(1, "proto", "start");
        var bytes = Save(world);
        var raw = Unpack(bytes);
        var dims = BitConverter.GetBytes(world.Terrain!.CellsX).Concat(BitConverter.GetBytes(world.Terrain.CellsZ)).ToArray();
        var at = raw.AsSpan().IndexOf(dims);
        Assert.True(at > 0);
        foreach (var (x, z) in new[] { (1_000_000, 1_000_000), (int.MaxValue, 1), (0, 5), (-3, 5), (4096, 4096) })
        {
            var forged = (byte[])raw.Clone();
            BitConverter.GetBytes(x).CopyTo(forged, at);
            BitConverter.GetBytes(z).CopyTo(forged, at + 4);

            Assert.Throws<InvalidDataException>(() => Load(Pack(bytes, forged, Golferoo.Sim.Save.SaveFormat.Hash(forged))));
        }
    }

    [Fact]
    public void Load_ImpossibleBodyLength_Throws()
    {
        var bytes = Save(Build(1, "proto", "start"));

        foreach (var length in new[] { -1, bytes.Length, int.MaxValue, 100_000_000 })
        {
            var forged = (byte[])bytes.Clone();
            BitConverter.GetBytes(length).CopyTo(forged, 16);
            Assert.Throws<InvalidDataException>(() => Load(forged));
        }
    }

    [Fact]
    public void Load_DecompressedBodyOverTheCap_Throws()
    {
        var bytes = Save(Build(1, "proto", "start"));
        var huge = new byte[Golferoo.Sim.Save.SaveFormat.MaxBodyBytes + 1]; // des zéros : le gzip tient en quelques Ko

        Assert.Throws<InvalidDataException>(() => Load(Pack(bytes, huge, 0)));
    }

    [Fact]
    public void Reloaded_PaintingWoodsAfterwards_DrawsTheSameTrees()
    {
        var original = Build(2, "dogleg", "queued");
        original.Step(5);
        var loaded = Load(Save(original));

        foreach (var w in new[] { original, loaded })
        {
            w.PaintSurface(new Point2(60, 100), 15, Surface.Trees);
            w.Step(3);
        }

        Assert.Equal(original.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void Reloaded_SpawningAGroupAfterwards_GivesTheSameGolferIds()
    {
        var original = Build(3, "proto", "flight");
        var loaded = Load(Save(original));

        foreach (var w in new[] { original, loaded })
        {
            w.SpawnGroup();
            w.Step(10);
        }

        Assert.Equal(original.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void Reloaded_DuringAHoledShotAndAPenaltyShot_PlaysOutTheSame()
    {
        Assert.True(PlaysOutTheSameFromAFlight(g => g.Pending.Holed), "aucun coup au trou trouvé");
        Assert.True(PlaysOutTheSameFromAFlight(g => g.Pending.PenaltyStrokes > 0), "aucun coup avec pénalité trouvé");
    }

    /// <summary>Cherche (graines 1 à 80) un vol qui vérifie <paramref name="inFlight"/>, le sauve, le recharge et joue la partie jusqu'au bout des deux côtés.</summary>
    private static bool PlaysOutTheSameFromAFlight(Func<Golfer, bool> inFlight)
    {
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var world = new World(seed, Hole("proto"));
            var group = world.SpawnGroup();
            for (var i = 0; i < 20_000 && !group.Finished; i++)
            {
                world.Step();
                if (group.Golfers.Any(g => g.State == GolferState.BallInFlight && inFlight(g)))
                {
                    var loaded = Load(Save(world));
                    Assert.Equal(world.Checksum(), loaded.Checksum());
                    var other = loaded.Groups[0];
                    for (var k = 0; k < 20_000 && !(group.Finished && other.Finished); k++)
                    {
                        world.Step();
                        loaded.Step();
                    }

                    Assert.True(group.Finished && other.Finished);
                    Assert.Equal(world.Checksum(), loaded.Checksum());
                    return true;
                }
            }
        }

        return false;
    }
}
