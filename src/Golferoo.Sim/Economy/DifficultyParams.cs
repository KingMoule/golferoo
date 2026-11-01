using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Economy;

/// <summary>
/// Niveau de difficulté (C18), lu depuis <c>data/difficulty/facile.json</c> (un fichier par niveau, un seul chargé). Complète l'économie, qui garde l'argent de départ, les coûts et le seuil de faillite.
/// Les multiplicateurs valent 1,0 au comportement actuel du jeu. Tout paramètre est obligatoire et positif ou nul.
/// </summary>
public sealed record DifficultyParams
{
    /// <summary>Exigence des golfeurs (multiplicateur).</summary>
    public required double GolferDemand { get; init; }

    /// <summary>Vitesse de baisse de l'humeur (multiplicateur).</summary>
    public required double MoodDrain { get; init; }

    /// <summary>Récompense en cents de chaque objectif, par identifiant.</summary>
    public required Dictionary<string, long> ObjectiveRewards { get; init; }

    public static DifficultyParams Facile => FacileOnce.Value;

    private static readonly Lazy<DifficultyParams> FacileOnce = new(ReadFacile);

    /// <summary>Lit un fichier de difficulté ; <see cref="InvalidDataException"/> si un paramètre manque, est négatif ou inconnu.</summary>
    public static DifficultyParams Parse(string json)
    {
        DifficultyParams p;
        try
        {
            p = JsonSerializer.Deserialize<DifficultyParams>(json, JsonOptions) ?? throw new InvalidDataException("Le fichier de difficulté est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Difficulté illisible : {e.Message}", e);
        }

        if (!(p.GolferDemand >= 0) || !(p.MoodDrain >= 0) || p.ObjectiveRewards.Values.Any(v => v < 0))
        {
            throw new InvalidDataException("Difficulté incohérente : paramètres négatifs.");
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

    private static DifficultyParams ReadFacile()
    {
        using var stream = typeof(DifficultyParams).Assembly.GetManifestResourceStream("difficulty-facile.json")
            ?? throw new InvalidOperationException("difficulty-facile.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
