using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Time;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C16 lot 2 : adhésion, démission, ligue sur les vrais membres, pro qui se joint à un groupe de la file.</summary>
public class MembershipTests(ITestOutputHelper output)
{
    /// <summary>« Le neuf », pavillon, ligue à club vide (seul le pro). Résolveur de substitution pour aller vite sauf demande.</summary>
    private static World Empty(ulong seed = 1, bool physical = false, ArrivalParams? arrivals = null, bool league = true) =>
        new(seed, ClubhouseTests.LeNeuf(), physical ? null : new StubShotResolver(), league: league, placeClubhouse: true, emptyClub: league, arrivals: arrivals);

    private static readonly ArrivalParams NoArrivals = ArrivalParams.Default with { GroupsPerDay = 0 };

    private static void Days(World world, int days) => world.Step(days * Calendar.TicksPerDay);

    // --- la règle et ses données ---

    [Fact]
    public void Params_ThresholdsAre2_4_8_16_AndLevelsFollowThePoints()
    {
        var p = MembershipParams.Default;
        Assert.Equal([2, 4, 8, 16], p.Thresholds);
        Assert.Equal([MemberLevel.Visitor, MemberLevel.Visitor, MemberLevel.Member, MemberLevel.Member, MemberLevel.Silver, MemberLevel.Silver, MemberLevel.Gold, MemberLevel.Gold, MemberLevel.Platinum],
            new[] { 0, 1, 2, 3, 4, 7, 8, 15, 16 }.Select(p.LevelOf));
        Assert.Equal(MemberLevel.Platinum, p.LevelOf(10_000)); // pas de plafond
    }

    [Fact]
    public void Params_PointsFollowLikedHolesExitMoodAndThePro()
    {
        var p = MembershipParams.Default;
        Assert.Equal(0, p.PointsFor(0, 0, false));
        Assert.Equal(1, p.PointsFor(p.LikedHolesPerPoint, p.GoodExitMood - 1, false));
        Assert.Equal(1, p.PointsFor(0, p.GoodExitMood, false));
        Assert.Equal(2, p.PointsFor(p.LikedHolesPerPoint, p.GoodExitMood, false));
        Assert.Equal(p.ProPoints, p.PointsFor(0, 0, true) - p.PointsFor(0, 0, false));
    }

    [Theory]
    [InlineData("""{"thresholds":[2,4,8],"likedHolesPerPoint":3,"goodExitMood":7,"goodExitPoints":1,"proPoints":1}""")]
    [InlineData("""{"thresholds":[2,4,4,16],"likedHolesPerPoint":3,"goodExitMood":7,"goodExitPoints":1,"proPoints":1}""")]
    [InlineData("""{"thresholds":[2,4,8,16],"likedHolesPerPoint":0,"goodExitMood":7,"goodExitPoints":1,"proPoints":1}""")]
    [InlineData("""{"thresholds":[2,4,8,16],"likedHolesPerPoint":3,"goodExitMood":7,"goodExitPoints":1}""")]
    [InlineData("not json")]
    public void Params_RefuseABadFile(string json) => Assert.Throws<InvalidDataException>(() => MembershipParams.Parse(json));

    // --- l'adhésion d'une partie à l'autre ---

    /// <summary>Chaque partie finie rapporte les points de la règle : trous aimés et humeur de sortie, rien pour qui a abandonné.</summary>
    [Fact]
    public void EachFinishedGame_EarnsThePointsOfTheRule()
    {
        var world = Empty(league: false);
        var golfers = new Dictionary<int, Golfer>();
        var before = new Dictionary<int, int>();
        world.GroupArrived += g =>
        {
            foreach (var o in g.Golfers)
            {
                golfers[o.VisitorId] = o;
                before[o.VisitorId] = world.Visitors.Visitors[o.VisitorId].Points;
            }
        };
        var checkedGames = 0;
        world.GroupLeft += visitors =>
        {
            foreach (var v in visitors)
            {
                var g = golfers[v.Id];
                var wanted = g.Quit ? 0 : world.Memberships.PointsFor(g.LikedHoles, g.Mood, false);
                Assert.Equal(before[v.Id] + wanted, v.Points);
                Assert.Equal(world.Memberships.LevelOf(v.Points), v.Resigned ? MemberLevel.Visitor : v.Level);
                checkedGames += g.LikedHoles > 0 ? 1 : 0;
            }
        };
        Days(world, 6);

        Assert.True(checkedGames > 0, "aucune partie finie avec un trou aimé");
    }

