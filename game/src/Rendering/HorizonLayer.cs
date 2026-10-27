using Godot;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Horizon procédural : un anneau de relief autour de la carte, au rendu seulement. Il part de la hauteur du bord du terrain
/// (pas de marche à la jonction) et monte vers des collines douces qui ferment la vue rasante de la caméra libre.
/// Un seul maillage (quelques milliers de triangles), un matériau simple, bruit déterministe.
/// </summary>
public partial class HorizonLayer : MeshInstance3D
{
    /// <summary>Largeur de l'anneau, en mètres (couvre la portée utile de la caméra libre).</summary>
    public const float Width = 700f;

    private const float Step = 8f;
    private const float HillStart = 40f;
    private const float HillAmplitude = 55f;
    private static readonly float[] Offsets = [8f, 16f, 32f, 64f, 128f, 256f, 400f, 550f, Width];

    private TerrainGrid? _grid;
    private Rect2 _map;

    /// <summary>Triangles du maillage (pour les vérifications).</summary>
    public int TriangleCount { get; private set; }

    /// <summary>Boîte englobante de l'anneau en x et z (monde), pour vérifier qu'il entoure la carte.</summary>
    public Rect2 Extent => _map.Grow(Width);

    public void Build(TerrainGrid grid)
    {
        _grid = grid;
        _map = new Rect2(0, 0, (float)(grid.CellsX * grid.CellSize), (float)(grid.CellsZ * grid.CellSize));
        var xs = Lines(_map.Size.X);
        var zs = Lines(_map.Size.Y);
        TriangleCount = 0;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var j = 0; j + 1 < zs.Count; j++)
        {
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                if (xs[i] >= 0f && xs[i + 1] <= _map.Size.X && zs[j] >= 0f && zs[j + 1] <= _map.Size.Y)
                {
                    continue; // sous la carte : le terrain y est déjà
                }

                Quad(st, xs[i], xs[i + 1], zs[j], zs[j + 1]);
                TriangleCount += 2;
            }
        }

        st.GenerateNormals();
        Mesh = st.Commit();
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            AlbedoColor = Colors.White,
            Roughness = 1f,
            MetallicSpecular = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }

    /// <summary>Hauteur de l'anneau en (x, z) ; au bord de la carte, celle du terrain.</summary>
    public float HeightAt(float x, float z)
    {
        var px = Mathf.Clamp(x, 0f, _map.Size.X);
        var pz = Mathf.Clamp(z, 0f, _map.Size.Y);
        var edge = (float)_grid!.Sample(px, pz);
        var d = Mathf.Sqrt(((x - px) * (x - px)) + ((z - pz) * (z - pz)));
        var s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp((d - HillStart) / (Width - HillStart), 0f, 1f));

        // Collines : deux octaves de bruit de valeur ; la base remonte avec la distance pour fermer l'horizon.
        var n = (0.65f * Noise(x / 140f, z / 140f)) + (0.35f * Noise(x / 55f, z / 55f));
        var near = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(d / HillStart, 0f, 1f));
        return edge + (near * 4f * (n - 0.3f)) + (s * HillAmplitude * (0.35f + (0.65f * n)));
    }

    private static List<float> Lines(float size)
    {
        var lines = new List<float>();
        for (var k = Offsets.Length - 1; k >= 0; k--)
        {
            lines.Add(-Offsets[k]);
        }

        for (var v = 0f; v < size; v += Step)
        {
            lines.Add(v);
        }

        lines.Add(size);
        foreach (var o in Offsets)
        {
            lines.Add(size + o);
        }

        return lines;
    }

    private void Quad(SurfaceTool st, float x0, float x1, float z0, float z1)
    {
        Vertex(st, x0, z0);
        Vertex(st, x1, z0);
        Vertex(st, x0, z1);
        Vertex(st, x1, z0);
        Vertex(st, x1, z1);
        Vertex(st, x0, z1);
    }

    private void Vertex(SurfaceTool st, float x, float z)
    {
        var y = HeightAt(x, z);
        // Teinte d'herbe proche du rough, un peu plus sombre et bleutée avec la distance.
        var d = Mathf.Clamp(Mathf.Max(Mathf.Max(-x, x - _map.Size.X), Mathf.Max(-z, z - _map.Size.Y)) / Width, 0f, 1f);
        var shade = 0.85f + (0.3f * Noise(x / 30f + 11f, z / 30f + 7f));
        var near = new Color(0.30f, 0.46f, 0.17f);
        var far = new Color(0.27f, 0.42f, 0.28f);
        st.SetColor(near.Lerp(far, d) * shade);
        st.AddVertex(new Vector3(x, y, z));
    }

    private static float Hash(int x, int z)
    {
        var h = unchecked((uint)((x * 374761393) + (z * 668265263)));
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFFu) / 65535f;
    }

    private static float Noise(float x, float z)
    {
        var ix = Mathf.FloorToInt(x);
        var iz = Mathf.FloorToInt(z);
        var fx = Mathf.SmoothStep(0f, 1f, x - ix);
        var fz = Mathf.SmoothStep(0f, 1f, z - iz);
        return Mathf.Lerp(
            Mathf.Lerp(Hash(ix, iz), Hash(ix + 1, iz), fx),
            Mathf.Lerp(Hash(ix, iz + 1), Hash(ix + 1, iz + 1), fx),
            fz);
    }
}
