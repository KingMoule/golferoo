namespace Golferoo.Sim.Weather;

/// <summary>
/// Climat d'un thème : chances de chaque ciel et vent, par saison (tableaux indexés par <see cref="Trees.Season"/>).
/// Les chances de pluie comprennent la forte pluie. Vent de base en m/s : moyenne ± écart.
/// </summary>
public sealed record Climate(double[] RainChance, double[] HeavyRainChance, double[] CloudChance, double[] WindMean, double[] WindSpread)
{
    /// <summary>Tempéré, le thème actuel : printemps et automne humides, été sec, hiver doux sans neige.</summary>
    public static readonly Climate Temperate = new(
        RainChance: [0.35, 0.20, 0.35, 0.30],
        HeavyRainChance: [0.08, 0.05, 0.10, 0.06],
        CloudChance: [0.30, 0.25, 0.30, 0.35],
        WindMean: [5.0, 4.0, 6.0, 6.0],
        WindSpread: [3.0, 3.0, 4.0, 4.0]);

    /// <summary>Toujours beau et sans vent : le comportement d'avant la météo, pour les tests.</summary>
    public static readonly Climate Calm = new([0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]);
}
