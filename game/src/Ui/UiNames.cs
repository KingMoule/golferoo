namespace Golferoo.Game.Ui;

/// <summary>Les noms affichés du calendrier et du ciel, en minuscules, dans l'ordre des énumérations de la sim : une seule table pour tout l'écran.</summary>
public static class UiNames
{
    public static readonly string[] Months = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];
    public static readonly string[] MonthsShort = ["jan", "fév", "mars", "avr", "mai", "juin", "juil", "août", "sept", "oct", "nov", "déc"];
    public static readonly string[] Seasons = ["printemps", "été", "automne", "hiver"];
    public static readonly string[] Levels = ["Visiteur", "Membre", "Argent", "Or", "Platine"];

    /// <summary>Ce qui a fait réagir un golfeur, dans l'ordre de <c>MoodEvent</c> : le rapport des réactions par trou.</summary>
    public static readonly string[] Reactions =
    [
        "putt difficile rentré", "putt facile raté", "coup décevant", "balle dans l'eau", "balle dans un bunker", "balle dans un arbre", "aucun endroit sûr où viser", "attente au départ",
        "jeu lent devant", "balle d'un autre groupe tombée tout près", "vue panoramique", "gazon usé", "arbre remarquable", "beau trou", "longue marche", "le pro joue avec le groupe",
        "visite d'un service du club",
    ];

    public static readonly string[] Skies = ["soleil", "nuages", "pluie", "forte pluie"];

    /// <summary>« Printemps » pour « printemps ».</summary>
    public static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
