using Golferoo.Sim.Shots;
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

    /// <summary>La balle est en l'air ou roule ; le coup est compté quand elle s'arrête.</summary>
    BallInFlight = 8,
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

    /// <summary>Coups joués sur le trou (pénalités comprises).</summary>
    public int Strokes { get; internal set; }

    /// <summary>Position de la balle de ce golfeur.</summary>
    public Point2 Ball { get; internal set; }

    /// <summary>Niveau de jeu : multiplie la dispersion (1 = golfeur de référence, plus grand = plus brouillon). Voir <see cref="SkillLevels"/>.</summary>
    public double Skill { get; internal set; } = 1.0;

    public bool Holed { get; internal set; }

    /// <summary>Balle ramassée après le nombre maximal de coups, sans être entrée dans le trou.</summary>
    public bool PickedUp { get; internal set; }

    /// <summary>Résultat du coup en cours, appliqué quand la balle est au repos.</summary>
    internal ShotResult Pending { get; set; }

    /// <summary>Trajectoire du coup en cours (un échantillon par tick).</summary>
    internal IReadOnlyList<Vec3>? FlightPath { get; set; }

    /// <summary>Indice de l'échantillon courant de <see cref="FlightPath"/>.</summary>
    internal int FlightIndex { get; set; }

    /// <summary>Ticks restants du geste en cours.</summary>
    internal int SwingTicks { get; set; }

    /// <summary>Mode charge : indice du point de passage visé.</summary>
    internal int WaypointIndex { get; set; }
}
