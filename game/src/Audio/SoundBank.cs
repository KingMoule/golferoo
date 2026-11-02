using System.Text.Json;
using Godot;

namespace Golferoo.Game.Sound;

/// <summary>Un évènement sonore de <c>sounds.json</c> : ses fichiers (un tiré au hasard), son bus, son volume, sa variation de hauteur et son délai minimal entre deux lectures.</summary>
public sealed record SoundDef(string Id, string[] Files, string Bus, float VolumeDb, float PitchVariation, double MinGap)
{
    /// <summary>Flux chargés ; vide si tous les fichiers manquent (le son est alors absent, pas une erreur).</summary>
    public List<AudioStream> Streams { get; } = [];
}

/// <summary>
/// Banque de sons (C20) lue dans <c>res://data/audio/sounds.json</c>. Lecture stricte, comme les données de la sim :
/// clé inconnue, champ manquant, type faux, bus inconnu ou id en double refusent tout le fichier. Un fichier audio absent est sauté et journalisé une fois.
/// </summary>
public sealed class SoundBank
{
    public const string Path = "res://data/audio/sounds.json";
    public const string AudioDir = "res://assets/audio/";

    private static readonly string[] Buses = ["Musique", "Effets", "Ambiance"];
    private static readonly string[] Keys = ["id", "files", "bus", "volume_db", "pitch_variation", "min_gap"];

    private readonly Dictionary<string, SoundDef> _defs = [];

    public IReadOnlyCollection<SoundDef> Defs => _defs.Values;

    public SoundDef? Get(string id) => _defs.GetValueOrDefault(id);

    /// <summary>Lit le fichier ; rend null (avec la raison dans <paramref name="error"/>) s'il est absent ou refusé.</summary>
    public static SoundBank? LoadFile(string path, out string error)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            error = $"banque de sons absente : {path}";
            return null;
        }

        return Parse(Godot.FileAccess.GetFileAsString(path), out error);
    }

    public static SoundBank? Parse(string json, out string error)
    {
        var bank = new SoundBank();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 || !root.TryGetProperty("sounds", out var sounds) || sounds.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("la racine doit être {\"sounds\": [...]}");
            }

            foreach (var item in sounds.EnumerateArray())
            {
                var def = ReadDef(item);
                if (!bank._defs.TryAdd(def.Id, def))
                {
                    throw new FormatException($"id en double : {def.Id}");
                }
            }
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            error = $"banque de sons refusée : {e.Message}";
            return null;
        }

        error = string.Empty;
        foreach (var def in bank._defs.Values)
        {
            foreach (var file in def.Files)
            {
                var path = AudioDir + file;
                if (ResourceLoader.Exists(path) && ResourceLoader.Load(path) is AudioStream stream)
                {
                    def.Streams.Add(stream);
                }
                else
                {
                    GD.PushWarning($"son absent, sauté : {path}");
                }
            }
        }

        return bank;
    }

    private static SoundDef ReadDef(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("une entrée n'est pas un objet");
        }

        foreach (var property in item.EnumerateObject())
        {
            if (Array.IndexOf(Keys, property.Name) < 0)
            {
                throw new FormatException($"clé inconnue : {property.Name}");
            }
        }

        foreach (var key in Keys)
        {
            if (!item.TryGetProperty(key, out _))
            {
                throw new FormatException($"champ manquant : {key}");
            }
        }

        var id = item.GetProperty("id").GetString();
        var bus = item.GetProperty("bus").GetString();
        var files = item.GetProperty("files").EnumerateArray().Select(f => f.GetString()).ToArray();
        if (string.IsNullOrEmpty(id) || bus is null || Array.IndexOf(Buses, bus) < 0 || files.Length == 0 || files.Any(string.IsNullOrEmpty))
        {
            throw new FormatException($"entrée invalide : {id}");
        }

        var def = new SoundDef(id, files!, bus, item.GetProperty("volume_db").GetSingle(), item.GetProperty("pitch_variation").GetSingle(), item.GetProperty("min_gap").GetDouble());
        if (def.PitchVariation is < 0f or > 0.5f || def.MinGap < 0)
        {
            throw new FormatException($"valeur hors bornes : {id}");
        }

        return def;
    }
}
