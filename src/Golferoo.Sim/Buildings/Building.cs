using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Buildings;

/// <summary>Genre de bâtiment. Les valeurs servent d'index stable (sauvegarde) : ne pas réordonner.</summary>
public enum BuildingKind : byte
{
    Clubhouse,
    SnackBar,
    ProShop,
    MaintenanceShed,
    Bench,
}

/// <summary>Côté de l'emprise où s'ouvre la porte (z croît vers le sud).</summary>
public enum DoorSide : byte
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// Bâtiment posé sur la grille (C15, que C17 étend aux autres genres) : une emprise rectangulaire de cellules qui bloque le passage et les arbres,
/// et une porte, la cellule libre qui touche le milieu du côté <see cref="Side"/>.
/// </summary>
public sealed record Building(BuildingKind Kind, CellRect Footprint, DoorSide Side)
{
    /// <summary>Cellule de la porte, hors de l'emprise.</summary>
    public (int X, int Z) DoorCell => Side switch
    {
        DoorSide.North => ((Footprint.MinX + Footprint.MaxX) / 2, Footprint.MinZ - 1),
        DoorSide.South => ((Footprint.MinX + Footprint.MaxX) / 2, Footprint.MaxZ + 1),
        DoorSide.West => (Footprint.MinX - 1, (Footprint.MinZ + Footprint.MaxZ) / 2),
        _ => (Footprint.MaxX + 1, (Footprint.MinZ + Footprint.MaxZ) / 2),
    };

    /// <summary>Position du bâtiment : le centre de son emprise, en mètres.</summary>
    public Point2 Position(double cellSize) => new((Footprint.MinX + Footprint.MaxX + 1) * 0.5 * cellSize, (Footprint.MinZ + Footprint.MaxZ + 1) * 0.5 * cellSize);

    /// <summary>Centre de la cellule de la porte, en mètres.</summary>
    public Point2 DoorPosition(double cellSize) => new((DoorCell.X + 0.5) * cellSize, (DoorCell.Z + 0.5) * cellSize);

    internal void Write(SaveWriter w)
    {
        w.Byte((byte)Kind);
        w.Int(Footprint.MinX);
        w.Int(Footprint.MinZ);
        w.Int(Footprint.MaxX);
        w.Int(Footprint.MaxZ);
        w.Byte((byte)Side);
    }

    internal static Building Read(SaveReader r)
    {
        var kind = (BuildingKind)r.Byte();
        var rect = new CellRect(r.Int(), r.Int(), r.Int(), r.Int());
        var side = (DoorSide)r.Byte();
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(side) || rect.MaxX < rect.MinX || rect.MaxZ < rect.MinZ)
        {
            throw new InvalidDataException("Sauvegarde incohérente : bâtiment impossible.");
        }

        return new Building(kind, rect, side);
    }

    internal void Mix(ref ulong hash)
    {
        World.Mix(ref hash, (ulong)Kind | ((ulong)Side << 8));
        World.Mix(ref hash, ((ulong)(uint)Footprint.MinX << 32) | (uint)Footprint.MinZ);
        World.Mix(ref hash, ((ulong)(uint)Footprint.MaxX << 32) | (uint)Footprint.MaxZ);
    }
}
