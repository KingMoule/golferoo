using System.Globalization;
using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Game.Ui;
using Golferoo.Sim;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Game.Tests;

/// <summary>
/// Test de fumée headless : charge la scène principale, laisse tourner quelques images,
/// vérifie que la simulation répond, puis quitte avec un code non nul en cas d'échec.
/// La CI exige aussi la ligne « SMOKE OK » : si le C# ne se charge pas, elle n'apparaît jamais.
/// </summary>
public partial class SmokeTest : Node
{
    private const int FramesToRun = 30;
    private const int TutorialFrames = 10;

    private Main? _main;
    private Main? _tutorial;

    /// <summary>C16 lot 4 : un monde à arrivées saturées, mis en pause, où la sim lève de vrais évènements d'humeur (<see cref="CheckThoughtsReal"/>).</summary>
    private Main? _real;
    private int _frames;

    public override void _Ready()
    {
        // La proposition du tutoriel n'a rien à faire dans la fumée.
        Main.OfferTutorial = false;

        // Sans écran, la fenêtre ferait 64 px : l'interface couvrirait tout. Taille du projet, pour que les clics simulés tombent où il faut.
        GetTree().Root.Size = new Vector2I(1280, 720);
        var scene = GD.Load<PackedScene>("res://Main.tscn");
        _main = scene.Instantiate<Main>();
        AddChild(_main);

        Main.ArrivalsOverride = ArrivalParams.Default.AtMaxAttraction();
        _real = scene.Instantiate<Main>();
        AddChild(_real);
        _real.Paused = true;
        _real.SetProcessUnhandledInput(false); // le clavier de la fumée vise _main, pas ce second monde (Échap y ouvrirait un menu pause)
        Main.ArrivalsOverride = null;
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (_frames == FramesToRun - 5)
        {
            // C13 : tiroir Terrain et ligue ouverts quelques images avant les clics simulés, pour que leur mise en page soit faite.
            _main!.Bar.Select(ToolFamily.Terrain);
            _main.League?.Toggle();
        }

        if (_frames < FramesToRun)
        {
            return;
        }

        if (_frames == FramesToRun)
        {
            var failures = Check();
            if (failures.Count == 0)
            {
                StartTutorial();
                return;
            }

            Finish(failures);
            return;
        }

        if (_frames >= FramesToRun + TutorialFrames)
        {
            var tutorialFailures = CheckTutorial();
            CheckFollow(tutorialFailures);
            CheckSave(tutorialFailures);
            CheckMenus(tutorialFailures);
            Finish(tutorialFailures);
        }
    }

    private void Finish(List<string> failures)
    {
        if (failures.Count == 0)
        {
            GD.Print($"SMOKE OK frames={_frames} tick={_main!.World.Tick} checksum={_main.World.Checksum():x16}");
            GetTree().Quit(0);
        }
        else
        {
            foreach (var failure in failures)
            {
                GD.PrintErr($"SMOKE FAIL {failure}");
            }

            GetTree().Quit(1);
        }
    }

    /// <summary>C07 : un second monde, la terre vierge du tutoriel, que <see cref="CheckTutorial"/> construit par commandes.</summary>
    private void StartTutorial()
    {
        Main.TutorialMode = true;
        Main.ArrivalsOverride = ArrivalParams.Default.AtMaxAttraction(); // la suite de la leçon attend des visiteurs : qu'ils arrivent vite
        _tutorial = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(_tutorial);
        Main.ArrivalsOverride = null;
        Main.TutorialMode = false;
    }

    /// <summary>
    /// C13 critères 7 et 8 : le tutoriel se fait en entier aux boutons. À chaque tour, le bouton mis en évidence existe dans <c>UiRoot</c> et
    /// se voit ; s'il n'est pas encore pris, on le presse, sinon on fait le clic de l'étape sur le terrain (par les commandes que ce clic
    /// poste, avec l'état de l'outil que les boutons ont choisi). Aucun texte de la bulle ne cite une touche.
    /// </summary>
    private List<string> CheckTutorial()
    {
        var failures = new List<string>();
        var main = _tutorial!;
        if (!_main!.Birdie.Muted || !main.Birdie.Muted)
        {
            failures.Add("birdie : la sourdine ne survit pas à une nouvelle scène");
        }

        GD.Print($"SMOKE birdie sourdine rechargée : {(main.Birdie.Muted ? "toujours coupée" : "perdue")}");
        var world = main.World;
        var grid = world.Terrain!;
        var tutorial = main.Tutorial;
        var tee = new Point2(28, 244);
        var green = new Point2(268, 56);
        if (world.HoleOpen || tutorial is null || tutorial.Phase != Golferoo.Game.Tutorial.TutorialPanel.Mode.Welcome)
        {
            failures.Add("tutoriel : terre vierge ouverte ou bulle absente");
            return failures;
        }

        var texts = new List<string> { tutorial.Text };
        Press(main, UiIds.Tutorial.Start, failures);
        var highlighted = new HashSet<string>();
        var played = false;
        for (var turn = 0; turn < 3000 && Golferoo.Sim.Tutorial.TutorialSteps.Current(world) is var step && step != Golferoo.Sim.Tutorial.TutorialStep.Done; turn++)
        {
            tutorial._Process(0);
            texts.Add(tutorial.Text);
            if (step is Golferoo.Sim.Tutorial.TutorialStep.OpenHole or Golferoo.Sim.Tutorial.TutorialStep.WatchPair or Golferoo.Sim.Tutorial.TutorialStep.FirstGreenFee or Golferoo.Sim.Tutorial.TutorialStep.ReadThought)
            {
                // C19 : on attend l'évènement de l'étape, sans bouton à presser.
                played |= step == Golferoo.Sim.Tutorial.TutorialStep.FirstGreenFee && world.Groups.Any(g => g.FromArrivals && g.Golfers.Any(o => o.Strokes > 0));
                if (tutorial.Highlighted is not null || tutorial.Text.Length == 0)
                {
                    failures.Add($"tutoriel, étape {step} : un bouton mis en évidence ou un texte vide");
                    break;
                }

                if (step == Golferoo.Sim.Tutorial.TutorialStep.ReadThought && world.Groups.SelectMany(g => g.Golfers).FirstOrDefault(world.CarriesThought) is { } thoughtful)
                {
                    main.Thoughts.OpenCard(thoughtful.VisitorId); // la fiche affiche la pensée : c'est elle qui la lit
                }

                main.StepTicks(100);
                continue;
            }

            if (tutorial.Highlighted is not { } id || main.Ui.Find(id) is not BaseButton { Visible: true } button || !button.IsVisibleInTree())
            {
                failures.Add($"tutoriel, étape {step} : bouton mis en évidence « {tutorial.Highlighted} » absent de UiRoot ou caché");
                break;
            }

            highlighted.Add(id);
            if (!button.ButtonPressed)
            {
                Press(main, id, failures);
            }
            else if (!TutorialClick(main, step, tee, green))
            {
                failures.Add($"tutoriel, étape {step} : rien à cliquer avec « {id} » pris");
                break;
            }

            main.StepTicks(2);
        }

        tutorial._Process(0);
        texts.Add(tutorial.Text);
        if (!world.HoleOpen || tutorial.Phase != Golferoo.Game.Tutorial.TutorialPanel.Mode.Finished || tutorial.Highlighted != UiIds.Tutorial.Close)
        {
            failures.Add($"tutoriel : pas fini aux boutons (trou ouvert {world.HoleOpen}, phase {tutorial.Phase}, étape {Golferoo.Sim.Tutorial.TutorialSteps.Current(world)})");
        }

        // C19 critère 9 : la leçon va de la terre vierge au snack-bar posé, avec une paire qui a joué, et se sauvegarde alors.
        if (!world.State.ClubActive || world.Clubhouse is null || !played || !world.State.ThoughtRead || world.PlacedBuildings.Count(b => b.Kind == BuildingKind.SnackBar) != 1 || world.State.Vouchers(BuildingKind.SnackBar) != 0)
        {
            failures.Add($"tutoriel C19 : club activé {world.State.ClubActive}, pavillon {world.Clubhouse is not null}, paire vue au jeu {played}, pensée lue {world.State.ThoughtRead}, snack-bars {world.PlacedBuildings.Count}");
        }

        using (var stream = new MemoryStream())
        {
            world.Save(stream);
            if (!main.CanSave || World.Load(new MemoryStream(stream.ToArray())).Checksum() != world.Checksum())
            {
                failures.Add("tutoriel C19 : la leçon activée ne se sauvegarde pas à l'identique");
            }
        }

        if (main.Ui.Find(UiIds.Tutorial.Ring) is not { Visible: true })
        {
            failures.Add("tutoriel : l'anneau jaune ne se voit pas");
        }

        Press(main, UiIds.Tutorial.Close, failures);
        if (tutorial.Phase != Golferoo.Game.Tutorial.TutorialPanel.Mode.Off)
        {
            failures.Add("tutoriel : « Fermer » laisse la bulle");
        }

        main.StepTicks(20);
        CheckNoKeys(main, failures, texts);
        GD.Print($"SMOKE tutoriel : fini aux boutons, {highlighted.Count} boutons mis en évidence ({string.Join(", ", highlighted)}), {failures.Count} échec");
        return failures;
    }

    /// <summary>Le clic sur le terrain de l'étape <paramref name="step"/>, l'outil déjà choisi aux boutons ; faux si l'outil n'est pas dans le bon état.</summary>
    private static bool TutorialClick(Main main, Golferoo.Sim.Tutorial.TutorialStep step, Point2 tee, Point2 green)
    {
        var world = main.World;
        var brush = main.Brush;
        var size = (Godot.Range)main.Ui.Find(UiIds.Drawer.BrushSize)!;
        void Paint(double radius, params Point2[] points)
        {
            size.Value = radius;
            foreach (var p in points)
            {
                world.PaintSurface(p, brush.Radius, brush.PaintedSurface);
            }
        }

        switch (step)
        {
            case Golferoo.Sim.Tutorial.TutorialStep.ClearTrees when main.TreeTool.Mode == TreeTool.ClickMode.Select:
                var isolated = world.Terrain!.IsolatedTrees;
                var corridor = world.Trees.Trees.Where(t => t.State == TreeState.Standing && isolated.Any(p => p.X == t.X && p.Z == t.Z)).Select(t => (int?)t.Id).FirstOrDefault();
                return corridor is { } id && main.TreeTool.Select(id);
            case Golferoo.Sim.Tutorial.TutorialStep.PlantSapling when main.TreeTool.Mode == TreeTool.ClickMode.Sapling:
                main.TreeTool.PlantAt(new Point2(64, 208), TreePlantStage.Sapling);
                return true;
            case Golferoo.Sim.Tutorial.TutorialStep.PaintTee when brush.PaintedSurface == Surface.Tee:
                Paint(6, tee);
                return true;
            case Golferoo.Sim.Tutorial.TutorialStep.PaintFairway when brush.PaintedSurface == Surface.Fairway:
                Paint(10, new Point2(100, 236), new Point2(160, 236), new Point2(220, 236));
                return true;
            case Golferoo.Sim.Tutorial.TutorialStep.PaintGreen when brush.PaintedSurface == Surface.Green:
                Paint(11, green);
                return true;
            case Golferoo.Sim.Tutorial.TutorialStep.PlaceTeeAndHole when main.PlaceTool.Active && !main.PlaceTool.NewHole:
                return world.PlaceTee(main.CameraHole, tee) && world.PlaceHole(main.CameraHole, green);
            case Golferoo.Sim.Tutorial.TutorialStep.PlaceSnackBar when main.BuildingTool.Kind == BuildingKind.SnackBar:
                return BuildingSpot(world, world.Terrain!, BuildingKind.SnackBar) is { } spot && main.BuildingTool.TryPlace(spot) == BuildingPlacement.Placed;
            default:
                return false;
        }
    }

    /// <summary>C11 lot 3 : F5 et F8 dans l'InputMap ; sauve, recharge par la scène (un nouveau Main part du monde chargé), même somme de contrôle.</summary>
    private void CheckSave(List<string> failures)
    {
        foreach (var action in new[] { "game_quicksave", "game_quickload" })
        {
            if (!InputMap.HasAction(action) || InputMap.ActionGetEvents(action).Count == 0)
            {
                failures.Add($"action {action} absente de l'InputMap");
            }
        }

        var before = _main!.World.Checksum();
        SaveSlots.Serialize(_main.World, out var ms);
        string? error = null;
        if (!_main.QuickSave() || (error = Main.TryLoad(SaveSlots.QuickPath)) is not null)
        {
            failures.Add($"sauvegarde ou lecture en échec : {error}");
            return;
        }

        _main.QueueFree();
        _main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(_main);
        var after = _main.World.Checksum();
        if (after != before)
        {
            failures.Add($"somme de contrôle après rechargement {after:x16} au lieu de {before:x16}");
            return;
        }

        GD.Print($"SAVE OK checksum={before:x16} apres={after:x16} serialisation={ms.ToString("F2", CultureInfo.InvariantCulture)} ms");
    }

