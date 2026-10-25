using Golferoo.Sim.Time;

namespace Golferoo.Sim.Trees;

/// <summary>
/// Feuillage affiché des feuillus adultes au fil de l'année (rendu seul, la balle suit <see cref="Season"/>) : couleur et part des feuilles,
/// interpolées entre des dates clés pour que les arbres changent peu à peu. Une position dans l'année se compte en mois depuis le 1er janvier
/// (8,5 = 6 septembre) ; chaque arbre la décale de quelques jours pour que la forêt ne change pas d'un bloc.
/// </summary>
public static class Foliage
{
    /// <summary>
    /// Couleur : 0 vert d'été, 1 roux d'automne, 2 brun terne d'hiver, 3 vert de nouveau (le printemps passe du brun au vert sans repasser
    /// par le roux). Le roux vient en septembre, le brun en novembre, le vert en mars.
    /// </summary>
    private static readonly (double Month, double Value)[] ColourKeys = [(2.0, 2), (3.0, 3), (3.0, 0), (8.0, 0), (9.0, 1), (10.3, 1), (11.0, 2)];

    /// <summary>Tour complet de la couleur : <see cref="Colour"/> va de 0 inclus à cette valeur exclue.</summary>
    public const double ColourCycle = 3;

    /// <summary>Part des feuilles : elles tombent d'octobre à fin novembre, jusqu'au quart gardé l'hiver, et repoussent de mars à fin avril.</summary>
    private static readonly (double Month, double Value)[] LeafKeys = [(2.3, 0.25), (4.0, 1), (9.0, 1), (11.0, 0.25)];

    /// <summary>Étapes de feuillage : toutes les feuilles, 75 %, 50 %, 25 %.</summary>
    public const int Stages = 4;

    public static double Position(Date date) => date.Month - 1 + ((date.Day - 1 + date.DayFraction) / Calendar.DaysPerMonth);

    public static double Colour(double position) => Interpolate(ColourKeys, position) % ColourCycle;

    public static double Leaves(double position) => Interpolate(LeafKeys, position);

    /// <summary>Étape de feuillage, de 0 (toutes les feuilles) à <see cref="Stages"/> - 1 (un quart).</summary>
    public static int Stage(double position) => (int)Math.Round((1 - Leaves(position)) * Stages);

    /// <summary>Vrai si couleur et feuilles ne changent pas entre <paramref name="position"/> ± <paramref name="spread"/> (été, hiver).</summary>
    public static bool Steady(double position, double spread) => Steady(ColourKeys, position, spread) && Steady(LeafKeys, position, spread);

    private static bool Steady((double Month, double Value)[] keys, double position, double spread)
    {
        var value = Interpolate(keys, position - spread);
        if (Interpolate(keys, position + spread) != value)
        {
            return false;
        }

        foreach (var key in keys)
        {
            for (var shift = -12.0; shift <= 12.0; shift += 12.0)
            {
                var m = key.Month + shift;
                if (m > position - spread && m < position + spread && key.Value != value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Interpolation linéaire entre les clés, qui bouclent sur l'année.</summary>
    private static double Interpolate((double Month, double Value)[] keys, double position)
    {
        var p = ((position % Calendar.MonthsPerYear) + Calendar.MonthsPerYear) % Calendar.MonthsPerYear;
        var previous = (Month: keys[^1].Month - Calendar.MonthsPerYear, keys[^1].Value);
        foreach (var key in keys)
        {
            if (p <= key.Month)
            {
                return Lerp(previous, key, p);
            }

            previous = key;
        }

        return Lerp(previous, (keys[0].Month + Calendar.MonthsPerYear, keys[0].Value), p);
    }

    private static double Lerp((double Month, double Value) a, (double Month, double Value) b, double p) =>
        a.Value + ((b.Value - a.Value) * (p - a.Month) / (b.Month - a.Month));
}
