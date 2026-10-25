using Godot;
using Golferoo.Sim.Trees;

namespace Golferoo.Game;

/// <summary>Course du soleil : se lève à l'est (+X), passe au sud (+Z), se couche à l'ouest ; jamais sous l'horizon, moins haut en hiver.</summary>
public static class SunPath
{
    private const float HorizonDegrees = 8f;

    /// <summary>Orientation de la lumière (elle éclaire vers l'opposé du soleil) pour un moment de la journée (0 à 1).</summary>
    public static Basis At(double dayFraction, Season season)
    {
        var peak = season switch
        {
            Season.Summer => 65f,
            Season.Winter => 30f,
            _ => 50f,
        };
        var f = (float)Math.Clamp(dayFraction, 0.0, 1.0);
        var azimuth = Mathf.Pi * f;
        var elevation = Mathf.DegToRad(Mathf.Lerp(HorizonDegrees, peak, Mathf.Sin(Mathf.Pi * f)));
        var towardSun = new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation));
        return Basis.LookingAt(-towardSun);
    }
}
