using Godot;

namespace Golferoo.Game.Tools;

/// <summary>
/// Mode mesure (`-- --fps-bench`) : 1920x1080, vsync coupé, 200 golfeurs, 5 s de chauffe puis 30 s de mesure.
/// Affiche le résultat, l'écrit dans fps-result.txt à côté de l'exécutable et quitte 10 s plus tard.
/// </summary>
public partial class FpsBench : Node
{
    private const double WarmupSeconds = 5;
    private const double MeasureSeconds = 30;
    private const double ShowSeconds = 10;

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
            $"Golferoo - mesure FPS\n" +
            $"FPS moyen : {avg:F1}\n" +
            $"1 % bas : {1.0 / p99:F1} FPS (pire image {sorted[^1] * 1000:F1} ms, p99 {p99 * 1000:F1} ms)\n" +
            $"Tick de simulation moyen : {tickMs:F3} ms\n" +
            $"Golfeurs : {_main.Actors.VisibleGolfers}\n" +
            $"Resolution : {size.X:F0}x{size.Y:F0}, facteur {_main.Factor}\n" +
            $"Carte graphique : {RenderingServer.GetVideoAdapterName()}\n" +
            $"Version : {version}\n" +
            $"Images mesurees : {_frames.Count} en {total:F1} s\n";
        _label.Text = text + "\nFermeture dans 10 s.";
        GD.Print($"FPSBENCH avg={avg:F1} low1={1.0 / p99:F1} tickMs={tickMs:F3} golfers={_main.Actors.VisibleGolfers} res={size.X:F0}x{size.Y:F0} factor={_main.Factor} gpu=\"{RenderingServer.GetVideoAdapterName()}\" version={version}");

        var dir = System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".";
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "fps-result.txt"), text);
        }
        catch (Exception e)
        {
            GD.PrintErr($"FPSBENCH écriture impossible : {e.Message}");
        }
    }
}
