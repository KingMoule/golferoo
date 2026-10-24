using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Monde simulé, avancé par ticks fixes. Le rendu lit son état et lui envoie des commandes ;
/// il ne connaît pas Godot.
/// </summary>
public sealed class World
{
    /// <summary>Fréquence fixe de la simulation (spec C01 : 20 Hz).</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Durée d'un tick en secondes.</summary>
    public const double TickSeconds = 1.0 / TicksPerSecond;

    private readonly DeterministicRng _rng;

    public World(ulong seed, TerrainGrid? terrain = null)
    {
        Seed = seed;
        Terrain = terrain;
        _rng = new DeterministicRng(seed);
    }

    public ulong Seed { get; }

    /// <summary>Terrain du trou, s'il y en a un (son état entre dans la somme de contrôle).</summary>
    public TerrainGrid? Terrain { get; }

    /// <summary>Nombre de ticks écoulés depuis la création.</summary>
    public long Tick { get; private set; }

    public void Step()
    {
        // Le squelette ne simule encore rien : le tirage garde l'état du RNG lié au temps,
        // pour que la somme de contrôle prouve déjà le déterminisme.
        _rng.NextUInt64();
        Tick++;
    }

    public void Step(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Step();
        }
    }

    /// <summary>Somme de contrôle de l'état (FNV-1a 64 bits) : même graine et mêmes commandes, même valeur.</summary>
    public ulong Checksum()
    {
        var hash = 0xCBF29CE484222325UL;
        Mix(ref hash, Seed);
        Mix(ref hash, (ulong)Tick);
        Mix(ref hash, _rng.StateHash());
        if (Terrain is not null)
        {
            Mix(ref hash, Terrain.Checksum());
        }

        return hash;
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash *= 0x100000001B3UL;
        }
    }
}
