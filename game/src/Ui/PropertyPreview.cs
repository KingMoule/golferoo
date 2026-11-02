using Godot;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Ui;

/// <summary>Miniature vue du dessus d'une propriété générée (C24) : un pixel par cellule, relief ombré, eau, bois et pavillon au centre. Calcul pur, sans nœud : sûr hors du fil principal.</summary>
public static class PropertyPreview
{
    private static readonly Color Low = new("5E9E52");
    private static readonly Color High = new("BDD98C");
    private static readonly Color Water = new("3E7FB5");
    private static readonly Color Wood = new("23502D");

    /// <summary>Pixels RVB (3 octets chacun) de la carte, ligne par ligne.</summary>
    public static byte[] Pixels(TerrainGrid terrain)
    {
        var (nx, nz) = (terrain.CellsX, terrain.CellsZ);
        double low = double.MaxValue, high = double.MinValue;
        for (var j = 0; j <= nz; j++)
        {
            for (var i = 0; i <= nx; i++)
            {
                var h = terrain.Heights.GetCornerHeight(i, j);
                low = Math.Min(low, h);
                high = Math.Max(high, h);
            }
        }

        var bytes = new byte[nx * nz * 3];
        for (var cz = 0; cz < nz; cz++)
        {
            for (var cx = 0; cx < nx; cx++)
            {
                var color = terrain.GetCellSurface(cx, cz) switch
                {
                    Surface.Water => Water,
                    Surface.Trees => Wood,
                    _ => Ground(terrain, cx, cz, low, high),
                };
                Put(bytes, nx, cx, cz, color);
            }
        }

        // Le pavillon, entouré d'un liseré clair pour qu'il se voie sur le gazon comme dans les bois.
        var (x0, z0) = ((nx - Clubhouse.Width) / 2, (nz - Clubhouse.Depth) / 2);
        for (var cz = z0 - 1; cz <= z0 + Clubhouse.Depth; cz++)
        {
            for (var cx = x0 - 1; cx <= x0 + Clubhouse.Width; cx++)
            {
                var inside = cx >= x0 && cx < x0 + Clubhouse.Width && cz >= z0 && cz < z0 + Clubhouse.Depth;
                Put(bytes, nx, cx, cz, inside ? UiTheme.Brick : UiTheme.Cream);
            }
        }

        return bytes;
    }

    private static Color Ground(TerrainGrid terrain, int cx, int cz, double low, double high)
    {
        var h = terrain.Heights;
        var t = (float)((h.GetCornerHeight(cx, cz) - low) / Math.Max(high - low, 1e-6));
        var slope = (float)(h.GetCornerHeight(cx, cz) - h.GetCornerHeight(cx + 1, cz + 1)); // lumière du nord-ouest
        return Low.Lerp(High, t) * Math.Clamp(0.9f + (slope * 0.25f), 0.7f, 1.2f);
    }

    private static void Put(byte[] bytes, int width, int cx, int cz, Color color)
    {
        var at = ((cz * width) + cx) * 3;
        bytes[at] = (byte)Math.Round(Math.Clamp(color.R, 0f, 1f) * 255f);
        bytes[at + 1] = (byte)Math.Round(Math.Clamp(color.G, 0f, 1f) * 255f);
        bytes[at + 2] = (byte)Math.Round(Math.Clamp(color.B, 0f, 1f) * 255f);
    }

    /// <summary>La miniature en texture (fil principal).</summary>
    public static ImageTexture Texture(TerrainGrid terrain, byte[] pixels) =>
        ImageTexture.CreateFromImage(Image.CreateFromData(terrain.CellsX, terrain.CellsZ, false, Image.Format.Rgb8, pixels));
}
