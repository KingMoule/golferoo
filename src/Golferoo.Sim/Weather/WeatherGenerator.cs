using Golferoo.Sim.Time;

namespace Golferoo.Sim.Weather;

/// <summary>
/// Météo, fonction pure de la graine, du climat et du numéro du jour : rien n'est stocké, et retoucher le terrain ne change pas
/// le temps de demain. Les jours vont par blocs de <see cref="SpellDays"/> : le ciel d'un jour est celui de son bloc deux fois sur trois,
/// son propre tirage sinon (la chance de chaque ciel reste celle du climat, et la pluie vient par épisodes). Direction et force du vent
/// sont interpolées entre les blocs.
/// </summary>
public static class WeatherGenerator
{
    /// <summary>Longueur d'un épisode, en jours.</summary>
    public const int SpellDays = 3;

    /// <summary>Une forte pluie souffle plus fort.</summary>
    private const double HeavyRainWind = 1.5;

    /// <summary>Chance qu'une prévision se trompe d'un cran.</summary>
    public const double ForecastError = 0.2;

    private enum Stream : ulong
    {
        SpellSky = 1,
        DaySky = 2,
        UseDay = 3,
        WindX = 4,
        WindZ = 8,
        WindSpeed = 5,
        Forecast = 6,
        ForecastSide = 7,
    }

    /// <summary>Temps du jour <paramref name="day"/> (jours écoulés depuis le début de la partie, décalage de debug compris).</summary>
    public static WeatherDay For(ulong seed, Climate climate, int day)
    {
        var season = (int)Calendar.At((long)day * Calendar.TicksPerDay, 0).Season;
        var spell = day / SpellDays;
        var u = Unit(seed, Stream.UseDay, day) < 2.0 / 3.0 ? Unit(seed, Stream.SpellSky, spell) : Unit(seed, Stream.DaySky, day);
        var sky = u < climate.HeavyRainChance[season] ? Sky.HeavyRain
            : u < climate.RainChance[season] ? Sky.Rain
            : u < climate.RainChance[season] + climate.CloudChance[season] ? Sky.Clouds
            : Sky.Sun;

        // Direction : vecteur tiré par bloc, interpolé puis normalisé (ni trigonométrie ni log, pour rester déterministe partout).
        var f = (day % SpellDays) / (double)SpellDays;
        var dx = Lerp(Signed(seed, Stream.WindX, spell), Signed(seed, Stream.WindX, spell + 1), f);
        var dz = Lerp(Signed(seed, Stream.WindZ, spell), Signed(seed, Stream.WindZ, spell + 1), f);
        var norm = Math.Sqrt((dx * dx) + (dz * dz));
        (dx, dz) = norm < 1e-6 ? (1.0, 0.0) : (dx / norm, dz / norm);
        var s = Lerp(Unit(seed, Stream.WindSpeed, spell), Unit(seed, Stream.WindSpeed, spell + 1), f);
        var wind = Math.Max(climate.WindMean[season] + (climate.WindSpread[season] * ((2.0 * s) - 1.0)), 0.0);
        return new WeatherDay(sky, dx, dz, sky == Sky.HeavyRain ? wind * HeavyRainWind : wind);
    }

    private static double Lerp(double a, double b, double f) => a + ((b - a) * f);

    private static double Signed(ulong seed, Stream stream, int index) => (2.0 * Unit(seed, stream, index)) - 1.0;

    /// <summary>Ciel annoncé pour le jour <paramref name="day"/> : le vrai, ou un cran à côté une fois sur cinq.</summary>
    public static Sky Forecast(ulong seed, Climate climate, int day)
    {
        var sky = For(seed, climate, day).Sky;
        if (Unit(seed, Stream.Forecast, day) >= ForecastError)
        {
            return sky;
        }

        var up = sky == Sky.Sun || (sky != Sky.HeavyRain && Unit(seed, Stream.ForecastSide, day) < 0.5);
        return up ? sky + 1 : sky - 1;
    }

    /// <summary>Uniforme dans [0, 1) tirée d'un hachage (SplitMix64) de la graine, du flux et de l'indice.</summary>
    private static double Unit(ulong seed, Stream stream, int index)
    {
        unchecked
        {
            var z = seed ^ ((ulong)stream * 0xD1B54A32D192ED03UL) ^ ((ulong)(uint)index * 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }
    }
}
