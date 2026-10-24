using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Vue d'un golfeur pour le rendu : copie de valeurs, sans référence vers la simulation.
/// <paramref name="Ball3D"/> est la position 3D de la balle (hauteur comprise) : en vol, l'échantillon courant de sa trajectoire ;
/// sinon la balle posée sur le terrain.
/// </summary>
public readonly record struct GolferView(int Id, int GroupId, Point2 Position, Point2 Ball, GolferState State, int Strokes, bool Holed, Vec3 Ball3D = default);

/// <summary>
/// Instantané en lecture seule du monde. Passe-le à <see cref="World.Snapshot(WorldSnapshot?)"/> à chaque image
/// pour le réutiliser : plus aucune allocation une fois la liste à sa taille.
/// </summary>
public sealed class WorldSnapshot
{
    private readonly List<GolferView> _golfers = [];

    public long Tick { get; internal set; }

    public IReadOnlyList<GolferView> Golfers => _golfers;

    internal List<GolferView> Items => _golfers;
}
