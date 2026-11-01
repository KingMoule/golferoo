using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Objectives;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>
/// Partie (C18) : objectifs évalués à chaque changement de jour, faillite comptée à chaque mois clos, bilan annuel à la clôture de décembre.
/// </summary>
public sealed partial class World
{
    /// <summary>Niveau de difficulté (Facile par défaut ; non sauvé, comme les autres données embarquées).</summary>
    public DifficultyParams Difficulty { get; private set; } = DifficultyParams.Facile;

    /// <summary>État de la partie : objectifs accomplis, bons de pose, faillite, bilans.</summary>
    public GameState State { get; private set; } = null!;

    internal int ProMemberId => _proMemberId;

    /// <summary>Un objectif vient d'être atteint : sa récompense est déjà versée.</summary>
    public event Action<ObjectiveDef>? ObjectiveCompleted;

    /// <summary>Le solde est resté sous le seuil de faillite assez de mois de suite (levé une fois).</summary>
    public event Action? Bankrupt;

    /// <summary>Une année civile vient de se clore.</summary>
    public event Action<YearReport>? YearClosed;

    /// <summary>Compteur actuel d'un objectif, pour l'affichage (« 7 / 10 »).</summary>
    public int ObjectiveProgress(ObjectiveDef def) => ObjectiveEvaluator.Progress(this, def);

    /// <summary>Le joueur continue malgré la faillite : plus aucun écran de fin.</summary>
    public void ContinueAfterBankruptcy()
    {
        if (State.Bankrupt)
        {
            State.ContinuedAfterBankruptcy = true;
        }
    }

    /// <summary>Appelé au changement de jour : coche les objectifs atteints et verse leur récompense (argent au grand livre, bon de pose).</summary>
    private void EvaluateObjectives()
    {
        if (!State.ObjectivesOn)
        {
            return;
        }

        foreach (var i in ObjectiveEvaluator.Reached(this).ToList())
        {
            var def = ObjectiveCatalog.Default[i];
            State.Complete(i);
            Ledger.Post(LedgerCategory.Objectives, Difficulty.ObjectiveRewards.GetValueOrDefault(def.Id));
            if (def.Voucher is { } kind)
            {
                State.AddVoucher(kind);
            }

            ObjectiveCompleted?.Invoke(def);
        }
    }

    /// <summary>Appelé après la clôture du mois <paramref name="month"/> : compteur de faillite, puis bilan si c'était décembre.</summary>
    private void AfterMonthClosed(long month)
    {
        State.MonthsBelow = Ledger.Balance < -Economy.BankruptcyThreshold ? State.MonthsBelow + 1 : 0;
        if (State.MonthsBelow >= Economy.BankruptcyMonths && !State.Bankrupt)
        {
            State.Bankrupt = true;
            Bankrupt?.Invoke();
        }

        var (year, monthOfYear) = Calendar.YearAndMonth(month);
        if (monthOfYear != 12)
        {
            return;
        }

        var totals = new long[Enum.GetValues<LedgerCategory>().Length];
        foreach (var m in Ledger.Months.Where(m => Calendar.YearAndMonth(m.Month).Year == year))
        {
            for (var c = 0; c < totals.Length; c++)
            {
                totals[c] += m.Total((LedgerCategory)c);
            }
        }

        var members = Visitors.Visitors.Count(v => v.Level != Golfers.MemberLevel.Visitor);
        var report = new YearReport(year, totals, State.YearStartBalance, Ledger.Balance, State.YearGolfers, members, State.YearMoodTotal, State.YearObjectives);
        State.CloseYear(report);
        YearClosed?.Invoke(report);
    }
}
