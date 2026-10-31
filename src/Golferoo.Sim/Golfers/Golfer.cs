using Golferoo.Sim.Save;
using Golferoo.Sim.Scoring;
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

    /// <summary>Marche du green qu'il vient de finir vers le départ du trou suivant.</summary>
    ToNextTee = 10,

    /// <summary>Groupe de visiteurs qui a fini sa partie : marche vers la porte du pavillon, puis quitte le monde (C15).</summary>
    Leaving = 11,

    /// <summary>A abandonné la partie (C16) : marche vers la porte du pavillon, puis <see cref="Done"/> ; ses partenaires continuent sans lui.</summary>
    Quitting = 12,

    /// <summary>Marche vers la porte du snack-bar ou du pro shop qu'il a choisi (C17) ; arrivé, il paie et reprend l'état de <see cref="Golfer.Resume"/>.</summary>
    Visiting = 13,
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

    /// <summary>Carte de score de la partie, un trou par ligne du parcours (0 : trou pas encore joué, ou sauté parce que fermé). Nulle hors d'un groupe.</summary>
    public ScoreCard? Card { get; internal set; }

    /// <summary>Coups joués sur le trou en cours (pénalités comprises) ; remis à zéro au départ du trou suivant.</summary>
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

    /// <summary>Planification du coup en cours, étalée sur les ticks du geste (C12 lot 4) ; nulle pour un putt, un coup du joueur ou quand le geste est fini.</summary>
    internal PlanJob? Plan { get; set; }

    /// <summary>Coup choisi par le joueur (votre pro), joué au prochain geste.</summary>
    internal Pro.ProShot? PlannedShot { get; set; }

    /// <summary>Numéro du visiteur du vivier (<see cref="VisitorPool"/>) que ce golfeur incarne (−1 : ni visiteur arrivé au pavillon, ni membre).</summary>
    public int VisitorId { get; internal set; } = -1;

    /// <summary>Humeur de −10 à +10 (<see cref="MoodParams"/>) ; ne vit que chez les visiteurs du vivier (<see cref="VisitorId"/> ≥ 0).</summary>
    public int Mood { get; internal set; }

    /// <summary>A abandonné la partie : il ne joue plus et ne paie plus les trous restants (C14).</summary>
    public bool Quit { get; internal set; }

    /// <summary>Combien de fois chaque événement a touché ce golfeur (<see cref="MoodEvent"/>), contrariétés mineures isolées comprises.</summary>
    internal int[] Occurrences { get; } = new int[MoodParams.EventCount];

    /// <summary>Ticks passés à attendre le groupe devant sur le trou en cours ; remis à zéro au départ du trou suivant.</summary>
    internal int WaitTicks { get; set; }

    /// <summary>Trous finis qu'il a aimés : au moins un événement positif sur le trou (C16 lot 2, adhésion).</summary>
    public int LikedHoles { get; internal set; }

    /// <summary>Un événement positif a touché le golfeur sur le trou en cours.</summary>
    internal bool LikedThisHole { get; set; }

    /// <summary>Le golfeur joue une journée de ligue.</summary>
    internal bool InLeague { get; set; }

    /// <summary>Genres de bâtiment (un bit par <see cref="Buildings.BuildingKind"/>) dont le golfeur a déjà eu l'occasion cette partie : visité ou non, on ne le lui propose qu'une fois.</summary>
    internal byte Offered { get; set; }

    /// <summary>Bâtiment qu'il visite (état <see cref="GolferState.Visiting"/>).</summary>
    internal Buildings.BuildingKind VisitKind { get; set; }

    /// <summary>Porte du bâtiment qu'il visite.</summary>
    internal Point2 VisitDoor { get; set; }

    /// <summary>État qu'il reprend après sa visite.</summary>
    internal GolferState Resume { get; set; }

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

        w.Bool(Plan is not null);
        Plan?.Write(w);
        w.Bool(InLeague);
        w.Int(WaypointIndex);
        w.Bool(Card is not null);
        Card?.Write(w);
        w.Int(VisitorId);
        w.Int(Mood);
        w.Bool(Quit);
        w.List(Occurrences, w.Int);
        w.Int(WaitTicks);
        w.Int(LikedHoles);
        w.Bool(LikedThisHole);
        w.Byte(Offered);
        w.Byte((byte)VisitKind);
        w.Point(VisitDoor);
        w.Byte((byte)Resume);
    }

    internal static Golfer Read(SaveReader r)
    {
        // Le numéro de visiteur (v13) puis l'humeur (v14) viennent en dernier : on les lit après la carte.

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
        golfer.Plan = r.Bool() ? PlanJob.Read(r) : null;
        golfer.InLeague = r.Bool();
        golfer.WaypointIndex = r.Int();
        golfer.Card = r.Bool() ? ScoreCard.Read(r) : null;
        if (r.Version >= 13)
        {
            golfer.VisitorId = r.Int();
        }

        if (r.Version >= 14)
        {
            golfer.Mood = r.Int();
            golfer.Quit = r.Bool();
            var occurrences = r.List(r.Int);
            golfer.WaitTicks = r.Int();
            if (r.Version < 18 && occurrences.Count == MoodParams.EventCount - 1)
            {
                occurrences.Add(0); // sauvegarde d'avant C17 lot 2 : l'événement « service visité » n'existait pas
            }

            if (occurrences.Count != MoodParams.EventCount || golfer.Mood is < MoodParams.Min or > MoodParams.Max || occurrences.Any(o => o < 0) || golfer.WaitTicks < 0)
            {
                throw new InvalidDataException("Sauvegarde incohérente : humeur d'un golfeur.");
            }

            occurrences.CopyTo(golfer.Occurrences);
        }

        if (r.Version >= 15)
        {
            golfer.LikedHoles = r.Int();
            golfer.LikedThisHole = r.Bool();
            if (golfer.LikedHoles < 0)
            {
                throw new InvalidDataException("Sauvegarde incohérente : trous aimés d'un golfeur.");
            }
        }
        else if (r.Version < 14 && golfer.VisitorId >= 0)
        {
            golfer.Mood = MoodParams.Default.Start; // sauvegarde v13 : les visiteurs en jeu repartent avec l'humeur de départ
        }

        if (r.Version >= 18)
        {
            golfer.Offered = r.Byte();
            golfer.VisitKind = (Buildings.BuildingKind)r.Byte();
            golfer.VisitDoor = r.Point();
            golfer.Resume = (GolferState)r.Byte();
            if (!Enum.IsDefined(golfer.VisitKind) || !Enum.IsDefined(golfer.Resume) || golfer.Resume == GolferState.Visiting
                || (golfer.State == GolferState.Visiting && golfer.VisitKind is not (Buildings.BuildingKind.SnackBar or Buildings.BuildingKind.ProShop)))
            {
                throw new InvalidDataException("Sauvegarde incohérente : visite d'un golfeur.");
            }
        }

        return golfer;
    }
}
