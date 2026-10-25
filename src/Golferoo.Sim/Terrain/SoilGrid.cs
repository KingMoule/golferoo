using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>
/// Sol par cellule : résistance au roulement, restitution et frottement, précalculés depuis <see cref="SurfaceTable"/>
/// la saison et l'humidité. La balle les lit ici. L'été est la référence (facteurs à 1 : valeurs identiques à la table, au bit près ;
/// d'où des <see cref="double"/> et non des <see cref="float"/>). Recalcul complet à un changement de saison, d'humidité ou de surface.
/// </summary>
public sealed class SoilGrid
{
    private readonly TerrainGrid _terrain;
    private readonly double[] _rollResistance;
    private readonly double[] _restitution;
    private readonly double[] _friction;
    private long _version = -1;
    private Season _computedFor;
    private double _wetFor;

    public SoilGrid(TerrainGrid terrain)
    {
        _terrain = terrain;
        var cells = terrain.CellsX * terrain.CellsZ;
        _rollResistance = new double[cells];
        _restitution = new double[cells];
        _friction = new double[cells];
    }

    /// <summary>Saison du sol ; la changer fait recalculer le tableau à la prochaine lecture.</summary>
    /// <remarks>Réglée par le <c>World</c> : un seul monde par terrain.</remarks>
    public Season Season { get; set; } = Season.Summer;

    /// <summary>Humidité du sol, de 0 (sec) à 1 (pluie du jour) : la balle roule moins et rebondit moins. Réglée par le <c>World</c>.</summary>
    public double Wetness { get; set; }

    public double RollResistanceAt(double x, double z)
    {
        Refresh();
        return _rollResistance[IndexAt(x, z)];
    }

    public double RestitutionAt(double x, double z)
    {
        Refresh();
        return _restitution[IndexAt(x, z)];
    }

    public double FrictionAt(double x, double z)
    {
        Refresh();
        return _friction[IndexAt(x, z)];
    }

    private static double RollFactor(Season season) => season switch
    {
        Season.Spring => 1.18,
        Season.Autumn or Season.Winter => 1.08,
        _ => 1.0,
    };

    private static double RestitutionFactor(Season season) => season switch
    {
        Season.Spring => 0.9,
        Season.Autumn or Season.Winter => 0.95,
        _ => 1.0,
    };

    /// <summary>Sol mouillé : à humidité 1, résistance au roulement ×1,7 (environ un tiers de roulement en moins), restitution ×0,8.</summary>
    private const double WetRoll = 0.7;
    private const double WetRestitution = 0.2;

    private int IndexAt(double x, double z) => (_terrain.CellZOf(z) * _terrain.CellsX) + _terrain.CellXOf(x);

    private void Refresh()
    {
        if (_version == _terrain.SurfaceVersion && _computedFor == Season && _wetFor == Wetness)
        {
            return;
        }

        var roll = RollFactor(Season) * (1.0 + (WetRoll * Wetness));
        var rest = RestitutionFactor(Season) * (1.0 - (WetRestitution * Wetness));
        var surfaces = _terrain.Surfaces;
        for (var i = 0; i < surfaces.Length; i++)
        {
            var p = SurfaceTable.Get(surfaces[i]);
            _rollResistance[i] = p.RollResistance * roll;
            _restitution[i] = p.Restitution * rest;
            _friction[i] = p.Friction;
        }

        _version = _terrain.SurfaceVersion;
        _computedFor = Season;
        _wetFor = Wetness;
    }
}
