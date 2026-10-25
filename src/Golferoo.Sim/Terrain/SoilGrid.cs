using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>
/// Sol par cellule : résistance au roulement, restitution et frottement, précalculés depuis <see cref="SurfaceTable"/>
/// et la saison. La balle les lit ici. L'été est la référence (facteurs à 1 : valeurs identiques à la table, au bit près ;
/// d'où des <see cref="double"/> et non des <see cref="float"/>). Recalcul complet à un changement de saison ou de surface.
/// </summary>
public sealed class SoilGrid
{
    private readonly TerrainGrid _terrain;
    private readonly double[] _rollResistance;
    private readonly double[] _restitution;
    private readonly double[] _friction;
    private long _version = -1;
    private Season _computedFor;

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

    private int IndexAt(double x, double z) => (_terrain.CellZOf(z) * _terrain.CellsX) + _terrain.CellXOf(x);

    private void Refresh()
    {
        if (_version == _terrain.SurfaceVersion && _computedFor == Season)
        {
            return;
        }

        var roll = RollFactor(Season);
        var rest = RestitutionFactor(Season);
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
    }
}
