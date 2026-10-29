namespace Golferoo.Sim.Rating;

/// <summary>
/// Note du parcours : coups moyens d'un golfeur scratch et d'un golfeur bogey sur les <paramref name="Holes"/> trous du parcours
/// (balle ramassée à <see cref="World.MaxStrokes"/>), et par du parcours. Les notes et le slope se ramènent à 18 trous.
/// <paramref name="ScratchByHole"/> : coups moyens du scratch trou par trou (C21), pour l'écart d'une carte qui a sauté un trou fermé ;
/// nul, la note scratch se répartit également.
/// </summary>
public sealed record CourseRating(int Holes, int Par, double ScratchStrokes, double BogeyStrokes, IReadOnlyList<double>? ScratchByHole = null)
{
    public const int Reference = 113;
    public const int MinSlope = 55;
    public const int MaxSlope = 155;

    public double ScratchPerHole => ScratchStrokes / Holes;

    /// <summary>Note scratch du trou <paramref name="hole"/> (base 0) ; la moyenne par trou sans note par trou ou au-delà.</summary>
    public double ScratchOf(int hole) => ScratchByHole is { } s && hole < s.Count ? s[hole] : ScratchPerHole;

    public double BogeyPerHole => BogeyStrokes / Holes;

    /// <summary>Note scratch ramenée à 18 trous (« Note 72,4 »).</summary>
    public double Scratch18 => ScratchPerHole * 18;

    public double Bogey18 => BogeyPerHole * 18;

    public double Par18 => (double)Par / Holes * 18;

    /// <summary>Égalité de valeur, note par trou comprise (un record compare sinon la liste par référence).</summary>
    public bool Equals(CourseRating? other) =>
        other is not null && (Holes, Par, ScratchStrokes, BogeyStrokes) == (other.Holes, other.Par, other.ScratchStrokes, other.BogeyStrokes)
        && (ScratchByHole ?? []).SequenceEqual(other.ScratchByHole ?? []) && (ScratchByHole is null) == (other.ScratchByHole is null);

    public override int GetHashCode() => HashCode.Combine(Holes, Par, ScratchStrokes, BogeyStrokes, ScratchByHole?.Count ?? -1);

    /// <summary>Slope USGA : 5,381 × (note bogey − note scratch) à 18 trous, arrondi, de 55 à 155 (113 : parcours moyen).</summary>
    public int Slope => (int)Math.Clamp(Math.Round(5.381 * (Bogey18 - Scratch18), MidpointRounding.AwayFromZero), MinSlope, MaxSlope);
}
