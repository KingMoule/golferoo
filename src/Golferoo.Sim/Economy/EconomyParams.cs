using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Economy;

/// <summary>
/// Paramètres de l'économie d'une difficulté (spec C14), lus depuis <c>data/economy/&lt;difficulté&gt;.json</c> : montants en cents entiers,
/// taux en points de base (1 % = 100). Tous obligatoires, positifs ou nuls ; un paramètre inconnu est refusé aussi.
/// « Le neuf », les tests et le tutoriel jouent en <see cref="Facile"/>, embarqué dans la bibliothèque.
/// </summary>
public sealed record EconomyParams
{
    public required long StartingMoney { get; init; }

    public required long OverdraftInterestBasisPoints { get; init; }

    /// <summary>Découvert permis à partir de <see cref="OverdraftFreeHoles"/> trous ouverts (le solde ne descend pas sous son opposé).</summary>
    public required long OverdraftLimit { get; init; }

    public required long OverdraftFreeHoles { get; init; }

    /// <summary>C18 : trésorerie sous l'opposé de ce seuil pendant <see cref="BankruptcyMonths"/> mois de suite.</summary>
    public required long BankruptcyThreshold { get; init; }

    public required long BankruptcyMonths { get; init; }

    public required long PaintFairwayPerM2 { get; init; }

    public required long PaintGreenPerM2 { get; init; }

    public required long PaintTeePerM2 { get; init; }

    public required long PaintBunkerPerM2 { get; init; }

    public required long PaintWaterPerM2 { get; init; }

    public required long PaintPathPerM2 { get; init; }

    public required long PaintRoughPerM2 { get; init; }

    public required long PaintTreesPerM2 { get; init; }

    public required long ReliefPerM3 { get; init; }

    public required long NewHole { get; init; }

    public required long TreeSapling { get; init; }

    public required long TreeMound { get; init; }

    public required long TreeRaze { get; init; }

    public required long TreeRemarkableMultiplier { get; init; }

    public required long TreeClearPerM2 { get; init; }

    public required long TreeClearFallen { get; init; }

    public required long MaintenancePerHole { get; init; }

    /// <summary>Majoration de l'entretien des trous si les consignes de gazon sont exigeantes (<see cref="Turf.TurfSettings.Demanding"/>).</summary>
    public required long DemandingTurfBasisPoints { get; init; }

    public required long GreenFeeVisitor { get; init; }

    public required long GreenFeeMember { get; init; }

    public required long GreenFeeSilver { get; init; }

    public required long GreenFeeGold { get; init; }

    public required long GreenFeePlatinum { get; init; }

    /// <summary>Cotisations annuelles, prélevées par douzièmes (lot 2).</summary>
    public required long DuesMember { get; init; }

    public required long DuesSilver { get; init; }

    public required long DuesGold { get; init; }

    public required long DuesPlatinum { get; init; }

    public required long MoodFactorMinBasisPoints { get; init; }

    public required long MoodFactorMaxBasisPoints { get; init; }

    /// <summary>C15 : déplacer le pavillon.</summary>
    public required long ClubhouseMove { get; init; }

    /// <summary>Prix d'un mètre carré peint de <paramref name="surface"/> (surface d'arrivée).</summary>
    public long PaintCost(Surface surface) => surface switch
    {
        Surface.Fairway => PaintFairwayPerM2,
        Surface.Green => PaintGreenPerM2,
        Surface.Tee => PaintTeePerM2,
        Surface.Bunker => PaintBunkerPerM2,
        Surface.Water => PaintWaterPerM2,
        Surface.Path => PaintPathPerM2,
        Surface.Rough => PaintRoughPerM2,
        Surface.Trees => PaintTreesPerM2,
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Surface inconnue."),
    };

    public long PlantCost(TreePlantStage stage) => stage == TreePlantStage.Mound ? TreeMound : TreeSapling;

    /// <summary>Abattage d'un arbre debout, multiplié pour un remarquable.</summary>
    public long RazeCost(Tree tree) => TreeRaze * (tree.Remarkable ? TreeRemarkableMultiplier : 1);

    /// <summary>Paramètres de Facile (<c>data/economy/facile.json</c>, embarqué à la compilation).</summary>
    public static EconomyParams Facile => FacileOnce.Value;

    private static readonly Lazy<EconomyParams> FacileOnce = new(ReadFacile);

    public static EconomyParams Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new InvalidDataException($"Lecture impossible de '{path}' : {e.Message}", e);
        }

        return Parse(json);
    }

    /// <summary>Lit un fichier de paramètres ; <see cref="InvalidDataException"/> si un paramètre manque, est négatif ou inconnu.</summary>
    public static EconomyParams Parse(string json) => Parse(json, olderSave: false);

    /// <summary>Avec <paramref name="olderSave"/> (sauvegarde v11), les paramètres nés depuis (<c>clubhouseMove</c>) prennent la valeur de Facile s'ils manquent.</summary>
    internal static EconomyParams Parse(string json, bool olderSave)
    {
        EconomyParams e;
        try
        {
            if (olderSave && System.Text.Json.Nodes.JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is System.Text.Json.Nodes.JsonObject node && !node.ContainsKey("clubhouseMove"))
            {
                node["clubhouseMove"] = Facile.ClubhouseMove;
                json = node.ToJsonString();
            }

            e = JsonSerializer.Deserialize<EconomyParams>(json, JsonOptions) ?? throw new InvalidDataException("Le fichier d'économie est vide.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Paramètres d'économie invalides : {error.Message}", error);
        }

        foreach (var p in Fields)
        {
            if ((long)p.GetValue(e)! < 0)
            {
                throw new InvalidDataException($"Paramètre d'économie négatif : {p.Name}.");
            }
        }

        return e;
    }

    /// <summary>Le fichier de paramètres équivalent (sauvegarde).</summary>
    internal string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Valeurs de tous les paramètres, dans un ordre fixe (somme de contrôle).</summary>
    internal IEnumerable<long> Values() => Fields.Select(p => (long)p.GetValue(this)!);

    private static readonly PropertyInfo[] Fields = [.. typeof(EconomyParams).GetProperties().Where(p => p.PropertyType == typeof(long)).OrderBy(p => p.MetadataToken)];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    private static EconomyParams ReadFacile()
    {
        using var stream = typeof(EconomyParams).Assembly.GetManifestResourceStream("facile.json")
            ?? throw new InvalidOperationException("facile.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
