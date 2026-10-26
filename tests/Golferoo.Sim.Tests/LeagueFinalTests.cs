using Golferoo.Sim.League;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

public class LeagueFinalTests(ITestOutputHelper output)
{
    private static List<Member> Members(int enrolled)
    {
        var list = new List<Member>();
        for (var i = 0; i < enrolled; i++)
        {
            list.Add(new Member(i, $"M{i}", 2 + i, 60 + (i % 40)));
        }

        list.Add(new Member(999, "Pro", 36, 100, isPro: true));
        return list;
    }

    private static ScoreCard Card(int strokes)
    {
        var card = new ScoreCard([4]);
        card.Record(0, strokes);
        return card;
    }

    /// <summary>Joue les jours [from, to] à la main : chaque groupe rend ses cartes dès le départ. Brut = niveau du membre (id) + bruit déterministe.</summary>
    private static void Play(LeagueSeason s, int from, int to, Func<int, bool>? storm = null)
    {
        for (var day = from; day <= to; day++)
        {
            if (s.OnNewDay(day, storm?.Invoke(day) ?? false, day * 1000L) is { } round)
            {
                foreach (var pair in round.Pairs)
                {
                    s.GroupFinished(round.Number, [.. pair.Select(id => (id, Card(3 + (id % 7) + ((id + round.Number) % 3))))], null, (day * 1000L) + 1);
                }
            }
        }
    }

    [Fact]
    public void Finalists_DivisionOf3_AllQualified()
    {
        var s = new LeagueSeason(1, Members(10));
        var m = s.Members;
        s.Divisions = [[m[0], m[1], m[2]], [m[3], m[4]], [m[5]]];

        Assert.Equal([3, 2, 1], s.Finalists().Select(f => f.Count));
    }

    [Fact]
    public void Finalists_TieForFourth_BestCumulativeGrossThenId()
    {
        var s = new LeagueSeason(1, Members(10));
        var m = s.Members;
        s.Divisions = [[m[0], m[1], m[2], m[3], m[4], m[5], m[6]]];
        double[] points = [9, 8, 7, 6, 6, 6, 1];
        for (var i = 0; i < 7; i++)
        {
            m[i].SeasonPoints = points[i];
        }

        // m3, m4, m5 sont à égalité pour la 4e place : bruts cumulés 9, 7, 7 → m4 puis m5 devant m3 ; id départage m4 et m5.
        int[] gross = [1, 1, 1, 9, 7, 7, 1];
        for (var i = 0; i < 7; i++)
        {
            s.Rounds[0].Cards[i] = Card(gross[i]);
        }

        Assert.Equal([0, 1, 2, 4], s.Finalists()[0].Select(x => x.Id));
    }

    [Fact]
    public void Final_Takes4PerDivision_ChampionIsDivision1Winner_AndSlipsUnderHeavyRain()
    {
        var s = new LeagueSeason(1, Members(20));
        Play(s, 0, 53);
        Assert.True(s.Finished);
        Assert.Null(s.Champion);

        Assert.Null(s.OnNewDay(54, storm: true, 0));
        Assert.Null(s.OnNewDay(55, storm: true, 0));
        var final = s.OnNewDay(56, false, 56000)!;
        Assert.Equal((5, 56, LeagueFormat.StrokePlay), (final.Number, final.StartDay, final.Format));
        Assert.Equal(54, final.NominalDay);
        var ids = final.Pairs.SelectMany(p => p).ToList();
        Assert.Equal(8, ids.Count);
        foreach (var division in s.Divisions)
        {
            Assert.Equal(4, division.Count(m => ids.Contains(m.Id)));
            var qualified = division.Where(m => ids.Contains(m.Id)).Min(m => m.SeasonPoints);
            Assert.All(division.Where(m => !ids.Contains(m.Id)), m => Assert.True(m.SeasonPoints <= qualified));
        }

        foreach (var pair in final.Pairs)
        {
            s.GroupFinished(5, [.. pair.Select(id => (id, Card(3 + ((id * 5) % 4))))], null, 56500);
        }

        Assert.True(final.Closed);
        var winner = s.FinalRanking[0][0];
        Assert.Equal(winner, s.Champion!.Value.MemberId);
        Assert.Equal(final.Cards[winner].Gross, s.Champion.Value.Gross);
        Assert.Equal(s.FinalRanking[0].Select(id => final.Cards[id].Gross).Order(), s.FinalRanking[0].Select(id => final.Cards[id].Gross));
        Assert.Contains(s.Divisions[0], m => m.Id == winner);
        Assert.Equal([s.Champion.Value], s.Champions);
        Assert.Equal(new LeagueEvent(0, LeagueEventType.Champion, winner, 0), s.Events[0]);
    }