    /// <summary>
    /// C18 lot 1 : le menu titre offre ses entrées, une nouvelle partie part de la propriété vierge, Échap ouvre le menu pause, Sauvegarder écrit l'emplacement
    /// (métadonnées lisibles sans le monde) et le recharger rend la même somme de contrôle. « Menu titre » et « Nouvelle partie » changent de scène : seule leur présence est vérifiée.
    /// </summary>
    private void CheckMenus(List<string> failures)
    {
        var title = GD.Load<PackedScene>(TitleMenu.Scene).Instantiate<TitleMenu>();
        AddChild(title);
        foreach (var id in new[] { UiIds.Menu.NewGame, UiIds.Menu.Load, UiIds.Menu.Demo, UiIds.Menu.Options, UiIds.Menu.Quit })
        {
            if (title.FindChild(id, true, false) is not Button { Disabled: false })
            {
                failures.Add($"menu titre : bouton {id} absent ou grisé");
            }
        }

        (title.FindChild(UiIds.Menu.NewGame, true, false) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
        if (title.FindChild(UiIds.Menu.Start, true, false) is null)
        {
            failures.Add("menu titre : Nouvelle partie n'ouvre pas le choix de la graine");
        }

        title.QueueFree();

        Main.Request = Main.GameRequest.NewGame;
        Main.NewGameSeed = 42;
        var game = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(game);
        if (game.World.Seed != 42 || game.World.Terrain!.Name != "Nouvelle propriété" || game.World.Clubhouse is null)
        {
            failures.Add("nouvelle partie : graine, propriété ou pavillon inattendus");
        }

        CheckGameFlow(game, failures);
        for (var i = 0; i < 40; i++)
        {
            game.World.Step();
        }

        var before = game.World.Checksum();
        PushKey(Key.Escape);
        if (!game.PauseMenu.IsOpen || !game.Paused)
        {
            failures.Add("Échap n'ouvre pas le menu pause");
        }

        foreach (var id in new[] { UiIds.Menu.Resume, UiIds.Menu.Save, UiIds.Menu.Load, UiIds.Menu.Options, UiIds.Menu.ToTitle, UiIds.Menu.Quit })
        {
            if (game.Ui.Find(id) is not Button { Disabled: false })
            {
                failures.Add($"menu pause : bouton {id} absent ou grisé");
            }
        }

        (game.Ui.Find(UiIds.Menu.Save) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
        (game.Ui.Find(UiIds.Menu.Slot("slot1")) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
        if (SaveSlots.Meta("slot1") is not { } meta || meta.Checksum != before || meta.OpenHoles != 1)
        {
            failures.Add("sauvegarde dans l'emplacement 1 : métadonnées absentes ou fausses");
        }

        PushKey(Key.Escape);
        if (game.PauseMenu.IsOpen || game.Paused)
        {
            failures.Add("Échap ne ferme pas le menu pause");
        }

        game.QueueFree();
        if (Main.TryLoad(SaveSlots.PathOf("slot1")) is { } error)
        {
            failures.Add($"chargement de l'emplacement 1 : {error}");
            return;
        }

        game = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(game);
        if (game.World.Checksum() != before)
        {
            failures.Add("emplacement 1 rechargé : la somme de contrôle a changé");
        }

        GD.Print($"SMOKE menus : titre, nouvelle partie, pause, emplacement ({failures.Count} échec)");
        CheckOptions(game, failures);
    }

    /// <summary>
    /// C18 lot 3 : l'écran Options s'ouvre du menu titre et du menu pause, une valeur change et <c>options.cfg</c> est écrit puis relu ; un fichier absent ou partiel (un fichier corrompu n'est pas testé ici : le moteur le journalise en ERROR, que la CI refuse)
    /// rend les valeurs par défaut ; la pause automatique se déclenche sur un message de la liste fermée, pas sur un autre. Le vrai fichier du joueur n'est pas touché.
    /// </summary>
    private void CheckOptions(Main game, List<string> failures)
    {
        var before = failures.Count;
        var (real, wasAuto) = (GameOptions.FilePath, GameOptions.AutoPause);
        GameOptions.FilePath = "user://options-smoke.cfg";
        DirAccess.RemoveAbsolute(GameOptions.FilePath);
        GameOptions.Load();
        if (GameOptions.Resolution != GameOptions.Resolutions[0] || GameOptions.Fullscreen || GameOptions.UiScale != 1f || !GameOptions.AutoPause)
        {
            failures.Add("options : fichier absent, les valeurs par défaut ne sont pas rendues");
        }

        WriteOptions("[options]\nauto_pause=false\nwidth=\"x\"\nui_scale=7.0\n");
        if (GameOptions.AutoPause || GameOptions.Resolution != GameOptions.Resolutions[0] || GameOptions.UiScale != 1f)
        {
            failures.Add("options : fichier partiel ou invalide mal lu");
        }

        GameOptions.AutoPause = true;
        var title = GD.Load<PackedScene>(TitleMenu.Scene).Instantiate<TitleMenu>();
        AddChild(title);
        (title.FindChild(UiIds.Menu.Options, true, false) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
        if (title.FindChild(UiIds.Menu.OptionsAutoPause, true, false) is not CheckButton { ButtonPressed: true } toggle
            || title.FindChild(UiIds.Menu.OptionsResolution, true, false) is null || title.FindChild(UiIds.Menu.OptionsFullscreen, true, false) is null || title.FindChild(UiIds.Menu.OptionsScale, true, false) is null)
        {
            failures.Add("options : l'écran ne s'ouvre pas du menu titre");
        }
        else
        {
            toggle.ButtonPressed = false;
            GameOptions.AutoPause = true;
            GameOptions.Load();
            if (GameOptions.AutoPause || !Godot.FileAccess.FileExists(GameOptions.FilePath))
            {
                failures.Add("options : le changement n'est pas écrit dans options.cfg");
            }
        }

        title.QueueFree();
        (game.Ui.Find(UiIds.Menu.Options) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
        if (game.Ui.Find(UiIds.Menu.OptionsScreen) is null)
        {
            failures.Add("options : l'écran ne s'ouvre pas du menu pause");
        }

        game.PauseMenu.Toggle(); // ferme l'écran Options
        game.PauseMenu.Toggle(); // ferme le menu pause
        GameOptions.AutoPause = true;
        game.Paused = false;
        if (game.AutoPause.Notify("meteo") || game.Paused)
        {
            failures.Add("pause automatique : un message hors liste met en pause");
        }

        if (!game.AutoPause.Notify("abandon") || !game.Paused)
        {
            failures.Add("pause automatique : un message de la liste ne met pas en pause");
        }

        game.Paused = false;
        GameOptions.AutoPause = false;
        if (game.AutoPause.Notify("abandon") || game.Paused)
        {
            failures.Add("pause automatique : l'option coupée met quand même en pause");
        }

        DirAccess.RemoveAbsolute(GameOptions.FilePath);
        (GameOptions.FilePath, GameOptions.AutoPause) = (real, wasAuto);
        GD.Print($"SMOKE options ({failures.Count - before} échec)");
    }

    private static void WriteOptions(string text)
    {
        using (var file = Godot.FileAccess.Open(GameOptions.FilePath, Godot.FileAccess.ModeFlags.Write))
        {
            file.StoreString(text);
        }

        GameOptions.Load();
    }

    /// <summary>
    /// C18 lot 2 : une nouvelle partie porte ses objectifs sous l'écusson (le bouton ouvre le panneau, sept lignes), la clôture de décembre ouvre le bilan annuel,
    /// et l'écran de fin de la faillite offre charger, menu titre et « Continuer quand même », qui le ferme et relance la partie.
    /// </summary>
    private static void CheckGameFlow(Main game, List<string> failures)
    {
        if (game.Ui.Find(UiIds.Hud.ObjectivesPill) is not Button pill || !game.World.State.ObjectivesOn)
        {
            failures.Add("objectifs : bouton sous l'écusson absent");
            return;
        }

        pill.EmitSignal(BaseButton.SignalName.Pressed);
        var rows = game.Flow.ObjectivesPanel?.Visible == true ? game.Flow.ObjectivesPanel.Content.GetChild(0).GetChildCount() : -1;
        if (rows < Sim.Objectives.ObjectiveCatalog.Default.Count)
        {
            failures.Add($"objectifs : le panneau montre {rows} lignes");
        }

        game.Panels.Close(game.Flow.ObjectivesPanel!);

        for (var i = 0; i < 9; i++)
        {
            game.World.SkipToNextMonth(); // d'avril à décembre
        }

        if (!game.Flow.ReportPanel.Visible || game.World.State.Reports.Count != 1)
        {
            failures.Add("bilan annuel : la clôture de décembre n'ouvre pas son panneau");
        }

        game.Panels.Close(game.Flow.ReportPanel);

        game.Flow.OpenEnd();
        foreach (var id in new[] { UiIds.Menu.EndLoad, UiIds.Menu.EndTitle, UiIds.Menu.EndContinue })
        {
            if (game.Ui.Find(id) is not Button { Disabled: false })
            {
                failures.Add($"écran de fin : bouton {id} absent ou grisé");
            }
        }

        if (!game.Flow.EndOpen || !game.Paused)
        {
            failures.Add("écran de fin : ni ouvert ni en pause");
        }

        (game.Ui.Find(UiIds.Menu.EndContinue) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
        if (game.Flow.EndOpen || game.Paused)
        {
            failures.Add("écran de fin : « Continuer quand même » ne le ferme pas");
        }

        GD.Print($"SMOKE objectifs : bouton, panneau {rows} lignes, bilan de décembre, écran de fin ({failures.Count} échec)");
    }

    /// <summary>C10 lot 1 : touches de caméra dans l'InputMap, Début centre sur le départ, Tab lance le suivi (vérifié par <see cref="CheckFollow"/>).</summary>
    private void CheckCamera(List<string> failures)
    {
        foreach (var action in new[] { "camera_forward", "camera_back", "camera_left", "camera_right", "camera_rotate_left", "camera_rotate_right", "camera_home", "camera_follow_next", "camera_toggle_mode" })
        {
            if (!InputMap.HasAction(action) || InputMap.ActionGetEvents(action).Count == 0)
            {
                failures.Add($"action {action} absente de l'InputMap");
            }
        }

        var tee = _main!.World.Terrain!.Tee;
        _main.Camera.SetView(45f, 150f, Vector3.Zero);
        _main.CameraHome();
        var offTee = new Vector2(_main.Camera.Target.X - (float)tee.X, _main.Camera.Target.Z - (float)tee.Z).Length();
        if (offTee > 0.01f)
        {
            failures.Add($"Début : cible à {offTee:F2} m du départ");
        }

        _main.CameraFollowNext();
        if (_main.Camera.FollowedId is null)
        {
            failures.Add("Tab : aucun golfeur suivi alors que deux sont sur le parcours");
        }

        CheckFreeCamera(failures);
        CheckHorizon(failures);
    }

    /// <summary>C10 lot 5 : l'anneau d'horizon existe, entoure la carte (quatre côtés, coins) et joint le bord du terrain sans marche.</summary>
    private void CheckHorizon(List<string> failures)
    {
        var horizon = _main!.Horizon;
        var grid = _main.World.Terrain!;
        var w = (float)(grid.CellsX * grid.CellSize);
        var d = (float)(grid.CellsZ * grid.CellSize);
        if (horizon.Mesh is null || horizon.TriangleCount is < 500 or > 16000)
        {
            failures.Add($"horizon : maillage absent ou hors budget ({horizon.TriangleCount} triangles)");
            return;
        }

        var box = horizon.Mesh.GetAabb();
        if (box.Position.X > -HorizonLayer.Width + 1f || box.End.X < w + HorizonLayer.Width - 1f || box.Position.Z > -HorizonLayer.Width + 1f || box.End.Z < d + HorizonLayer.Width - 1f)
        {
            failures.Add($"horizon : l'anneau n'entoure pas la carte ({box})");
        }

        // Jonction : le long des quatre bords et aux coins, la hauteur de l'anneau égale celle du terrain à 0,1 m près.
        for (var t = 0; t <= 8; t++)
        {
            var f = t / 8f;
            foreach (var (x, z) in new[] { (f * w, 0f), (f * w, d), (0f, f * d), (w, f * d) })
            {
                var seam = Mathf.Abs(horizon.HeightAt(x, z) - (float)grid.Sample(x, z));
                if (seam > 0.1f)
                {
                    failures.Add($"horizon : marche de {seam:F2} m en ({x:F0}, {z:F0})");
                    return;
                }
            }
        }

        // Et juste au-delà du bord, pas de saut non plus (une marche de plus de 1 m sur 2 m serait visible).
        foreach (var (x, z) in new[] { (-2f, d / 2f), (w + 2f, d / 2f), (w / 2f, -2f), (w / 2f, d + 2f), (-2f, -2f), (w + 2f, d + 2f) })
        {
            var px = Mathf.Clamp(x, 0f, w);
            var pz = Mathf.Clamp(z, 0f, d);
            if (Mathf.Abs(horizon.HeightAt(x, z) - (float)grid.Sample(px, pz)) > 1f)
            {
                failures.Add($"horizon : saut au-delà du bord en ({x:F0}, {z:F0})");
                return;
            }
        }
    }

    /// <summary>C10 lot 2 : la garde, la bascule iso, libre, iso (même cible, projection orthographique), Début et Tab en libre.</summary>
    private void CheckFreeCamera(List<string> failures)
    {
        var grid = _main!.World.Terrain!;
        var bounds = new Rect2(0, 0, (float)(grid.CellsX * grid.CellSize), (float)(grid.CellsZ * grid.CellSize));
        Func<float, float, float> ground = (x, z) => (float)grid.Sample(x, z);
        for (var i = -2; i <= 12; i++)
        {
            for (var k = -2; k <= 12; k++)
            {
                for (var h = -20f; h <= 60f; h += 20f)
                {
                    var wanted = new Vector3(bounds.Position.X + (bounds.Size.X * i / 10f), h, bounds.Position.Y + (bounds.Size.Y * k / 10f));
                    var kept = CameraRig.KeepAbove(wanted, ground, bounds);
                    var inside = kept.X >= bounds.Position.X && kept.X <= bounds.End.X && kept.Z >= bounds.Position.Y && kept.Z <= bounds.End.Y;
                    if (!inside || kept.Y < ground(kept.X, kept.Z) + CameraRig.Clearance - 0.001f)
                    {
                        failures.Add($"garde : {wanted} devient {kept}, hors carte ou sous 1,5 m du sol");
                        return;
                    }
                }
            }
        }

        var camera = _main.Camera;
        var target = new Vector3(bounds.Size.X / 2f, 0f, bounds.Size.Y / 2f);
        camera.SetView(45f, 150f, target);
        camera.SetMode(CameraMode.Free, instant: true);
        var position = camera.View!.GlobalPosition;
        if (camera.View.Projection != Camera3D.ProjectionType.Perspective)
        {
            failures.Add("libre : la projection n'est pas en perspective");
        }

        if (position.Y < ground(position.X, position.Z) + CameraRig.Clearance - 0.001f)
        {
            failures.Add("libre : la caméra est sous 1,5 m du sol");
        }

        camera.SetMode(CameraMode.Iso, instant: true);
        if (camera.View.Projection != Camera3D.ProjectionType.Orthogonal || camera.Target.DistanceTo(target) > 0.01f)
        {
            failures.Add($"bascule iso, libre, iso : projection {camera.View.Projection}, cible à {camera.Target.DistanceTo(target):F3} m");
        }

        // Début puis Tab en mode libre ; le suivi reste lancé pour CheckFollow, la vue revient en iso.
        camera.SetMode(CameraMode.Free, instant: true);
        _main.CameraHome();
        var tee = grid.Tee;
        if (new Vector2(camera.Target.X - (float)tee.X, camera.Target.Z - (float)tee.Z).Length() > 0.01f)
        {
            failures.Add("libre : Début ne centre pas sur le départ");
        }

        _main.CameraFollowNext();
        if (camera.FollowedId is null)
        {
            failures.Add("libre : Tab ne suit aucun golfeur");
        }

        camera.SetMode(CameraMode.Iso, instant: true);
        CheckLowBenchPath(failures, camera, grid, bounds, ground);
    }

    /// <summary>Trajet de la mesure rasante : sur 80 s échantillonnées, la caméra reste dans la carte et à 1,5 m au moins du sol.</summary>
    private void CheckLowBenchPath(List<string> failures, CameraRig camera, TerrainGrid grid, Rect2 bounds, Func<float, float, float> ground)
    {
        var tee = new Vector2((float)grid.Tee.X, (float)grid.Tee.Z);
        var green = new Vector2((float)grid.Green.X, (float)grid.Green.Z);
        for (var t = 0.0; t <= 80.0; t += 2.0)
        {
            FpsBench.PlaceLow(camera, tee, green, t);
            var p = camera.View!.GlobalPosition;
            if (!bounds.HasPoint(new Vector2(p.X, p.Z)) || p.Y < ground(p.X, p.Z) + CameraRig.Clearance - 0.001f)
            {
                failures.Add($"banc rasant : caméra hors carte ou sous le sol à {t:F0} s ({p})");
                break;
            }
        }

        camera.SetMode(CameraMode.Iso, instant: true);
    }

    /// <summary>Quelques images après Tab : la cible de la caméra est à moins de 1 m du golfeur suivi.</summary>
    private void CheckFollow(List<string> failures)
    {
        if (_main!.Camera.FollowedId is not { } id)
        {
            failures.Add("suivi : arrêté alors que le golfeur est encore sur le parcours");
            return;
        }

        // Même instantané que celui que lit la caméra : un golfeur qui avance entre deux instantanés ne fausse pas la mesure.
        if (_main.GolferPosition(id) is not { } golfer)
        {
            failures.Add($"suivi : golfeur {id} introuvable");
            return;
        }

        var distance = new Vector2(_main.Camera.Target.X - golfer.X, _main.Camera.Target.Z - golfer.Z).Length();
        if (distance > 1f)
        {
            failures.Add($"suivi : cible à {distance:F2} m du golfeur {id}");
        }

        // C12, C13 : la pastille « Trou n, par p », en haut au centre, s'affiche tant qu'un groupe est suivi.
        _main.RefreshHud();
        var cartouche = (_main.Ui.Find(UiIds.Hud.Hole) as UiPill)?.Label;
        if (cartouche is null || !_main.Ui.Find(UiIds.Hud.Hole)!.Visible || !cartouche.Text.StartsWith("Trou ", StringComparison.Ordinal) || !cartouche.Text.Contains(", par "))
        {
            failures.Add($"pastille de trou absente ou muette : « {cartouche?.Text} »");
        }

    }

    /// <summary>
    /// C12 lot 3 : « Le neuf » est le parcours chargé par défaut (9 trous, par 36, chacun ouvert), chaque trou a son drapeau, son repère et son numéro,
    /// la caméra va de trou en trou, et une paire joue les 9 trous (ticks directs, plus vite que x16). Écrit <c>COURSE OK 9</c>.
    /// </summary>
    private void CheckCourse(List<string> failures)
    {
        var grid = _main!.World.Terrain!;
        if (grid.Course.Count != 9 || grid.Course.Holes.Sum(h => h.Par) != 36)
        {
            failures.Add($"parcours : {grid.Course.Count} trous, par {grid.Course.Holes.Sum(h => h.Par)} (9 trous, par 36 attendus)");
            return;
        }

        foreach (var hole in grid.Course.Holes)
        {
            if (HoleCheck.Check(grid, hole).Count > 0)
            {
                failures.Add($"parcours : le trou {hole.Number} n'est pas jouable");
            }

            foreach (var name in new[] { $"Flag{hole.Number}", $"TeeMarker{hole.Number}", $"HoleNumber{hole.Number}" })
            {
                if (_main.Actors.GetNodeOrNull(name) is null)
                {
                    failures.Add($"parcours : {name} absent de la couche des acteurs");
                }
            }
        }

        foreach (var action in new[] { "cam_next_hole", "cam_prev_hole" })
        {
            if (!InputMap.HasAction(action) || InputMap.ActionGetEvents(action).Count == 0)
            {
                failures.Add($"action {action} absente de l'InputMap");
            }
        }

        foreach (var (step, expected) in new[] { (1, 2), (-1, 1), (-1, 9), (1, 1) })
        {
            _main.CameraStepHole(step);
            var tee = grid.Course[expected].Tee;
            var off = new Vector2(_main.Camera.Target.X - (float)tee.X, _main.Camera.Target.Z - (float)tee.Z).Length();
            if (off > 0.01f)
            {
                failures.Add($"caméra : trou {expected} attendu, cible à {off:F1} m de son départ");
            }
        }

        var world = new World(7, Main.LoadTerrain(_main.Factor));
        var group = world.SpawnGroup();
        var holes = new List<int> { 1 };
        for (var i = 0; i < 120_000 && !group.Finished; i++)
        {
            world.Step();
            if (group.Hole != holes[^1])
            {
                holes.Add(group.Hole);
            }
        }

        if (!group.Finished || !holes.SequenceEqual(Enumerable.Range(1, 9)) || group.Golfers.Any(g => g.Card is not { IsComplete: true }))
        {
            failures.Add($"parcours : la paire n'a pas fini les 9 trous (trous vus {string.Join(",", holes)}, tick {world.Tick})");
            return;
        }

        GD.Print($"COURSE OK {grid.Course.Count} ticks={world.Tick} scores={string.Join("/", group.Golfers.Select(g => g.Card!.Gross))}");
    }

    /// <summary>
    /// C23 lot 1 : le golfeur animé. Le .glb et la sim placent l'impact au même tick ; sur « trois trous », un golfeur suivi de près joue le clip
    /// de son état, l'image d'impact tombe au tick où la balle part (un tick d'écart au plus), la marche avance d'un cycle par cycle de distance
    /// (10 % près), la bascule à 150 m ne déplace pas le golfeur et le pool ne dépasse jamais sa borne. Écrit <c>GOLFERS OK</c>.
    /// </summary>
    private void CheckGolfers(List<string> failures)
    {
        if (GolferModel.Load() is not { } model)
        {
            failures.Add($"golfeurs : {GolferModel.ScenePath} absent");
            return;
        }

        string[] clips = [GolferModel.Walk, GolferModel.Idle, GolferModel.Swing, GolferModel.Chip, GolferModel.Putt];
        var impactTicks = (int)Math.Round(model.Impact(GolferModel.Swing) * World.TicksPerSecond);
        if (model.Bones != 21 || clips.Any(c => !model.Has(c)) || model.WalkCycle <= 0 || impactTicks != World.SwingTicksCount)
        {
            failures.Add($"golfeurs : {model.Bones} os, clips {string.Join(",", clips.Where(model.Has))}, cycle {model.WalkCycle} m, impact à {impactTicks} ticks (sim : {World.SwingTicksCount})");
            return;
        }

        var before = failures.Count;
        const string dir = "res://data/courses";
        var grid = CourseLoader.Parse(ReadText($"{dir}/trois-trous.json"), dir, _main!.Factor, relative => ReadText((dir + "/" + relative).SimplifyPath()));
        var world = new World(11, grid);
        var layer = new ActorLayer { Name = "GolferActors" };
        AddChild(layer);
        layer.Build(grid);
        var id = world.SpawnGroup().Golfers[0].Id;
        var (prev, cur) = (world.Snapshot(), world.Snapshot());
        var mismatches = new List<string>();
        long? impactTick = null, departTick = null;
        double walked = 0, cycles = 0;
        GolferView last = default;
        var lastClip = string.Empty;
        var lastTime = 0.0;
        for (var i = 0; i < 20 * 600 && (departTick is null || cycles < 3); i++)
        {
            (prev, cur) = (cur, prev);
            world.Step();
            world.Snapshot(cur);
            var view = cur.Golfers.First(g => g.Id == id);
            layer.Viewer = new Vector3((float)view.Position.X, 2f, (float)view.Position.Z);
            layer.Update(cur, prev, 1f, true);
            if (layer.RigOf(id) is not { } rig)
            {
                failures.Add($"golfeurs : le golfeur suivi n'est pas animé au tick {cur.Tick}");
                break;
            }

            var expected = view.State switch
            {
                GolferState.Arriving or GolferState.WalkingToBall or GolferState.Exiting or GolferState.ToNextTee => GolferModel.Walk,
                GolferState.Swinging => rig.Clip == GolferModel.Chip ? GolferModel.Chip : GolferModel.Swing,
                GolferState.Putting => GolferModel.Putt,
                GolferState.BallInFlight => rig.Clip,
                _ => GolferModel.Idle,
            };
            if (rig.Clip != expected && mismatches.Count < 5)
            {
                mismatches.Add($"{view.State}:{rig.Clip}@{cur.Tick}");
            }

            if (impactTick is null && rig.Clip is GolferModel.Swing or GolferModel.Chip && rig.Time >= model.Impact(rig.Clip) - 1e-6)
            {
                impactTick = cur.Tick;
            }

            if (departTick is null && view.State == GolferState.BallInFlight)
            {
                departTick = cur.Tick;
            }

            if (rig.Clip == GolferModel.Walk && lastClip == GolferModel.Walk)
            {
                var length = model.Length(GolferModel.Walk);
                cycles += ((rig.Time - lastTime + length) % length) / length;
                walked += new Vector2((float)(view.Position.X - last.Position.X), (float)(view.Position.Z - last.Position.Z)).Length();
            }

            (last, lastClip, lastTime) = (view, rig.Clip, rig.Time);
        }

        var ratio = cycles > 0 ? walked / (cycles * model.WalkCycle * ProceduralMeshes.GolferScale) : 0.0;
        if (mismatches.Count > 0)
        {
            failures.Add($"golfeurs : clip qui ne suit pas l'état ({string.Join(", ", mismatches)})");
        }

        if (impactTick is null || departTick is null || Math.Abs(impactTick.Value - departTick.Value) > 1)
        {
            failures.Add($"golfeurs : impact joué au tick {impactTick}, balle partie au tick {departTick}");
        }

        if (cycles < 1 || Math.Abs(ratio - 1.0) > 0.1)
        {
            failures.Add($"golfeurs : {walked:F1} m pour {cycles:F2} cycles de marche (rapport {ratio:F2} au cycle du modèle)");
        }

        // Bascule : à 149 m le golfeur est animé, à 151 m une silhouette du MultiMesh, au même endroit.
        var at = cur.Golfers.First(g => g.Id == id).Position;
        layer.Viewer = new Vector3((float)at.X + 149f, 0f, (float)at.Z);
        layer.Update(cur, cur, 1f, true);
        var animated = layer.RigOf(id) is not null;
        var near = layer.PositionOf(id);
        layer.Viewer = new Vector3((float)at.X + 151f, 0f, (float)at.Z);
        layer.Update(cur, cur, 1f, true);
        var far = layer.PositionOf(id);
        // Le rendu factice du mode headless ne garde pas les instances du MultiMesh : on compte les silhouettes de la couche.
        var silhouette = layer.RigOf(id) is null && layer.VisibleGolfers - layer.AnimatedGolfers == cur.Golfers.Count;
        if (!animated || !silhouette || near is null || far is null || near.Value.DistanceTo(far.Value) > 1e-3f)
        {
            failures.Add($"golfeurs : bascule à 150 m (animé à 149 m : {animated}, silhouette à 151 m : {silhouette}, positions {near} puis {far})");
        }

        // Pool : 50 golfeurs de trop, tous près ; puis un mode charge plus petit rend les rigs en trop.
        layer.Range = float.PositiveInfinity;
        world.SetLoadMode(ActorLayer.PoolSize + 50);
        layer.Update(world.Snapshot(cur), cur, 1f, true);
        var full = (layer.AnimatedGolfers, layer.RigCount);
        world.SetLoadMode(10);
        layer.Update(world.Snapshot(cur), cur, 1f, true);
        if (full != (ActorLayer.PoolSize, ActorLayer.PoolSize) || layer.RigCount > ActorLayer.PoolSize || layer.AnimatedGolfers != cur.Golfers.Count)
        {
            failures.Add($"golfeurs : pool {full.AnimatedGolfers} animés / {full.RigCount} rigs pour {ActorLayer.PoolSize + 50} golfeurs, puis {layer.AnimatedGolfers} pour {cur.Golfers.Count}");
        }

        if (failures.Count == before)
        {
            GD.Print($"GOLFERS OK impact={impactTick} depart={departTick} marche={walked:F1} m / {cycles:F2} cycles (rapport {ratio:F2}) pool={full.RigCount}");
        }

        layer.QueueFree();
    }

    /// <summary>
    /// C12 lot 5 : sur un parcours de trois trous, un quatrième trou s'ajoute (drapeau, repère et numéro apparaissent), une paire le joue,
    /// puis il se retire (les trois nœuds disparaissent). Écrit <c>NEWHOLE OK</c>.
    /// </summary>
    private void CheckNewHole(List<string> failures)
    {
        const string dir = "res://data/courses";
        var grid = CourseLoader.Parse(ReadText($"{dir}/trois-trous.json"), dir, _main!.Factor, relative => ReadText((dir + "/" + relative).SimplifyPath()));
        var world = new World(11, grid);
        var layer = new ActorLayer { Name = "NewHoleActors" };
        AddChild(layer);
        layer.Build(grid);
        var snapshot = world.Snapshot();
        var names = new[] { "Flag4", "TeeMarker4", "HoleNumber4" };

        var hole = grid.Course[2];
        if (!world.AddHole(hole.Tee, hole.Hole))
        {
            failures.Add($"nouveau trou : refusé ({string.Join(",", world.NewHoleIssues(hole.Tee, hole.Hole))})");
            layer.QueueFree();
            return;
        }

        world.Step();
        layer.Update(world.Snapshot(snapshot), snapshot, 1f, false);
        if (grid.Course.Count != 4 || grid.Course[4].Par != TerrainGrid.DeducedPar(hole.Tee, hole.Hole) || names.Any(n => layer.GetNodeOrNull(n) is null))
        {
            failures.Add($"nouveau trou : {grid.Course.Count} trous, nœuds {string.Join(",", names.Select(n => layer.GetNodeOrNull(n) is null ? "absent" : "ok"))}");
        }

        var group = world.SpawnGroup();
        var seen = new HashSet<int>();
        for (var i = 0; i < 120_000 && !group.Finished; i++)
        {
            world.Step();
            seen.Add(group.Hole);
        }

        if (!group.Finished || !seen.Contains(4) || group.Golfers.Any(g => g.Card is not { HoleCount: 4, IsComplete: true }))
        {
            failures.Add($"nouveau trou : la paire n'a pas joué le trou 4 (trous vus {string.Join(",", seen)}, tick {world.Tick})");
        }

        if (!world.RemoveLastHole())
        {
            failures.Add("nouveau trou : retrait refusé après la partie");
        }

        world.Step();
        layer.Update(world.Snapshot(snapshot), snapshot, 1f, false);
        if (grid.Course.Count != 3 || names.Any(n => layer.GetNodeOrNull(n) is not null))
        {
            failures.Add($"nouveau trou : après retrait, {grid.Course.Count} trous, nœuds {string.Join(",", names.Select(n => layer.GetNodeOrNull(n) is null ? "absent" : "reste"))}");
        }

        layer.QueueFree();
        if (!failures.Any(f => f.StartsWith("nouveau trou", StringComparison.Ordinal)))
        {
            GD.Print($"NEWHOLE OK holes={grid.Course.Count} ticks={world.Tick}");
        }
    }

    private static string ReadText(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
            ?? throw new HoleDataException($"Lecture impossible de '{path}'.");
        return file.GetAsText();
    }

    private List<string> Check()
    {
        var failures = new List<string>();
        if (_main is null)
        {
            failures.Add("scène principale absente");
            return failures;
        }

        CheckClicksStayOnUi(failures);
        if (_main.GetNodeOrNull<Label>("TickLabel") is not { } hud)
        {
            failures.Add("TickLabel absent de Main.tscn");
        }
        else if (hud.Visible || !PushKey(Key.F12) || !hud.Visible)
        {
            failures.Add($"débogage : caché au lancement et montré par F12 attendu (visible {hud.Visible})");
        }
        else if (!hud.Text.Contains("km/h") || !hud.Text.Contains("prévision :"))
        {
            failures.Add($"le HUD n'affiche pas le temps et la prévision : « {hud.Text} »");
        }

        else if (!hud.Text.Contains("greens ") || !hud.Text.Contains(" pieds)"))
        {
            failures.Add($"le HUD n'affiche pas la vitesse des greens : « {hud.Text} »");
        }

        if (_main.World.League is not { } league || !league.Divisions.Any(d => d.Any(m => m.IsPro)))
        {
            failures.Add("la ligue est absente ou le pro n'y est pas inscrit");
        }
        else if (_main.GetNodeOrNull<Label>("TickLabel") is { } label && !label.Text.Contains("Votre pro : division"))
        {
            failures.Add($"le HUD n'affiche pas le pro de la ligue : « {label.Text} »");
        }

        var cells = _main.World.Terrain!;
        var expectedChunks = ((cells.CellsX + TerrainRenderer.ChunkCells - 1) / TerrainRenderer.ChunkCells)
            * ((cells.CellsZ + TerrainRenderer.ChunkCells - 1) / TerrainRenderer.ChunkCells);
        if (_main.TerrainView.ChunkCount != expectedChunks)
        {
            failures.Add($"blocs de terrain attendus {expectedChunks} au facteur {_main.Factor}, obtenus {_main.TerrainView.ChunkCount}");
        }

        if (_main.World.Tick <= 0)
        {
            failures.Add("la simulation n'avance pas");
        }

        _main.SpawnGroup();
        var golfers = _main.World.Snapshot().Golfers.Count;
        if (golfers != 2)
        {
            failures.Add($"G devait créer 2 golfeurs, obtenu {golfers}");
        }

        CheckCourse(failures);
        CheckNewHole(failures);
        CheckGolfers(failures);
        CheckCamera(failures);
        CheckBrush(failures);
        CheckHeldBrushFlow(failures);
        CheckAssets(failures);
        CheckAssetLibrary(failures);

        // L'arbre de tools/blender/tree_lowpoly.py doit être chargé dès que le .glb est présent ;
        // la couche affiche exactement les arbres de la simulation, et une plantation ne reconstruit que des blocs.
        var trees = _main.TerrainView.GetNodeOrNull<TreeLayer>("Trees");
        var simTrees = _main.World.Trees.Count;
        if (trees is null || trees.TreeCount == 0)
        {
            failures.Add("aucun arbre affiché");
        }
        else
        {
            if (trees.UsesExternalTree != ResourceLoader.Exists(TreeLayer.ExternalTreePath))
            {
                failures.Add($"{TreeLayer.ExternalTreePath} présent mais non chargé");
            }

            if (trees.TotalCount != simTrees)
            {
                failures.Add($"TreeLayer affiche {trees.TreeCount} arbres, la simulation en compte {simTrees}");
            }

            var grid = _main.World.Terrain!;
            var rebuildsBefore = trees.BlockRebuilds;
            _main.World.PaintSurface(new Point2(grid.Tee.X + 40.0, grid.Tee.Z + 30.0), 12.0, Surface.Trees);
            _main.StepTicks(1);
            if (trees.TotalCount != _main.World.Trees.Count)
            {
                failures.Add($"après plantation, TreeLayer affiche {trees.TreeCount} arbres, la simulation en compte {_main.World.Trees.Count}");
            }

            if (trees.BlockRebuilds <= rebuildsBefore || trees.BlockRebuilds - rebuildsBefore > _main.TerrainView.ChunkCount)
            {
                failures.Add($"plantation : {trees.BlockRebuilds - rebuildsBefore} blocs d'arbres reconstruits");
            }

            // Vieillissement : la couche affiche toujours autant d'arbres que la sim, avec les modèles des essences quand ils existent.
            var ageBefore = _main.World.Trees.MeanAgeMonths;
            _main.SkipToNextMonth();
            if (_main.World.Trees.MeanAgeMonths < ageBefore + 3.9)
            {
                failures.Add($"SkipToNextMonth : âge moyen {ageBefore:F1} → {_main.World.Trees.MeanAgeMonths:F1} mois");
            }

            if (trees.TotalCount != _main.World.Trees.Count)
            {
                failures.Add($"après SkipToNextMonth, TreeLayer affiche {trees.TreeCount} arbres, la simulation en compte {_main.World.Trees.Count}");
            }

            CheckTreeTools(trees, failures);

            // Trois essences x (plant, adulte) x trois variantes, plus l'automne des feuillus, plus leur hiver terne s'il est livré.
            var expectedModels = 24 + (ResourceLoader.Exists("res://assets/trees/oak_autumn_early_01.glb") ? 12 : 0);
            if (trees.SpeciesModelCount != expectedModels)
            {
                failures.Add($"{trees.SpeciesModelCount} modèles d'essences chargés sur {expectedModels}");
            }

            // Feuillage : les couleurs du chêne et du bouleau suivent la date ; en plein été rien ne change d'un jour à l'autre, en automne tout.
            GD.Print($"SMOKE surfaces de feuillage saisonnières : {trees.SeasonalSurfaceCount}");
            if (trees.SeasonalSurfaceCount == 0)
            {
                failures.Add("feuillage : aucune surface ne prend les couleurs des saisons (noms de matériaux ?)");
            }

            trees.SetFoliage(6.5);
            if (trees.SetFoliage(6.6) != 0 || trees.SetFoliage(8.5) == 0 || trees.TotalCount != _main.World.Trees.Count)
            {
                failures.Add($"SetFoliage : blocs refaits à tort ou arbres perdus ({trees.TotalCount} sur {_main.World.Trees.Count})");
            }

            trees.SetFoliage(Foliage.Position(_main.World.Date));

            // Fleurs : au printemps, des touffes visibles dans le rough ; cachées l'été.
            var view0 = _main.TerrainView;
            view0.SetSeason(Season.Spring);
            var flowers = view0.Flowers!;
            var springCount = flowers.Count;
            var springVisible = flowers.Visible;
            view0.SetSeason(Season.Summer);
            GD.Print($"SMOKE fleurs au printemps : {springCount}");
            if (springCount == 0 || !springVisible || flowers.Visible)
            {
                failures.Add($"fleurs : {springCount} touffes au printemps (visibles {springVisible}), visibles l'été {flowers.Visible}");
            }

            view0.SetSeason(_main.World.Date.Season);

            // Pluie : des gouttes sous la forte pluie, aucune au soleil.
            var view = _main.WeatherView;
            view.Show(Golferoo.Sim.Weather.Sky.HeavyRain, Vector2.Right, Vector3.Zero, 100f);
            var drops = view.DropCount;
            view.Show(Golferoo.Sim.Weather.Sky.Sun, Vector2.Right, Vector3.Zero, 100f);
            if (drops == 0 || view.DropCount != 0)
            {
                failures.Add($"WeatherView : {drops} gouttes sous la forte pluie, {view.DropCount} au soleil");
            }

            // Flaques : sous la pluie, la couche dessine autant de cellules que la sim en compte dans des flaques.
            var wet = _main.World.Terrain!;
            var wetness = wet.Soil.Wetness;
            wet.Soil.Wetness = 1.0;
            _main.TerrainView.RefreshPuddles();
            var expected = 0;
            for (var cz = 0; cz < wet.CellsZ; cz++)
            {
                for (var cx = 0; cx < wet.CellsX; cx++)
                {
                    expected += wet.Puddles.IsPuddle(cx, cz) ? 1 : 0;
                }
            }

            GD.Print($"SMOKE flaques sous la pluie : {expected} cellules");
            if (_main.TerrainView.PuddleCells != expected)
            {
                failures.Add($"flaques : {_main.TerrainView.PuddleCells} cellules dessinées, {expected} dans la sim");
            }

            wet.Soil.Wetness = wetness;
            _main.TerrainView.RefreshPuddles();
        }

        // Même graine, mêmes ticks : la simulation doit être déterministe aussi sous Godot.
        var a = new World(seed: 99);
        var b = new World(seed: 99);
        a.Step(200);
        b.Step(200);
        if (a.Checksum() != b.Checksum())
        {
            failures.Add("somme de contrôle différente à graine égale");
        }

        CheckProAim(failures);
        CheckProSheet(failures);
        CheckUi(failures);
        CheckBar(failures);
        if (_main.GetNodeOrNull<Label>("TickLabel") is { Visible: true } && PushKey(Key.F12) && _main.GetNode<Label>("TickLabel").Visible)
        {
            failures.Add("débogage : F12 ne cache pas le libellé");
        }

        CheckClubhouse(failures);
        return failures;
    }

    /// <summary>C15 lot 3 : le pavillon est rendu à la position de la sim, la file se tient devant la porte, G est muette hors débogage, Finances nomme Buildings.</summary>
    private void CheckClubhouse(List<string> failures)
    {
        var main = _main!;
        var world = main.World;
        if (world.Clubhouse is not { } club || world.Terrain is not { } grid)
        {
            failures.Add("C15 : pas de pavillon dans le monde du jeu");
            return;
        }

        var center = club.Position(grid.CellSize);
        var model = main.Clubhouse.ModelCenter;
        if (main.Clubhouse.Shown != club || Math.Abs(model.X - center.X) > 0.05 || Math.Abs(model.Z - center.Z) > 0.05)
        {
            failures.Add($"C15 : le pavillon n'est pas rendu à la position de la sim (modèle {model}, sim {center})");
        }

        var count = world.Groups.Count;
        PushKey(Key.G);
        if (world.Groups.Count != count)
        {
            failures.Add("C15 : la touche G lance un groupe hors du mode débogage");
        }

        if (Finances.CategoryName(LedgerCategory.Buildings) != "Bâtiments")
        {
            failures.Add("C15 : Finances n'étiquette pas Buildings « Bâtiments »");
        }

        Group? arrived = null;
        world.GroupArrived += g => arrived ??= g;
        for (var i = 0; i < 3000 && arrived is null; i++)
        {
            main.StepTicks(20);
        }

        var door = club.DoorPosition(grid.CellSize);
        if (arrived is null)
        {
            failures.Add("C15 : aucun groupe n'est arrivé au pavillon en 60 000 ticks");
        }
        else if (arrived.Golfers.Any(g => Math.Abs(g.Position.X - door.X) + Math.Abs(g.Position.Z - door.Z) > 25.0))
        {
            failures.Add("C15 : le groupe arrivé n'est pas devant la porte");
        }
        else if (main.Actors.VisibleGolfers < arrived.Golfers.Count)
        {
            failures.Add($"C15 : la file n'est pas rendue ({main.Actors.VisibleGolfers} golfeurs visibles pour {arrived.Golfers.Count} arrivés)");
        }
    }

    /// <summary>Un point où le genre se poserait (porte au sud) : le centre d'une emprise que la sim accepte, ou nul.</summary>
    private static Point2? BuildingSpot(World world, TerrainGrid grid, BuildingKind kind)
    {
        var rect = world.BuildingRules.At(kind, 0, 0, DoorSide.South)!.Footprint;
        for (var z = 1; z < grid.CellsZ - rect.MaxZ - 2; z += 2)
        {
            for (var x = 1; x < grid.CellsX - rect.MaxX - 2; x += 2)
            {
                if (world.CheckBuilding(kind, x, z, DoorSide.South) == BuildingPlacement.Placed)
                {
                    return new Point2((x + ((rect.MaxX + 1) / 2.0)) * grid.CellSize, (z + ((rect.MaxZ + 1) / 2.0)) * grid.CellSize);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// C17 lot 3, critère 9 : chaque bouton du tiroir Bâtiments choisit son genre et pose son bâtiment (sans touche), rendu à la position de la sim ; le fantôme est vert
    /// sur un emplacement valide et rouge sur un fairway ; une vente rend la moitié du prix. Le monde est remis comme avant.
    /// </summary>
    private void CheckBuildings(List<string> failures)
    {
        var main = _main!;
        var world = main.World;
        var grid = world.Terrain!;
        var tool = main.BuildingTool;
        var layer = main.Structures;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"C17 bâtiments : {what}");
            }
        }

        Expect(Press(UiIds.Tool.Buildings, failures) && tool.Active && main.Bar.Current == ToolFamily.Buildings && !main.PlaceTool.Active && !main.Brush.Active, "le bouton Bâtiments ne prend pas l'outil");
        var placed = new List<Golferoo.Sim.Buildings.Building>();
        foreach (var kind in BuildingTool.Kinds)
        {
            var name = kind.ToString();
            Expect(Press(UiIds.Action.Building(name), failures) && tool.Kind == kind, $"{name} : le bouton ne choisit pas le genre");
            if (BuildingSpot(world, grid, kind) is not { } spot)
            {
                failures.Add($"C17 bâtiments : {name} : aucun emplacement valide sur le parcours");
                continue;
            }

            var count = world.PlacedBuildings.Count;
            var balance = world.Ledger.Balance;
            Expect(tool.ShowGhost(spot) == BuildingPlacement.Placed && layer.GhostVisible && layer.GhostValid, $"{name} : le fantôme n'est pas vert sur un emplacement valide");
            var result = tool.TryPlace(spot);
            Expect(result == BuildingPlacement.Placed && world.PlacedBuildings.Count == count + 1, $"{name} : pose refusée ({result})");
            Expect(world.Ledger.Balance <= balance - (world.BuildingCost(kind) ?? 0), $"{name} : le prix n'est pas payé");
            if (world.PlacedBuildings.Count == count + 1)
            {
                placed.Add(world.PlacedBuildings[^1]);
            }
        }

        layer.Update(world.PlacedBuildings);
        var centers = layer.ModelCenters.ToList();
        Expect(layer.ShownCount == placed.Count && centers.Count == placed.Count, $"{layer.ShownCount} modèles rendus pour {placed.Count} bâtiments posés");
        for (var i = 0; i < Math.Min(centers.Count, placed.Count); i++)
        {
            var at = placed[i].Position(grid.CellSize);
            Expect(Math.Abs(centers[i].X - at.X) < 0.05 && Math.Abs(centers[i].Z - at.Z) < 0.05, $"{placed[i].Kind} : le modèle n'est pas à la position de la sim ({centers[i]} pour {at})");
        }

        // Le fantôme d'un snack-bar sur un fairway est rouge, avec une raison.
        Press(UiIds.Action.Building(nameof(BuildingKind.SnackBar)), failures);
        Point2? fairway = null;
        for (var z = 10.0; z < grid.CellsZ * grid.CellSize && fairway is null; z += 5.0)
        {
            for (var x = 10.0; x < grid.CellsX * grid.CellSize && fairway is null; x += 5.0)
            {
                fairway = grid.SurfaceAt(x, z) == Surface.Fairway ? new Point2(x, z) : null;
            }
        }

        Expect(fairway is { } f && tool.ShowGhost(f) == BuildingPlacement.BadSurface && layer.GhostVisible && !layer.GhostValid && tool.Status.Contains("rough"), "le fantôme ne montre pas de refus sur un fairway");

        // Une vente rend la moitié du prix et libère l'emprise.
        foreach (var building in placed.AsEnumerable().Reverse())
        {
            var cost = world.BuildingCost(building.Kind) ?? 0;
            var balance = world.Ledger.Balance;
            var count = world.PlacedBuildings.Count;
            Expect(tool.TrySell(building.Position(grid.CellSize)) && world.Ledger.Balance == balance + (cost / 2) && world.PlacedBuildings.Count == count - 1, $"{building.Kind} : la vente ne rend pas la moitié du prix");
        }

        layer.Update(world.PlacedBuildings);
        Expect(layer.ShownCount == 0, "des modèles restent après la vente");
        main.Bar.Select(ToolFamily.None, toggle: false);
        Expect(!tool.Active && !layer.GhostVisible, "l'outil et son fantôme ne se ferment pas");
    }

    /// <summary>Pousse une touche (appui puis relâché) dans la fenêtre, comme le clavier ; rend vrai pour s'enchaîner dans une condition.</summary>
    private bool PushKey(Key key)
    {
        foreach (var pressed in new[] { true, false })
        {
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
        }

        return true;
    }

    /// <summary>Pousse un clic gauche (appui seul, ou relâché seul) à la position d'écran <paramref name="at"/>.</summary>
    private void PushClick(Vector2 at, bool pressed)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = at, GlobalPosition = at, ButtonMask = pressed ? MouseButtonMask.Left : 0 });
    }

    /// <summary>
    /// C21 lot 3, critère 11 : pinceau tenu 10 s (un coup par tick sur une carte chargée de marcheurs), aucun marcheur ne reste plus de 2 s de jeu
    /// sans bouger, c'est-à-dire sans que son champ de flux ait été refait. Écrit <c>FLOW OK</c>.
    /// </summary>
    private void CheckHeldBrushFlow(List<string> failures)
    {
        var grid = Main.LoadTerrain(_main!.Factor);
        var world = new World(5, grid);
        world.SetLoadMode(40);
        world.Step(200);
        var snapshot = new WorldSnapshot();
        var last = new Dictionary<int, Point2>();
        var still = new Dictionary<int, int>();
        var worst = 0;
        var maxStill = 2 * World.TicksPerSecond;
        for (var tick = 0; tick < 10 * World.TicksPerSecond; tick++)
        {
            world.PaintSurface(new Point2(2.0 + (tick % 2), 2.0), 1.0, tick % 2 == 0 ? Surface.Bunker : Surface.Rough);
            world.Step();
            world.Snapshot(snapshot);
            foreach (var g in snapshot.Golfers)
            {
                if (g.State != GolferState.Roaming)
                {
                    continue;
                }

                var moved = !last.TryGetValue(g.Id, out var before) || before != g.Position;
                still[g.Id] = moved ? 0 : still.GetValueOrDefault(g.Id) + 1;
                last[g.Id] = g.Position;
                worst = Math.Max(worst, still[g.Id]);
            }
        }

        if (worst > maxStill)
        {
            failures.Add($"pinceau tenu : un marcheur est resté {worst} ticks sans bouger (limite {maxStill}, soit 2 s de jeu)");
            return;
        }

        GD.Print($"FLOW OK worst={worst} ticks");
    }

    /// <summary>
    /// C13 critère 4 : pinceau actif sur le fairway, un clic gauche sur la barre, sur le tiroir ou sur le panneau de la ligue ne peint aucune cellule ;
    /// le même clic plus haut sur le terrain, lui, peint (témoin).
    /// </summary>
    private void CheckClicksStayOnUi(List<string> failures)
    {
        var main = _main!;
        var grid = main.World.Terrain!;
        var brush = main.Brush;
        main.Bar.Select(ToolFamily.Terrain, toggle: false);
        brush.SelectSurface(Surface.Fairway);
        var targets = new List<(string Name, Control? Control)>
        {
            ("barre", main.Ui.Find(UiIds.Tool.Buildings)),
            ("tiroir", main.Ui.Find(UiIds.Drawer.Status)?.GetParentControl()),
            ("ligue", main.Ui.Find(UiIds.Panel.League)),
        };
        foreach (var (name, control) in targets)
        {
            if (control is not { Visible: true })
            {
                failures.Add($"clic sur l'interface : cible « {name} » absente ou cachée");
                continue;
            }

            var at = control.GetGlobalRect().GetCenter();
            var version = (grid.SurfaceVersion, grid.HeightVersion);
            PushClick(at, pressed: true);
            var painted = brush.PickAt(at, out var hit) && brush.Apply(hit);
            main.StepTicks(1);
            PushClick(at, pressed: false);
            if (painted || (grid.SurfaceVersion, grid.HeightVersion) != version)
            {
                failures.Add($"clic sur l'interface ({name}, {at}) : le pinceau a peint le terrain");
            }
        }

        // Témoin : la caméra centrée sur le milieu de la carte, un clic juste au-dessus du centre de l'écran (loin de la barre et du tiroir).
        var followed = main.Camera.FollowedId;
        var center = new Vector3((float)(grid.CellsX * grid.CellSize) / 2f, 0f, (float)(grid.CellsZ * grid.CellSize) / 2f);
        main.Camera.CenterOn(center);
        var middle = main.Camera.View!.UnprojectPosition(center with { Y = (float)grid.Sample(center.X, center.Z) }) - new Vector2(0f, 80f);
        main.Camera.Follow(followed);
        var before = grid.SurfaceVersion;
        PushClick(middle, pressed: true);
        var applied = brush.PickAt(middle, out var ground) && brush.Apply(ground);
        main.StepTicks(1);
        PushClick(middle, pressed: false);
        if (!applied || grid.SurfaceVersion == before)
        {
            failures.Add($"témoin : un clic sur le terrain ({middle}) ne peint pas (posé {applied}, sol {brush.PickAt(middle, out _)}, sous la souris {GetViewport().GuiGetHoveredControl()?.GetPath()}, attente {main.World.PendingEdits}/{main.World.PendingTreeCommands})");
        }

        main.League?.Toggle();
        main.Bar.Select(ToolFamily.None, toggle: false);
    }

    /// <summary>Émet le signal <c>Pressed</c> du bouton d'identifiant <paramref name="id"/>, comme un clic ; faux s'il manque.</summary>
    private bool Press(string id, List<string> failures) => Press(_main!, id, failures);

    private static bool Press(Main main, string id, List<string> failures)
    {
        if (main.Ui.Find(id) is not BaseButton button || button.Disabled)
        {
            failures.Add($"barre : bouton {id} absent ou grisé");
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>
    /// C13 critères 1, 3 et 5 : barre, tiroir et pastille du trou sont sous la racine thémée ; chaque outil se prend par le signal <c>Pressed</c>
    /// de son bouton (aucun événement clavier) et l'état de l'outil suit ; le bulldozer rend au rough un carré de fairway et rase son arbre.
    /// </summary>
    private void CheckBar(List<string> failures)
    {
        var main = _main!;
        var ui = main.Ui;
        var brush = main.Brush;
        var trees = main.TreeTool;
        var place = main.PlaceTool;
        var world = main.World;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"barre : {what}");
            }
        }

        Expect(ui.Theme == UiTheme.Theme && ui.GetParent<CanvasLayer>().Name == "Ui", "racine d'interface sans thème ou hors du calque Ui");
        foreach (var id in new[] { UiIds.Hud.Bar, UiIds.Drawer.Root, UiIds.Hud.Hole, UiIds.Hud.Message, UiIds.Hud.Crest })
        {
            Expect(ui.Find(id) is { } c && ui.IsAncestorOf(c), $"{id} absent de UiRoot");
        }

        Expect(ui.Find(UiIds.Drawer.Root)?.GetThemeStylebox("panel") == UiTheme.Theme.GetStylebox("panel", "UiPanel"), "le tiroir n'a pas le verre du thème");
        Expect(ui.Find(UiIds.Hud.Hole)?.GetThemeStylebox("panel") == UiTheme.Theme.GetStylebox("panel", "UiPill"), "la pastille du trou n'a pas le style du thème");
        Expect(ui.Find(UiIds.Tool.Terrain)?.GetThemeFont("font") == UiTheme.Fredoka && ui.Find(UiIds.Action.HoleNew)?.GetThemeFont("font") == UiTheme.Fredoka, "les boutons de la barre et du tiroir ne sont pas en Fredoka");

        // Terrain : chaque surface.
        Press(UiIds.Tool.Terrain, failures);
        Expect(brush.Active && !brush.ReliefMode && !brush.BulldozerMode && main.Bar.Current == ToolFamily.Terrain && ui.Find(UiIds.Drawer.Root)!.Visible, "Terrain ne prend pas le pinceau de surface ou n'ouvre pas le tiroir");
        (string Id, Surface Surface)[] surfaces =
        [
            (UiIds.Thumb.Tee, Surface.Tee), (UiIds.Thumb.Green, Surface.Green), (UiIds.Thumb.Fairway, Surface.Fairway), (UiIds.Thumb.Rough, Surface.Rough),
            (UiIds.Thumb.Bunker, Surface.Bunker), (UiIds.Thumb.Water, Surface.Water), (UiIds.Thumb.Wood, Surface.Trees), (UiIds.Thumb.Path, Surface.Path),
        ];
        foreach (var (id, surface) in surfaces)
        {
            Press(id, failures);
            Expect(brush.Active && brush.PaintedSurface == surface && ((UiThumb)ui.Find(id)!).Chosen, $"{id} ne choisit pas {surface}");
        }

        // Relief, puis les arbres : chaque essence (plant, motte) et chaque action.
        Press(UiIds.Tool.Relief, failures);
        Expect(brush.Active && brush.ReliefMode, "Relief ne prend pas le relief");
        Press(UiIds.Tool.Trees, failures);
        Expect(trees.Active && !brush.Active, "Arbres ne prend pas l'outil arbres");
        foreach (var species in Enum.GetValues<TreeSpeciesId>())
        {
            foreach (var stage in new[] { TreePlantStage.Sapling, TreePlantStage.Mound })
            {
                Press(UiIds.Thumb.Tree(species.ToString(), stage.ToString()), failures);
                Expect(trees.Species == species && trees.Mode == (stage == TreePlantStage.Mound ? TreeTool.ClickMode.Mound : TreeTool.ClickMode.Sapling), $"vignette {species} {stage}");
            }
        }

        Press(UiIds.Action.TreeRazeArea, failures);
        Expect(trees.Mode == TreeTool.ClickMode.RazeArea, "Raser une zone");
        Press(UiIds.Action.TreeSelect, failures);
        Expect(trees.Mode == TreeTool.ClickMode.Select, "Sélectionner");
        var keep = trees.KeepRemarkable;
        Press(UiIds.Action.TreeKeepRemarkable, failures);
        Expect(trees.KeepRemarkable == !keep, "Garder les remarquables");
        Press(UiIds.Action.TreeKeepRemarkable, failures);
        var standing = world.Trees.Trees.Where(t => t.State == TreeState.Standing).Take(2).ToList();
        if (standing.Count == 2)
        {
            trees.Select(standing[0].Id);
            var queued = world.PendingTreeCommands;
            Press(UiIds.Action.TreeRaze, failures);
            Expect(world.PendingTreeCommands == queued + 1 && trees.SelectedId == -1, "Raser n'abat pas l'arbre sélectionné");
            trees.Select(standing[1].Id);
            trees.FellSelected();
            main.StepTicks(1);
            Press(UiIds.Action.TreeClearFallen, failures);
            Expect(world.PendingTreeCommands == 1, "Dégager le tronc ne dégage pas le tronc tombé");
            main.StepTicks(1);
        }
        else
        {
            failures.Add("barre : moins de deux arbres debout pour les actions d'arbres");
        }

        // Trous : pose, nouveau trou, supprimer, précédent et suivant ; la caméra reprend ensuite son golfeur.
        var followed = main.Camera.FollowedId;
        Press(UiIds.Tool.Holes, failures);
        Expect(place.Active && !trees.Active && !brush.Active, "Trous ne prend pas l'outil de pose");
        Press(UiIds.Action.HoleNew, failures);
        Expect(place.NewHole, "Nouveau trou");
        Press(UiIds.Action.HolePose, failures);
        Expect(!place.NewHole, "Poser départ et trou");
        Press(UiIds.Action.HoleNext, failures);
        Expect(main.CameraHole == 2, $"Trou suivant : trou {main.CameraHole}");
        Press(UiIds.Action.HolePrevious, failures);
        Expect(main.CameraHole == 1, $"Trou précédent : trou {main.CameraHole}");
        Press(UiIds.Action.HoleRemove, failures);
        Expect(ui.Find(UiIds.Hud.Message) is UiPill { Visible: true } message && message.Label.Text.Contains("trou"), "Supprimer ne dit rien");
        main.Camera.Follow(followed);

        CheckBulldozer(failures);

        // Entretien, vitesses et pause, caméra libre, panneaux.
        Press(UiIds.Tool.Views, failures);
        Expect(main.TurfOverlay, "Vues ne montre pas l'état du gazon");
        Press(UiIds.Tool.Views, failures);
        Press(UiIds.Tool.Turf, failures);
        Expect(main.Turf.IsOpen, "Gazon n'ouvre pas les consignes");
        Press(UiIds.Tool.Turf, failures);
        foreach (var speed in new[] { 1, 2, 4, 8, 16 })
        {
            Press(UiIds.Hud.Speed(speed), failures);
            Expect(main.Speed == speed && !main.Paused, $"vitesse ×{speed}");
        }

        Press(UiIds.Hud.Pause, failures);
        Expect(main.Paused, "Pause");
        Press(UiIds.Hud.Speed(1), failures);
        Expect(!main.Paused && main.Speed == 1, "reprise à ×1");
        Press(UiIds.Round.Camera, failures);
        Expect(main.Camera.Mode == CameraMode.Free, "Caméra libre");
        Press(UiIds.Round.Camera, failures);
        Expect(main.Camera.Mode == CameraMode.Iso, "retour à la vue iso");
        Expect(ui.Find(UiIds.Tool.Amenities) is BaseButton { Disabled: true }, $"{UiIds.Tool.Amenities} devrait être grisé");
        CheckBuildings(failures);

        main.Bar.Select(ToolFamily.None, toggle: false);
        Expect(!brush.Active && !trees.Active && !place.Active && !ui.Find(UiIds.Drawer.Root)!.Visible, "rien de pris : le tiroir reste ouvert");
        GD.Print($"SMOKE barre : outils, vignettes, actions, vitesses, caméra par leurs boutons ({failures.Count} échec)");
        CheckPanels(failures);
        CheckBirdie(failures);
        CheckThoughts(failures);
        CheckThoughtsReal(failures);
        CheckReactions(failures);
        CheckUiScale(failures);
        CheckNoKeys(main, failures, []);
        CheckBirdieDays(failures);
        CheckFinances(failures);
    }

    /// <summary>
    /// Décision de Guillaume : Birdie publie un message au changement de saison et un message de météo chaque matin. Placé en dernier, car
    /// <c>SkipToNextMonth</c> (F10) avance le monde d'un mois à chaque appel.
    /// </summary>
    private void CheckBirdieDays(List<string> failures)
    {
        var main = _main!;
        var birdie = main.Birdie;
        var season = main.World.Season;
        var seasonal = birdie.Chirps.Count(c => c.Tag == "#saison");
        var unread = birdie.Unread;
        main.SkipToNextMonth();
        main.SkipToNextMonth();
        if (birdie.Chirps[^1].Tag != "#météo" || birdie.Chirps.Count(c => c.Tag == "#météo") != 1 || birdie.Unread != unread + (main.World.Season != season ? 1 : 0))
        {
            failures.Add($"birdie : deux matins devraient laisser un seul message météo sans non lu de plus ({birdie.Chirps.Count(c => c.Tag == "#météo")} messages, non lus {unread} puis {birdie.Unread})");
        }

        for (var i = 0; i < 4 && main.World.Season == season; i++)
        {
            main.SkipToNextMonth();
        }

        var latest = birdie.Chirps.TakeLast(2).Select(c => c.Tag).ToList();
        if (main.World.Season == season || birdie.Chirps.Count(c => c.Tag == "#saison") != seasonal + 1 || !latest.SequenceEqual(["#saison", "#météo"]))
        {
            failures.Add($"birdie : saison {season} puis {main.World.Season}, derniers messages {string.Join(",", latest)}");
        }

        Press(UiIds.Hud.BirdieMute, failures); // la sourdine doit survivre à un nouveau Main (F9 recharge la scène) : voir CheckTutorial
        GD.Print($"SMOKE birdie jours : météo au matin, saison {season} puis {main.World.Season} annoncée (derniers messages {string.Join(",", latest)})");
    }

    /// <summary>Pousse Ctrl+Z (appui puis relâché), comme le clavier.</summary>
    private void PushUndoKey()
    {
        foreach (var pressed in new[] { true, false })
        {
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Z, PhysicalKeycode = Key.Z, CtrlPressed = true, Pressed = pressed });
        }
    }

    private static string[][] TableRows(UiTable table)
    {
        var cells = table.GetChildren().OfType<Label>().Skip(table.Columns).Select(l => l.Text).ToList();
        return [.. Enumerable.Range(0, cells.Count / table.Columns).Select(r => cells.Skip(r * table.Columns).Take(table.Columns).ToArray())];
    }

    /// <summary>
    /// C14 lot 3 : la pastille d'argent et le panneau suivent le grand livre (une dépense, puis une clôture de mois) ; le prix près du curseur égale
    /// <c>StrokeCost</c> ; la confirmation du découvert s'ouvre une fois par passage ; Annuler et Ctrl+Z défont le coup dans les cinq secondes
    /// et plus après ; un achat refusé par la limite écrit dans Birdie. Placé en dernier : il dépense, saute un mois et finit par vider la caisse.
    /// </summary>
    private void CheckFinances(List<string> failures)
    {
        var main = _main!;
        var world = main.World;
        var grid = world.Terrain!;
        var ledger = world.Ledger;
        var brush = main.Brush;
        var finances = main.Finances;
        var money = (UiPill)main.Ui.Find(UiIds.Hud.Money)!;

        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add("finances : " + what);
            }
        }

        string Pill() => UiMoney.Pill(ledger);
        if (FreeSpot(world, grid) is not { } p)
        {
            failures.Add("finances : aucun emplacement libre");
            return;
        }

        // 1. Le grand livre bouge : la pastille et le panneau suivent, puis la clôture du mois les range en deux colonnes.
        main.Bar.Select(ToolFamily.None, toggle: false);
        Press(UiIds.Round.Finances, failures);
        Expect(finances.IsOpen, "le bouton n'ouvre pas le panneau");
        var start = ledger.Balance;
        world.PaintSurface(p, 8, Surface.Water);
        main.StepTicks(1);
        main.Bar.Sync();
        var spent = start - ledger.Balance;
        var balanceLabel = (Label)main.Ui.Find(UiIds.Finance.Balance)!;
        Expect(spent > 0 && money.Label.Text == Pill() && balanceLabel.Text == UiMoney.Format(ledger.Balance), $"après une dépense de {spent}, pastille « {money.Label.Text} » ou solde « {balanceLabel.Text} » hors du grand livre");
        var rows = TableRows(finances.Table);
        Expect(rows.Length == 10 && rows[^2][0] == "Bâtiments" && rows[2][0] == "Surfaces" && rows[2][1] == UiMoney.Format(ledger.Current(LedgerCategory.Surfaces), true) && rows[^1][1] == UiMoney.Format(UiMoney.MonthResult(ledger), true), "le détail du mois ne suit pas le grand livre");
        main.SkipToNextMonth();
        main.Bar.Sync();
        var closed = ledger.Months[^1];
        rows = TableRows(finances.Table);
        Expect(money.Label.Text == Pill() && rows[^1][2] == UiMoney.Format(closed.Result, true) && rows[2][2] == UiMoney.Format(closed.Total(LedgerCategory.Surfaces), true), "la clôture du mois n'arrive pas dans la colonne du mois précédent");
        var chart = finances.Chart;
        Expect(chart.Known[^1] && chart.Known[^2] && !chart.Known[0] && chart.Balance[^1] == ledger.Balance && chart.Expense[^2] > 0, "le graphique ne montre pas le mois clos et le mois en cours");
        Press(UiIds.Round.Finances, failures);

        // 2 et 4. Un coup de pinceau : le prix près du curseur égale StrokeCost ; Annuler, puis Ctrl+Z, puis plus rien après cinq secondes.
        var followed = main.Camera.FollowedId;
        main.Camera.CenterOn(new Vector3((float)p.X, 0f, (float)p.Z));
        Press(UiIds.Tool.Terrain, failures);
        brush.SetRadius(6f);
        var at = main.Camera.View!.UnprojectPosition(new Vector3((float)p.X, (float)grid.Sample(p.X, p.Z), (float)p.Z));
        var undo = main.Ui.Find(UiIds.Action.Undo)!;
        var pill = finances.CostPill;
        var surfaces = new[] { Surface.Bunker, Surface.Green, Surface.Path };
        for (var round = 0; round < surfaces.Length; round++)
        {
            brush.SelectSurface(surfaces[round]);
            var before = ledger.Balance;
            PushClick(at, pressed: true);
            brush.Apply(brush.PickAt(at, out var hit) ? hit : default);
            main.StepTicks(1);
            brush._Process(0);
            Expect(world.StrokeCost > 0 && pill.Visible && pill.Text == UiMoney.Format(world.StrokeCost) && before - ledger.Balance == world.StrokeCost, $"manche {round} : prix près du curseur « {pill.Text} » pour un coup de {world.StrokeCost}, débit {before - ledger.Balance}");
            PushClick(at, pressed: false);
            main.Bar.Sync();
            Expect(brush.CanUndo && undo.Visible, $"manche {round} : Annuler absent juste après le coup");
            var cost = world.StrokeCost;
            if (round == 0)
            {
                Press(UiIds.Action.Undo, failures);
            }
            else if (round == 1)
            {
                PushUndoKey();
            }
            else
            {
                brush.AgeLastStroke(Brush.UndoWindowMilliseconds + 1);
                main.Bar.Sync();
                Expect(!brush.CanUndo && !undo.Visible, "Annuler reste offert après cinq secondes");
                PushUndoKey();
            }

            main.StepTicks(1);
            main.Bar.Sync();
            Expect(round == 2 ? before - ledger.Balance == cost : ledger.Balance == before && !undo.Visible, $"manche {round} : solde {ledger.Balance} au lieu de {(round == 2 ? before - cost : before)}");
        }

        // Un clic sans retouche ne rend pas ses cinq secondes au coup précédent ; la pause arrête le compte à rebours et grise le bouton.
        void Stroke(Surface surface)
        {
            brush.SelectSurface(surface);
            PushClick(at, pressed: true);
            brush.Apply(brush.PickAt(at, out var h) ? h : default);
            main.StepTicks(1);
            PushClick(at, pressed: false);
            main.Bar.Sync();
        }

        Stroke(Surface.Water);
        Expect(brush.CanUndo, "pause : pas de coup annulable pour commencer");
        brush.AgeLastStroke(3000);
        PushClick(at, pressed: true);
        PushClick(at, pressed: false);
        brush.AgeLastStroke(2500);
        Expect(!brush.CanUndo, "un clic sans retouche a rendu cinq secondes au coup précédent");
        Stroke(Surface.Fairway);
        var paused = ledger.Balance;
        main.Paused = true;
        brush._Process(10.0);
        main.Bar.Sync();
        PushUndoKey();
        main.StepTicks(1);
        Expect(brush.UndoOffered && !brush.CanUndo && undo.Visible && ((Button)undo).Disabled && ledger.Balance == paused, "en pause, l'annulation devrait attendre : bouton grisé, Ctrl+Z sans effet, temps gardé");
        main.Paused = false;
        brush._Process(0);
        main.Bar.Sync();
        Expect(brush.CanUndo && !((Button)undo).Disabled, "à la reprise, le coup devrait rester annulable");
        PushUndoKey();
        main.StepTicks(1);
        Expect(ledger.Balance > paused, "à la reprise, Ctrl+Z n'annule pas le coup");

        // Le prix annoncé d'une zone rasée (et du bulldozer) égale ce que la sim débite.
        world.PaintSurface(p, 8, Surface.Fairway);
        main.StepTicks(1);
        world.PlantTree(p.X, p.Z, TreeSpeciesId.Oak, TreePlantStage.Mound);
        main.StepTicks(1);
        var razePrice = TreeTool.RazePrice(world, p, 6.0, keepRemarkable: false);
        var beforeRaze = ledger.Balance;
        world.RazeArea(p, 6.0, keepRemarkable: false);
        main.StepTicks(1);
        Expect(razePrice > 0 && beforeRaze - ledger.Balance == razePrice, $"zone rasée : annoncé {razePrice}, débité {beforeRaze - ledger.Balance}");

        // 3. Confirmation du découvert : une seule fois par passage ; refus par la limite : un message de Birdie.
        var prompts = finances.Prompts;
        var over = ledger.Balance + 1;
        Expect(!finances.Allow(over) && finances.PromptOpen && finances.Prompts == prompts + 1, "le passage sous zéro n'ouvre pas la confirmation");
        Expect(!finances.Allow(over) && finances.Prompts == prompts + 1, "la confirmation s'ouvre deux fois");
        Press(UiIds.Finance.OverdraftNo, failures);
        Expect(!finances.PromptOpen && !finances.Allow(over) && finances.Prompts == prompts + 2, "après refus, la commande suivante devrait redemander");
        Press(UiIds.Finance.OverdraftYes, failures);
        Expect(!finances.PromptOpen && finances.Allow(over) && finances.Allow(over) && finances.Prompts == prompts + 2, "après accord, le passage ne redemande pas");
        Expect(finances.Allow(1) && !finances.Allow(over) && finances.Prompts == prompts + 3, "un achat resté positif devrait annuler l'accord");
        finances.DismissPrompt();

        var refused = ledger.Refused;
        for (var i = 0; i < 60 && ledger.Refused == refused; i++)
        {
            world.RaiseTerrain(new Point2(grid.CellsX * grid.CellSize * 0.5, grid.CellsZ * grid.CellSize * 0.5), 40.0, i % 2 == 0 ? 2.0 : -2.0);
            main.StepTicks(1);
        }

        if (world.OpenHoles >= 3)
        {
            Expect(ledger.Refused > refused && main.Birdie.Chirps.Any(c => c.Tag == "#finances"), $"achat refusé ({ledger.Refused - refused}) sans message Birdie");
        }
        else
        {
            // Moins de trois trous ouverts : la sim ne refuse rien ; on lève l'événement à la main pour vérifier le message.
            finances.OnRefused(LedgerCategory.Relief, 5_000_000);
            Expect(main.Birdie.Chirps.Any(c => c.Tag == "#finances"), "un refus de la limite n'écrit pas dans Birdie");
        }

        main.Camera.Follow(followed);
        main.Bar.Select(ToolFamily.None, toggle: false);
        GD.Print($"SMOKE finances : pastille, panneau, prix du coup, découvert ({finances.Prompts} confirmations), Annuler et Ctrl+Z, refus Birdie ({failures.Count} échec)");
    }

