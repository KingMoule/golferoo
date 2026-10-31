using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>Mesures d'une série de jours sur « Le neuf » (C16 lot 1) : abandons, humeur de sortie, événements par trou. Sert aux tests et au calage.</summary>
internal sealed class MoodRun
{
    public int Golfers;
    public int Quits;
    public readonly List<int> ExitMoods = [];

    /// <summary>Événements positifs levés, et parmi eux ceux que le plafond a écrêtés (changement réel inférieur à la valeur).</summary>
    public int Positives;
    public int Clipped;

    public double ClippedShare => Positives == 0 ? 0.0 : Clipped / (double)Positives;
    public readonly int[] Events = new int[MoodParams.EventCount];

    /// <summary>Somme des variations d'humeur par trou (indice = numéro du trou).</summary>
    public readonly double[] DeltaByHole = new double[19];

    /// <summary>Nombre de golfeurs ayant fini chaque trou (indice = numéro du trou).</summary>
    public readonly int[] PlaysByHole = new int[19];

    public readonly List<(int Group, int Golfer, int Hole, int Delta, MoodEvent Event)> Trace = [];
    public readonly Dictionary<int, int> GroupOf = [];
    public readonly List<string> Groups = [];
    public World World = null!;

    public double QuitShare => Golfers == 0 ? 0.0 : Quits / (double)Golfers;

    public double MeanExit => ExitMoods.Count == 0 ? 0.0 : ExitMoods.Average();

    /// <summary>« Le neuf » avec son pavillon, sans ligue ; la graine de référence est 1.</summary>
    public static MoodRun Go(int days, bool physical, ulong seed = 1, TerrainGrid? terrain = null, MoodParams? mood = null, ArrivalParams? arrivals = null, Action<World>? setup = null, bool noQuit = false, Buildings.BuildingParams? buildings = null)
    {
        var world = new World(seed, terrain ?? ClubhouseTests.LeNeuf(), physical ? null : new StubShotResolver(), league: false, placeClubhouse: true, arrivals: arrivals, mood: mood, buildings: buildings);
        setup?.Invoke(world);
        var run = new MoodRun { World = world };
        var byVisitor = new Dictionary<int, Golfer>();
        world.GroupArrived += g =>
        {
            foreach (var o in g.Golfers)
            {
                byVisitor[o.VisitorId] = o;
                run.GroupOf[o.Id] = g.Id;
            }

            run.Groups.Add($"G {g.Id} {string.Join(" ", g.Golfers.Select(o => o.Id))}");
        };
        world.MoodChanged += c =>
        {
            var nominal = world.Moods.Definitions[(int)c.Event].Value;
            if (nominal > 0)
            {
                run.Positives++;
                run.Clipped += c.Delta < nominal ? 1 : 0;
            }

            if (noQuit)
            {
                c.Golfer.Mood = MoodParams.Max; // mesure des événements seuls : personne ne passe sous zéro
            }

            run.Events[(int)c.Event]++;
            run.DeltaByHole[c.Hole] += c.Delta;
            run.Trace.Add((run.GroupOf[c.Golfer.Id], c.Golfer.Id, c.Hole, nominal, c.Event));
        };
        world.GolferQuit += _ => run.Quits++;
        world.GroupLeft += visitors =>
        {
            foreach (var v in visitors)
            {
                var g = byVisitor[v.Id];
                run.ExitMoods.Add(g.Mood);
                for (var h = 1; h <= g.Card!.HoleCount; h++)
                {
                    run.PlaysByHole[h] += g.Card.Strokes(h - 1) > 0 ? 1 : 0;
                }
            }
        };
        var released = new HashSet<int>();
        for (var i = 0; i < days * Time.Calendar.TicksPerDay; i++)
        {
            world.Step();
            foreach (var g in world.Groups.Where(g => g.FromArrivals && g.Released && !g.Leaving))
            {
                if (released.Add(g.Id))
                {
                    run.Golfers += g.Golfers.Count;
                }
            }
        }

        return run;
    }
}
