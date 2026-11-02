using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Terrain;

/// <summary>
/// Bornes de la propriété générée (C24), lues depuis <c>data/property/parkland.json</c>. Tout paramètre est obligatoire ;
/// une clé inconnue, un champ manquant ou une borne incohérente refuse le fichier. Longueurs en mètres.
/// </summary>
public sealed record PropertyParams
{
    public required double ReliefAmplitudeMin { get; init; }

    public required double ReliefAmplitudeMax { get; init; }

    /// <summary>Longueur d'onde de la première octave du relief (les deux suivantes la divisent par 2 et 4).</summary>
    public required double ReliefWavelength { get; init; }

    /// <summary>Pente moyenne maximale du sol hors eau (0,06 = 6 %).</summary>
    public required double MaxMeanSlope { get; init; }

    public required int HillsMin { get; init; }

    public required int HillsMax { get; init; }

    /// <summary>Hauteur des collines et profondeur du creux (un seul), rayons à mi-hauteur.</summary>
    public required double BumpHeightMin { get; init; }

    public required double BumpHeightMax { get; init; }

    public required double BumpRadiusMin { get; init; }

    public required double BumpRadiusMax { get; init; }

    /// <summary>Rayon de la clairière plate du pavillon, au centre de la carte.</summary>
    public required double ClearingRadius { get; init; }

    /// <summary>Largeur de la couronne qui raccorde la clairière au relief.</summary>
    public required double ClearingBlend { get; init; }

    /// <summary>Bombement de la clairière (centre au-dessus du bord), pour que l'eau s'en écoule.</summary>
    public required double ClearingDome { get; init; }

    public required int PondsMin { get; init; }

    public required int PondsMax { get; init; }

    public required double PondRadiusMin { get; init; }

    public required double PondRadiusMax { get; init; }

    public required double PondDepth { get; init; }

    /// <summary>Largeur de la berge en pente douce autour d'un étang.</summary>
    public required double PondShore { get; init; }

    /// <summary>Distance minimale entre deux étangs.</summary>
    public required double PondSpacing { get; init; }

    /// <summary>Aucune eau à moins de cette distance du centre du pavillon.</summary>
    public required double MinWaterDistance { get; init; }

    public required double StreamRadius { get; init; }

    public required double StreamMinLength { get; init; }

    public required double StreamCarve { get; init; }

    /// <summary>Intervalle le long du ruisseau entre deux gués, et demi-longueur d'un gué.</summary>
    public required double FordSpacing { get; init; }

    public required double FordHalfLength { get; init; }

    /// <summary>Gués au moins dans la zone jouable (<see cref="PlayableRadius"/> autour du pavillon).</summary>
    public required int FordsMin { get; init; }

    public required double PlayableRadius { get; init; }

    public static PropertyParams Parkland => ParklandOnce.Value;

    private static readonly Lazy<PropertyParams> ParklandOnce = new(ReadParkland);

    /// <summary>Lit un fichier de bornes ; <see cref="InvalidDataException"/> s'il manque un champ, en contient un inconnu ou est incohérent.</summary>
    public static PropertyParams Parse(string json)
    {
        PropertyParams p;
        try
        {
            p = JsonSerializer.Deserialize<PropertyParams>(json, JsonOptions) ?? throw new InvalidDataException("Le fichier de propriété est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Propriété illisible : {e.Message}", e);
        }

        double[] positives =
        [
            p.ReliefAmplitudeMin, p.ReliefWavelength, p.MaxMeanSlope, p.BumpHeightMin, p.BumpRadiusMin, p.ClearingRadius, p.ClearingBlend,
            p.PondRadiusMin, p.PondDepth, p.PondShore, p.PondSpacing, p.MinWaterDistance, p.StreamRadius, p.StreamMinLength, p.FordSpacing, p.FordHalfLength, p.PlayableRadius,
        ];
        if (positives.Any(v => !(v > 0))
            || !(p.ClearingDome >= 0) || !(p.StreamCarve >= 0)
            || p.ReliefAmplitudeMin > p.ReliefAmplitudeMax || p.BumpHeightMin > p.BumpHeightMax || p.BumpRadiusMin > p.BumpRadiusMax || p.PondRadiusMin > p.PondRadiusMax
            || p.HillsMin < 0 || p.HillsMin > p.HillsMax || p.PondsMin < 1 || p.PondsMin > p.PondsMax || p.FordsMin < 0)
        {
            throw new InvalidDataException("Propriété incohérente : bornes négatives, nulles ou inversées.");
        }

        return p;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    private static PropertyParams ReadParkland()
    {
        using var stream = typeof(PropertyParams).Assembly.GetManifestResourceStream("property-parkland.json")
            ?? throw new InvalidOperationException("property-parkland.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
