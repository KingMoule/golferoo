namespace Golferoo.Sim.Golfers;

/// <summary>
/// Niveau de jeu (<see cref="Golfer.Skill"/>) d'après le handicap. Deux repères calibrés sur proto-par4 (test de calibrage) :
/// scratch (handicap 0) et bogey (handicap 20) ; entre eux on interpole, au-delà on prolonge la droite jusqu'à 54.
/// </summary>
public static class SkillLevels
{
    public const double Scratch = 1.35;
    public const double Bogey = 2.15;
    public const double MaxHandicap = 54.0;

    public static double FromHandicap(double handicap) =>
        Scratch + ((Bogey - Scratch) * Math.Clamp(handicap, 0.0, MaxHandicap) / 20.0);
}
