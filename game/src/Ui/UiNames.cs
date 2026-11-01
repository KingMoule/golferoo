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

    /// <summary>Les textes de la bulle du pro pour la suite du tutoriel (C19), sans le « Étape n sur N » que la bulle ajoute ; vide pour les étapes de la construction.</summary>
    public static string TutorialClub(Golferoo.Sim.Tutorial.TutorialStep step) => step switch
    {
        Golferoo.Sim.Tutorial.TutorialStep.OpenHole => "le trou est valide, votre club ouvre ! Le pavillon est posé près du départ ; les premiers golfeurs ne vont pas tarder. Laissez le temps passer, regardez-les arriver.",
        Golferoo.Sim.Tutorial.TutorialStep.WatchPair => "une première paire est arrivée au pavillon. Regardez-la prendre le départ.",
        Golferoo.Sim.Tutorial.TutorialStep.FirstGreenFee => "la paire joue votre trou. Chaque golfeur paie son green fee à la fin du trou : patientez, vous pouvez accélérer le temps.",
        Golferoo.Sim.Tutorial.TutorialStep.ReadThought => "premier green fee encaissé ! Les golfeurs pensent à voix haute quand quelque chose leur plaît ou les contrarie. Survolez un golfeur ou cliquez sur lui pour lire sa pensée : c'est ce que vous corrigez sur le terrain.",
        Golferoo.Sim.Tutorial.TutorialStep.PlaceSnackBar => "le club vous offre un snack-bar. Cliquez sur [b]Bâtiments[/b], choisissez le [b]Snack-bar[/b], puis cliquez sur du rough près du parcours.",
        _ => string.Empty,
    };

    /// <summary>Le mot de fin de la leçon.</summary>
    public const string TutorialDone = "[b]Leçon terminée ![/b] Votre club tourne : les golfeurs viennent, paient et vous disent ce qu'ils pensent. À vous de jouer !";

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
