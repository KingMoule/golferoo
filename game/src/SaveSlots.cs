using Godot;
using Golferoo.Sim;

namespace Golferoo.Game;

/// <summary>Sauvegarde C11 : les deux emplacements de user://saves, la sérialisation en mémoire et l'écriture de fond.</summary>
public static class SaveSlots
{
    public static string Dir => ProjectSettings.GlobalizePath("user://saves");

    public static string QuickPath => System.IO.Path.Combine(Dir, "rapide.golf");

    public static string AutoPath => System.IO.Path.Combine(Dir, "auto.golf");

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

    /// <summary>Écrit le fichier (par un fichier temporaire, pour qu'un chargement ne lise jamais un fichier à moitié écrit).</summary>
    public static void Write(string path, byte[] bytes)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        System.IO.File.WriteAllBytes(temp, bytes);
        System.IO.File.Move(temp, path, overwrite: true);
    }

    /// <summary>Écrit sur une tâche de fond ; rend faux, sans rien écrire, si l'écriture précédente n'est pas finie.</summary>
    public static bool WriteInBackground(string path, byte[] bytes)
    {
        if (!_writing.IsCompleted)
        {
            return false;
        }

        _writing = Task.Run(() =>
        {
            try
            {
                Write(path, bytes);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Sauvegarde automatique : {e.Message}");
            }
        });
        return true;
    }

    /// <summary>Le plus récent des deux emplacements, ou null s'il n'y en a pas.</summary>
    public static string? Latest() =>
        new[] { QuickPath, AutoPath }.Where(System.IO.File.Exists).OrderByDescending(System.IO.File.GetLastWriteTimeUtc).FirstOrDefault();

    public static World Read(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        return World.Load(stream);
    }
}