    [Fact]
    public void Adjustment_Moves2PerBoundary_NextDayAfterTheFinal_SizesUnchanged()
    {
        var s = new LeagueSeason(1, Members(30)); // 31 inscrits : 3 divisions de 11, 10, 10
        Assert.Equal(3, s.Divisions.Count);
        var sizes = s.Divisions.Select(d => d.Count).ToArray();
        Play(s, 0, 54);
        Assert.True(s.Final.Closed);
        Assert.False(s.Adjusted);
        var before = s.Divisions.Select(d => d.ToList()).ToList();
        Assert.Single(s.Champions);

        Play(s, 55, 55);

        Assert.True(s.Adjusted);
        Assert.Equal(sizes, s.Divisions.Select(d => d.Count));
        var all = s.Divisions.SelectMany(d => d).Select(m => m.Id).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        for (var d = 0; d < 3; d++)
        {
            var standing = before[d].OrderByDescending(m => m.SeasonPoints).ThenBy(m => Place(s, d, m)).ThenBy(m => m.Id).ToList();
            if (d > 0)
            {
                Assert.All(standing.Take(2), m => Assert.Contains(m, s.Divisions[d - 1]));
            }

            if (d < 2)
            {
                Assert.All(standing.TakeLast(2), m => Assert.Contains(m, s.Divisions[d + 1]));
            }

            if (d > 0 && d < 2)
            {
                Assert.All(standing.Skip(2).SkipLast(2), m => Assert.Contains(m, s.Divisions[d]));
            }
        }

        Assert.Equal(4, s.Events.Count(e => e.Type == LeagueEventType.Promoted));
        Assert.Equal(4, s.Events.Count(e => e.Type == LeagueEventType.Relegated));
        var promoted = s.Events.First(e => e.Type == LeagueEventType.Promoted);
        Assert.Contains(s.Divisions[promoted.Division], m => m.Id == promoted.MemberId);
    }

    private static int Place(LeagueSeason s, int division, Member m)
    {
        var k = s.FinalRanking[division].ToList().IndexOf(m.Id);
        return k < 0 ? int.MaxValue : k;
    }

    [Fact]
    public void Adjustment_SingleDivision_ChangesNothing()
    {
        var s = new LeagueSeason(1, Members(10));
        var before = s.Divisions[0].Select(m => m.Id).ToList();

        Play(s, 0, 57);

        Assert.True(s.Adjusted);
        Assert.Equal(before, s.Divisions[0].Select(m => m.Id));
        Assert.Equal([LeagueEventType.Champion], s.Events.Select(e => e.Type));
    }

    [Fact]
    public void NextSeason_April1st_DivisionsRecomputed_PointsReset_NewcomerAtTheBottom_CardsKept()
    {
        var newcomer = new Member(50, "Neuf", 3, 10);
        var s = new LeagueSeason(1, [.. Members(9), newcomer]); // 10 inscrits : une division
        Play(s, 0, 57);
        var cards = s.Members.Sum(m => m.Cards.Count);
        Assert.True(cards > 0);
        Assert.Single(s.Divisions);

        // Le nouveau, d'un meilleur niveau que tous, s'inscrit pendant l'hiver : il arrive tout en bas, avant le pro.
        newcomer.Opinion = 80;
        Play(s, 58, 119);
        Assert.Equal(0, s.Year);
        Play(s, 120, 120);

        Assert.Equal(1, s.Year);
        Assert.Equal(cards, s.Members.Sum(m => m.Cards.Count));
        Assert.All(s.Members, m => Assert.Equal(0, m.SeasonPoints));
        Assert.Equal([120 + 4, 130 + 4, 140 + 4, 150 + 4, 160 + 4], s.Rounds.Select(r => r.NominalDay));
        Assert.Null(s.Champion);
        Assert.Single(s.Champions);
        var order = s.Divisions.SelectMany(d => d).Select(m => m.Id).ToList();
        Assert.Equal(999, order[^1]);
        Assert.Equal(50, order[^2]);
        Assert.False(s.Adjusted);
        Assert.Empty(newcomer.Cards);
    }

