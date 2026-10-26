using Golferoo.Sim.League;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Weather;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

public class LeagueTests(ITestOutputHelper output)
{
    private static List<Member> Members(int enrolled, int notEnrolled = 0)
    {
        var list = new List<Member>();
        for (var i = 0; i < enrolled; i++)
        {
            list.Add(new Member(i, $"M{i}", 2 + i, 60 + (i % 40)));
        }

        for (var i = 0; i < notEnrolled; i++)
        {
            list.Add(new Member(100 + i, $"X{i}", 10, 59 - i));
        }

        list.Add(new Member(999, "Pro", 36, 100, isPro: true));
        return list;
    }

    private static int[] Sizes(LeagueSeason s) => [.. s.Divisions.Select(d => d.Count)];

    [Fact]
    public void GenerateMembers_Gives40MembersAndAProAtHandicap36()
    {
        var members = LeagueSeason.GenerateMembers(5);

        Assert.Equal(41, members.Count);
        Assert.All(members.Take(40), m => Assert.InRange(m.StartHandicap, 2, 36));
        Assert.All(members.Take(40), m => Assert.InRange(m.Opinion, 0, 100));
        Assert.Equal(36, members[^1].StartHandicap);
        Assert.True(members[^1].IsPro);
        Assert.Equal(members.Select(m => m.Name), LeagueSeason.GenerateMembers(5).Select(m => m.Name));
    }

    [Fact]
    public void Enrollment_OnlyOpinionAtLeast60AndThePro()
    {
        var season = new LeagueSeason(1, Members(10, notEnrolled: 5));

        var enrolled = season.Divisions.SelectMany(d => d).ToList();
        Assert.Equal(11, enrolled.Count);
        Assert.All(enrolled, m => Assert.True(m.IsPro || m.Opinion >= 60));
    }

    [Fact]
    public void Divisions_15EnrolledOneDivision_16Two_21Eleven10()
    {
        Assert.Single(new LeagueSeason(1, Members(14)).Divisions);
        Assert.Equal(2, new LeagueSeason(1, Members(15)).Divisions.Count);
        Assert.Equal([11, 10], Sizes(new LeagueSeason(1, Members(20))));
    }

    [Fact]
    public void Divisions_RankedByLevel_ProAtTheBottomOfTheLast()
    {
        var season = new LeagueSeason(1, Members(20));

        Assert.True(season.Divisions[^1][^1].IsPro);
        var others = season.Divisions.SelectMany(d => d).Where(m => !m.IsPro).Select(m => m.StartHandicap).ToList();
        Assert.Equal(others.Order(), others);
    }

    [Fact]
    public void Calendar_TenRoundsOnThe5thAnd10th_FormatsFollowTheCycle()
    {
        var season = new LeagueSeason(1, Members(20));

        Assert.Equal(10, season.Rounds.Count);
        Assert.Equal([4, 9, 14, 19, 24, 29, 34, 39, 44, 49], season.Rounds.Select(r => r.NominalDay));
        var cycle = new[] { LeagueFormat.StrokePlay, LeagueFormat.Stableford, LeagueFormat.MatchPlay, LeagueFormat.VersusPar };
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(cycle[i % 4], season.Rounds[i].Format);
        }