    /// <summary>
    /// C13 critère 6 : Gazon, Ligue et Votre pro s'ouvrent par leur bouton et s'empilent de droite à gauche sans se fermer ; Échap ferme le
    /// dernier ouvert, puis le précédent ; la croix ferme le sien, même au milieu de la pile.
    /// </summary>
    private void CheckPanels(List<string> failures)
    {
        var main = _main!;
        var stack = main.Panels;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"panneaux : {what}");
            }
        }

        UiPanel Panel(string id) => (UiPanel)main.Ui.Find(id)!;
        bool Open(params string[] ids) => stack.Opened.Select(p => p.Name.ToString()).SequenceEqual(ids) && ids.All(id => Panel(id).IsVisibleInTree());

        Expect(stack.Opened.Count == 0, "la pile n'est pas vide au départ");
        Press(UiIds.Tool.Turf, failures);
        Press(UiIds.Round.League, failures);
        Press(UiIds.Round.Pro, failures);
        Expect(Open(UiIds.Panel.Turf, UiIds.Panel.League, UiIds.Panel.Pro) && main.Turf.IsOpen && main.League?.IsOpen == true && main.ProPanel?.IsOpen == true, "les trois ne s'empilent pas");
        Expect(Panel(UiIds.Panel.Pro).GetIndex() < Panel(UiIds.Panel.League).GetIndex() && Panel(UiIds.Panel.League).GetIndex() < Panel(UiIds.Panel.Turf).GetIndex(), "le dernier ouvert n'est pas à gauche des autres");
        PushKey(Key.Escape);
        Expect(Open(UiIds.Panel.Turf, UiIds.Panel.League), "Échap ne ferme pas Votre pro, le dernier ouvert");
        PushKey(Key.Escape);
        Expect(Open(UiIds.Panel.Turf), "un second Échap ne ferme pas la Ligue");
        Press(UiIds.Round.League, failures);
        Press(UiIds.Round.Pro, failures);
        Panel(UiIds.Panel.League).CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
        Expect(Open(UiIds.Panel.Turf, UiIds.Panel.Pro), "la croix de la Ligue ne ferme pas la sienne seule");
        Panel(UiIds.Panel.Turf).CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
        PushKey(Key.Escape);
        Expect(stack.Opened.Count == 0 && !main.Turf.IsOpen && main.ProPanel?.IsOpen != true, "la croix du Gazon puis Échap ne vident pas la pile");
        GD.Print($"SMOKE panneaux : trois empilés, Échap et croix ({failures.Count} échec)");
    }

    /// <summary>
    /// C13 critère 10 : Birdie reçoit le message de sauvegarde (F5) et un avis de fin de saison de la ligue ; le compteur de non lus du bouton
    /// monte à chacun et retombe à zéro quand le bouton ouvre le fil ; un clic sur l'avis d'un membre sur le parcours suit son golfeur.
    /// </summary>
    private void CheckBirdie(List<string> failures)
    {
        var main = _main!;
        var birdie = main.Birdie;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"birdie : {what} (non lus {birdie.Unread}, messages {birdie.Chirps.Count})");
            }
        }

        var unread = birdie.Unread;
        var badge = (Label)main.Ui.Find(UiIds.Hud.BirdieUnread)!;
        Expect(!birdie.IsOpen && main.Ui.Find(UiIds.Round.Birdie) is BaseButton { Disabled: false }, "fil ouvert ou bouton grisé au départ");
        PushKey(Key.F5);
        Expect(birdie.Chirps[^1].Text.Contains("sauvegardée") && birdie.Unread == unread + 1, "F5 ne poste pas la sauvegarde");

        var league = main.World.League!;
        var golfer = main.World.Groups.SelectMany(g => g.Golfers).FirstOrDefault(g => g.MemberId >= 0);
        var member = league.Members[golfer?.MemberId ?? 0];
        main.League!.Announce(new Golferoo.Sim.League.LeagueEvent(league.Year, Golferoo.Sim.League.LeagueEventType.Champion, member.Id, 0));
        Expect(birdie.Chirps[^1].Text.Contains("Champion") && birdie.Chirps[^1].MemberId == member.Id && birdie.Unread == unread + 2, "l'avis de champion manque");
        Expect(badge.Visible && badge.Text == (unread + 2).ToString(CultureInfo.InvariantCulture) && main.Ui.Find(UiIds.Hud.BirdiePeek)!.Visible, "compteur ou aperçu absent");

        Press(UiIds.Round.Birdie, failures);
        Expect(birdie.IsOpen && main.Panels.Opened.Contains(main.Ui.Find(UiIds.Panel.Birdie)) && birdie.Unread == 0 && !badge.Visible, "l'ouverture ne remet pas le compteur à zéro");
        var row = (Control)main.Ui.Find(UiIds.Hud.BirdieFeed)!.GetChild(0);
        row.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Expect(golfer is null || main.Camera.FollowedId == golfer.Id, "le clic sur l'avis ne suit pas le golfeur du membre");
        Press(UiIds.Round.Birdie, failures);
        Expect(!birdie.IsOpen, "le bouton ne referme pas le fil");

        // Bouton « couper les apparitions » : le message s'ajoute au fil et au compteur sans ouvrir l'aperçu ; rétabli, l'aperçu revient.
        var peek = main.Ui.Find(UiIds.Hud.BirdiePeek)!;
        Press(UiIds.Hud.BirdieMute, failures);
        var (count, before) = (birdie.Chirps.Count, birdie.Unread);
        Expect(birdie.Muted && !peek.Visible, "le bouton ne coupe pas les apparitions ou l'aperçu reste");
        birdie.Post(Birdie.Club, "Message muet", "#test");
        Expect(!peek.Visible && birdie.Unread == before + 1 && birdie.Chirps.Count == count + 1, "un message muet ouvre l'aperçu ou ne compte pas");
        Press(UiIds.Hud.BirdieMute, failures);
        birdie.Post(Birdie.Club, "Message audible", "#test");
        Expect(!birdie.Muted && peek.Visible && birdie.Unread == before + 2, "le bouton ne rétablit pas les apparitions");
        GD.Print($"SMOKE birdie muet : aperçu fermé et compteur {before + 1} en sourdine, aperçu rétabli ensuite ({failures.Count} échec)");
        GD.Print($"SMOKE birdie : sauvegarde et avis de ligue reçus, compteur remis à zéro, suivi {(golfer is null ? "sans golfeur membre" : "vérifié")} ({failures.Count} échec)");
    }

    /// <summary>
    /// C16 critère 9 : une pensée ouvre une bulle au-dessus du golfeur et entre dans sa fiche ; coupée par son bouton, elle n'ouvre plus de bulle mais
    /// reste dans la fiche ; au plus <see cref="Thoughts.MaxBubbles"/> bulles ; l'info-bulle et la fiche s'ouvrent.
    /// </summary>
    private void CheckThoughts(List<string> failures)
    {
        var main = _main!;
        var thoughts = main.Thoughts;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"pensées : {what} (bulles {thoughts.BubbleCount})");
            }
        }

        if (main.World.Visitors.Visitors.Count == 0)
        {
            failures.Add("pensées : le vivier est vide, pas de fiche à ouvrir");
            return;
        }

        var visitor = main.World.Visitors.Visitors[0];
        var group = main.World.SpawnGroup();
        var golfer = group.Golfers[0];
        Expect(!Thoughts.Off && thoughts.BubbleCount == 0, "des bulles avant tout évènement");
        thoughts.Think(golfer.Id, visitor.Id, "Quelle vue magnifique d'ici.", 1);
        Expect(thoughts.BubbleCount == 1 && main.Ui.Find(UiIds.Hud.ThoughtBubble) is not null, "pas de bulle après un évènement");

        thoughts.Tip(golfer, new Vector2(100f, 100f));
        Expect(thoughts.TipVisible && ((Control)main.Ui.Find(UiIds.Hud.GolferTip)!).GetChildCount() > 0, "l'info-bulle ne s'ouvre pas");

        thoughts.OpenCard(visitor.Id);
        Expect(thoughts.CardOpen && thoughts.CardVisitor == visitor.Id && main.Panels.Opened.Contains(main.Ui.Find(UiIds.Panel.Golfer)), "la fiche ne s'ouvre pas");
        Expect(AllText(main.Ui.Find(UiIds.Panel.Golfer)!).Any(t => t.Contains("Quelle vue magnifique")), "la fiche n'a pas la pensée");

        Press(UiIds.Hud.ThoughtsToggle, failures);
        Expect(Thoughts.Off, "le bouton ne coupe pas les bulles");
        var shown = thoughts.BubbleCount;
        thoughts.Think(golfer.Id, visitor.Id, "Je poireaute au départ depuis un bon moment.", -1);
        Expect(thoughts.BubbleCount == shown, "une bulle s'ouvre malgré le réglage");
        Expect(AllText(main.Ui.Find(UiIds.Panel.Golfer)!).Any(t => t.Contains("poireaute")), "la pensée coupée n'est pas dans la fiche");
        Press(UiIds.Hud.ThoughtsToggle, failures);
        Expect(!Thoughts.Off, "le bouton ne rétablit pas les bulles");

        thoughts.ClearBubbles();
        main.Panels.Close((UiPanel)main.Ui.Find(UiIds.Panel.Golfer)!);
        GD.Print($"SMOKE pensées : bulle, info-bulle, fiche et réglage ({failures.Count} échec)");
    }

    /// <summary>
    /// C16 lot 4 : le chemin entier, sans appeler <c>Think</c> : un vrai visiteur arrive, la sim lève un vrai évènement d'humeur (<c>World.MoodChanged</c>), la bulle s'ouvre,
    /// <c>OpenCardAt</c> ouvre sa fiche (niveau, parties, jauge d'humeur à jour) ; puis le plafond de bulles avec de vrais golfeurs, et une bulle cachée qui cède sa place.
    /// </summary>
    private void CheckThoughtsReal(List<string> failures)
    {
        var main = _real!;
        var world = main.World;
        var thoughts = main.Thoughts;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"pensées réelles : {what} (bulles {thoughts.BubbleCount})");
            }
        }

        MoodChange? seen = null;
        world.MoodChanged += c => seen ??= c.Delta != 0 && c.Golfer.VisitorId >= 0 ? c : null;
        for (var i = 0; i < 3 * Golferoo.Sim.Time.Calendar.TicksPerDay / 100 && seen is null; i++)
        {
            world.Step(100);
        }

        if (seen is not { } change)
        {
            failures.Add("pensées réelles : aucun évènement d'humeur en trois jours d'arrivées");
            return;
        }

        Expect(!Thoughts.Off && thoughts.BubbleCount >= 1 && main.Ui.Find(UiIds.Hud.ThoughtBubble) is not null, "la sim a levé un évènement mais aucune bulle ne s'ouvre");

        Vector3 Ground(Golfer g) => new((float)g.Position.X, (float)world.Terrain!.Sample(g.Position.X, g.Position.Z), (float)g.Position.Z);
        main.Camera.SetView(45f, 60f, Ground(change.Golfer));
        Expect(thoughts.OpenCardAt(main.Camera.View!.UnprojectPosition(Ground(change.Golfer))) && thoughts.CardOpen, "OpenCardAt n'ouvre pas la fiche d'un golfeur du vivier");
        if (thoughts.CardOpen)
        {
            var card = main.Ui.Find(UiIds.Panel.Golfer)!;
            var visitor = world.Visitors.Visitors[thoughts.CardVisitor];
            var onCourse = world.Groups.SelectMany(g => g.Golfers).First(g => g.VisitorId == visitor.Id);
            var text = AllText(card).ToList();
            Expect(text.Contains(UiNames.Levels[(int)visitor.Level]), $"la fiche n'affiche pas le niveau {UiNames.Levels[(int)visitor.Level]}");
            Expect(text.Any(t => t.Contains("partie")), "la fiche n'affiche pas les parties jouées");
            var gauge = card.FindChild("MoodGauge", true, false) as UiGauge;
            Expect(gauge?.Value.Text == onCourse.Mood.ToString(CultureInfo.InvariantCulture), $"la jauge de la fiche dit {gauge?.Value.Text} pour une humeur de {onCourse.Mood}");

            // La jauge suit l'humeur quand elle change plus tard (un évènement, ou la baisse tous les quelques trous).
            var shown = onCourse.Mood;
            for (var i = 0; i < 3 * Golferoo.Sim.Time.Calendar.TicksPerDay / 100 && onCourse.Mood == shown && world.Groups.Any(g => g.Golfers.Contains(onCourse)); i++)
            {
                world.Step(100);
            }

            thoughts._Process(0.0);
            if (onCourse.Mood != shown)
            {
                Expect(((UiGauge)card.FindChild("MoodGauge", true, false)!).Value.Text == onCourse.Mood.ToString(CultureInfo.InvariantCulture), "la jauge de la fiche ne suit pas l'humeur");
            }
        }

        // Le plafond, avec de vrais golfeurs : une bulle cachée (golfeur hors de l'écran) cède sa place à un golfeur qu'on voit.
        List<Golfer> Visitors() => [.. world.Groups.SelectMany(g => g.Golfers).Where(g => g.VisitorId >= 0)];
        for (var i = 0; i < Golferoo.Sim.Time.Calendar.TicksPerDay / 100 && Visitors().Count <= Thoughts.MaxBubbles + 3; i++)
        {
            world.Step(100);
        }

        var golfers = Visitors();
        if (golfers.Count <= Thoughts.MaxBubbles + 3)
        {
            failures.Add($"pensées réelles : {golfers.Count} golfeurs du vivier seulement, il en faut plus de {Thoughts.MaxBubbles + 3}");
            return;
        }

        thoughts.ClearBubbles();
        var size = (float)(world.Terrain!.CellsX * world.Terrain.CellSize);
        var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, size), new Vector3(size, 0f, size) };
        var away = corners.MaxBy(c => golfers.Min(g => new Vector2(c.X - (float)g.Position.X, c.Z - (float)g.Position.Z).Length()));
        main.Camera.SetView(45f, 30f, away);
        foreach (var g in golfers.Take(Thoughts.MaxBubbles + 3))
        {
            thoughts.Think(g.Id, g.VisitorId, "Zut.", -1);
        }

        Expect(thoughts.BubbleCount == Thoughts.MaxBubbles, $"{thoughts.BubbleCount} bulles au lieu de {Thoughts.MaxBubbles} au plafond");
        Expect(thoughts.VisibleBubbleCount == 0, "des golfeurs sont à l'écran dans le coin le plus vide : le cas du plafond n'est pas préparé");
        var next = golfers[Thoughts.MaxBubbles + 3];
        main.Camera.SetView(45f, 30f, Ground(next));
        thoughts.Think(next.Id, next.VisitorId, "Enfin un beau coup.", 1);
        Expect(thoughts.BubbleCount == Thoughts.MaxBubbles && thoughts.VisibleBubbleCount == 1, $"une bulle cachée bloque le golfeur qu'on voit ({thoughts.VisibleBubbleCount} bulles visibles)");
        thoughts.ClearBubbles();
        main.Panels.Close((UiPanel)main.Ui.Find(UiIds.Panel.Golfer)!);
        GD.Print($"SMOKE pensées réelles : évènement {change.Event} levé par la sim, bulle, fiche par clic (niveau, parties, humeur), plafond de {Thoughts.MaxBubbles} bulles et bulle cachée ({failures.Count} échec)");
    }

    /// <summary>C16 lot 4 : le rapport affiche les compteurs de la sim (mauvaises réactions d'abord, triées), et la carte se montre avec un disque par réaction gardée.</summary>
    private void CheckReactions(List<string> failures)
    {
        var main = _real!;
        var world = main.World;
        var report = main.ReactionReport;
        void Expect(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"réactions : {what}");
            }
        }

        var holes = world.Terrain!.Course.Count;
        var total = Enumerable.Range(1, holes).Sum(h => Enum.GetValues<MoodEvent>().Sum(e => world.Reactions.Count(h, e) + world.Reactions.Previous(h, e)));
        Expect(total > 0, "la sim n'a compté aucune réaction");
        Expect(Press(main, UiIds.Round.Reactions, failures) && report.IsOpen && main.Panels.Opened.Contains(main.Ui.Find(UiIds.Panel.Reactions)), "le bouton n'ouvre pas le rapport");
        Expect(report.Rows.Sum(r => r.Now + r.Before) == total, $"le rapport totalise {report.Rows.Sum(r => r.Now + r.Before)} réactions, la sim {total}");
        foreach (var hole in report.Rows.GroupBy(r => r.Hole))
        {
            Expect(hole.Select(r => r.Good).SequenceEqual(hole.Select(r => r.Good).OrderBy(g => g)), $"trou {hole.Key} : les mauvaises réactions ne viennent pas d'abord");
            foreach (var side in hole.GroupBy(r => r.Good))
            {
                Expect(side.Select(r => r.Now).SequenceEqual(side.Select(r => r.Now).OrderByDescending(n => n)), $"trou {hole.Key} : réactions mal triées");
            }
        }

        var worst = report.Rows.Where(r => !r.Good).OrderByDescending(r => r.Now).FirstOrDefault();
        var text = AllText(main.Ui.Find(UiIds.Panel.Reactions)!).ToList();
        Expect(worst.Text is null || (text.Contains($"Trou {worst.Hole}") && text.Contains($"×{worst.Now}")), "le rapport n'affiche pas la pire réaction avec son nombre");

        Expect(!report.MapOn, "la carte est montrée avant qu'on la demande");
        Expect(Press(main, UiIds.Hud.ReactionMap, failures) && report.MapOn, "le bouton n'active pas la carte");
        Expect(report.MapDiscs == world.Reactions.Spots.Count && report.MapDiscs > 0, $"{report.MapDiscs} disques pour {world.Reactions.Spots.Count} réactions gardées");
        Press(main, UiIds.Hud.ReactionMap, failures);
        Expect(!report.MapOn, "le bouton ne cache pas la carte");
        Press(main, UiIds.Round.Reactions, failures);
        Expect(!report.IsOpen, "le bouton ne referme pas le rapport");
        GD.Print($"SMOKE réactions : rapport de {report.Rows.Count} lignes pour {total} réactions comptées, carte de {world.Reactions.Spots.Count} disques ({failures.Count} échec)");
    }

    private static IEnumerable<string> AllText(Node root) =>
        (root is Label label ? [label.Text] : Enumerable.Empty<string>()).Concat(root.GetChildren().SelectMany(AllText));

    /// <summary>
    /// C13 critère 11 : à l'échelle 1,5 en 1080p, l'écran logique fait 1280×720 ; les deux étages de la barre, le tiroir Terrain et deux
    /// panneaux ouverts (Ligue, Birdie) y tiennent sans déborder.
    /// </summary>
    private void CheckUiScale(List<string> failures)
    {
        var main = _main!;
        var window = GetTree().Root;
        var (oldSize, oldMode, oldFactor) = (window.ContentScaleSize, window.ContentScaleMode, window.ContentScaleFactor);
        (window.ContentScaleSize, window.ContentScaleMode, window.ContentScaleFactor) = (new Vector2I(1920, 1080), Window.ContentScaleModeEnum.CanvasItems, 1.5f);
        main.Bar.Select(ToolFamily.Terrain, toggle: false);
        main.League!.Toggle();
        main.Birdie.Toggle();
        var screen = GetViewport().GetVisibleRect().Size;
        var tiers = new[] { "BarTop", "BarBottom" }.Select(n => main.Bar.GetNode<Control>(n).GetCombinedMinimumSize().X).ToArray();
        var dock = main.Panels.Dock.GetCombinedMinimumSize();
        if (screen.DistanceTo(new Vector2(1280f, 720f)) > 1f || tiers.Any(w => w > screen.X) || dock.X > screen.X - 32f || dock.Y > screen.Y - BottomBar.Height - 28f)
        {
            failures.Add($"échelle 1,5 : écran {screen}, étages {string.Join(" / ", tiers)}, colonne {dock}");
        }

        var worst = (UiPill)main.Ui.Find(UiIds.Hud.Money)!;
        worst.SetText(UiMoney.PillText(-123_456_700, -12_345_600));
        var wide = new[] { "BarTop", "BarBottom" }.Select(n => main.Bar.GetNode<Control>(n).GetCombinedMinimumSize().X).ToArray();
        var club = (Label)((UiPill)main.Ui.Find(UiIds.Hud.Club)!).Label;
        if (wide.Any(w => w > screen.X) || club.TextOverrunBehavior != TextServer.OverrunBehavior.TrimEllipsis)
        {
            failures.Add($"échelle 1,5 : un solde de -1,23 M$ fait déborder la barre ({string.Join(" / ", wide)}) ou le nom du club n'a pas de points de suspension");
        }

        main.Bar.Sync();
        GD.Print($"SMOKE echelle 1,5 : écran {screen}, étages {string.Join(" / ", tiers)}, colonne {dock}");
        main.Birdie.Toggle();
        main.League.Toggle();

        // Décision de Guillaume : à 150 %, Gazon 240 + trois panneaux de 380 ne tiennent pas dans 1 248 px ; le plus ancien se replie, les autres restent dépliés.
        Press(UiIds.Tool.Turf, failures);
        Press(UiIds.Round.League, failures);
        Press(UiIds.Round.Pro, failures);
        Press(UiIds.Round.Birdie, failures);
        var opened = main.Panels.Opened;
        var folded = opened.Where(p => p.Folded).Select(p => p.Name.ToString()).ToList();
        var column = main.Panels.Dock.GetCombinedMinimumSize();
        var offscreen = column.X > screen.X - 32f ? 1 : 0; // la hauteur se juge sur la mise en page réelle, dans la capture (hors_ecran)
        if (opened.Count != 4 || !folded.SequenceEqual([UiIds.Panel.Turf]) || offscreen != 0)
        {
            failures.Add($"repli : {opened.Count} panneaux, repliés {string.Join(",", folded)}, colonne {column}");
        }

        GD.Print($"SMOKE repli 1,5 : {opened.Count} panneaux, repliés [{string.Join(",", folded)}], colonne {column.X} px sur {screen.X - 32f}, hors_ecran={offscreen}");
        var turf = opened[0];
        turf.EmitSignal(UiPanel.SignalName.UnfoldRequested);
        if (turf.Folded || opened.Count(p => p.Folded) != 1 || opened.Single(p => p.Folded).Name != UiIds.Panel.League)
        {
            failures.Add("repli : un clic sur l'onglet ne le déplie pas en repliant le plus ancien des autres");
        }

        // Fermer un seul panneau redonne la place : la Ligue, repliée, se déplie.
        main.Panels.Close(opened[^1]);
        if (opened.Count != 3 || opened.Any(p => p.Folded))
        {
            failures.Add($"repli : fermer Birdie ne déplie pas la Ligue ({opened.Count(p => p.Folded)} replié)");
        }

        GD.Print($"SMOKE repli fermeture : un panneau fermé, {opened.Count(p => p.Folded)} replié");
        foreach (var panel in opened.ToList())
        {
            main.Panels.Close(panel);
        }

        if (opened.Count != 0 || turf.Folded)
        {
            failures.Add("repli : fermer ne remet pas les panneaux dépliés");
        }

        main.Bar.Select(ToolFamily.None, toggle: false);
        (window.ContentScaleSize, window.ContentScaleMode, window.ContentScaleFactor) = (oldSize, oldMode, oldFactor);
    }

    /// <summary>C13 critère 7 : aucun libellé, info-bulle ou texte de l'interface (et du tutoriel, <paramref name="extra"/>) ne cite une touche.</summary>
    private static void CheckNoKeys(Main main, List<string> failures, IEnumerable<string> extra)
    {
        var texts = new List<string>(extra);
        foreach (var node in main.Ui.FindChildren("*", "", recursive: true, owned: false))
        {
            if (node is Control control)
            {
                texts.Add(control.TooltipText);
            }

            texts.Add(node switch
            {
                Label label => label.Text,
                Button button => button.Text,
                RichTextLabel rich => rich.Text,
                _ => "",
            });
        }

        foreach (var text in texts.Where(t => t.Length > 0).Distinct())
        {
            if (text.Contains("(T", StringComparison.Ordinal) || text.Contains("[b]F1", StringComparison.Ordinal) || text.Contains("touche", StringComparison.OrdinalIgnoreCase)
                || System.Text.RegularExpressions.Regex.IsMatch(text, @"\[b\](\\?.|F\d+)\[/b\]"))
            {
                failures.Add($"texte qui cite une touche : « {text} »");
            }
        }

        GD.Print($"SMOKE touches : {texts.Count} textes sans touche citée");
    }

    /// <summary>C13 critère 5 : un carré de fairway et l'arbre planté dessus ; un trait de bulldozer au même point les efface.</summary>
    private void CheckBulldozer(List<string> failures)
    {
        var main = _main!;
        var world = main.World;
        var grid = world.Terrain!;
        if (FreeSpot(world, grid) is not { } p)
        {
            failures.Add("bulldozer : aucun emplacement libre");
            return;
        }

        world.PaintSurface(p, 8, Surface.Fairway);
        main.StepTicks(1);
        world.PlantTree(p.X, p.Z, TreeSpeciesId.Oak, TreePlantStage.Mound);
        main.StepTicks(1);
        var tree = world.Trees.Trees[^1];
        if (grid.SurfaceAt(p.X, p.Z) != Surface.Fairway || Math.Abs(tree.X - p.X) > 0.01 || Math.Abs(tree.Z - p.Z) > 0.01)
        {
            failures.Add("bulldozer : le fairway ou l'arbre de départ n'est pas posé");
            return;
        }

        var followed = main.Camera.FollowedId;
        main.Camera.CenterOn(new Vector3((float)p.X, 0f, (float)p.Z));
        Press(UiIds.Tool.Bulldozer, failures);
        var brush = main.Brush;
        if (!brush.Active || !brush.BulldozerMode)
        {
            failures.Add("bulldozer : le bouton ne prend pas le bulldozer");
        }

        brush.SetRadius(6f);
        var at = main.Camera.View!.UnprojectPosition(new Vector3((float)p.X, (float)grid.Sample(p.X, p.Z), (float)p.Z));
        PushClick(at, pressed: true);
        var posted = brush.PickAt(at, out var hit) && brush.Apply(hit);
        main.StepTicks(1);
        PushClick(at, pressed: false);
        var cleared = grid.SurfaceAt(p.X, p.Z) == Surface.Rough && !world.Trees.TryGet(tree.Id, out _);
        if (!posted || !cleared)
        {
            failures.Add($"bulldozer : trait posé {posted} au point visé {hit} (écran {at}), surface {grid.SurfaceAt(p.X, p.Z)}, arbre présent {world.Trees.TryGet(tree.Id, out _)}");
        }

        main.Camera.Follow(followed);
    }

    /// <summary>
    /// C13 lot 1 (critère 1) : la racine « Ui » porte <see cref="UiTheme.Theme"/> ; chaque composant s'y construit ; le style de panneau
    /// et la police d'un bouton viennent du thème (palette et polices du design) ; <see cref="UiRoot.Find"/> retrouve un identifiant ; la croix ferme.
    /// </summary>
    private void CheckUi(List<string> failures)
    {
        var root = UiTheme.CreateRoot(this);
        var layer = root.GetParent<CanvasLayer>();
        if (layer.Name != "Ui" || root.Name != "UiRoot" || root.Theme != UiTheme.Theme)
        {
            failures.Add($"racine d'interface : calque {layer.Name}, racine {root.Name}, thème du design {root.Theme == UiTheme.Theme}");
        }

        var panel = UiPanel.Create(UiIds.Panel.Pro, "Votre pro", "figurine", 320f);
        var tool = UiToolButton.Create(UiIds.Tool.Terrain, "toolBrush", "Terrain", "Peindre les surfaces.", ("Coût", "—"));
        var thumb = UiThumb.Create(UiIds.Thumb.Fairway, "Fairway", Colors.DarkGreen, Colors.LightGreen, Colors.Black);
        panel.Content.AddChild(UiGauge.Create("Gauge", "Puissance", 0.6, "60"));
        panel.Content.AddChild(UiTable.Create("Table", ["Membre", "Points"], [["Votre pro", "35"]]));
        panel.Content.AddChild(UiPill.Create("Pill", "—", "coin"));
        panel.Content.AddChild(tool);
        panel.Content.AddChild(thumb);
        root.AddChild(panel);
        var closed = false;
        panel.Closed += () => closed = true;

        var style = panel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (style is null || style != UiTheme.Theme.GetStylebox("panel", "UiPanel") || style.BgColor != UiTheme.Glass || style.CornerRadiusTopLeft != UiTheme.GlassRadius)
        {
            failures.Add($"le style du panneau ne vient pas du thème (fond {style?.BgColor}, coin {style?.CornerRadiusTopLeft})");
        }

        foreach (var (button, kind) in new[] { (tool, "outil"), (panel.CloseButton, "croix") })
        {
            if (button.GetThemeFont("font") is not FontFile font || font != UiTheme.Fredoka || !font.GetFontName().Contains("Fredoka"))
            {
                failures.Add($"la police du bouton ({kind}) ne vient pas du thème : {button.GetThemeFont("font")?.GetFontName()}");
            }
        }

        if (panel.Title.GetThemeFont("font") != UiTheme.Fredoka || panel.Title.GetThemeFontSize("font_size") != UiTheme.TitleSize
            || panel.GetThemeFont("font") is not FontFile body || !body.GetFontName().Contains("Nunito"))
        {
            failures.Add("titre en Fredoka et texte en Nunito attendus");
        }

        if (root.Find(UiIds.Tool.Terrain) != tool || root.Find(UiIds.Thumb.Fairway) != thumb || root.Find("Absent") is not null)
        {
            failures.Add("UiRoot.Find ne retrouve pas les identifiants");
        }

        if (tool._MakeCustomTooltip(tool.TooltipText) is not UiTooltip tip || tip.GetChildCount() != 3)
        {
            failures.Add("l'info-bulle du bouton d'outil n'a pas titre, phrase et coût");
        }
        else
        {
            tip.Free();
        }

        panel.CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
        if (!closed)
        {
            failures.Add("la croix du panneau n'émet pas Closed");
        }

        layer.QueueFree();
    }

    /// <summary>
    /// C25 critère 9 : la fiche du pro montre pour les 17 habiletés le niveau, la barre d'XP et l'effet actuel et suivant ; un niveau gagné
    /// publie un avis Birdie qui nomme l'habileté, le niveau et l'effet.
    /// </summary>
    private void CheckProSheet(List<string> failures)
    {
        var main = _main!;
        var panel = main.ProPanel;
        if (panel is null || main.World.Pro is not { } skills)
        {
            failures.Add("fiche du pro : absente");
            return;
        }

        panel.Toggle();
        var shown = Enum.GetValues<Golferoo.Sim.Pro.ProSkill>()
            .Count(s => main.Ui.Find($"ProSkill{s}") is { TooltipText: var tip } && tip.Contains("niv. ", StringComparison.Ordinal) && main.Ui.Find($"ProEffect{s}") is Label { Text: var effect } && System.Text.RegularExpressions.Regex.IsMatch(effect, @"^[−+]?\d+ % "));
        if (shown != Golferoo.Sim.Pro.ProSkills.Count)
        {
            failures.Add($"fiche du pro : {shown} habiletés sur {Golferoo.Sim.Pro.ProSkills.Count} avec ligne, effet et info-bulle (le nombre de lignes visibles se compte dans les captures, où la mise en page est faite)");
        }

        var before = main.Birdie.Chirps.Count;
        skills.Gain(Golferoo.Sim.Pro.ProSkill.LongIrons, skills.XpForLevel(1), league: false, year: main.World.PlayYear);
        panel._Process(0.0);
        var level = skills.Level(Golferoo.Sim.Pro.ProSkill.LongIrons);
        var expected = Golferoo.Sim.Pro.ProEffects.LevelMessage(Golferoo.Sim.Pro.ProSkill.LongIrons, level);
        // Le pro joue pendant la fumée : un autre niveau gagné au même moment poste aussi son avis, d'où « parmi les nouveaux ».
        if (level < 1 || !main.Birdie.Chirps.Skip(before).Any(c => c.Text == expected))
        {
            failures.Add($"fiche du pro : pas d'avis Birdie au niveau gagné (niveau {level}, attendu « {expected} »)");
        }

        panel.Toggle();
    }

    /// <summary>C08 : le pro attend son coup, le mode visée s'ouvre en pause avec une trajectoire prévue, et « Jouer » envoie le coup.</summary>
    private void CheckProAim(List<string> failures)
    {
        var world = _main!.World;
        world.StartProRound();
        for (var i = 0; i < 20_000 && world.AwaitingPro is null; i++)
        {
            _main.StepTicks(1);
        }

        if (world.AwaitingPro is not { } pro)
        {
            failures.Add("le pro n'attend jamais son coup");
            return;
        }

        _main.ProAim.Sync();
        if (!_main.ProAim.Active || !_main.Paused || _main.ProAim.Preview.Trajectory is not { Count: > 1 })
        {
            failures.Add($"mode visée : actif={_main.ProAim.Active}, pause={_main.Paused}, trajectoire prévue absente");
            return;
        }

        _main.ProAim.Play();
        _main.ProAim.Sync();
        if (_main.ProAim.Active || _main.Paused)
        {
            // Bogue vu par Guillaume : la visée se rouvrait avant le tick du coup et remettait la cible par défaut.
            failures.Add("après « Jouer », la visée se rouvre avant que le coup parte");
        }

        // On compte les envols plutôt que les coups : une balle à l'eau ajoute une pénalité au compte (le vent varie avec l'heure de la fumée).
        var flights = 0;
        for (var i = 0; i < 200; i++)
        {
            var wasInFlight = pro.State == GolferState.BallInFlight;
            _main.StepTicks(1);
            if (!wasInFlight && pro.State == GolferState.BallInFlight)
            {
                flights++;
            }
        }

        if (flights != 1)
        {
            failures.Add($"après « Jouer », le pro frappe {flights} fois au lieu d'une");
        }
    }

    /// <summary>Outil arbres : plante, rase, fait tomber et dégage un arbre ; la couche compte toujours les arbres de la sim (debout, tombés, souches).</summary>
    private void CheckTreeTools(TreeLayer trees, List<string> failures)
    {
        var world = _main!.World;
        var grid = world.Terrain!;
        var tool = _main.TreeTool;
        var spot = FreeSpot(world, grid);
        if (spot is not { } p)
        {
            failures.Add("outil arbres : aucun emplacement libre pour planter");
            return;
        }

        var before = world.Trees.Count;
        tool.PlantAt(p, TreePlantStage.Mound);
        _main.StepTicks(1);
        if (world.Trees.Count != before + 1 || trees.TotalCount != world.Trees.Count)
        {
            failures.Add($"outil arbres, plantation : sim {world.Trees.Count} (attendu {before + 1}), couche {trees.TotalCount}");
            return;
        }

        var planted = world.Trees.Trees[^1];
        if (-world.Ledger.Current(LedgerCategory.Trees) != world.Economy.TreeMound || !tool.Select(planted.Id) || !tool.Describe().Contains("ans"))
        {
            failures.Add($"outil arbres : dépenses {-world.Ledger.Current(LedgerCategory.Trees)}, panneau « {tool.Describe()} »");
        }

        var standing = trees.TreeCount;
        if (!tool.FellSelected())
        {
            failures.Add("outil arbres : FellSelected refusé");
        }

        _main.StepTicks(1);
        if (trees.FallenCount != 1 || trees.TreeCount != standing - 1 || trees.FallingCount != 1 || trees.TotalCount != world.Trees.Count)
        {
            failures.Add($"outil arbres, chute : tombés {trees.FallenCount}, debout {trees.TreeCount} (avant {standing}), chutes {trees.FallingCount}");
        }

        if (!tool.ClearSelected())
        {
            failures.Add("outil arbres : ClearSelected refusé");
        }

        _main.StepTicks(1);
        if (trees.StumpCount != 1 || trees.FallenCount != 0 || trees.TotalCount != world.Trees.Count)
        {
            failures.Add($"outil arbres, dégagement : souches {trees.StumpCount}, tombés {trees.FallenCount}");
        }

        var second = FreeSpot(world, grid);
        tool.PlantAt(second ?? p, TreePlantStage.Sapling);
        _main.StepTicks(1);
        var sapling = world.Trees.Trees[^1];
        tool.Select(sapling.Id);
        if (!tool.RazeSelected())
        {
            failures.Add("outil arbres : RazeSelected refusé");
        }

        _main.StepTicks(1);
        if (world.Trees.TryGet(sapling.Id, out _) || trees.TotalCount != world.Trees.Count)
        {
            failures.Add($"outil arbres, abattage : sim {world.Trees.Count}, couche {trees.TotalCount}");
        }

        var e = world.Economy;
        var expected = e.TreeMound + e.TreeClearFallen + e.TreeSapling + e.TreeRaze;
        if (-world.Ledger.Current(LedgerCategory.Trees) != expected)
        {
            failures.Add($"outil arbres : dépenses {-world.Ledger.Current(LedgerCategory.Trees)}, attendu {expected}");
        }

        tool.SetActive(false);
    }

    /// <summary>Point de surface plantable (ni eau, green, tee, bunker, sous-bois) à plus de 15 m de tout arbre, ou null.</summary>
    private static Point2? FreeSpot(World world, TerrainGrid grid)
    {
        var near = new List<Golferoo.Sim.Trees.Tree>();
        for (var z = 10.0; z < grid.CellsZ * grid.CellSize - 10.0; z += 10.0)
        {
            for (var x = 10.0; x < grid.CellsX * grid.CellSize - 10.0; x += 10.0)
            {
                if (grid.SurfaceAt(x, z) is not (Surface.Fairway or Surface.Rough))
                {
                    continue;
                }

                near.Clear();
                world.Trees.Query(x, z, 15.0, near);
                if (near.Count == 0)
                {
                    return new Point2(x, z);
                }
            }
        }

        return null;
    }

    /// <summary>Maillages générés par code : chacun construit (ligne ASSET déjà imprimée), non vide et dans son budget de triangles.</summary>
    private static void CheckAssets(List<string> failures)
    {
        _ = ProceduralMeshes.Golfer();
        _ = ProceduralMeshes.Ball();
        _ = ProceduralMeshes.Flag();
        _ = ProceduralMeshes.TeeMarker();
        for (var v = 0; v < ProceduralMeshes.TreeVariants; v++)
        {
            _ = ProceduralMeshes.Tree(v);
        }

        var budgets = new Dictionary<string, int>
        {
            ["Golfer"] = ProceduralMeshes.GolferBudget,
            ["Ball"] = ProceduralMeshes.BallBudget,
            ["Flag"] = ProceduralMeshes.FlagBudget,
            ["TeeMarker"] = ProceduralMeshes.TeeMarkerBudget,
        };
        for (var v = 0; v < ProceduralMeshes.TreeVariants; v++)
        {
            budgets[$"Tree{v}"] = ProceduralMeshes.TreeBudget;
        }

        foreach (var (name, budget) in budgets)
        {
            var stat = ProceduralMeshes.Stats.FirstOrDefault(s => s.Name == name);
            if (stat.Name is null || stat.Triangles < 1)
            {
                failures.Add($"asset {name} absent ou vide");
            }
            else if (stat.Triangles > budget)
            {
                failures.Add($"asset {name} : {stat.Triangles} triangles, budget {budget}");
            }
        }
    }

    /// <summary>Budget de triangles de chaque famille de <c>res://assets/</c> (docs/guide-ia-assets-3d.md).</summary>
    private static readonly Dictionary<string, int> FamilyBudgets = new()
    {
        ["trees"] = 800,
        ["bushes"] = 150,
        ["rocks"] = 120,
        ["furniture"] = 300,
        ["maintenance"] = 800,
        ["vehicles"] = 800,
        ["buildings"] = 3000,
        ["structures"] = 600,
        ["golfers"] = 2500, // le golfeur animé de C23 (tools/blender/golfer_anim.py)
        ["ui"] = 0, // polices et icônes de l'interface (C13) : aucun modèle 3D admis.
    };

    /// <summary>
    /// Chaque .glb de <c>res://assets/&lt;famille&gt;/</c> (scripts de tools/blender/) : famille connue, importé par Godot,
    /// un seul MeshInstance3D, pied à y = 0 et dans le budget de sa famille.
    /// </summary>
    private static void CheckAssetLibrary(List<string> failures)
    {
        var count = 0;
        foreach (var family in DirAccess.GetDirectoriesAt("res://assets"))
        {
            if (!FamilyBudgets.TryGetValue(family, out var budget))
            {
                failures.Add($"res://assets/{family} : famille sans budget");
                continue;
            }

            foreach (var file in DirAccess.GetFilesAt($"res://assets/{family}").Where(f => f.EndsWith(".glb", StringComparison.Ordinal)))
            {
                var path = $"res://assets/{family}/{file}";
                var root = GD.Load<PackedScene>(path)?.Instantiate();
                if (root is null)
                {
                    failures.Add($"{path} : scène illisible");
                    continue;
                }

                var meshes = root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Where(m => m.Mesh is not null).ToList();
                if (meshes.Count != 1)
                {
                    failures.Add($"{path} : {meshes.Count} MeshInstance3D au lieu d'un seul");
                }
                else
                {
                    var mesh = meshes[0].Mesh;
                    var triangles = mesh.GetFaces().Length / 3;
                    var aabb = mesh.GetAabb();
                    if (triangles < 1 || triangles > budget)
                    {
                        failures.Add($"{path} : {triangles} triangles, budget {budget}");
                    }

                    if (Mathf.Abs(aabb.Position.Y) > 0.01f)
                    {
                        failures.Add($"{path} : pied à y = {aabb.Position.Y:0.###} au lieu de 0");
                    }

                    count++;
                }

                root.Free();
            }
        }

        GD.Print($"ASSETS {count} modèles .glb vérifiés");
    }

    /// <summary>Retouche par code : peinture d'eau puis montée de relief, vérifiées dans la simulation et dans le rendu.</summary>
    private void CheckBrush(List<string> failures)
    {
        var world = _main!.World;
        var grid = world.Terrain!;
        var center = new Point2(grid.CellsX * grid.CellSize * 0.5, grid.CellsZ * grid.CellSize * 0.5);
        var cx = grid.CellXOf(center.X);
        var cz = grid.CellZOf(center.Z);

        var chunksBefore = _main.TerrainView.RebuiltChunks;
        world.PaintSurface(center, 6.0, Surface.Water);
        _main.StepTicks(1);
        if (grid.GetCellSurface(cx, cz) != Surface.Water)
        {
            failures.Add("PaintSurface n'a pas changé la surface");
        }

        var h0 = grid.Sample(center.X, center.Z);
        world.RaiseTerrain(center, 6.0, 1.0);
        _main.StepTicks(1);
        if (grid.Sample(center.X, center.Z) <= h0)
        {
            failures.Add("RaiseTerrain n'a pas monté le relief");
        }

        if (_main.TerrainView.RebuiltChunks <= chunksBefore)
        {
            failures.Add("RebuildChunks n'a reconstruit aucun bloc");
        }

        var brush = _main.Brush;
        brush.SetActive(true);
        brush.SelectSurface(Surface.Bunker);
        brush.ChangeRadius(4f);
        if (brush.PaintedSurface != Surface.Bunker || Math.Abs(brush.Radius - 10f) > 0.01f)
        {
            failures.Add($"pinceau inattendu : {brush.PaintedSurface}, rayon {brush.Radius}");
        }

        if (!Brush.TryPick(grid, new Vector3((float)center.X, 200f, (float)center.Z), Vector3.Down, 400f, out var hit)
            || Mathf.Abs(hit.Y - (float)grid.Sample(center.X, center.Z)) > 0.05f)
        {
            failures.Add("la projection du rayon sur le terrain échoue");
        }

        brush.SetActive(false);
    }
}