    [Fact]
    public void NextSeason_DivisionCountFollowsEnrolled_ClubOrderKeptFromTheAdjustment()
    {
        var members = Members(30);
        var s = new LeagueSeason(1, members);
        Play(s, 0, 57);
        var adjusted = s.Divisions.SelectMany(d => d).Select(m => m.Id).Where(id => id != 999).ToList();
        var sizes = s.Divisions.Select(d => d.Count);
        Assert.Equal([11, 10, 10], sizes);

        Play(s, 58, 120);

        Assert.Equal([11, 10, 10], s.Divisions.Select(d => d.Count));
        Assert.Equal(adjusted, s.Divisions.SelectMany(d => d).Select(m => m.Id).Where(id => id != 999));
    }

    [Fact]
    public void NextSeason_WithFewerEnrolled_FallsBackToOneDivision()
    {
        var members = Members(30);
        var s = new LeagueSeason(1, members);
        Play(s, 0, 57);
        foreach (var m in members.Skip(10).Where(m => !m.IsPro))
        {
            m.Opinion = 0;
        }

        Play(s, 58, 120);

        Assert.Single(s.Divisions);
        Assert.Equal(11, s.Divisions[0].Count);
    }

    // --- deux saisons complètes dans le monde ---

    private static readonly Lazy<Task<TwoSeasons>> Shared = new(() => Task.Run(() => Simulate()));

    private sealed record TwoSeasons(ulong Checksum, int[] DivisionSizes, double PointsAfterApril1, int[] ChampionYears, int Year, bool Adjusted, int Cards, int Enrolled, int ChampionDivision, int[] LastOrder);

    private static TwoSeasons Simulate()
    {
        var world = new World(11, HoleLoader.Load(TerrainTests.HolePath(), 2), league: true);
        world.SetCourseRating(CourseRater.Rate(HoleLoader.Load(TerrainTests.HolePath(), 2), 11, 15));
        var league = world.League!;
        var points = double.NaN;
        var order = Array.Empty<int>();
        while (!(league.Year == 1 && league.Adjusted) && world.DayIndex < 300)
        {
            world.Step(Calendar.TicksPerDay);
            if (league.Year == 1 && double.IsNaN(points))
            {
                points = league.Members.Sum(m => m.SeasonPoints);
                order = [.. league.Divisions.SelectMany(d => d).Select(m => m.Id)];
            }
        }

        var enrolled = league.Divisions.SelectMany(d => d).ToList();
        return new TwoSeasons(
            world.Checksum(),
            [.. league.Divisions.Select(d => d.Count)],
            points,
            [.. league.Champions.Select(c => c.Year)],
            league.Year,
            league.Adjusted,
            league.Members.Sum(m => m.Cards.Count),
            enrolled.Count,
            league.Divisions[0].Any(m => m.Id == league.Champion!.Value.MemberId) ? 0 : 1,
            order);
    }

    [Fact]
    public async Task TwoSimulatedSeasons_AreDeterministic()
    {
        var a = await Shared.Value;
        var b = await Task.Run(Simulate);

        Assert.Equal(a.Checksum, b.Checksum);
        Assert.Equal(a.Cards, b.Cards);
    }

    [Fact]
    public async Task TwoSimulatedSeasons_DivisionsRecomputed_PointsReset_ProAtTheBottom_TwoChampions()
    {
        var r = await Shared.Value;

        Assert.Equal(1, r.Year);
        Assert.True(r.Adjusted);
        Assert.Equal([0, 1], r.ChampionYears);
        Assert.Equal(0.0, r.PointsAfterApril1);
        Assert.Equal(40, r.LastOrder[^1]);
        Assert.Equal(r.Enrolled / 8, r.DivisionSizes.Length);
        Assert.Equal(r.Enrolled, r.DivisionSizes.Sum());
        // 2 saisons de 6 cartes (5 journées et la finale pour les finalistes) : au moins 10 cartes par inscrit.
        Assert.True(r.Cards >= r.Enrolled * 10);
        Assert.Equal(0, r.ChampionDivision);
        output.WriteLine($"Divisions {string.Join('+', r.DivisionSizes)}, cartes {r.Cards}");
    }
}
