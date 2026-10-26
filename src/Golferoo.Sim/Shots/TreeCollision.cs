using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Shots;

/// <summary>
/// Rencontre de la balle en vol avec les arbres de la sim : le tronc (cylindre) renvoie la balle, la couronne (ellipsoïde)
/// l'arrête avec la probabilité 1 − exp(−k · densité · longueur traversée) sur le pas. Seuls + − × ÷ et √ interviennent
/// (l'exponentielle est un développement limité), et le RNG n'est tiré que si le pas traverse une couronne.
/// </summary>
public static class TreeCollision
{
    /// <summary>Coefficient d'extinction de la couronne (1/m) : une couronne pleine de 8 m de corde arrête environ neuf balles sur dix.</summary>
    public const double CrownK = 0.3;

    /// <summary>Restitution de l'écorce : part de la vitesse normale qui revient après le choc avec un tronc.</summary>
    public const double BarkRestitution = 0.3;

    /// <summary>Part de la vitesse horizontale qui survit à l'arrêt dans une couronne.</summary>
    public const double CrownDamping = 0.05;

    private const double BallRadius = 0.0214;
    private const double TrunkTopShare = 0.3;
    private const double Reach = 0.5;

    /// <summary>Marge horizontale autour de la couronne, en mètres : un pas de vol (0,4 m à 80 m/s) et le rayon de la balle.</summary>
    private const double StepReach = 0.5;

    /// <summary>Rayon dans lequel la sonde garde ses candidats autour de son centre, en mètres.</summary>
    private const double ProbeRadius = 6.0;

    /// <summary>
    /// Arbres proches de la balle, rassemblés une fois pour plusieurs pas : la sonde ne refait la recherche dans l'index que
    /// lorsque la balle s'écarte de <see cref="ProbeRadius"/> du centre de la dernière.
    /// </summary>
    public sealed class Probe
    {
        internal List<Tree> Candidates { get; } = [];

        internal double CenterX { get; set; }

        internal double CenterZ { get; set; }

        internal bool Valid { get; set; }
    }

    /// <summary>
    /// Mode espérance de la planification : une couronne n'arrête pas la balle, elle retire de <see cref="Pass"/> la probabilité d'arrêt
    /// du pas et verse cette probabilité (au point du pas) dans la position moyenne d'arrêt. Aucun tirage.
    /// </summary>
    public sealed class Expectation
    {
        /// <summary>En dessous de cette probabilité de passer, la branche « passée » est arrêtée net (poids négligeable).</summary>
        internal const double NegligiblePass = 0.01;

        /// <summary>Probabilité que la balle ait franchi toutes les couronnes rencontrées jusqu'ici.</summary>
        public double Pass { get; private set; } = 1.0;

        /// <summary>Probabilité cumulée d'être arrêtée par une couronne.</summary>
        public double Stopped => 1.0 - Pass;

        /// <summary>Position moyenne d'arrêt, pondérée par la probabilité (point de départ si jamais arrêtée).</summary>
        public Point2 StopPoint => Stopped > 1e-12 ? new Point2(_sumX / Stopped, _sumZ / Stopped) : default;

        private double _sumX;
        private double _sumZ;

        internal void Add(double p, double x, double z)
        {
            var w = Pass * p;
            _sumX += w * x;
            _sumZ += w * z;
            Pass -= w;
        }
    }

    /// <summary>Marge horizontale de la recherche : plus grand rayon de couronne, plus un pas de vol.</summary>
    public static readonly double Margin = TreeSpeciesTable.MaxCrownRadius + Reach;

