namespace Golferoo.Sim.Golfers;

/// <summary>
/// Niveau de jeu (<see cref="Golfer.Skill"/>) d'après le handicap : il multiplie les écarts types des clubs (<see cref="Shots.Clubs"/>)
/// et retire de la vitesse de balle au-delà de 1. Deux repères (C21), calés sur « Le neuf » pour des taux d'amateurs réels
/// (test <c>DispersionTests</c>) : scratch (handicap 0, niveau 1 : les écarts types du club tels quels, 65 % de fairways et de greens
/// en régulation) et bogey (handicap 20) ; un handicap 18 touche 45 % des fairways et 25 % des greens. Entre eux on interpole,
/// au-delà on prolonge la droite jusqu'à 54.
/// </summary>
public static class SkillLevels
{
    public const double Scratch = 1.0;
    public const double Bogey = 2.1;
    public const double MaxHandicap = 54.0;

    /// <summary>Handicap le plus bas accepté (+4, le pro du circuit de C25). Les golfeurs ordinaires restent à 0 ou plus par leur handicap de départ.</summary>
    public const double MinHandicap = -4.0;

    /// <summary>Probabilité d'un choix non optimal au handicap 0, et ce qui s'y ajoute au handicap 36.</summary>
    public const double ScratchMistake = 0.05;

    public const double MistakeAt36 = 0.15;

    public static double FromHandicap(double handicap) =>
        Scratch + ((Bogey - Scratch) * Math.Clamp(handicap, MinHandicap, MaxHandicap) / 20.0);

    /// <summary>Handicap d'un niveau (inverse de <see cref="FromHandicap"/>, borné à 0 et 54).</summary>
    public static double ToHandicap(double skill) =>
        Math.Clamp((skill - Scratch) * 20.0 / (Bogey - Scratch), 0.0, MaxHandicap);

    /// <summary>
    /// Probabilité qu'un golfeur du niveau <paramref name="skill"/> prenne un choix non optimal (C21) : 5 % + 15 % × h / 36, h son handicap.
    /// </summary>
    public static double MistakeChance(double skill) => ScratchMistake + (MistakeAt36 * ToHandicap(skill) / 36.0);
}
