using Golferoo.Sim.Golfers;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim;

/// <summary>
/// Humeur (C16 lot 1) des visiteurs du vivier : les événements que la sim mesure déjà changent leur humeur (<see cref="MoodChanged"/>), elle baisse
/// un peu tous les quelques trous, et sous zéro le golfeur abandonne à la fin d'un trou (<see cref="GolferQuit"/>), ses partenaires perdent 1.
/// Les golfeurs de la ligue et votre pro n'ont pas d'humeur. Les réactions sont comptées par trou (<see cref="Reactions"/>).
/// </summary>
public sealed partial class World
{
    private BeautyMap? _beautyMap;

    /// <summary>Réglages de l'humeur (<c>data/golfers/mood.json</c> par défaut ; non sauvés, comme les données embarquées).</summary>
    public MoodParams Moods { get; }

    /// <summary>Réactions des golfeurs par trou et par événement, ce mois-ci et le mois précédent.</summary>
    public Reactions Reactions { get; private set; } = new();

    /// <summary>Un golfeur a réagi : l'humeur a changé (levé au tick de l'événement ; pas pour une contrariété mineure isolée).</summary>
    public event Action<MoodChange>? MoodChanged;

    /// <summary>Un golfeur abandonne la partie : son humeur est tombée sous zéro (levé à la fin du trou).</summary>
    public event Action<QuitInfo>? GolferQuit;

    private static bool HasMood(Golfer g) => g.VisitorId >= 0;

    /// <summary>
    /// Vrai si le golfeur porte une pensée : un visiteur du vivier à qui un évènement d'humeur est arrivé (une contrariété mineure compte à partir de sa
    /// <see cref="MoodParams.RepeatThreshold"/>-ième occurrence, comme pour la bulle).
    /// </summary>
    public bool CarriesThought(Golfer g) => HasMood(g) && Moods.Definitions.Any(d => g.Occurrences[(int)d.Event] >= (d.Minor ? Moods.RepeatThreshold : 1));

    /// <summary>
    /// Le joueur ouvre la fiche ou l'info-bulle du golfeur <paramref name="golferId"/> (C19) : si ce golfeur porte une pensée, <see cref="GameState.ThoughtRead"/> passe à vrai.
    /// Faux, et sans effet, pour un golfeur inconnu ou sans pensée.
    /// </summary>
    public bool ReadThought(int golferId)
    {
        var golfer = _groups.SelectMany(g => g.Golfers).Concat(_roamers).FirstOrDefault(g => g.Id == golferId);
        if (golfer is null || !CarriesThought(golfer))
        {
            return false;
        }

        State.ThoughtRead = true;
        return true;
    }

    private (int X, int Z) CellOf(Point2 p) => (Terrain!.CellXOf(p.X), Terrain.CellZOf(p.Z));

    /// <summary>
    /// L'événement <paramref name="e"/> touche le golfeur ; <see cref="MoodChange.Delta"/> est le changement réel, écrêté à ±10 (nul au plafond). Une contrariété mineure ne compte qu'à partir de sa
    /// <see cref="MoodParams.RepeatThreshold"/>-ième occurrence : avant, rien ne change, rien n'est compté ni levé.
    /// </summary>
    private void Raise(Golfer g, MoodEvent e, int hole, Point2 at)
    {
        var def = Moods.Definitions[(int)e];
        var seen = ++g.Occurrences[(int)e];
        if (def.Minor && seen < Moods.RepeatThreshold)
        {
            return;
        }

        var before = g.Mood;
        g.Mood = MoodParams.Clamp(before + Drained(def.Value));
        g.LikedThisHole |= def.Value > 0 && e is not (MoodEvent.ProPlaysWithUs or MoodEvent.ServiceVisited);
        var cell = CellOf(at);
        Reactions.Add(hole, e, cell);
        MoodChanged?.Invoke(new MoodChange(g, e, hole, cell, g.Mood - before));
    }

    /// <summary>Un changement d'humeur selon la difficulté : les baisses sont multipliées par <see cref="DifficultyParams.MoodDrain"/> (arrondi au plus loin de zéro, 1,0 ne change rien).</summary>
    private int Drained(int delta) => delta < 0 ? (int)Math.Round(delta * Difficulty.MoodDrain, MidpointRounding.AwayFromZero) : delta;

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(Dist2(a, b));