    /// <summary>
    /// Traite le pas (px, py, pz) → (x, y, z) : un tronc touché modifie la position et la vitesse, une couronne qui arrête
    /// la balle ne modifie que la vitesse et lève <paramref name="crownDone"/> (plus de couronne jusqu'au prochain contact avec le sol).
    /// </summary>
    public static void Step(
        TerrainGrid terrain,
        TreeStand trees,
        Season season,
        DeterministicRng? rng,
        Probe probe,
        double px,
        double py,
        double pz,
        ref double x,
        ref double y,
        ref double z,
        ref double vx,
        ref double vy,
        ref double vz,
        ref bool crownDone,
        Expectation? expectation = null)
    {
        var mx = (x + px) * 0.5;
        var mz = (z + pz) * 0.5;
        var sx = mx - probe.CenterX;
        var sz = mz - probe.CenterZ;
        if (!probe.Valid || (sx * sx) + (sz * sz) > ProbeRadius * ProbeRadius)
        {
            probe.Candidates.Clear();
            trees.Query(mx, mz, Margin + ProbeRadius, probe.Candidates);
            probe.CenterX = mx;
            probe.CenterZ = mz;
            probe.Valid = true;
        }

        var near = Margin * Margin;
        foreach (var tree in probe.Candidates)
        {
            // Un tronc tombé ou une souche n'arrête pas la balle (lot 4 de C02) : seuls les arbres debout comptent.
            if (tree.State != TreeState.Standing)
            {
                continue;
            }

            var tx = tree.X - mx;
            var tz = tree.Z - mz;
            if ((tx * tx) + (tz * tz) > near)
            {
                continue;
            }

            var species = TreeSpeciesTable.Get(tree.Species);
            var g = Tree.Growth(species, tree.AgeMonths);
            var a = 0.5 * (species.SaplingCrownDiameter + ((species.AdultCrownDiameter - species.SaplingCrownDiameter) * g));
            var reach = a + StepReach;
            if ((tx * tx) + (tz * tz) > reach * reach)
            {
                continue;
            }

            var ground = terrain.Sample(tree.X, tree.Z);
            var height = species.SaplingHeight + ((species.AdultHeight - species.SaplingHeight) * g);
            if (Math.Min(py, y) > ground + height)
            {
                continue;
            }

            var crownBase = species.FirstBranchHeight * height / species.AdultHeight;
            var trunkRadius = 0.5 * (species.SaplingTrunkDiameter + ((species.TrunkDiameter - species.SaplingTrunkDiameter) * g));

            // Tronc : du sol à un peu au-dessus de la base de la couronne.
            var trunkTop = ground + crownBase + (TrunkTopShare * (height - crownBase));
            if (HitTrunk(tree, trunkRadius + BallRadius, ground, trunkTop, px, py, pz, ref x, ref y, ref z, ref vx, ref vz))
            {
                continue;
            }

            if (crownDone)
            {
                continue;
            }

            var b = (height - crownBase) * 0.5;
            var length = ChordLength(tree.X, ground + crownBase + b, tree.Z, a, b, px, py, pz, x, y, z);
            if (length <= 0)
            {
                continue;
            }

            var p = StopProbability(CrownK * species.CrownDensity(season) * length);
            if (expectation is not null)
            {
                expectation.Add(p, x, z);
            }

            // Mode espérance : une fois la balle presque sûrement arrêtée, la branche « passée » n'a plus de poids ; on l'arrête aussi.
            if (expectation is null ? rng!.NextDouble() < p : expectation.Pass < Expectation.NegligiblePass)
            {
                crownDone = true;
                vx *= CrownDamping;
                vz *= CrownDamping;
                if (vy > 0)
                {
                    vy = 0;
                }
            }
        }
    }

    /// <summary>
    /// 1 − exp(−a), sans Math.Exp : exp(a) = exp(a/16)^16, avec exp(a/16) en développement limité à l'ordre 8 (a/16 ≤ 0,5 pour
    /// a ≤ 8, erreur relative sous 1e-7). Au-delà de 8, la probabilité est celle de 8 (0,99966) : la balle est arrêtée à coup sûr
    /// pour tout usage.
    /// </summary>
    public static double StopProbability(double a)
    {
        a = Math.Clamp(a, 0.0, 8.0);
        var b = a / 16.0;
        var e = 1.0 + (b * (1.0 + (b / 2.0 * (1.0 + (b / 3.0 * (1.0 + (b / 4.0 * (1.0 + (b / 5.0 * (1.0 + (b / 6.0 * (1.0 + (b / 7.0 * (1.0 + (b / 8.0)))))))))))))));
        for (var i = 0; i < 4; i++)
        {
            e *= e;
        }

        return 1.0 - (1.0 / e);
    }

