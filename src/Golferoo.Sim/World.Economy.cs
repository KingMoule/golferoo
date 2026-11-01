using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>
/// Économie (C14) : la caisse débite chaque commande au moment où elle s'applique, d'après ce qui change réellement, encaisse un green fee
/// à chaque trou joué, et clôt chaque mois (entretien des trous ouverts, intérêts du découvert, cotisations des membres). À partir de <see cref="EconomyParams.OverdraftFreeHoles"/> trous ouverts, une commande
/// qui ferait passer le solde sous la limite de découvert est refusée entière.
/// </summary>
public sealed partial class World
{
    /// <summary>Paramètres de l'économie (Facile par défaut).</summary>
    public EconomyParams Economy { get; }

    /// <summary>Caisse et grand livre du club.</summary>
    public Ledger Ledger { get; }

    /// <summary>Trous ouverts aux golfeurs (aucun sans terrain).</summary>
    public int OpenHoles => Terrain is null ? 0 : _holesOpen.Count(open => open);

    /// <summary>Solde minimal permis à un achat : aucun sous <see cref="EconomyParams.OverdraftFreeHoles"/> trous ouverts, puis l'opposé de la limite.</summary>
    private long? OverdraftFloor => OpenHoles >= Economy.OverdraftFreeHoles ? -Economy.OverdraftLimit : null;

    private bool TrySpend(LedgerCategory category, long cost) => Ledger.TrySpend(category, cost, OverdraftFloor);

    /// <summary>Prix en cents de <paramref name="cells"/> cellules à <paramref name="perM2"/> cents le m² : le prix d'une cellule, arrondi au cent une fois, multiplié en entiers.</summary>
    private static long PerCell(TerrainGrid terrain, int cells, long perM2) => cells * (long)Math.Round(terrain.CellSize * terrain.CellSize * perM2);

    /// <summary>Prix en cents de <paramref name="cubicMeters"/> m³ de terre déplacée, arrondi au cent (les hauteurs sont des réels).</summary>
    private long ReliefPrice(double cubicMeters) => (long)Math.Round(cubicMeters * Economy.ReliefPerM3);

    /// <summary>
    /// Green fee d'un trou joué : le tarif du niveau d'adhésion (visiteur, Member, Silver, Gold ou Platinum), rien pour votre pro.
    /// Celui d'un visiteur du vivier suit son humeur à la fin du trou (×0,5 à −10, ×1 à zéro, ×1,2 à +10) ; celui d'un membre en journée de ligue, non.
    /// </summary>
    internal long GreenFee(Golfer g)
    {
        if (g.VisitorId >= 0)
        {
            return LevelFee(Visitors.Visitors[g.VisitorId].Level) * MoodParams.FeeBasisPoints(g.Mood, Economy.MoodFactorMinBasisPoints, Economy.MoodFactorMaxBasisPoints) / 10_000;
        }

        if (g.MemberId < 0)
        {
            return Economy.GreenFeeVisitor;
        }

        var member = League!.Members[g.MemberId];
        return member.IsPro ? 0 : member.VisitorId >= 0 ? LevelFee(Visitors.Visitors[member.VisitorId].Level) : Economy.GreenFeeMember;
    }

    /// <summary>Cotisations annuelles de tous les membres du vivier, chacune selon son niveau (votre pro ne paie rien).</summary>
    private long AnnualDues => Visitors.Visitors.Sum(v => LevelDues(v.Level));

    /// <summary>
    /// Clôture du mois <paramref name="month"/> : entretien des trous ouverts (majoré si les consignes sont exigeantes), 1 % d'intérêts sur un
    /// solde négatif, puis le douzième de la cotisation annuelle de chaque membre.
    /// </summary>
    private void CloseMonth(long month)
    {
        var demanding = Terrain?.Turf.Settings.Demanding == true;
        var maintenance = OpenHoles * Economy.MaintenancePerHole * (10_000 + (demanding ? Economy.DemandingTurfBasisPoints : 0)) / 10_000;
        Ledger.Post(LedgerCategory.Maintenance, -WithShed(maintenance));
        if (Ledger.Balance < 0)
        {
            Ledger.Post(LedgerCategory.Interest, Ledger.Balance * Economy.OverdraftInterestBasisPoints / 10_000);
        }

        Ledger.Post(LedgerCategory.Dues, AnnualDues / Calendar.MonthsPerYear);

        Ledger.CloseMonth(month);
        AfterMonthClosed(month);
        Reactions.CloseMonth();
        ForgetStroke(); // un mois clos ne se rembourse plus dans le suivant
    }

    private long CurrentMonth => Calendar.MonthIndex(Tick, _dayOffset);

    /// <summary>
    /// Un monde Facile dont la caisse n'a jamais bougé mélange 0, comme l'ancien total des travaux d'arbres à cette place :
    /// les sommes de contrôle relevées avant C14 restent valables.
    /// </summary>
    private void MixEconomy(ref ulong hash)
    {
        if (Ledger.Untouched && Economy == EconomyParams.Facile)
        {
            Mix(ref hash, 0UL);
            return;
        }

        foreach (var v in Economy.Values())
        {
            Mix(ref hash, (ulong)v);
        }

        Ledger.Mix(ref hash);
    }
}
