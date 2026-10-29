using System.Runtime.InteropServices;
using Golferoo.Sim.Save;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>
/// Terrain simulé : champ de hauteur lissé et une surface discrète par cellule, plus le parcours (repères des trous).
/// Unités : mètres, x vers l'est, z vers le sud, y la hauteur.
/// </summary>
public sealed class TerrainGrid
{
    private readonly Surface[] _surfaces;

    /// <summary>Terrain d'un trou : un parcours d'un seul trou, de numéro 1 et de par déduit.</summary>
    public TerrainGrid(HeightField heights, string name, Point2 tee, Point2 green, Point2 hole)
        : this(heights, name, new Course([new CourseHole(1, tee, green, hole, DeducedPar(tee, hole))]))
    {
    }

    public TerrainGrid(HeightField heights, string name, Course course)
    {
        Heights = heights ?? throw new ArgumentNullException(nameof(heights));
        Name = name;
        Course = course ?? throw new ArgumentNullException(nameof(course));
        _surfaces = new Surface[heights.CellsX * heights.CellsZ];
        Array.Fill(_surfaces, Surface.Rough);
        Turf = new Golferoo.Sim.Turf.TurfGrid(this);
        Soil = new SoilGrid(this);
        Puddles = new Puddles(this);
    }

    /// <summary>Hauteur de l'herbe et consignes d'entretien ; le sol les lit.</summary>
    public Golferoo.Sim.Turf.TurfGrid Turf { get; }

    /// <summary>Sol par cellule (roulement, rebond, frottement) que lit la balle ; la saison s'y règle.</summary>
    public SoilGrid Soil { get; }

    /// <summary>Flaques après la pluie, selon le relief et l'humidité du sol.</summary>
    public Puddles Puddles { get; }

    public HeightField Heights { get; }

    /// <summary>Les trous du parcours posés sur cette carte : un seul pour un fichier de trou.</summary>
    public Course Course { get; internal set; }

    /// <summary>Arbres isolés listés par le fichier du trou, plantés à la création du monde (vide hors fichier).</summary>
    public IReadOnlyList<Golferoo.Sim.Trees.TreePlacement> IsolatedTrees { get; internal set; } = [];

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

    /// <summary>Départ du trou 1, en mètres (raccourci de <c>Course[1].Tee</c> ; les groupes ne jouent que le trou 1 jusqu'au lot 2 de C12).</summary>
    public Point2 Tee => Course[1].Tee;

    /// <summary>Terre vierge (C07) : le fichier ne donne que des repères provisoires, départ et trou sont à poser et le trou reste fermé jusqu'à ce qu'il soit valide.</summary>
    public bool Blank { get; internal set; }

    /// <summary>Centre du green du trou 1, en mètres.</summary>
    public Point2 Green => Course[1].Green;

    /// <summary>Par du trou 1 : celui du fichier, sinon déduit de la distance tee-trou.</summary>
    public int Par
    {
        get => Course[1].Par;
        internal set => Course[1].Par = value;
    }

    /// <summary>Barème de l'USGA : jusqu'à 230 m par 3, jusqu'à 430 m par 4, au-delà par 5.</summary>
    public static int DeducedPar(Point2 tee, Point2 hole)
    {
        var d = Math.Sqrt(((hole.X - tee.X) * (hole.X - tee.X)) + ((hole.Z - tee.Z) * (hole.Z - tee.Z)));
        return d <= 230.0 ? 3 : d <= 430.0 ? 4 : 5;
    }

    /// <summary>Drapeau du trou 1, en mètres.</summary>
    public Point2 Hole => Course[1].Hole;

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

    internal bool IsOutsideGreen(int cx, int cz) =>
        cx < 0 || cx >= CellsX || cz < 0 || cz >= CellsZ || _surfaces[(cz * CellsX) + cx] != Surface.Green;

    public SurfaceProperties PropertiesAt(double x, double z) => SurfaceTable.Get(SurfaceAt(x, z));

    /// <summary>
    /// Édition : change la hauteur du coin (i, j) de la grille de cellules. Bas niveau : sur un monde peuplé, les altitudes de cime
    /// des arbres ne sont pas recalculées ; passer par <see cref="World.RaiseTerrain"/>, qui appelle ensuite
    /// <see cref="Golferoo.Sim.Trees.TreeStand.RefreshTops"/>, ou l'appeler soi-même sur la zone modifiée.
    /// </summary>
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

