using System.Globalization;
using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Game.Ui;
using Golferoo.Sim;
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
        _tutorial = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(_tutorial);
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
        for (var turn = 0; turn < 200 && Golferoo.Sim.Tutorial.TutorialSteps.Current(world) is var step && step != Golferoo.Sim.Tutorial.TutorialStep.Done; turn++)
        {
            tutorial._Process(0);
            texts.Add(tutorial.Text);
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
        if (!world.HoleOpen || tutorial.Phase != Golferoo.Game.Tutorial.TutorialPanel.Mode.Finished || tutorial.Highlighted != UiIds.Tutorial.Golfers)
        {
            failures.Add($"tutoriel : pas fini aux boutons (trou ouvert {world.HoleOpen}, phase {tutorial.Phase}, étape {Golferoo.Sim.Tutorial.TutorialSteps.Current(world)})");
        }

        if (main.Ui.Find(UiIds.Tutorial.Ring) is not { Visible: true })
        {
            failures.Add("tutoriel : l'anneau jaune ne se voit pas");
        }

        Press(main, UiIds.Tutorial.Golfers, failures);
        if (world.Groups.Count == 0 || tutorial.Phase != Golferoo.Game.Tutorial.TutorialPanel.Mode.Off)
        {
            failures.Add("tutoriel : « Lancer des golfeurs » ne lance personne ou laisse la bulle");
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
        CheckUi(failures);
        CheckBar(failures);
        if (_main.GetNodeOrNull<Label>("TickLabel") is { Visible: true } && PushKey(Key.F12) && _main.GetNode<Label>("TickLabel").Visible)
        {
            failures.Add("débogage : F12 ne cache pas le libellé");
        }

        return failures;
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
        foreach (var id in new[] { UiIds.Tool.Buildings, UiIds.Tool.Amenities, UiIds.Round.Finances })
        {
            Expect(ui.Find(id) is BaseButton { Disabled: true }, $"{id} devrait être grisé");
        }

        main.Bar.Select(ToolFamily.None, toggle: false);
        Expect(!brush.Active && !trees.Active && !place.Active && !ui.Find(UiIds.Drawer.Root)!.Visible, "rien de pris : le tiroir reste ouvert");
        GD.Print($"SMOKE barre : outils, vignettes, actions, vitesses, caméra par leurs boutons ({failures.Count} échec)");
        CheckPanels(failures);
        CheckBirdie(failures);
        CheckUiScale(failures);
        CheckNoKeys(main, failures, []);
        CheckBirdieDays(failures);
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
        var (morning, seasonal) = (birdie.Chirps.Count(c => c.Tag == "#météo"), birdie.Chirps.Count(c => c.Tag == "#saison"));
        main.SkipToNextMonth();
        if (birdie.Chirps[^1].Tag != "#météo" || birdie.Chirps.Count(c => c.Tag == "#météo") != morning + 1)
        {
            failures.Add("birdie : un nouveau matin ne publie pas la météo");
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

        GD.Print($"SMOKE birdie jours : météo au matin, saison {season} puis {main.World.Season} annoncée (derniers messages {string.Join(",", latest)})");
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
