using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Pro;

/// <summary>Réglages de la progression du pro (C25), lus depuis <c>data/pro/progression.json</c>, embarqué à la compilation.</summary>
public sealed record ProgressionParams(
    double CostScale,
    double TrainingCapPerYear,
    double ClubFraction,
    double ClubFloor,
    double PutterMeters,
    double PutterFraction,
    double PrecisionFraction,
    double DosageFraction,
    double DistanceFloor,
    double FullForce,
    double CurveFraction,
    double WindMin,
    double WindFull,
    double GreenMeters,
    double GreenFraction,
    double GreenMinSlope,
    double WindBiasForce,
    double WindBiasAngle,
    double SlopeBiasAngle,
    double SlopeBiasScale,
    double ChipSigma)
{
    public const int MaxLevel = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Réglages du jeu.</summary>
    public static ProgressionParams Default { get; } = Read();

    internal static ProgressionParams Parse(string json) =>
        JsonSerializer.Deserialize<ProgressionParams>(json, JsonOptions) ?? throw new InvalidDataException("progression.json vide.");

    /// <summary>Le fichier de paramètres équivalent (sauvegarde).</summary>
    internal string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Valeurs de tous les paramètres, dans un ordre fixe (somme de contrôle).</summary>
    internal IEnumerable<long> Values() =>
        [BitConverter.DoubleToInt64Bits(CostScale), BitConverter.DoubleToInt64Bits(TrainingCapPerYear), BitConverter.DoubleToInt64Bits(ClubFraction),
         BitConverter.DoubleToInt64Bits(ClubFloor), BitConverter.DoubleToInt64Bits(PutterMeters), BitConverter.DoubleToInt64Bits(PutterFraction),
         BitConverter.DoubleToInt64Bits(PrecisionFraction), BitConverter.DoubleToInt64Bits(DosageFraction), BitConverter.DoubleToInt64Bits(DistanceFloor),
         BitConverter.DoubleToInt64Bits(FullForce), BitConverter.DoubleToInt64Bits(CurveFraction), BitConverter.DoubleToInt64Bits(WindMin),
         BitConverter.DoubleToInt64Bits(WindFull), BitConverter.DoubleToInt64Bits(GreenMeters), BitConverter.DoubleToInt64Bits(GreenFraction),
         BitConverter.DoubleToInt64Bits(GreenMinSlope), BitConverter.DoubleToInt64Bits(WindBiasForce), BitConverter.DoubleToInt64Bits(WindBiasAngle),
         BitConverter.DoubleToInt64Bits(SlopeBiasAngle), BitConverter.DoubleToInt64Bits(SlopeBiasScale), BitConverter.DoubleToInt64Bits(ChipSigma)];

    /// <summary>XP, en centièmes entiers, pour passer du niveau <paramref name="n"/> au suivant : a × (n + 1 + 300 × 2^((n + 1) / 7)) / 4.</summary>
    public long CostHundredths(int n) =>
        (long)Math.Round(100.0 * CostScale * (n + 1 + (300.0 * Math.Pow(2.0, (n + 1) / 7.0))) / 4.0, MidpointRounding.AwayFromZero);

    private static ProgressionParams Read()
    {
        using var stream = typeof(ProgressionParams).Assembly.GetManifestResourceStream("progression.json")
            ?? throw new InvalidOperationException("progression.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
