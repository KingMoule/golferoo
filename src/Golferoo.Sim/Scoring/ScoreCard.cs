namespace Golferoo.Sim.Scoring;

/// <summary>
/// Carte de score : le par de chaque trou et les coups joués. Une balle ramassée se note <see cref="World.MaxStrokes"/>.
/// Le handicap de jeu est un nombre entier de coups, réparti sur les trous dans l'ordre (le parcours n'a pas d'index de difficulté).
/// </summary>
public sealed class ScoreCard
{
    private readonly int[] _pars;
    private readonly int[] _strokes;

    public ScoreCard(IReadOnlyList<int> pars)
    {
        ArgumentNullException.ThrowIfNull(pars);
        if (pars.Count == 0)
        {
            throw new ArgumentException("Un parcours compte au moins un trou.", nameof(pars));
        }

        _pars = [.. pars];
        _strokes = new int[_pars.Length];
    }

    public int HoleCount => _pars.Length;

    public int Par(int hole) => _pars[hole];

    /// <summary>Coups joués au trou, 0 s'il n'est pas encore joué.</summary>
    public int Strokes(int hole) => _strokes[hole];

    public bool IsComplete => !_strokes.Contains(0);

    public int TotalPar => _pars.Sum();

    /// <summary>Note les coups d'un trou (au plus <see cref="World.MaxStrokes"/>) ; une balle ramassée s'inscrit avec <see cref="PickUp"/>.</summary>
    public void Record(int hole, int strokes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(strokes, 1);
        _strokes[hole] = Math.Min(strokes, World.MaxStrokes);
    }

    public void PickUp(int hole) => _strokes[hole] = World.MaxStrokes;

    /// <summary>Total brut des trous joués.</summary>
    public int Gross => _strokes.Sum();

    /// <summary>Brut moins le handicap de jeu.</summary>
    public int Net(int playingHandicap) => Gross - playingHandicap;

    /// <summary>Coups rendus sur un trou : la part entière du handicap, plus un coup sur les premiers trous pour le reste.</summary>
    public int HandicapStrokes(int hole, int playingHandicap) =>
        (playingHandicap / HoleCount) + (hole < playingHandicap % HoleCount ? 1 : 0);

    public int NetStrokes(int hole, int playingHandicap) => _strokes[hole] - HandicapStrokes(hole, playingHandicap);

    /// <summary>Points stableford d'un trou : 0 à double bogey ou pire, 1 bogey, 2 par, 3 birdie, 4 eagle, 5 albatros.</summary>
    public int StablefordPoints(int hole, int playingHandicap = 0) =>
        _strokes[hole] == 0 ? 0 : Math.Clamp(2 + _pars[hole] - NetStrokes(hole, playingHandicap), 0, 5);

    public int Stableford(int playingHandicap = 0) =>
        Enumerable.Range(0, HoleCount).Sum(h => StablefordPoints(h, playingHandicap));

    /// <summary>Résultat d'un trou contre le par : +1 mieux que le par, 0 égal, -1 moins bien.</summary>
    public int VersusParHole(int hole, int playingHandicap = 0) =>
        _strokes[hole] == 0 ? 0 : Math.Sign(_pars[hole] - NetStrokes(hole, playingHandicap));

    /// <summary>Trous gagnés moins trous perdus contre le par.</summary>
    public int VersusPar(int playingHandicap = 0) =>
        Enumerable.Range(0, HoleCount).Sum(h => VersusParHole(h, playingHandicap));
}
