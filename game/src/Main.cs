using System.Globalization;
using Godot;
using Golferoo.Game.CameraRig;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Sim;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Game;

/// <summary>
/// Scène principale : charge le trou, avance la simulation à 20 Hz fixe (accumulateur, vitesses x1 à x16, pause)
/// et affiche son état interpolé. Aucune règle de jeu ici.
/// </summary>
public partial class Main : Node3D
{
    public const string HoleFile = "data/holes/proto-par4.json";
    public const int DefaultFactor = 2;
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
    private DirectionalLight3D? _sun;
    private int _month;
    private bool _turfOverlay;
    private RatingJob? _ratingJob;
    private (long Version, string Text) _stimp = (-1, string.Empty);

    /// <summary>Moment de la journée imposé au soleil (captures) ; sans valeur, il suit le calendrier.</summary>
    public double? SunDayFraction { get; set; }

    private static readonly string[] MonthNames = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];
    private static readonly string[] SeasonNames = ["printemps", "été", "automne", "hiver"];
    private static readonly string[] SkyNames = ["soleil", "nuages", "pluie", "forte pluie"];

    // D'où vient le vent, par huitième de tour depuis le nord (−Z), dans le sens horaire (vers +X, l'est).
    private static readonly string[] CompassNames = ["N", "NE", "E", "SE", "S", "SO", "O", "NO"];
    private Rect2I? _dirtyCells;
    private Rect2I? _dirtyTreeCells;

    public Main()
    {
        _current = _snapshotA;
        _previous = _snapshotB;
    }

    public World World { get; private set; } = null!;

    public TerrainRenderer TerrainView { get; private set; } = null!;

    /// <summary>Ciel et pluie à l'écran.</summary>
    public WeatherView WeatherView { get; private set; } = null!;

    public ActorLayer Actors { get; private set; } = null!;

    public MowerLayer Mowers { get; private set; } = null!;

    public IsoCamera Camera { get; private set; } = null!;

    public Brush Brush { get; private set; } = null!;

    public TreeTool TreeTool { get; private set; } = null!;

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

    /// <summary>Chemin réellement utilisé pour lire le JSON du trou (res:// à l'export, dépôt en éditeur).</summary>
    public static string HoleSource { get; private set; } = string.Empty;

    /// <summary>Lit le JSON du trou : res://data/holes/... (copié par le csproj, embarqué à l'export), sinon le dépôt.</summary>
    public static string ReadHoleJson()
    {
        var resPath = "res://" + HoleFile;
        if (Godot.FileAccess.FileExists(resPath))
        {
            using var file = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
            HoleSource = resPath;
            return file.GetAsText();
        }

        HoleSource = FindHolePath();
        return System.IO.File.ReadAllText(HoleSource);
    }

    /// <summary>Temps cumulé passé dans World.Step (ms) et nombre de ticks, pour le mode mesure.</summary>
    public double TickMsTotal { get; private set; }

    public long TickCount { get; private set; }

    public override void _Ready()
    {
        if (int.TryParse(CommandLineOption("--factor"), out var factor))
        {
            Factor = Math.Clamp(factor, HoleLoader.MinFactor, HoleLoader.MaxFactor);
        }

        var grid = HoleLoader.Parse(ReadHoleJson(), Factor);
        GD.Print($"HOLE source={HoleSource}");
        World = new World(seed: 1, grid);
        if (OS.GetCmdlineUserArgs().Contains("--fps-bench"))
        {
            FpsBench.FillTrees(World.Trees, grid);
        }

        var environment = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.55f, 0.75f, 0.92f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.8f, 0.9f),
                AmbientLightEnergy = 0.7f,
            },
        };
        AddChild(environment);

        TerrainView = new TerrainRenderer { Name = "TerrainView" };
        AddChild(TerrainView);
        TerrainView.Build(grid, World.Trees);
        TerrainView.SetSeason(World.Date.Season);

        Actors = new ActorLayer { Name = "Actors" };
        AddChild(Actors);
        Actors.Build(grid);

        Mowers = new MowerLayer { Name = "Mowers" };
        AddChild(Mowers);
        Mowers.Build(grid);

        var width = (float)(grid.CellsX * grid.CellSize);
        var depth = (float)(grid.CellsZ * grid.CellSize);
        Camera = new IsoCamera { Name = "IsoCamera" };
        Camera.SetBounds(new Rect2(0, 0, width, depth));
        AddChild(Camera);
        Camera.SetView(45f, 150f, new Vector3(width / 2f, 0f, depth / 2f));

        World.TerrainEdited += OnTerrainEdited;
        _ratingJob = new RatingJob(grid, World.Seed, World.Season, World.SetCourseRating);
        Brush = new Brush { Name = "Brush" };
        AddChild(Brush);
        Brush.Init(this);
        World.TreesEdited += OnTreesEdited;
        World.TreeFelled += tree => TerrainView.Trees?.PlayFall(tree);
        TreeTool = new TreeTool { Name = "TreeTool" };
        AddChild(TreeTool);
        TreeTool.Init(this);

        var turfPanel = new TurfPanel { Name = "TurfPanelLayer" };
        AddChild(turfPanel);
        turfPanel.Init(this);

        _hud = GetNode<Label>("TickLabel");
        _sun = GetNode<DirectionalLight3D>("Sun");
        WeatherView = new WeatherView { Name = "WeatherView" };
        AddChild(WeatherView);
        WeatherView.Init(environment.Environment, _sun);
        _month = World.Date.Month;
        ResetSnapshots();
        UpdateHud(0);

        if (OS.GetCmdlineUserArgs().Contains("--fps-bench"))
        {
            AddChild(new FpsBench { Name = "FpsBench" });
        }
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

        _ratingJob?.Process(delta, World.Season);
        Render();
        UpdateSun();
        UpdateWind();
        UpdateHud(delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key.Keycode);
                break;
            case InputEventMouseButton when Brush.Active || TreeTool.Active:
                // Le pinceau garde les clics : la caméra ne reçoit plus que la molette et le clic du milieu.
                if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp })
                {
                    Camera.Zoom(0.85f);
                }
                else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown })
                {
                    Camera.Zoom(1f / 0.85f);
                }

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
        if (TreeTool.HandleKey(key))
        {
            return;
        }

        if (key == Key.B && !Brush.Active)
        {
            TreeTool.SetActive(false);
        }

        if (Brush.HandleKey(key))
        {
            return;
        }

        switch (key)
        {
            case Key.Key1 or Key.Key2 or Key.Key3 or Key.Key4 or Key.Key5:
                _speedIndex = (int)key - (int)Key.Key1;
                break;
            case Key.Space:
                Paused = !Paused;
                break;
            case Key.H:
                _turfOverlay = !_turfOverlay;
                TerrainView.SetTurfOverlay(_turfOverlay);
                break;
            case Key.G:
                SpawnGroup();
                break;
            case Key.L:
                ToggleLoadMode();
                break;
            case Key.F10:
                SkipToNextMonth();
                break;
            case Key.F9:
                World.CycleSky();
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

    /// <summary>Commande de debug (F10) : saute au 1er du mois suivant ; les arbres poussent et la couche les redessine.</summary>
    public void SkipToNextMonth()
    {
        World.SkipToNextMonth();
        OnMonthChanged();
    }

    private void OnMonthChanged()
    {
        _month = World.Date.Month;
        // Un changement de feuillage refait déjà tous les blocs, croissance comprise.
        if (TerrainView.SetSeason(World.Date.Season) == 0)
        {
            TerrainView.Trees?.Regrow();
        }
    }

    private void StepOnce()
    {
        (_previous, _current) = (_current, _previous);
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        World.Step();
        if (World.Date.Month != _month)
        {
            OnMonthChanged();
        }

        TickMsTotal += System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        TickCount++;
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
        FlushTerrainEdits();
        Actors.Update(_current, _previous, alpha, _previousChanged);
        Mowers.Update(World.DayIndex, SunDayFraction ?? World.Date.DayFraction, _current);
        _previousChanged = false;
    }

    private void OnTerrainEdited(CellRect rect)
    {
        _ratingJob?.Touch();
        var r = new Rect2I(rect.MinX, rect.MinZ, rect.MaxX - rect.MinX + 1, rect.MaxZ - rect.MinZ + 1);
        _dirtyCells = _dirtyCells is { } d ? d.Merge(r) : r;
    }

    private void OnTreesEdited(CellRect rect)
    {
        var r = new Rect2I(rect.MinX, rect.MinZ, rect.MaxX - rect.MinX + 1, rect.MaxZ - rect.MinZ + 1);
        _dirtyTreeCells = _dirtyTreeCells is { } d ? d.Merge(r) : r;
    }

    /// <summary>Reconstruit une fois par image la zone modifiée depuis la dernière (les retouches d'un même tick se fusionnent).</summary>
    private void FlushTerrainEdits()
    {
        if (_dirtyTreeCells is { } trees)
        {
            // Arbres seuls (plantés, rasés, tombés) : les blocs d'arbres touchés, pas le terrain.
            _dirtyTreeCells = null;
            TerrainView.Trees?.RebuildCells(trees.Position.X, trees.Position.Y, trees.End.X, trees.End.Y);
        }

        if (_dirtyCells is { } rect)
        {
            _dirtyCells = null;
            TerrainView.RebuildChunks(rect);
        }
    }

    private void UpdateSun()
    {
        if (_sun is not null)
        {
            var date = World.Date;
            _sun.Basis = SunPath.At(SunDayFraction ?? date.DayFraction, date.Season);
        }
    }

    /// <summary>Temps du moment : balancement des arbres, drapeau, ciel, pluie et sol mouillé.</summary>
    private void UpdateWind()
    {
        var w = World.Wind;
        var dir = new Vector2((float)w.X, (float)w.Z);
        TreeLayer.SetWind(dir, dir.Length());
        Actors.SetWind(dir);
        WeatherView.Show(World.Weather.Sky, dir, Camera.Target, Camera.Size);
        TerrainView.SetWetness((float)World.SoilWetness);
        TerrainView.RefreshPuddles();
        TerrainView.RefreshTurf();
    }

    private void UpdateHud(double delta)
    {
        if (_hud is not null)
        {
            _hud.Text = $"{Engine.GetFramesPerSecond()} FPS | {Actors.VisibleGolfers} golfeurs | tick {World.Tick} | x{Speed}{(Paused ? " (pause)" : string.Empty)} | {DateText()} (F10 : mois suivant, T : outil arbres) | travaux arbres {World.TreeSpending}\n{WeatherText()} (F9 : ciel suivant) | {StimpText()} (H : état du gazon) | {RatingText()}";
        }
    }

    /// <summary>Vitesse des greens, recalculée seulement quand la hauteur ou les consignes changent.</summary>
    private string StimpText()
    {
        var turf = World.Terrain!.Turf;
        if (_stimp.Version != turf.Version)
        {
            var (meters, feet) = turf.Stimp();
            _stimp = (turf.Version, $"greens {meters.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} m ({feet:0} pieds)");
        }

        return _stimp.Text;
    }

    private string RatingText() =>
        World.CourseRating is { } r
            ? $"Note {r.Scratch18.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} / slope {r.Slope}"
            : "Note : calcul en cours";

    private string WeatherText()
    {
        var w = World.Wind;
        var kmh = Math.Sqrt((w.X * w.X) + (w.Z * w.Z)) * 3.6;
        var from = Math.Atan2(-w.X, w.Z); // direction d'où il vient, comptée depuis le nord (−Z) vers l'est (+X)
        var octant = ((int)Math.Round(from / (Math.PI / 4.0)) + 8) % 8;
        var forecast = string.Join(", ", Enumerable.Range(1, 3).Select(d => SkyNames[(int)World.Forecast(d)]));
        return $"{SkyNames[(int)World.Weather.Sky]}, vent {kmh:0} km/h {CompassNames[octant]} | prévision : {forecast}";
    }

    private string DateText()
    {
        var d = World.Date;
        return $"{d.Day} {MonthNames[d.Month - 1]}, an {d.Year}, {SeasonNames[(int)d.Season]}";
    }
}
