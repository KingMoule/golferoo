using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Golfers;

/// <summary>Étape du trou où se trouve un golfeur. Les valeurs entrent dans la somme de contrôle : ne pas réordonner.</summary>
public enum GolferState : byte
{
    /// <summary>Marche vers le tee.</summary>
    Arriving = 0,

    /// <summary>Debout près de sa balle, attend son tour.</summary>
    WaitingTurn = 1,

    /// <summary>Balance son coup (hors green).</summary>
    Swinging = 2,

    /// <summary>Putte (balle sur le green).</summary>
    Putting = 3,

    /// <summary>Marche vers sa balle.</summary>
    WalkingToBall = 4,

    /// <summary>Marche vers le point de sortie.</summary>
    Exiting = 5,

    /// <summary>A quitté le green.</summary>
    Done = 6,

    /// <summary>Mode charge : marche en boucle entre des points tirés au hasard, sans jouer.</summary>
    Roaming = 7,
}

public sealed class Golfer
{
    internal Golfer(int id, Point2 position, GolferState state)
    {
        Id = id;
        Position = position;
        State = state;
        Ball = position;
    }

    public int Id { get; }

    public Point2 Position { get; internal set; }

    public GolferState State { get; internal set; }

    /// <summary>Coups joués sur le trou (pénalités comprises, il n'y en a pas encore).</summary>
    public int Strokes { get; internal set; }

    /// <summary>Position de la balle de ce golfeur.</summary>
    public Point2 Ball { get; internal set; }

    public bool Holed { get; internal set; }

    /// <summary>Ticks restants du geste en cours.</summary>
    internal int SwingTicks { get; set; }

    /// <summary>Mode charge : indice du point de passage visé.</summary>
    internal int WaypointIndex { get; set; }
}
