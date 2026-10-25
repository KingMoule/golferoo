using Godot;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Sim;
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
            _main.AdvanceTreesOneYear();
            if (_main.World.Trees.MeanAgeMonths < ageBefore + 11.9)
            {
                failures.Add($"AdvanceYear : âge moyen {ageBefore:F1} → {_main.World.Trees.MeanAgeMonths:F1} mois");
            }

            if (trees.TotalCount != _main.World.Trees.Count)
            {
                failures.Add($"après AdvanceYear, TreeLayer affiche {trees.TreeCount} arbres, la simulation en compte {_main.World.Trees.Count}");
            }

            CheckTreeTools(trees, failures);

            if (trees.SpeciesModelCount != 18)
            {
                failures.Add($"{trees.SpeciesModelCount} modèles d'essences chargés sur 18 (trois essences, plant et adulte, trois variantes)");
            }
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

        return failures;
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
        for (var v = 0; v < ProceduralMeshes.TreeVariants; v++)
        {
            _ = ProceduralMeshes.Tree(v);
        }

        var budgets = new Dictionary<string, int>
        {
            ["Golfer"] = ProceduralMeshes.GolferBudget,
            ["Ball"] = ProceduralMeshes.BallBudget,
            ["Flag"] = ProceduralMeshes.FlagBudget,
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
        ["buildings"] = 1500,
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
