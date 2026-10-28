using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C12 lot 5 : ajouter et retirer le dernier trou en jeu (commandes en file).</summary>
public class NewHoleTests
{
    private static (Point2 Tee, Point2 Pin) FourthHole(TerrainGrid t) => (t.Course[2].Tee, t.Course[2].Hole);

    private static void PlayOut(World world)
    {
        for (var i = 0; i < 120_000 && !(world.Groups.Count > 0 && world.Groups.All(g => g.Finished)); i++)
        {
            world.Step();
        }

        Assert.All(world.Groups, g => Assert.True(g.Finished, "la partie n'est pas finie"));
    }

    private static World WithFourthHole(ulong seed)
    {
        var world = new World(seed, CourseTests.ThreeHoles());
        var (tee, pin) = FourthHole(world.Terrain!);
        Assert.True(world.AddHole(tee, pin));
        world.Step();
        return world;
    }

    [Theory]
    [InlineData(100.0, 3)]
    [InlineData(229.9, 3)]
    [InlineData(230.1, 4)]
    [InlineData(429.9, 4)]
    [InlineData(430.1, 5)]
    [InlineData(600.0, 5)]
    public void DeducedPar_FollowsTheThresholds(double metres, int par) =>
        Assert.Equal(par, TerrainGrid.DeducedPar(new Point2(0, 0), new Point2(metres, 0)));

    [Fact]
    public void AddHole_AppliedAtNextTick_WithNextNumberAndDeducedPar()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        var (tee, pin) = FourthHole(world.Terrain!);
        Assert.True(world.AddHole(tee, pin));
        Assert.Equal(3, world.Terrain!.Course.Count);
        world.Step();

