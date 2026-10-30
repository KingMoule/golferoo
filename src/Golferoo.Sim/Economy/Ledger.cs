using Golferoo.Sim.Save;

namespace Golferoo.Sim.Economy;

/// <summary>Catégories du grand livre. Les valeurs servent d'index stable (sauvegarde) : ne pas réordonner.</summary>
public enum LedgerCategory : byte
{
    Surfaces,
    Relief,
    Holes,
    Trees,
    Maintenance,
    Interest,
    GreenFees,
    Dues,
    Buildings,
}

/// <summary>Ligne d'un mois clos : son numéro (<see cref="Time.Calendar.MonthIndex"/>) et le total signé de chaque catégorie (dépenses négatives).</summary>
public sealed class LedgerMonth
{
    private readonly long[] _totals;

    internal LedgerMonth(long month, long[] totals)
    {
        Month = month;
        _totals = totals;
    }

    public long Month { get; }

    public long Total(LedgerCategory category) => _totals[(int)category];

    /// <summary>Résultat du mois : somme des catégories.</summary>
    public long Result => _totals.Sum();

    internal IReadOnlyList<long> Totals => _totals;
}

/// <summary>
/// Caisse du club (C14) : solde en cents et totaux signés par catégorie, pour le mois courant et les <see cref="MonthsKept"/> derniers mois clos.
/// Seul le monde l'écrit, au début d'un tick (commandes) ou à la clôture d'un mois ; le jeu lit et écoute les évènements.
/// </summary>
public sealed class Ledger
{
    /// <summary>Mois clos gardés dans l'historique.</summary>
    public const int MonthsKept = 24;

    private static readonly int CategoryCount = Enum.GetValues<LedgerCategory>().Length;

    private readonly long _start;
    private long[] _current = new long[CategoryCount];
    private readonly List<LedgerMonth> _months = [];

    internal Ledger(long balance)
    {
        _start = balance;
        Balance = balance;
    }

    /// <summary>Levé à chaque mouvement, avec le nouveau solde.</summary>
    public event Action<long>? MoneyChanged;

    /// <summary>Levé à la clôture d'un mois, avec sa ligne.</summary>
    public event Action<LedgerMonth>? MonthClosed;

    /// <summary>Levé quand un achat est refusé par la limite de découvert, avec sa catégorie et son prix : rien n'a été appliqué.</summary>
    public event Action<LedgerCategory, long>? PurchaseRefused;

    /// <summary>Solde, en cents.</summary>
    public long Balance { get; private set; }

    /// <summary>Achats refusés depuis le début.</summary>
    public int Refused { get; private set; }

    /// <summary>Mois clos, du plus ancien au plus récent.</summary>
    public IReadOnlyList<LedgerMonth> Months => _months;

    /// <summary>Total signé de la catégorie pour le mois en cours.</summary>
    public long Current(LedgerCategory category) => _current[(int)category];

    /// <summary>Formate un montant en dollars (cents facultatifs), pour l'affichage.</summary>
    public static string Dollars(long cents) => FormattableString.Invariant($"{cents / 100m:0.##} $");

    /// <summary>Inscrit un mouvement signé (revenu positif, dépense négative), sans limite.</summary>
    internal void Post(LedgerCategory category, long amount)
    {
        if (amount == 0)
        {
            return;
        }

        _current[(int)category] += amount;
        Balance += amount;
        MoneyChanged?.Invoke(Balance);
    }

    /// <summary>
    /// Débite <paramref name="cost"/> si le solde reste au-dessus de <paramref name="floor"/> (nul : sans limite) ; sinon rien n'est débité,
    /// <see cref="PurchaseRefused"/> est levé et la réponse est faux. Un achat gratuit passe toujours.
    /// </summary>
    internal bool TrySpend(LedgerCategory category, long cost, long? floor)
    {
        if (cost > 0 && floor is { } f && Balance - cost < f)
        {
            Refused++;
            PurchaseRefused?.Invoke(category, cost);
            return false;
        }

        Post(category, -cost);
        return true;
    }

    /// <summary>Clôt le mois <paramref name="month"/> : ses totaux passent dans l'historique et le mois en cours repart de zéro.</summary>
    internal LedgerMonth CloseMonth(long month)
    {
        var line = new LedgerMonth(month, _current);
        _current = new long[CategoryCount];
        _months.Add(line);
        if (_months.Count > MonthsKept)
        {
            _months.RemoveAt(0);
        }

        MonthClosed?.Invoke(line);
        return line;
    }

    /// <summary>Vrai tant que rien n'a bougé depuis le solde de départ.</summary>
    internal bool Untouched => Balance == _start && Refused == 0 && _months.Count == 0 && _current.All(v => v == 0);

    internal void Mix(ref ulong hash)
    {
        World.Mix(ref hash, (ulong)_start);
        World.Mix(ref hash, (ulong)Balance);
        World.Mix(ref hash, (ulong)Refused);
        MixTotals(ref hash, _current);
        World.Mix(ref hash, (ulong)_months.Count);
        foreach (var m in _months)
        {
            World.Mix(ref hash, (ulong)m.Month);
            MixTotals(ref hash, m.Totals);
        }
    }

    private static void MixTotals(ref ulong hash, IReadOnlyList<long> totals)
    {
        foreach (var v in totals)
        {
            World.Mix(ref hash, (ulong)v);
        }
    }

    internal void Write(SaveWriter w)
    {
        w.Long(_start);
        w.Long(Balance);
        w.Int(Refused);
        WriteTotals(w, _current);
        w.List(_months, m =>
        {
            w.Long(m.Month);
            WriteTotals(w, m.Totals);
        });
    }

    private static void WriteTotals(SaveWriter w, IReadOnlyList<long> totals)
    {
        foreach (var v in totals)
        {
            w.Long(v);
        }
    }

    internal static Ledger Read(SaveReader r)
    {
        var ledger = new Ledger(r.Long()) { Balance = r.Long(), Refused = r.Int() };
        ledger._current = ReadTotals(r);
        ledger._months.AddRange(r.List(() => new LedgerMonth(r.Long(), ReadTotals(r))));
        if (ledger._months.Count > MonthsKept)
        {
            throw new InvalidDataException("Sauvegarde incohérente : trop de mois dans le grand livre.");
        }

        return ledger;
    }

    private static long[] ReadTotals(SaveReader r)
    {
        var totals = new long[CategoryCount];
        for (var i = 0; i < (r.Version < 12 ? CategoryCount - 1 : CategoryCount); i++) // v11 : sans la catégorie Buildings
        {
            totals[i] = r.Long();
        }

        return totals;
    }
}
