namespace Golferoo.Sim.Terrain;

/// <summary>Rectangle de cellules, bornes incluses (indices de cellules).</summary>
public readonly record struct CellRect(int MinX, int MinZ, int MaxX, int MaxZ)
{
    public bool Contains(int cx, int cz) => cx >= MinX && cx <= MaxX && cz >= MinZ && cz <= MaxZ;

    public CellRect Include(int cx, int cz) =>
        new(Math.Min(MinX, cx), Math.Min(MinZ, cz), Math.Max(MaxX, cx), Math.Max(MaxZ, cz));

    public CellRect Union(CellRect other) =>
        new(Math.Min(MinX, other.MinX), Math.Min(MinZ, other.MinZ), Math.Max(MaxX, other.MaxX), Math.Max(MaxZ, other.MaxZ));

    /// <summary>Rectangle élargi de <paramref name="margin"/> cellules, borné à une grille de <paramref name="cellsX"/> x <paramref name="cellsZ"/>.</summary>
    public CellRect Expand(int margin, int cellsX, int cellsZ) =>
        new(Math.Max(0, MinX - margin), Math.Max(0, MinZ - margin), Math.Min(cellsX - 1, MaxX + margin), Math.Min(cellsZ - 1, MaxZ + margin));
}
