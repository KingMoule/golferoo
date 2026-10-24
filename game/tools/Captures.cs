using Godot;

namespace Golferoo.Game.Tools;

/// <summary>
/// Produit 8 captures (4 rotations x 2 zooms : ensemble et green) du trou avec une paire de golfeurs, graine fixe.
/// Usage : godot --path game res://tools/Captures.tscn -- --out dossier [--factor n]
/// Code de sortie 0 si les 8 images existent, ont la même taille et ne sont pas uniformes ; non nul sinon.
/// </summary>
public partial class Captures : Node
{
    private const int WarmupTicks = 400;
    private const int SettleFrames = 6;

    private static readonly float[] Yaws = [45f, 135f, 225f, 315f];

    public override async void _Ready()
    {
        var code = 1;
        try
        {
            code = await Run();
        }
        catch (Exception e)
        {
            GD.PrintErr($"CAPTURES FAIL exception {e}");
        }

        GetTree().Quit(code);
    }

    private async Task<int> Run()
    {
        var outDir = Main.CommandLineOption("--out") ?? "captures";
        System.IO.Directory.CreateDirectory(outDir);

        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        main.SpawnGroup();
        main.StepTicks(WarmupTicks);

        var grid = main.World.Terrain!;
        var width = (float)(grid.CellsX * grid.CellSize);
        var depth = (float)(grid.CellsZ * grid.CellSize);
        var center = new Vector3(width / 2f, 0f, depth / 2f);
        var green = new Vector3((float)grid.Green.X, 0f, (float)grid.Green.Z);

        var failures = 0;
        Vector2I? size = null;
        var index = 0;
        foreach (var (zoomName, viewSize, target) in new[] { ("overview", 240f, center), ("green", 30f, green) })
        {
            foreach (var yaw in Yaws)
            {
                main.Camera.SetView(yaw, viewSize, target);
                for (var f = 0; f < SettleFrames; f++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                var image = GetViewport().GetTexture().GetImage();
                var path = System.IO.Path.Combine(outDir, $"capture-{index:00}-{zoomName}-{(int)yaw}.png");
                index++;
                var stddev = LuminanceStdDev(image);
                var err = image.SavePng(path);
                GD.Print($"CAPTURE {path} {image.GetWidth()}x{image.GetHeight()} stddev={stddev:F4} save={err}");
                if (err != Error.Ok || stddev <= 0.0001)
                {
                    GD.PrintErr($"CAPTURES FAIL {path} (écriture ou image uniforme)");
                    failures++;
                }

                size ??= image.GetSize();
                if (image.GetSize() != size)
                {
                    GD.PrintErr($"CAPTURES FAIL {path} taille différente");
                    failures++;
                }
            }
        }

        if (failures == 0 && index == 8)
        {
            GD.Print($"CAPTURES OK {index} images");
            return 0;
        }

        return 1;
    }

    private static double LuminanceStdDev(Image image)
    {
        double sum = 0;
        double sum2 = 0;
        var n = 0;
        var w = image.GetWidth();
        var h = image.GetHeight();
        for (var y = 0; y < h; y += 2)
        {
            for (var x = 0; x < w; x += 2)
            {
                var c = image.GetPixel(x, y);
                double l = (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B);
                sum += l;
                sum2 += l * l;
                n++;
            }
        }

        var mean = sum / Math.Max(n, 1);
        return Math.Sqrt(Math.Max((sum2 / Math.Max(n, 1)) - (mean * mean), 0));
    }
}
