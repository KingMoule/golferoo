namespace Golferoo.Sim.Turf;

public enum GreenSpeed : byte { Slow, Normal, Fast }

public enum FairwayFirmness : byte { Soft, Normal, Firm }

public enum RoughLength : byte { Short, Normal, Thick }

/// <summary>Les trois consignes d'entretien, pour tout le parcours (spec C05 : pas de réglage par trou ni par zone).</summary>
public readonly record struct TurfSettings(GreenSpeed GreenSpeed, FairwayFirmness FairwayFirmness, RoughLength RoughLength)
{
    public static TurfSettings Normal => new(GreenSpeed.Normal, FairwayFirmness.Normal, RoughLength.Normal);

    /// <summary>Consignes exigeantes (C14) : greens rapides ou rough court ; l'entretien des trous est alors majoré.</summary>
    public bool Demanding => GreenSpeed == GreenSpeed.Fast || RoughLength == RoughLength.Short;

    /// <summary>Hauteur visée du green, en mm : 3 (rapide), 4, 5 (lent).</summary>
    public double GreenMm => GreenSpeed switch { GreenSpeed.Fast => 3.0, GreenSpeed.Slow => 5.0, _ => 4.0 };

    /// <summary>Hauteur visée du rough, en mm : 30 (court), 50, 80 (épais).</summary>
    public double RoughMm => RoughLength switch { RoughLength.Short => 30.0, RoughLength.Thick => 80.0, _ => 50.0 };

    /// <summary>Facteur de résistance au roulement du fairway : 0,85 ferme, 1, 1,15 tendre.</summary>
    public double FairwayRoll => FairwayFirmness switch { FairwayFirmness.Firm => 0.85, FairwayFirmness.Soft => 1.15, _ => 1.0 };
}
