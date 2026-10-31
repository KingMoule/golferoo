namespace Golferoo.Sim.Golfers;

/// <summary>
/// Les événements qui changent l'humeur d'un golfeur (C16). L'identifiant stable est le nom, en camelCase dans <c>data/golfers/mood.json</c> ;
/// les valeurs entrent dans la sauvegarde et la somme de contrôle (indices des compteurs) : ne pas réordonner.
/// </summary>
public enum MoodEvent : byte
{
    HardPuttHoled = 0,
    EasyPuttMissed = 1,

    /// <summary>Coup décevant : la balle n'a presque pas rapproché du trou, ou l'en a éloignée.</summary>
    PoorShot = 2,
    Water = 3,
    Bunker = 4,
    TreeHit = 5,

    /// <summary>Le planificateur n'a trouvé aucun endroit sûr où viser : même son meilleur point de chute est un obstacle.</summary>
    NoSafeAim = 6,
    TeeWait = 7,
    SlowPlay = 8,

    /// <summary>La balle d'un autre groupe est tombée tout près.</summary>
    NearMiss = 9,

    /// <summary>Vue panoramique : la beauté (<see cref="Terrain.BeautyMap"/>) du départ.</summary>
    Panorama = 10,
    WornTurf = 11,
    RemarkableTree = 12,
    BeautifulHole = 13,
    LongWalk = 14,

    /// <summary>Le pro joue avec le groupe (choix 5) : déclaré au lot 1, levé par la file au lot 2.</summary>
    ProPlaysWithUs = 15,

    /// <summary>Le golfeur est passé au snack-bar ou au pro shop (C17) : levé à la porte du bâtiment.</summary>
    ServiceVisited = 16,
}

/// <summary>Un événement vu du dehors : un golfeur a réagi (levé quand l'humeur change, pas pour une contrariété mineure isolée).</summary>
/// <param name="Cell">Cellule du terrain où cela s'est passé.</param>
/// <param name="Delta">Changement réel de l'humeur : la valeur de l'événement, écrêtée pour que l'humeur reste dans −10…+10 (donc 0 au plafond).</param>
public readonly record struct MoodChange(Golfer Golfer, MoodEvent Event, int Hole, (int X, int Z) Cell, int Delta);

/// <summary>Un golfeur abandonne la partie. <paramref name="Reason"/> : l'événement qui a le plus pesé sur son humeur, ou nul si elle a seulement baissé trou après trou.</summary>
public readonly record struct QuitInfo(Golfer Golfer, MoodEvent? Reason, int Hole);

/// <summary>Un événement de <c>mood.json</c> : valeur, mineur (compte à sa répétition) et pensée en français.</summary>
public sealed record MoodEventDef(MoodEvent Event, int Value, bool Minor, string Thought);
