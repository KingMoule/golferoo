using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golferoo.Sim.Golfers;

/// <summary>Une ligne du fichier : l'identifiant est le nom de <see cref="MoodEvent"/> en camelCase.</summary>
public sealed record MoodEventRow(string Id, int Value, bool Minor, string Thought);

/// <summary>
/// Humeur des golfeurs (C16), lue depuis <c>data/golfers/mood.json</c>, embarqué à la compilation (non sauvé, comme les arrivées).
/// L'humeur est un entier de <see cref="Min"/> à <see cref="Max"/>. Elle part de <see cref="Start"/>, baisse de 1 tous les <see cref="DropEveryHoles"/> trous,
/// et change aux événements de <see cref="Definitions"/>.
/// </summary>
public sealed record MoodParams
{
    public const int Min = -10;
    public const int Max = 10;

    public required int Start { get; init; }

    /// <summary>Baisse de 1 après chaque trou dont le numéro est un multiple de cette valeur.</summary>
    public required int DropEveryHoles { get; init; }

    /// <summary>Occurrence à partir de laquelle une contrariété mineure compte.</summary>
    public required int RepeatThreshold { get; init; }

    /// <summary>Humeur de sortie qui vaut une sortie tout à fait bonne pour la réputation.</summary>
    public required int ReputationFullMood { get; init; }

    public required double HardPuttMeters { get; init; }

    public required double EasyPuttMeters { get; init; }

    public required double PoorShotMinMeters { get; init; }

    /// <summary>Part de la distance au trou qu'un coup doit au moins gagner pour ne pas décevoir.</summary>
    public required double PoorShotProgress { get; init; }

    public required double NearMissMeters { get; init; }

    public required long TeeWaitTicks { get; init; }

    public required int SlowPlayTicks { get; init; }

    /// <summary>Usure de la cellule (0 à 255) à partir de laquelle le gazon est jugé abîmé.</summary>
    public required int WornTurf { get; init; }

    public required double PanoramaBeauty { get; init; }

    public required double BeautifulHoleBeauty { get; init; }

    public required double RemarkableTreeMeters { get; init; }

    public required double LongWalkMeters { get; init; }

    public required MoodEventRow[] Events { get; init; }

    /// <summary>Les événements, rangés par <see cref="MoodEvent"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<MoodEventDef> Definitions { get; private init; } = [];

    /// <summary>Nombre d'événements.</summary>
    public static int EventCount { get; } = Enum.GetValues<MoodEvent>().Length;

    /// <summary>Humeur bornée.</summary>
    public static int Clamp(int mood) => Math.Clamp(mood, Min, Max);

    /// <summary>Facteur du green fee à cette humeur, en points de base : ×1 à humeur neutre, <paramref name="min"/> à −10, <paramref name="max"/> à +10 (interpolation linéaire).</summary>
    public static long FeeBasisPoints(int mood, long min, long max) =>
        mood >= 0 ? 10_000 + (mood * (max - 10_000) / Max) : 10_000 + (mood * (10_000 - min) / -Min);

    /// <summary>Qualité d'une sortie de 0 (humeur −10) à 1 (humeur d'au moins <see cref="ReputationFullMood"/>) : ce que la réputation retient. <paramref name="demand"/> (difficulté) multiplie l'humeur qu'il faut pour une sortie parfaite : au-dessus de 1, golfeurs plus exigeants.</summary>
    public double ExitScore(int mood, double demand = 1.0) => Math.Clamp((mood - Min) / Math.Max((ReputationFullMood - Min) * demand, 1e-9), 0.0, 1.0);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Réglages du jeu.</summary>
    public static MoodParams Default { get; } = ReadDefault();

    /// <summary>Lit un fichier de réglages ; <see cref="InvalidDataException"/> s'il manque un paramètre, un événement, une pensée, ou s'il est incohérent.</summary>
    public static MoodParams Parse(string json)
    {
        MoodParams p;
        try
        {
            p = JsonSerializer.Deserialize<MoodParams>(json, JsonOptions) ?? throw new InvalidDataException("mood.json est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"mood.json illisible : {e.Message}", e);
        }

        if (p.Start is < Min or > Max || p.DropEveryHoles < 1 || p.RepeatThreshold < 1 || p.ReputationFullMood <= Min || p.ReputationFullMood > Max
            || p.TeeWaitTicks < 1 || p.SlowPlayTicks < 1 || p.WornTurf is < 1 or > 255)
        {
            throw new InvalidDataException("mood.json incohérent : départ de −10 à 10, baisse, répétition et seuils positifs.");
        }

        var defs = new MoodEventDef?[EventCount];
        foreach (var row in p.Events)
        {
            if (!Enum.TryParse<MoodEvent>(row.Id, ignoreCase: true, out var e) || !Enum.IsDefined(e) || defs[(int)e] is not null
                || string.IsNullOrWhiteSpace(row.Thought) || row.Value is 0 or < Min or > Max)
            {
                throw new InvalidDataException($"mood.json incohérent : événement « {row.Id} » inconnu, en double, sans pensée ou de valeur nulle.");
            }

            defs[(int)e] = new MoodEventDef(e, row.Value, row.Minor, row.Thought);
        }

        var missing = Enum.GetValues<MoodEvent>().Where(e => defs[(int)e] is null).ToList();
        return missing.Count == 0
            ? p with { Definitions = [.. defs.Select(d => d!)] }
            : throw new InvalidDataException($"mood.json incohérent : il manque {string.Join(", ", missing)}.");
    }

    private static MoodParams ReadDefault()
    {
        using var stream = typeof(MoodParams).Assembly.GetManifestResourceStream("mood.json")
            ?? throw new InvalidOperationException("mood.json n'est pas embarqué dans Golferoo.Sim.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
