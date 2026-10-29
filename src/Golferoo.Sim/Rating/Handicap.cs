using Golferoo.Sim.Scoring;

namespace Golferoo.Sim.Rating;

/// <summary>Handicap WHS simplifié : fonctions pures sur des écarts de cartes de score.</summary>
public static class Handicap
{
    public const int MinGames = 3;
    public const int RecentGames = 20;
    public const double MaxIndex = 54.0;

    /// <summary>Écart d'une carte : (brut − note scratch des trous joués) × 113 / slope, ramené à 18 trous.</summary>
    public static double Differential(int gross, CourseRating rating, int holesPlayed)
    {
        ArgumentNullException.ThrowIfNull(rating);
        ArgumentOutOfRangeException.ThrowIfLessThan(holesPlayed, 1);
        return (gross - (rating.ScratchPerHole * holesPlayed)) * CourseRating.Reference / rating.Slope * 18.0 / holesPlayed;
    }

    /// <summary>
    /// Écart d'une carte (C21) : brut des trous joués moins la note scratch de ces mêmes trous (<see cref="CourseRating.ScratchOf"/>),
    /// × 113 / slope, ramené à 18 trous. Un trou fermé, donc non joué, ne compte ni dans le brut ni dans la note.
    /// </summary>
    public static double Differential(ScoreCard card, CourseRating rating)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(rating);
        var (played, scratch) = (0, 0.0);
        for (var h = 0; h < card.HoleCount; h++)
        {
            if (card.Strokes(h) > 0)
            {
                played++;
                scratch += rating.ScratchOf(h);
            }
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(played, 1);
        return (card.Gross - scratch) * CourseRating.Reference / rating.Slope * 18.0 / played;
    }

    /// <summary>Sous ce nombre de trous, les écarts sont trop bruités pour le barème WHS : l'index est la moyenne de tous.</summary>
    public const int WhsMinHoles = 9;

    /// <summary>
    /// Index d'après les écarts, du plus ancien au plus récent (les 20 derniers comptent) ; nul avant 3 parties.
    /// <paramref name="courseHoles"/> : trous du parcours. À moins de 9 trous, moyenne de tous les écarts ; sinon barème WHS.
    /// </summary>
    public static double? Index(IReadOnlyList<double> differentials, int courseHoles)
    {
        ArgumentNullException.ThrowIfNull(differentials);
        var n = Math.Min(differentials.Count, RecentGames);
        if (n < MinGames)
        {
            return null;
        }

        if (courseHoles < WhsMinHoles)
        {
            var mean = differentials.Skip(differentials.Count - n).Average();
            return Math.Clamp(Math.Round(mean, 1, MidpointRounding.AwayFromZero), 0.0, MaxIndex);
        }

        var sorted = differentials.Skip(differentials.Count - n).Order().ToArray();
        var (count, adjustment) = n switch
        {
            3 => (1, -2.0),
            4 => (1, -1.0),
            5 => (1, 0.0),
            6 => (2, -1.0),
            <= 8 => (2, 0.0),
            <= 11 => (3, 0.0),
            <= 14 => (4, 0.0),
            <= 16 => (5, 0.0),
            <= 18 => (6, 0.0),
            19 => (7, 0.0),
            _ => (8, 0.0),
        };
        var index = sorted.Take(count).Average() + adjustment;
        return Math.Clamp(Math.Round(index, 1, MidpointRounding.AwayFromZero), 0.0, MaxIndex);
    }

    /// <summary>Handicap de jeu : index × slope / 113 + (note scratch − par) à 18 trous, ramené aux trous joués, arrondi à l'entier.</summary>
    public static int PlayingHandicap(double index, CourseRating rating, int holesPlayed)
    {
        ArgumentNullException.ThrowIfNull(rating);
        ArgumentOutOfRangeException.ThrowIfLessThan(holesPlayed, 1);
        var for18 = (index * rating.Slope / CourseRating.Reference) + (rating.Scratch18 - rating.Par18);
        return (int)Math.Round(for18 * holesPlayed / 18.0, MidpointRounding.AwayFromZero);
    }
}
