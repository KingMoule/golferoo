using System.Diagnostics;
using Godot;

namespace Golferoo.Game.Rendering;

/// <summary>Temps de construction et taille d'un maillage généré par code.</summary>
public readonly record struct AssetStat(string Name, double Milliseconds, int Triangles);

/// <summary>
/// Maillages low-poly construits par code : ombrage plat (sommets non partagés, normales par face),
/// couleurs par sommet. Tout est déterministe (aucun Random non graine). Chaque maillage est mis en cache,
/// chronométré et annoncé une seule fois par <c>ASSET nom ms tri</c>.
/// </summary>
public static class ProceduralMeshes
{
    public const int GolferBudget = 300;
    public const int BallBudget = 80;
    public const int FlagBudget = 60;
    public const int TreeBudget = 400;
    public const int TreeVariants = 4;

    /// <summary>Rayon visuel de la balle : 0,16 m au lieu de 0,12 m, car en vue isométrique une balle de 0,24 m reste sous le pixel.</summary>
    public const float BallRadius = 0.16f;

    /// <summary>Hauteur du mât du drapeau.</summary>
    public const float FlagHeight = 2.4f;

    private static readonly Dictionary<string, ArrayMesh> Cache = new();
    private static readonly List<AssetStat> StatList = new();

    public static IReadOnlyList<AssetStat> Stats => StatList;

