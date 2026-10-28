using Golferoo.Sim.Economy;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>
/// Économie (C14) : la caisse débite chaque commande au moment où elle s'applique, d'après ce qui change réellement, et clôt chaque mois
/// (entretien des trous ouverts, intérêts du découvert). Au-delà de <see cref="EconomyParams.OverdraftFreeHoles"/> trous ouverts, une commande
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

    /// <summary>Solde minimal permis à un achat : aucun jusqu'à <see cref="EconomyParams.OverdraftFreeHoles"/> trous ouverts, puis l'opposé de la limite.</summary>
    private long? OverdraftFloor => OpenHoles > Economy.OverdraftFreeHoles ? -Economy.OverdraftLimit : null;

    private bool TrySpend(LedgerCategory category, long cost) => Ledger.TrySpend(category, cost, OverdraftFloor);

    /// <summary>Prix en cents de <paramref name="quantity"/> unités (m² ou m³) à <paramref name="perUnit"/> cents, arrondi au cent.</summary>
    private static long Price(double quantity, long perUnit) => (long)Math.Round(quantity * perUnit);

    /// <summary>Clôture du mois <paramref name="month"/> : entretien des trous ouverts (majoré si les consignes sont exigeantes), puis 1 % d'intérêts sur un solde négatif.</summary>
    private void CloseMonth(long month)
    {
        var demanding = Terrain?.Turf.Settings.Demanding == true;
        var maintenance = OpenHoles * Economy.MaintenancePerHole * (10_000 + (demanding ? Economy.DemandingTurfBasisPoints : 0)) / 10_000;
        Ledger.Post(LedgerCategory.Maintenance, -maintenance);
        if (Ledger.Balance < 0)
        {
            Ledger.Post(LedgerCategory.Interest, Ledger.Balance * Economy.OverdraftInterestBasisPoints / 10_000);
        }

        Ledger.CloseMonth(month);
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