    /// <summary>Un coup vient de s'arrêter (<paramref name="from"/> : d'où il est parti) : putt réussi ou raté, eau, bunker, arbre, coup décevant, et balle qui frôle un autre groupe.</summary>
    private void AfterShot(Group group, Golfer g, Point2 from, bool putt, in ShotResult result)
    {
        var terrain = Terrain!;
        var hole = group.Hole;
        var pin = terrain.Course[hole].Hole;
        if (HasMood(g))
        {
            var before = Dist(from, pin);
            if (putt)
            {
                if (result.Holed && before >= Moods.HardPuttMeters)
                {
                    Raise(g, MoodEvent.HardPuttHoled, hole, from);
                }
                else if (!result.Holed && before <= Moods.EasyPuttMeters)
                {
                    Raise(g, MoodEvent.EasyPuttMissed, hole, from);
                }
            }
            else if (result.Outcome is ShotOutcome.Water or ShotOutcome.Bunker or ShotOutcome.Trees)
            {
                Raise(g, result.Outcome switch { ShotOutcome.Water => MoodEvent.Water, ShotOutcome.Bunker => MoodEvent.Bunker, _ => MoodEvent.TreeHit }, hole, result.Ball);
            }
            else if (!result.Holed && before >= Moods.PoorShotMinMeters && Dist(result.Ball, pin) > before * (1.0 - Moods.PoorShotProgress))
            {
                Raise(g, MoodEvent.PoorShot, hole, result.Ball);
            }
        }

        if (putt)
        {
            return;
        }

        var limit = Moods.NearMissMeters * Moods.NearMissMeters;
        foreach (var other in _groups)
        {
            if (other == group)
            {
                continue;
            }

            foreach (var o in other.Golfers)
            {
                if (HasMood(o) && !o.Quit && o.State is not (GolferState.Done or GolferState.Leaving or GolferState.Quitting) && Dist2(o.Position, result.Ball) <= limit)
                {
                    Raise(o, MoodEvent.NearMiss, other.Hole, o.Position);
                }
            }
        }
    }

    /// <summary>Le golfeur va jouer : le planificateur n'a trouvé que de l'eau, un bunker ou des arbres où viser.</summary>
    private void BeforeHit(Group group, Golfer g)
    {
        if (!HasMood(g))
        {
            return;
        }

        var terrain = Terrain!;
        if (g.Plan is { Result: { } plan } && terrain.SurfaceAt(plan.Landing.X, plan.Landing.Z) is Surface.Water or Surface.Trees or Surface.Bunker)
        {
            Raise(g, MoodEvent.NoSafeAim, group.Hole, g.Ball);
        }
    }

    /// <summary>Le groupe part de la file : si elle a duré trop longtemps, chacun s'en plaint.</summary>
    private void AfterRelease(Group group)
    {
        if (group.FromArrivals && Tick - group.ArrivedTick >= Moods.TeeWaitTicks)
        {
            foreach (var g in group.Golfers.Where(HasMood))
            {
                Raise(g, MoodEvent.TeeWait, 1, g.Position);
            }
        }

        OfferProShop(group);
    }

    /// <summary>Le golfeur attend le groupe devant : à <see cref="MoodParams.SlowPlayTicks"/> ticks sur un même trou, le jeu est lent.</summary>
    private void Waiting(Group group, Golfer g)
    {
        if (HasMood(g) && ++g.WaitTicks == Moods.SlowPlayTicks)
        {
            Raise(g, MoodEvent.SlowPlay, group.Hole, g.Position);
        }
    }

    /// <summary>Le trou est fini : sa beauté (vue du départ, trou, arbre remarquable) ou son gazon abîmé touche chacun, puis l'humeur baisse un peu tous les quelques trous.</summary>
    private void AfterHole(Group group)
    {
        var terrain = Terrain!;
        var hole = terrain.Course[group.Hole];
        var beauty = Beauty(terrain);
        var panorama = beauty.At(hole.Tee.X, hole.Tee.Z) >= Moods.PanoramaBeauty;
        var beautiful = HoleBeauty(beauty, hole) >= Moods.BeautifulHoleBeauty;
        var tree = RemarkableTreeNear(hole);
        var worn = HoleIsWorn(terrain, hole);
        foreach (var g in group.Active)
        {
            if (!HasMood(g))
            {
                continue;
            }

            if (panorama)
            {
                Raise(g, MoodEvent.Panorama, group.Hole, hole.Tee);
            }

            if (beautiful)
            {
                Raise(g, MoodEvent.BeautifulHole, group.Hole, hole.Green);
            }

            if (worn)
            {
                Raise(g, MoodEvent.WornTurf, group.Hole, hole.Hole);
            }

            if (tree is { } t)
            {
                Raise(g, MoodEvent.RemarkableTree, group.Hole, t);
            }

            if (group.Hole % Moods.DropEveryHoles == 0)
            {
                g.Mood = MoodParams.Clamp(g.Mood + Drained(-1));
            }

            if (g.LikedThisHole)
            {
                g.LikedHoles++;
                g.LikedThisHole = false;
            }
        }
    }

    private BeautyMap Beauty(TerrainGrid terrain)
    {
        if (_beautyMap is null || !_beautyMap.StillValid(terrain, Trees, Season, _beautySources, BenchAccent))
        {
            _beautyMap = BeautyMap.Build(terrain, Trees, Season, _beautySources, BenchAccent);
        }

        return _beautyMap;
    }

    private const int BeautySamples = 8;

