using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pro;

/// <summary>
/// Qualité d'un coup du pro, habileté par habileté (C25) : qualité = (1 − écart / seuil)², de 0 à 1. XP = 10 × qualité, en centièmes
/// (1 000 pour une qualité de 1). Seule l'habileté du club a un plancher. Le point visé est celui que le joueur a vu : l'arrêt du coup
/// prévu sans hasard (<c>aimed</c>), pour un putt la cible ; si le coup prévu subit une pénalité (eau, hors limites), son point de drop ne dit rien
/// de la visée : on prend la cible. Avec une pénalité sur le coup joué, toutes les habiletés autres que le club reçoivent 0 (le club garde son
/// plancher). Sous 1 m de distance visée, la direction n'a pas de sens : ni Précision ni Dosage ne reçoivent d'XP.
/// </summary>
internal static class ProQuality
{
    /// <summary>XP, en centièmes, d'un coup de qualité 1.</summary>
    internal const int FullHundredths = 1000;

    /// <summary>Distance visée, en mètres, sous laquelle Précision et Dosage ne gagnent rien.</summary>
    internal const double MinAimedDistance = 1.0;

    /// <summary>(1 − écart / seuil)² ; 0 au seuil et au-delà.</summary>
    internal static double Quality(double deviation, double threshold) =>
        threshold <= 0.0 || deviation >= threshold ? 0.0 : Math.Pow(1.0 - (deviation / threshold), 2.0);

    internal static int Hundredths(double quality) => (int)Math.Round(FullHundredths * quality, MidpointRounding.AwayFromZero);

    /// <summary>Les habiletés nourries par ce coup, avec leur XP en centièmes (jamais négative).</summary>
    internal static List<(ProSkill Skill, int Hundredths)> Awards(ProgressionParams p, TerrainGrid terrain, Point2 start, in ProShot shot, in ShotResult aimed, in ShotResult result)
    {
        var awards = new List<(ProSkill, int)>();
        var putt = shot.Club.IsPutter;
        var penalty = result.PenaltyStrokes > 0;
        var reference = putt || aimed.PenaltyStrokes > 0 ? shot.Target : aimed.Ball;
        var d = Math.Max(Dist(start, reference), 1e-6);
        var tiny = d < MinAimedDistance;
        var (ux, uz) = ((reference.X - start.X) / d, (reference.Z - start.Z) / d);
        var (bx, bz) = (result.Ball.X - start.X, result.Ball.Z - start.Z);
        var holed = result.Holed;
        var lateral = holed ? 0.0 : Math.Abs((ux * bz) - (uz * bx));
        var depth = holed ? 0.0 : Math.Abs(((ux * bx) + (uz * bz)) - d);
        var total = holed ? 0.0 : Math.Sqrt((lateral * lateral) + (depth * depth));

        // Club : plancher, et le lie compte (celui visé 1, rough 0,5, le reste ou une pénalité 0).
        double clubQuality;
        if (putt)
        {
            clubQuality = holed ? 1.0 : Quality(total, Math.Max(p.PutterMeters, p.PutterFraction * d));
        }
        else
        {
            var aimedLie = terrain.SurfaceAt(reference.X, reference.Z);
            var landed = terrain.SurfaceAt(result.Ball.X, result.Ball.Z);
            var lie = holed || (!penalty && landed == aimedLie) ? 1.0 : !penalty && landed == Surface.Rough ? 0.5 : 0.0;
            clubQuality = lie * Quality(total, p.ClubFraction * d);
        }

        awards.Add((ProSkills.OfClub(shot.Club), Hundredths(Math.Max(clubQuality, p.ClubFloor))));
        if (penalty)
        {
            return awards;
        }

        var partial = shot.Force < p.FullForce;
        if (!putt && !tiny)
        {
            awards.Add((ProSkill.Precision, Hundredths(holed ? 1.0 : Quality(lateral, p.PrecisionFraction * d))));
        }

        if ((putt || partial) && !tiny)
        {
            awards.Add((ProSkill.Dosage, Hundredths(holed ? 1.0 : Quality(depth, p.DosageFraction * d))));
        }

        if (!putt && !partial && (shot.Club == Clubs.Driver || shot.Club == Clubs.Wood3 || shot.Club == Clubs.Iron5))
        {
            var ratio = Dist(start, result.Ball) / shot.Club.TargetRange;
            var way = Math.Clamp((ratio - p.DistanceFloor) / (1.0 - p.DistanceFloor), 0.0, 1.0);
            awards.Add((ProSkill.Distance, Hundredths(way * way)));
        }

        if (shot.Shape is ShotShape.Draw or ShotShape.Fade)
        {
            // Courbure : écart latéral de la balle à la ligne de départ (départ vers la cible), obtenu contre voulu (coup prévu).
            var (tx, tz) = (shot.Target.X - start.X, shot.Target.Z - start.Z);
            var tl = Math.Max(Math.Sqrt((tx * tx) + (tz * tz)), 1e-6);
            double Side(Point2 q) => (((tx * (q.Z - start.Z)) - (tz * (q.X - start.X))) / tl);
            var wanted = Side(aimed.Ball);
            var error = Math.Abs(Side(result.Ball) - wanted);
            var skill = shot.Shape == ShotShape.Draw ? ProSkill.Draw : ProSkill.Fade;
            awards.Add((skill, Hundredths(Quality(error, p.CurveFraction * Math.Max(Math.Abs(wanted), 0.5)))));
        }
        else if (shot.Shape == ShotShape.Chip)
        {
            awards.Add((ProSkill.Chip, Hundredths(Quality(total, p.ClubFraction * d))));
        }

        var from = terrain.SurfaceAt(start.X, start.Z);
        if (from is Surface.Rough or Surface.Bunker)
        {
            awards.Add((from == Surface.Rough ? ProSkill.RoughExit : ProSkill.BunkerExit, Hundredths(Quality(total, p.ClubFraction * d))));
        }

        return awards;
    }

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
