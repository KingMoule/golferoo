using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>
/// Saisons types du pro (C25 lot 4) : il joue « Le neuf » avec le choix de coup des golfeurs (C09) mais les effets de ses habiletés, et chaque coup
/// nourrit ses habiletés par <see cref="ProSkills.Record"/>, comme dans le jeu. Une saison = cinq journées de ligue (xp doublée, sans plafond) et assez
/// de parties d'entraînement pour atteindre le plafond de l'année sur Précision et Putter. Un putt à moins de 2 m est donné, sans expérience.
/// </summary>
internal static class ProSeasonSim
{
    /// <summary>Journées de ligue d'une saison (<c>LeagueSeason.RoundCount</c>).</summary>
    internal const int LeagueRounds = 5;

    /// <summary>Parties d'entraînement au plus par saison.</summary>
    internal const int MaxTrainingRounds = 60;


    internal static TerrainGrid LeNeuf() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    internal static PhysicalShotResolver ResolverFor(TerrainGrid terrain, World world) =>
        new() { Trees = world.Trees, Season = world.Season, MapCapacity = World.MapCapacityFor(terrain) };

    /// <summary>Joue neuf trous ; renvoie le nombre de coups. L'expérience va dans <paramref name="pro"/>.</summary>
    internal static int PlayRound(ProSkills pro, PhysicalShotResolver resolver, TerrainGrid terrain, ulong seed, bool league, int year, Action<IReadOnlyList<(ProSkill Skill, int Hundredths)>>? onAwards = null, int shapeMode = 0, double wind = 0.0)
    {
        var plan = new DeterministicRng(0xA11CE + seed);
        var exec = new DeterministicRng(0xB0B + seed);
        var skill = SkillLevels.FromHandicap(pro.OverallHandicap);
        var total = 0;
        foreach (var hole in terrain.Course.Holes)
        {
            var ball = hole.Tee;
            var strokes = 0;
            while (strokes < World.MaxStrokes)
            {
                ProShot shot;
                var onGreen = terrain.SurfaceAt(ball.X, ball.Z) == Surface.Green;
                if (onGreen)
                {
                    var dx = hole.Hole.X - ball.X;
                    var dz = hole.Hole.Z - ball.Z;
                    var d = Math.Sqrt((dx * dx) + (dz * dz));
                    if (d < World.GimmeDistance)
                    {
                        strokes++;
                        break;
                    }

                    // Un putt qui passe un peu le trou (50 cm), comme le joue un joueur.
                    var aim = new Point2(hole.Hole.X + (0.5 * dx / d), hole.Hole.Z + (0.5 * dz / d));
                    shot = new ProShot(Clubs.Putter, aim, ShotShape.Straight, 1.0);
                    for (var k = 0; k < 4; k++)
                    {
                        // Le joueur lit la pente avec l'aperçu : il corrige la visée jusqu'à ce que le coup prévu s'arrête au trou (plus 50 cm).
                        var preview = pro.Execute(resolver, terrain, ball, shot, null, hole.Hole).Ball;
                        aim = new Point2(aim.X + (hole.Hole.X + (0.5 * dx / d) - preview.X), aim.Z + (hole.Hole.Z + (0.5 * dz / d) - preview.Z));
                        shot = shot with { Target = aim };
                    }
                }
                else
                {
                    resolver.Resolve(new ShotRequest(ball, hole.Hole, strokes, skill), terrain, plan, out var chosen);
                    var c = chosen!.Value;
                    var shape = c.Low && c.Shape == ShotShape.Straight ? ShotShape.Chip : c.Shape;
                    shot = new ProShot(c.Club, new Point2(ball.X + (c.DirX * 100.0), ball.Z + (c.DirZ * 100.0)), shape, c.Force);
                    var reach = c.Club.TargetRange * c.Force;
                    shot = shot with { Target = new Point2(ball.X + (c.DirX * reach), ball.Z + (c.DirZ * reach)) };
                }

                if (shapeMode == 1 && !onGreen)
                {
                    var near = Math.Sqrt(((hole.Hole.X - ball.X) * (hole.Hole.X - ball.X)) + ((hole.Hole.Z - ball.Z) * (hole.Hole.Z - ball.Z))) < 40.0;
                    shot = shot with { Shape = near ? ShotShape.Chip : (strokes % 3) switch { 0 => ShotShape.Draw, 1 => ShotShape.Fade, _ => shot.Shape } };
                }

                resolver.Wind = new Vec3(wind, 0.0, 0.0);
                var aimed = pro.Execute(resolver, terrain, ball, shot, null, hole.Hole);
                var r = pro.Execute(resolver, terrain, ball, shot, exec, hole.Hole);
                pro.Record(terrain, ball, shot, aimed, r, league, year, wind, hole.Hole);
                if (onAwards is not null)
                {
                    onAwards(ProQuality.Awards(ProgressionParams.Default, terrain, ball, shot, aimed, r, wind));
                }

                strokes += 1 + r.PenaltyStrokes;
                ball = r.Ball;
                if (r.Holed)
                {
                    break;
                }
            }

            total += Math.Min(strokes, World.MaxStrokes);
        }

        return total;
    }

    /// <summary>
    /// Joue <paramref name="seasons"/> saisons de suite ; renvoie les niveaux des 17 habiletés à la fin de chaque saison. L'année de jeu de la
    /// saison <c>i</c> est <c>i + 1</c> : le plafond d'entraînement repart à zéro à chaque saison (1er avril).
    /// </summary>
    internal static int[][] Run(ProgressionParams progression, int seasons, ulong seed, Action<int, ProSkills>? afterSeason = null)
    {
        var terrain = LeNeuf();
        var world = new World(1, terrain);
        var resolver = ResolverFor(terrain, world);
        var pro = new ProSkills(progression);
        var levels = new int[seasons][];
        for (var s = 0; s < seasons; s++)
        {
            var year = s + 1;
            var salt = (seed * 1_000_003UL) + ((ulong)s * 1009UL);
            for (var r = 0; r < LeagueRounds; r++)
            {
                PlayRound(pro, resolver, terrain, salt + (ulong)r, league: true, year);
            }

            for (var t = 0; t < MaxTrainingRounds; t++)
            {
                if (pro.TrainingThisYear(ProSkill.Precision, year) >= pro.TrainingCap && pro.TrainingThisYear(ProSkill.Putter, year) >= pro.TrainingCap)
                {
                    break;
                }

                PlayRound(pro, resolver, terrain, salt + 100UL + (ulong)t, league: false, year);
            }

            levels[s] = [.. Enumerable.Range(0, ProSkills.Count).Select(i => pro.Level((ProSkill)i))];
            afterSeason?.Invoke(s, pro);
        }

        return levels;
    }
}
