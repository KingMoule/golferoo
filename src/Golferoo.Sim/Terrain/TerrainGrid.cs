namespace Golferoo.Sim.Terrain;

/// <summary>
/// Terrain simulé : champ de hauteur lissé et une surface discrète par cellule, plus les repères du trou.
/// Unités : mètres, x vers l'est, z vers le sud, y la hauteur.
/// </summary>
public sealed class TerrainGrid
{
    private readonly Surface[] _surfaces;

    public TerrainGrid(HeightField heights, string name, Point2 tee, Point2 green, Point2 hole)
    {
        Heights = heights ?? throw new ArgumentNullException(nameof(heights));
        Name = name;
        Tee = tee;
        Green = green;
        Hole = hole;
        _surfaces = new Surface[heights.CellsX * heights.CellsZ];
        Array.Fill(_surfaces, Surface.Rough);
    }

    public HeightField Heights { get; }

    /// <summary>Hauteur minimale d'un coin, en mètres (bornes des retouches de relief).</summary>
    public const double MinHeight = -5.0;

    /// <summary>Hauteur maximale d'un coin, en mètres (bornes des retouches de relief).</summary>
    public const double MaxHeight = 15.0;

    /// <summary>Variation maximale de hauteur d'un coin par retouche, en mètres.</summary>
    public const double MaxRaiseDelta = 2.0;

    /// <summary>Compteur incrémenté à chaque édition (hauteur ou surface).</summary>
    public long Version { get; private set; }

    /// <summary>Compteur incrémenté à chaque changement de surface : les champs de flux (coûts par surface) s'y comparent.</summary>
    public long SurfaceVersion { get; private set; }

    /// <summary>Compteur incrémenté à chaque changement de hauteur.</summary>
    public long HeightVersion { get; private set; }

    internal ReadOnlySpan<Surface> Surfaces => _surfaces;

    public string Name { get; }

    /// <summary>Position de départ, en mètres.</summary>
    public Point2 Tee { get; }

    /// <summary>Centre du green, en mètres.</summary>
    public Point2 Green { get; }

    /// <summary>Position du trou, en mètres.</summary>
    public Point2 Hole { get; }

    public int CellsX => Heights.CellsX;

    public int CellsZ => Heights.CellsZ;

    public double CellSize => Heights.CellSize;

    /// <summary>Hauteur en mètres au point (x, z) ; borne hors carte.</summary>
    public double Sample(double x, double z) => Heights.Sample(x, z);

    public Vec3 SampleNormal(double x, double z) => Heights.SampleNormal(x, z);

    public Surface GetCellSurface(int cx, int cz) => _surfaces[CellIndex(cx, cz)];

    /// <summary>Surface sous le point (x, z) ; borne hors carte.</summary>
    public Surface SurfaceAt(double x, double z)
    {
        var cx = Math.Clamp((int)Math.Floor(x / CellSize), 0, CellsX - 1);
        var cz = Math.Clamp((int)Math.Floor(z / CellSize), 0, CellsZ - 1);
        return _surfaces[(cz * CellsX) + cx];
    }

    /// <summary>Indice de la cellule contenant l'abscisse x, borné à la carte.</summary>
    public int CellXOf(double x) => Math.Clamp((int)Math.Floor(x / CellSize), 0, CellsX - 1);

    /// <summary>Indice de la cellule contenant la profondeur z, borné à la carte.</summary>
    public int CellZOf(double z) => Math.Clamp((int)Math.Floor(z / CellSize), 0, CellsZ - 1);

    /// <summary>Centre de la cellule (cx, cz), en mètres.</summary>
    public Point2 CellCenter(int cx, int cz) => new((cx + 0.5) * CellSize, (cz + 0.5) * CellSize);

    /// <summary>Vrai si un golfeur peut marcher sur la cellule ; faux hors grille.</summary>
    public bool IsWalkable(int cx, int cz) =>
        cx >= 0 && cx < CellsX && cz >= 0 && cz < CellsZ && SurfaceTable.Get(_surfaces[(cz * CellsX) + cx]).Walkable;

    public SurfaceProperties PropertiesAt(double x, double z) => SurfaceTable.Get(SurfaceAt(x, z));

    /// <summary>Édition : change la hauteur du coin (i, j) de la grille de cellules.</summary>
    public void SetCornerHeight(int i, int j, double height)
    {
        Heights.SetCornerHeight(i, j, height);
        HeightVersion++;
        Version++;
    }

    /// <summary>Édition : peint la surface de la cellule (cx, cz).</summary>
    public void PaintCell(int cx, int cz, Surface surface)
    {
        if ((int)surface < 0 || (int)surface >= SurfaceTable.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(surface), surface, "Surface inconnue.");
        }