    private static bool HitTrunk(Tree tree, double radius, double ground, double top, double px, double py, double pz, ref double x, ref double y, ref double z, ref double vx, ref double vz)
    {
        var dx = x - px;
        var dz = z - pz;
        var ox = px - tree.X;
        var oz = pz - tree.Z;
        var r2 = radius * radius;
        var a = (dx * dx) + (dz * dz);
        var c = (ox * ox) + (oz * oz) - r2;
        double s;
        if (c <= 0)
        {
            s = 0;
        }
        else
        {
            var bh = (ox * dx) + (oz * dz);
            var disc = (bh * bh) - (a * c);
            if (a < 1e-18 || disc < 0)
            {
                return false;
            }

            s = (-bh - Math.Sqrt(disc)) / a;
            if (s < 0 || s > 1)
            {
                return false;
            }
        }

        var hy = py + ((y - py) * s);
        if (hy < ground || hy > top)
        {
            return false;
        }

        var hx = px + (dx * s);
        var hz = pz + (dz * s);
        var nx = hx - tree.X;
        var nz = hz - tree.Z;
        var len = Math.Sqrt((nx * nx) + (nz * nz));
        if (len < 1e-9)
        {
            var sp = Math.Sqrt((vx * vx) + (vz * vz));
            if (sp < 1e-9)
            {
                return false;
            }

            nx = -vx / sp;
            nz = -vz / sp;
        }
        else
        {
            nx /= len;
            nz /= len;
        }

        var vn = (vx * nx) + (vz * nz);
        if (vn >= 0)
        {
            return false;
        }

        // Pas de frottement tangentiel sur l'écorce (choix du lot 2) : seule la composante normale est modifiée.
        // Réflexion sur la normale horizontale ; la composante normale revient à 30 %, le reste du vecteur est inchangé.
        vx -= (1.0 + BarkRestitution) * vn * nx;
        vz -= (1.0 + BarkRestitution) * vn * nz;
        x = tree.X + (nx * (radius + 1e-3));
        z = tree.Z + (nz * (radius + 1e-3));
        y = hy;
        return true;
    }

    /// <summary>Longueur du segment (p → q) à l'intérieur de l'ellipsoïde de centre (cx, cy, cz), de demi-axes a, b, a.</summary>
    private static double ChordLength(double cx, double cy, double cz, double a, double b, double px, double py, double pz, double qx, double qy, double qz)
    {
        var ia = 1.0 / a;
        var ib = 1.0 / b;
        var ox = (px - cx) * ia;
        var oy = (py - cy) * ib;
        var oz = (pz - cz) * ia;
        var dx = (qx - px) * ia;
        var dy = (qy - py) * ib;
        var dz = (qz - pz) * ia;
        var qa = (dx * dx) + (dy * dy) + (dz * dz);
        if (qa < 1e-18)
        {
            return 0;
        }

        var qb = (ox * dx) + (oy * dy) + (oz * dz);
        var qc = (ox * ox) + (oy * oy) + (oz * oz) - 1.0;
        var disc = (qb * qb) - (qa * qc);
        if (disc <= 0)
        {
            return 0;
        }

        var root = Math.Sqrt(disc);
        var s0 = Math.Max(0.0, (-qb - root) / qa);
        var s1 = Math.Min(1.0, (-qb + root) / qa);
        if (s1 <= s0)
        {
            return 0;
        }

        var ex = qx - px;
        var ey = qy - py;
        var ez = qz - pz;
        return (s1 - s0) * Math.Sqrt((ex * ex) + (ey * ey) + (ez * ez));
    }
}