    /// <summary>Golfeur de 1,8 m, pieds en y = 0, visière vers +Z. Surface 0 : haut et casquette (couleur de sommet multipliée par la couleur d'instance), 1 : peau, 2 : pantalon et chaussures (couleurs fixes du matériau).</summary>
    public static ArrayMesh Golfer() => Get("Golfer", () =>
    {
        var b = new Builder();
        var tint = b.Surface(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f });
        var skin = b.Surface(new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.66f, 0.5f), Roughness = 0.9f });
        var pants = b.Surface(new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.72f, 0.55f), Roughness = 0.9f });
        var shoes = b.Surface(new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.55f, 0.55f), Roughness = 0.9f });

        // Jambes (12 tri chacune) et chaussures.
        b.Box(pants, new Vector3(-0.11f, 0.45f, 0), new Vector3(0.16f, 0.9f, 0.18f), Colors.White);
        b.Box(pants, new Vector3(0.11f, 0.45f, 0), new Vector3(0.16f, 0.9f, 0.18f), Colors.White);
        b.Box(shoes, new Vector3(-0.11f, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.3f), new Color(0.55f, 0.55f, 0.55f));
        b.Box(shoes, new Vector3(0.11f, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.3f), new Color(0.55f, 0.55f, 0.55f));
        // Torse effilé vers la taille, bras, mains.
        b.Frustum(tint, new Vector3(0, 0.88f, 0), 0.2f, 0.27f, 0.62f, 6, Colors.White);
        b.Box(tint, new Vector3(-0.33f, 1.18f, 0), new Vector3(0.11f, 0.4f, 0.13f), new Color(0.85f, 0.85f, 0.85f));
        b.Box(tint, new Vector3(0.33f, 1.18f, 0), new Vector3(0.11f, 0.4f, 0.13f), new Color(0.85f, 0.85f, 0.85f));
        b.Box(skin, new Vector3(-0.33f, 0.93f, 0), new Vector3(0.1f, 0.1f, 0.1f), Colors.White);
        b.Box(skin, new Vector3(0.33f, 0.93f, 0), new Vector3(0.1f, 0.1f, 0.1f), Colors.White);
        // Cou, tête, casquette et visière.
        b.Box(skin, new Vector3(0, 1.52f, 0), new Vector3(0.1f, 0.08f, 0.1f), Colors.White);
        b.Box(skin, new Vector3(0, 1.64f, 0), new Vector3(0.2f, 0.2f, 0.2f), Colors.White);
        b.Box(tint, new Vector3(0, 1.77f, 0), new Vector3(0.23f, 0.08f, 0.23f), Colors.White);
        b.Box(tint, new Vector3(0, 1.73f, 0.17f), new Vector3(0.2f, 0.03f, 0.16f), new Color(0.8f, 0.8f, 0.8f));
        return b.Build();
    });

    /// <summary>Icosphère à 80 triangles.</summary>
    public static ArrayMesh Ball() => Get("Ball", () =>
    {
        var b = new Builder();
        var s = b.Surface(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.5f });
        foreach (var (p, q, r) in Icosphere(1))
        {
            b.Tri(s, p * BallRadius, q * BallRadius, r * BallRadius, Vector3.Zero, Colors.White);
        }

        return b.Build();
    });

    /// <summary>Mât blanc, fanion rouge triangulaire et liseré blanc côté mât. Origine au pied, drapeau double face.</summary>
    public static ArrayMesh Flag() => Get("Flag", () =>
    {
        var b = new Builder();
        var pole = b.Surface(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.6f });
        var cloth = b.Surface(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled });
        b.Frustum(pole, new Vector3(0, FlagHeight * 0.5f, 0), 0.03f, 0.03f, FlagHeight, 6, new Color(0.93f, 0.93f, 0.93f));
        b.Frustum(pole, new Vector3(0, FlagHeight + 0.03f, 0), 0.05f, 0.0f, 0.06f, 6, new Color(0.9f, 0.8f, 0.2f));
        var top = FlagHeight - 0.05f;
        var bottom = FlagHeight - 0.55f;
        b.Raw(cloth, new Vector3(0.03f, top, 0), new Vector3(0.03f, bottom, 0), new Vector3(0.75f, (top + bottom) * 0.5f, 0), new Color(0.9f, 0.15f, 0.12f));
        // Liseré le long du mât : deux triangles formant un rectangle.
        var w = 0.1f;
        b.Raw(cloth, new Vector3(0.03f, top, 0.001f), new Vector3(0.03f, bottom, 0.001f), new Vector3(0.03f + w, bottom, 0.001f), Colors.White);
        b.Raw(cloth, new Vector3(0.03f, top, 0.001f), new Vector3(0.03f + w, bottom, 0.001f), new Vector3(0.03f + w, top, 0.001f), Colors.White);
        return b.Build();
    });

    /// <summary>Arbre de la variante donnée (modulo 4) : tronc et trois masses irrégulières. Pied en y = 0, environ 4,5 m.</summary>
    public static ArrayMesh Tree(int variant)
    {
        var v = ((variant % TreeVariants) + TreeVariants) % TreeVariants;
        return Get($"Tree{v}", () =>
        {
            var b = new Builder();
            var s = b.Surface(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f });
            var rng = new Hash((uint)(v + 1) * 2654435761u);
            var trunkTop = 1.4f + (rng.Next() * 0.4f);
            b.Frustum(s, new Vector3(0, trunkTop * 0.5f, 0), 0.26f, 0.15f, trunkTop, 6, new Color(0.40f, 0.26f, 0.14f));

            var y = trunkTop + 0.4f;
            var radius = 1.2f + (rng.Next() * 0.35f);
            for (var m = 0; m < 3; m++)
            {
                var center = new Vector3((rng.Next() - 0.5f) * 0.3f, y, (rng.Next() - 0.5f) * 0.3f);
                var squash = 0.92f + (rng.Next() * 0.12f);
                var green = new Color(0.10f + (0.04f * m), 0.33f + (0.05f * m), 0.14f + (0.02f * m));
                var seed = (uint)((v * 31) + m + 1);
                foreach (var (p, q, r) in Icosphere(1))
                {
                    Vector3 Shape(Vector3 d)
                    {
                        var k = 0.95f + (Hash.OfVector(d, seed) * 0.10f);
                        return center + new Vector3(d.X * radius * k, d.Y * radius * k * squash, d.Z * radius * k);
                    }

                    var shade = 0.9f + (Hash.OfVector(p + q + r, seed + 7) * 0.2f);
                    b.Tri(s, Shape(p), Shape(q), Shape(r), center, new Color(green.R * shade, green.G * shade, green.B * shade));
                }

                y += radius * squash * 0.85f;
                radius *= 0.82f;
            }

            return b.Build();
        });
    }

    private static ArrayMesh Get(string name, Func<Builder.Result> make)
    {
        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var sw = Stopwatch.StartNew();
        var result = make();
        sw.Stop();
        var stat = new AssetStat(name, sw.Elapsed.TotalMilliseconds, result.Triangles);
        StatList.Add(stat);
        GD.Print($"ASSET {name} {stat.Milliseconds:F2} ms {stat.Triangles} tri");
        Cache[name] = result.Mesh;
        return result.Mesh;
    }

    /// <summary>Icosaèdre subdivisé <paramref name="subdivisions"/> fois (20 × 4^n triangles) sur la sphère unité.</summary>
    private static List<(Vector3, Vector3, Vector3)> Icosphere(int subdivisions)
    {
        var t = (1f + MathF.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var tris = new List<(Vector3, Vector3, Vector3)>();
        for (var i = 0; i < f.Length; i += 3)
        {
            tris.Add((v[f[i]].Normalized(), v[f[i + 1]].Normalized(), v[f[i + 2]].Normalized()));
        }

        for (var s = 0; s < subdivisions; s++)
        {
            var next = new List<(Vector3, Vector3, Vector3)>();
            foreach (var (a, b, c) in tris)
            {
                var ab = ((a + b) * 0.5f).Normalized();
                var bc = ((b + c) * 0.5f).Normalized();
                var ca = ((c + a) * 0.5f).Normalized();
                next.Add((a, ab, ca));
                next.Add((b, bc, ab));
                next.Add((c, ca, bc));
                next.Add((ab, bc, ca));
            }

            tris = next;
        }

        return tris;
    }

    /// <summary>Petit générateur déterministe (hachage entier), indépendant de System.Random.</summary>
    private struct Hash
    {
        private uint _state;

        public Hash(uint seed) => _state = seed;

        /// <summary>Flottant dans [0, 1).</summary>
        public float Next()
        {
            _state = Mix(_state + 0x9E3779B9u);
            return (_state & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Valeur dans [0, 1) qui ne dépend que de la position (sommets partagés, donc sans fissure).</summary>
        public static float OfVector(Vector3 p, uint seed)
        {
            unchecked
            {
                var h = seed;
                h = Mix(h ^ (uint)(int)MathF.Round(p.X * 1000f));
                h = Mix(h ^ (uint)(int)MathF.Round(p.Y * 1000f));
                h = Mix(h ^ (uint)(int)MathF.Round(p.Z * 1000f));
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return h;
            }
        }
    }

    /// <summary>Accumule des triangles non indexés par surface, avec normale de face et couleur.</summary>
    private sealed class Builder
    {
        private readonly List<SurfaceData> _surfaces = new();

        public readonly record struct Result(ArrayMesh Mesh, int Triangles);

        public int Surface(Material material)
        {
            _surfaces.Add(new SurfaceData(material));
            return _surfaces.Count - 1;
        }

        /// <summary>Triangle dont la face avant (sens horaire dans Godot) regarde à l'opposé de <paramref name="inside"/>.</summary>
        public void Tri(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color color)
        {
            var n = (a - c).Cross(a - b);
            if (n.Dot(((a + b + c) / 3f) - inside) < 0f)
            {
                (b, c) = (c, b);
            }

            Raw(s, a, b, c, color);
        }

        /// <summary>Triangle tel quel (pour les surfaces double face).</summary>
        public void Raw(int s, Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var n = (a - c).Cross(a - b).Normalized();
            var d = _surfaces[s];
            foreach (var p in new[] { a, b, c })
            {
                d.Vertices.Add(p);
                d.Normals.Add(n);
                d.Colors.Add(color);
            }
        }

        public void Box(int s, Vector3 center, Vector3 size, Color color)
        {
            var h = size * 0.5f;
            Vector3 P(int i) => center + new Vector3(((i & 1) == 0 ? -1 : 1) * h.X, ((i & 2) == 0 ? -1 : 1) * h.Y, ((i & 4) == 0 ? -1 : 1) * h.Z);
            int[][] quads = { new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 } };
            foreach (var q in quads)
            {
                // Les faces hautes sont plus claires que les faces basses : lisibilité sans éclairage.
                var shade = Mathf.Clamp(1f + (((P(q[0]) + P(q[2])) * 0.5f - center).Normalized().Y * 0.12f), 0.8f, 1.1f);
                var c = new Color(color.R * shade, color.G * shade, color.B * shade);
                Tri(s, P(q[0]), P(q[1]), P(q[2]), center, c);
                Tri(s, P(q[0]), P(q[2]), P(q[3]), center, c);
            }
        }

        /// <summary>Prisme à <paramref name="sides"/> côtés, rayon bas puis haut ; fermé en haut si le rayon haut est non nul. Centré sur <paramref name="center"/>.</summary>
        public void Frustum(int s, Vector3 center, float bottomRadius, float topRadius, float height, int sides, Color color)
        {
            var yb = center.Y - (height * 0.5f);
            var yt = center.Y + (height * 0.5f);
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * MathF.Tau / sides;
                var a1 = (i + 1) * MathF.Tau / sides;
                var b0 = new Vector3(center.X + (MathF.Cos(a0) * bottomRadius), yb, center.Z + (MathF.Sin(a0) * bottomRadius));
                var b1 = new Vector3(center.X + (MathF.Cos(a1) * bottomRadius), yb, center.Z + (MathF.Sin(a1) * bottomRadius));
                var t0 = new Vector3(center.X + (MathF.Cos(a0) * topRadius), yt, center.Z + (MathF.Sin(a0) * topRadius));
                var t1 = new Vector3(center.X + (MathF.Cos(a1) * topRadius), yt, center.Z + (MathF.Sin(a1) * topRadius));
                var shade = 0.88f + (0.2f * (0.5f + (0.5f * MathF.Cos(((a0 + a1) * 0.5f) - 0.8f))));
                var c = new Color(color.R * shade, color.G * shade, color.B * shade);
                Tri(s, b0, b1, t1, center, c);
                if (topRadius > 0f)
                {
                    Tri(s, b0, t1, t0, center, c);
                    Tri(s, center + new Vector3(0, height * 0.5f, 0), t0, t1, center, c);
                }
            }
        }

        public Result Build()
        {
            var mesh = new ArrayMesh();
            var tris = 0;
            var surfaceIndex = 0;
            foreach (var d in _surfaces)
            {
                if (d.Vertices.Count == 0)
                {
                    continue;
                }

                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = d.Vertices.ToArray();
                arrays[(int)Mesh.ArrayType.Normal] = d.Normals.ToArray();
                arrays[(int)Mesh.ArrayType.Color] = d.Colors.ToArray();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                mesh.SurfaceSetMaterial(surfaceIndex++, d.Material);
                tris += d.Vertices.Count / 3;
            }

            return new Result(mesh, tris);
        }

        private sealed class SurfaceData(Material material)
        {
            public Material Material { get; } = material;

            public List<Vector3> Vertices { get; } = new();

            public List<Vector3> Normals { get; } = new();

            public List<Color> Colors { get; } = new();
        }
    }
}
