using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Turf;

/// <summary>
/// Hauteur de l'herbe par cellule, en 64<sup>e</sup> de la hauteur de référence de sa surface (un octet : 64 = référence, 255 = quatre fois).
/// Tout se met à jour au changement de jour (<see cref="NextDay"/>) ; la balle lit le résultat par <see cref="SoilGrid"/>.
/// </summary>
public sealed class TurfGrid
{
    /// <summary>Hauteur de référence (jour 0, consignes normales), en unités de hauteur.</summary>
    public const byte Reference = 64;

    private const double StimpSpeed = 1.83;
    private const double MetersPerFoot = 0.3048;

    // Racine de (h / référence) : exactement 1 à la référence.
    private static readonly double[] RollByHeight = Enumerable.Range(0, 256).Select(h => Math.Sqrt(h / (double)Reference)).ToArray();

    private readonly TerrainGrid _terrain;
    private readonly byte[] _height;
    private readonly byte[] _wear;
    private TurfSettings _settings = TurfSettings.Normal;

    public TurfGrid(TerrainGrid terrain)
    {
        _terrain = terrain;
        _height = new byte[terrain.CellsX * terrain.CellsZ];
        _wear = new byte[_height.Length];
        Array.Fill(_height, Reference);
    }

    /// <summary>Usure d'un pas de golfeur, d'une motte (coup depuis un fairway ou un départ) et d'une marque de balle sur un green, en crans.</summary>
    public const int StepWear = 1, DivotWear = 16, MarkWear = 8;

    /// <summary>Réparation de l'usure par nuit, doublée après la pluie : un sentier saturé (255) disparaît en un mois sans passage.</summary>
    public const int RepairWear = 8;

    /// <summary>Usure de la cellule (0 à 255). Elle ne change ni la balle ni le coût de marche : pas de <see cref="Version"/>.</summary>
    public byte Wear(int cx, int cz) => _wear[(cz * _terrain.CellsX) + cx];

    /// <summary>Ajoute de l'usure à la cellule si c'est de l'herbe (départ, green, fairway, rough) ; sature à 255.</summary>
    internal void AddWear(int cx, int cz, int amount)
    {
        var i = (cz * _terrain.CellsX) + cx;
        if (_terrain.Surfaces[i] <= Surface.Rough)
        {
            _wear[i] = (byte)Math.Min(_wear[i] + amount, 255);
        }
    }

    /// <summary>Change à chaque modification de hauteur ou de consigne : le sol se recalcule alors.</summary>
    public long Version { get; private set; }

    public TurfSettings Settings
    {
        get => _settings;
        set
        {
            if (value != _settings)
            {
                _settings = value;
                Version++;
            }
        }
    }

    internal double RollFactor(int cell) => RollByHeight[_height[cell]];

    /// <summary>Hauteur de l'herbe de la cellule, en mm (hauteur de référence de la surface × hauteur / 64).</summary>
    public double HeightMm(int cx, int cz)
    {
        var i = (cz * _terrain.CellsX) + cx;
        return _terrain.Surfaces[i] switch
        {
            Surface.Tee => 10.0,
            Surface.Green => 4.0,
            Surface.Fairway => 15.0,
            _ => 50.0,
        } * _height[i] / Reference;
    }

    /// <summary>
    /// Changement de jour : l'usure se répare de <see cref="RepairWear"/> crans (le double après la pluie) ; l'herbe pousse (saison, ×1,5 s'il a plu la veille), puis la tonte la ramène à la hauteur visée :
    /// greens chaque jour, départs et fairways un jour sur trois, rough une fois par semaine. La tonte ne fait que baisser.
    /// </summary>
    public void NextDay(Season season, bool rainedYesterday, int dayIndex)
    {
        var rain = rainedYesterday ? 1.5 : 1.0;
        var (rate, fineRate) = season switch { Season.Spring => (8.0, 4.0), Season.Summer => (6.0, 3.0), Season.Autumn => (4.0, 2.0), _ => (0.0, 0.0) };
        var roughGrowth = (int)Math.Round(rate * rain, MidpointRounding.AwayFromZero);
        var fineGrowth = (int)Math.Round(fineRate * rain, MidpointRounding.AwayFromZero);
        var cutThird = dayIndex % 3 == 0;
        var cutWeek = dayIndex % 7 == 0;
        var green = Target(_settings.GreenMm, 4.0);
        var rough = Target(_settings.RoughMm, 50.0);
        var repair = rainedYesterday ? 2 * RepairWear : RepairWear;
        var surfaces = _terrain.Surfaces;
        for (var i = 0; i < _height.Length; i++)
        {
            var s = surfaces[i];
            if (s > Surface.Rough)
            {
                continue;
            }

            _wear[i] = (byte)Math.Max(_wear[i] - repair, 0);
            var h = _height[i] + (s == Surface.Rough ? roughGrowth : fineGrowth);
            var cut = s switch { Surface.Green => green, Surface.Rough => cutWeek ? rough : 255, _ => cutThird ? Reference : 255 };
            _height[i] = (byte)Math.Min(Math.Min(h, cut), 255);
        }

        Version++;
    }

    private static int Target(double mm, double referenceMm) => (int)Math.Round(Reference * mm / referenceMm, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Vitesse des greens : distance que parcourt une balle lâchée à 1,83 m/s (stimpmètre) sur la résistance moyenne des greens,
    /// en mètres et en pieds. Ne dépend que de la hauteur : saison et humidité n'y entrent pas.
    /// </summary>
    public (double Meters, double Feet) Stimp()
    {
        var surfaces = _terrain.Surfaces;
        double sum = 0;
        var n = 0;
        for (var i = 0; i < _height.Length; i++)
        {
            if (surfaces[i] == Surface.Green)
            {
                sum += RollByHeight[_height[i]];
                n++;
            }
        }

        var resistance = SurfaceTable.Get(Surface.Green).RollResistance * (n == 0 ? 1.0 : sum / n);
        var meters = StimpSpeed * StimpSpeed / (2.0 * resistance * BallSimulator.Gravity);
        return (meters, meters / MetersPerFoot);
    }

    /// <summary>Empreinte FNV-1a 64 bits (consignes, hauteurs et usure), pour <see cref="World.Checksum"/>.</summary>
    public ulong Checksum()
    {
        var hash = 0xCBF29CE484222325UL;
        hash = (hash ^ ((byte)_settings.GreenSpeed | ((ulong)(byte)_settings.FairwayFirmness << 8) | ((ulong)(byte)_settings.RoughLength << 16))) * 0x100000001B3UL;
        foreach (var h in _height)
        {
            hash = (hash ^ h) * 0x100000001B3UL;
        }

        foreach (var w in _wear)
        {
            hash = (hash ^ w) * 0x100000001B3UL;
        }

        return hash;
    }
}
