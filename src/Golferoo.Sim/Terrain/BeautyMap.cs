using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>
/// Beauté de 0 à 1 par cellule, pour l'artiste : chaque source (l'eau, les arbres, les fleurs au printemps, les sources de beauté
/// posées dans le monde) apporte son poids × (1 − d / <see cref="Radius"/>), d étant la distance à la plus proche ; les apports
/// s'additionnent, plafonnés à 1. Distances par chanfrein sur la grille (deux passes). Refaite quand le terrain, les arbres, la saison
/// ou les sources changent (<see cref="IsFor"/>).
/// </summary>
public sealed class BeautyMap
{
    public const double Radius = 15.0;

    public const double WaterWeight = 1.0;
    public const double TreeWeight = 0.6;
    public const double FlowerWeight = 0.4;
    public const double SourceWeight = 1.0;

    private readonly float[] _beauty;
    private readonly TerrainGrid _terrain;
    private long _terrainVersion;
    private bool[] _water = [];
    private bool[] _flowers = [];
    private readonly TreeStand? _trees;
    private readonly long _treesVersion;
    private readonly Season _season;
    private readonly IReadOnlyList<Point2> _sources;
    private readonly int _sourceCount;

    private BeautyMap(TerrainGrid terrain, TreeStand? trees, Season season, IReadOnlyList<Point2> sources)
    {
        _terrain = terrain;
        _terrainVersion = terrain.SurfaceVersion;
        _trees = trees;
        _treesVersion = trees?.Version ?? 0;
        _season = season;
        _sources = sources;
        _sourceCount = sources.Count;
        _beauty = new float[terrain.CellsX * terrain.CellsZ];
    }

    /// <summary>Beauté au point (x, z), de 0 à 1.</summary>
    public double At(double x, double z) => _beauty[(_terrain.CellZOf(z) * _terrain.CellsX) + _terrain.CellXOf(x)];

    /// <summary>La carte vaut encore pour ce terrain, ces arbres, cette saison et ces sources.</summary>
    public bool IsFor(TerrainGrid terrain, TreeStand? trees, Season season, IReadOnlyList<Point2> sources) =>
        ReferenceEquals(terrain, _terrain) && terrain.SurfaceVersion == _terrainVersion
        && ReferenceEquals(trees, _trees) && (trees?.Version ?? 0) == _treesVersion
        && season == _season && ReferenceEquals(sources, _sources) && sources.Count == _sourceCount;

    /// <summary>
    /// Comme <see cref="IsFor"/>, mais une retouche des surfaces qui laisse l'eau où elle était ne compte pas et les fleurs du printemps non plus :
    /// la carte est alors la même, on la garde (C12 lot 4 : sans cela, chaque retouche coûtait une carte entière au premier artiste qui joue).
    /// </summary>
    public bool StillValid(TerrainGrid terrain, TreeStand? trees, Season season, IReadOnlyList<Point2> sources)
    {
        if (IsFor(terrain, trees, season, sources))
        {
            return true;
        }

        if (!ReferenceEquals(terrain, _terrain) || !ReferenceEquals(trees, _trees) || (trees?.Version ?? 0) != _treesVersion
            || season != _season || !ReferenceEquals(sources, _sources) || sources.Count != _sourceCount)
        {
            return false;
        }

        var nx = terrain.CellsX;
        for (var i = 0; i < _water.Length; i++)
        {
            if ((terrain.GetCellSurface(i % nx, i / nx) == Surface.Water) != _water[i] || (season == Season.Spring && Flowers.HasTuft(terrain, i % nx, i / nx) != _flowers[i]))
            {
                return false;
            }
        }

        _terrainVersion = terrain.SurfaceVersion;
        return true;
    }

    public static BeautyMap Build(TerrainGrid terrain, TreeStand? trees, Season season, IReadOnlyList<Point2> sources)
    {
        var map = new BeautyMap(terrain, trees, season, sources);
        var nx = terrain.CellsX;
        var n = nx * terrain.CellsZ;
        var mask = new bool[n];

        void Add(double weight)
        {
            var d = Distances(mask, nx, terrain.CellsZ, terrain.CellSize);
            for (var i = 0; i < n; i++)
            {
                map._beauty[i] = (float)Math.Min(1.0, map._beauty[i] + (weight * Math.Max(0.0, 1.0 - (d[i] / Radius))));
            }

            Array.Clear(mask);
        }

        for (var i = 0; i < n; i++)
        {
            mask[i] = terrain.GetCellSurface(i % nx, i / nx) == Surface.Water;
        }

        map._water = (bool[])mask.Clone();
        Add(WaterWeight);
        if (trees is not null && trees.Count > 0)
        {
            foreach (var t in trees.Trees)
            {
                if (t.State == TreeState.Standing)
                {
                    mask[(terrain.CellZOf(t.Z) * nx) + terrain.CellXOf(t.X)] = true;
                }
            }

            Add(TreeWeight);
        }

        if (season == Season.Spring)
        {
            for (var i = 0; i < n; i++)
            {
                mask[i] = Flowers.HasTuft(terrain, i % nx, i / nx);
            }

            map._flowers = (bool[])mask.Clone();
            Add(FlowerWeight);
        }

        if (sources.Count > 0)
        {
            foreach (var s in sources)
            {
                mask[(terrain.CellZOf(s.Z) * nx) + terrain.CellXOf(s.X)] = true;
            }

            Add(SourceWeight);
        }

        return map;
    }

    /// <summary>Distance de chaque cellule à la plus proche cellule marquée, en mètres (chanfrein 1 / √2, deux passes).</summary>
    private static float[] Distances(bool[] mask, int nx, int nz, double cellSize)
    {
        var straight = (float)cellSize;
        var diagonal = (float)(cellSize * Math.Sqrt(2.0));
        var d = new float[mask.Length];
        for (var i = 0; i < d.Length; i++)
        {
            d[i] = mask[i] ? 0f : float.MaxValue;
        }

        void Relax(int i, int x, int z, int dx, int dz, float step)
        {
            var px = x + dx;
            var pz = z + dz;
            if (px >= 0 && px < nx && pz >= 0 && pz < nz)
            {
                var v = d[(pz * nx) + px];
                if (v < float.MaxValue && v + step < d[i])
                {
                    d[i] = v + step;
                }
            }
        }

        for (var z = 0; z < nz; z++)
        {
            for (var x = 0; x < nx; x++)
            {
                var i = (z * nx) + x;
                Relax(i, x, z, -1, 0, straight);
                Relax(i, x, z, 0, -1, straight);
                Relax(i, x, z, -1, -1, diagonal);
                Relax(i, x, z, 1, -1, diagonal);
            }
        }

        for (var z = nz - 1; z >= 0; z--)
        {
            for (var x = nx - 1; x >= 0; x--)
            {
                var i = (z * nx) + x;
                Relax(i, x, z, 1, 0, straight);
                Relax(i, x, z, 0, 1, straight);
                Relax(i, x, z, 1, 1, diagonal);
                Relax(i, x, z, -1, 1, diagonal);
            }
        }

        return d;
    }
}
