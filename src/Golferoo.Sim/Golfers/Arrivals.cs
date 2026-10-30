using System.Text.Json;
using System.Text.Json.Serialization;
using Golferoo.Sim.Time;
using Golferoo.Sim.Weather;

namespace Golferoo.Sim.Golfers;

/// <summary>
/// Arrivées des golfeurs au pavillon (spec C15 lot 2), lues depuis <c>data/golfers/arrivals.json</c>, embarqué à la compilation.
/// Les groupes arrivent tout au long de la journée, sans heures : le taux est <see cref="GroupsPerDay"/> × <see cref="Attraction"/> par journée de jeu.
/// </summary>
public sealed record ArrivalParams
{
    public required double GroupsPerDay { get; init; }

    /// <summary>Poids des groupes de 1, 2, 3 et 4 golfeurs.</summary>
    public required double[] GroupSizeWeights { get; init; }

    public required int QueueCapacity { get; init; }

    /// <summary>Groupes de visiteurs sur le parcours au plus, par trou du parcours (le banc P1 de C12 tient 36 groupes sur 18 trous) : au-delà, la file attend.</summary>
    public required int MaxGroupsPerHole { get; init; }

    /// <summary>Facteur sur la vitesse de marche des golfeurs IA (pas le vol de balle, ni le pro joué par le joueur).</summary>
    public required double AiWalkSpeedFactor { get; init; }

    /// <summary>Cadence de lecture du vol de balle des golfeurs IA (trajectoire et point de chute inchangés ; pas le pro joué par le joueur).</summary>
    public required double AiFlightSpeedFactor { get; init; }

    public required double HolesReference { get; init; }

    public required double HolesExponent { get; init; }

    public required int HolesCap { get; init; }

    public required double ReputationMinFactor { get; init; }

    public required double ReputationMaxFactor { get; init; }

    /// <summary>Facteur de météo par ciel : soleil, nuages, pluie, forte pluie.</summary>
    public required double[] WeatherFactors { get; init; }

    public required double ReputationStart { get; init; }

    /// <summary>Poids d'une sortie dans la moyenne glissante de la réputation.</summary>
    public required double ReputationWeight { get; init; }

    /// <summary>Part des places d'un groupe tentées avec un visiteur du vivier qui veut revenir.</summary>
    public required double ReturnShare { get; init; }

    /// <summary>Envie de revenir à partir de laquelle un visiteur peut être choisi.</summary>
    public required double ReturnThreshold { get; init; }

    public required double DesireStart { get; init; }

    public required double DesireGood { get; init; }

    public required double DesireRefused { get; init; }

    public required double HandicapMin { get; init; }

    public required double HandicapMax { get; init; }

    public required string[] FirstNames { get; init; }

    /// <summary>Banc et tests : attrait imposé, sans regard aux trous, à la réputation ni à la météo.</summary>
    public double? ForcedAttraction { get; init; }

    /// <summary>Attrait le plus haut : toutes les composantes au maximum.</summary>
    public double MaxAttraction => HolesFactor(HolesCap) * ReputationMaxFactor * WeatherFactors.Max();

    /// <summary>Groupes par jour du banc P1 et des tests de charge : de quoi saturer le parcours (la valeur de C15 lot 2).</summary>
    private const double SaturatingGroupsPerDay = 35;

    /// <summary>Attrait imposé à son maximum, à un taux qui sature le parcours (banc P1).</summary>
    public ArrivalParams AtMaxAttraction() => this with { ForcedAttraction = MaxAttraction, GroupsPerDay = Math.Max(GroupsPerDay, SaturatingGroupsPerDay) };

    /// <summary>Facteur de trous ouverts : nul sans trou ouvert, au plus <see cref="HolesCap"/> comptés.</summary>
    public double HolesFactor(int openHoles) => Math.Pow(Math.Min(openHoles, HolesCap) / HolesReference, HolesExponent);

    public double ReputationFactor(double reputation) => ReputationMinFactor + ((ReputationMaxFactor - ReputationMinFactor) * Math.Clamp(reputation, 0.0, 1.0));

    /// <summary>Attrait du club : trous ouverts × réputation × météo du jour (la saison n'a pas d'effet en soi).</summary>
    public double Attraction(int openHoles, double reputation, Sky sky) =>
        openHoles <= 0 ? 0.0 : ForcedAttraction ?? (HolesFactor(openHoles) * ReputationFactor(reputation) * WeatherFactors[(int)sky]);

    /// <summary>Probabilité d'une arrivée à un tick, d'attrait <paramref name="attraction"/> : constante sur la journée.</summary>
    public double RatePerTick(double attraction) => GroupsPerDay * attraction / Calendar.TicksPerDay;

    /// <summary>Taille d'un groupe (1 à 4) pour un tirage <paramref name="u"/> de [0, 1[.</summary>
    public int GroupSize(double u)
    {
        var target = u * GroupSizeWeights.Sum();
        for (var i = 0; i < GroupSizeWeights.Length; i++)
        {
            target -= GroupSizeWeights[i];
            if (target < 0)
            {
                return i + 1;
            }
        }

        return GroupSizeWeights.Length;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Réglages du jeu.</summary>
    public static ArrivalParams Default { get; } = ReadDefault();

    /// <summary>Lit un fichier de réglages ; <see cref="InvalidDataException"/> s'il manque un paramètre ou s'il est incohérent.</summary>
    public static ArrivalParams Parse(string json)
    {
        ArrivalParams p;
        try
        {
            p = JsonSerializer.Deserialize<ArrivalParams>(json, JsonOptions) ?? throw new InvalidDataException("arrivals.json est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"arrivals.json illisible : {e.Message}", e);
        }

        return p.AiWalkSpeedFactor > 0 && p.AiFlightSpeedFactor >= 1 && p.GroupsPerDay >= 0
            && p.GroupSizeWeights.Length == 4 && p.GroupSizeWeights.All(w => w >= 0) && p.GroupSizeWeights.Sum() > 0
            && p.WeatherFactors.Length == 4 && p.FirstNames.Length > 0 && p.QueueCapacity > 0 && p.MaxGroupsPerHole > 0 && p.HolesReference > 0 && p.HolesCap > 0
            && p.HandicapMax >= p.HandicapMin
            ? p
            : throw new InvalidDataException("arrivals.json incohérent : 4 tailles, 4 météos, une file et des trous positifs.");
    }

    private static ArrivalParams ReadDefault()
    {
        using var stream = typeof(ArrivalParams).Assembly.GetManifestResourceStream("arrivals.json")
            ?? throw new InvalidOperationException("arrivals.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
