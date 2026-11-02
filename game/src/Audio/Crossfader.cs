using Godot;

namespace Golferoo.Game.Sound;

/// <summary>
/// Lecteur à deux pistes pour une boucle (musique ou ambiance) : changer de morceau fond l'ancien vers le silence pendant que le nouveau monte ;
/// la pause fond puis arrête la piste, la reprise la relance en fondu. Un id absent de la banque ou sans fichier se traduit par le silence.
/// </summary>
public partial class Crossfader(string bus, double fade) : Node
{
    private const float Silent = -60f;

    private readonly AudioStreamPlayer[] _tracks = [new(), new()];
    private readonly Tween?[] _tweens = new Tween?[2];
    private int _active;
    private string? _id;
    private float _db;
    private bool _paused;

    public string? Id => _id;

    public override void _Ready()
    {
        foreach (var track in _tracks)
        {
            track.Bus = bus;
            track.VolumeDb = Silent;
            AddChild(track);
        }
    }

    public override void _ExitTree()
    {
        foreach (var track in _tracks)
        {
            track.Stop();
            track.Stream = null;
        }
    }

    public void Set(string? id, SoundDef? def)
    {
        if (id == _id)
        {
            return;
        }

        _id = id;
        _paused = false;
        var old = _tracks[_active];
        FadeTo(old, Silent, stop: true);
        if (def is null || def.Streams.Count == 0)
        {
            return;
        }

        _active = 1 - _active;
        var next = _tracks[_active];
        if (def.Streams[0] is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = true;
        }

        _db = def.VolumeDb;
        next.Stream = def.Streams[0];
        next.VolumeDb = Silent;
        next.Play();
        FadeTo(next, _db, stop: false);
    }

    /// <summary>Menu pause : la piste courante fond puis s'arrête ; à la reprise elle repart en fondu.</summary>
    public void Pause(bool paused)
    {
        if (paused == _paused)
        {
            return;
        }

        _paused = paused;
        var track = _tracks[_active];
        if (!track.Playing && !track.StreamPaused)
        {
            return;
        }

        if (paused)
        {
            FadeTo(track, Silent, stop: false, then: () => track.StreamPaused = true);
        }
        else
        {
            track.StreamPaused = false;
            FadeTo(track, _db, stop: false);
        }
    }

    /// <summary>Joue tous les fondus de l'arbre en cours jusqu'à leur fin (les tests n'attendent pas les images).</summary>
    public void Settle()
    {
        foreach (var tween in GetTree().GetProcessedTweens())
        {
            tween.CustomStep(fade + 1.0);
        }
    }

    public bool Playing(int track) => _tracks[track].Playing && !_tracks[track].StreamPaused;

    public int Active => _active;

    public float Db(int track) => _tracks[track].VolumeDb;

    /// <summary>Un seul fondu par piste : le nouveau tue l'ancien, dont le rappel (arrêt, pause) ne doit plus tomber.</summary>
    private void FadeTo(AudioStreamPlayer track, float db, bool stop, Action? then = null)
    {
        var index = Array.IndexOf(_tracks, track);
        _tweens[index]?.Kill();
        var tween = _tweens[index] = CreateTween();
        tween.TweenProperty(track, "volume_db", db, fade);
        tween.TweenCallback(Callable.From(() =>
        {
            if (stop)
            {
                track.Stop();
            }

            then?.Invoke();
        }));
    }
}
