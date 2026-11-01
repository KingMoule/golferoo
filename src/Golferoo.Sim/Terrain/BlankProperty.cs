namespace Golferoo.Sim.Terrain;

/// <summary>
/// Propriété provisoire d'une nouvelle partie (C18) : une carte plate de 1 200 × 800 m en rough, sans arbre ni eau, avec un premier trou à 180 m (départ, fairway, green)
/// près du bord ouest, où le pavillon se pose. Même graine, même carte ; la graine nourrit le monde (météo, arrivées, ligue), non le terrain.
/// C24 remplacera ce générateur par une propriété générée.
/// </summary>
public static class BlankProperty
{
    public const double CellSize = 4.0;
    public const int CellsX = 300;
    public const int CellsZ = 200;

    private static readonly Point2 Tee = new(60.0, 400.0);
    private static readonly Point2 Pin = new(240.0, 400.0);

    public static TerrainGrid Create()
    {
        var terrain = new TerrainGrid(new HeightField(CellsX, CellsZ, CellSize), "Nouvelle propriété", Tee, Pin, Pin);
        Paint(terrain, Tee.X - 8, Tee.Z - 8, Tee.X + 8, Tee.Z + 8, Surface.Tee);
        Paint(terrain, Tee.X + 8, Tee.Z - 12, Pin.X - 12, Tee.Z + 12, Surface.Fairway);
        Paint(terrain, Pin.X - 12, Pin.Z - 12, Pin.X + 12, Pin.Z + 12, Surface.Green);
        return terrain;
    }

    private static void Paint(TerrainGrid terrain, double x0, double z0, double x1, double z1, Surface surface)
    {
        for (var cz = terrain.CellZOf(z0); cz <= terrain.CellZOf(z1 - 0.01); cz++)
        {
            for (var cx = terrain.CellXOf(x0); cx <= terrain.CellXOf(x1 - 0.01); cx++)
            {
                terrain.PaintCell(cx, cz, surface);
            }
        }
    }
}
