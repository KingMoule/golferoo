using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Game.Rendering;
using Golferoo.Game.Ui;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
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

    private const int LeagueViewCount = 5;
    private const int TutorialViewCount = 3;
    private const int ProfileViewCount = 3;
    private const int FreeViewCount = 4;
    private const int SaveViewCount = 2;
    private const int CourseViewCount = 14;
    private const int GalleryViewCount = 1;
    private const int FinanceViewCount = 1;
    private const int ProViewCount = 2;

    /// <summary>Lisière sud de la forêt du coude, la balle de <c>ForestExitTests.Edge</c> (tuile 18,5 / 26,5 de 8 m).</summary>
    private static readonly Point2 ForestEdge = new(18.5 * 8, 26.5 * 8);

    /// <summary>
    /// Approche de 92 m dans le rough (<c>ProfileTests.Approach</c>) : les six archétypes y visent six points
    /// de chute écartés de 3 m au moins, trouvés en balayant le dogleg tous les 4 m.
    /// </summary>
    private static readonly Point2 Approach = new(328, 128);

    /// <summary>Couleur de chaque archétype attribuable dans <c>c09-profils</c> et <c>c09-sortie</c>, dans l'ordre de <see cref="Archetype"/>.</summary>
    private static readonly Color[] ProfileColors =
    [
        new(0.3f, 0.6f, 1f), new(1f, 0.25f, 0.2f), new(1f, 0.9f, 0.2f), new(1f, 0.3f, 1f), Colors.White, new(0.2f, 1f, 0.85f),
    ];

    private int? _clearingMound;
    private string _outDir = string.Empty;
    private string _prefix = string.Empty;
    private bool _thumbs;
    private bool _full;
    private int _index;
    private int _limit;
    private int _failures;
    private Vector2I? _size;

    private static readonly float[] Yaws = [45f, 135f, 225f, 315f];

    /// <summary>Vues des saisons, dans l'ordre de <c>Season</c>.</summary>
    private static readonly string[] SeasonViews = ["season-spring", "season-summer", "season-autumn", "season-winter"];
    private const float SeasonSize = 90f;

    /// <summary>Mois montré par chaque vue de saison : avril, juillet, novembre (feuilles qui tombent), janvier.</summary>
    private static readonly int[] SeasonMonths = [4, 7, 11, 1];

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
        Main.OfferTutorial = false;

        // Les vues avancent des semaines de jeu : sans pavillon, personne n'arrive (seule la vue du pavillon, à la fin, le place).
        Main.ClubhouseOnNewGame = false;

        // Les vues d'avant C12 sont réglées sur le dogleg (lisière, approche) : elles gardent ce trou seul ; « Le neuf » vient à la fin.
        Main.CourseOverride = "data/holes/dogleg-par4.json";
        _outDir = Main.CommandLineOption("--out") ?? "captures";
        _thumbs = OS.GetCmdlineUserArgs().Contains("--thumbs");
        _full = OS.GetCmdlineUserArgs().Contains("--full");
        _prefix = Main.CommandLineOption("--prefix") ?? "";
        _limit = int.TryParse(Main.CommandLineOption("--limit"), out var limit) ? limit : 0;
        System.IO.Directory.CreateDirectory(_outDir);
        if (Main.CommandLineOption("--only") == "course")
        {
            // Mise au point d'un écran de « Le neuf » (c12, c13) sans attendre les vues d'avant : `-- --only course`.
            await CourseViews();
            return _failures == 0 ? 0 : 1;
        }

        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        // Les vues avancent le temps sans joueur : le pro joue avec l'IA, sans attendre son coup.
        main.World.ManualPro = false;
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

        var clearingTarget = center;
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
            ("mower-green", 30f, green, [Yaws[0]], null),
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
                    _failures++;
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

            // Surcouche H seulement sur sa vue ; les autres vues repartent en consignes normales.
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
                    _failures++;
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

                    GD.Print($"CLEARING20 {main.World.Trees.Count} arbres, tombés {main.TerrainView.Trees?.FallenCount}, chutes en cours {main.TerrainView.Trees?.FallingCount}, solde {Golferoo.Sim.Economy.Ledger.Dollars(main.World.Ledger.Balance)}");
                }
            }

            if (Array.IndexOf(SeasonViews, zoomName) is var s and >= 0)
            {
                // Mois suivants (F10) jusqu'au mois de la saison voulue : feuillage et teinte de l'herbe changent.
                for (var month = 0; month < 12 && main.World.Date.Month != SeasonMonths[s]; month++)
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
                "sun-morning" or "mower-green" => 0.1, // le matin : les tondeuses sont dehors (premier quart de la journée)
                "sun-noon" => 0.5,
                "sun-evening" => 0.9,
                _ => 0.5, // midi : les autres vues gardent un éclairage comparable à C02
            };
            main.TerrainView.Trees?.SetTrackedBall(followed);
            main.TerrainView.RefreshTurf();
            if (zoomName == "mower-green")
            {
                // La tondeuse est construite à la première image du matin : on attend, puis on la cadre.
                for (var f = 0; f < 3; f++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                target = main.Mowers.FirstPosition ?? target;
            }

            foreach (var yaw in yaws)
            {
                main.Camera.SetView(yaw, viewSize, target);
                for (var f = 0; f < SettleFrames; f++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                if (zoomName == "mower-green")
                {
                    GD.Print($"MOWERS {main.Mowers.ActiveMowers} premiere={main.Mowers.FirstPosition} cible={target}");
                }

                Save(zoomName + "-" + (int)yaw);
            }
        }

        await SaveViews(await FreeViews(await TutorialViews(await ProfileViews(await AimView(await LeagueViews(main))))));
        await CourseViews();
        await GalleryView();
        if (_failures == 0 && _index == expected + LeagueViewCount + TutorialViewCount + ProfileViewCount + FreeViewCount + SaveViewCount + CourseViewCount + GalleryViewCount + FinanceViewCount + ProViewCount)
        {
            GD.Print($"CAPTURES OK {_index} images");
            return 0;
        }

        return 1;
    }

    /// <summary>Enregistre l'image du viewport (PNG, et en JPEG base64 dans le journal avec --thumbs et --full) et contrôle taille et contenu.</summary>
    private void Save(string name)
    {
        var image = GetViewport().GetTexture().GetImage();
        var path = System.IO.Path.Combine(_outDir, $"{_prefix}capture-{_index:00}-{name}.png");
        _index++;
        var stddev = LuminanceStdDev(image);
        var err = image.SavePng(path);
        GD.Print($"CAPTURE {path} {image.GetWidth()}x{image.GetHeight()} stddev={stddev:F4} save={err}");
        if (_thumbs)
        {
            // Vignette JPEG en base64 dans le journal : on peut regarder les captures sans télécharger l'artefact.
            var thumb = (Image)image.Duplicate();
            thumb.Resize(384, 216, Image.Interpolation.Bilinear);
            GD.Print($"THUMB {System.IO.Path.GetFileName(path)} {Convert.ToBase64String(thumb.SaveJpgToBuffer(0.7f))}");
        }

        if (_full)
        {
            // Taille réelle, JPEG qualité 85 : lisible sans télécharger l'artefact.
            GD.Print($"FULL {System.IO.Path.GetFileName(path)} {Convert.ToBase64String(image.SaveJpgToBuffer(0.85f))}");
        }

        if (_limit > 0 && _index == _limit)
        {
            // Les passes aux facteurs 3 et 4 ne gardent que les premières vues (rapport C01) : `-- --limit n` s'arrête après n images.
            GD.Print($"CAPTURES OK {_index} images (limite)");
            GetTree().Quit(_failures == 0 && err == Error.Ok && stddev > 0.0001 ? 0 : 1);
        }

        if (err != Error.Ok || stddev <= 0.0001)
        {
            GD.PrintErr($"CAPTURES FAIL {path} (écriture ou image uniforme)");
            _failures++;
        }

        _size ??= image.GetSize();
        if (image.GetSize() != _size)
        {
            GD.PrintErr($"CAPTURES FAIL {path} taille différente");
            _failures++;
        }
    }

    /// <summary>
    /// Une saison de ligue sur un monde neuf, sans rendu : on avance par <c>World.Step</c> jusqu'à la finale de septembre
    /// et à l'ajustement du lendemain (environ 8 s de calcul), après avoir donné au monde la note du parcours (calcul direct, 6 s).
    /// Vues : le panneau de ligue, puis la carte de score du pro.
    /// </summary>
    private async Task<Main> LeagueViews(Main previous)
    {
        previous.QueueFree();
        await Frames(2);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        var world = main.World;
        world.ManualPro = false;
        world.SetCourseRating(CourseRater.Rate(world.Terrain!, world.Seed));
        var league = world.League!;
        for (var day = 0; day < 150 && !league.Final.Closed; day++)
        {
            world.Step(Calendar.TicksPerDay);
        }

        world.Step(Calendar.TicksPerDay);
        main.StepTicks(1);
        GD.Print($"LEAGUE an={league.Year + 1} finale={league.Final.Closed} ajustee={league.Adjusted} champion={league.Champion?.MemberId} evenements={league.Events.Count}");
        main.SunDayFraction = 0.5;
        main.Camera.SetView(45f, 120f, new Vector3((float)world.Terrain!.Green.X, 0f, (float)world.Terrain.Green.Z));
        main.League!.Toggle();
        await Frames(SettleFrames);
        CheckWidth(main.Ui.Find(UiIds.Panel.League)!, LeaguePanel.Width);
        Save("league-panel");

        // C09 : la fiche d'un membre porte son style (celle du pro n'en a pas), sans élargir la carte.
        main.League.ShowCard(league.Members.First(m => !m.IsPro));
        await Frames(SettleFrames);
        CheckWidth(main.Ui.Find(UiIds.Panel.LeagueCard)!, LeaguePanel.CardWidth);
        Save("league-card-member");
        main.League.ShowCard(league.Members.First(m => m.IsPro));
        await Frames(SettleFrames);
        Save("league-card");
        return main;
    }

    /// <summary>C08 : le mode visée au départ d'une partie d'entraînement, un draw au driver vers l'angle du dogleg.</summary>
    private async Task<Main> AimView(Main previous)
    {
        previous.QueueFree();
        await Frames(2);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        var world = main.World;
        world.StartProRound();
        for (var i = 0; i < 20_000 && world.AwaitingPro is null; i++)
        {
            world.Step();
        }

        main.StepTicks(1);
        main.ProAim.Sync();
        var tee = world.Terrain!.Tee;
        main.ProAim.SetClub(Clubs.Driver);
        main.ProAim.SetShape(ShotShape.Draw);
        main.ProAim.SetTarget(new Point2(tee.X + 215.0, tee.Z + 15.0));
        main.ProAim.SetForce(1.0);
        main.ProAim.Sync();
        main.SunDayFraction = 0.5;
        await Frames(SettleFrames);
        GD.Print($"AIM actif={main.ProAim.Active} prevue={main.ProAim.Preview.Ball} issue={main.ProAim.Preview.Outcome}");
        Save("pro-aim");

        // C25 : l'aperçu faussé, un fer long avec la note de lecture du vent (niveau 0 : l'aperçu peut se tromper de la moitié de la force du vent).
        main.ProAim.SetClub(Clubs.Iron5);
        main.ProAim.SetShape(ShotShape.Straight);
        main.ProAim.SetForce(0.8);
        main.ProAim.Sync();
        await Frames(SettleFrames);
        GD.Print($"APERCU club={main.ProAim.Club} prevue={main.ProAim.Preview.Ball} vent={world.Wind}");
        Save("c25-apercu");
        main.ProAim.SetClub(Clubs.Driver);
        main.ProAim.SetShape(ShotShape.Draw);
        main.ProAim.SetForce(1.0);
        main.ProAim.Sync();

        // Le coup joué, la fiche du pro avec sa première expérience.
        main.ProAim.Play();
        main.StepTicks(1);
        for (var i = 0; i < 4_000 && world.AwaitingPro is null; i++)
        {
            main.StepTicks(1);
        }

        main.ProPanel!.Toggle();
        await Frames(SettleFrames);
        GD.Print($"PRO driver={world.Pro!.Experience(Golferoo.Sim.Pro.ProSkill.Driver)} draw={world.Pro.Experience(Golferoo.Sim.Pro.ProSkill.Draw)}");
        Save("pro-sheet");
        return main;
    }

    /// <summary>
    /// C09 : le coup prévu de chaque archétype, au même niveau (handicap 10), une couleur par archétype, sans hasard et sans vent
    /// (le résolveur de la vue a un vent nul ; le HUD affiche celui du monde) : depuis le départ du dogleg (<c>c09-profils</c>),
    /// depuis une approche où les six chutes diffèrent (<c>c09-approche</c>), puis depuis la balle en lisière du lot 1 (<c>c09-sortie</c>).
    /// </summary>
    private async Task<Main> ProfileViews(Main previous)
    {
        previous.QueueFree();
        await Frames(2);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        main.SunDayFraction = 0.5;
        var world = main.World;
        var terrain = world.Terrain!;
        var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
        var level = SkillLevels.FromHandicap(10);
        var mesh = new ImmediateMesh();
        main.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
                RenderPriority = 10,
            },
        });
        var legend = new VBoxContainer();
        // En haut à gauche, hors du parcours dans les trois vues (C21) : à 240 px du haut, elle couvrait le départ de c09-profils.
        var panel = new PanelContainer { Position = new Vector2(16f, 16f) };
        panel.AddChild(legend);
        var layer = new CanvasLayer { Layer = 10 };
        layer.AddChild(panel);
        main.AddChild(layer);

        // Le départ et l'approche : centrés entre la balle et le trou ; la lisière : centrée sur la balle, assez large pour les sorties.
        var views = new[]
        {
            ("c09-profils", terrain.Tee, "depuis le départ", true, 260f),
            ("c09-approche", Approach, "approche de 92 m", true, 190f),
            ("c09-sortie", ForestEdge, "depuis la lisière", false, 0f),
        };
        foreach (var (name, ball, title, between, size) in views)
        {
            mesh.ClearSurfaces();
            foreach (var child in legend.GetChildren())
            {
                legend.RemoveChild(child);
                child.QueueFree();
            }

            legend.AddChild(new Label { Text = $"Coup prévu, handicap 10, sans vent, {title}" });
            var far = 0.0;
            for (var a = 1; a <= GolferProfile.Count; a++)
            {
                var archetype = (Archetype)a;
                var color = ProfileColors[a - 1];
                var label = new Label { Text = LeaguePanel.StyleName(archetype) };
                label.AddThemeColorOverride("font_color", color);
                legend.AddChild(label);
                if (resolver.Planned(new ShotRequest(ball, terrain.Hole, ball == terrain.Tee ? 0 : 1, level, archetype), terrain) is not { Trajectory: { Count: > 1 } path } shot)
                {
                    continue;
                }

                var d = Math.Sqrt(Math.Pow(shot.Ball.X - ball.X, 2) + Math.Pow(shot.Ball.Z - ball.Z, 2));
                far = Math.Max(far, d);
                GD.Print($"PROFIL {name} {archetype} chute=({shot.Ball.X:F1},{shot.Ball.Z:F1}) distance={d:F1} issue={shot.Outcome}");
                // Trois brins décalés de 0,4 m : un trait lisible de loin.
                foreach (var o in new[] { -0.4f, 0f, 0.4f })
                {
                    mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
                    mesh.SurfaceSetColor(color);
                    foreach (var p in path)
                    {
                        mesh.SurfaceAddVertex(new Vector3((float)p.X + o, (float)p.Y + 0.3f, (float)p.Z + o));
                    }

                    mesh.SurfaceEnd();
                }
            }

            var target = between ? new Vector3((float)(ball.X + terrain.Hole.X) / 2f, 0f, (float)(ball.Z + terrain.Hole.Z) / 2f) : new Vector3((float)ball.X, 0f, (float)ball.Z);
            main.Camera.SetView(45f, between ? size : (float)Math.Max(80.0, 2.4 * far), target);
            await Frames(SettleFrames);
            Save(name);
        }

        return main;
    }

    /// <summary>C07 : la terre vierge et ses arbres cerclés, l'étape du green avec son contour, le trou valide.</summary>
    private async Task<Main> TutorialViews(Main previous)
    {
        previous.QueueFree();
        await Frames(2);
        Main.TutorialMode = true;
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        Main.TutorialMode = false;
        var world = main.World;
        var grid = world.Terrain!;
        main.Paused = true;
        main.SunDayFraction = 0.5;
        var center = new Vector3((float)(grid.CellsX * grid.CellSize) / 2f, 0f, (float)(grid.CellsZ * grid.CellSize) / 2f);
        main.Camera.SetView(45f, 220f, center);
        main.Tutorial!.Start();
        await Frames(SettleFrames);
        Save("tutorial-blank");

        foreach (var p in grid.IsolatedTrees)
        {
            world.RazeTree(world.Trees.Trees.First(t => t.X == p.X && t.Z == p.Z).Id);
        }

        world.PlantTree(64, 208, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.PaintSurface(new Point2(28, 244), 6, Surface.Tee);
        foreach (var x in new[] { 100.0, 160.0, 220.0 })
        {
            world.PaintSurface(new Point2(x, 236), 10, Surface.Fairway);
        }

        main.StepTicks(2);
        main.Camera.SetView(45f, 140f, new Vector3(268f, 0f, 56f));
        await Frames(SettleFrames);
        Save("tutorial-green");

        world.PaintSurface(new Point2(268, 56), 11, Surface.Green);
        main.StepTicks(1);
        world.PlaceTee(1, new Point2(28, 244));
        world.PlaceHole(1, new Point2(268, 56));
        main.StepTicks(2);
        GD.Print($"TUTORIAL ouvert={world.HoleOpen} etape={Golferoo.Sim.Tutorial.TutorialSteps.Current(world)}");
        main.Camera.SetView(45f, 220f, center);
        await Frames(SettleFrames);
        Save("tutorial-done");
        return main;
    }

    /// <summary>C10 lot 2 : caméra libre sur le dogleg, du départ vers le green, à environ 60°, 30° et 8° au-dessus de l'horizon.</summary>
    private async Task<Main> FreeViews(Main previous)
    {
        previous.QueueFree();
        await Frames(2);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        main.World.ManualPro = false;
        main.SpawnGroup();
        main.StepTicks(WarmupTicks);
        main.SunDayFraction = 0.5;
        main.Brush.SetProcess(false);
        main.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, null, 0f);
        var grid = main.World.Terrain!;
        var tee = new Vector3((float)grid.Tee.X, 0f, (float)grid.Tee.Z);
        var toGreen = new Vector3((float)grid.Green.X, 0f, (float)grid.Green.Z) - tee;
        var length = toGreen.Length();
        toGreen /= length;

        // La caméra est derrière la cible, sur la droite départ-green : cap = direction opposée au regard.
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-toGreen.X, -toGreen.Z));
        foreach (var (name, pitch, distance, ahead) in new[] { ("haute", 60f, 170f, 0.5f), ("mi", 30f, 100f, 0.25f), ("rasante", 8f, 45f, 0.12f) })
        {
            main.Camera.SetFreeView(yaw, pitch, distance, tee + (toGreen * length * ahead));
            await Frames(SettleFrames);
            Save("c10-libre-" + name);
        }

        // Horizon : depuis le bord est de la carte, 5° au-dessus de l'horizon, regard vers l'extérieur (+x).
        var edge = new Vector3((float)(grid.CellsX * grid.CellSize) - 6f, 0f, (float)(grid.CellsZ * grid.CellSize) / 2f);
        main.Camera.SetFreeView(Mathf.RadToDeg(Mathf.Atan2(-1f, 0f)), 5f, 40f, edge);
        await Frames(SettleFrames);
        Save("c10-horizon");
        return main;
    }

    /// <summary>C11 lot 3 : la même vue avant la sauvegarde et après le rechargement par la scène, au même tick ; sommes de contrôle égales.</summary>
    private async Task SaveViews(Main previous)
    {
        var main = await NewSaveView(previous);
        main.StepTicks(WarmupTicks);
        var (tick, sum) = (main.World.Tick, main.World.Checksum());
        await Frames(SettleFrames);
        Save("c11-avant");
        if (!main.QuickSave() || Main.TryLoad(SaveSlots.QuickPath) is not null)
        {
            GD.PrintErr("CAPTURES FAIL c11 : sauvegarde ou lecture en échec");
            _failures++;
            return;
        }

        main = await NewSaveView(main);
        var same = main.World.Tick == tick && main.World.Checksum() == sum;
        GD.Print($"C11 tick={tick}/{main.World.Tick} checksum={sum:x16}/{main.World.Checksum():x16} {(same ? "OK" : "FAIL")}");
        if (!same)
        {
            _failures++;
        }

        await Frames(SettleFrames);
        Save("c11-apres");
    }

    private async Task<Main> NewSaveView(Main previous)
    {
        previous.QueueFree();
        await Frames(2);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        main.World.ManualPro = false;
        if (main.World.Tick == 0)
        {
            main.SpawnGroup();
        }

        main.SunDayFraction = 0.5;
        main.Brush.SetProcess(false);
        main.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, null, 0f);
        var grid = main.World.Terrain!;
        main.Camera.SetView(45f, 150f, new Vector3((float)(grid.CellsX * grid.CellSize) / 2f, 0f, (float)(grid.CellsZ * grid.CellSize) / 2f));
        return main;
    }

    /// <summary>Un panneau plus large que ses ancres (<paramref name="width"/> px) déborde : la capture échoue.</summary>
    private void CheckWidth(Control panel, float width)
    {
        GD.Print($"LEAGUE largeur {panel.Name}={panel.Size.X} (au plus {width})");
        if (panel.Size.X > width + 0.5f)
        {
            GD.PrintErr($"CAPTURES FAIL {panel.Name} trop large : {panel.Size.X} px au lieu de {width}");
            _failures++;
        }
    }

    /// <summary>
    /// C12 lot 3, sur « Le neuf » (le parcours par défaut) : <c>c12-parcours</c> (vue d'ensemble libre, de haut), <c>c12-trou-3</c> (le par 5 de bout en bout)
    /// et <c>c12-groupes</c> (cinq groupes lâchés d'affilée, cadrés ensemble) ; C21 lot 1 : <c>c21-bordures</c> et <c>c21-eau</c> (contours lissés, de près).
    /// </summary>
    private async Task CourseViews()
    {
        foreach (var old in GetChildren().OfType<Main>())
        {
            old.QueueFree();
        }

        await Frames(2);
        Main.CourseOverride = null;
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        main.World.ManualPro = false;
        main.SunDayFraction = 0.5;
        main.Brush.SetProcess(false);
        main.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, null, 0f);
        var grid = main.World.Terrain!;
        var center = new Vector3((float)(grid.CellsX * grid.CellSize) / 2f, 0f, (float)(grid.CellsZ * grid.CellSize) / 2f);
        GD.Print($"C12 parcours trous={grid.Course.Count} par={grid.Course.Holes.Sum(h => h.Par)} carte={grid.CellsX * grid.CellSize:F0}x{grid.CellsZ * grid.CellSize:F0} m");

        main.Camera.SetFreeView(0f, 80f, 600f, center);
        await Frames(SettleFrames);
        Save("c12-parcours");

        var hole3 = grid.Course[3];
        var mid = new Vector3((float)(hole3.Tee.X + hole3.Green.X) / 2f, 0f, (float)(hole3.Tee.Z + hole3.Green.Z) / 2f);
        main.Camera.SetMode(CameraMode.Iso, instant: true);
        main.Camera.SetView(45f, 300f, mid);
        await Frames(SettleFrames);
        Save("c12-trou-3");

        // C21 : de près, les contours lissés du green 1 et de ses bunkers, puis la rive de l'étang voisin.
        var green1 = grid.Course[1].Green;
        main.Camera.SetView(225f, 70f, new Vector3((float)green1.X, (float)grid.Sample(green1.X, green1.Z), (float)green1.Z));
        await Frames(SettleFrames);
        Save("c21-bordures");
        var pond = new Point2(262, 130);
        main.Camera.SetFreeView(200f, 40f, 60f, new Vector3((float)pond.X, (float)grid.Sample(pond.X, pond.Z), (float)pond.Z));
        await Frames(SettleFrames);
        Save("c21-eau");
        main.Camera.SetMode(CameraMode.Iso, instant: true);

        for (var i = 0; i < 5; i++)
        {
            main.SpawnGroup();
        }

        main.StepTicks(3000);
        var golfers = main.World.Snapshot().Golfers;
        var holes = string.Join(",", main.World.Groups.Select(g => g.Hole));
        GD.Print($"C12 groupes={main.World.Groups.Count} trous={holes} golfeurs={golfers.Count}");
        var placed = golfers.Where(g => g.State != GolferState.Roaming).ToList();
        if (placed.Count == 0)
        {
            placed = [.. golfers];
        }

        var (minX, maxX) = (placed.Min(g => g.Position.X), placed.Max(g => g.Position.X));
        var (minZ, maxZ) = (placed.Min(g => g.Position.Z), placed.Max(g => g.Position.Z));
        var focus = new Vector3((float)(minX + maxX) / 2f, 0f, (float)(minZ + maxZ) / 2f);
        var span = (float)Math.Max(maxX - minX, maxZ - minZ);
        main.Camera.SetView(45f, Math.Clamp(span * 0.8f, 120f, 400f), focus);
        await Frames(SettleFrames);
        Save("c12-groupes");

        // C23 : de près, un golfeur qui marche, puis un golfeur au sommet de son swing (le modèle de Guillaume).
        main.Actors.Range = float.PositiveInfinity;
        await CloseUp(main, "c23-marche", g => g.State is GolferState.WalkingToBall or GolferState.ToNextTee or GolferState.Exiting, _ => true);
        await CloseUp(main, "c23-swing", g => g.State == GolferState.Swinging, rig => rig.Clip == GolferModel.Swing && Math.Abs(rig.Time - SwingTopSeconds) < 0.03);
        main.Actors.Range = ActorLayer.AnimatedRange;
        await InterfaceView(main, placed[0].Id);
        await PavilionView();
    }

    /// <summary>C15 lot 3 : le pavillon et une file de golfeurs devant sa porte (deux groupes lancés bloquent le départ, les arrivées font la file), soleil en début de matinée.</summary>
    private async Task PavilionView()
    {
        foreach (var old in GetChildren().OfType<Main>())
        {
            old.QueueFree();
        }

        await Frames(2);
        Main.ClubhouseOnNewGame = true;
        Main.ArrivalsOverride = Golferoo.Sim.Golfers.ArrivalParams.Default.AtMaxAttraction();
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Paused = true;
        main.World.ManualPro = false;
        main.SunDayFraction = 0.3;
        main.Brush.SetProcess(false);
        main.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, null, 0f);
        main.Actors.Range = float.PositiveInfinity;
        main.SpawnGroup();
        main.SpawnGroup();
        for (var i = 0; i < 60 && main.World.QueueLength < 3; i++)
        {
            main.StepTicks(100);
        }

        var grid = main.World.Terrain!;
        var club = main.World.Clubhouse!;
        var door = club.DoorPosition(grid.CellSize);
        var middle = club.Position(grid.CellSize);
        var focus = new Vector3((float)(door.X + middle.X) / 2f, (float)grid.Sample(door.X, door.Z), (float)(door.Z + middle.Z) / 2f);
        GD.Print($"C15 pavillon={club.Footprint} porte={club.Side} file={main.World.QueueLength} groupes={main.World.Groups.Count}");
        main.Camera.SetMode(CameraMode.Iso, instant: true);
        main.Camera.SetView(45f, 60f, focus);
        await Frames(SettleFrames);
        Save("c15-pavillon");
        await ThoughtsView(main);
        await ReactionsView(main);
    }

    /// <summary>
    /// C16 lots 3 et 4 : de vrais visiteurs arrivés et jouant sur « Le neuf » ; on avance jusqu'à ce que la sim lève une dizaine de réactions de golfeurs différents, puis on montre
    /// leurs pensées (celles des données, bonnes en vert, mauvaises en rouge) en bulles au-dessus de leurs têtes, l'info-bulle de l'un d'eux et sa fiche, à droite (<c>c16-pensees</c>).
    /// </summary>
    private async Task ThoughtsView(Main main)
    {
        var world = main.World;
        var thoughts = main.Thoughts;
        var recent = new List<MoodChange>();
        void Note(MoodChange c)
        {
            if (c.Delta != 0 && c.Golfer.VisitorId >= 0 && recent.TrueForAll(r => r.Golfer.Id != c.Golfer.Id))
            {
                recent.Add(c);
            }
        }

        world.MoodChanged += Note;
        for (var i = 0; i < 6000 && recent.Count < 10; i++)
        {
            main.StepTicks(10);
        }

        world.MoodChanged -= Note;
        thoughts.ClearBubbles();
        if (recent.Count == 0)
        {
            GD.PrintErr("CAPTURES FAIL pensées : aucun évènement d'humeur en 6000 pas");
            _failures++;
            return;
        }

        // Le cadre : le golfeur qui a le plus de voisins parmi ceux qui ont réagi.
        var centre = recent.MaxBy(c => recent.Count(o => Math.Abs(o.Golfer.Position.X - c.Golfer.Position.X) + Math.Abs(o.Golfer.Position.Z - c.Golfer.Position.Z) < 40.0))!.Golfer;
        var ground = new Vector3((float)centre.Position.X, (float)world.Terrain!.Sample(centre.Position.X, centre.Position.Z), (float)centre.Position.Z);
        main.Camera.SetView(45f, 50f, ground);
        await Frames(SettleFrames);
        foreach (var c in recent.TakeLast(Thoughts.MaxBubbles))
        {
            thoughts.Think(c.Golfer.Id, c.Golfer.VisitorId, world.Moods.Definitions[(int)c.Event].Thought, c.Delta);
        }

        thoughts.OpenCard(centre.VisitorId);
        await Frames(2);
        thoughts.Tip(centre, main.Camera.View!.UnprojectPosition(ground));
        thoughts.Pinned = true;
        await Frames(2);
        GD.Print($"PENSEES reactions={recent.Count} bulles={thoughts.BubbleCount} visibles={thoughts.VisibleBubbleCount} fiche={thoughts.CardOpen} info_bulle={thoughts.TipVisible}");
        if (thoughts.VisibleBubbleCount == 0 || !thoughts.CardOpen)
        {
            GD.PrintErr("CAPTURES FAIL pensées : aucune bulle visible ou fiche absente");
            _failures++;
        }

        Save("c16-pensees");
        thoughts.ClearBubbles();
        thoughts.Pinned = false;
        main.Panels.Close(main.Panels.Opened.Last());
    }

    /// <summary>C16 lot 4 : après quelques jours d'arrivées, le rapport des réactions par trou ouvert à droite et la carte des réactions sur tout le parcours (<c>c16-reactions</c>).</summary>
    private async Task ReactionsView(Main main)
    {
        var world = main.World;
        for (var day = 0; day < 3; day++)
        {
            world.Step(Calendar.TicksPerDay);
        }

        main.StepTicks(1);
        main.ReactionReport.Toggle();
        main.ReactionReport.SetMap(true);
        var grid = world.Terrain!;
        var holes = grid.Course.Holes;
        var (minX, maxX) = (holes.Min(h => Math.Min(h.Tee.X, h.Hole.X)), holes.Max(h => Math.Max(h.Tee.X, h.Hole.X)));
        var (minZ, maxZ) = (holes.Min(h => Math.Min(h.Tee.Z, h.Hole.Z)), holes.Max(h => Math.Max(h.Tee.Z, h.Hole.Z)));
        var span = (float)Math.Max(maxX - minX, maxZ - minZ);
        main.SunDayFraction = 0.4;
        main.Camera.SetView(45f, Math.Clamp(span * 0.8f, 120f, 400f), new Vector3((float)(minX + maxX) / 2f, 0f, (float)(minZ + maxZ) / 2f));
        await Frames(SettleFrames);
        var report = main.ReactionReport;
        GD.Print($"REACTIONS lignes={report.Rows.Count} disques={report.MapDiscs} gardees={world.Reactions.Spots.Count} jours={world.DayIndex}");
        CheckWidth(main.Ui.Find(UiIds.Panel.Reactions)!, ReactionReport.Width);
        if (report.Rows.Count == 0 || report.MapDiscs == 0)
        {
            GD.PrintErr("CAPTURES FAIL réactions : rapport ou carte vide");
            _failures++;
        }

        Save("c16-reactions");
        report.SetMap(false);
        report.Toggle();
    }

    /// <summary>Sommet du swing de golf_swing_2s (image 23 sur 61 à 30 images/s).</summary>
    private const double SwingTopSeconds = 22.0 / 30.0;

    /// <summary>Avance tick par tick jusqu'à un golfeur animé qui remplit les deux conditions, puis le cadre de près en vue libre.</summary>
    private async Task CloseUp(Main main, string name, Func<GolferView, bool> state, Func<GolferRig, bool> pose)
    {
        for (var i = 0; i < 20 * 600; i++)
        {
            foreach (var g in main.World.Snapshot().Golfers.Where(state))
            {
                if (main.Actors.RigOf(g.Id) is { } rig && pose(rig) && main.Actors.PositionOf(g.Id) is { } at)
                {
                    GD.Print($"C23 {name} golfeur={g.Id} etat={g.State} clip={rig.Clip} t={rig.Time:F2} tick={main.World.Tick}");
                    main.Camera.SetFreeView(rig.Rotation.Y * 180f / MathF.PI, 10f, 10f, at + new Vector3(0f, 1.8f, 0f));
                    await Frames(SettleFrames);
                    Save(name);
                    return;
                }
            }

            main.StepTicks(1);
        }

        GD.PrintErr($"CAPTURES FAIL {name} : aucun golfeur trouvé");
        _failures++;
    }

    /// <summary>
    /// C13 lot 2 : l'écran de jeu de « Le neuf » comme la maquette v2, en 1920×1080 mis à l'échelle de la fenêtre : barre du bas, écusson,
    /// tiroir Terrain ouvert sur le Fairway avec son info-bulle, pastille du trou suivi en haut au centre.
    /// </summary>
    private async Task InterfaceView(Main main, int golfer)
    {
        var window = GetTree().Root;
        var (oldSize, oldMode) = (window.ContentScaleSize, window.ContentScaleMode);
        window.ContentScaleSize = UiGallery.Reference;
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        main.Camera.SetView(45f, 140f, main.Camera.Target);
        main.Camera.Follow(golfer);
        main.Bar.Select(ToolFamily.Terrain, toggle: false);
        main.Brush.SelectSurface(Surface.Fairway);
        main.Bar.Sync();
        main.RefreshHud();
        await Frames(SettleFrames);

        // L'info-bulle du survol, montrée sans souris au-dessus du tiroir, droit sur la vignette (comme UiGallery).
        var thumb = (UiThumb)main.Ui.Find(UiIds.Thumb.Fairway)!;
        var tip = UiTooltip.Framed(UiTooltip.Create("Fairway", thumb.Description, thumb.Costs));
        main.Ui.AddChild(tip);
        await Frames(1);
        var rect = thumb.GetGlobalRect();
        tip.Position = new Vector2(rect.GetCenter().X - (tip.Size.X / 2f), main.Bar.Drawer.GetGlobalRect().Position.Y - tip.Size.Y - 10f);
        await Frames(SettleFrames);
        Save("c13-interface");
        tip.QueueFree();
        main.Bar.Select(ToolFamily.None, toggle: false);

        // C13 lot 3 : trois panneaux ouverts par leurs boutons, empilés de droite à gauche au-dessus de la barre.
        foreach (var id in new[] { UiIds.Tool.Turf, UiIds.Round.League, UiIds.Round.Pro })
        {
            ((BaseButton)main.Ui.Find(id)!).EmitSignal(BaseButton.SignalName.Pressed);
        }

        await Frames(SettleFrames);
        GD.Print($"PANNEAUX ouverts={string.Join(",", main.Panels.Opened.Select(p => p.Name))}");
        Save("c13-panneaux");
        foreach (var panel in main.Panels.Opened.ToList())
        {
            main.Panels.Close(panel);
        }

        await BirdieViews(main);
        await FinanceView(main);
        window.ContentScaleSize = oldSize;
        window.ContentScaleMode = oldMode;
    }

    /// <summary>
    /// C13 lot 4 : trois messages du jeu dans Birdie, l'aperçu du dernier en haut à gauche et le compteur sur son bouton (<c>c13-birdie</c>) ;
    /// puis l'interface à 150 % (l'écran logique fait 1280×720) avec le tiroir Terrain et quatre panneaux (Gazon, Ligue, Votre pro, Birdie) dont le plus ancien se replie (<c>c13-echelle-150</c>).
    /// </summary>
    private async Task BirdieViews(Main main)
    {
        main.Birdie.Post(Birdie.Club, "Partie sauvegardée. Le registre du club est à jour.", "#sauvegarde");
        if (main.World.League is { } league && league.Members.FirstOrDefault(m => !m.IsPro) is { } member)
        {
            main.League!.Announce(new Golferoo.Sim.League.LeagueEvent(league.Year, Golferoo.Sim.League.LeagueEventType.Champion, member.Id, 0));
        }

        main.Birdie.Post(Birdie.Club, "Journée de ligue aujourd'hui, en stroke play. Les membres partent du 1.", "#ligue");
        await Frames(SettleFrames);
        GD.Print($"BIRDIE non lus={main.Birdie.Unread} messages={main.Birdie.Chirps.Count}");
        Save("c13-birdie");

        var window = GetTree().Root;
        window.ContentScaleFactor = 1.5f;
        main.Bar.Select(ToolFamily.Terrain, toggle: false);
        main.Turf.Toggle();
        main.League?.Toggle();
        main.ProPanel?.Toggle();
        main.Birdie.Toggle();
        await Frames(SettleFrames);
        var screen = window.GetVisibleRect();
        var outside = main.Ui.FindChildren("*", "Control", recursive: true, owned: false).OfType<Control>()
            .Count(c => c.IsVisibleInTree() && c.Size.X > 0f && !screen.Grow(1f).Encloses(c.GetGlobalRect()));
        GD.Print($"ECHELLE 1,5 ecran={screen.Size} hors_ecran={outside} replies={string.Join(",", main.Panels.Opened.Where(p => p.Folded).Select(p => p.Name))}");
        Save("c13-echelle-150");
        foreach (var panel in main.Panels.Opened.ToList())
        {
            main.Panels.Close(panel);
        }

        // C25 : la fiche du pro seule à 150 % (écran logique de 1280×720), avec des niveaux répartis dans les quatre groupes ; elle doit tenir à l'écran.
        if (main.ProPanel is { } sheet && main.World.Pro is { } pro)
        {
            var level = 3;
            foreach (var skill in Enum.GetValues<Golferoo.Sim.Pro.ProSkill>())
            {
                pro.Gain(skill, pro.XpForLevel(level) + ((pro.XpForLevel(level + 1) - pro.XpForLevel(level)) / 2) - pro.Experience(skill), league: true, year: main.World.PlayYear);
                level = (level * 7 % 23) + 1;
            }

            sheet.Toggle();
            await Frames(SettleFrames);
            var wide = main.Ui.FindChildren("*", "Control", recursive: true, owned: false).OfType<Control>()
                .Count(c => c.IsVisibleInTree() && c.Size.X > 0f && !screen.Grow(1f).Encloses(c.GetGlobalRect()));
            var rows150 = sheet.VisibleRows();
            GD.Print($"FICHE 1,5 ecran={screen.Size} lignes_visibles={rows150} hors_ecran={wide} birdie={main.Birdie.Chirps.Count(c => c.Tag == "#progrès")}");
            if (rows150 < 8)
            {
                GD.PrintErr($"CAPTURES FAIL fiche du pro : {rows150} habiletés visibles à 150 % avec le tiroir ouvert, 8 au moins attendues");
                _failures++;
            }

            Save("c25-fiche");
            sheet.Toggle();
            window.ContentScaleFactor = 1f;
            sheet.Toggle();
            await Frames(SettleFrames);
            var rows100 = sheet.VisibleRows();
            GD.Print($"FICHE 1,0 lignes_visibles={rows100}");
            if (rows100 != Golferoo.Sim.Pro.ProSkills.Count)
            {
                GD.PrintErr($"CAPTURES FAIL fiche du pro : {rows100} habiletés visibles à 100 % sur {Golferoo.Sim.Pro.ProSkills.Count}");
                _failures++;
            }

            sheet.Toggle();
            window.ContentScaleFactor = 1.5f;
        }

        main.Bar.Select(ToolFamily.None, toggle: false);
        window.ContentScaleFactor = 1f;
    }


    /// <summary>
    /// C14 lot 3 : le panneau Finances après huit mois de travaux (<c>c14-finances</c>) : douze mois en barres revenus et dépenses avec la ligne du solde,
    /// puis le détail du mois courant et du précédent.
    /// </summary>
    private async Task FinanceView(Main main)
    {
        var world = main.World;
        var grid = world.Terrain!;
        var center = new Point2(grid.CellsX * grid.CellSize * 0.5, grid.CellsZ * grid.CellSize * 0.5);
        var surfaces = new[] { Surface.Bunker, Surface.Fairway, Surface.Water, Surface.Path, Surface.Green, Surface.Fairway, Surface.Bunker, Surface.Water };
        for (var month = 0; month < surfaces.Length; month++)
        {
            world.PaintSurface(center, 6 + (month * 3), surfaces[month]);
            main.StepTicks(1);
            main.SkipToNextMonth();
        }

        world.PaintSurface(center, 12, Surface.Green);
        main.StepTicks(1);
        main.Bar.Select(ToolFamily.None, toggle: false);
        ((BaseButton)main.Ui.Find(UiIds.Round.Finances)!).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(SettleFrames);
        var ledger = world.Ledger;
        GD.Print($"FINANCES solde={ledger.Balance} mois_clos={ledger.Months.Count} mois_connus={main.Finances.Chart.Known.Count(k => k)} lignes={main.Finances.Table.GetChildCount() / main.Finances.Table.Columns}");
        Save("c14-finances");
        main.Finances.Toggle();
    }

    /// <summary>C13 lot 1 : l'écran témoin des composants d'interface (<c>res://tools/UiGallery.tscn</c>), seul à l'écran.</summary>
    private async Task GalleryView()
    {
        foreach (var old in GetChildren().OfType<Main>())
        {
            old.QueueFree();
        }

        var gallery = GD.Load<PackedScene>("res://tools/UiGallery.tscn").Instantiate<UiGallery>();
        AddChild(gallery);
        await Frames(SettleFrames);
        Save("c13-composants");
        gallery.QueueFree();
        await Frames(1);
    }

    private async Task Frames(int count)
    {
        for (var f = 0; f < count; f++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
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

        GD.Print($"CLEARING centre=({centre.X:F0},{centre.Z:F0}) arbres {before} puis {world.Trees.Count}, refusées {world.TreeCommandsRefused}, solde {Golferoo.Sim.Economy.Ledger.Dollars(world.Ledger.Balance)}");
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
