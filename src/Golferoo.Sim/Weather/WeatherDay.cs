using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Weather;

/// <summary>Ciel du jour, du plus sec au plus mouillé.</summary>
public enum Sky : byte
{
    Sun = 0,
    Clouds = 1,
    Rain = 2,
    HeavyRain = 3,
}

/// <summary>Temps d'un jour : ciel, direction unitaire vers laquelle souffle le vent (X, Z) et force de base (m/s).</summary>
public readonly record struct WeatherDay(Sky Sky, double WindDirX, double WindDirZ, double WindBase)
{
    /// <summary>Vent au moment <paramref name="dayFraction"/> de la journée (0 à l'ouverture, 1 à la fermeture) : il se lève l'après-midi.</summary>
    public Vec3 WindAt(double dayFraction)
    {
        var speed = WindBase * (0.7 + (0.6 * dayFraction));
        return new Vec3(WindDirX * speed, 0.0, WindDirZ * speed);
    }
}
