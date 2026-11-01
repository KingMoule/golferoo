using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Time;

/// <summary>Date de jeu. <paramref name="Month"/> va de 1 à 12, <paramref name="Day"/> de 1 à <see cref="Calendar.DaysPerMonth"/>.</summary>
/// <param name="DayFraction">Moment de la journée : 0 à l'ouverture, 1 à la fermeture.</param>
public readonly record struct Date(int Day, int Month, int Year, Season Season, double DayFraction);

/// <summary>Calendrier déduit du tick et d'un décalage de jours (commande de debug) : aucun état à recalculer.</summary>
public static class Calendar
{
    /// <summary>Une journée de 9 minutes réelles en vitesse x1 (C21 : assez pour que la ligue joue tous les trous du parcours).</summary>
    public const int TicksPerDay = 9 * 60 * World.TicksPerSecond;

    /// <summary>Heures de jeu : la journée (<see cref="Date.DayFraction"/> de 0 à 1) va de l'ouverture à la fermeture.</summary>
    public const double OpeningHour = 6.0;

    public const double ClosingHour = 18.0;

    public const int DaysPerMonth = 10;
    public const int MonthsPerYear = 12;

    /// <summary>Mois de départ (1er avril de l'an 1).</summary>
    public const int StartMonth = 4;

    /// <summary>Mois d'âge d'arbre gagnés par mois de jeu.</summary>
    public const int TreeMonthsPerMonth = 4;

    /// <summary>Rang du mois (0 = le mois de départ de l'an 1) ; il change quand le mois change.</summary>
    public static long MonthIndex(long tick, int dayOffset) => ((tick / TicksPerDay) + dayOffset) / DaysPerMonth;

    /// <summary>Année de jeu (à partir de 1) et mois (1 à 12) du mois de rang <paramref name="monthIndex"/>.</summary>
    public static (int Year, int Month) YearAndMonth(long monthIndex)
    {
        var months = monthIndex + StartMonth - 1;
        return ((int)(months / MonthsPerYear) + 1, (int)(months % MonthsPerYear) + 1);
    }

    public static Date At(long tick, int dayOffset)
    {
        var day = (tick / TicksPerDay) + dayOffset;
        var months = (day / DaysPerMonth) + StartMonth - 1;
        var month = (int)(months % MonthsPerYear) + 1;
        return new Date(
            (int)(day % DaysPerMonth) + 1,
            month,
            (int)(months / MonthsPerYear) + 1,
            SeasonOf(month),
            (double)(tick % TicksPerDay) / (TicksPerDay - 1));
    }

    /// <summary>Mars à mai printemps, juin à août été, septembre à novembre automne, décembre à février hiver.</summary>
    public static Season SeasonOf(int month) => month switch
    {
        >= 3 and <= 5 => Season.Spring,
        >= 6 and <= 8 => Season.Summer,
        >= 9 and <= 11 => Season.Autumn,
        _ => Season.Winter,
    };
}
