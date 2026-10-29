using Godot;
using Golferoo.Sim.Economy;

namespace Golferoo.Game.Ui;

/// <summary>
/// Les finances du club (C14 lot 3) : le panneau (solde, douze derniers mois en barres, détail par catégorie sur le mois courant et le précédent),
/// la pastille du coût du coup près du curseur, la confirmation du passage au découvert (une fois par passage) et le message de Birdie quand la
/// limite de découvert refuse un achat. Ne lit que le grand livre de la sim (<see cref="Ledger"/>) ; aucune règle d'argent ici.
/// </summary>
public partial class Finances : Node
{
    public const float Width = 460f;
    private const float ChartHeight = 150f;
    private const ulong RefusalGapMilliseconds = 4000;

    private static readonly LedgerCategory[] Revenues = [LedgerCategory.GreenFees, LedgerCategory.Dues];
    private static readonly LedgerCategory[] Expenses =
    [
        LedgerCategory.Surfaces, LedgerCategory.Relief, LedgerCategory.Holes, LedgerCategory.Trees, LedgerCategory.Maintenance, LedgerCategory.Interest,
    ];

    private Main _main = null!;
    private UiPanel _panel = null!;
    private Label _balance = null!;
    private FinanceChart _chart = null!;
    private UiTable _table = null!;
    private string _tableKey = string.Empty;
    private Label _cost = null!;
    private UiPanel _prompt = null!;
    private Label _promptText = null!;
    private bool _accepted;
    private bool _inDebt;
    private ulong _lastRefusal;

    public bool IsOpen => _panel.Visible;

    /// <summary>La confirmation du découvert est à l'écran.</summary>
    public bool PromptOpen => _prompt.Visible;

    /// <summary>Combien de fois la confirmation s'est ouverte depuis le début de la partie.</summary>
    public int Prompts { get; private set; }

    public FinanceChart Chart => _chart;

    public UiTable Table => _table;

    /// <summary>La pastille du coût du coup, près du curseur.</summary>
    public Label CostPill => _cost;

    public static string CategoryName(LedgerCategory category) => category switch
    {
        LedgerCategory.Surfaces => "Surfaces",
        LedgerCategory.Relief => "Relief",
        LedgerCategory.Holes => "Trous",
        LedgerCategory.Trees => "Arbres",
        LedgerCategory.Maintenance => "Entretien",
        LedgerCategory.Interest => "Intérêts",
        LedgerCategory.GreenFees => "Green fees",
        _ => "Cotisations",
    };

    public void Toggle()
    {
        _main.Panels.Toggle(_panel);
        Refresh();
    }

