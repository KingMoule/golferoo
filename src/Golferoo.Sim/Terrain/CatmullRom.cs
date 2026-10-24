namespace Golferoo.Sim.Terrain;

/// <summary>
/// Catmull-Rom bicubique sur une grille régulière de valeurs aux coins. Interpole (passe par les valeurs),
/// dérivée continue. Aux bords, les points fantômes prolongent linéairement : un plan est reproduit exactement.
/// </summary>
internal static class CatmullRom
{
    /// <summary>Valeur et dérivées en unités de grille. <paramref name="nx"/> et <paramref name="nz"/> sont des nombres de coins (au moins 2).</summary>
    public static double Evaluate(double[] data, int nx, int nz, double u, double v, out double du, out double dv)
    {
        u = Math.Clamp(u, 0.0, nx - 1);
        v = Math.Clamp(v, 0.0, nz - 1);
        var i = Math.Min((int)Math.Floor(u), nx - 2);
        var j = Math.Min((int)Math.Floor(v), nz - 2);
        var tu = u - i;
        var tv = v - j;

        Span<double> rowValue = stackalloc double[4];
        Span<double> rowSlope = stackalloc double[4];
        for (var r = 0; r < 4; r++)
        {
            var z = j - 1 + r;
            rowValue[r] = Cubic(
                Get(data, nx, nz, i - 1, z),
                Get(data, nx, nz, i, z),
                Get(data, nx, nz, i + 1, z),
                Get(data, nx, nz, i + 2, z),
                tu,
                out rowSlope[r]);
        }

        var value = Cubic(rowValue[0], rowValue[1], rowValue[2], rowValue[3], tv, out dv);
        du = Cubic(rowSlope[0], rowSlope[1], rowSlope[2], rowSlope[3], tv, out _);
        return value;
    }

    private static double Get(double[] data, int nx, int nz, int i, int j)
    {
        if (i < 0)
        {
            return (2 * Get(data, nx, nz, 0, j)) - Get(data, nx, nz, 1, j);
        }

        if (i >= nx)
        {
            return (2 * Get(data, nx, nz, nx - 1, j)) - Get(data, nx, nz, nx - 2, j);
        }

        if (j < 0)
        {
            return (2 * Get(data, nx, nz, i, 0)) - Get(data, nx, nz, i, 1);
        }

        if (j >= nz)
        {
            return (2 * Get(data, nx, nz, i, nz - 1)) - Get(data, nx, nz, i, nz - 2);
        }

        return data[(j * nx) + i];
    }

    private static double Cubic(double p0, double p1, double p2, double p3, double t, out double slope)
    {
        var a = (2 * p0) - (5 * p1) + (4 * p2) - p3;
        var b = (-p0) + (3 * p1) - (3 * p2) + p3;
        slope = 0.5 * ((p2 - p0) + (2 * a * t) + (3 * b * t * t));
        return 0.5 * ((2 * p1) + ((p2 - p0) * t) + (a * t * t) + (b * t * t * t));
    }
}
