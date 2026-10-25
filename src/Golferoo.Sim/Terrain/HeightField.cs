namespace Golferoo.Sim.Terrain;

/// <summary>
/// Champ de hauteur lissé : hauteurs aux coins des cellules, interpolées en Catmull-Rom bicubique.
/// C'est la seule fonction de hauteur du jeu : maillage et balle l'échantillonnent.
/// Hors carte, les coordonnées sont bornées au bord.
/// </summary>
public sealed class HeightField
{
    private readonly double[] _corners;
    private double _maxCorner;
    private bool _maxDirty;

    public HeightField(int cellsX, int cellsZ, double cellSize)
    {
        if (cellsX < 1 || cellsZ < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cellsX), "La grille doit avoir au moins une cellule.");
        }

        if (!(cellSize > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "La taille de cellule doit être positive.");
        }

        CellsX = cellsX;
        CellsZ = cellsZ;
        CellSize = cellSize;
        _corners = new double[(cellsX + 1) * (cellsZ + 1)];
    }

    public int CellsX { get; }

    public int CellsZ { get; }

    /// <summary>Côté d'une cellule en mètres.</summary>
    public double CellSize { get; }

    /// <summary>Largeur de la carte en mètres (axe x).</summary>
    public double WidthMeters => CellsX * CellSize;

    /// <summary>Profondeur de la carte en mètres (axe z).</summary>
    public double DepthMeters => CellsZ * CellSize;

    internal double[] Corners => _corners;

    /// <summary>Plus grande hauteur des coins, donc du relief (surface bilinéaire), en mètres. Entretenue à chaque écriture.</summary>
    public double MaxCornerHeight
    {
        get
        {
            if (_maxDirty)
            {
                _maxCorner = _corners.Max();
                _maxDirty = false;
            }

            return _maxCorner;
        }
    }

    /// <summary>Hauteur du coin (i, j), de 0 à CellsX et de 0 à CellsZ inclus.</summary>
    public double GetCornerHeight(int i, int j) => _corners[CornerIndex(i, j)];

    public void SetCornerHeight(int i, int j, double height)
    {
        if (!double.IsFinite(height))
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "La hauteur doit être finie.");
        }

        var index = CornerIndex(i, j);
        if (_corners[index] >= _maxCorner && height < _corners[index])
        {
            _maxDirty = true;
        }

        _corners[index] = height;
        if (height > _maxCorner)
        {
            _maxCorner = height;
        }
    }

    /// <summary>Hauteur en mètres au point (x, z).</summary>
    public double Sample(double x, double z) => Evaluate(x, z, out _, out _);

    /// <summary>Normale unitaire (y vers le haut) au point (x, z), à partir des dérivées analytiques.</summary>
    public Vec3 SampleNormal(double x, double z)
    {
        Evaluate(x, z, out var dhdx, out var dhdz);
        var inv = 1.0 / Math.Sqrt((dhdx * dhdx) + 1.0 + (dhdz * dhdz));
        return new Vec3(-dhdx * inv, inv, -dhdz * inv);
    }

    private double Evaluate(double x, double z, out double dhdx, out double dhdz)
    {
        var value = CatmullRom.Evaluate(_corners, CellsX + 1, CellsZ + 1, x / CellSize, z / CellSize, out var du, out var dv);
        dhdx = du / CellSize;
        dhdz = dv / CellSize;
        return value;
    }

    private int CornerIndex(int i, int j)
    {
        if (i < 0 || i > CellsX)
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, $"Coin hors grille (0 à {CellsX}).");
        }

        if (j < 0 || j > CellsZ)
        {
            throw new ArgumentOutOfRangeException(nameof(j), j, $"Coin hors grille (0 à {CellsZ}).");
        }

        return (j * (CellsX + 1)) + i;
    }
}
