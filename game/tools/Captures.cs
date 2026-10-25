using Godot;
using Golferoo.Game.Rendering;

namespace Golferoo.Game.Tools;

/// <summary>
/// Produit 10 captures du trou avec une paire de golfeurs, graine fixe : 4 rotations x 2 zooms (ensemble et green),
/// puis 2 vues proches d'un golfeur à côté d'un arbre mature (la balle suivie sert de point focal : couronnes tramées).
/// Usage : godot --path game res://tools/Captures.tscn -- --out dossier [--factor n] [--prefix texte] [--thumbs] [--full]
/// Code de sortie 0 si les 10 images existent, ont la même taille et ne sont pas uniformes ; non nul sinon.
/// </summary>
public partial class Captures : Node
{
    private const int WarmupTicks = 400;
    private const int SettleFrames = 6;
    private const float CloseUpSize = 36f;
    private const float CloseUpDistance = 20f;
    private const int CloseUpMaxSteps = 300;

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
        var thumbs = OS.GetCmdlineUserArgs().Contains("--thumbs");
        var full = OS.GetCmdlineUserArgs().Contains("--full");
        var prefix = Main.CommandLineOption("--prefix") ?? "";
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

        // Les points focaux viennent des captures (un golfeur), pas de la souris.
        main.Brush.SetProcess(false);
        main.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, null, 0f);

        if (main.TerrainView.Trees is { } layer)
        {
            var (n, h, c) = layer.RenderedStats();
            GD.Print($"TREES n={n} h_moy={h:F1} couronne_moy={c:F1}");
        }

        var failures = 0;
        Vector2I? size = null;
        var index = 0;
        var views = new List<(string Name, float Size, Vector3 Target, float[] Yaws, Vector3? Followed)>
        {
            ("overview", 240f, center, Yaws, null),
            ("green", 30f, green, Yaws, null),
        };
        views.Add(("closeup", CloseUpSize, center, [45f, 225f], null));

        foreach (var (zoomName, viewSize, target0, yaws0, followed0) in views)
        {
            var (target, yaws, followed) = (target0, yaws0, followed0);
            if (zoomName == "closeup")
            {
                if (await FindCloseUp(main) is not { } close)
                {
                    GD.PrintErr("CAPTURES FAIL aucune vue proche (pas de golfeur ou d'arbre mature)");
                    failures++;
                    continue;
                }

                (target, yaws, followed) = (close.Target, close.Yaws, close.Golfer);
            }

            main.TerrainView.Trees?.SetTrackedBall(followed);
            foreach (var yaw in yaws)
            {
                main.Camera.SetView(yaw, viewSize, target);
                for (var f = 0; f < SettleFrames; f++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                var image = GetViewport().GetTexture().GetImage();
                var path = System.IO.Path.Combine(outDir, $"{prefix}capture-{index:00}-{zoomName}-{(int)yaw}.png");
                index++;
                var stddev = LuminanceStdDev(image);
                var err = image.SavePng(path);
                GD.Print($"CAPTURE {path} {image.GetWidth()}x{image.GetHeight()} stddev={stddev:F4} save={err}");
                if (thumbs)
                {
                    // Vignette JPEG en base64 dans le journal : on peut regarder les captures sans télécharger l'artefact.
                    var thumb = (Image)image.Duplicate();
                    thumb.Resize(384, 216, Image.Interpolation.Bilinear);
                    GD.Print($"THUMB {System.IO.Path.GetFileName(path)} {Convert.ToBase64String(thumb.SaveJpgToBuffer(0.7f))}");
                }

                if (full)
                {
                    // Taille réelle, JPEG qualité 85 : lisible sans télécharger l'artefact.
                    GD.Print($"FULL {System.IO.Path.GetFileName(path)} {Convert.ToBase64String(image.SaveJpgToBuffer(0.85f))}");
                }

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

        if (failures == 0 && index == 10)
        {
            GD.Print($"CAPTURES OK {index} images");
            return 0;
        }

        return 1;
    }

    /// <summary>
    /// Vue proche : fait avancer la simulation (au plus <see cref="CloseUpMaxSteps"/> pas de 20 ticks) jusqu'à ce qu'un golfeur
    /// soit à <see cref="CloseUpDistance"/> mètres au plus d'un arbre mature (12 m ou plus), sinon prend le couple le plus proche.
    /// La cible est entre eux, le golfeur est le point focal ; deux angles : l'arbre entre la caméra et le golfeur (couronne
    /// tramée), puis l'opposé. Null s'il n'y a pas de golfeur ou d'arbre mature.
    /// </summary>
    private async Task<(Vector3 Target, float[] Yaws, Vector3 Golfer)?> FindCloseUp(Main main)
    {
        var grid = main.World.Terrain!;
        Golferoo.Sim.Trees.Tree? best = null;
        var bestPos = default(Vector3);
        var bestDistance = double.MaxValue;
        for (var step = 0; step <= CloseUpMaxSteps; step++)
        {
            bestDistance = double.MaxValue;
            foreach (var g in main.World.Snapshot().Golfers)
            {
                foreach (var t in main.World.Trees.Trees)
                {
                    if (t.Height() < 12.0)
                    {
                        continue;
                    }

                    var d = Math.Sqrt(Math.Pow(t.X - g.Position.X, 2) + Math.Pow(t.Z - g.Position.Z, 2));
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = t;
                        bestPos = new Vector3((float)g.Position.X, (float)grid.Sample(g.Position.X, g.Position.Z), (float)g.Position.Z);
                    }
                }
            }

            if (bestDistance <= CloseUpDistance || step == CloseUpMaxSteps)
            {
                break;
            }

            main.StepTicks(20);
        }

        if (best is not { } tree)
        {
            return null;
        }

        GD.Print($"CLOSEUP tick={main.World.Tick} distance golfeur-arbre={bestDistance:F1} m, arbre {tree.Height():F1} m");
        var treeBase = new Vector3((float)tree.X, (float)grid.Sample(tree.X, tree.Z), (float)tree.Z);
        var target = (treeBase + bestPos) * 0.5f;
        target.Y = Math.Max(treeBase.Y, bestPos.Y) + 3f;
        var toTree = new Vector2(treeBase.X - bestPos.X, treeBase.Z - bestPos.Z).Normalized();
        float bestDot = float.MinValue, worstDot = float.MaxValue, bestYaw = 45f, worstYaw = 225f;
        for (var yaw = 0f; yaw < 360f; yaw += 45f)
        {
            main.Camera.SetView(yaw, CloseUpSize, target);
            var cam = main.Camera.View!.GlobalPosition;
            var dot = new Vector2(cam.X - target.X, cam.Z - target.Z).Normalized().Dot(toTree);
            if (dot > bestDot)
            {
                (bestDot, bestYaw) = (dot, yaw);
            }

            if (dot < worstDot)
            {
                (worstDot, worstYaw) = (dot, yaw);
            }
        }

        await Task.CompletedTask;
        return (target, [bestYaw, worstYaw], bestPos);
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
