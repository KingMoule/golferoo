using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Game.Tools;

/// <summary>
/// Mode mesure (`-- --fps-bench`) : 1920x1080, vsync coupé, 200 golfeurs, 5 s de chauffe puis 30 s de mesure.
/// Affiche le résultat, l'écrit dans fps-result.txt à côté de l'exécutable et quitte 10 s plus tard.
/// `-- --fps-bench-low` : même chargement, mais en caméra libre rasante qui longe le parcours (fps-result-low.txt).
/// </summary>
public partial class FpsBench : Node
{
    private const double WarmupSeconds = 5;
    private const double MeasureSeconds = 30;
    private const double ShowSeconds = 10;

    /// <summary>Vrai si l'une des deux mesures est demandée (même trou, mêmes arbres, pas d'interface de jeu).</summary>
    public static bool Active => Low || OS.GetCmdlineUserArgs().Contains("--fps-bench");

    /// <summary>Vrai pour la mesure en vue rasante.</summary>
    public static bool Low => OS.GetCmdlineUserArgs().Contains("--fps-bench-low");

    private const float LowPitchDegrees = 8f;
    private const float LowDistance = 15f;
    private const float LowSpeed = 12f;

    /// <summary>
    /// Cible de la vue rasante après <paramref name="seconds"/> : va du départ au green à vitesse constante puis revient (aller-retour).
    /// À 8° et 15 m de la cible, la caméra est à environ 2 m du sol.
    /// </summary>
    public static Vector3 LowTarget(Vector2 tee, Vector2 green, double seconds)
    {
        var length = tee.DistanceTo(green);
        var travelled = (float)(seconds * LowSpeed) % (2f * length);
        var t = (travelled <= length ? travelled : (2f * length) - travelled) / Math.Max(length, 0.001f);
        var p = tee.Lerp(green, t);
        return new Vector3(p.X, 0f, p.Y);
    }

    /// <summary>Place la caméra libre rasante à l'instant donné, regard vers le green.</summary>
    public static void PlaceLow(CameraRig camera, Vector2 tee, Vector2 green, double seconds)
    {
        var target = LowTarget(tee, green, seconds);
        var dir = (green - tee).Normalized();
        camera.SetFreeView(Mathf.RadToDeg(Mathf.Atan2(-dir.X, -dir.Y)), LowPitchDegrees, LowDistance, target);
    }

    /// <summary>
    /// Nombre d'arbres visé par la mesure : 5 000 à l'échelle réelle (critère 7 de specs/C02-arbres-echelle.md) ; agrandis,
    /// leurs couronnes couvrent autant de terrain avec SizeScale² fois moins d'arbres.
    /// </summary>
    public const int BenchTrees = (int)(5000 / (TreeSpeciesTable.SizeScale * TreeSpeciesTable.SizeScale));

    /// <summary>
    /// Complète le boisement jusqu'à <see cref="BenchTrees"/> arbres adultes, en grille sur toute la carte et sans
    /// règle d'espacement : la mesure porte sur le coût de rendu, pas sur un boisement crédible.
    /// </summary>
    public static void FillTrees(TreeStand trees, TerrainGrid grid)
    {
        var missing = BenchTrees - trees.Count;
        if (missing <= 0)
        {
            return;
        }

        var width = grid.CellsX * grid.CellSize;
        var depth = grid.CellsZ * grid.CellSize;
        var step = Math.Sqrt(width * depth / missing);
        var cols = Math.Max(1, (int)(width / step));
        for (var i = 0; i < missing; i++)
        {
            var x = ((i % cols) + 0.5) * width / cols;
            var z = ((i / cols) + 0.5) * step;
            trees.Add(x, Math.Min(z, depth - 0.5), (TreeSpeciesId)(i % 3), 40 * 12);
        }
    }

    private readonly List<double> _frames = [];
    private Main _main = null!;
    private Label _label = null!;
    private double _elapsed;
    private bool _done;
    private double _tickMsStart;
    private long _tickCountStart;
    private bool _started;

