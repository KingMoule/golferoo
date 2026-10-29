using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim;

/// <summary>
/// Vue d'un golfeur pour le rendu : copie de valeurs, sans référence vers la simulation.
/// <paramref name="Ball3D"/> est la position 3D de la balle (hauteur comprise) : en vol, l'échantillon courant de sa trajectoire ;
/// sinon la balle posée sur le terrain. <paramref name="Pin"/> est le drapeau du trou en cours du groupe (le rendu y oriente le swing).
/// </summary>
public readonly record struct GolferView(int Id, int GroupId, Point2 Position, Point2 Ball, GolferState State, int Strokes, bool Holed, Vec3 Ball3D = default, bool PickedUp = false, Point2 Pin = default);

/// <summary>
/// Instantané en lecture seule du monde. Passe-le à <see cref="World.Snapshot(WorldSnapshot?)"/> à chaque image
/// pour le réutiliser : plus aucune allocation une fois la liste à sa taille.
/// </summary>
public sealed class WorldSnapshot
{
    private readonly List<GolferView> _golfers = [];

    private readonly List<Tree> _trees = [];

    public long Tick { get; internal set; }

    /// <summary>Arbres du monde, recopiés seulement quand <see cref="TreesVersion"/> change.</summary>
    public IReadOnlyList<Tree> Trees => _trees;

    /// <summary>Version du <see cref="TreeStand"/> au moment de la copie (-1 : jamais copié).</summary>
    public long TreesVersion { get; internal set; } = -1;

    internal List<Tree> TreeItems => _trees;

    public IReadOnlyList<GolferView> Golfers => _golfers;

    internal List<GolferView> Items => _golfers;
}