        _surfaces[CellIndex(cx, cz)] = surface;
        SurfaceVersion++;
        Version++;
    }

    /// <summary>
    /// Édition : peint de <paramref name="surface"/> toutes les cellules dont le centre est à au plus <paramref name="radius"/> mètres de <paramref name="center"/>.
    /// Rend le rectangle de cellules modifiées, ou null si aucune.
    /// </summary>
    /// <summary>
    /// Vrai si peindre <paramref name="surface"/> dans ce disque laisse au moins une cellule franchissable sur la carte
    /// (sans quoi la repose des golfeurs et les champs de flux n'ont plus de solution).
    /// </summary>
    public bool KeepsWalkableCell(Point2 center, double radius, Surface surface)
    {
        if (SurfaceTable.Get(surface).Walkable)
        {
            return true;
        }

        var r2 = radius * radius;
        for (var cz = 0; cz < CellsZ; cz++)
        {
            for (var cx = 0; cx < CellsX; cx++)
            {
                if (!SurfaceTable.Get(_surfaces[(cz * CellsX) + cx]).Walkable)
                {
                    continue;
                }

                var c = CellCenter(cx, cz);
                var dx = c.X - center.X;
                var dz = c.Z - center.Z;
                if ((dx * dx) + (dz * dz) > r2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public CellRect? PaintDisc(Point2 center, double radius, Surface surface)
    {
        ValidateRadius(radius);
        if ((int)surface < 0 || (int)surface >= SurfaceTable.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(surface), surface, "Surface inconnue.");
        }

        var r2 = radius * radius;
        var x0 = Math.Max(0, (int)Math.Floor(((center.X - radius) / CellSize) - 0.5));
        var x1 = Math.Min(CellsX - 1, (int)Math.Ceiling(((center.X + radius) / CellSize) - 0.5));
        var z0 = Math.Max(0, (int)Math.Floor(((center.Z - radius) / CellSize) - 0.5));
        var z1 = Math.Min(CellsZ - 1, (int)Math.Ceiling(((center.Z + radius) / CellSize) - 0.5));
        CellRect? rect = null;
        for (var cz = z0; cz <= z1; cz++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                var c = CellCenter(cx, cz);
                var dx = c.X - center.X;
                var dz = c.Z - center.Z;
                if ((dx * dx) + (dz * dz) > r2)
                {
                    continue;
                }

                if (_surfaces[(cz * CellsX) + cx] != surface)
                {
                    _surfaces[(cz * CellsX) + cx] = surface;
                    rect = rect is { } r ? r.Include(cx, cz) : new CellRect(cx, cz, cx, cz);
                }
            }
        }

        if (rect is not null)
        {
            SurfaceVersion++;
            Version++;
        }

        return rect;
    }

    /// <summary>
    /// Édition : monte (ou baisse si négatif) les coins à au plus <paramref name="radius"/> mètres de <paramref name="center"/>,
    /// avec un profil en cosinus (plein au centre, nul au bord). Le delta est borné à ±<see cref="MaxRaiseDelta"/> et
    /// la hauteur des coins à [<see cref="MinHeight"/>, <see cref="MaxHeight"/>].
    /// Rend le rectangle des cellules touchant un coin modifié, ou null si rien n'a changé.
    /// </summary>
    public CellRect? RaiseDisc(Point2 center, double radius, double deltaMeters)
    {
        ValidateRadius(radius);
        if (!double.IsFinite(deltaMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaMeters), deltaMeters, "Le delta doit être fini.");
        }

        var delta = Math.Clamp(deltaMeters, -MaxRaiseDelta, MaxRaiseDelta);
        var i0 = Math.Max(0, (int)Math.Ceiling((center.X - radius) / CellSize));
        var i1 = Math.Min(CellsX, (int)Math.Floor((center.X + radius) / CellSize));
        var j0 = Math.Max(0, (int)Math.Ceiling((center.Z - radius) / CellSize));
        var j1 = Math.Min(CellsZ, (int)Math.Floor((center.Z + radius) / CellSize));
        CellRect? rect = null;
        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                var dx = (i * CellSize) - center.X;
                var dz = (j * CellSize) - center.Z;
                var d = Math.Sqrt((dx * dx) + (dz * dz));
                if (d > radius)
                {
                    continue;
                }

                var weight = 0.5 * (1.0 + Math.Cos(Math.PI * d / radius));
                var old = Heights.GetCornerHeight(i, j);
                var value = Math.Clamp(old + (delta * weight), MinHeight, MaxHeight);
                if (value == old)
                {
                    continue;
                }

                Heights.SetCornerHeight(i, j, value);
                var cell = new CellRect(Math.Max(0, i - 1), Math.Max(0, j - 1), Math.Min(CellsX - 1, i), Math.Min(CellsZ - 1, j));
                rect = rect is { } r ? r.Union(cell) : cell;
            }
        }

        if (rect is not null)
        {
            HeightVersion++;
            Version++;
        }

        return rect;
    }

    private static void ValidateRadius(double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Le rayon doit être fini et positif.");
        }
    }

    /// <summary>Empreinte FNV-1a 64 bits de l'état du terrain (hauteurs, surfaces, repères), pour <see cref="World.Checksum"/>.</summary>
    public ulong Checksum()
    {
        var hash = 0xCBF29CE484222325UL;
        Mix(ref hash, (ulong)CellsX);
        Mix(ref hash, (ulong)CellsZ);
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(CellSize));
        foreach (var h in Heights.Corners)
        {
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(h));
        }

        foreach (var s in _surfaces)
        {
            hash ^= (byte)s;
            hash *= 0x100000001B3UL;
        }

        foreach (var p in new[] { Tee, Green, Hole })
        {
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Z));
        }

        return hash;
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash *= 0x100000001B3UL;
        }
    }

    private int CellIndex(int cx, int cz)
    {
        if (cx < 0 || cx >= CellsX)
        {
            throw new ArgumentOutOfRangeException(nameof(cx), cx, $"Cellule hors grille (0 à {CellsX - 1}).");
        }

        if (cz < 0 || cz >= CellsZ)
        {
            throw new ArgumentOutOfRangeException(nameof(cz), cz, $"Cellule hors grille (0 à {CellsZ - 1}).");
        }

        return (cz * CellsX) + cx;
    }
}
