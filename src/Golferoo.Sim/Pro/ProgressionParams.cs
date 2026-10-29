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
    double CurveFraction)
{
    public const int MaxLevel = 50;

    /// <summary>Réglages du jeu.</summary>
    public static ProgressionParams Default { get; } = Read();

    internal static ProgressionParams Parse(string json) =>
        JsonSerializer.Deserialize<ProgressionParams>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
        }) ?? throw new InvalidDataException("progression.json vide.");

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