    [Fact]
    public void FromAnEmptyClub_VisitorsBecomeMembersThenPromoted_EachByAnEvent_AndJoinThePool()
    {
        var world = Empty();
        var events = new List<(string Kind, Visitor Who, MemberLevel Level)>();
        world.MemberJoined += v => events.Add(("joined", v, v.Level));
        world.MemberPromoted += v => events.Add(("promoted", v, v.Level));
        Days(world, 60);

        Assert.Contains(events, e => e.Kind == "joined");
        Assert.Contains(events, e => e.Kind == "promoted");
        foreach (var promoted in events.Where(e => e.Kind == "promoted"))
        {
            Assert.True(events.IndexOf(promoted) > events.FindIndex(e => e.Kind == "joined" && e.Who == promoted.Who), "promu avant d'avoir adhéré");
        }

        var members = world.Visitors.Visitors.Where(v => v.Level != MemberLevel.Visitor).ToList();
        Assert.Equal(members.Count, world.League!.Members.Count(m => !m.IsPro && !m.Resigned));
        Assert.All(members, v => Assert.Equal(world.Memberships.LevelOf(v.Points), v.Level));
        output.WriteLine($"{events.Count(e => e.Kind == "joined")} adhésions, {events.Count(e => e.Kind == "promoted")} promotions, {members.Count} membres");
    }

    /// <summary>Le calage de la CI : le résolveur physique, une semaine et demie ; des membres apparaissent, et la bascule de la règle se voit par les évènements.</summary>
    [Fact]
    public void LeNeuf_PhysicalTenDays_SomeVisitorsJoin()
    {
        var world = Empty(physical: true);
        var joined = 0;
        world.MemberJoined += _ => joined++;
        Days(world, 10);

        Assert.True(joined >= 1, "personne n'a adhéré en 10 jours");
        Assert.True(joined < world.Visitors.Visitors.Count / 2, "presque tous les visiteurs adhèrent : la règle est trop généreuse");
    }

    /// <summary>Critère 4 sur 60 jours, résolveur physique (environ 10 minutes) : lancé si <c>GOLFEROO_LONG</c> est défini.</summary>
    [Fact]
    public void LeNeuf_PhysicalSixtyDays_AMemberThenASilver()
    {
        if (Environment.GetEnvironmentVariable("GOLFEROO_LONG") is null)
        {
            return;
        }

        var world = Empty(physical: true);
        var order = new List<string>();
        world.MemberJoined += v => order.Add("joined");
        world.MemberPromoted += v => order.Add(v.Level == MemberLevel.Silver ? "silver" : "promoted");
        Days(world, 60);

        Assert.Contains("joined", order);
        Assert.Contains("silver", order);
        Assert.True(order.IndexOf("joined") < order.IndexOf("silver"));
    }

    // --- démission ---

    [Fact]
    public void AMemberWhoQuits_Resigns_AndNeverComesBack()
    {
        var world = Empty();
        Visitor? resigned = null;
        var raised = 0;
        world.MemberResigned += v =>
        {
            resigned = v;
            raised++;
        };
        var forced = false;
        for (var i = 0; i < 40 * Calendar.TicksPerDay && !forced; i++)
        {
            world.Step();
            var member = world.Groups.Where(g => g.FromArrivals && g.Released && g.Hole >= 2 && g.Hole < 9)
                .SelectMany(g => g.Golfers).FirstOrDefault(g => world.Visitors.Visitors[g.VisitorId].Level != MemberLevel.Visitor && !g.Quit);
            if (member is not null)
            {
                member.Mood = -3; // sous zéro à la fin du trou : il abandonne
                forced = true;
            }
        }

        Assert.True(forced, "aucun membre en jeu");
        for (var i = 0; i < Calendar.TicksPerDay && resigned is null; i++)
        {
            world.Step();
        }

        Assert.NotNull(resigned);
        Assert.Equal(1, raised);
        Assert.True(resigned.Resigned);
        Assert.Equal(MemberLevel.Visitor, resigned.Level);
        var seat = world.League!.Members.Single(m => m.VisitorId == resigned.Id);
        Assert.True(seat.Resigned);
        Assert.DoesNotContain(seat, world.League.Divisions.SelectMany(d => d));

        // Il ne revient jamais : ni dans un groupe qui arrive, ni dans la liste des visiteurs qui peuvent revenir.
        var last = world.Visitors.Visitors.Count;
        var back = false;
        var wasInPlay = false;
        for (var d = 0; d < 20; d++)
        {
            Days(world, 1);
            var inGroup = world.Groups.SelectMany(g => g.Golfers).Any(g => g.VisitorId == resigned.Id && !g.Quit);
            wasInPlay |= d > 0 && inGroup;
            back |= d > 0 && inGroup;
        }

        Assert.False(back);
        Assert.True(world.Visitors.Visitors.Count > last);
        Assert.Equal(MemberLevel.Visitor, resigned.Level);
    }

