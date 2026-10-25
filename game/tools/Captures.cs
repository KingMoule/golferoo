using Godot;
using Golferoo.Game.Rendering;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;

namespace Golferoo.Game.Tools;

/// <summary>
/// Produit 27 captures du trou avec une paire de golfeurs, graine fixe : 4 rotations x 2 zooms (ensemble et green),
/// puis 2 vues proches d'un golfeur à côté d'un arbre mature (la balle suivie sert de point focal : couronnes tramées),
/// une vue d'ensemble après 15 ans, puis un défrichage replanté (6 à 10 jeunes plants et une motte au milieu du sous-bois rasé),
/// vu de près, puis vingt ans plus tard avec la motte tombée en tronc couché, le soleil le matin, à midi et le soir,
/// le trou aux quatre saisons (feuillage et teinte de l'herbe), puis sous une forte pluie.
/// Usage : godot --path game res://tools/Captures.tscn -- --out dossier [--factor n] [--prefix texte] [--thumbs] [--full]
/// Code de sortie 0 si toutes les images existent, ont la même taille et ne sont pas uniformes ; non nul sinon.
/// </summary>
public partial class Captures : Node
{
    private const int WarmupTicks = 400;
    private const int SettleFrames = 6;
    private const float CloseUpSize = 36f;
    private const float CloseUpDistance = 20f;
    private const int CloseUpMaxSteps = 300;
    private const float ClearingSize = 40f;
    private const double ClearingRadius = 12.0;
    private const int FallWaitFrames = 300;
    private const float TurfSize = 100f;

    private int? _clearingMound;

    private static readonly float[] Yaws = [45f, 135f, 225f, 315f];

