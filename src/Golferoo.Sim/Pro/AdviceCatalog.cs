using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Pro;

/// <summary>Ce qui déclenche un conseil du pro (C19 lot 2).</summary>
public enum AdviceCondition
{
    FirstWalkout,
    LowCash,
    UnplayedHole,
    WornTurf,
}

/// <summary>Un conseil (<c>data/pro/advice.json</c>) : sa condition, son seuil et le texte de la bulle du pro.</summary>
public sealed record AdviceDef(string Id, AdviceCondition Condition, long Threshold, string Text);

/// <summary>Les conseils, dans l'ordre du fichier : l'indice d'un conseil est son bit dans <see cref="Objectives.GameState.AdviceGiven"/>.</summary>
public static class AdviceCatalog
{
    private sealed record File(AdviceDef[] Advice);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly Lazy<IReadOnlyList<AdviceDef>> DefaultOnce = new(ReadDefault);

    public static IReadOnlyList<AdviceDef> Default => DefaultOnce.Value;

    /// <summary>Lit un fichier de conseils ; <see cref="InvalidDataException"/> s'il est illisible, vide, dépasse 64 conseils, répète un identifiant ou une condition, ou a un seuil ou un texte invalide.</summary>
    public static IReadOnlyList<AdviceDef> Parse(string json)
    {
        AdviceDef[] all;
        try
        {
            all = JsonSerializer.Deserialize<File>(json, JsonOptions)?.Advice ?? throw new InvalidDataException("Le fichier des conseils est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Conseils illisibles : {e.Message}", e);
        }

        if (all.Length is 0 or > 64 || all.Select(a => a.Id).Distinct().Count() != all.Length || all.Select(a => a.Condition).Distinct().Count() != all.Length
            || all.Any(a => a.Threshold < 1 || string.IsNullOrWhiteSpace(a.Text)))
        {
            throw new InvalidDataException("Conseils incohérents : 1 à 64, identifiants et conditions uniques, seuils d'au moins 1, textes remplis.");
        }

        return all;
    }

    private static IReadOnlyList<AdviceDef> ReadDefault()
    {
        using var stream = typeof(AdviceCatalog).Assembly.GetManifestResourceStream("advice.json")
            ?? throw new InvalidOperationException("advice.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