    /// <summary>Le gazon de jeu du trou est abîmé : au moins la moitié des points de fairway ou de green de la ligne du départ au drapeau (deux au moins) sont usés.</summary>
    private bool HoleIsWorn(TerrainGrid terrain, CourseHole hole)
    {
        int turf = 0, worn = 0;
        for (var i = 0; i < BeautySamples; i++)
        {
            var t = (i + 0.5) / BeautySamples;
            var (x, z) = (hole.Tee.X + ((hole.Hole.X - hole.Tee.X) * t), hole.Tee.Z + ((hole.Hole.Z - hole.Tee.Z) * t));
            if (terrain.SurfaceAt(x, z) is Surface.Fairway or Surface.Green)
            {
                turf++;
                worn += terrain.Turf.Wear(terrain.CellXOf(x), terrain.CellZOf(z)) >= Moods.WornTurf ? 1 : 0;
            }
        }

        return turf >= 2 && worn * 2 >= turf;
    }

    /// <summary>Beauté moyenne le long de la ligne du départ au drapeau.</summary>
    private static double HoleBeauty(BeautyMap beauty, CourseHole hole)
    {
        var sum = 0.0;
        for (var i = 0; i < BeautySamples; i++)
        {
            var t = (i + 0.5) / BeautySamples;
            sum += beauty.At(hole.Tee.X + ((hole.Hole.X - hole.Tee.X) * t), hole.Tee.Z + ((hole.Hole.Z - hole.Tee.Z) * t));
        }

        return sum / BeautySamples;
    }

    /// <summary>Position du premier arbre remarquable debout à moins de <see cref="MoodParams.RemarkableTreeMeters"/> de la ligne du départ au drapeau, ou nul.</summary>
    private Point2? RemarkableTreeNear(CourseHole hole)
    {
        var reach = Moods.RemarkableTreeMeters;
        var (ax, az, bx, bz) = (hole.Tee.X, hole.Tee.Z, hole.Hole.X, hole.Hole.Z);
        var length = Math.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
        var found = new List<Tree>();
        Trees.Query((ax + bx) / 2.0, (az + bz) / 2.0, (length / 2.0) + reach, found);
        Point2? best = null;
        var bestId = int.MaxValue;
        foreach (var t in found)
        {
            if (!t.Remarkable || t.State != TreeState.Standing || t.Id >= bestId)
            {
                continue;
            }

            var u = length < 1e-9 ? 0.0 : Math.Clamp((((t.X - ax) * (bx - ax)) + ((t.Z - az) * (bz - az))) / (length * length), 0.0, 1.0);
            if (Dist(new Point2(t.X, t.Z), new Point2(ax + ((bx - ax) * u), az + ((bz - az) * u))) <= reach)
            {
                best = new Point2(t.X, t.Z);
                bestId = t.Id;
            }
        }

        return best;
    }

    /// <summary>Le groupe va au trou <paramref name="next"/> : un départ trop loin du green précédent fait râler (marche longue).</summary>
    private void BeforeMove(Group group, CourseHole next)
    {
        if (Dist(ExitPointOf(group.Hole), next.Tee) < Moods.LongWalkMeters)
        {
            return;
        }

        foreach (var g in group.Active.Where(HasMood))
        {
            Raise(g, MoodEvent.LongWalk, next.Number, g.Position);
        }
    }

    /// <summary>
    /// Fin de trou, trous restants à jouer : tout golfeur sous zéro abandonne et marche vers la porte du pavillon ; chaque partenaire perd 1,
    /// ce qui peut en faire abandonner un autre (on recommence jusqu'à ce que plus personne ne passe sous zéro).
    /// </summary>
    private void ResolveQuits(Group group)
    {
        while (group.Active.FirstOrDefault(g => HasMood(g) && g.Mood < 0) is { } quitter)
        {
            quitter.Quit = true;
            State.Walkouts++;
            quitter.State = GolferState.Quitting;
            ResignIfMember(quitter);
            foreach (var partner in group.Active)
            {
                partner.Mood = HasMood(partner) ? MoodParams.Clamp(partner.Mood - 1) : partner.Mood;
            }

            GolferQuit?.Invoke(new QuitInfo(quitter, QuitReason(quitter), group.Hole));
        }
    }

    /// <summary>L'événement qui a le plus pesé sur l'humeur du golfeur (occurrences × valeur, négatif), ou nul si elle n'a que baissé trou après trou.</summary>
    private MoodEvent? QuitReason(Golfer g)
    {
        MoodEvent? reason = null;
        var worst = 0;
        foreach (var def in Moods.Definitions)
        {
            var weight = g.Occurrences[(int)def.Event] * def.Value;
            if (weight < worst)
            {
                worst = weight;
                reason = def.Event;
            }
        }

        return reason;
    }

    /// <summary>Qualité de la sortie d'un groupe, de 0 à 1 : la moyenne de l'humeur de sortie de ses visiteurs, abandons compris (le pro n'a pas d'humeur).</summary>
    private double ExitScore(Group group) => group.Golfers.Where(HasMood).Average(g => Moods.ExitScore(g.Mood, Difficulty.GolferDemand));

    private void MixMood(ref ulong hash)
    {
        if (!Reactions.Untouched)
        {
            Mix(ref hash, 0x4D4F4F44UL);
            Reactions.Mix(ref hash);
        }
    }
}
