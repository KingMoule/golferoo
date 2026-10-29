using System.Globalization;
using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Game.Tutorial;
using Golferoo.Game.Ui;
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
    /// <summary>Parcours joué : « Le neuf » (C12), ou celui d'`--course fichier` ; le tutoriel et la mesure FPS gardent leur trou.</summary>
    public static string HoleFile => TutorialMode ? "data/holes/blank-coude.json" : FpsBench.Active && FpsBench.Golfers == 0 ? "data/holes/proto-par4.json" : CourseOverride ?? CommandLineOption("--course") ?? "data/courses/le-neuf.json";

    /// <summary>Parcours imposé par le code (captures de trou seul) ; sans valeur, le parcours par défaut s'applique.</summary>
    public static string? CourseOverride { get; set; }

    private static bool? _tutorialOverride;

    /// <summary>Tutoriel de C07 : le monde est la terre vierge, sans ligue. Option de lancement `-- --tutorial`, ou fixé par le code (« Oui » de la proposition, fumée).</summary>
    public static bool TutorialMode
    {
        get => _tutorialOverride ?? OS.GetCmdlineUserArgs().Contains("--tutorial");
        set => _tutorialOverride = value;
    }

    /// <summary>Le pro propose le tutoriel au démarrage ; la fumée et les captures l'éteignent pour garder l'écran net.</summary>
    public static bool OfferTutorial { get; set; } = true;
    public const int DefaultFactor = 2;
    public const int LoadModeGolfers = 200;
    public const int MaxTicksPerFrame = 16;

    /// <summary>Les vitesses du jeu, de ×1 à ×16 (touches 1 à 5, boutons de la barre).</summary>
    public static readonly int[] Speeds = [1, 2, 4, 8, 16];

    private readonly WorldSnapshot _snapshotA = new();
    private readonly WorldSnapshot _snapshotB = new();
    private WorldSnapshot _current;
    private WorldSnapshot _previous;
    private double _accumulator;
    private bool _previousChanged = true;
    private int _speedIndex;
    private bool _loadMode;
    private Label? _hud;
    private UiPill? _holePill;
    private UiPill? _messagePill;
    private double _hudSince = double.MaxValue;
    private int _cameraHole = 1;
    private DirectionalLight3D? _sun;
    private int _month;
    private int _day;
    private bool _turfOverlay;
    private RatingJob? _ratingJob;
    private int _ratedHoles;
    private LeaguePanel? _league;
    private (long Version, string Text) _stimp = (-1, string.Empty);
    private string _message = string.Empty;
    private ulong _messageUntil;

    /// <summary>Monde chargé (F8, `--load`) que le prochain <see cref="_Ready"/> reprend au lieu d'en créer un neuf.</summary>
    private static World? _pendingWorld;
    private static bool _loadOptionDone;
    private static string? _carryMessage;
    private static (int Speed, bool Paused)? _carryPlay;

    /// <summary>Moment de la journée imposé au soleil (captures) ; sans valeur, il suit le calendrier.</summary>
    public double? SunDayFraction { get; set; }

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

    /// <summary>Anneau de relief et collines autour de la carte.</summary>
    public HorizonLayer Horizon { get; private set; } = null!;

    /// <summary>Ciel et pluie à l'écran.</summary>
    public WeatherView WeatherView { get; private set; } = null!;

    public ActorLayer Actors { get; private set; } = null!;

    public MowerLayer Mowers { get; private set; } = null!;

    public CameraRig Camera { get; private set; } = null!;

    public Brush Brush { get; private set; } = null!;

    public TreeTool TreeTool { get; private set; } = null!;

    /// <summary>Racine de l'interface (calque « Ui ») : barre, tiroir, pastilles.</summary>
    public UiRoot Ui { get; private set; } = null!;

    /// <summary>La pile des panneaux (Gazon, Ligue, Votre pro) au-dessus de la barre.</summary>
    public PanelStack Panels { get; private set; } = null!;

    /// <summary>Le fil des messages du jeu (bouton Birdie).</summary>
    public Birdie Birdie { get; private set; } = null!;

    /// <summary>Échelle d'interface (<c>-- --ui-scale 1.25</c>) : 1, 1,25 ou 1,5.</summary>
    public static float UiScale => float.TryParse(CommandLineOption("--ui-scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale) ? Math.Clamp(scale, 1f, 1.5f) : 1f;

    /// <summary>La barre du bas, d'où part tout outil.</summary>
    public BottomBar Bar { get; private set; } = null!;

    /// <summary>Panneau des consignes d'entretien (bouton Gazon).</summary>
    public TurfPanel Turf { get; private set; } = null!;

    /// <summary>Panneau de la ligue (bouton Ligue, touche J).</summary>
    public LeaguePanel? League => _league;

    /// <summary>Surcouche d'état du gazon (bouton Vues, touche H).</summary>
    public bool TurfOverlay => _turfOverlay;

    /// <summary>Numéro du trou que montre la caméra (trou précédent et suivant) : celui que l'outil de pose modifie.</summary>
    public int CameraHole => _cameraHole;

    public int Factor { get; private set; } = DefaultFactor;

    public bool Paused { get; set; }

    public ProAim ProAim { get; private set; } = null!;

    public ProPanel? ProPanel { get; private set; }

    public PlaceTool PlaceTool { get; private set; } = null!;

    public TutorialPanel? Tutorial { get; private set; }

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
    public static string FindHolePath(string? file = null)
    {
        file ??= HoleFile;
        var dir = ProjectSettings.GlobalizePath("res://");
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = System.IO.Path.Combine(dir, file);
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }

            dir = System.IO.Path.GetDirectoryName(dir.TrimEnd('/', '\\')) ?? string.Empty;
        }

        throw new System.IO.FileNotFoundException($"{file} introuvable au-dessus de res://");
    }

    /// <summary>Chemin réellement utilisé pour lire le JSON du trou (res:// à l'export, dépôt en éditeur).</summary>
    public static string HoleSource { get; private set; } = string.Empty;

    /// <summary>Lit le JSON du trou : res://data/holes/... (copié par le csproj, embarqué à l'export), sinon le dépôt.</summary>
    public static string ReadHoleJson(string? holeFile = null)
    {
        var name = holeFile ?? HoleFile;
        var resPath = "res://" + name;
        if (Godot.FileAccess.FileExists(resPath))
        {
            using var file = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
            HoleSource = resPath;
            return file.GetAsText();
        }

        HoleSource = FindHolePath(name);
        return System.IO.File.ReadAllText(HoleSource);
    }

    /// <summary>Charge le parcours (ou le trou seul) de <see cref="HoleFile"/> ; à l'export, les trous du parcours se lisent aussi dans res://.</summary>
    public static TerrainGrid LoadTerrain(int factor)
    {
        var json = ReadHoleJson();
        if (!HoleSource.StartsWith("res://", StringComparison.Ordinal))
        {
            return CourseLoader.Parse(json, System.IO.Path.GetDirectoryName(HoleSource)!, factor);
        }

        var dir = HoleSource[..HoleSource.LastIndexOf('/')];
        return CourseLoader.Parse(json, dir, factor, relative =>
        {
            var path = (dir + "/" + relative).SimplifyPath();
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
                ?? throw new HoleDataException($"Lecture impossible de '{path}'.");
            return file.GetAsText();
        });
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

        if (_pendingWorld is null && !_loadOptionDone && CommandLineOption("--load") is { } file)
        {
            _loadOptionDone = true;
            _carryMessage = TryLoad(file);
        }

        var loaded = _pendingWorld;
        _pendingWorld = null;
        TerrainGrid grid;
        if (loaded is not null)
        {
            World = loaded;
            grid = loaded.Terrain!;
            if (_carryPlay is { } play)
            {
                (_speedIndex, Paused) = play;
            }
        }
        else
        {
            grid = LoadTerrain(Factor);
            GD.Print($"HOLE source={HoleSource}");
            World = new World(seed: 1, grid, league: !grid.Blank);
            if (FpsBench.Active)
            {
                FpsBench.FillTrees(World.Trees, grid);
            }
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
        TerrainView.SetDate(World.Date);

        Horizon = new HorizonLayer { Name = "Horizon" };
        AddChild(Horizon);
        Horizon.Build(grid);

        Actors = new ActorLayer { Name = "Actors" };
        AddChild(Actors);
        Actors.Build(grid);

        Mowers = new MowerLayer { Name = "Mowers" };
        AddChild(Mowers);
        Mowers.Build(grid);

        var width = (float)(grid.CellsX * grid.CellSize);
        var depth = (float)(grid.CellsZ * grid.CellSize);
        Camera = new CameraRig { Name = "CameraRig", FollowSource = GolferPosition, GroundHeight = TerrainHeight };
        Camera.SetBounds(new Rect2(0, 0, width, depth));
        AddChild(Camera);
        Camera.SetView(45f, 150f, new Vector3(width / 2f, 0f, depth / 2f));
        if (grid.Course.Count > 1)
        {
            CameraGoToHole(1);
        }

        World.TerrainEdited += OnTerrainEdited;
        if (!FpsBench.Active && !grid.Blank)
        {
            // Pas en mesure FPS : avec 5 000 arbres la note prend ~30 s de cœur et fausse la mesure du rendu.
            _ratedHoles = grid.Course.Count;
            _ratingJob = new RatingJob(grid, World.Trees, World.Seed, World.Season, World.SetCourseRating, dirty: World.CourseRating is null);
        }

        Brush = new Brush { Name = "Brush" };
        AddChild(Brush);
        Brush.Init(this);
        World.TreesEdited += OnTreesEdited;
        World.TreeFelled += tree => TerrainView.Trees?.PlayFall(tree);
        TreeTool = new TreeTool { Name = "TreeTool" };
        AddChild(TreeTool);
        TreeTool.Init(this);

        Ui = UiTheme.CreateRoot(this);
        Panels = PanelStack.Create(Ui);
        Turf = new TurfPanel { Name = "TurfPanel" };
        AddChild(Turf);
        Turf.Init(this);

        if (World.League is not null)
        {
            _league = new LeaguePanel { Name = "LeaguePanel" };
            AddChild(_league);
            _league.Init(this);
            ProPanel = new ProPanel { Name = "ProPanel" };
            AddChild(ProPanel);
            ProPanel.Init(this);
        }

        PlaceTool = new PlaceTool { Name = "PlaceTool" };
        AddChild(PlaceTool);
        PlaceTool.Init(this);
        BuildUi();
        Birdie = new Birdie { Name = "Birdie" };
        AddChild(Birdie);
        Birdie.Init(this);
        // L'échelle agrandit toute l'interface (calques 2D), pas le rendu 3D.
        GetWindow().ContentScaleFactor = UiScale;

        // Débogage (FPS, tick, golfeurs) : caché au lancement, F12 le montre.
        _hud = GetNode<Label>("TickLabel");
        _hud.Visible = false;
        _sun = GetNode<DirectionalLight3D>("Sun");
        _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        _sun.DirectionalShadowMaxDistance = 400f;
        WeatherView = new WeatherView { Name = "WeatherView" };
        AddChild(WeatherView);
        WeatherView.Init(environment.Environment, _sun);
        _month = World.Date.Month;
        _day = World.DayIndex;
        ResetSnapshots();
        UpdateHud(0);

        if (FpsBench.Active)
        {
            AddChild(new FpsBench { Name = "FpsBench" });
        }

        ProAim = new ProAim { Name = "ProAim" };
        AddChild(ProAim);
        ProAim.Init(this);
        if (!FpsBench.Active && World.League is not null)
        {
            // Dans le jeu, le pro ne joue jamais seul : à son tour, le mode visée met le jeu en pause.
            World.ManualPro = true;
        }

        if (!FpsBench.Active)
        {
            Tutorial = new TutorialPanel { Name = "Tutorial" };
            AddChild(Tutorial);
            Tutorial.Init(this, loaded is null && TutorialMode ? TutorialPanel.Mode.Welcome : loaded is null && OfferTutorial && OS.GetCmdlineUserArgs().Length == 0 ? TutorialPanel.Mode.Offer : TutorialPanel.Mode.Off);
        }

        _carryPlay = null;
        if (_carryMessage is not null)
        {
            Birdie.Post(Birdie.Club, _carryMessage, "#sauvegarde");
            _carryMessage = null;
        }

        if (OS.GetCmdlineUserArgs().Contains("--pro") && World.League is not null)
        {
            // Test du pro : une partie d'entraînement part tout de suite.
            World.StartProRound();
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

        if (World.Terrain is { } rated && rated.Course.Count != _ratedHoles)
        {
            _ratedHoles = rated.Course.Count; // trou ajouté ou retiré : la sim a invalidé la note, on la refait
            _ratingJob?.Touch();
        }

        _ratingJob?.Process(delta, World.Season);
        Render();
        UpdateSun();
        UpdateWind();
        UpdateHud(delta);
    }

    /// <summary>Tab passe avant l'UI : sinon un bouton qui a le focus le prend pour la navigation (<c>ui_focus_next</c>).</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("camera_follow_next"))
        {
            CameraFollowNext();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when ProAim.HandleClick(click.Position):
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when PlaceTool.HandleClick(click.Position):
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
                if (Camera.Mode == CameraMode.Free)
                {
                    Camera.Tilt(motion.Relative.Y * 0.004f);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>Lance une paire de golfeurs sur le trou (touche G).</summary>
    public void SpawnGroup()
    {
        if (!World.HoleOpen)
        {
            // Terre vierge : le trou n'est pas encore valide, la bulle du pro dit ce qui manque.
            return;
        }

        World.SpawnGroup();
        ResetSnapshots();
    }

    /// <summary>Bascule le mode charge à 200 golfeurs (touche L), ou à <paramref name="golfers"/>.</summary>
    public void ToggleLoadMode(int golfers = LoadModeGolfers)
    {
        _loadMode = !_loadMode;
        World.SetLoadMode(_loadMode ? golfers : 0);
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

    private void HandleKey(InputEventKey @event)
    {
        var key = @event.Keycode;
        if (ProAim.HandleKey(key) || PlaceTool.HandleKey(key) || TreeTool.HandleKey(key))
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
                ToggleTurfOverlay();
                break;
            case Key.F12:
                ToggleDebug();
                break;
            case Key.Escape:
                Panels.CloseLast();
                break;
            case Key.G:
                SpawnGroup();
                break;
            case Key.L:
                ToggleLoadMode();
                break;
            case Key.J:
                _league?.Toggle();
                break;
            case Key.O:
                ProPanel?.Toggle();
                break;
            case Key.F10:
                SkipToNextMonth();
                break;
            case Key.F9:
                World.CycleSky();
                break;
            default:
                HandleSaveAction(@event);
                HandleCameraAction(@event);
                break;
        }
    }

    private void HandleSaveAction(InputEvent @event)
    {
        if (@event.IsActionPressed("game_quicksave"))
        {
            QuickSave();
        }
        else if (@event.IsActionPressed("game_quickload"))
        {
            QuickLoad();
        }
    }

    /// <summary>La terre vierge du tutoriel : ni sauvegarde ni sauvegarde automatique.</summary>
    private bool InTutorial => World.Terrain!.Blank;

    internal void ShowMessage(string text)
    {
        _message = text;
        _messageUntil = Time.GetTicksMsec() + 3000;
        _messagePill?.SetText(text);
        _messagePill?.SetVisible(true);
    }

    /// <summary>Vitesse ×<paramref name="speed"/> (1, 2, 4, 8 ou 16) ; elle relance le jeu s'il était en pause.</summary>
    public void SetSpeed(int speed)
    {
        _speedIndex = Math.Max(0, Array.IndexOf(Speeds, speed));
        Paused = false;
    }

    public void ToggleTurfOverlay()
    {
        _turfOverlay = !_turfOverlay;
        TerrainView.SetTurfOverlay(_turfOverlay);
    }

    /// <summary>F12 : montre ou cache le libellé de débogage (FPS, tick, golfeurs), à jour dès qu'il paraît.</summary>
    public void ToggleDebug()
    {
        _hud!.Visible = !_hud.Visible;
        RefreshHud();
    }

    /// <summary>Calque d'interface (sa pile de panneaux est déjà là) : barre du bas et tiroir, pastille du trou suivi et des messages, en haut au centre.</summary>
    private void BuildUi()
    {
        var thumbnails = new ThumbnailRenderer { Name = "Thumbnails" };
        AddChild(thumbnails);
        Bar = BottomBar.Create(this, Ui, thumbnails);

        var top = new VBoxContainer { Name = "TopCenter", MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", 8);
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        top.GrowHorizontal = Control.GrowDirection.Both;
        top.OffsetTop = 12f;
        _holePill = UiPill.Create(UiIds.Hud.Hole, "", "flag", UiTheme.Flag);
        _holePill.Label.ThemeTypeVariation = "UiName";
        _holePill.Label.AddThemeFontSizeOverride("font_size", 18);
        _holePill.Visible = false;
        _holePill.MouseFilter = Control.MouseFilterEnum.Ignore;
        top.AddChild(_holePill);
        _messagePill = UiPill.Create(UiIds.Hud.Message, "", "information", UiTheme.Sky);
        _messagePill.Visible = false;
        _messagePill.MouseFilter = Control.MouseFilterEnum.Ignore;
        top.AddChild(_messagePill);
        Ui.AddChild(top);
    }

    /// <summary>F5 : écrit l'emplacement rapide. Rend faux (et le dit à l'écran) si la sauvegarde n'a pas eu lieu.</summary>
    public bool QuickSave()
    {
        if (InTutorial)
        {
            Birdie.Post(Birdie.Pro, "Pas de sauvegarde pendant la leçon : on finit le trou d'abord.", "#tutoriel");
            return false;
        }

        try
        {
            SaveSlots.Write(SaveSlots.QuickPath, SaveSlots.Serialize(World, out var ms));
            GD.Print($"SAVE rapide serialisation={ms:F2} ms tick={World.Tick}");
            Birdie.Post(Birdie.Club, "Partie sauvegardée. Le registre du club est à jour.", "#sauvegarde");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Birdie.Post(Birdie.Club, $"Sauvegarde impossible : {e.Message}", "#sauvegarde");
            return false;
        }
    }

    /// <summary>F8 : recharge le plus récent des deux emplacements par la scène ; en cas d'erreur le jeu continue.</summary>
    public void QuickLoad()
    {
        string? error = null;
        foreach (var path in SaveSlots.ByRecency())
        {
            // Le plus récent est illisible : on essaie l'autre avant de renoncer.
            if ((error = TryLoad(path)) is null)
            {
                _carryPlay = (_speedIndex, Paused);
                GetTree().ReloadCurrentScene();
                return;
            }
        }

        Birdie.Post(Birdie.Club, error ?? "Aucune sauvegarde à charger pour l'instant.", "#sauvegarde");
    }

    /// <summary>Lit une sauvegarde et la garde pour le prochain <see cref="_Ready"/> ; rend le message d'erreur, ou null si tout va bien.</summary>
    public static string? TryLoad(string path)
    {
        try
        {
            _pendingWorld = SaveSlots.Read(path);
            GD.Print($"LOAD {path} tick={_pendingWorld.Tick}");
            _carryMessage = "Partie chargée. Bon retour au club !";
            return null;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            GD.PrintErr($"Chargement de {path} : {e.Message}");
            return $"Chargement impossible : {e.Message}";
        }
    }

    /// <summary>Sauvegarde automatique au changement de jour : octets en mémoire ici, écriture du fichier en fond.</summary>
    private void AutoSave()
    {
        if (FpsBench.Active || InTutorial)
        {
            return;
        }

        var bytes = SaveSlots.Serialize(World, out var ms);
        GD.Print($"SAVE auto serialisation={ms:F2} ms octets={bytes.Length} jour={World.DayIndex}");
        SaveSlots.WriteInBackground(SaveSlots.AutoPath, bytes);
    }

    private void HandleCameraAction(InputEvent @event)
    {
        if (@event.IsActionPressed("camera_rotate_left"))
        {
            Camera.RotateQuarter(-1);
        }
        else if (@event.IsActionPressed("camera_rotate_right"))
        {
            Camera.RotateQuarter(1);
        }
        else if (@event.IsActionPressed("camera_home"))
        {
            CameraHome();
        }
        else if (@event.IsActionPressed("cam_next_hole"))
        {
            CameraStepHole(1);
        }
        else if (@event.IsActionPressed("cam_prev_hole"))
        {
            CameraStepHole(-1);
        }
        else if (@event.IsActionPressed("camera_toggle_mode"))
        {
            Camera.ToggleMode();
        }
    }

    private float TerrainHeight(float x, float z) => World.Terrain is { } terrain ? (float)terrain.Sample(x, z) : 0f;

    /// <summary>Centre la caméra sur le départ du trou 1 et arrête le suivi (touche Début).</summary>
    public void CameraHome() => CameraGoToHole(1);

    /// <summary>Centre la caméra sur le départ du trou de ce numéro (le premier si le numéro est hors parcours) et arrête le suivi.</summary>
    public void CameraGoToHole(int number)
    {
        if (World.Terrain is not { } terrain)
        {
            return;
        }

        _cameraHole = number >= 1 && number <= terrain.Course.Count ? number : 1;
        var tee = terrain.Course[_cameraHole].Tee;
        Camera.CenterOn(new Vector3((float)tee.X, 0f, (float)tee.Z));
    }

    /// <summary>Trou suivant (<paramref name="step"/> = 1) ou précédent (-1), en bouclant (Page suivante, Page précédente).</summary>
    public void CameraStepHole(int step)
    {
        var count = World.Terrain?.Course.Count ?? 1;
        CameraGoToHole(((_cameraHole - 1 + step + count) % count) + 1);
    }

    /// <summary>Numéro du trou en cours du groupe suivi par la caméra, ou null si aucun groupe n'est suivi.</summary>
    public int? FollowedHole()
    {
        if (Camera.FollowedId is not { } id)
        {
            return null;
        }

        foreach (var g in _current.Golfers)
        {
            if (g.Id == id)
            {
                return World.Groups.FirstOrDefault(group => group.Id == g.GroupId)?.Hole;
            }
        }

        return null;
    }

    /// <summary>Suit le golfeur du membre <paramref name="memberId"/> de la ligue ; faux s'il n'est pas sur le parcours.</summary>
    public bool FollowMember(int memberId)
    {
        var golfer = World.Groups.SelectMany(group => group.Golfers).FirstOrDefault(g => g.MemberId == memberId);
        Camera.Follow(golfer?.Id ?? Camera.FollowedId);
        return golfer is not null;
    }

    /// <summary>Suit le golfeur suivant sur le parcours, par id croissant (touche Tab).</summary>
    public void CameraFollowNext() =>
        Camera.Follow(CameraRig.NextGolfer(_current.Golfers.Select(g => g.Id), Camera.FollowedId));

    /// <summary>Position du golfeur au dernier instantané lu par le rendu, ou null s'il a quitté le parcours.</summary>
    public Vector3? GolferPosition(int id)
    {
        foreach (var g in _current.Golfers)
        {
            if (g.Id == id)
            {
                return new Vector3((float)g.Position.X, 0f, (float)g.Position.Z);
            }
        }

        return null;
    }

    /// <summary>Commande de debug (F10) : saute au 1er du mois suivant ; les arbres poussent et la couche les redessine.</summary>
    public void SkipToNextMonth()
    {
        World.SkipToNextMonth();
        OnDayChanged();
    }

    /// <summary>Nouveau jour : le feuillage avance ; un nouveau mois fait aussi pousser les arbres.</summary>
    private void OnDayChanged()
    {
        _day = World.DayIndex;
        AutoSave();
        _league?.OnDayChanged();
        var newMonth = World.Date.Month != _month;
        _month = World.Date.Month;
        // Un changement de feuillage refait déjà tous les blocs, croissance comprise.
        if (TerrainView.SetDate(World.Date) == 0 && newMonth)
        {
            TerrainView.Trees?.Regrow();
        }
    }

    private void StepOnce()
    {
        (_previous, _current) = (_current, _previous);
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        World.Step();
        if (World.DayIndex != _day)
        {
            OnDayChanged();
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
        _ratingJob?.Touch();
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

            // Relief retouché sur le bord de la carte : l'horizon repart de la nouvelle hauteur du bord (quelques ms, rare).
            var grid = World.Terrain!;
            if (rect.Position.X <= 0 || rect.Position.Y <= 0 || rect.End.X >= grid.CellsX || rect.End.Y >= grid.CellsZ)
            {
                Horizon.Build(grid);
            }
        }
    }

    private void UpdateSun()
    {
        if (_sun is not null)
        {
            var date = World.Date;
            _sun.Basis = SunPath.At(SunDayFraction ?? date.DayFraction, date.Season);

            // Ombres en cascades seulement en vue libre : en iso, le rendu reste sans ombres portées.
            _sun.ShadowEnabled = Camera.Mode == CameraMode.Free;
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

    /// <summary>Pastilles du haut et libellé de débogage, à 4 Hz au plus (<see cref="RefreshHud"/> force la mise à jour).</summary>
    private void UpdateHud(double delta)
    {
        _hudSince += delta;
        if (_hudSince >= 0.25)
        {
            RefreshHud();
        }
    }

    public void RefreshHud()
    {
        _hudSince = 0;
        if (_holePill is not null)
        {
            if (FollowedHole() is { } n && World.Terrain is { } t && n <= t.Course.Count)
            {
                _holePill.SetText($"Trou {n}, par {t.Course[n].Par}");
                _holePill.Visible = true;
            }
            else
            {
                _holePill.Visible = false;
            }
        }

        _messagePill?.SetVisible(Time.GetTicksMsec() < _messageUntil);
        if (_hud is { Visible: true })
        {
            _hud.Text = $"{Engine.GetFramesPerSecond()} FPS | {Actors.VisibleGolfers} golfeurs | tick {World.Tick} | x{Speed}{(Paused ? " (pause)" : string.Empty)} | {DateText()} | solde {Golferoo.Sim.Economy.Ledger.Dollars(World.Ledger.Balance)}\n{WeatherText()} | {StimpText()} | {RatingText()}{(_league is null ? string.Empty : $"\n{_league.ProText()}")}{(Time.GetTicksMsec() < _messageUntil ? "\n" + _message : string.Empty)}";
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
            ? $"Note {LeaguePanel.Decimal(r.Scratch18)} / slope {r.Slope}"
            : World.Terrain!.Blank ? "pas de ligue sur la terre vierge" : "Note : calcul en cours";

    private string WeatherText()
    {
        var w = World.Wind;
        var kmh = Math.Sqrt((w.X * w.X) + (w.Z * w.Z)) * 3.6;
        var from = Math.Atan2(-w.X, w.Z); // direction d'où il vient, comptée depuis le nord (−Z) vers l'est (+X)
        var octant = ((int)Math.Round(from / (Math.PI / 4.0)) + 8) % 8;
        var forecast = string.Join(", ", Enumerable.Range(1, 3).Select(d => UiNames.Skies[(int)World.Forecast(d)]));
        return $"{UiNames.Skies[(int)World.Weather.Sky]}, vent {kmh:0} km/h {CompassNames[octant]} | prévision : {forecast}";
    }

    private string DateText()
    {
        var d = World.Date;
        return $"{d.Day} {UiNames.Months[d.Month - 1]}, an {d.Year}, {UiNames.Seasons[(int)d.Season]}";
    }
}
