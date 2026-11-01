using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Save;

namespace Golferoo.Sim.Objectives;

/// <summary>
/// Bilan d'une année civile close (décembre) : la somme des mois clos de l'année au grand livre, plus ce que le club a vécu pendant l'année.
/// Le premier bilan ne couvre que les mois depuis le départ (avril).
/// </summary>
/// <param name="Year">Année de jeu (1 pour la première).</param>
/// <param name="Totals">Total signé de chaque <see cref="LedgerCategory"/> sur l'année.</param>
/// <param name="OpeningBalance">Solde au début de l'année (1er janvier, ou le départ).</param>
/// <param name="ClosingBalance">Solde à la clôture de décembre.</param>
/// <param name="Golfers">Golfeurs qui ont fini leur partie (abandons compris) dans l'année.</param>
/// <param name="Members">Membres du club à la clôture.</param>
/// <param name="MoodTotal">Somme des humeurs de sortie de ces golfeurs.</param>
/// <param name="ObjectivesDone">Objectifs accomplis dans l'année.</param>
public sealed record YearReport(int Year, long[] Totals, long OpeningBalance, long ClosingBalance, int Golfers, int Members, long MoodTotal, int ObjectivesDone)
{
    /// <summary>Résultat de l'année : somme des catégories.</summary>
    public long Result => Totals.Sum();

    /// <summary>Revenus : les catégories dont le total est positif.</summary>
    public long Income => Totals.Where(t => t > 0).Sum();

    /// <summary>Dépenses (négatives) : les catégories dont le total est négatif.</summary>
    public long Expenses => Totals.Where(t => t < 0).Sum();

    public long Total(LedgerCategory category) => Totals[(int)category];

    /// <summary>Humeur de sortie moyenne (0 sans golfeur).</summary>
    public double AverageMood => Golfers == 0 ? 0.0 : (double)MoodTotal / Golfers;

    internal void Mix(ref ulong hash)
    {
        World.Mix(ref hash, (ulong)Year);
        foreach (var t in Totals)
        {
            World.Mix(ref hash, (ulong)t);
        }

        World.Mix(ref hash, (ulong)OpeningBalance);
        World.Mix(ref hash, (ulong)ClosingBalance);
        World.Mix(ref hash, ((ulong)(uint)Golfers << 32) | (uint)Members);
        World.Mix(ref hash, (ulong)MoodTotal);
        World.Mix(ref hash, (ulong)ObjectivesDone);
    }

    internal void Write(SaveWriter w)
    {
        w.Int(Year);
        w.List(Totals, w.Long);
        w.Long(OpeningBalance);
        w.Long(ClosingBalance);
        w.Int(Golfers);
        w.Int(Members);
        w.Long(MoodTotal);
        w.Int(ObjectivesDone);
    }

    internal static YearReport Read(SaveReader r) => new(r.Int(), [.. r.List(r.Long)], r.Long(), r.Long(), r.Int(), r.Int(), r.Long(), r.Int());
}

/// <summary>
/// État de la partie (C18) : objectifs accomplis (un bit chacun), bons de pose, faillite, et ce qui sert au bilan annuel. Seul le monde l'écrit ;
/// tout champ entre dans <see cref="World.Checksum"/> et dans <see cref="World.Save"/>.
/// </summary>
public sealed class GameState
{
    private static readonly int KindCount = Enum.GetValues<BuildingKind>().Length;

    private readonly List<YearReport> _reports = [];
    private readonly int[] _vouchers = new int[KindCount];

    internal GameState(long startingBalance, bool objectives)
    {
        ObjectivesOn = objectives;
        YearStartBalance = startingBalance;
        _startingBalance = startingBalance;
    }

    private readonly long _startingBalance;

    /// <summary>Les objectifs sont évalués (une carrière) ; faux pour le parcours de démonstration.</summary>
    public bool ObjectivesOn { get; private set; }

    /// <summary>Objectifs accomplis : le bit n est celui de l'objectif n du catalogue.</summary>
    public ulong Completed { get; private set; }

    /// <summary>La caisse est restée sous le seuil de faillite assez de mois de suite ; reste vrai ensuite.</summary>
    public bool Bankrupt { get; internal set; }

    /// <summary>Le joueur a choisi de continuer malgré la faillite : plus aucun écran de fin.</summary>
    public bool ContinuedAfterBankruptcy { get; internal set; }

    /// <summary>Mois clos de suite avec le solde sous le seuil de faillite.</summary>
    public int MonthsBelow { get; internal set; }

    /// <summary>Solde au début de l'année en cours.</summary>
    public long YearStartBalance { get; internal set; }

