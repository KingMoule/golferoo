using System.Text.Json;

namespace Golferoo.Sim.Save;

/// <summary>Ce qu'un écran « Charger » montre d'un emplacement, lu sans désérialiser le monde.</summary>
/// <param name="Checksum">Somme de contrôle du monde au moment de l'écriture.</param>
public sealed record SlotMeta(string Club, int Day, int Month, int Year, long Money, int OpenHoles, long Tick, ulong Checksum)
{
    public static SlotMeta Of(World world, string club)
    {
        var date = world.Date;
        return new SlotMeta(club, date.Day, date.Month, date.Year, world.Ledger.Balance, world.OpenHoles, world.Tick, world.Checksum());
    }
}

/// <summary>
/// Emplacements de sauvegarde (C18) : <c>slot1..3</c> écrits par le joueur, <c>auto</c> par le jeu, <c>rapide</c> par F5.
/// Chaque emplacement est un <c>.golf</c>, une ligne de métadonnées <c>.json</c> et une vignette <c>.jpg</c> voisines.
/// L'écriture automatique ne vise que <see cref="Auto"/> : elle ne peut pas écraser un emplacement du joueur.
/// </summary>
public static class SaveSlotStore
{
    public const string Auto = "auto";
    public const string Quick = "rapide";

    /// <summary>Les trois emplacements du joueur.</summary>
    public static readonly IReadOnlyList<string> Manual = ["slot1", "slot2", "slot3"];

    /// <summary>Tous les emplacements, dans l'ordre d'affichage.</summary>
    public static readonly IReadOnlyList<string> All = [.. Manual, Auto, Quick];

    public static string SavePath(string dir, string slot) => Path.Combine(dir, Check(slot) + ".golf");

    public static string ThumbPath(string dir, string slot) => Path.Combine(dir, Check(slot) + ".jpg");

    private static string MetaPath(string dir, string slot) => Path.Combine(dir, Check(slot) + ".json");

    /// <summary>Écrit l'emplacement : sauvegarde, métadonnées, puis vignette si elle existe (chacune par fichier temporaire).</summary>
    public static void Write(string dir, string slot, byte[] save, SlotMeta meta, byte[]? thumbnail)
    {
        Directory.CreateDirectory(dir);
        WriteAtomic(SavePath(dir, slot), save);
        WriteAtomic(MetaPath(dir, slot), JsonSerializer.SerializeToUtf8Bytes(meta));
        if (thumbnail is not null)
        {
            WriteAtomic(ThumbPath(dir, slot), thumbnail);
        }
    }

    /// <summary>Écriture de la sauvegarde automatique : le seul chemin du jeu vers <see cref="Auto"/>, qui ne touche aucun emplacement du joueur.</summary>
    public static void WriteAuto(string dir, byte[] save, SlotMeta meta, byte[]? thumbnail) => Write(dir, Auto, save, meta, thumbnail);

    /// <summary>Les métadonnées de l'emplacement, ou nul s'il est vide ou si sa ligne est illisible.</summary>
    public static SlotMeta? ReadMeta(string dir, string slot)
    {
        try
        {
            return File.Exists(SavePath(dir, slot)) ? JsonSerializer.Deserialize<SlotMeta>(File.ReadAllBytes(MetaPath(dir, slot))) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static World Read(string dir, string slot)
    {
        using var stream = File.OpenRead(SavePath(dir, slot));
        return World.Load(stream);
    }

    private static string Check(string slot) =>
        All.Contains(slot) ? slot : throw new ArgumentOutOfRangeException(nameof(slot), slot, "Emplacement inconnu.");

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }
}