    // --- ligue ---

    private static Member Recruit(World world, string name, double handicap)
    {
        var visitor = world.Visitors.Add(name, handicap, Archetype.Neutral, 0.5, MemberLevel.Member, world.Memberships.Thresholds[0]);
        var member = new Member(world.League!.Members.Count, name, handicap, profile: visitor.Profile, visitorId: visitor.Id);
        world.League.Enroll(member);
        return member;
    }

    [Fact]
    public void League_ClosedWithSixMembers_OpensADivisionAtSeven_AndNoRoundBeforeTheyExist()
    {
        var world = Empty(arrivals: NoArrivals);
        for (var i = 0; i < 6; i++)
        {
            Recruit(world, $"M{i}", 10 + i);
        }

        Assert.False(world.League!.Open);
        Assert.Empty(world.League.Divisions);
        Days(world, 12);
        Assert.Equal(-1, world.League.Rounds[0].StartDay); // la journée du 5 avril est passée sans ligue
        Assert.DoesNotContain(world.Groups, g => g.LeagueRound >= 0);

        Recruit(world, "M6", 16);
        Assert.True(world.League.Open);
        Assert.Single(world.League.Divisions);
        Assert.Equal(8, world.League.Divisions[0].Count);
        Assert.Contains(world.League.Divisions[0], m => m.IsPro);
    }

    [Fact]
    public void League_NextRound_OnlyRealMembersAndThePro()
    {
        var world = Empty(arrivals: NoArrivals);
        for (var i = 0; i < 7; i++)
        {
            Recruit(world, $"M{i}", 10 + i);
        }

        var real = world.League!.Members.Where(m => !m.IsPro).Select(m => m.Id).ToHashSet();
        var seen = new HashSet<int>();
        for (var i = 0; i < 5 * Calendar.TicksPerDay; i++)
        {
            world.Step();
            foreach (var g in world.Groups.Where(g => g.LeagueRound >= 0))
            {
                foreach (var o in g.Golfers)
                {
                    seen.Add(o.MemberId);
                }
            }
        }

        Assert.NotEmpty(seen);
        Assert.All(seen, id => Assert.True(real.Contains(id) || world.League.Members[id].IsPro));
        Assert.All(seen.Where(id => !world.League.Members[id].IsPro), id => Assert.True(world.League.Members[id].VisitorId >= 0));
    }

    [Fact]
    public void League_AMemberWhoResigns_LeavesTheDivision_AndEightNoLongerOpenWithoutHim()
    {
        var world = Empty();
        var first = Recruit(world, "M0", 10);
        for (var i = 1; i < 6; i++)
        {
            Recruit(world, $"M{i}", 10 + i);
        }

        world.League!.Resign(first);
        Recruit(world, "M6", 16); // 5 membres + 1 qui part + 1 : 6 en tout, plus le pro, donc 7 : fermée
        Assert.False(world.League.Open);
        Recruit(world, "M7", 17);
        Assert.True(world.League.Open);
        Assert.DoesNotContain(first, world.League.Divisions.SelectMany(d => d));
    }

    [Fact]
    public void ClubStart_EmptyHasOnlyThePro_PrefilledHas40MembersInThePool()
    {
        var empty = Empty();
        Assert.Single(empty.League!.Members);
        Assert.Empty(empty.Visitors.Visitors);

        var full = new World(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, placeClubhouse: true);
        Assert.Equal(41, full.League!.Members.Count);
        Assert.Equal(40, full.Visitors.Visitors.Count(v => v.Level == MemberLevel.Member));
        Assert.All(full.League.Members.Where(m => !m.IsPro), m => Assert.Equal(m.Name, full.Visitors.Visitors[m.VisitorId].Name));
        Assert.True(full.League.Open);
    }

    // --- tarifs selon le niveau ---

