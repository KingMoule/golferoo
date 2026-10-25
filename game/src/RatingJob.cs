using Godot;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Game;

/// <summary>
/// Calcule la note du parcours hors du fil du jeu : au démarrage, 10 s après la dernière retouche du terrain et à chaque changement
/// de saison. Un seul calcul à la fois ; si le terrain est retouché pendant le calcul, son résultat est jeté et un nouveau
/// part après 10 s de calme. Le résultat est remis à <paramref name="apply"/> depuis <see cref="Process"/> (fil principal).
/// </summary>
public sealed class RatingJob
{
    public const double QuietSeconds = 10.0;

    private readonly TerrainGrid _terrain;
    private readonly ulong _seed;
    private readonly Action<CourseRating> _apply;
    private int _generation;
    private int _taskGeneration;
    private double _quiet = QuietSeconds;
    private bool _dirty = true;
    private Season _season;
    private Task<CourseRating>? _task;

    public RatingJob(TerrainGrid terrain, ulong seed, Season season, Action<CourseRating> apply)
    {
        _terrain = terrain;
        _seed = seed;
        _season = season;
        _apply = apply;
    }

    /// <summary>Le terrain vient d'être retouché : la note est périmée.</summary>
    public void Touch()
    {
        _generation++;
        _quiet = 0.0;
        _dirty = true;
    }

    /// <summary>À appeler à chaque image, sur le fil principal.</summary>
    public void Process(double delta, Season season)
    {
        if (season != _season)
        {
            _season = season;
            Touch();
        }

        if (_task is { IsCompleted: true } done)
        {
            _task = null;
            if (done.IsFaulted)
            {
                GD.PrintErr($"Note du parcours : {done.Exception?.GetBaseException().Message}");
                _dirty = false;
            }
            else if (_taskGeneration == _generation)
            {
                _apply(done.Result);
                _dirty = false;
            }
        }

        if (_task is null && _dirty)
        {
            _quiet += delta;
            if (_quiet >= QuietSeconds)
            {
                _taskGeneration = _generation;
                var s = _season;
                _task = Task.Run(() => CourseRater.Rate(_terrain, _seed, season: s));
            }
        }
    }
}