        var last = Calendar.At((long)season.Rounds[9].NominalDay * Calendar.TicksPerDay, 0);
        Assert.Equal((10, 8), (last.Day, last.Month));
        var first = Calendar.At((long)season.Rounds[0].NominalDay * Calendar.TicksPerDay, 0);
        Assert.Equal((5, 4), (first.Day, first.Month));
    }

    [Fact]
    public void Storm_PostponesTheRoundToTheNextDay()
    {
        var season = new LeagueSeason(1, Members(20));

        Assert.Null(season.OnNewDay(3, false, 0));
        Assert.Null(season.OnNewDay(4, storm: true, 0));
        Assert.Null(season.OnNewDay(5, storm: true, 0));
        var round = season.OnNewDay(6, false, 0);

        Assert.NotNull(round);
        Assert.Equal(6, round.StartDay);
        Assert.Equal(0, round.Number);
        // Le report ne décale pas le calendrier prévu : la suivante reste au 10.
        Assert.Null(season.OnNewDay(8, false, 0));
        Assert.Equal(1, season.OnNewDay(9, false, 0)?.Number);
    }

    [Fact]
    public void Points_BehindPlusHalfPerTie_AndCardsFeedTheIndex()
    {
        var season = new LeagueSeason(1, Members(7));
        var round = season.OnNewDay(4, false, 0)!;
        var rating = new CourseRating(1, 4, 4.0, 5.0);

        // Division unique de 8 (7 + pro) : bruts 4, 3, 4, 6, 5, 4, 6 puis 9 pour le pro.
        var gross = new Dictionary<int, int> { [0] = 4, [1] = 3, [2] = 4, [3] = 6, [4] = 5, [5] = 4, [6] = 6, [999] = 9 };
        foreach (var pair in round.Pairs)
        {
            season.GroupFinished(0, [.. pair.Select(id => (id, Card(gross[id])))], rating, 100);
        }

        Assert.True(round.Closed);
        Assert.Equal(7.0, round.Points[1]);
        Assert.Equal(5.0, round.Points[0]);
        Assert.Equal(5.0, round.Points[2]);
        Assert.Equal(5.0, round.Points[5]);
        Assert.Equal(3.0, round.Points[4]);
        Assert.Equal(1.5, round.Points[3]);
        Assert.Equal(1.5, round.Points[6]);
        Assert.Equal(0.0, round.Points[999]);
        Assert.Equal(round.Points[1], season.Members.First(m => m.Id == 1).SeasonPoints);

        // Le handicap se calcule à partir des cartes : 3 cartes de 4 coups sur un 4 de note 4,0 donnent un écart nul.
        Assert.Null(season.Members[0].Index(1));
        for (var r = 1; r <= 2; r++)
        {
            season.Members[0].AddCard(new LeagueCard(r, 4, Handicap.Differential(4, rating, 1)));
        }

        Assert.Equal(0.0, season.Members[0].Index(1));
    }

    [Fact]
    public void MatchPlay_WinnersThenHalvedThenLosers_SoloPlayerHalved()
    {
        var season = new LeagueSeason(1, Members(8));
        for (var i = 0; i < 2; i++)
        {
            season.OnNewDay(4 + (5 * i), false, 0);
        }

        var round = season.OnNewDay(14, false, 0)!;
        Assert.Equal(LeagueFormat.MatchPlay, round.Format);
        Assert.Single(round.Pairs, p => p.Length == 1);
        var rng = new Random(3);
        foreach (var pair in round.Pairs)
        {
            season.GroupFinished(2, [.. pair.Select(id => (id, Card(3 + rng.Next(4))))], null, 5);
        }

        var order = season.Divisions[0].OrderByDescending(m => round.Points[m.Id]).ToList();
        var categories = order.Select(m => Category(round, m.Id)).ToList();
        Assert.Equal(categories.Order(), categories);
    }

    private static int Category(LeagueRound round, int id)
    {
        var pair = round.Pairs.First(p => p.Contains(id));
        if (pair.Length == 1)
        {
            return 1;
        }

        var other = pair[0] == id ? pair[1] : pair[0];
        var r = MatchResult.Play(round.Cards[id], round.Cards[other]);
        return r.Won > r.Lost ? 0 : r.Won < r.Lost ? 2 : 1;
    }

    private static ScoreCard Card(int strokes)
    {
        var card = new ScoreCard([4]);
        card.Record(0, strokes);
        return card;
    }

    [Fact]
    public async Task SimulatedSeason_EveryEnrolledMemberHasTenCards_PointsFollowTheRule_AndItIsDeterministic()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(Season)));
        var (world, checksum) = results[0];

        Assert.Equal(checksum, results[1].Checksum);
        var league = world.League!;
        Assert.True(league.Finished);
        Assert.Equal(league.Members.Select(m => m.SeasonPoints), results[1].World.League!.Members.Select(m => m.SeasonPoints));

        var enrolled = league.Divisions.SelectMany(d => d).ToHashSet();
        foreach (var m in league.Members)
        {
            Assert.Equal(enrolled.Contains(m) ? 10 : 0, m.Cards.Count);
        }

        Assert.All(enrolled, m => Assert.NotNull(m.Index(1)));
        var ties = 0;
        foreach (var round in league.Rounds)
        {
            output.WriteLine($"Journée {round.Number + 1} {round.Format}: jour {round.StartDay} (prévu {round.NominalDay}), durée {(round.EndTick - round.StartTick) / (double)Calendar.TicksPerDay:F2} jour(s) de jeu");
            Assert.True(round.Closed);
            Assert.True(round.EndTick - round.StartTick < 3L * Calendar.TicksPerDay);
            for (var d = round.NominalDay; d < round.StartDay; d++)
            {
                Assert.Equal(Sky.HeavyRain, WeatherGenerator.For(world.Seed, world.Climate, d).Sky);
            }

            foreach (var division in league.Divisions)
            {
                var keys = division.ToDictionary(m => m.Id, m => Key(round, m.Id));
                foreach (var m in division)
                {
                    var expected = division.Where(o => o != m).Sum(o => keys[o.Id] > keys[m.Id] ? 1.0 : keys[o.Id] == keys[m.Id] ? 0.5 : 0.0);
                    Assert.Equal(expected, round.Points[m.Id]);
                    ties += division.Count(o => o != m && keys[o.Id] == keys[m.Id]) > 0 ? 1 : 0;
                }
            }
        }

        Assert.True(ties > 0);
        output.WriteLine($"Inscrits {enrolled.Count}, divisions {string.Join('+', league.Divisions.Select(d => d.Count))}, ex aequo {ties}");
    }

    private static int Key(LeagueRound round, int id) => round.Format switch
    {
        LeagueFormat.StrokePlay => round.Cards[id].Gross,
        LeagueFormat.Stableford => -round.Cards[id].Stableford(),
        LeagueFormat.VersusPar => -round.Cards[id].VersusPar(),
        _ => (Category(round, id) * 1000) + round.Cards[id].Gross,
    };

    private static (World World, ulong Checksum) Season()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var world = new World(11, terrain, league: true);
        world.SetCourseRating(CourseRater.Rate(HoleLoader.Load(TerrainTests.HolePath(), 2), 11, 15));
        while (!world.League!.Finished && world.DayIndex < 190)
        {
            world.Step(Calendar.TicksPerDay);
        }

        return (world, world.Checksum());
    }

    [Fact]
    public void FirstRound_With12Groups_FinishesInUnderThreeDays()
    {
        var seed = Enumerable.Range(1, 500).First(x => new LeagueSeason((ulong)x, LeagueSeason.GenerateMembers((ulong)x)).Divisions.Sum(d => d.Count) >= 23);
        var world = new World((ulong)seed, HoleLoader.Load(TerrainTests.HolePath(), 2), league: true) { Climate = Climate.Calm };
        while (!world.League!.Rounds[0].Closed && world.DayIndex < 20)
        {
            world.Step(Calendar.TicksPerDay / 4);
        }

        var round = world.League.Rounds[0];
        output.WriteLine($"{round.Pairs.Count} groupes : {(round.EndTick - round.StartTick) / (double)Calendar.TicksPerDay:F2} jour(s) de jeu");
        Assert.True(round.Pairs.Count >= 11);
        Assert.True(round.EndTick - round.StartTick < 3L * Calendar.TicksPerDay);
    }

    [Fact]
    public void NoLeague_ByDefault_NoMembersAndNoChange()
    {
        var plain = new World(11, HoleLoader.Load(TerrainTests.HolePath(), 2));
        plain.Step(Calendar.TicksPerDay * 6);

        Assert.Null(plain.League);
        Assert.Empty(plain.Groups);
    }
}
