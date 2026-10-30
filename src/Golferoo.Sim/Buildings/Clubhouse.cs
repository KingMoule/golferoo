using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Buildings;

/// <summary>Le pavillon : le premier bâtiment, de 12 × 8 cellules, avec une porte.</summary>
public static class Clubhouse
{
    public const int Width = 12;
    public const int Depth = 8;

    /// <summary>Pavillon dont l'angle nord-ouest est la cellule (<paramref name="minX"/>, <paramref name="minZ"/>).</summary>
    public static Building At(int minX, int minZ, DoorSide side) =>
        new(BuildingKind.Clubhouse, new CellRect(minX, minZ, minX + Width - 1, minZ + Depth - 1), side);
}
