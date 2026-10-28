using System.Globalization;
using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Game.Ui;
using Golferoo.Sim;
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
        var scene = GD.Load<PackedScene>("res://Main.tscn");
        _main = scene.Instantiate<Main>();
        AddChild(_main);
    }

    public override void _Process(double delta)
    {
        _frames++;
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

    private List<string> CheckTutorial()
    {
        var failures = new List<string>();
        var main = _tutorial!;
        var world = main.World;
        var tee = new Point2(28, 244);
        var green = new Point2(268, 56);
        if (world.HoleOpen || main.Tutorial is null || main.Tutorial.Phase != Golferoo.Game.Tutorial.TutorialPanel.Mode.Welcome)
        {
            failures.Add("tutoriel : terre vierge ouverte ou bulle absente");
            return failures;
        }

        foreach (var p in world.Terrain!.IsolatedTrees)
        {
            world.RazeTree(world.Trees.Trees.First(t => t.X == p.X && t.Z == p.Z).Id);
        }

        world.PlantTree(64, 208, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.PaintSurface(tee, 6, Surface.Tee);
        foreach (var x in new[] { 100.0, 160.0, 220.0 })
        {
            world.PaintSurface(new Point2(x, 236), 10, Surface.Fairway);
        }

        world.PaintSurface(green, 11, Surface.Green);
        main.StepTicks(1);
        if (!world.PlaceTee(1, tee) || !world.PlaceHole(1, green))
        {
            failures.Add("tutoriel : pose du départ ou du trou refusée");
        }

        main.StepTicks(2);
        if (!world.HoleOpen)
        {
            failures.Add("tutoriel : le trou n'est pas valide après la construction");
        }

        main.SpawnGroup();
        main.StepTicks(20);
        GD.Print($"SMOKE tutoriel : trou valide, étape {Golferoo.Sim.Tutorial.TutorialSteps.Current(world)}");
        return failures;
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

        // C12 : le cartouche « Trou n, par p » s'affiche tant qu'un groupe est suivi.
        var cartouche = _main.GetNodeOrNull<Label>("HoleLabelLayer/HoleLabel");
        if (cartouche is null || !cartouche.Visible || !cartouche.Text.StartsWith("Trou ", StringComparison.Ordinal) || !cartouche.Text.Contains(", par "))
        {
            failures.Add($"cartouche de trou absent ou muet : « {cartouche?.Text} »");
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

        if (_main.GetNodeOrNull<Label>("TickLabel") is not { } hud)
        {
            failures.Add("TickLabel absent de Main.tscn");
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
        return failures;
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

        _main.StepTicks(200);
        if (pro.Strokes != 1)
        {
            failures.Add($"après « Jouer », le pro compte {pro.Strokes} coups au lieu de 1");
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
        if (world.TreeSpending != TreeCosts.Mound || !tool.Select(planted.Id) || !tool.Describe().Contains("ans"))
        {
            failures.Add($"outil arbres : dépenses {world.TreeSpending}, panneau « {tool.Describe()} »");
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

        var expected = TreeCosts.Mound + TreeCosts.ClearFallen + TreeCosts.Sapling + TreeCosts.Raze;
        if (world.TreeSpending != expected)
        {
            failures.Add($"outil arbres : dépenses {world.TreeSpending}, attendu {expected}");
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
        if (!brush.Describe().Contains("Bunker") || !brush.Describe().Contains("10"))
        {
            failures.Add($"panneau du pinceau inattendu : {brush.Describe()}");
        }

        if (!Brush.TryPick(grid, new Vector3((float)center.X, 200f, (float)center.Z), Vector3.Down, 400f, out var hit)
            || Mathf.Abs(hit.Y - (float)grid.Sample(center.X, center.Z)) > 0.05f)
        {
            failures.Add("la projection du rayon sur le terrain échoue");
        }

        brush.SetActive(false);
    }
}