    [Fact]
    public void GreenFeeAndDues_FollowTheLevel()
    {
        var world = Empty(league: true);
        var visitors = Enum.GetValues<MemberLevel>().Select(l =>
        {
            var v = world.Visitors.Add($"V{l}", 10, Archetype.Neutral, 0.5, l);
            return (v, golfer: new Golfer(100 + (int)l, default, default) { VisitorId = v.Id, Mood = 0 });
        }).ToList();
        var e = world.Economy;
        Assert.Equal([e.GreenFeeVisitor, e.GreenFeeMember, e.GreenFeeSilver, e.GreenFeeGold, e.GreenFeePlatinum], visitors.Select(x => world.GreenFee(x.golfer)).ToArray());
        Assert.True(e.GreenFeeVisitor > e.GreenFeeMember && e.GreenFeeMember > e.GreenFeeSilver && e.GreenFeeSilver > e.GreenFeeGold && e.GreenFeeGold > e.GreenFeePlatinum);

        world.SkipToNextMonth();
        Assert.Equal((e.DuesMember + e.DuesSilver + e.DuesGold + e.DuesPlatinum) / Calendar.MonthsPerYear, world.Ledger.Months[0].Total(Golferoo.Sim.Economy.LedgerCategory.Dues));
    }

    // --- le pro et la file ---

    /// <summary>Un monde où plusieurs groupes font la queue : arrivées à leur maximum, parcours bloqué par un groupe de ligue en tête n'est pas nécessaire, la file se remplit seule.</summary>
    private static World WithQueue(ulong seed = 3, int groups = 2)
    {
        var world = new World(seed, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: true, placeClubhouse: true, emptyClub: true, arrivals: ArrivalParams.Default.AtMaxAttraction());
        for (var i = 0; i < 60_000 && world.Groups.Count(g => g.FromArrivals && !g.Released) < groups; i++)
        {
            world.Step();
        }

        Assert.True(world.Groups.Count(g => g.FromArrivals && !g.Released) >= groups, "pas assez de groupes en file");
        return world;
    }

    [Fact]
    public void JoinQueuedGroup_PutsTheGroupFirstInTheQueue_AndTheProLeavesWithIt()
    {
        var world = WithQueue(groups: 3);
        var queue = world.Groups.Where(g => g.FromArrivals && !g.Released).ToList();
        var chosen = queue[^1];
        var proId = world.League!.Members.First(m => m.IsPro).Id;
        Group? joined = null;
        world.ProJoinedGroup += g => joined = g;
        var moods = new List<MoodChange>();
        world.MoodChanged += moods.Add;

        Assert.True(world.CanJoinQueuedGroup(chosen.Id));
        Assert.True(world.JoinQueuedGroup(chosen.Id));
        Assert.False(world.JoinQueuedGroup(chosen.Id)); // une seule commande à la fois
        Assert.DoesNotContain(chosen.Golfers, g => g.MemberId == proId); // la commande s'applique au tick suivant
        world.Step();

        Assert.Same(chosen, joined);
        Assert.Contains(chosen.Golfers, g => g.MemberId == proId);
        Assert.Equal(chosen, world.Groups.First(g => g.FromArrivals && !g.Released));
        var visitors = chosen.Golfers.Where(g => g.VisitorId >= 0).ToList();
        Assert.All(visitors, v => Assert.Contains(moods, c => c.Golfer == v && c.Event == MoodEvent.ProPlaysWithUs && c.Hole == 1));
        Assert.DoesNotContain(moods, c => c.Golfer.MemberId == proId); // le pro n'a pas d'humeur
        Assert.False(world.CanJoinQueuedGroup(queue[0].Id)); // le pro est pris

        for (var i = 0; i < 4 * Calendar.TicksPerDay && !chosen.Released; i++)
        {
            world.Step();
        }

        Assert.True(chosen.Released);
        Assert.DoesNotContain(world.Groups.TakeWhile(g => g != chosen), g => g.FromArrivals && !g.Released); // aucun groupe de la file ne l'a doublé en sens inverse
    }