    /// <summary>Construit le panneau (fermé, dans la pile), la pastille de coût et la confirmation sous la racine de l'interface.</summary>
    public void Init(Main main)
    {
        _main = main;
        var ledger = main.World.Ledger;
        _panel = UiPanel.Create(UiIds.Panel.Finances, "Finances", "coin", Width);
        var head = new HBoxContainer();
        head.AddChild(new Label { Text = "Solde", ThemeTypeVariation = "UiKey", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        _balance = new Label { Name = UiIds.Finance.Balance, ThemeTypeVariation = "UiValue" };
        _balance.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(_balance);
        _panel.Content.AddChild(head);

        _chart = new FinanceChart
        {
            Name = UiIds.Finance.Chart,
            CustomMinimumSize = new Vector2(0f, ChartHeight),
            TooltipText = "Les douze derniers mois, le mois en cours à droite. Barres : revenus (vert) et dépenses (brique). Ligne : solde en fin de mois, sur sa propre échelle.",
        };
        _panel.Content.AddChild(_chart);
        _panel.Content.AddChild(Legend());

        _table = UiTable.Create(UiIds.Finance.Table, ["", "Ce mois", "Mois précédent"], [], -1);
        _panel.Content.AddChild(_table);
        main.Panels.Add(_panel);

        _cost = new Label { Name = UiIds.Hud.StrokeCost, ThemeTypeVariation = "UiPrice", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        main.Ui.AddChild(_cost);
        BuildPrompt();

        ledger.MoneyChanged += OnMoneyChanged;
        ledger.PurchaseRefused += OnRefused;
        Refresh();
    }

    private static HBoxContainer Legend()
    {
        var legend = new HBoxContainer();
        legend.AddThemeConstantOverride("separation", 14);
        foreach (var (text, color) in new[] { ("■ Revenus", UiTheme.Grass), ("■ Dépenses", UiTheme.Brick), ("● Solde", UiTheme.Flag) })
        {
            var label = new Label { Text = text, ThemeTypeVariation = "UiCaption" };
            label.AddThemeColorOverride("font_color", color);
            legend.AddChild(label);
        }

        return legend;
    }

    private void BuildPrompt()
    {
        _prompt = UiPanel.Create(UiIds.Panel.Overdraft, "Passer au découvert ?", "coin", 400f);
        _prompt.Visible = false;
        _prompt.SetAnchorsPreset(Control.LayoutPreset.Center);
        _prompt.GrowHorizontal = _prompt.GrowVertical = Control.GrowDirection.Both;
        _prompt.Closed += () => _prompt.Visible = false;
        _promptText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360f, 0f) };
        _prompt.Content.AddChild(_promptText);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 8);
        var no = new Button { Name = UiIds.Finance.OverdraftNo, Text = "Pas maintenant", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 32f) };
        no.Pressed += () => _prompt.Visible = false;
        var yes = new Button { Name = UiIds.Finance.OverdraftYes, Text = "Passer au découvert", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 32f) };
        yes.Pressed += () =>
        {
            _accepted = true;
            _prompt.Visible = false;
        };
        buttons.AddChild(no);
        buttons.AddChild(yes);
        _prompt.Content.AddChild(buttons);
        _main.Ui.AddChild(_prompt);
    }

    /// <summary>
    /// Une dépense de <paramref name="cost"/> cents (0 : montant inconnu) peut-elle partir ? Oui tant que le solde reste positif, ou une fois le
    /// découvert accepté ; sinon la confirmation s'ouvre (une seule fois à la fois) et la dépense ne part pas : on la refait après avoir accepté.
    /// </summary>
    public bool Allow(long cost)
    {
        var balance = _main.World.Ledger.Balance;
        if (balance - cost >= 0)
        {
            // Un solde qui reste positif n'a rien à confirmer ; un accord donné pour un passage précédent ne vaut plus.
            _accepted = _accepted && balance < 0;
            return true;
        }

        if (_accepted)
        {
            return true;
        }

        if (!_prompt.Visible)
        {
            var economy = _main.World.Economy;
            _promptText.Text = $"Cette dépense{(cost > 0 ? $" (environ {UiMoney.Format(cost)})" : string.Empty)} fait passer le solde sous zéro. "
                + $"Le découvert coûte {economy.OverdraftInterestBasisPoints / 100m:0.##} % d'intérêts par mois ; à partir de {economy.OverdraftFreeHoles} trous ouverts, il est limité à {UiMoney.Format(economy.OverdraftLimit)}.";
            _prompt.Visible = true;
            Prompts++;
        }

        return false;
    }

    /// <summary>Échap ferme la confirmation avant les panneaux ; vrai si elle était ouverte.</summary>
    public bool DismissPrompt()
    {
        var open = _prompt.Visible;
        _prompt.Visible = false;
        return open;
    }

    /// <summary>La pastille du coût cumulé du coup, à côté du curseur pendant le coup (cachée sans coût).</summary>
    public void ShowCost(bool stroking, Vector2 cursor, long cost)
    {
        _cost.Visible = stroking && cost > 0;
        if (_cost.Visible)
        {
            _cost.Text = UiMoney.Format(cost);
            _cost.Position = cursor + new Vector2(22f, 24f);
        }
    }

    private void OnMoneyChanged(long balance)
    {
        if (balance < 0)
        {
            _inDebt = true;
        }
        else if (_inDebt)
        {
            _inDebt = false;
            _accepted = false;
        }
    }

    /// <summary>Un achat refusé par la limite : un message de Birdie (pas plus d'un toutes les quatre secondes pendant qu'on garde le bouton).</summary>
    private void OnRefused(LedgerCategory category, long cost)
    {
        var now = Time.GetTicksMsec();
        if (_lastRefusal != 0 && now - _lastRefusal < RefusalGapMilliseconds)
        {
            return;
        }

        _lastRefusal = now;
        var economy = _main.World.Economy;
        _main.Birdie?.Post(Birdie.Club, $"Achat refusé : {UiMoney.Format(cost)} de {CategoryName(category).ToLowerInvariant()}. Au-delà de {economy.OverdraftFreeHoles} trous ouverts, le découvert est limité à {UiMoney.Format(economy.OverdraftLimit)}.", "#finances");
    }

    /// <summary>Recopie le grand livre dans le panneau ouvert ; les lignes du tableau ne sont refaites que si un montant a changé.</summary>
    public void Refresh()
    {
        if (!_panel.Visible)
        {
            return;
        }

        var ledger = _main.World.Ledger;
        var text = UiMoney.Format(ledger.Balance);
        if (_balance.Text != text)
        {
            _balance.Text = text;
            _balance.AddThemeColorOverride("font_color", ledger.Balance < 0 ? UiTheme.BrickLight : UiTheme.Grass);
        }

        FillChart(ledger);
        var rows = Rows(ledger);
        var key = string.Join('|', rows.Select(r => string.Join(';', r)));
        if (key != _tableKey)
        {
            _tableKey = key;
            _table.SetRows(rows, rows.Length - 1);
        }
    }

    private static string[][] Rows(Ledger ledger)
    {
        var previous = ledger.Months.Count > 0 ? ledger.Months[^1] : null;
        string[] Row(string name, long current, long? last) => [name, UiMoney.Format(current, true), last is { } l ? UiMoney.Format(l, true) : "—"];
        var rows = Revenues.Concat(Expenses).Select(c => Row(CategoryName(c), ledger.Current(c), previous?.Total(c))).ToList();
        rows.Add(Row("Résultat", UiMoney.MonthResult(ledger), previous?.Result));
        return [.. rows];
    }

    private void FillChart(Ledger ledger)
    {
        var chart = _chart;
        var month = _main.World.Date.Month;
        for (var i = 0; i < FinanceChart.Months; i++)
        {
            chart.Labels[i] = UiNames.MonthsShort[((month - 1 - (FinanceChart.Months - 1 - i) + (FinanceChart.Months * 2)) % FinanceChart.Months)];
            chart.Known[i] = false;
        }

        var last = FinanceChart.Months - 1;
        Set(last, ledger.Balance, ledger.Current);
        var end = ledger.Balance - UiMoney.MonthResult(ledger);
        for (var k = 0; k < ledger.Months.Count && k < last; k++)
        {
            var closed = ledger.Months[^(k + 1)];
            Set(last - 1 - k, end, closed.Total);
            end -= closed.Result;
        }

        chart.QueueRedraw();

        void Set(int slot, long balance, Func<LedgerCategory, long> total)
        {
            chart.Known[slot] = true;
            chart.Balance[slot] = balance;
            chart.Revenue[slot] = Enum.GetValues<LedgerCategory>().Sum(c => Math.Max(0L, total(c)));
            chart.Expense[slot] = -Enum.GetValues<LedgerCategory>().Sum(c => Math.Min(0L, total(c)));
        }
    }
}
