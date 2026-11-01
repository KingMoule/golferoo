using Golferoo.Sim;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Objectives;

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

    public static string BuildingName(BuildingKind kind) => kind switch
    {
        BuildingKind.SnackBar => "Snack-bar",
        BuildingKind.ProShop => "Pro shop",
        BuildingKind.MaintenanceShed => "Hangar d'entretien",
        BuildingKind.Bench => "Banc",
        _ => "Pavillon",
    };

    public static string BuildingTip(BuildingKind kind) => kind switch
    {
        BuildingKind.SnackBar => "Les golfeurs y font une pause au virage : une visite rapporte un peu et remonte l'humeur.",
        BuildingKind.ProShop => "Les golfeurs y passent avant leur départ : une visite rapporte un peu et affine leur jeu.",
        BuildingKind.MaintenanceShed => "Réduit l'entretien mensuel du parcours et abrite les tondeuses.",
        BuildingKind.Bench => "Un petit coin de repos : il embellit les environs.",
        _ => "",
    };

    /// <summary>Pourquoi la sim refuse une pose.</summary>
    public static string Refusal(BuildingPlacement result) => result switch
    {
        BuildingPlacement.Placed => "",
        BuildingPlacement.Locked => "Pas encore débloqué : ouvrez plus de trous.",
        BuildingPlacement.OutOfMap => "Hors de la carte.",
        BuildingPlacement.BadSurface => "Il faut du rough ou du sous-bois, et une porte praticable.",
        BuildingPlacement.Occupied => "Un autre bâtiment est déjà là.",
        BuildingPlacement.Golfer => "Un golfeur ou une balle est dans l'emprise.",
        BuildingPlacement.NoPath => "La porte n'aurait plus de chemin vers le départ du trou 1.",
        BuildingPlacement.Funds => "Solde insuffisant.",
        _ => "Impossible de poser ici.",
    };

    /// <summary>La récompense d'un objectif : l'argent de la difficulté, et le bon de pose s'il y en a un (« +5 000 $ et un bon : Snack-bar »).</summary>
    public static string Reward(ObjectiveDef def, long cents)
    {
        var money = cents > 0 ? UiMoney.Format(cents, signed: true) : string.Empty;
        var voucher = def.Voucher is { } kind ? $"un bon de pose : {BuildingName(kind).ToLowerInvariant()}" : string.Empty;
        return money.Length > 0 && voucher.Length > 0 ? $"{money} et {voucher}" : money + voucher;
    }

    /// <summary>« Printemps » pour « printemps ».</summary>
    public static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