        var hole = world.Terrain.Course[4];
        Assert.Equal(4, world.Terrain.Course.Count);
        Assert.Equal(4, hole.Number);
        Assert.Equal(tee, hole.Tee);
        Assert.Equal(pin, hole.Hole);
        Assert.Equal(TerrainGrid.DeducedPar(tee, pin), hole.Par);
        Assert.True(world.IsHoleOpen(4));
    }

    [Fact]
    public void FourthHole_IsPlayedByAGroup_ThenRemoved()
    {
        var world = WithFourthHole(1);
        world.SpawnGroup();
        PlayOut(world);
        Assert.All(world.Groups[0].Golfers, g =>
        {
            Assert.Equal(4, g.Card!.HoleCount);
            Assert.True(g.Card.IsComplete);
        });

        Assert.True(world.RemoveLastHole());
        world.Step();
        Assert.Equal(3, world.Terrain!.Course.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.IsHoleOpen(4));
        world.Step(500);
    }

    [Fact]
    public void Refused_WhatLeavesNoCourse_OrBreaksAPlayingGroup_OrMissesACell()
    {
        var single = new World(1, HoleLoader.Load(TerrainTests.HolePath(), 2));
        Assert.False(single.RemoveLastHole());

        var world = new World(1, CourseTests.ThreeHoles());
        var (tee, pin) = FourthHole(world.Terrain!);
        Assert.False(world.AddHole(pin, pin));
        Assert.False(world.AddHole(tee, tee));
        Assert.False(world.AddHole(world.Terrain!.Course[1].Tee, pin));
        Assert.False(world.AddHole(new Point2(double.NaN, 0), pin));
        world.SpawnGroup();
        Assert.False(world.RemoveLastHole()); // le groupe a le trou 3 dans sa carte
        PlayOut(world);
        Assert.True(world.RemoveLastHole());
        Assert.True(world.RemoveLastHole());
        Assert.False(world.RemoveLastHole()); // il resterait un seul trou, mais deux retraits sont déjà en file : 3 - 2 = 1
    }

    [Fact]
    public void GroupInPlay_KeepsItsThreeHoleCard_AndLaterGroupsPlayFour()
    {
        var world = new World(5, CourseTests.ThreeHoles());
        var first = world.SpawnGroup();
        world.Step(200);
        var (tee, pin) = FourthHole(world.Terrain!);
        Assert.True(world.AddHole(tee, pin));
        world.Step();
        Assert.Equal(4, world.Terrain!.Course.Count);
        var second = world.SpawnGroup();
        PlayOut(world);

        Assert.All(first.Golfers, g =>
        {
            Assert.Equal(3, g.Card!.HoleCount);
            Assert.True(g.Card.IsComplete);
        });
        Assert.All(second.Golfers, g =>
        {
            Assert.Equal(4, g.Card!.HoleCount);
            Assert.True(g.Card.IsComplete);
        });
        Assert.Equal(3, first.Hole);
    }

    [Fact]
    public void RemoveLastHole_RefusedWhileAnUnfinishedGroupHasItInItsCard()
    {
        var world = WithFourthHole(2);
        world.SpawnGroup();
        Assert.False(world.RemoveLastHole());
        PlayOut(world);
        Assert.True(world.RemoveLastHole());
    }

    [Fact]
    public void AddAndRemove_InvalidateTheCourseRating_UntilItIsRecomputed()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        world.SetCourseRating(new Golferoo.Sim.Rating.CourseRating(3, 12, 80.0, 100.0));
        world.Step();
        Assert.Equal(3, world.CourseRating!.Holes);
        var (tee, pin) = FourthHole(world.Terrain!);
        Assert.True(world.AddHole(tee, pin));
        world.Step();
        Assert.Null(world.CourseRating);

        world.SetCourseRating(Golferoo.Sim.Rating.CourseRater.Rate(world.Terrain!, 1));
        world.Step();
        Assert.Equal(4, world.CourseRating!.Holes);
        Assert.True(world.RemoveLastHole());
        world.Step();
        Assert.Null(world.CourseRating);
    }

    [Fact]
    public void RatingForAnotherHoleCount_IsIgnored()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        world.SetCourseRating(new Golferoo.Sim.Rating.CourseRating(4, 16, 80.0, 100.0));
        world.Step();
        Assert.Null(world.CourseRating);
        world.SetCourseRating(new Golferoo.Sim.Rating.CourseRating(3, 12, 80.0, 100.0));
        world.Step();
        Assert.Equal(3, world.CourseRating!.Holes);
    }

    [Fact]
    public void RefusedAtApplication_IsCounted()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        Assert.Equal(0, world.HoleCommandsRefused);
        Assert.True(world.RemoveLastHole());
        world.SpawnGroup(); // un groupe prend le trou 3 dans sa carte avant l'application
        world.Step();
        Assert.Equal(3, world.Terrain!.Course.Count);
        Assert.Equal(1, world.HoleCommandsRefused);
    }

    [Fact]
    public void LeagueCards_FinishedWithoutAValidRating_AreHeldThenReturnedWithTheirDifferential()
    {
        var seed = Enumerable.Range(1, 500).First(x => new Golferoo.Sim.League.LeagueSeason((ulong)x, Golferoo.Sim.League.LeagueSeason.GenerateMembers((ulong)x)).Divisions.Sum(d => d.Count) >= 23);
        var world = new World((ulong)seed, HoleLoader.Load(TerrainTests.HolePath(), 2), league: true) { Climate = Golferoo.Sim.Weather.Climate.Calm };
        world.SetCourseRating(Golferoo.Sim.Rating.CourseRater.Rate(world.Terrain!, 3, 15));
        while (!world.Groups.Any(g => g.LeagueRound >= 0) && world.DayIndex < 20)
        {
            world.Step(Golferoo.Sim.Time.Calendar.TicksPerDay / 8);
        }

        var hole = world.Terrain!.Course[1];
        Assert.True(world.AddHole(hole.Tee, hole.Hole));
        while (!world.Groups.All(g => g.Finished) && world.DayIndex < 40)
        {
            world.Step(Golferoo.Sim.Time.Calendar.TicksPerDay / 8);
        }

        Assert.Null(world.CourseRating);
        Assert.All(world.League!.Members, m => Assert.Empty(m.Cards));
        Assert.False(world.League.Rounds[0].Closed);

        using var stream = new MemoryStream();
        world.Save(stream);
        var loaded = World.Load(new MemoryStream(stream.ToArray()));
        Assert.Equal(world.Checksum(), loaded.Checksum());

        foreach (var w in new[] { world, loaded })
        {
            w.SetCourseRating(Golferoo.Sim.Rating.CourseRater.Rate(w.Terrain!, 3, 15));
            w.Step();
            Assert.True(w.League!.Rounds[0].Closed);
            Assert.Contains(w.League.Members, m => m.Cards.Count > 0);
            Assert.All(w.League.Members.SelectMany(m => m.Cards), c => Assert.NotNull(c.Differential));
        }

        Assert.Equal(world.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void AddHole_StopsAtEighteenHoles()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        var (tee, pin) = FourthHole(world.Terrain!);
        for (var i = 0; i < 15; i++)
        {
            Assert.True(world.AddHole(tee, pin));
        }

        Assert.False(world.AddHole(tee, pin));
        world.Step();
        Assert.Equal(Course.MaxHoles, world.Terrain!.Course.Count);
    }

    [Fact]
    public void NewHoleCheck_IsLocal_SoOtherHolesSurfacesDoNotCount()
    {
        var world = new World(1, CourseTests.ThreeHoles());
        var t = world.Terrain!;
        var pin = t.Course[1].Hole;
        var near = HoleCheck.AreaNear(t, Surface.Green, pin, pin, HoleCheck.GreenReach);
        Assert.InRange(near, HoleCheck.MinGreenArea, HoleCheck.Area(t, Surface.Green) - 1);
        Assert.Empty(HoleCheck.Check(t, t.Course[1], true));
        Assert.Contains(HoleIssue.NoPath, world.NewHoleIssues(t.Course[1].Tee, t.Course[2].Hole));
    }

    [Fact]
    public void SameSeed_AddThenPlay_HasTheSameChecksum()
    {
        ulong Run()
        {
            var world = WithFourthHole(7);
            world.SpawnGroup();
            PlayOut(world);
            return world.Checksum();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void QueuedCommand_ChangesTheChecksum()
    {
        var a = new World(1, CourseTests.ThreeHoles());
        var b = new World(1, CourseTests.ThreeHoles());
        var (tee, pin) = FourthHole(a.Terrain!);
        a.AddHole(tee, pin);
        Assert.NotEqual(b.Checksum(), a.Checksum());
    }
}
