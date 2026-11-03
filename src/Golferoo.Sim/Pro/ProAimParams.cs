using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Pro;

/// <summary>Réglages de la visée du pro (C27), lus depuis <c>data/pro/aim.json</c>, embarqué à la compilation.</summary>
/// <param name="AimFloor">Part plancher de la dispersion du club, atteinte par un clic tout près de la balle.</param>
/// <param name="CurveScale">Multiplicateur de la courbure du draw et du fade du pro.</param>
public sealed record ProAimParams(double AimFloor, double CurveScale)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>Réglages du jeu.</summary>
    public static ProAimParams Default { get; } = Read();

    internal static ProAimParams Parse(string json) =>
        JsonSerializer.Deserialize<ProAimParams>(json, JsonOptions) ?? throw new InvalidDataException("aim.json vide.");

    private static ProAimParams Read()
    {
        using var stream = typeof(ProAimParams).Assembly.GetManifestResourceStream("aim.json")
            ?? throw new InvalidOperationException("aim.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
