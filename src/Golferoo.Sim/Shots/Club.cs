namespace Golferoo.Sim.Shots;

/// <summary>
/// Un club : vitesse de balle et angle de lancement plausibles, portée totale visée sur fairway plat (repère de calibration)
/// et dispersion de l'exécution (écart type de la direction et de la vitesse).
/// </summary>
/// <param name="Name">Nom du club.</param>
/// <param name="Speed">Vitesse de balle au départ, en m/s, à pleine force.</param>
/// <param name="LaunchDegrees">Angle de lancement au-dessus du sol, en degrés (0 : la balle roule).</param>
/// <param name="TargetRange">Portée totale visée sur fairway plat, en mètres.</param>
/// <param name="DirectionSigmaDegrees">Écart type de la direction, en degrés.</param>
/// <param name="SpeedSigma">Écart type relatif de la vitesse (0,02 = 2 %).</param>
public sealed record Club(
    string Name,
    double Speed,
    double LaunchDegrees,
    double TargetRange,
    double DirectionSigmaDegrees,
    double SpeedSigma)
{
    /// <summary>Cosinus de l'angle de lancement (trigonométrie seulement ici, pour poser la direction de départ).</summary>
    public double LaunchCos { get; } = Math.Cos(LaunchDegrees * Math.PI / 180.0);

    /// <summary>Sinus de l'angle de lancement.</summary>
    public double LaunchSin { get; } = Math.Sin(LaunchDegrees * Math.PI / 180.0);

    /// <summary>Écart type de la direction, en radians.</summary>
    public double DirectionSigma { get; } = DirectionSigmaDegrees * Math.PI / 180.0;

    public bool IsPutter => LaunchDegrees <= 0.0;
}

/// <summary>Table des clubs. Valeurs plausibles, sans réglage fin : les vitesses sont calibrées sur la table de portées (tests).</summary>
public static class Clubs
{
    public static readonly Club Driver = new("Driver", 69.3, 12.0, 220.0, 4.5, 0.05);
    public static readonly Club Wood3 = new("Bois 3", 62.4, 13.5, 200.0, 4.2, 0.05);
    public static readonly Club Iron5 = new("Fer 5", 52.7, 17.0, 170.0, 3.8, 0.05);
    public static readonly Club Iron7 = new("Fer 7", 43.4, 21.0, 140.0, 3.6, 0.05);
    public static readonly Club Iron9 = new("Fer 9", 34.7, 27.0, 110.0, 4.0, 0.06);
    public static readonly Club Wedge = new("Wedge", 27.2, 35.0, 80.0, 4.0, 0.06);
    public static readonly Club Putter = new("Putter", 0.0, 0.0, 0.0, 0.25, 0.015);

    /// <summary>Clubs de jeu hors green, du plus long au plus court.</summary>
    public static IReadOnlyList<Club> Full { get; } = [Driver, Wood3, Iron5, Iron7, Iron9, Wedge];
}