    public CellRect? PaintDisc(Point2 center, double radius, Surface surface) => PaintDisc(center, radius, surface, null, out _);

    /// <summary>
    /// Comme <see cref="PaintDisc(Point2, double, Surface)"/>, mais seules les cellules de surface <paramref name="only"/> sont changées
    /// (toutes si null) ; <paramref name="changed"/> reçoit leur nombre.
    /// </summary>
    public CellRect? PaintDisc(Point2 center, double radius, Surface surface, Surface? only, out int changed) =>
        PaintCells(center, radius, surface, only, apply: true, out changed);

    /// <summary>Nombre de cellules que <see cref="PaintDisc(Point2, double, Surface, Surface?, out int)"/> changerait, sans rien changer (prix d'une commande).</summary>
    public int CountPaint(Point2 center, double radius, Surface surface, Surface? only = null)
    {
        PaintCells(center, radius, surface, only, apply: false, out var changed);
        return changed;
    }

    private CellRect? PaintCells(Point2 center, double radius, Surface surface, Surface? only, bool apply, out int changed)
    {
        changed = 0;
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

                var current = _surfaces[(cz * CellsX) + cx];
                if (current != surface && (only is null || current == only))
                {
                    if (apply)
                    {
                        _surfaces[(cz * CellsX) + cx] = surface;
                    }

                    changed++;
                    rect = rect is { } r ? r.Include(cx, cz) : new CellRect(cx, cz, cx, cz);
                }
            }
        }

        if (rect is not null && apply)
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
    public CellRect? RaiseDisc(Point2 center, double radius, double deltaMeters) => RaiseCorners(center, radius, deltaMeters, apply: true, out _);

    /// <summary>
    /// Volume de terre, en m³, que <see cref="RaiseDisc"/> déplacerait, sans rien changer (prix d'une commande) : somme des variations absolues
    /// de hauteur des coins, chaque coin comptant pour une cellule (<see cref="CellSize"/>²).
    /// </summary>
    public double RaiseVolume(Point2 center, double radius, double deltaMeters)
    {
        RaiseCorners(center, radius, deltaMeters, apply: false, out var volume);
        return volume;
    }

    private CellRect? RaiseCorners(Point2 center, double radius, double deltaMeters, bool apply, out double volume)
    {
        volume = 0;
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

                volume += Math.Abs(value - old) * CellSize * CellSize;
                if (apply)
                {
                    Heights.SetCornerHeight(i, j, value);
                }

                var cell = new CellRect(Math.Max(0, i - 1), Math.Max(0, j - 1), Math.Min(CellsX - 1, i), Math.Min(CellsZ - 1, j));
                rect = rect is { } r ? r.Union(cell) : cell;
            }
        }

        if (rect is not null && apply)
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

        Course.MixInto(ref hash);
        return hash;
    }

    /// <summary>Écrit tout l'état du terrain, gazon compris. Sol, flaques et champs de flux se recalculent : ils ne sont pas écrits.</summary>
    internal void Write(SaveWriter w)
    {
        Heights.Write(w);
        w.String(Name);
        Course.Write(w);
        w.Bool(Blank);
        w.Long(Version);
        w.Long(SurfaceVersion);
        w.Long(HeightVersion);
        w.Bytes(MemoryMarshal.AsBytes(Surfaces));
        w.List(IsolatedTrees, p =>
        {
            w.Point(new Point2(p.X, p.Z));
            w.Byte((byte)p.Species);
            w.Int(p.AgeMonths);
        });
        Turf.Write(w);
    }

    internal static TerrainGrid Read(SaveReader r)
    {
        var heights = HeightField.Read(r);
        var name = r.String();
        var course = Course.Read(r);
        var terrain = new TerrainGrid(heights, name, course)
        {
            Blank = r.Bool(),
            Version = r.Long(),
            SurfaceVersion = r.Long(),
            HeightVersion = r.Long(),
        };
        var surfaces = r.Bytes(terrain._surfaces.Length);
        for (var i = 0; i < surfaces.Length; i++)
        {
            terrain._surfaces[i] = surfaces[i] < SurfaceTable.Count ? (Surface)surfaces[i] : throw new InvalidDataException("Surface inconnue.");
        }

        terrain.IsolatedTrees = r.List(() =>
        {
            var at = r.Point();
            return new TreePlacement(at.X, at.Z, (TreeSpeciesId)r.Byte(), r.Int());
        });
        terrain.Turf.Read(r);
        return terrain;
    }

    internal static void Mix(ref ulong hash, ulong value)
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
