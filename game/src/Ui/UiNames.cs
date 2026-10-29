namespace Golferoo.Game.Ui;

/// <summary>Les noms affichés du calendrier et du ciel, en minuscules, dans l'ordre des énumérations de la sim : une seule table pour tout l'écran.</summary>
public static class UiNames
{
    public static readonly string[] Months = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];
    public static readonly string[] Seasons = ["printemps", "été", "automne", "hiver"];
    public static readonly string[] Skies = ["soleil", "nuages", "pluie", "forte pluie"];

    /// <summary>« Printemps » pour « printemps ».</summary>
    public static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
