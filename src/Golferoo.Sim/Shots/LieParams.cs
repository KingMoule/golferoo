using System.Text.Json;
using System.Text.Json.Serialization;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Shots;

/// <summary>
/// Ce que le lie de départ change au coup (C25 lot 2) : multiplicateur de la vitesse de balle, des écarts types du club, et du backspin du club.
/// </summary>
public readonly record struct LieEffect(double Speed, double Sigma, double Backspin)
{
    /// <summary>Départ, fairway, chemin : le club tel quel.</summary>
    public static LieEffect None { get; } = new(1.0, 1.0, 1.0);
}

/// <summary>
/// Pénalité de lie, pour tous les golfeurs (C25 lot 2), lue depuis <c>data/shots/lie.json</c>, embarqué à la compilation. Depuis le rough ou le
/// bunker, la balle perd de la vitesse et les écarts types grandissent, d'autant plus que le handicap est haut : deux repères (scratch,
/// handicap 36), on interpole entre eux et on prolonge la droite au-delà. Le pro passe le handicap équivalent de son habileté de sortie.
/// </summary>
public sealed record LieParams(
    double RoughSpeedLossScratch,
    double RoughSpeedLossAt36,
    double RoughSigmaExcessScratch,
    double RoughSigmaExcessAt36,
    double BunkerSpeedLossScratch,
    double BunkerSpeedLossAt36,
    double BunkerSigmaExcessScratch,
    double BunkerSigmaExcessAt36,
    double RoughBackspinShare,
    double BunkerBackspinShare)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Réglages du jeu.</summary>
    public static LieParams Default { get; } = Read();

    internal static LieParams Parse(string json) =>
        JsonSerializer.Deserialize<LieParams>(json, JsonOptions) ?? throw new InvalidDataException("lie.json vide.");

    /// <summary>Effet du lie <paramref name="lie"/> pour un golfeur de handicap <paramref name="handicap"/> (négatif : comme scratch).</summary>
    public LieEffect At(Surface lie, double handicap)
    {
        var t = Math.Clamp(handicap, 0.0, SkillLevelsMax) / 36.0;
        static double Lerp(double a, double b, double t) => a + ((b - a) * t);
        return lie switch
        {
            Surface.Rough => new LieEffect(1.0 - Lerp(RoughSpeedLossScratch, RoughSpeedLossAt36, t), 1.0 + Lerp(RoughSigmaExcessScratch, RoughSigmaExcessAt36, t), RoughBackspinShare),
            Surface.Bunker => new LieEffect(1.0 - Lerp(BunkerSpeedLossScratch, BunkerSpeedLossAt36, t), 1.0 + Lerp(BunkerSigmaExcessScratch, BunkerSigmaExcessAt36, t), BunkerBackspinShare),
            _ => LieEffect.None,
        };
    }

    /// <summary>Effet du lie pour un golfeur ordinaire de niveau <paramref name="skill"/> (<see cref="Golfers.SkillLevels"/>).</summary>
    public LieEffect ForSkill(Surface lie, double skill) => At(lie, Golfers.SkillLevels.ToHandicap(skill));

    private const double SkillLevelsMax = Golfers.SkillLevels.MaxHandicap;

    private static LieParams Read()
    {
        using var stream = typeof(LieParams).Assembly.GetManifestResourceStream("lie.json")
            ?? throw new InvalidOperationException("lie.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
