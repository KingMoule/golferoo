using Golferoo.Sim.Save;
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

    /// <summary>Votre pro, à son tour : attend le coup du joueur (<see cref="World.PlayProShot"/>).</summary>
    AwaitingShot = 9,
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

    /// <summary>Membre de la ligue qui joue ce golfeur (−1 pour un golfeur ordinaire).</summary>
    public int MemberId { get; internal set; } = -1;

    public Point2 Position { get; internal set; }

    public GolferState State { get; internal set; }

    /// <summary>Coups joués sur le trou (pénalités comprises).</summary>
    public int Strokes { get; internal set; }

    /// <summary>Position de la balle de ce golfeur.</summary>
    public Point2 Ball { get; internal set; }

    /// <summary>Niveau de jeu : multiplie la dispersion (1 = golfeur de référence, plus grand = plus brouillon). Voir <see cref="SkillLevels"/>.</summary>
    public double Skill { get; internal set; } = 1.0;

    /// <summary>Style de jeu (<see cref="GolferProfile"/>) : celui du membre, ou tiré à l'arrivée pour un golfeur ordinaire.</summary>
    public Archetype Profile { get; internal set; }

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

    /// <summary>Coup choisi par le joueur (votre pro), joué au prochain geste.</summary>
    internal Pro.ProShot? PlannedShot { get; set; }

    /// <summary>Le golfeur joue une journée de ligue.</summary>
    internal bool InLeague { get; set; }

    /// <summary>Mode charge : indice du point de passage visé.</summary>
    internal int WaypointIndex { get; set; }

    /// <summary>Tout l'état du golfeur. Le résultat en attente partage sa trajectoire avec <see cref="FlightPath"/> : elle n'est écrite qu'une fois.</summary>
    internal void Write(SaveWriter w)
    {
        w.Int(Id);
        w.Int(MemberId);
        w.Point(Position);
        w.Byte((byte)State);
        w.Int(Strokes);
        w.Point(Ball);
        w.Double(Skill);
        w.Byte((byte)Profile);
        w.Bool(Holed);
        w.Bool(PickedUp);
        w.Point(Pending.Ball);
        w.Bool(Pending.Holed);
        w.Int(Pending.PenaltyStrokes);
        w.Byte((byte)Pending.Outcome);
        w.Bool(FlightPath is not null);
        if (FlightPath is not null)
        {
            w.List(FlightPath, w.Vec);
        }

        w.Int(FlightIndex);
        w.Int(SwingTicks);
        w.Bool(PlannedShot is not null);
        if (PlannedShot is { } shot)
        {
            w.ProShot(shot);
        }

        w.Bool(InLeague);
        w.Int(WaypointIndex);
    }

    internal static Golfer Read(SaveReader r)
    {
        var golfer = new Golfer(r.Int(), default, default)
        {
            MemberId = r.Int(),
            Position = r.Point(),
            State = (GolferState)r.Byte(),
            Strokes = r.Int(),
            Ball = r.Point(),
            Skill = r.Double(),
            Profile = (Archetype)r.Byte(),
            Holed = r.Bool(),
            PickedUp = r.Bool(),
        };
        var pending = (Ball: r.Point(), Holed: r.Bool(), Penalty: r.Int(), Outcome: (ShotOutcome)r.Byte());
        golfer.FlightPath = r.Bool() ? r.List(r.Vec) : null;
        golfer.Pending = new ShotResult(pending.Ball, pending.Holed, pending.Penalty, pending.Outcome, golfer.FlightPath);
        golfer.FlightIndex = r.Int();
        golfer.SwingTicks = r.Int();
        golfer.PlannedShot = r.Bool() ? r.ProShot() : null;
        golfer.InLeague = r.Bool();
        golfer.WaypointIndex = r.Int();
        return golfer;
    }
}
