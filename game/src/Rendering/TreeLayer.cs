using Godot;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>Arbres provisoires (cylindre + cône), un par cellule d'arbres, via deux MultiMesh. Le lot 7 les remplacera.</summary>
public partial class TreeLayer : Node3D
{
    public int TreeCount { get; private set; }

    public void Build(TerrainGrid grid)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var trunks = new List<Transform3D>();
        var crowns = new List<Transform3D>();
        for (var cz = 0; cz < grid.CellsZ; cz++)
        {
            for (var cx = 0; cx < grid.CellsX; cx++)
            {
                if (grid.GetCellSurface(cx, cz) != Surface.Trees)
                {
                    continue;
                }

                var h = Hash(cx, cz);
                var jx = (((h & 0xFF) / 255f) - 0.5f) * (float)grid.CellSize * 0.6f;
                var jz = ((((h >> 8) & 0xFF) / 255f) - 0.5f) * (float)grid.CellSize * 0.6f;
                var scale = 0.8f + ((((h >> 16) & 0xFF) / 255f) * 0.7f);
                var c = grid.CellCenter(cx, cz);
                var x = (float)c.X + jx;
                var z = (float)c.Z + jz;
                var y = (float)grid.Sample(x, z);
                var basis = Basis.FromScale(new Vector3(scale, scale, scale));
                trunks.Add(new Transform3D(basis, new Vector3(x, y + (0.6f * scale), z)));
                crowns.Add(new Transform3D(basis, new Vector3(x, y + (2.6f * scale), z)));
            }
        }

        TreeCount = trunks.Count;
        AddLayer("Trunks", new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 1.2f, RadialSegments = 6, Rings = 1 }, new Color(0.40f, 0.26f, 0.14f), trunks);
        AddLayer("Crowns", new CylinderMesh { TopRadius = 0f, BottomRadius = 1.1f, Height = 3.2f, RadialSegments = 7, Rings = 1 }, new Color(0.13f, 0.38f, 0.17f), crowns);
    }

    private void AddLayer(string name, Mesh mesh, Color color, List<Transform3D> transforms)
    {
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count,
        };
        for (var i = 0; i < transforms.Count; i++)
        {
            multi.SetInstanceTransform(i, transforms[i]);
        }

        AddChild(new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f },
        });
    }

    private static uint Hash(int x, int z)
    {
        unchecked
        {
            var h = ((uint)x * 73856093u) ^ ((uint)z * 19349663u);
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return h;
        }
    }
}
