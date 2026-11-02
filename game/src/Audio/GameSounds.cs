using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;
using Sky = Golferoo.Sim.Weather.Sky;

namespace Golferoo.Game.Sound;

/// <summary>
/// Sons de la partie (C20) : lit les évènements existants de la sim et joue le son qui leur répond ; N'ajoute aucun état à la sim.
/// Gestion : caisse, objectif, conseil, abandon. Golf (golfeurs en vue seulement, muet en pause et à ×16) : frappe selon le club, balle dans le trou,
/// et eau, arbre ou bunker (évènements d'humeur) au moment où la balle retombe. Ambiance : une boucle par ciel et saison.
/// Les gestionnaires sont publics pour que la fumée les déclenche avec un lecteur factice.
/// </summary>
public partial class GameSounds : Node
{
    private Main _main = null!;
    private long _lastFees;
    private bool _quitHeard;
    private readonly Dictionary<int, (GolferState State, bool Holed)> _seen = [];
    private readonly Dictionary<int, string> _landing = [];

    /// <summary>Vrai si ce point du terrain est à l'écran ; la fumée le remplace.</summary>
    public Func<Point2, bool> InView { get; set; } = _ => false;

    /// <summary>Distance au drapeau (m) au-delà de laquelle la frappe est un drive, et en deçà de laquelle c'est un chip.</summary>
    public const double DriveRange = 180.0;
    public const double ChipRange = 40.0;

    /// <summary>Ciel et saison choisissent la boucle d'ambiance : les oiseaux au sec hors hiver ; la pluie n'a pas de fichier (null : silence).</summary>
    public static string? AmbienceFor(Sky sky, Season season) => sky is Sky.Sun or Sky.Clouds && season != Season.Winter ? "amb_birds" : null;

    public void Init(Main main)
    {
        _main = main;
        var world = main.World;
        _lastFees = world.Ledger.Current(LedgerCategory.GreenFees);
        world.Ledger.MoneyChanged += OnMoney;
        world.Ledger.PurchaseRefused += OnRefused;
        world.ObjectiveCompleted += OnObjective;
        world.AdviceGiven += OnAdvice;
        world.GolferQuit += OnQuit;
        world.MoodChanged += OnMood;
        InView = p => main.Camera.View is { } view && view.IsInsideTree() && view.IsPositionInFrustum(new Vector3((float)p.X, (float)(world.Terrain?.Sample(p.X, p.Z) ?? 0.0), (float)p.Z));
        UpdateAmbience();
        Audio.Sfx.Music("music_game");
    }

    /// <summary>Au changement de jour : la boucle d'ambiance du ciel du jour, en fondu enchaîné.</summary>
    public void UpdateAmbience() => Audio.Sfx.Ambience(AmbienceFor(_main.World.Weather.Sky, _main.World.Season));

    private bool Heard(Point2 at) => !_main.Paused && _main.Speed < 16 && InView(at);

    /// <summary>Après chaque tick : frappe au départ de la balle, balle tombée (eau, arbre, bunker) et balle dans le trou, pour les golfeurs en vue.</summary>
    public void OnStep(WorldSnapshot snapshot)
    {
        if (_seen.Count > (snapshot.Golfers.Count * 2) + 32)
        {
            _seen.Clear();
            _landing.Clear();
        }

        foreach (var g in snapshot.Golfers)
        {
            if (_seen.TryGetValue(g.Id, out var old))
            {
                if (g.State == GolferState.BallInFlight && old.State is GolferState.Swinging or GolferState.Putting && Heard(g.Position))
                {
                    Audio.Play(old.State == GolferState.Putting ? "strike_putt" : StrikeFor(Math.Sqrt(Dist2(g.Position, g.Pin))));
                }

                if (old.State == GolferState.BallInFlight && g.State != GolferState.BallInFlight && _landing.Remove(g.Id, out var sound) && Heard(g.Position))
                {
                    Audio.Play(sound);
                }

                if (g.Holed && !old.Holed && Heard(g.Position))
                {
                    Audio.Play("hole");
                }
            }

            _seen[g.Id] = (g.State, g.Holed);
        }
    }

    public static string StrikeFor(double toPin) => toPin >= DriveRange ? "strike_drive" : toPin < ChipRange ? "strike_chip" : "strike_iron";

    private static double Dist2(Point2 a, Point2 b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>L'eau, l'arbre ou le bunker sont connus au départ de la balle ; le bruit attend qu'elle retombe.</summary>
    public void OnMood(MoodChange change)
    {
        if (change.Event switch { MoodEvent.Water => "water", MoodEvent.TreeHit => "tree", MoodEvent.Bunker => "bunker", _ => null } is { } sound)
        {
            _landing[change.Golfer.Id] = sound;
        }
    }

    public override void _ExitTree()
    {
        if (_main?.World is { } world)
        {
            world.Ledger.MoneyChanged -= OnMoney;
            world.Ledger.PurchaseRefused -= OnRefused;
            world.ObjectiveCompleted -= OnObjective;
            world.AdviceGiven -= OnAdvice;
            world.GolferQuit -= OnQuit;
            world.MoodChanged -= OnMood;
            Audio.Sfx.Ambience(null);
        }
    }

    /// <summary>Un green fee de plus ce mois-ci : la caisse (une clôture de mois fait baisser le total, sans son).</summary>
    public void OnMoney(long balance) => OnFees(_main.World.Ledger.Current(LedgerCategory.GreenFees));

    public void OnFees(long fees)
    {
        if (fees > _lastFees)
        {
            Audio.Play("cash");
        }

        _lastFees = fees;
    }

    public void OnRefused(LedgerCategory category, long cost) => Audio.Play("ui_refuse");

    public void OnObjective(Sim.Objectives.ObjectiveDef def) => Audio.Play("objective");

    /// <summary>Le ding accompagne la bulle du pro : muet si le joueur a coupé les conseils.</summary>
    public void OnAdvice(Sim.Pro.AdviceDef def, int hole)
    {
        if (GameOptions.ProAdvice)
        {
            Audio.Play("advice");
        }
    }

    /// <summary>Seul le premier abandon de la session sonne : les suivants sont dans le journal des pensées.</summary>
    public void OnQuit(QuitInfo info)
    {
        if (!_quitHeard)
        {
            _quitHeard = true;
            Audio.Play("quit");
        }
    }
}
