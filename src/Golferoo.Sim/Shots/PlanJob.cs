using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Shots;

/// <summary>Candidat évalué : coût espéré, part des balles arrêtées sous les arbres, bonus de beauté, et le coup.</summary>
internal readonly record struct PlanCandidate(double Cost, double Stop, double Bonus, ShotPlan Plan);

/// <summary>
/// Planification d'un coup en cours, étalée sur plusieurs ticks (<see cref="PhysicalShotResolver.Advance"/>, un budget d'évaluations par tick).
/// Tout ce qu'il faut pour reprendre est ici : étape, curseur dans l'étape, candidats déjà évalués, évaluations des finalistes. L'ordre des
/// évaluations ne dépend que de la demande, jamais du temps : le résultat est celui d'une planification d'un seul tenant.
/// Étapes : 0 coups pleins, 1 coups bas, 2 directions larges, 3 finalistes (candidats triés à l'entrée), 4 fini.
/// </summary>
internal sealed class PlanJob
{
    internal const int Done = 4;

    internal int Stage { get; set; }

    /// <summary>Évaluations déjà faites dans l'étape courante.</summary>
    internal int Cursor { get; set; }

    internal List<PlanCandidate> Candidates { get; private set; } = [];

    /// <summary>Évaluations des finalistes dans l'ordre : gauche, droite, puis (prudent) trop long, trop court, pour chaque finaliste.</summary>
    internal List<(double Cost, double Bonus)> Finals { get; private set; } = [];

    internal ShotPlan? Result { get; set; }

    internal bool IsDone => Result is not null;

    internal void Write(SaveWriter w)
    {
        w.Int(Stage);
        w.Int(Cursor);
        w.List(Candidates, c =>
        {
            w.Double(c.Cost);
            w.Double(c.Stop);
            w.Double(c.Bonus);
            WritePlan(w, c.Plan);
        });
        w.List(Finals, f =>
        {
            w.Double(f.Cost);
            w.Double(f.Bonus);
        });
        w.Bool(Result is not null);
        if (Result is { } plan)
        {
            WritePlan(w, plan);
        }
    }

    internal static PlanJob Read(SaveReader r)
    {
        var job = new PlanJob { Stage = r.Int(), Cursor = r.Int() };
        if (job.Stage is < 0 or > Done || job.Cursor < 0)
        {
            throw new InvalidDataException("Planification illisible.");
        }

        job.Candidates = r.List(() => new PlanCandidate(r.Double(), r.Double(), r.Double(), ReadPlan(r)));
        job.Finals = r.List(() => (r.Double(), r.Double()));
        job.Result = r.Bool() ? ReadPlan(r) : null;
        return job;
    }

    internal void MixInto(ref ulong hash)
    {
        TerrainGrid.Mix(ref hash, ((ulong)Stage << 32) | (uint)Cursor);
        TerrainGrid.Mix(ref hash, (ulong)Candidates.Count);
        foreach (var c in Candidates)
        {
            TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(c.Cost));
            TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(c.Stop));
            TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(c.Bonus));
            MixPlan(ref hash, c.Plan);
        }

        TerrainGrid.Mix(ref hash, (ulong)Finals.Count);
        foreach (var f in Finals)
        {
            TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(f.Cost));
            TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(f.Bonus));
        }

        if (Result is { } plan)
        {
            MixPlan(ref hash, plan);
        }
    }

    private static void MixPlan(ref ulong hash, in ShotPlan p)
    {
        TerrainGrid.Mix(ref hash, ((ulong)ClubIndex(p.Club) << 16) | ((ulong)p.Shape << 8) | (p.Low ? 1UL : 0UL));
        TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.DirX));
        TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.DirZ));
        TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Force));
        TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Landing.X));
        TerrainGrid.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Landing.Z));
    }

    private static int ClubIndex(Club club)
    {
        for (var i = 0; i < Clubs.Full.Count; i++)
        {
            if (ReferenceEquals(Clubs.Full[i], club))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Un coup planifié se joue avec un club du sac.");
    }

    private static void WritePlan(SaveWriter w, in ShotPlan p)
    {
        w.Byte((byte)ClubIndex(p.Club));
        w.Double(p.DirX);
        w.Double(p.DirZ);
        w.Double(p.Force);
        w.Bool(p.Low);
        w.Byte((byte)p.Shape);
        w.Point(p.Landing);
    }

    private static ShotPlan ReadPlan(SaveReader r)
    {
        var club = r.Byte();
        if (club >= Clubs.Full.Count)
        {
            throw new InvalidDataException("Planification illisible : club inconnu.");
        }

        return new ShotPlan(Clubs.Full[club], r.Double(), r.Double(), r.Double(), r.Bool(), (ShotShape)r.Byte(), r.Point());
    }
}
