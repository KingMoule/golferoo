using Godot;

namespace Golferoo.Game.Sound;

/// <summary>Point d'entrée des sons : <c>Audio.Play("id")</c>. Les tests y glissent un lecteur factice.</summary>
public interface ISfx
{
    void Play(string id);

    /// <summary>Joue la musique <paramref name="id"/> en fondu (null : silence).</summary>
    void Music(string? id);

    /// <summary>Boucle d'ambiance <paramref name="id"/> en fondu enchaîné (null : silence).</summary>
    void Ambience(string? id);

    /// <summary>Pause de la musique (menu pause) : fondu puis arrêt, reprise en fondu.</summary>
    void PauseMusic(bool paused);
}

public static class Audio
{
    private sealed class Silent : ISfx
    {
        public void Play(string id)
        {
        }

        public void Music(string? id)
        {
        }

        public void Ambience(string? id)
        {
        }

        public void PauseMusic(bool paused)
        {
        }
    }

    public static readonly ISfx None = new Silent();

    public static ISfx Sfx { get; set; } = None;

    public static void Play(string id) => Sfx.Play(id);

    /// <summary>Arrête tout et libère les flux ; à appeler juste avant <c>GetTree().Quit()</c> (le moteur signale sinon des ressources encore utilisées).</summary>
    public static void Shutdown() => (Sfx as Sfx)?.Shutdown();
}

/// <summary>Anti-rafale : un délai minimal par id et une borne globale de sons par seconde (le jeu à ×16 ne doit pas faire de brouhaha). Horloge passée en paramètre.</summary>
public sealed class SfxGate(int maxPerSecond)
{
    private readonly Dictionary<string, double> _last = [];
    private readonly Queue<double> _recent = new();

    public bool Allow(string id, double minGap, double now)
    {
        while (_recent.Count > 0 && now - _recent.Peek() >= 1.0)
        {
            _recent.Dequeue();
        }

        if (_recent.Count >= maxPerSecond || (_last.TryGetValue(id, out var last) && now - last < minGap))
        {
            return false;
        }

        _last[id] = now;
        _recent.Enqueue(now);
        return true;
    }
}

/// <summary>
/// Lecteur de sons (autoload « Sfx », C20) : petit pool de <see cref="AudioStreamPlayer"/>, variation de hauteur tirée dans un RNG de présentation
/// (jamais celui de la sim), anti-rafale par <see cref="SfxGate"/>. Les boutons de l'interface cliquent tous : ils sont branchés à leur arrivée dans l'arbre.
/// </summary>
public partial class Sfx : Node, ISfx
{
    private const int PoolSize = 8;
    private const int MaxPerSecond = 8;

    private readonly SfxGate _gate = new(MaxPerSecond);
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<AudioStreamPlayer> _pool = [];
    private readonly Crossfader _music = new("Musique", 1.5);
    private readonly Crossfader _ambience = new("Ambiance", 3.0);
    private SoundBank? _bank;

    public override void _Ready()
    {
        _rng.Randomize();
        for (var i = 0; i < PoolSize; i++)
        {
            var player = new AudioStreamPlayer();
            AddChild(player);
            _pool.Add(player);
        }

        AddChild(_music);
        AddChild(_ambience);
        _bank = SoundBank.LoadFile(SoundBank.Path, out var error);
        if (_bank is null)
        {
            GD.PushWarning(error);
        }

        GameOptions.ApplyAudio();
        Audio.Sfx = this;
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
        Shutdown();
    }

    public void Shutdown()
    {
        foreach (var player in _pool)
        {
            player.Stop();
            player.Stream = null;
        }

        _music.Release();
        _ambience.Release();
        _bank?.Release();
        _bank = null;
        if (Audio.Sfx == this)
        {
            Audio.Sfx = Audio.None;
        }
    }

    public void Play(string id)
    {
        var def = _bank?.Get(id);
        if (def is null || def.Streams.Count == 0 || !_gate.Allow(id, def.MinGap, Time.GetTicksMsec() / 1000.0))
        {
            return;
        }

        var player = _pool.Find(p => !p.Playing) ?? _pool[0];
        player.Stream = def.Streams[_rng.RandiRange(0, def.Streams.Count - 1)];
        player.Bus = def.Bus;
        player.VolumeDb = def.VolumeDb;
        player.PitchScale = 1f + _rng.RandfRange(-def.PitchVariation, def.PitchVariation);
        player.Play();
    }

    public void Music(string? id) => _music.Set(id, id is null ? null : _bank?.Get(id));

    public void Ambience(string? id) => _ambience.Set(id, id is null ? null : _bank?.Get(id));

    public void PauseMusic(bool paused) => _music.Pause(paused);

    private void OnNodeAdded(Node node)
    {
        if (node is BaseButton button)
        {
            var id = node is Ui.UiThumb or Ui.UiToolButton ? "ui_select" : "ui_click";
            button.Pressed += () => Play(id);
        }
    }
}