    [Fact]
    public void JoinQueuedGroup_RefusedWhenTheGroupIsNotQueued_OrTheProPlays()
    {
        var world = WithQueue();
        Assert.False(world.JoinQueuedGroup(-5));
        var released = world.Groups.FirstOrDefault(g => g.FromArrivals && g.Released);
        if (released is not null)
        {
            Assert.False(world.JoinQueuedGroup(released.Id));
        }

        var training = world.StartProRound(); // le pro joue déjà ailleurs
        Assert.False(world.JoinQueuedGroup(world.Groups.First(g => g.FromArrivals && !g.Released).Id));
        Assert.Contains(training, world.Groups);
        Assert.False(new World(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), placeClubhouse: true).JoinQueuedGroup(0)); // pas de ligue, pas de pro
    }

    [Fact]
    public void JoinQueuedGroup_AMemberWithTheProEarnsOnePointMore()
    {
        Assert.Equal(MembershipParams.Default.ProPoints, MembershipParams.Default.PointsFor(0, 0, true));

        var world = WithQueue(groups: 1);
        var group = world.Groups.First(g => g.FromArrivals && !g.Released);
        var host = world.Visitors.Visitors[group.Golfers[0].VisitorId];
        host.Level = MemberLevel.Member;
        host.Points = 2;
        Assert.True(world.JoinQueuedGroup(group.Id));
        var before = host.Points;
        var golfer = group.Golfers[0];
        var left = false;
        world.GroupLeft += visitors => left |= visitors.Contains(host);
        for (var i = 0; i < 6 * Calendar.TicksPerDay && !left; i++)
        {
            world.Step();
        }

        Assert.True(left, "le groupe n'est pas sorti");
        Assert.Equal(before + world.Memberships.PointsFor(golfer.LikedHoles, golfer.Mood, true), host.Points);
        Assert.DoesNotContain(world.Groups, g => g.Golfers.Any(o => o.MemberId == world.League!.Members.First(m => m.IsPro).Id) && g == group);
    }

    [Fact]
    public void JoinQueuedGroup_SameSeedSameCommands_SameChecksum()
    {
        ulong Run()
        {
            var world = WithQueue(7, 2);
            world.JoinQueuedGroup(world.Groups.Last(g => g.FromArrivals && !g.Released).Id);
            Days(world, 2);
            return world.Checksum();
        }

        Assert.Equal(Run(), Run());
    }

    // --- déterminisme et sauvegarde d'une partie qui avance ---

    [Fact]
    public void TenDays_WithMembershipAndTheProJoining_ReloadedHasTheSameChecksum()
    {
        static World Reload(World w)
        {
            using var stream = new MemoryStream();
            w.Save(stream);
            return World.Load(new MemoryStream(stream.ToArray()), new StubShotResolver());
        }

        var started = WithQueue(5, 2);
        started.JoinQueuedGroup(started.Groups.First(g => g.FromArrivals && !g.Released).Id);
        Days(started, 3);
        var world = Reload(started); // les paramètres d'arrivée ne sont pas sauvés : les deux mondes repartent des mêmes (par défaut)
        var loaded = Reload(world);
        Assert.Equal(started.Checksum(), world.Checksum());
        Assert.Equal(world.Checksum(), loaded.Checksum());
        Days(world, 7);
        Days(loaded, 7);

        Assert.Equal(world.Checksum(), loaded.Checksum());
        Assert.Contains(world.Visitors.Visitors, v => v.Points > 0);
    }

    /// <summary>
    /// Une vraie sauvegarde v14 produite par <c>main</c> (ac0a7bc, graine 3, groupes en jeu, humeurs variées) : l'humeur, les membres, les divisions et le solde survivent au chargement v15.
    /// </summary>
    [Fact]
    public void Load_RealV14Save_KeepsMoodMembersDivisionsAndBalance()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var expected = File.ReadAllLines(Path.Combine(dir, "v14-groups.txt"));
        var world = World.Load(File.OpenRead(Path.Combine(dir, "v14-groups.sav")), new StubShotResolver());

        Assert.Equal($"tick {world.Tick}", expected[0]);
        Assert.Equal($"balance {world.Ledger.Balance}", expected[1]);
        Assert.Equal($"members {world.League!.Members.Count}", expected[2]);
        Assert.Equal("divisions " + string.Join(";", world.League.Divisions.Select(d => string.Join(",", d.Select(m => m.Id)))), expected[3]);
        var golfers = world.Groups.SelectMany(g => g.Golfers).Where(o => o.VisitorId >= 0).Select(o => $"golfer {o.Id} {o.VisitorId} {o.Mood}").ToList();
        Assert.Equal(expected.Skip(4), golfers);
        Assert.True(golfers.Select(x => x.Split(' ')[3]).Distinct().Count() > 3, "la fixture doit avoir des humeurs variées");
        Assert.Equal(40, world.Visitors.Visitors.Count(v => v.Level == MemberLevel.Member)); // les 40 membres de la graine, adoptés
        Assert.Equal(world.Checksum(), World.Load(new MemoryStream(Resave(world)), new StubShotResolver()).Checksum());
    }

    private static byte[] Resave(World world)
    {
        using var stream = new MemoryStream();
        world.Save(stream);
        return stream.ToArray();
    }
}
