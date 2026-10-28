using Golferoo.Sim.Save;

namespace Golferoo.Sim.Terrain;

/// <summary>Parcours de 1 à 18 trous, rangés dans l'ordre de jeu (numéros 1 à N). Le relief et les surfaces restent dans <see cref="TerrainGrid"/>.</summary>
public sealed class Course
{
    public const int MaxHoles = 18;

    private readonly List<CourseHole> _holes;

    public Course(IEnumerable<CourseHole> holes)
    {
        _holes = [.. holes];
        if (_holes.Count is < 1 or > MaxHoles)
        {
            throw new ArgumentException($"Un parcours compte de 1 à {MaxHoles} trous ({_holes.Count} reçus).", nameof(holes));
        }

        for (var i = 0; i < _holes.Count; i++)
        {
            if (_holes[i].Number != i + 1)
            {
                throw new ArgumentException($"Le trou n° {_holes[i].Number} est en position {i + 1} : les numéros vont de 1 à N dans l'ordre.", nameof(holes));
            }
        }
    }

    public IReadOnlyList<CourseHole> Holes => _holes;

    /// <summary>Ajoute un trou en fin de parcours (jeu, C12 lot 5).</summary>
    internal void Add(CourseHole hole) => _holes.Add(hole);

    /// <summary>Retire le dernier trou.</summary>
    internal void RemoveLast() => _holes.RemoveAt(_holes.Count - 1);

    public int Count => _holes.Count;

    /// <summary>Le trou de ce numéro (1 à N).</summary>
    public CourseHole this[int number] => number >= 1 && number <= _holes.Count
        ? _holes[number - 1]
        : throw new ArgumentOutOfRangeException(nameof(number), number, $"Trou inexistant (1 à {_holes.Count}).");

    /// <summary>Le trou 1 comme avant : départ, green et drapeau d'abord. Les trous suivants s'ajoutent avec leur numéro et leur par, ce qui laisse la somme d'un trou seul inchangée.</summary>
    internal void MixInto(ref ulong hash)
    {
        foreach (var h in _holes)
        {
            if (h.Number > 1)
            {
                TerrainGrid.Mix(ref hash, (ulong)h.Number);
                TerrainGrid.Mix(ref hash, (ulong)h.Par);
            }

            foreach (var p in new[] { h.Tee, h.Green, h.Hole })
            {
                TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.X));
                TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Z));
            }
        }
    }

    internal void Write(SaveWriter w) => w.List(_holes, h => h.Write(w));

    internal static Course Read(SaveReader r)
    {
        var holes = r.List(() => CourseHole.Read(r));
        try
        {
            return new Course(holes);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException("Parcours illisible.", e);
        }
    }
}
