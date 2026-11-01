using System.Text.Json;
using System.Text.Json.Serialization;
using Golferoo.Sim.Buildings;

namespace Golferoo.Sim.Objectives;

/// <summary>Ce que compte un objectif : il est atteint quand le compteur vaut au moins le seuil.</summary>
public enum ObjectiveCondition
{
    OpenHoles,
    Members,
    GoldMembers,
    PositiveYears,
    ProPromotions,
}

/// <summary>Un objectif du club (<c>data/objectives/objectives.json</c>). <paramref name="Voucher"/> : bâtiment dont la pose est offerte en plus de l'argent de la difficulté.</summary>
public sealed record ObjectiveDef(string Id, ObjectiveCondition Condition, int Threshold, string Title, string Text, BuildingKind? Voucher = null);

/// <summary>La liste des objectifs, dans l'ordre du fichier : l'indice d'un objectif est son bit dans <see cref="GameState"/>.</summary>
public static class ObjectiveCatalog
{
    private sealed record File(ObjectiveDef[] Objectives);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly Lazy<IReadOnlyList<ObjectiveDef>> DefaultOnce = new(ReadDefault);

    public static IReadOnlyList<ObjectiveDef> Default => DefaultOnce.Value;

    /// <summary>Lit un fichier d'objectifs ; <see cref="InvalidDataException"/> s'il est illisible, vide, dépasse 64 objectifs, répète un identifiant ou a un seuil nul.</summary>
    public static IReadOnlyList<ObjectiveDef> Parse(string json)
    {
        ObjectiveDef[] all;
        try
        {
            all = JsonSerializer.Deserialize<File>(json, JsonOptions)?.Objectives ?? throw new InvalidDataException("Le fichier des objectifs est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Objectifs illisibles : {e.Message}", e);
        }

        if (all.Length is 0 or > 64 || all.Select(o => o.Id).Distinct().Count() != all.Length || all.Any(o => o.Threshold < 1 || string.IsNullOrWhiteSpace(o.Title)))
        {
            throw new InvalidDataException("Objectifs incohérents : 1 à 64, identifiants uniques, seuils d'au moins 1, titres remplis.");
        }

        return all;
    }

    private static IReadOnlyList<ObjectiveDef> ReadDefault()
    {
        using var stream = typeof(ObjectiveCatalog).Assembly.GetManifestResourceStream("objectives.json")
            ?? throw new InvalidOperationException("objectives.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
