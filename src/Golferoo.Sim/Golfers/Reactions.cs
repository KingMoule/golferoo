using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Golfers;

/// <summary>
/// Réactions des golfeurs comptées par trou et par événement (C16) : celles du mois en cours, remises à zéro à chaque clôture de mois
/// (<see cref="CloseMonth"/>), et celles du mois précédent, gardées pour le rapport. Un trou retiré puis ajouté repart avec ses compteurs.
/// Les <see cref="MaxSpots"/> dernières réactions gardent aussi leur cellule (<see cref="Spots"/>), pour la carte du terrain.
/// </summary>
public sealed class Reactions
{
    /// <summary>Nombre de réactions dont la cellule est gardée : les plus récentes, au-delà les plus anciennes s'effacent.</summary>
    public const int MaxSpots = 256;

    private static readonly int Events = MoodParams.EventCount;
    private readonly List<ReactionSpot> _spots = [];

    private int[] _current = new int[Events * Course.MaxHoles];
    private int[] _previous = new int[Events * Course.MaxHoles];

    private static int Index(int hole, MoodEvent e) =>
        hole is >= 1 and <= Course.MaxHoles ? ((hole - 1) * Events) + (int)e : throw new ArgumentOutOfRangeException(nameof(hole), hole, "Trou de 1 à 18.");

    /// <summary>Réactions de ce type sur ce trou ce mois-ci.</summary>
    public int Count(int hole, MoodEvent e) => _current[Index(hole, e)];

    /// <summary>Réactions de ce type sur ce trou le mois précédent (0 avant la première clôture).</summary>
    public int Previous(int hole, MoodEvent e) => _previous[Index(hole, e)];

    /// <summary>Les dernières réactions et où elles ont eu lieu, de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<ReactionSpot> Spots => _spots;

    internal void Add(int hole, MoodEvent e, (int X, int Z)? at = null)
    {
        _current[Index(hole, e)]++;
        if (at is { } cell)
        {
            if (_spots.Count == MaxSpots)
            {
                _spots.RemoveAt(0);
            }

            _spots.Add(new ReactionSpot(cell.X, cell.Z, e));
        }
    }

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
        WriteSpots(w);
    }

    internal void WriteSpots(SaveWriter w) => w.List(_spots, s =>
    {
        w.Int(s.X);
        w.Int(s.Z);
        w.Byte((byte)s.Event);
    });

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
        if (r.Version >= 16)
        {
            var spots = r.List(() => new ReactionSpot(r.Int(), r.Int(), (MoodEvent)r.Byte()));
            if (spots.Count > MaxSpots || spots.Any(s => !Enum.IsDefined(s.Event)))
            {
                throw new InvalidDataException("Sauvegarde incohérente : position des réactions.");
            }

            reactions._spots.AddRange(spots);
        }

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

        foreach (var s in _spots)
        {
            World.Mix(ref hash, (ulong)(uint)s.X | ((ulong)(uint)s.Z << 24) | ((ulong)s.Event << 48));
        }
    }
}

/// <summary>Une réaction : la cellule du terrain où elle a eu lieu et son événement.</summary>
public readonly record struct ReactionSpot(int X, int Z, MoodEvent Event);