    /// <summary>Vues des saisons, dans l'ordre de <c>Season</c>.</summary>
    private static readonly string[] SeasonViews = ["season-spring", "season-summer", "season-autumn", "season-winter"];
    private const float SeasonSize = 90f;

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
            GD.Print($"TREES avant n={n} h_moy={h:F1} couronne_moy={c:F1} age_moy={main.World.Trees.MeanAgeMonths / 12.0:F1}");
        }

        var failures = 0;
        var clearingTarget = center;
        Vector2I? size = null;
        var index = 0;
        var mid = new Vector3((float)(grid.Tee.X + grid.Green.X) / 2f, 0f, (float)(grid.Tee.Z + grid.Green.Z) / 2f);
        var views = new List<(string Name, float Size, Vector3 Target, float[] Yaws, Vector3? Followed)>
        {
            // Le gazon d'abord (critère 4 de C05) : consignes normales, puis greens rapides et rough épais, une semaine de jeu, la surcouche.
            ("turf-normal", TurfSize, mid, [Yaws[0]], null),
            ("turf-green-normal", 30f, green, [Yaws[0]], null),
            ("turf-fast-thick", TurfSize, mid, [Yaws[0]], null),
            ("turf-week", TurfSize, mid, [Yaws[0]], null),
            ("turf-week-green", 30f, green, [Yaws[0]], null),
            ("turf-overlay", TurfSize, mid, [Yaws[0]], null),
            ("overview", 240f, center, Yaws, null),
            ("green", 30f, green, Yaws, null),
        };
        views.Add(("closeup", CloseUpSize, center, [45f, 225f], null));
        views.Add(("aged15", 240f, center, [Yaws[0]], null));
        views.Add(("clearing", ClearingSize, center, [Yaws[0]], null));
        views.Add(("clearing20", ClearingSize, center, [Yaws[0]], null));
        views.Add(("sun-morning", 240f, center, [Yaws[0]], null));
        views.Add(("sun-noon", 240f, center, [Yaws[0]], null));
        views.Add(("sun-evening", 240f, center, [Yaws[0]], null));
        foreach (var season in SeasonViews)
        {
            views.Add((season, SeasonSize, center, [Yaws[0]], null));
        }

        views.Add(("rain", SeasonSize, center, [Yaws[0]], null));

        var expected = views.Sum(v => v.Yaws.Length);
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

            if (zoomName == "turf-fast-thick")
            {
                // Greens rapides, rough épais : deux semaines de croissance au printemps (la tonte ne fait que baisser).
                main.World.SetTurfSettings(new TurfSettings(GreenSpeed.Fast, FairwayFirmness.Normal, RoughLength.Thick));
                main.StepTicks(14 * Calendar.TicksPerDay);
            }

            if (zoomName == "turf-week")
            {
                // Une semaine de jeu : 200 marcheurs, et une paire de golfeurs sur le trou toutes les 2 000 ticks (mottes, marques).
                main.ToggleLoadMode();
                for (var i = 0; i < 7 * Calendar.TicksPerDay / 2000; i++)
                {
                    main.SpawnGroup();
                    main.StepTicks(2000);
                }

                main.ToggleLoadMode();
            }

            // Surcouche G seulement sur sa vue ; les autres vues repartent en consignes normales.
            main.TerrainView.SetTurfOverlay(zoomName == "turf-overlay");
            if (zoomName == "overview")
            {
                main.World.SetTurfSettings(TurfSettings.Normal);
            }

            if (zoomName == "aged15")
            {
                // Quinze ans d'arbre de plus, soit 45 mois de jeu (commande de debug F10) : les plants ont grandi, les bouleaux sont adultes.
                for (var month = 0; month < 15 * 3; month++)
                {
                    main.SkipToNextMonth();
                }

                if (main.TerrainView.Trees is { } aged)
                {
                    var (n, h, c) = aged.RenderedStats();
                    GD.Print($"TREES après n={n} h_moy={h:F1} couronne_moy={c:F1} age_moy={main.World.Trees.MeanAgeMonths / 12.0:F1}");
                }
            }

            if (zoomName == "clearing")
            {
                if (ReplantClearing(main) is not { } spot)
                {
                    GD.PrintErr("CAPTURES FAIL aucun sous-bois à défricher");
                    failures++;
                    continue;
                }

                target = spot;
                clearingTarget = spot;
            }

            if (zoomName == "clearing20")
            {
                // Vingt ans d'arbre plus tard (60 mois de jeu) : la motte est un grand arbre isolé, qu'on fait tomber ; on attend la fin de la chute.
                for (var month = 0; month < 20 * 3; month++)
                {
                    main.SkipToNextMonth();
                }

                target = clearingTarget;
                if (_clearingMound is { } mound)
                {
                    main.World.FellTree(mound, Math.PI / 3.0);
                    main.StepTicks(1);
                    for (var f = 0; f < FallWaitFrames && main.TerrainView.Trees is { FallingCount: > 0 }; f++)
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }

                    GD.Print($"CLEARING20 {main.World.Trees.Count} arbres, tombés {main.TerrainView.Trees?.FallenCount}, chutes en cours {main.TerrainView.Trees?.FallingCount}, dépenses {main.World.TreeSpending}");
                }
            }

            if (Array.IndexOf(SeasonViews, zoomName) is var s and >= 0)
            {
                // Mois suivants (F10) jusqu'à la saison voulue : feuillage et teinte de l'herbe changent.
                for (var month = 0; month < 12 && (int)main.World.Date.Season != s; month++)
                {
                    main.SkipToNextMonth();
                }

                GD.Print($"SEASON {zoomName} {main.World.Date.Month}/{main.World.Date.Year} saison={main.World.Date.Season}");
            }

            if (zoomName == "rain")
            {
                // Forte pluie imposée (F9) : ciel gris, gouttes, sol plus sombre.
                while (main.World.Weather.Sky != Golferoo.Sim.Weather.Sky.HeavyRain)
                {
                    main.World.CycleSky();
                }
            }

            main.SunDayFraction = zoomName switch
            {
                "sun-morning" => 0.1,
                "sun-noon" => 0.5,
                "sun-evening" => 0.9,
                _ => 0.5, // midi : les autres vues gardent un éclairage comparable à C02
            };
            main.TerrainView.Trees?.SetTrackedBall(followed);
            main.TerrainView.RefreshTurf();
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

        if (failures == 0 && index == expected)
        {
            GD.Print($"CAPTURES OK {index} images");
            return 0;
        }

        return 1;
    }

    /// <summary>
    /// Rase (rayon <see cref="ClearingRadius"/>) le sous-bois le plus proche du centre de la carte, y plante une motte au milieu
    /// et 8 jeunes plants mélangés (chêne, bouleau, sapin) sur deux couronnes, et rend le point visé ; null sans sous-bois.
    /// </summary>
    private Vector3? ReplantClearing(Main main)
    {
        var world = main.World;
        var grid = world.Terrain!;
        var mid = new Point2(grid.CellsX * grid.CellSize * 0.5, grid.CellsZ * grid.CellSize * 0.5);
        Point2? best = null;
        var bestDistance = double.MaxValue;
        for (var cz = 0; cz < grid.CellsZ; cz++)
        {
            for (var cx = 0; cx < grid.CellsX; cx++)
            {
                if (grid.GetCellSurface(cx, cz) != Surface.Trees)
                {
                    continue;
                }

                var c = grid.CellCenter(cx, cz);
                var d = ((c.X - mid.X) * (c.X - mid.X)) + ((c.Z - mid.Z) * (c.Z - mid.Z));
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }
        }

        if (best is not { } centre)
        {
            return null;
        }

        var before = world.Trees.Count;
        world.RazeArea(centre, ClearingRadius, keepRemarkable: false);
        main.StepTicks(1);
        var species = new[] { Golferoo.Sim.Trees.TreeSpeciesId.Oak, Golferoo.Sim.Trees.TreeSpeciesId.Birch, Golferoo.Sim.Trees.TreeSpeciesId.Fir };
        world.PlantTree(centre.X, centre.Z, Golferoo.Sim.Trees.TreeSpeciesId.Oak, TreePlantStage.Mound);
        for (var i = 0; i < 8; i++)
        {
            var ring = i % 2 == 0 ? 5.0 : 9.0;
            var a = i * Math.PI / 4.0;
            world.PlantTree(centre.X + (Math.Cos(a) * ring), centre.Z + (Math.Sin(a) * ring), species[i % 3], TreePlantStage.Sapling);
        }

        main.StepTicks(1);
        _clearingMound = null;
        foreach (var t in world.Trees.Trees)
        {
            if (t.AgeMonths is >= 96 and <= 120 && Math.Abs(t.X - centre.X) < 0.01 && Math.Abs(t.Z - centre.Z) < 0.01)
            {
                _clearingMound = t.Id;
            }
        }

        GD.Print($"CLEARING centre=({centre.X:F0},{centre.Z:F0}) arbres {before} puis {world.Trees.Count}, refusées {world.TreeCommandsRefused}, dépenses {world.TreeSpending}");
        return new Vector3((float)centre.X, (float)grid.Sample(centre.X, centre.Z) + 2f, (float)centre.Z);
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