    /// <summary>Golfeurs qui ont fini leur partie depuis le début de l'année, et la somme de leurs humeurs de sortie.</summary>
    public int YearGolfers { get; private set; }

    public long YearMoodTotal { get; private set; }

    /// <summary>Objectifs accomplis depuis le début de l'année.</summary>
    public int YearObjectives { get; private set; }

    /// <summary>Bilans des années closes, du plus ancien au plus récent.</summary>
    public IReadOnlyList<YearReport> Reports => _reports;

    /// <summary>Golfeurs qui ont fini leur partie depuis le début, année en cours comprise.</summary>
    public int TotalGolfers => _reports.Sum(r => r.Golfers) + YearGolfers;

    /// <summary>Objectifs accomplis depuis le début.</summary>
    public int CompletedCount => System.Numerics.BitOperations.PopCount(Completed);

    public bool IsCompleted(int index) => (Completed & (1UL << index)) != 0;

    /// <summary>Bons de pose restants pour ce genre de bâtiment.</summary>
    public int Vouchers(BuildingKind kind) => _vouchers[(int)kind];

    internal void Complete(int index)
    {
        Completed |= 1UL << index;
        YearObjectives++;
    }

    internal void AddVoucher(BuildingKind kind) => _vouchers[(int)kind]++;

    internal void UseVoucher(BuildingKind kind) => _vouchers[(int)kind]--;

    internal void RecordExit(int golfers, long moodTotal)
    {
        YearGolfers += golfers;
        YearMoodTotal += moodTotal;
    }

    /// <summary>Range le bilan de l'année qui se clôt et repart de zéro.</summary>
    internal void CloseYear(YearReport report)
    {
        _reports.Add(report);
        YearStartBalance = report.ClosingBalance;
        YearGolfers = 0;
        YearMoodTotal = 0;
        YearObjectives = 0;
    }

    /// <summary>Un état que rien n'a touché ne change pas la somme de contrôle (les mondes sans partie gardent leurs empreintes).</summary>
    internal bool Untouched =>
        !ObjectivesOn && Completed == 0 && !Bankrupt && !ContinuedAfterBankruptcy && MonthsBelow == 0 && YearStartBalance == _startingBalance
        && YearGolfers == 0 && YearMoodTotal == 0 && YearObjectives == 0 && _reports.Count == 0 && _vouchers.All(v => v == 0);

    internal void Mix(ref ulong hash)
    {
        if (Untouched)
        {
            return;
        }

        World.Mix(ref hash, 0x47414D45UL);
        World.Mix(ref hash, Completed);
        World.Mix(ref hash, (ObjectivesOn ? 1UL : 0UL) | (Bankrupt ? 2UL : 0UL) | (ContinuedAfterBankruptcy ? 4UL : 0UL) | ((ulong)(uint)MonthsBelow << 8));
        World.Mix(ref hash, (ulong)YearStartBalance);
        World.Mix(ref hash, ((ulong)(uint)YearGolfers << 32) | (uint)YearObjectives);
        World.Mix(ref hash, (ulong)YearMoodTotal);
        foreach (var v in _vouchers)
        {
            World.Mix(ref hash, (ulong)v);
        }

        World.Mix(ref hash, (ulong)_reports.Count);
        foreach (var r in _reports)
        {
            r.Mix(ref hash);
        }
    }

    internal void Write(SaveWriter w)
    {
        w.Bool(ObjectivesOn);
        w.ULong(Completed);
        w.Bool(Bankrupt);
        w.Bool(ContinuedAfterBankruptcy);
        w.Int(MonthsBelow);
        w.Long(YearStartBalance);
        w.Int(YearGolfers);
        w.Long(YearMoodTotal);
        w.Int(YearObjectives);
        w.List(_vouchers, w.Int);
        w.List(_reports, r => r.Write(w));
    }

    internal static GameState Read(SaveReader r, long startingBalance)
    {
        var state = new GameState(startingBalance, r.Bool()) { Completed = r.ULong(), Bankrupt = r.Bool(), ContinuedAfterBankruptcy = r.Bool(), MonthsBelow = r.Int(), YearStartBalance = r.Long() };
        (state.YearGolfers, state.YearMoodTotal, state.YearObjectives) = (r.Int(), r.Long(), r.Int());
        var vouchers = r.List(r.Int);
        if (vouchers.Count != KindCount || vouchers.Any(v => v < 0) || state.MonthsBelow < 0 || state.YearGolfers < 0 || state.YearObjectives < 0)
        {
            throw new InvalidDataException("Sauvegarde incohérente : état de partie.");
        }

        vouchers.CopyTo(state._vouchers);
        state._reports.AddRange(r.List(() => YearReport.Read(r)));
        return state;
    }
}
