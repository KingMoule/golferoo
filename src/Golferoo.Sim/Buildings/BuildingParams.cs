using System.Text.Json;
using System.Text.Json.Serialization;
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
}

/// <summary>Les quatre genres posables de C17 (tous sauf le pavillon).</summary>
public sealed record BuildingParams
{
    public required BuildingSpec SnackBar { get; init; }

    public required BuildingSpec ProShop { get; init; }

    public required BuildingSpec MaintenanceShed { get; init; }

    public required BuildingSpec Bench { get; init; }

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

        return new[] { p.SnackBar, p.ProShop, p.MaintenanceShed, p.Bench }.All(s => s is { Width: >= 1, Depth: >= 1, Cost: >= 0, UnlockHoles: >= 0 })
            ? p
            : throw new InvalidDataException("buildings.json incohérent : taille d'au moins 1 cellule, prix et seuil positifs ou nuls.");
    }

    private static BuildingParams ReadDefault()
    {
        using var stream = typeof(BuildingParams).Assembly.GetManifestResourceStream("buildings.json")
            ?? throw new InvalidOperationException("buildings.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