    public override void _Ready()
    {
        _main = GetParent<Main>();
        var window = GetWindow();
        window.Mode = Window.ModeEnum.Windowed;
        window.Borderless = true;
        window.Size = new Vector2I(1920, 1080);
        window.Position = Vector2I.Zero;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        _main.ToggleLoadMode();

        _label = new Label { Position = new Vector2(40, 100) };
        _label.AddThemeFontSizeOverride("font_size", 36);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 8);
        var layer = new CanvasLayer { Layer = 10 };
        layer.AddChild(_label);
        AddChild(layer);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (Low && !_done)
        {
            var terrain = _main.World.Terrain!;
            PlaceLow(_main.Camera, new Vector2((float)terrain.Tee.X, (float)terrain.Tee.Z), new Vector2((float)terrain.Green.X, (float)terrain.Green.Z), _elapsed);
        }

        if (_done)
        {
            if (_elapsed >= WarmupSeconds + MeasureSeconds + ShowSeconds)
            {
                GetTree().Quit(0);
            }

            return;
        }

        if (_elapsed < WarmupSeconds)
        {
            _label.Text = $"Mesure FPS : chauffe {WarmupSeconds - _elapsed:F0} s ({_main.Actors.VisibleGolfers} golfeurs)";
            return;
        }

        if (!_started)
        {
            _started = true;
            _tickMsStart = _main.TickMsTotal;
            _tickCountStart = _main.TickCount;
        }

        _frames.Add(delta);
        var left = WarmupSeconds + MeasureSeconds - _elapsed;
        if (left > 0)
        {
            _label.Text = $"Mesure FPS en cours : {left:F0} s\n{Engine.GetFramesPerSecond()} FPS";
            return;
        }

        Finish();
    }

    private void Finish()
    {
        _done = true;
        var total = _frames.Sum();
        var avg = _frames.Count / total;
        var sorted = _frames.OrderBy(x => x).ToArray();
        var p99 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.99))];
        var ticks = _main.TickCount - _tickCountStart;
        var tickMs = ticks > 0 ? (_main.TickMsTotal - _tickMsStart) / ticks : 0;
        var size = GetViewport().GetVisibleRect().Size;
        var version = "inconnue";
        if (Godot.FileAccess.FileExists("res://data/version.txt"))
        {
            using var f = Godot.FileAccess.Open("res://data/version.txt", Godot.FileAccess.ModeFlags.Read);
            version = f.GetAsText().Trim();
        }

        var text =
            $"Golferoo - mesure FPS{(Low ? " (vue rasante)" : string.Empty)}\n" +
            $"FPS moyen : {avg:F1}\n" +
            $"1 % bas : {1.0 / p99:F1} FPS (pire image {sorted[^1] * 1000:F1} ms, p99 {p99 * 1000:F1} ms)\n" +
            $"Tick de simulation moyen : {tickMs:F3} ms\n" +
            $"Golfeurs : {_main.Actors.VisibleGolfers}\n" +
            $"Arbres : {_main.World.Trees.Count}\n" +
            $"Resolution : {size.X:F0}x{size.Y:F0}, facteur {_main.Factor}\n" +
            $"Carte graphique : {RenderingServer.GetVideoAdapterName()}\n" +
            $"Version : {version}\n" +
            $"Images mesurees : {_frames.Count} en {total:F1} s\n";
        _label.Text = text + "\nFermeture dans 10 s.";
        GD.Print($"FPSBENCH{(Low ? "-LOW" : string.Empty)} avg={avg:F1} low1={1.0 / p99:F1} tickMs={tickMs:F3} golfers={_main.Actors.VisibleGolfers} trees={_main.World.Trees.Count} res={size.X:F0}x{size.Y:F0} factor={_main.Factor} gpu=\"{RenderingServer.GetVideoAdapterName()}\" version={version}");

        var dir = System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".";
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, Low ? "fps-result-low.txt" : "fps-result.txt"), text);
        }
        catch (Exception e)
        {
            GD.PrintErr($"FPSBENCH écriture impossible : {e.Message}");
        }
    }
}
