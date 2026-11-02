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

    /// <summary>Longueur d'onde de la première octave du bruit de boisement (la seconde la divise par 2).</summary>
    public required double WoodWavelength { get; init; }

    /// <summary>Part boisée tirée dans [min, max] des cellules sans eau hors de la zone dégagée (0,25 = 25 %).</summary>
    public required double WoodShareMin { get; init; }

    public required double WoodShareMax { get; init; }

    /// <summary>Rayon de la zone dégagée autour du pavillon, où le joueur construit ses premiers trous.</summary>
    public required double ClearedRadius { get; init; }

    /// <summary>Part boisée des cellules sans eau de la zone dégagée (jamais dans la clairière).</summary>
    public required double ClearedWoodShare { get; init; }

    /// <summary>Plafond d'arbres à la génération (la génération échoue au-delà et retente).</summary>
    public required int MaxTrees { get; init; }

    public required int RemarkablesMin { get; init; }

    public required int RemarkablesMax { get; init; }

    /// <summary>Âge des remarquables en multiple de l'âge adulte (au moins <see cref="Golferoo.Sim.Trees.Tree.RemarkableAgeFactor"/>).</summary>
    public required double RemarkableAgeFactorMin { get; init; }

    public required double RemarkableAgeFactorMax { get; init; }

    /// <summary>Un remarquable est posé à plus de cette distance de toute eau et de tout massif, et à plus de <see cref="ClearingRadius"/> + cette distance du pavillon.</summary>
    public required double RemarkableIsolation { get; init; }

    /// <summary>Distance maximale d'un remarquable au pavillon.</summary>
    public required double RemarkableMaxDistance { get; init; }

    /// <summary>Distance minimale entre deux remarquables.</summary>
    public required double RemarkableSpacing { get; init; }

    /// <summary>Surface constructible d'un seul tenant autour du pavillon, en hectares, sans eau et sous <see cref="BuildableMaxSlope"/>.</summary>
    public required double BuildableMinHa { get; init; }

    public required double BuildableMaxSlope { get; init; }

    /// <summary>Caps (par pas de 15°) qui offrent un couloir libre, et écart minimal en degrés entre deux d'entre eux.</summary>
    public required int CorridorsMin { get; init; }

    public required int CorridorSpreadDegrees { get; init; }

    public required double CorridorLength { get; init; }

    public required double CorridorHalfWidth { get; init; }

    public required double CorridorMaxSlope { get; init; }

    /// <summary>Un couloir part à moins de cette distance du pavillon.</summary>
    public required double CorridorStartRadius { get; init; }

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
            p.WoodWavelength, p.ClearedRadius, p.RemarkableIsolation, p.RemarkableMaxDistance, p.RemarkableSpacing, p.BuildableMaxSlope, p.CorridorLength, p.CorridorHalfWidth, p.CorridorMaxSlope, p.CorridorStartRadius,
        ];
        if (positives.Any(v => !(v > 0))
            || !(p.ClearingDome >= 0) || !(p.StreamCarve >= 0)
            || p.ReliefAmplitudeMin > p.ReliefAmplitudeMax || p.BumpHeightMin > p.BumpHeightMax || p.BumpRadiusMin > p.BumpRadiusMax || p.PondRadiusMin > p.PondRadiusMax
            || !(p.WoodShareMin >= 0) || p.WoodShareMin > p.WoodShareMax || p.WoodShareMax > 1 || !(p.ClearedWoodShare >= 0) || p.ClearedWoodShare > 1 || p.MaxTrees < 0
            || p.RemarkablesMin < 0 || p.RemarkablesMin > p.RemarkablesMax || p.RemarkableAgeFactorMin < Golferoo.Sim.Trees.Tree.RemarkableAgeFactor || p.RemarkableAgeFactorMin > p.RemarkableAgeFactorMax
            || !(p.BuildableMinHa >= 0) || p.CorridorsMin < 0 || p.CorridorSpreadDegrees is < 0 or > 180
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
