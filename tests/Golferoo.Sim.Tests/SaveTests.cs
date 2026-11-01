using System.IO.Compression;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
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
            Assert.True(world.Pro!.Experience(ProSkill.Putter) > 0);
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
        Assert.Equal(x.Members.Select(m => (m.Id, m.Name, m.StartHandicap, m.Profile, m.IsPro, m.VisitorId, m.Resigned)), y.Members.Select(m => (m.Id, m.Name, m.StartHandicap, m.Profile, m.IsPro, m.VisitorId, m.Resigned)));
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
        var world = Build(1, "proto", "start");
        var bytes = Save(world);
        var raw = Unpack(bytes);
        var tail = new Golferoo.Sim.Save.SaveWriter();
        world.WriteArrivals(tail);
        raw[raw.Length - tail.Written.Length - 2] = 1; // _manualPro : l'octet avant celui du pavillon et le bloc des arrivées, dans un monde sans ligue

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

    private static byte[] Bytes(Action<Golferoo.Sim.Save.SaveWriter> write)
    {
        var w = new Golferoo.Sim.Save.SaveWriter();
        write(w);
        return w.Written.ToArray();
    }

    private static byte[] ReplaceOnce(byte[] all, byte[] what, byte[] with)
    {
        var at = all.AsSpan().IndexOf(what);
        Assert.True(at >= 0, "section introuvable dans le corps");
        return [.. all[..at], .. with, .. all[(at + what.Length)..]];
    }

    /// <summary>
    /// Ramène le corps v15 de <paramref name="raw"/> (monde à club vide : seulement le pro dans la ligue, vivier sans membre) à la disposition de la v14 ou de la v13 :
    /// sans commandes du pro, sans trous aimés, sans adhésion des visiteurs, ligue des 40 membres de la graine et leur opinion fixe ; pour la v13, sans humeur, sans heure d'arrivée des groupes, sans réactions.
    /// </summary>
    private static byte[] LegacyBody(World world, byte[] raw, int version)
    {
        var league = world.League;
        Assert.True(league is null || league.Members.Count == 1);
        raw = raw[..^4]; // ni bâtiments posés (v17)
        raw = raw[..^4]; // ni commandes du pro (v15)
        if (world.Flow is { } flow && (flow.Pending.Count > 0 || flow.Count > 0))
        {
            var cached = flow.Cached.ToList();
            var keys = Bytes(w =>
            {
                w.List(flow.Pending, p => { w.Int(p.Key); w.Int(p.Origin); });
                w.List(cached, p => { w.Int(p.Key); w.Int(p.Origin); });
            });
            raw = ReplaceOnce(raw, keys, Bytes(w =>
            {
                w.List(flow.Pending.Select(p => p.Key).ToList(), w.Int);
                w.List(flow.CachedKeys, w.Int);
            })); // champs sans origine de borne (v19) : relus complets
        }

        if (version == 13)
        {
            raw = raw[..^Bytes(w => world.Reactions.Write(w)).Length]; // ni réactions (v14)
        }
        else
        {
            raw = raw[..^Bytes(w => world.Reactions.Write(w)).Length]; // les réactions sans leurs cellules (v16), au dernier événement près (v18)
            raw = [.. raw, .. Bytes(w =>
            {
                var events = Enum.GetValues<Golferoo.Sim.Golfers.MoodEvent>().Where(e => e != Golferoo.Sim.Golfers.MoodEvent.ServiceVisited).ToList();
                w.List(Enumerable.Range(1, 18).SelectMany(h => events.Select(e => world.Reactions.Count(h, e))).ToList(), w.Int);
                w.List(Enumerable.Range(1, 18).SelectMany(h => events.Select(e => world.Reactions.Previous(h, e))).ToList(), w.Int);
            })];
        }

        foreach (var group in world.Groups)
        {
            var bytes = Bytes(w => group.Write(w));
            var legacy = bytes;
            foreach (var golfer in group.Golfers)
            {
                var g = Bytes(w => golfer.Write(w));
                var cut = 5 + (version == 13 ? 4 + 1 + 4 + (4 * MoodParams.EventCount) + 4 : 0); // trous aimés (v15), puis humeur, abandon, compteurs et attente (v14)
                var older = g[..^(19 + cut)]; // ni visite (v18)
                if (version != 13)
                {
                    // les compteurs d'événements n'avaient pas le dernier (v18) : on le retire, avec 4 octets de moins dans leur longueur
                    var count = older.Length - 4 - (4 * MoodParams.EventCount) - 4;
                    older = [.. older[..count], .. BitConverter.GetBytes(MoodParams.EventCount - 1), .. older[(count + 4)..^8], .. older[^4..]];
                }

                legacy = ReplaceOnce(legacy, g, older);
            }

            raw = ReplaceOnce(raw, bytes, version == 13 ? legacy[..^8] : legacy); // l'heure d'arrivée du groupe (v14) est la dernière chose écrite
        }

        var pool = Bytes(w => world.Visitors.Write(w));
        var visitors = world.Visitors.Visitors;
        var legacyPool = Bytes(w => w.Int(visitors.Count)).Concat(visitors.SelectMany(v => Bytes(w => v.Write(w))[..^10])).ToArray(); // ni points, ni niveau, ni démission (v15), ni parties jouées (v16)
        raw = ReplaceOnce(raw, pool, legacyPool);

        if (league is not null)
        {
            var bytes = Bytes(w => league.Write(w));
            var roster = Bytes(w => w.List(league.Members, m => m.WriteRoster(w)));
            var states = Bytes(w => league.Members.ToList().ForEach(m => m.Write(w)));
            var divisions = Bytes(w => w.List(league.Divisions, d => w.List(d, m => w.Int(m.Id))));
            var rest = bytes[(13 + roster.Length + states.Length + divisions.Length)..];
            var club = new Golferoo.Sim.League.LeagueSeason(world.Seed, Golferoo.Sim.League.LeagueSeason.GenerateMembers(world.Seed));
            var legacyLeague = bytes[..13]
                .Concat(Bytes(w => club.Members.ToList().ForEach(m =>
                {
                    w.Int(50); // l'opinion fixe de C06
                    m.Write(w);
                })))
                .Concat(Bytes(w => w.List(club.Divisions, d => w.List(d, m => w.Int(m.Id)))))
                .Concat(rest).ToArray();
            raw = ReplaceOnce(raw, bytes, legacyLeague);
        }

        return raw;
    }

    private static byte[] LegacyFile(byte[] file, byte[] raw, int version)
    {
        var forged = (byte[])file.Clone();
        forged[4] = (byte)version;
        return Pack(forged, raw, Golferoo.Sim.Save.SaveFormat.Hash(raw));
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

    /// <summary>C12 lot 5 : un ajout de trou en file, puis un trou joué et retiré, se sauvent et se relisent à l'identique.</summary>
    [Fact]
    public void NewHoleCommands_QueuedThenPlayedAndRemoved_ReloadWithTheSameChecksum()
    {
        var world = new World(3, CourseTests.ThreeHoles());
        Assert.True(world.AddHole(world.Terrain!.Course[2].Tee, world.Terrain.Course[2].Hole));
        var queued = Load(Save(world));
        Assert.Equal(world.Checksum(), queued.Checksum());
        world.Step();
        queued.Step();
        Assert.Equal(4, queued.Terrain!.Course.Count);
        Assert.Equal(world.Checksum(), queued.Checksum());

        world.SpawnGroup();
        for (var i = 0; i < 120_000 && !world.Groups.All(g => g.Finished); i++)
        {
            world.Step();
        }

        Assert.True(world.RemoveLastHole());
        var removing = Load(Save(world));
        Assert.Equal(world.Checksum(), removing.Checksum());
        world.Step();
        removing.Step();
        var after = Load(Save(world));
        Assert.Equal(3, after.Terrain!.Course.Count);
        Assert.Equal(world.Checksum(), after.Checksum());
        Assert.Equal(world.Checksum(), removing.Checksum());
        after.Step(1000);
        world.Step(1000);
        Assert.Equal(world.Checksum(), after.Checksum());
    }

    /// <summary>C25 : l'XP de chaque habileté et son entraînement de l'année survivent à la sauvegarde et entrent dans la somme de contrôle.</summary>
    [Fact]
    public void ProSkills_EveryExperienceRoundTrips_AndEntersTheChecksum()
    {
        var world = LeagueWorld(2, "proto", "training");
        for (var i = 0; i < ProSkills.Count; i++)
        {
            world.Pro!.Gain((ProSkill)i, 137 + (i * 11), league: i % 2 == 0, year: 3);
        }

        var loaded = Load(Save(world));
        for (var i = 0; i < ProSkills.Count; i++)
        {
            var skill = (ProSkill)i;
            Assert.Equal(world.Pro!.Experience(skill), loaded.Pro!.Experience(skill));
            Assert.Equal(world.Pro.TrainingThisYear(skill, 3), loaded.Pro.TrainingThisYear(skill, 3));
            var other = Load(Save(world));
            other.Pro!.Gain(skill, 1, league: true, year: 3);
            Assert.NotEqual(world.Checksum(), other.Checksum());
        }

        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    /// <summary>C25 : les réglages de progression font partie de la partie (sauvegarde et somme de contrôle), comme les paramètres de l'économie.</summary>
    [Fact]
    public void Progression_RoundTrips_AndEntersTheChecksum()
    {
        var terrain = Hole("proto");
        var custom = ProgressionParams.Default with { CostScale = 0.1, TrainingCapPerYear = 123 };
        var world = new World(2, terrain, league: true, progression: custom);
        var loaded = Load(Save(world));

        Assert.Equal(custom, loaded.Progression);
        Assert.Equal(123 * 100, loaded.Pro!.TrainingCap);
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.NotEqual(new World(2, terrain, league: true).Checksum(), world.Checksum());
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
        bytes[4] = 20;

        Assert.Throws<InvalidDataException>(() => Load(bytes));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)] // C12 lot 4 : planification en cours dans le golfeur, champs de flux en attente et en cache dans le monde
    [InlineData(4)] // C14 lot 1 : grand livre et paramètres de l'économie
    [InlineData(5)] // C14 lot 2 : annulation du dernier coup de pinceau, genre des retouches en file
    [InlineData(6)] // C21 lot 2 : note scratch trou par trou
    [InlineData(7)] // C25 lot 1 : XP des 17 habiletés en centièmes
    [InlineData(8)] // C21 lot 3 : journée de 9 min, durées en ticks
    [InlineData(9)] // C25 lot 3 : seuils de Jeu dans le vent et de Lecture du green, biais des aperçus
    [InlineData(10)] // C25 lot 4 : écart type de l'abaissement du chip
    public void Load_OlderVersion_SaysItIsAnOlderVersion(byte version)
    {
        var bytes = Save(Build(1, "proto", "start"));
        bytes[4] = version;

        var e = Assert.Throws<InvalidDataException>(() => Load(bytes));
        Assert.Contains("ancienne version", e.Message);
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

    /// <summary>C12 lot 2 : cinq groupes, le premier en route vers le départ du trou 2, les derniers encore dans la file.</summary>
    private static World MidPlayThreeHoles(ulong seed)
    {
        var world = new World(seed, CourseTests.ThreeHoles());
        for (var i = 0; i < 5; i++)
        {
            world.SpawnGroup();
        }

        for (var i = 0; i < 60_000 && !world.Groups.SelectMany(g => g.Golfers).Any(g => g.State == GolferState.ToNextTee); i++)
        {
            world.Step();
        }

        Assert.Contains(world.Groups, g => g.Hole == 2);
        Assert.Contains(world.Groups, g => !g.Released);
        return world;
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void MidPlay_HoleCardsAndQueue_ReloadedAndAdvanced_HaveTheSameChecksum(ulong seed)
    {
        var original = MidPlayThreeHoles(seed);
        var bytes = Save(original);

        var loaded = Load(bytes);
        Assert.Equal(original.Checksum(), loaded.Checksum());
        Assert.Equal(original.Groups.Select(g => (g.Hole, g.Released)), loaded.Groups.Select(g => (g.Hole, g.Released)));
        Assert.Equal(original.Groups[0].Golfers[0].Card!.Gross, loaded.Groups[0].Golfers[0].Card!.Gross);
        Assert.Equal(bytes, Save(loaded));
        original.Step(3000);
        loaded.Step(3000);

        Assert.Equal(original.Checksum(), loaded.Checksum());
    }

    /// <summary>C12 lot 4 : un golfeur au milieu de la planification de son coup (candidats déjà évalués), et des champs de flux qui attendent après une retouche.</summary>
    [Theory]
    [InlineData("plan")]
    [InlineData("flow")]
    public void MidPlanOrMidRetouch_ReloadedAndAdvanced_HasTheSameChecksum(string scenario)
    {
        var original = new World(4, Hole("dogleg"));
        original.SetLoadMode(40);
        original.SpawnGroup();
        if (scenario == "plan")
        {
            for (var i = 0; i < 6000 && !original.Groups[0].Golfers.Any(g => g.Plan is { IsDone: false, Stage: > 0 } or { IsDone: false, Cursor: > 20 }); i++)
            {
                original.Step();
            }

            Assert.Contains(original.Groups[0].Golfers, g => g.Plan is { IsDone: false });
        }
        else
        {
            original.Step(300);
            original.PaintSurface(new Point2(150, 70), 12, Surface.Bunker);
            original.Step();
            Assert.NotEmpty(original.Flow!.Pending);
        }

        var bytes = Save(original);
        var loaded = Load(bytes);
        Assert.Equal(original.Checksum(), loaded.Checksum());
        Assert.Equal(bytes, Save(loaded));
        original.Step(600);
        loaded.Step(600);

        Assert.Equal(original.Checksum(), loaded.Checksum());
    }

    /// <summary>
    /// Banc C12 : les champs en cache des marcheurs sont bornés par la cellule de qui les a fait calculer ; la sauvegarde garde cette origine,
    /// pour que la couverture (donc qui attend un champ élargi) se refasse à l'identique.
    /// La preuve est l'égalité des champs en cache (origine comprise) : dans ce scénario, la somme après quelques centaines de ticks ne diverge pas sans l'origine.
    /// </summary>
    [Fact]
    public void BoundedFlowFields_ReloadWithTheirOrigin()
    {
        var original = new World(4, Hole("dogleg"));
        original.SetLoadMode(40);
        original.SpawnGroup();
        original.Step(300);
        Assert.Contains(original.Flow!.Cached, p => p.Origin >= 0);

        var loaded = Load(Save(original));
        Assert.Equal(original.Flow.Cached, loaded.Flow!.Cached);
        original.Step(600);
        loaded.Step(600);

        Assert.Equal(original.Checksum(), loaded.Checksum());
    }

    /// <summary>
    /// C14 lot 2 : sauvé au milieu d'un mois avec un coup de pinceau annulable et des retouches de coup en file, le monde rechargé
    /// redonne le même solde, le même grand livre et la même annulation. Sans la mémoire du coup dans la somme, deux mondes
    /// au même terrain, l'un annulable et l'autre non, auraient la même somme.
    /// </summary>
    [Fact]
    public void MidMonthStroke_ReloadedThenUndone_HasTheSameBalanceLedgerAndChecksum()
    {
        var original = new World(2, CourseTests.ThreeHoles());
        original.SpawnGroup();
        original.Step(300);
        var tee = original.Terrain!.Course[1].Tee;
        StrokeUndoTests.Stroke(original, new Point2(tee.X + 30, tee.Z + 30));
        original.BeginStroke();
        original.PaintSurface(new Point2(tee.X + 60, tee.Z + 30), 5, Surface.Green);
        original.UndoStroke();
        Assert.True(original.CanUndoStroke);

        var bytes = Save(original);
        var loaded = Load(bytes);
        Assert.Equal(original.Checksum(), loaded.Checksum());
        Assert.Equal(bytes, Save(loaded));
        Assert.True(loaded.CanUndoStroke);
        Assert.Equal(original.StrokeCost, loaded.StrokeCost);

        foreach (var w in new[] { original, loaded })
        {
            w.Step();
            w.UndoStroke();
            w.Step(2000);
        }

        Assert.Equal(original.Ledger.Balance, loaded.Ledger.Balance);
        Assert.Equal(original.Ledger.Months.Count, loaded.Ledger.Months.Count);
        Assert.Equal(original.Terrain!.Checksum(), loaded.Terrain!.Checksum());
        Assert.Equal(original.Checksum(), loaded.Checksum());

        // Même terrain et même caisse, mais seul le second peut encore annuler : sommes différentes.
        var a = new World(2, CourseTests.ThreeHoles());
        var b = new World(2, CourseTests.ThreeHoles());
        foreach (var w in new[] { a, b })
        {
            StrokeUndoTests.Stroke(w, new Point2(tee.X + 30, tee.Z + 30));
        }

        a.BeginStroke();
        a.Step();
        b.Step();
        Assert.Equal(a.Terrain!.Checksum(), b.Terrain!.Checksum());
        Assert.Equal(a.Ledger.Balance, b.Ledger.Balance);
        Assert.NotEqual(a.Checksum(), b.Checksum());
    }

    [Fact]
    public void NewState_EntersTheChecksum()
    {
        var a = MidPlayThreeHoles(1);
        var b = Load(Save(a));
        var before = b.Checksum();
        b.Groups[0].Hole = 3;
        Assert.NotEqual(before, b.Checksum());
        b.Groups[0].Hole = 2;
        b.Groups[0].Golfers[0].Card!.Record(2, 4);
        Assert.NotEqual(before, b.Checksum());
        b.Groups[0].Golfers[0].Card!.PickUp(2);
        b.Groups[^1].Released = true;
        Assert.NotEqual(before, b.Checksum());
    }

    [Theory]
    [InlineData("hole")]
    [InlineData("card")]
    public void Load_GroupHoleOrCardOutsideTheCourse_Throws(string what)
    {
        var world = new World(1, CourseTests.ThreeHoles());
        var group = world.SpawnGroup();
        world.Step(10);
        var bytes = Save(world);
        var raw = Unpack(bytes);
        byte[] pattern;
        int offset;
        if (what == "hole")
        {
            // Group: id, league round (-1), release tick, started, hole, released.
            pattern = [.. BitConverter.GetBytes(group.Id), .. BitConverter.GetBytes(-1), .. BitConverter.GetBytes(0L), (byte)(group.Started ? 1 : 0), .. BitConverter.GetBytes(group.Hole)];
            offset = pattern.Length - 4;
        }
        else
        {
            // Score card: hole count then pars (three holes of par four) ; claim nine holes.
            pattern = [.. BitConverter.GetBytes(3), .. BitConverter.GetBytes(4), .. BitConverter.GetBytes(4), .. BitConverter.GetBytes(4)];
            offset = 0;
        }

        var index = raw.AsSpan().IndexOf(pattern);
        Assert.True(index >= 0, "motif introuvable dans le corps");
        raw[index + offset] = (byte)(what == "hole" ? 9 : 2);
        var forged = Pack(bytes, raw, Golferoo.Sim.Save.SaveFormat.Hash(raw));

        Assert.Throws<InvalidDataException>(() => Load(forged));
    }

    [Fact]
    public void Load_HoleCommandOutsideTheCourse_Throws()
    {
        var terrain = CourseTests.ThreeHoles();
        var world = new World(1, terrain);
        var at = terrain.Course[3].Tee;
        Assert.True(world.PlaceTee(3, at));
        var bytes = Save(world);
        var body = new MemoryStream();
        using (var gzip = new GZipStream(new MemoryStream(bytes, 28, bytes.Length - 28), CompressionMode.Decompress))
        {
            gzip.CopyTo(body);
        }

        var raw = body.ToArray();
        var pattern = BitConverter.GetBytes(3).Append((byte)1).Concat(BitConverter.GetBytes(at.X)).Concat(BitConverter.GetBytes(at.Z)).ToArray();
        var index = raw.AsSpan().IndexOf(pattern);
        Assert.True(index >= 0, "commande de trou introuvable dans le corps");
        raw[index] = 7;
        var forged = Pack(bytes, raw, Golferoo.Sim.Save.SaveFormat.Hash(raw));

        var e = Assert.Throws<InvalidDataException>(() => Load(forged));
        Assert.Contains("incohérente", e.Message);
    }

    /// <summary>C21 lot 2 : la note scratch trou par trou se sauvegarde et entre dans la somme de contrôle.</summary>
    [Fact]
    public void RatingByHole_RoundTrips_AndEntersTheChecksum()
    {
        World With(double[]? byHole)
        {
            var world = new World(1, Hole("proto"));
            world.SetCourseRating(new CourseRating(1, 4, 4.5, 5.5, byHole));
            world.Step();
            return world;
        }

        var a = With([4.5]);
        var loaded = Load(Save(a));

        Assert.Equal([4.5], loaded.CourseRating!.ScratchByHole!);
        Assert.Equal(a.Checksum(), loaded.Checksum());
        Assert.NotEqual(a.Checksum(), With([4.25]).Checksum());
        Assert.NotEqual(a.Checksum(), With(null).Checksum());
        Assert.Null(Load(Save(With(null))).CourseRating!.ScratchByHole);
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

    /// <summary>C14 lot 1 : au milieu d'un mois, après une clôture, un refus et des débits, avec des paramètres changés.</summary>
    [Fact]
    public void Ledger_ReloadedMidMonth_KeepsBalanceMonthsAndParameters()
    {
        var economy = EconomyTests.With(("startingMoney", 1_000_000), ("paintBunkerPerM2", 900), ("overdraftFreeHoles", 0), ("overdraftLimit", 0));
        var world = new World(4, Hole("dogleg"), economy: economy);
        world.PaintSurface(new Point2(150, 70), 6, Surface.Bunker);
        world.RaiseTerrain(new Point2(100, 40), 15, 1.5);
        world.Step();
        world.SkipToNextMonth();
        world.PaintSurface(new Point2(150, 70), 40, Surface.Water); // refusée : sous la limite de zéro
        world.PlantTree(60, 30, TreeSpeciesId.Birch, TreePlantStage.Sapling);
        world.Step(3 * Calendar.TicksPerDay);
        var ledger = world.Ledger;
        Assert.Equal(1, ledger.Refused);
        Assert.Single(ledger.Months);
        Assert.NotEqual(0, ledger.Current(LedgerCategory.Trees));

        var loaded = Load(Save(world));

        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Equal(economy, loaded.Economy);
        Assert.Equal(ledger.Balance, loaded.Ledger.Balance);
        Assert.Equal(ledger.Refused, loaded.Ledger.Refused);
        foreach (var c in Enum.GetValues<LedgerCategory>())
        {
            Assert.Equal(ledger.Current(c), loaded.Ledger.Current(c));
            Assert.Equal(ledger.Months[0].Total(c), loaded.Ledger.Months[0].Total(c));
        }

        Assert.Equal(ledger.Months[0].Month, loaded.Ledger.Months[0].Month);
        world.Step(Calendar.TicksPerDay * Calendar.DaysPerMonth);
        loaded.Step(Calendar.TicksPerDay * Calendar.DaysPerMonth);
        Assert.Equal(2, loaded.Ledger.Months.Count);
        Assert.Equal(world.Ledger.Balance, loaded.Ledger.Balance);
        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    /// <summary>Chaque champ du grand livre et des paramètres entre dans la somme de contrôle.</summary>
    [Fact]
    public void Ledger_EveryField_EntersTheChecksum()
    {
        var world = new World(4, Hole("dogleg"));
        var pristine = world.Checksum();
        var economyStart = world.Ledger.Balance;
        Assert.NotEqual(pristine, new World(4, Hole("dogleg"), economy: EconomyTests.With(("greenFeeGold", 801))).Checksum());

        var sums = new HashSet<ulong> { pristine };
        world.Ledger.Post(LedgerCategory.Dues, 1);
        Assert.True(sums.Add(world.Checksum()));
        world.Ledger.Post(LedgerCategory.GreenFees, -1); // même solde qu'au départ : seuls les totaux diffèrent
        Assert.Equal(economyStart, world.Ledger.Balance);
        Assert.True(sums.Add(world.Checksum()));
        world.Ledger.TrySpend(LedgerCategory.Holes, 100, floor: 0);
        Assert.True(sums.Add(world.Checksum()));
        world.Ledger.TrySpend(LedgerCategory.Holes, long.MaxValue / 2, floor: 0); // refusée
        Assert.True(sums.Add(world.Checksum()));
        world.Ledger.CloseMonth(7);
        Assert.True(sums.Add(world.Checksum()));
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

    /// <summary>Cherche (graines 1 à 400 : C25 lot 2 a rendu les coups pénalisés plus rares) un vol qui vérifie <paramref name="inFlight"/>, le sauve, le recharge et joue la partie jusqu'au bout des deux côtés.</summary>
    private static bool PlaysOutTheSameFromAFlight(Func<Golfer, bool> inFlight)
    {
        for (ulong seed = 1; seed <= 400; seed++)
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

    /// <summary>C17 lot 1 (v17) : les bâtiments posés se sauvent et se relisent ; ce cas échoue si la liste est oubliée de <c>Save</c> ou du <c>Checksum</c>.</summary>
    [Fact]
    public void PlacedBuildings_AreSavedLoadedAndChecksummed()
    {
        var world = new World(3, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, placeClubhouse: true);
        var before = world.Checksum();
        var spot = Enumerable.Range(0, world.Terrain!.CellsZ).SelectMany(z => Enumerable.Range(0, world.Terrain.CellsX).Select(x => (x, z)))
            .First(p => world.CheckBuilding(BuildingKind.Bench, p.x, p.z, DoorSide.South) == BuildingPlacement.Placed);
        Assert.Equal(BuildingPlacement.Placed, world.PlaceBuilding(BuildingKind.Bench, spot.x, spot.z, DoorSide.South));
        Assert.NotEqual(before, world.Checksum());

        var loaded = Load(Save(world));

        Assert.Equal(world.PlacedBuildings, loaded.PlacedBuildings);
        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    /// <summary>C15 lot 1 (v12) : le pavillon se sauve, se relit et entre dans la somme de contrôle ; un pavillon déplacé se relit à sa nouvelle place.</summary>
    [Fact]
    public void Clubhouse_IsSavedLoadedAndChecksummed()
    {
        var world = new World(3, ClubhouseTests.LeNeuf(), league: true, placeClubhouse: true);
        var bare = new World(3, ClubhouseTests.LeNeuf(), league: true);
        Assert.NotEqual(bare.Checksum(), world.Checksum());

        var loaded = Load(Save(world));
        Assert.Equal(world.Clubhouse, loaded.Clubhouse);
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Equal(Save(world), Save(loaded));

        // Déplacé puis rechargé : même position, mêmes champs en attente, mêmes pas ensuite.
        var t = world.Terrain!;
        var placed = world.Clubhouse;
        var moved = Enumerable.Range(0, t.CellsX / 4)
            .Select(k => new Point2(((k * 4) + 0.5) * t.CellSize, ((placed!.Footprint.MinZ + 4) + 0.5) * t.CellSize))
            .Any(p => world.TryMoveClubhouse(p, DoorSide.West));
        Assert.True(moved);
        Assert.NotEqual(placed, world.Clubhouse);
        var reloaded = Load(Save(world));
        Assert.Equal(world.Clubhouse, reloaded.Clubhouse);
        Assert.Equal(world.Checksum(), reloaded.Checksum());
        world.Step(200);
        reloaded.Step(200);
        Assert.Equal(world.Checksum(), reloaded.Checksum());
    }

    /// <summary>
    /// Une sauvegarde v11 (sans pavillon, sans vivier, 8 catégories au grand livre, sans clubhouseMove) se charge, avec un pavillon placé automatiquement.
    /// Le fichier v11 est fabriqué en retirant ces trois ajouts d'une sauvegarde v12 de monde frais.
    /// </summary>
    [Fact]
    public void Load_V11_PlacesTheClubhouseAutomatically()
    {
        var world = new World(5, ClubhouseTests.LeNeuf(), league: true, emptyClub: true);
        var v12 = Save(world);
        var raw = LegacyBody(world, Unpack(v12), 13);

        byte[] Bytes(Action<Golferoo.Sim.Save.SaveWriter> write)
        {
            var w = new Golferoo.Sim.Save.SaveWriter();
            write(w);
            return w.Written.ToArray();
        }

        byte[] Replace(byte[] all, byte[] what, byte[] with)
        {
            var at = all.AsSpan().IndexOf(what);
            Assert.True(at >= 0, "section introuvable dans le corps");
            return [.. all[..at], .. with, .. all[(at + what.Length)..]];
        }

        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(world.Economy.ToJson())!.AsObject();
        Assert.True(legacyJson.Remove("clubhouseMove"));
        raw = Replace(raw, Bytes(w => w.String(world.Economy.ToJson())), Bytes(w => w.String(legacyJson.ToJsonString())));

        var ledger = Bytes(w => world.Ledger.Write(w));
        var categories = Enum.GetValues<LedgerCategory>().Length;
        var legacyLedger = ledger[..(20 + (8 * (categories - 1)))].Concat(ledger[(20 + (8 * categories))..]).ToArray();
        raw = Replace(raw, ledger, legacyLedger);

        raw = raw[..^Bytes(world.WriteArrivals).Length]; // ni vivier, ni réputation (v13)
        Assert.Equal(0, raw[^1]); // pas de pavillon
        raw = raw[..^1];

        var file = (byte[])v12.Clone();
        file[4] = 11;
        var loaded = Load(Pack(file, raw, Golferoo.Sim.Save.SaveFormat.Hash(raw)));

        Assert.NotNull(loaded.Clubhouse);
        Assert.Equal(world.Economy.ClubhouseMove, loaded.Economy.ClubhouseMove);
        Assert.Equal(world.Ledger.Balance, loaded.Ledger.Balance);
        Assert.NotNull(loaded.ClubhouseField);
        Assert.Equal(new World(5, ClubhouseTests.LeNeuf(), league: true, placeClubhouse: true).Clubhouse, loaded.Clubhouse);
    }

    /// <summary>C15 lot 2 (v13) : le vivier, la file, les groupes en sortie, la réputation et le générateur des arrivées se sauvent, se relisent et entrent dans la somme de contrôle.</summary>
    [Fact]
    public void Arrivals_AreSavedLoadedAndChecksummed()
    {
        var world = ArrivalTests.NewWorld(3);
        world.Step(3 * Calendar.TicksPerDay);
        while (world.QueueLength == 0 && world.Tick < 30 * Calendar.TicksPerDay)
        {
            world.Step(100);
        }

        Assert.NotEmpty(world.Visitors.Visitors);
        Assert.NotEqual(world.Arrivals.ReputationStart, world.Reputation);
        Assert.True(world.QueueLength > 0);
        Assert.Contains(world.Groups, g => g.Released);
        Assert.Contains(world.Groups, g => g.FromArrivals);

        var loaded = World.Load(new MemoryStream(Save(world)), new StubShotResolver());
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Equal(Save(world), Save(loaded));
        Assert.Equal(world.Reputation, loaded.Reputation);
        Assert.Equal(world.QueueLength, loaded.QueueLength);
        Assert.Equal(world.Visitors.Visitors.Select(v => (v.Id, v.Name, v.Handicap, v.Profile, v.Desire, v.LastVisitDay, v.InPlay)),
            loaded.Visitors.Visitors.Select(v => (v.Id, v.Name, v.Handicap, v.Profile, v.Desire, v.LastVisitDay, v.InPlay)));
        Assert.Equal(world.Groups.Select(g => (g.Id, string.Join(",", g.Golfers.Select(o => o.VisitorId)))).ToArray(),
            loaded.Groups.Select(g => (g.Id, string.Join(",", g.Golfers.Select(o => o.VisitorId)))).ToArray());

        // Le vivier, la réputation et la file comptent dans la somme de contrôle : en changer un seul change la somme.
        var before = world.Checksum();
        world.Visitors.Visitors[0].Desire += 0.01;
        Assert.NotEqual(before, world.Checksum());
        world.Visitors.Visitors[0].Desire -= 0.01;
        Assert.Equal(before, world.Checksum());

        world.Step(2000);
        loaded.Step(2000);
        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    /// <summary>Une sauvegarde v12 (pavillon, sans vivier ni réputation) se charge avec un vivier vide et la réputation de départ.</summary>
    [Fact]
    public void Load_V12_StartsWithAnEmptyPool()
    {
        var world = new World(5, ClubhouseTests.LeNeuf(), league: true, placeClubhouse: true, emptyClub: true);
        var file = Save(world);
        var raw = LegacyBody(world, Unpack(file), 13);
        raw = raw[..^Bytes(world.WriteArrivals).Length];
        var loaded = Load(LegacyFile(file, raw, 12));

        Assert.Equal(world.Clubhouse, loaded.Clubhouse);
        Assert.Equal(40, loaded.Visitors.Visitors.Count); // les 40 membres de la graine deviennent des membres du vivier
        Assert.Equal(loaded.Arrivals.ReputationStart, loaded.Reputation);
    }

    /// <summary>C16 lot 1 (v14) : humeur, abandon, compteurs d'événements, attente, arrivée du groupe et réactions se sauvent, se relisent et entrent dans la somme de contrôle.</summary>
    [Fact]
    public void Mood_IsSavedLoadedAndChecksummed()
    {
        var world = ArrivalTests.NewWorld(3);
        world.Step(3 * Calendar.TicksPerDay);
        while (!world.Groups.Any(g => g.FromArrivals && g.Released) && world.Tick < 30 * Calendar.TicksPerDay)
        {
            world.Step(100);
        }

        var group = world.Groups.First(g => g.FromArrivals && g.Released);
        var golfer = group.Golfers[0];
        golfer.Mood = 3;
        golfer.Quit = true;
        golfer.Occurrences[(int)Golferoo.Sim.Golfers.MoodEvent.Water] = 2;
        golfer.WaitTicks = 77;
        world.Reactions.Add(1, Golferoo.Sim.Golfers.MoodEvent.Water);
        world.Reactions.CloseMonth();
        world.Reactions.Add(2, Golferoo.Sim.Golfers.MoodEvent.Bunker);
        world.Reactions.Add(3, Golferoo.Sim.Golfers.MoodEvent.TreeHit, (12, 34));
        world.Visitors.Visitors[group.Golfers[0].VisitorId].Games = 5;

        var loaded = World.Load(new MemoryStream(Save(world)), new StubShotResolver());
        var copy = loaded.Groups.First(g => g.Id == group.Id).Golfers[0];
        Assert.Equal((3, true, 2, 77), (copy.Mood, copy.Quit, copy.Occurrences[(int)Golferoo.Sim.Golfers.MoodEvent.Water], copy.WaitTicks));
        Assert.Equal(group.ArrivedTick, loaded.Groups.First(g => g.Id == group.Id).ArrivedTick);
        Assert.Equal(1, loaded.Reactions.Previous(1, Golferoo.Sim.Golfers.MoodEvent.Water));
        Assert.Equal(1, loaded.Reactions.Count(2, Golferoo.Sim.Golfers.MoodEvent.Bunker));
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Equal(Save(world), Save(loaded));

        // Chaque état compte dans la somme : en changer un seul la change.
        void Changes(Action change, Action undo)
        {
            var before = world.Checksum();
            change();
            Assert.NotEqual(before, world.Checksum());
            undo();
            Assert.Equal(before, world.Checksum());
        }

        Changes(() => golfer.Mood++, () => golfer.Mood--);
        Changes(() => golfer.Quit = false, () => golfer.Quit = true);
        Changes(() => golfer.Occurrences[0]++, () => golfer.Occurrences[0]--);
        Changes(() => golfer.WaitTicks++, () => golfer.WaitTicks--);
        Changes(() => group.ArrivedTick++, () => group.ArrivedTick--);
        var visitor = world.Visitors.Visitors[group.Golfers[0].VisitorId];
        Assert.Equal(5, loaded.Visitors.Visitors[visitor.Id].Games);
        Changes(() => visitor.Games++, () => visitor.Games--);
        var reactionsBefore = world.Checksum();
        world.Reactions.Add(3, Golferoo.Sim.Golfers.MoodEvent.Water);
        Assert.NotEqual(reactionsBefore, world.Checksum());

        // La cellule d'une réaction se sauve et compte dans la somme, à compteurs égaux.
        Assert.Equal(new Golferoo.Sim.Golfers.ReactionSpot(12, 34, Golferoo.Sim.Golfers.MoodEvent.TreeHit), loaded.Reactions.Spots[^1]);
        Assert.Equal(world.Reactions.Spots, loaded.Reactions.Spots);
        var (a, b) = (Load(Save(loaded)), Load(Save(loaded)));
        a.Reactions.Add(3, Golferoo.Sim.Golfers.MoodEvent.Water, (1, 2));
        b.Reactions.Add(3, Golferoo.Sim.Golfers.MoodEvent.Water, (1, 3));
        Assert.NotEqual(a.Checksum(), b.Checksum());
    }

    /// <summary>Seules les <see cref="Golferoo.Sim.Golfers.Reactions.MaxSpots"/> dernières cellules sont gardées.</summary>
    [Fact]
    public void ReactionSpots_AreBounded_KeepingTheMostRecent()
    {
        var reactions = new Golferoo.Sim.Golfers.Reactions();
        for (var i = 0; i < Golferoo.Sim.Golfers.Reactions.MaxSpots + 10; i++)
        {
            reactions.Add(1, Golferoo.Sim.Golfers.MoodEvent.Water, (i, 0));
        }

        Assert.Equal(Golferoo.Sim.Golfers.Reactions.MaxSpots, reactions.Spots.Count);
        Assert.Equal(10, reactions.Spots[0].X);
        Assert.Equal(Golferoo.Sim.Golfers.Reactions.MaxSpots + 9, reactions.Spots[^1].X);
    }

    /// <summary>
    /// Une vraie sauvegarde v15 produite par <c>main</c> (c4df5d2, graine 3, 12 h par pas, groupes en jeu, humeurs variées, membres promus, réactions comptées) :
    /// le niveau, l'humeur, les divisions, le solde et les compteurs survivent au chargement v16, avec zéro partie jouée et aucune cellule de réaction.
    /// </summary>
    [Fact]
    public void Load_RealV15Save_KeepsMoodMembersCountersAndStartsWithoutGamesOrSpots()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var expected = File.ReadAllLines(Path.Combine(dir, "v15-groups.txt"));
        var world = Load(File.ReadAllBytes(Path.Combine(dir, "v15-groups.sav")));

        Assert.Equal($"tick {world.Tick}", expected[0]);
        Assert.Equal($"balance {world.Ledger.Balance}", expected[1]);
        Assert.Equal($"members {world.League!.Members.Count}", expected[2]);
        Assert.Equal("divisions " + string.Join(";", world.League.Divisions.Select(d => string.Join(",", d.Select(m => m.Id)))), expected[3]);
        var golfers = world.Groups.SelectMany(g => g.Golfers).Where(o => o.VisitorId >= 0).Select(o => $"golfer {o.Id} {o.VisitorId} {o.Mood}").ToList();
        Assert.Equal(expected.Where(l => l.StartsWith("golfer ")), golfers);
        Assert.True(golfers.Select(x => x.Split(' ')[3]).Distinct().Count() > 3, "la fixture doit avoir des humeurs variées");
        Assert.Equal(expected.Where(l => l.StartsWith("visitor ")), world.Visitors.Visitors.Where(v => v.Points > 0).Select(v => $"visitor {v.Id} {(int)v.Level} {v.Points}"));
        var counters = Enumerable.Range(1, 9).SelectMany(h => Enum.GetValues<Golferoo.Sim.Golfers.MoodEvent>().Select(e => (h, e, c: world.Reactions.Count(h, e), p: world.Reactions.Previous(h, e)))).Where(x => x.c + x.p > 0);
        Assert.Equal(expected.Where(l => l.StartsWith("reaction ")), counters.Select(x => $"reaction {x.h} {(int)x.e} {x.c} {x.p}"));
        Assert.All(world.Visitors.Visitors, v => Assert.Equal(0, v.Games));
        Assert.Empty(world.Reactions.Spots);
        Assert.Equal(world.Checksum(), Load(Save(world)).Checksum());
    }

    /// <summary>Une sauvegarde v13 (sans compteurs de réactions) se charge avec des compteurs vides et l'humeur de départ des visiteurs.</summary>
    [Fact]
    public void Load_V13_StartsWithEmptyReactions()
    {
        var world = ArrivalTests.NewWorld(3);
        var file = Save(world);
        var loaded = Load(LegacyFile(file, LegacyBody(world, Unpack(file), 13), 13));

        Assert.True(loaded.Reactions.Untouched);
        Assert.Equal(world.Visitors.Visitors.Count, loaded.Visitors.Visitors.Count);
    }

    /// <summary>« Le neuf » à club vide avec son pavillon et sa ligue (fermée), résolveur de substitution : des groupes en file et en jeu, des visiteurs au vivier.</summary>
    private static World EmptyClubWithGroups(ulong seed = 3)
    {
        var world = new World(seed, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, placeClubhouse: true, emptyClub: true, arrivals: ArrivalParams.Default.AtMaxAttraction());
        while ((world.QueueLength == 0 || !world.Groups.Any(g => g.Released && g.FromArrivals && !g.Finished)) && world.Tick < 30 * Calendar.TicksPerDay)
        {
            world.Step(100);
        }

        Assert.True(world.QueueLength > 0 && world.Groups.Any(g => g.Released && g.FromArrivals), "pas de groupe en file et en jeu");
        return world;
    }

    /// <summary>C16 lot 2 (v15) : points, niveau, démission, trous aimés, membres de la ligue et commande « le pro rejoint un groupe » se sauvent, se relisent et entrent dans la somme de contrôle.</summary>
    [Fact]
    public void Membership_IsSavedLoadedAndChecksummed()
    {
        var world = EmptyClubWithGroups();
        var visitor = world.Visitors.Visitors[0];
        visitor.Points = 5;
        visitor.Level = MemberLevel.Silver;
        var other = world.Visitors.Visitors[1];
        other.Resigned = true;
        var playing = world.Groups.First(g => g.Released && g.FromArrivals).Golfers[0];
        playing.LikedHoles = 4;
        playing.LikedThisHole = true;
        world.League!.Enroll(new Golferoo.Sim.League.Member(world.League.Members.Count, "Tess T.", 12, profile: Archetype.Cautious, visitorId: 0));
        Assert.True(world.JoinQueuedGroup(world.Groups.First(g => g.FromArrivals && !g.Released).Id));

        var loaded = World.Load(new MemoryStream(Save(world)), new StubShotResolver());
        var (v, o) = (loaded.Visitors.Visitors[0], loaded.Visitors.Visitors[1]);
        Assert.Equal((5, MemberLevel.Silver, false, 0, MemberLevel.Visitor, true), (v.Points, v.Level, v.Resigned, o.Points, o.Level, o.Resigned));
        var copy = loaded.Groups.SelectMany(g => g.Golfers).First(g => g.Id == playing.Id);
        Assert.Equal((4, true), (copy.LikedHoles, copy.LikedThisHole));
        var member = loaded.League!.Members[^1];
        Assert.Equal(("Tess T.", 12.0, Archetype.Cautious, 0, false), (member.Name, member.StartHandicap, member.Profile, member.VisitorId, member.Resigned));
        Assert.False(loaded.CanJoinQueuedGroup(world.Groups.First(g => g.FromArrivals && !g.Released).Id)); // la commande attend déjà
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Equal(Save(world), Save(loaded));

        // Chaque état compte dans la somme : en changer un seul la change.
        void Changes(Action change, Action undo)
        {
            var before = world.Checksum();
            change();
            Assert.NotEqual(before, world.Checksum());
            undo();
            Assert.Equal(before, world.Checksum());
        }

        Changes(() => visitor.Points++, () => visitor.Points--);
        Changes(() => visitor.Level = MemberLevel.Gold, () => visitor.Level = MemberLevel.Silver);
        Changes(() => other.Resigned = false, () => other.Resigned = true);
        Changes(() => playing.LikedHoles++, () => playing.LikedHoles--);
        Changes(() => playing.LikedThisHole = false, () => playing.LikedThisHole = true);
        Changes(() => world.League.Members[^1].VisitorId = 1, () => world.League.Members[^1].VisitorId = 0);
        Changes(() => world.League.Members[^1].Resigned = true, () => world.League.Members[^1].Resigned = false);
        var joinQueued = world.Groups.First(g => g.FromArrivals && !g.Released);
        var swapped = world.Checksum();
        world.Step();
        loaded.Step();
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.NotEqual(swapped, world.Checksum());
        Assert.Contains(joinQueued.Golfers, g => g.MemberId == world.League.Members.First(m => m.IsPro).Id); // la commande s'est appliquée au tick suivant
    }

    /// <summary>
    /// C16 lot 2, critère 1 : une sauvegarde v13 ou v14 qui a des groupes en jeu se charge, et ses 40 membres de la graine deviennent des membres du vivier
    /// (même handicap, même division). Le fichier est fabriqué en ramenant le corps d'un monde à club vide à la disposition d'avant la v15.
    /// </summary>
    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    public void Load_BeforeV15_WithGroupsInPlay_MakesTheSeedMembersVisitors(int version)
    {
        var world = EmptyClubWithGroups();
        var file = Save(world);
        var loaded = Load(LegacyFile(file, LegacyBody(world, Unpack(file), version), version));

        var before = world.Visitors.Visitors.Count;
        var club = new Golferoo.Sim.League.LeagueSeason(world.Seed, Golferoo.Sim.League.LeagueSeason.GenerateMembers(world.Seed));
        Assert.Equal(before + 40, loaded.Visitors.Visitors.Count);
        Assert.Equal(club.Members.Count, loaded.League!.Members.Count);
        Assert.Equal(club.Divisions.Select(d => d.Select(m => m.Id)), loaded.League.Divisions.Select(d => d.Select(m => m.Id)));
        foreach (var m in club.Members.Where(m => !m.IsPro))
        {
            var member = loaded.League.Members[m.Id];
            var visitor = loaded.Visitors.Visitors[member.VisitorId];
            Assert.Equal((m.Name, m.StartHandicap, m.Profile), (member.Name, member.StartHandicap, member.Profile));
            Assert.Equal((m.Name, m.StartHandicap, m.Profile, MemberLevel.Member, false), (visitor.Name, visitor.Handicap, visitor.Profile, visitor.Level, visitor.Resigned));
            Assert.True(member.VisitorId >= before);
        }

        // Les groupes en jeu sont intacts (ceux de la v13 n'avaient pas d'humeur : ils repartent de l'humeur de départ).
        Assert.Equal(world.Groups.Select(g => (g.Id, g.Hole, g.Released)), loaded.Groups.Select(g => (g.Id, g.Hole, g.Released)));
        Assert.Equal(world.Groups.SelectMany(g => g.Golfers).Select(g => (g.Id, g.VisitorId, g.Position)), loaded.Groups.SelectMany(g => g.Golfers).Select(g => (g.Id, g.VisitorId, g.Position)));
        if (version == 14)
        {
            Assert.Equal(world.Groups.SelectMany(g => g.Golfers).Select(g => g.Mood), loaded.Groups.SelectMany(g => g.Golfers).Select(g => g.Mood));
        }

        // Le monde chargé joue, se resauve à l'identique, et ses membres paient leur cotisation.
        var again = Load(Save(loaded));
        Assert.Equal(loaded.Checksum(), again.Checksum());
        loaded.Step(4000);
        again.Step(4000);
        Assert.Equal(loaded.Checksum(), again.Checksum());
        Assert.Equal(40, loaded.Visitors.Visitors.Count(v => v.Level == MemberLevel.Member));
    }
}
