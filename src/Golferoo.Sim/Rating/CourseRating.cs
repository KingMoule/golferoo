namespace Golferoo.Sim.Rating;

/// <summary>
/// Note du parcours : coups moyens d'un golfeur scratch et d'un golfeur bogey sur les <paramref name="Holes"/> trous du parcours
/// (balle ramassée à <see cref="World.MaxStrokes"/>), et par du parcours. Les notes et le slope se ramènent à 18 trous.
/// </summary>
public sealed record CourseRating(int Holes, int Par, double ScratchStrokes, double BogeyStrokes)
{
    public const int Reference = 113;
    public const int MinSlope = 55;
    public const int MaxSlope = 155;

    public double ScratchPerHole => ScratchStrokes / Holes;

    public double BogeyPerHole => BogeyStrokes / Holes;

    /// <summary>Note scratch ramenée à 18 trous (« Note 72,4 »).</summary>
    public double Scratch18 => ScratchPerHole * 18;

    public double Bogey18 => BogeyPerHole * 18;

    public double Par18 => (double)Par / Holes * 18;

    /// <summary>Slope USGA : 5,381 × (note bogey − note scratch) à 18 trous, arrondi, de 55 à 155 (113 : parcours moyen).</summary>
    public int Slope => (int)Math.Clamp(Math.Round(5.381 * (Bogey18 - Scratch18), MidpointRounding.AwayFromZero), MinSlope, MaxSlope);
}
