using Godot;
using Golferoo.Game.Tools;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tests;

/// <summary>
/// Test de fumée headless : charge la scène principale, laisse tourner quelques images,
/// vérifie que la simulation répond, puis quitte avec un code non nul en cas d'échec.
/// La CI exige aussi la ligne « SMOKE OK » : si le C# ne se charge pas, elle n'apparaît jamais.
/// </summary>
public partial class SmokeTest : Node
{
    private const int FramesToRun = 30;

    private Main? _main;
    private int _frames;

    public override void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://Main.tscn");
        _main = scene.Instantiate<Main>();
        AddChild(_main);
    }

    public override void _Process(double delta)
    {
        if (++_frames < FramesToRun)
        {
            return;
        }

        var failures = Check();
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

    private List<string> Check()
    {
        var failures = new List<string>();
        if (_main is null)
        {
            failures.Add("scène principale absente");
            return failures;
        }

        if (_main.GetNodeOrNull<Label>("TickLabel") is null)
        {
            failures.Add("TickLabel absent de Main.tscn");
        }

        if (_main.TerrainView.ChunkCount != 12)
        {
            failures.Add($"blocs de terrain attendus 12 au facteur {_main.Factor}, obtenus {_main.TerrainView.ChunkCount}");
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

        CheckBrush(failures);

        // Même graine, mêmes ticks : la simulation doit être déterministe aussi sous Godot.
        var a = new World(seed: 99);
        var b = new World(seed: 99);
        a.Step(200);
        b.Step(200);
        if (a.Checksum() != b.Checksum())
        {
            failures.Add("somme de contrôle différente à graine égale");
        }

        return failures;
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
