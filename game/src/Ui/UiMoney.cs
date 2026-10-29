using System.Globalization;
using Golferoo.Sim.Economy;

namespace Golferoo.Game.Ui;

/// <summary>Les montants à l'écran : dollars entiers sans centimes inutiles, espace entre les milliers (« 150 000 $ », « 0,20 $ »).</summary>
public static class UiMoney
{
    /// <summary>Formate <paramref name="cents"/> ; avec <paramref name="signed"/>, un montant positif porte « + » et zéro devient « — ».</summary>
    public static string Format(long cents, bool signed = false)
    {
        if (signed && cents == 0)
        {
            return "—";
        }

        var abs = Math.Abs(cents);
        var text = (abs / 100).ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
        if (abs % 100 != 0)
        {
            text += "," + (abs % 100).ToString("00", CultureInfo.InvariantCulture);
        }

        var sign = cents < 0 ? "-" : signed ? "+" : string.Empty;
        return sign + text + " $";
    }

    /// <summary>Le texte de la pastille d'argent : le solde, puis le résultat du mois (« 149 520 $ · -480 »).</summary>
    public static string Pill(Ledger ledger) => $"{Format(ledger.Balance)} · {Format(MonthResult(ledger), true).Replace(" $", string.Empty)}";

    /// <summary>Résultat du mois en cours : la somme des catégories du grand livre.</summary>
    public static long MonthResult(Ledger ledger) => Enum.GetValues<LedgerCategory>().Sum(ledger.Current);
}
