using System.Reflection;

namespace Golferoo.Sim.Tests;

/// <summary>Critère 4 de C01 : Golferoo.Sim ne dépend pas de Godot.</summary>
public class ArchitectureTests
{
    [Fact]
    public void SimAssembly_DoesNotReferenceGodot()
    {
        var references = typeof(World).Assembly.GetReferencedAssemblies().Select(r => r.Name ?? "");

        Assert.DoesNotContain(references, name => name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SimSources_DoNotMentionGodot()
    {
        var simDir = Path.Combine(FindRepoRoot(), "src", "Golferoo.Sim");
        var offenders = Directory
            .EnumerateFiles(simDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("using Godot", StringComparison.Ordinal)
                    || text.Contains("GodotSharp", StringComparison.Ordinal)
                    || text.Contains("Godot.NET.Sdk", StringComparison.Ordinal);
            })
            .ToList();

        Assert.Empty(offenders);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Golferoo.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Golferoo.sln introuvable au-dessus du dossier de test.");
    }
}
