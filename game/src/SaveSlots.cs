using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Save;

namespace Golferoo.Game;

/// <summary>Sauvegarde C11 et C18 : les emplacements de user://saves (voir <see cref="SaveSlotStore"/>), la sérialisation en mémoire et l'écriture de fond.</summary>
public static class SaveSlots
{
    public static string Dir => ProjectSettings.GlobalizePath("user://saves");

    public static string QuickPath => SaveSlotStore.SavePath(Dir, SaveSlotStore.Quick);

    public static string AutoPath => SaveSlotStore.SavePath(Dir, SaveSlotStore.Auto);

    public static string PathOf(string slot) => SaveSlotStore.SavePath(Dir, slot);

    private static Task _writing = Task.CompletedTask;

    /// <summary>Le monde en octets (fil principal) et le temps que cela a pris.</summary>
    public static byte[] Serialize(World world, out double ms)
    {
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        using var memory = new MemoryStream();
        world.Save(memory);
        ms = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        return memory.ToArray();
    }

    /// <summary>Écrit un emplacement du joueur ou l'emplacement rapide : sauvegarde, métadonnées, vignette.</summary>
    public static void Write(string slot, byte[] bytes, SlotMeta meta, byte[]? thumbnail) => SaveSlotStore.Write(Dir, slot, bytes, meta, thumbnail);

    /// <summary>Écrit l'emplacement automatique sur une tâche de fond ; rend faux, sans rien écrire, si l'écriture précédente n'est pas finie.</summary>
    public static bool WriteAutoInBackground(byte[] bytes, SlotMeta meta, byte[]? thumbnail)
    {
        if (!_writing.IsCompleted)
        {
            return false;
        }

        var dir = Dir;
        _writing = Task.Run(() =>
        {
            try
            {
                SaveSlotStore.WriteAuto(dir, bytes, meta, thumbnail);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Sauvegarde automatique : {e.Message}");
            }
        });
        return true;
    }

    public static SlotMeta? Meta(string slot) => SaveSlotStore.ReadMeta(Dir, slot);

    /// <summary>La vignette de l'emplacement, ou nulle s'il n'en a pas.</summary>
    public static Texture2D? Thumbnail(string slot)
    {
        var path = SaveSlotStore.ThumbPath(Dir, slot);
        return System.IO.File.Exists(path) && Image.LoadFromFile(path) is { } image ? ImageTexture.CreateFromImage(image) : null; // sans vignette (fumée sans écran) : pas d'erreur Godot
    }

    /// <summary>Les emplacements existants, le plus récent d'abord.</summary>
    public static IEnumerable<string> ByRecency() =>
        new[] { QuickPath, AutoPath }.Where(System.IO.File.Exists).OrderByDescending(System.IO.File.GetLastWriteTimeUtc);

    public static World Read(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        return World.Load(stream);
    }
}
