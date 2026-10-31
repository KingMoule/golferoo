using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Golfers;

/// <summary>
/// Réactions des golfeurs comptées par trou et par événement (C16) : celles du mois en cours, remises à zéro à chaque clôture de mois
/// (<see cref="CloseMonth"/>), et celles du mois précédent, gardées pour le rapport. Un trou retiré puis ajouté repart avec ses compteurs.
/// </summary>
public sealed class Reactions
{
    private static readonly int Events = MoodParams.EventCount;

    private int[] _current = new int[Events * Course.MaxHoles];
    private int[] _previous = new int[Events * Course.MaxHoles];

    private static int Index(int hole, MoodEvent e) =>
        hole is >= 1 and <= Course.MaxHoles ? ((hole - 1) * Events) + (int)e : throw new ArgumentOutOfRangeException(nameof(hole), hole, "Trou de 1 à 18.");

    /// <summary>Réactions de ce type sur ce trou ce mois-ci.</summary>
    public int Count(int hole, MoodEvent e) => _current[Index(hole, e)];

    /// <summary>Réactions de ce type sur ce trou le mois précédent (0 avant la première clôture).</summary>
    public int Previous(int hole, MoodEvent e) => _previous[Index(hole, e)];

    internal void Add(int hole, MoodEvent e) => _current[Index(hole, e)]++;

    /// <summary>Le mois en cours devient le mois précédent et les compteurs repartent de zéro.</summary>
    internal void CloseMonth()
    {
        (_previous, _current) = (_current, _previous);
        Array.Clear(_current);
    }

    /// <summary>Rien n'a été compté, ni ce mois-ci ni le précédent.</summary>
    internal bool Untouched => _current.All(c => c == 0) && _previous.All(c => c == 0);

    internal void Write(SaveWriter w)
    {
        w.List(_current, w.Int);
        w.List(_previous, w.Int);
    }

    internal static Reactions Read(SaveReader r)
    {
        var reactions = new Reactions();
        var (current, previous) = (r.List(r.Int), r.List(r.Int));
        if (current.Count != reactions._current.Length || previous.Count != reactions._previous.Length || current.Concat(previous).Any(c => c < 0))
        {
            throw new InvalidDataException("Sauvegarde incohérente : compteurs de réactions.");
        }

        current.CopyTo(reactions._current);
        previous.CopyTo(reactions._previous);
        return reactions;
    }

    internal void Mix(ref ulong hash)
    {
        foreach (var c in _current)
        {
            World.Mix(ref hash, (ulong)c);
        }

        foreach (var c in _previous)
        {
            World.Mix(ref hash, (ulong)c);
        }
    }
}
