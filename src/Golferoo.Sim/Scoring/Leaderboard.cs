namespace Golferoo.Sim.Scoring;

/// <summary>Une ligne de classement : rang (1, 2, 2, 4), ex aequo ou non.</summary>
public readonly record struct Ranked<T>(int Rank, bool Tied, T Item)
{
    /// <summary>« 3 » ou « T3 » pour un ex aequo.</summary>
    public string Label => Tied ? $"T{Rank}" : Rank.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public static class Leaderboard
{
    /// <summary>Classe par une clé (le plus petit d'abord, ou le plus grand si <paramref name="higherIsBetter"/>) ; tri stable, ex aequo au même rang.</summary>
    public static List<Ranked<T>> Rank<T>(IEnumerable<T> items, Func<T, double> key, bool higherIsBetter = false)
    {
        var sorted = (higherIsBetter ? items.OrderByDescending(key) : items.OrderBy(key)).ToList();
        var ranked = new List<Ranked<T>>(sorted.Count);
        for (var i = 0; i < sorted.Count; i++)
        {
            var k = key(sorted[i]);
            var first = i;
            while (first > 0 && key(sorted[first - 1]) == k)
            {
                first--;
            }

            var tied = first < i || (i + 1 < sorted.Count && key(sorted[i + 1]) == k);
            ranked.Add(new Ranked<T>(first + 1, tied, sorted[i]));
        }

        return ranked;
    }
}
