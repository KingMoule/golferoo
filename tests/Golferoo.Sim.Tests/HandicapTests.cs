using Golferoo.Sim.Rating;

namespace Golferoo.Sim.Tests;

public class HandicapTests
{
    [Fact]
    public void Index_IsNullBeforeThreeGames()
    {
        Assert.Null(Handicap.Index([], 18));
        Assert.Null(Handicap.Index([10.0, 12.0], 1));
        Assert.NotNull(Handicap.Index([10.0, 12.0, 14.0], 1));
    }

    [Theory]
    [InlineData(3, 8.0)]   // plus bas (10) - 2
    [InlineData(4, 9.0)]   // plus bas - 1
    [InlineData(5, 10.0)]  // plus bas
    [InlineData(6, 10.0)]  // moyenne des 2 plus bas (11) - 1
    [InlineData(7, 11.0)]  // moyenne des 2 plus bas
    [InlineData(9, 12.0)]  // 3 plus bas
    [InlineData(12, 13.0)] // 4 plus bas
    [InlineData(15, 14.0)] // 5 plus bas
    [InlineData(17, 15.0)] // 6 plus bas
    [InlineData(19, 16.0)] // 7 plus bas
    [InlineData(20, 17.0)] // 8 plus bas (10 a 24)
    [InlineData(25, 17.0)] // seuls les 20 derniers comptent
    public void Index_FollowsWhsScale_OnKnownLists(int games, double expected)
    {
        // Écarts 10, 12, ... rangés du plus ancien (le plus haut) au plus récent (10) ; seuls les 20 derniers comptent.
        var diffs = Enumerable.Range(0, games).Select(i => 10.0 + (2.0 * i)).Reverse().ToArray();

        Assert.Equal(expected, Handicap.Index(diffs, 18));
    }

    [Fact]
    public void Index_IsClampedAndRoundedToTenth()
    {
        Assert.Equal(0.0, Handicap.Index([-10.0, -9.0, -8.0], 18));
        Assert.Equal(54.0, Handicap.Index([90.0, 91.0, 92.0, 93.0, 94.0], 18));
        Assert.Equal(10.3, Handicap.Index([10.34, 20.0, 30.0, 40.0, 50.0], 18));
    }

    [Fact]
    public void Index_OnShortCourse_IsTheMeanOfTheLast20()
    {
        Assert.Equal(12.0, Handicap.Index([10.0, 12.0, 14.0], 1));
        var diffs = Enumerable.Repeat(100.0, 5).Concat(Enumerable.Repeat(10.0, 20)).ToArray();
        Assert.Equal(10.0, Handicap.Index(diffs, 1));
        Assert.Equal(0.0, Handicap.Index([-5.0, -6.0, -7.0], 1));
    }

    [Fact]
    public void Differential_ScalesBySlopeAndHoles()
    {
        var rating = new CourseRating(1, 4, 4.0, 5.0);   // slope 5,381 x 18 = 97
        var diff = Handicap.Differential(6, rating, 1);

        Assert.Equal(97, rating.Slope);
        Assert.Equal(2.0 * 113 / 97 * 18, diff, 9);
    }

    /// <summary>
    /// C21 lot 2, critère 8 : un trou fermé (non joué, 0 coup sur la carte) ne compte ni dans le brut ni dans la note ; l'écart ne dépend
    /// pas de sa note. Avant : brut des trous joués contre la note de tous les trous, soit un écart faussé vers le bas.
    /// </summary>
    [Theory]
    [InlineData(3.0)]
    [InlineData(6.0)]
    public void Differential_IgnoresAClosedHoleTheMemberDidNotPlay(double closedHoleScratch)
    {
        var rating = new CourseRating(3, 12, 8.0 + closedHoleScratch, 16.0 + closedHoleScratch, [3.5, closedHoleScratch, 4.5]);
        var card = new Scoring.ScoreCard([4, 4, 4]);
        card.Record(0, 5);
        card.Record(2, 6); // trou 2 fermé : sauté

        var expected = (11 - 8.0) * CourseRating.Reference / rating.Slope * 18.0 / 2;
        Assert.Equal(expected, Handicap.Differential(card, rating), 9);
    }

    [Fact]
    public void PlayingHandicap_AddsRatingMinusParAndScalesToHoles()
    {
        var rating = new CourseRating(1, 4, 4.2, 5.2);   // scratch18 75,6 ; par18 72 ; slope 97
        var for18 = (10.0 * 97 / 113) + (75.6 - 72.0);

        Assert.Equal((int)Math.Round(for18 / 18), Handicap.PlayingHandicap(10.0, rating, 1));
        Assert.Equal((int)Math.Round(for18), Handicap.PlayingHandicap(10.0, rating, 18));
    }
}
