using Godot;
using Golferoo.Game.CameraRig;
using Golferoo.Game.Rendering;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game;

/// <summary>
/// Scène principale : charge le trou, avance la simulation à 20 Hz fixe (accumulateur, vitesses x1 à x16, pause)
/// et affiche son état interpolé. Aucune règle de jeu ici.
/// </summary>
public partial class Main : Node3D
{
    public const string HoleFile = "data/holes/proto-par4.json";
    public const int DefaultFactor = 4;
    public const int LoadModeGolfers = 200;
    public const int MaxTicksPerFrame = 16;

    private static readonly int[] Speeds = [1, 2, 4, 8, 16];

    private readonly WorldSnapshot _snapshotA = new();
    private readonly WorldSnapshot _snapshotB = new();
    private WorldSnapshot _current;
    private WorldSnapshot _previous;
    private double _accumulator;
    private bool _previousChanged = true;
    private int _speedIndex;
    private bool _loadMode;
    private Label? _hud;

    public Main()
    {
        _current = _snapshotA;
        _previous = _snapshotB;
    }

    public World World { get; private set; } = null!;

    public TerrainRenderer TerrainView { get; private set; } = null!;

    public ActorLayer Actors { get; private set; } = null!;

    public IsoCamera Camera { get; private set; } = null!;

    public int Factor { get; private set; } = DefaultFactor;

    public bool Paused { get; set; }

    public int Speed => Speeds[_speedIndex];

    /// <summary>Valeur d'une option de ligne de commande après `--` (par exemple `--factor 2`), ou null.</summary>
    public static string? CommandLineOption(string name)
    {
        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Cherche data/holes/... en remontant depuis le projet Godot (le dépôt contient game/ et data/).</summary>
    public static string FindHolePath()
    {
        var dir = ProjectSettings.GlobalizePath("res://");
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = System.IO.Path.Combine(dir, HoleFile);
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }

            dir = System.IO.Path.GetDirectoryName(dir.TrimEnd('/', '\\')) ?? string.Empty;
        }

        throw new System.IO.FileNotFoundException($"{HoleFile} introuvable au-dessus de res://");
    }

    public override void _Ready()
    {
        if (int.TryParse(CommandLineOption("--factor"), out var factor))
        {
            Factor = Math.Clamp(factor, HoleLoader.MinFactor, HoleLoader.MaxFactor);
        }

        var grid = HoleLoader.Load(FindHolePath(), Factor);
        World = new World(seed: 1, grid);

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.55f, 0.75f, 0.92f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.8f, 0.9f),
                AmbientLightEnergy = 0.7f,
            },
        });

        TerrainView = new TerrainRenderer { Name = "TerrainView" };
        AddChild(TerrainView);
        TerrainView.Build(grid);

        Actors = new ActorLayer { Name = "Actors" };
        AddChild(Actors);
        Actors.Build(grid);

        var width = (float)(grid.CellsX * grid.CellSize);
        var depth = (float)(grid.CellsZ * grid.CellSize);
        Camera = new IsoCamera { Name = "IsoCamera" };
        Camera.SetBounds(new Rect2(0, 0, width, depth));
        AddChild(Camera);
        Camera.SetView(45f, 150f, new Vector3(width / 2f, 0f, depth / 2f));

        _hud = GetNode<Label>("TickLabel");
        ResetSnapshots();
        UpdateHud(0);
    }

    public override void _Process(double delta)
    {
        if (!Paused)
        {
            // Écrêté à 0,25 s : une fenêtre déplacée ou un gel ne doit pas rattraper des secondes de jeu d'un coup.
            _accumulator += Math.Min(delta, 0.25) * Speed;
        }

        var steps = 0;
        while (_accumulator >= World.TickSeconds && steps < MaxTicksPerFrame)
        {
            StepOnce();
            _accumulator -= World.TickSeconds;
            steps++;
        }

        if (steps >= MaxTicksPerFrame)
        {
            _accumulator = 0;
        }

        Render();
        UpdateHud(delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key.Keycode);
                break;
            case InputEventMouseButton { Pressed: true } button:
                if (button.ButtonIndex == MouseButton.WheelUp)
                {
                    Camera.Zoom(0.85f);
                }
                else if (button.ButtonIndex == MouseButton.WheelDown)
                {
                    Camera.Zoom(1f / 0.85f);
                }

                break;
            case InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Middle) != 0:
                Camera.RotateFree(-motion.Relative.X * 0.006f);
                break;
            default:
                break;
        }
    }

    /// <summary>Lance une paire de golfeurs sur le trou (touche G).</summary>
    public void SpawnGroup()
    {
        World.SpawnGroup();
        ResetSnapshots();
    }

    /// <summary>Bascule le mode charge à 200 golfeurs (touche L).</summary>
    public void ToggleLoadMode()
    {
        _loadMode = !_loadMode;
        World.SetLoadMode(_loadMode ? LoadModeGolfers : 0);
        ResetSnapshots();
    }

    /// <summary>Avance la simulation de <paramref name="ticks"/> ticks hors du temps réel (captures, tests).</summary>
    public void StepTicks(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            StepOnce();
        }

        _accumulator = 0;
        Render();
    }

    private void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Key1 or Key.Key2 or Key.Key3 or Key.Key4 or Key.Key5:
                _speedIndex = (int)key - (int)Key.Key1;
                break;
            case Key.Space:
                Paused = !Paused;
                break;
            case Key.G:
                SpawnGroup();
                break;
            case Key.L:
                ToggleLoadMode();
                break;
            case Key.Q:
                Camera.RotateQuarter(-1);
                break;
            case Key.E:
                Camera.RotateQuarter(1);
                break;
            default:
                break;
        }
    }

    private void StepOnce()
    {
        (_previous, _current) = (_current, _previous);
        World.Step();
        World.Snapshot(_current);
        _previousChanged = true;
    }

    private void ResetSnapshots()
    {
        World.Snapshot(_current);
        World.Snapshot(_previous);
        _previousChanged = true;
    }

    private void Render()
    {
        var alpha = (float)Math.Clamp(_accumulator / World.TickSeconds, 0.0, 1.0);
        Actors.Update(_current, _previous, alpha, _previousChanged);
        _previousChanged = false;
    }

    private void UpdateHud(double delta)
    {
        if (_hud is not null)
        {
            _hud.Text = $"{Engine.GetFramesPerSecond()} FPS | {Actors.VisibleGolfers} golfeurs | tick {World.Tick} | x{Speed}{(Paused ? " (pause)" : string.Empty)}";
        }
    }
}
