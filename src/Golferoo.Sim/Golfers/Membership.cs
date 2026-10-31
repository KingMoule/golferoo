using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Golfers;

/// <summary>Niveau d'adhésion d'un visiteur du vivier (C16). Les valeurs entrent dans la sauvegarde : ne pas réordonner.</summary>
public enum MemberLevel : byte
{
    Visitor = 0,
    Member = 1,
    Silver = 2,
    Gold = 3,
    Platinum = 4,
}

/// <summary>
/// Règle d'adhésion (<c>data/golfers/membership.json</c>, embarqué à la compilation, non sauvé comme les autres données) :
/// des points après chaque partie finie, et les seuils de chaque niveau.
/// </summary>
public sealed record MembershipParams
{
    /// <summary>Points pour atteindre Member, Silver, Gold puis Platinum.</summary>
    public required int[] Thresholds { get; init; }

    public required int LikedHolesPerPoint { get; init; }

    public required int GoodExitMood { get; init; }

    public required int GoodExitPoints { get; init; }

    public required int ProPoints { get; init; }

    /// <summary>Niveau de <paramref name="points"/> points (sans plafond de membres : Platinum est le dernier niveau).</summary>
    public MemberLevel LevelOf(int points)
    {
        var level = MemberLevel.Visitor;
        for (var i = 0; i < Thresholds.Length; i++)
        {
            if (points >= Thresholds[i])
            {
                level = (MemberLevel)(i + 1);
            }
        }

        return level;
    }

    /// <summary>Points d'une partie finie : trous aimés et humeur de sortie, plus <see cref="ProPoints"/> pour un membre accompagné du pro.</summary>
    public int PointsFor(int likedHoles, int exitMood, bool memberWithPro) =>
        (likedHoles / LikedHolesPerPoint) + (exitMood >= GoodExitMood ? GoodExitPoints : 0) + (memberWithPro ? ProPoints : 0);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Règle du jeu.</summary>
    public static MembershipParams Default { get; } = ReadDefault();

    /// <summary>Lit un fichier de règle ; <see cref="InvalidDataException"/> s'il manque un paramètre ou s'il est incohérent.</summary>
    public static MembershipParams Parse(string json)
    {
        MembershipParams p;
        try
        {
            p = JsonSerializer.Deserialize<MembershipParams>(json, JsonOptions) ?? throw new InvalidDataException("membership.json est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"membership.json illisible : {e.Message}", e);
        }

        return p.Thresholds is { Length: 4 } t && t[0] >= 1 && t.Zip(t.Skip(1), (a, b) => a < b).All(x => x)
            && p.LikedHolesPerPoint >= 1 && p.GoodExitMood is >= MoodParams.Min and <= MoodParams.Max && p.GoodExitPoints >= 0 && p.ProPoints >= 0
            ? p
            : throw new InvalidDataException("membership.json incohérent : quatre seuils croissants, trous aimés par point d'au moins 1, humeur de −10 à 10, points positifs.");
    }

    private static MembershipParams ReadDefault()
    {
        using var stream = typeof(MembershipParams).Assembly.GetManifestResourceStream("membership.json")
            ?? throw new InvalidOperationException("membership.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
