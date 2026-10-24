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

    public SurfaceProperties PropertiesAt(double x, double z) => SurfaceTable.Get(SurfaceAt(x, z));

    /// <summary>Édition : change la hauteur du coin (i, j) de la grille de cellules.</summary>
    public void SetCornerHeight(int i, int j, double height) => Heights.SetCornerHeight(i, j, height);

    /// <summary>Édition : peint la surface de la cellule (cx, cz).</summary>
    public void PaintCell(int cx, int cz, Surface surface)
    {
        if ((int)surface < 0 || (int)surface >= SurfaceTable.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(surface), surface, "Surface inconnue.");
        }

        _surfaces[CellIndex(cx, cz)] = surface;
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
