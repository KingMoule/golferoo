using System.Text.Json;
using System.Text.Json.Serialization;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Buildings;

/// <summary>Taille, prix et déblocage d'un genre de bâtiment, lus dans <c>data/buildings/buildings.json</c>.</summary>
public sealed record BuildingSpec
{
    /// <summary>Largeur en cellules, la porte au sud ; un quart de tour (porte à l'est ou à l'ouest) l'échange avec <see cref="Depth"/>.</summary>
    public required int Width { get; init; }

    public required int Depth { get; init; }

    /// <summary>Prix en cents ; la vente rend la moitié.</summary>
    public required long Cost { get; init; }

    /// <summary>Nombre de trous ouverts à partir duquel le genre se pose.</summary>
    public required int UnlockHoles { get; init; }

    /// <summary>Portée d'une visite : distance maximale en cellules du golfeur à la porte (0 : le genre ne se visite pas).</summary>
    public int Range { get; init; }

    /// <summary>Cents encaissés par visite.</summary>
    public long Revenue { get; init; }
}

/// <summary>Beauté ajoutée par un banc : poids et rayon en mètres.</summary>
public sealed record BenchBeauty(double Weight, double RadiusMeters);

/// <summary>Les quatre genres posables de C17 (tous sauf le pavillon).</summary>
public sealed record BuildingParams
{
    public required BuildingSpec SnackBar { get; init; }

    public required BuildingSpec ProShop { get; init; }

    public required BuildingSpec MaintenanceShed { get; init; }

    public required BuildingSpec Bench { get; init; }

    /// <summary>Un parcours d'au moins ce nombre de trous a son virage après le trou du milieu ; sinon après le dernier.</summary>
    public required int TurnMinHoles { get; init; }

    /// <summary>Probabilité de visiter le snack-bar au virage, par profil (absent : jamais).</summary>
    public required Dictionary<Archetype, double> SnackBarChance { get; init; }

    /// <summary>Probabilité de visiter le pro shop avant le départ, par profil (absent : jamais).</summary>
    public required Dictionary<Archetype, double> ProShopChance { get; init; }

    /// <summary>Facteur de la dispersion d'un golfeur qui a visité le pro shop (inférieur à 1).</summary>
    public required double ProShopSkillFactor { get; init; }

    /// <summary>Baisse de l'entretien mensuel avec au moins un hangar, en points de base.</summary>
    public required int ShedMaintenanceBasisPoints { get; init; }

    public required BenchBeauty BenchBeauty { get; init; }

    /// <summary>Probabilité qu'un golfeur de ce profil visite ce genre (0 pour un genre qu'on ne visite pas).</summary>
    public double Chance(BuildingKind kind, Archetype profile) =>
        (kind switch { BuildingKind.SnackBar => SnackBarChance, BuildingKind.ProShop => ProShopChance, _ => null })?.GetValueOrDefault(profile) ?? 0.0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Règle du jeu.</summary>
    public static BuildingParams Default { get; } = ReadDefault();

    /// <summary>Fiche d'un genre posable ; nulle pour le pavillon.</summary>
    public BuildingSpec? Spec(BuildingKind kind) => kind switch
    {
        BuildingKind.SnackBar => SnackBar,
        BuildingKind.ProShop => ProShop,
        BuildingKind.MaintenanceShed => MaintenanceShed,
        BuildingKind.Bench => Bench,
        _ => null,
    };

    /// <summary>Bâtiment de <paramref name="kind"/> dont l'angle nord-ouest est (<paramref name="minX"/>, <paramref name="minZ"/>), la porte du côté <paramref name="side"/> ; nul pour le pavillon.</summary>
    public Building? At(BuildingKind kind, int minX, int minZ, DoorSide side)
    {
        if (Spec(kind) is not { } s)
        {
            return null;
        }

        var (w, d) = side is DoorSide.East or DoorSide.West ? (s.Depth, s.Width) : (s.Width, s.Depth);
        return new Building(kind, new CellRect(minX, minZ, minX + w - 1, minZ + d - 1), side);
    }

    /// <summary>Lit un fichier de règle ; <see cref="InvalidDataException"/> s'il manque un genre ou si une valeur est incohérente.</summary>
    public static BuildingParams Parse(string json)
    {
        BuildingParams p;
        try
        {
            p = JsonSerializer.Deserialize<BuildingParams>(json, JsonOptions) ?? throw new InvalidDataException("buildings.json est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"buildings.json illisible : {e.Message}", e);
        }

        if (!new[] { p.SnackBar, p.ProShop, p.MaintenanceShed, p.Bench }.All(s => s is { Width: >= 1, Depth: >= 1, Cost: >= 0, UnlockHoles: >= 0, Range: >= 0, Revenue: >= 0 }))
        {
            throw new InvalidDataException("buildings.json incohérent : taille d'au moins 1 cellule, prix, seuil, portée et revenu positifs ou nuls.");
        }

        return p.TurnMinHoles >= 1 && p.SnackBarChance.Values.Concat(p.ProShopChance.Values).All(c => c is >= 0.0 and <= 1.0)
            && p.ProShopSkillFactor is > 0.0 and <= 1.0 && p.ShedMaintenanceBasisPoints is >= 0 and <= 10_000 && p.BenchBeauty is { Weight: >= 0.0, RadiusMeters: > 0.0 }
            ? p
            : throw new InvalidDataException("buildings.json incohérent : virage, probabilités de 0 à 1, facteur du pro shop de 0 (exclu) à 1, entretien de 0 à 10 000 points de base, beauté du banc.");
    }

    private static BuildingParams ReadDefault()
    {
        using var stream = typeof(BuildingParams).Assembly.GetManifestResourceStream("buildings.json")
            ?? throw new InvalidOperationException("buildings.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
