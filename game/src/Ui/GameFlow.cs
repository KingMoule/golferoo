using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Objectives;

namespace Golferoo.Game.Ui;

/// <summary>
/// La partie du début à la fin (C18 lot 2) : le panneau des objectifs sous l'écusson, le bilan annuel et l'écran de fin de la faillite
/// (charger, menu titre, « Continuer quand même »). Les évènements de la sim passent aussi par Birdie, la voix du club.
/// </summary>
public partial class GameFlow : Node
{
    private const float PanelWidth = 360f;

    private Main _main = null!;
    private Button? _pill;
    private UiPanel? _objectives;
    private VBoxContainer? _list;
    private UiPanel _report = null!;
    private VBoxContainer _reportBody = null!;
    private Control _end = null!;
    private UiPanel _endPanel = null!;
    private Label _endText = null!;
    private UiPanel? _slots;
    private double _sinceRefresh;

    public UiPanel? ObjectivesPanel => _objectives;

    public UiPanel ReportPanel => _report;

    /// <summary>L'écran de fin est affiché : il garde le clavier et la partie est en pause.</summary>
    public bool EndOpen => _end.Visible;

    public void Init(Main main)
    {
        _main = main;
        var world = main.World;
        if (world.State.ObjectivesOn)
        {
            BuildObjectives();
            world.ObjectiveCompleted += OnObjective;
        }

        BuildReport();
        BuildEnd();
        world.YearClosed += OnYear;
        world.Bankrupt += OnBankrupt;
        if (world.State.Bankrupt && !world.State.ContinuedAfterBankruptcy)
        {
            OpenEnd(); // une partie en faillite rechargée retrouve son écran de fin
        }
    }

    public override void _ExitTree()
    {
        if (_main?.World is { } world)
        {
            world.ObjectiveCompleted -= OnObjective;
            world.YearClosed -= OnYear;
            world.Bankrupt -= OnBankrupt;
        }
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (_sinceRefresh >= 0.5)
        {
            _sinceRefresh = 0;
            RefreshObjectives();
        }
    }

    // ---- Objectifs ----

    private void BuildObjectives()
    {
        _objectives = UiPanel.Create(UiIds.Panel.Objectives, "Objectifs du club", "flag", PanelWidth);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 8);
        _objectives.Content.AddChild(_list);
        _main.Panels.Add(_objectives);

        _pill = new Button { Name = UiIds.Hud.ObjectivesPill, FocusMode = Control.FocusModeEnum.None, ThemeTypeVariation = "UiCaption" };
        _pill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        (_pill.OffsetLeft, _pill.OffsetRight, _pill.OffsetTop, _pill.OffsetBottom) = (14f, 14f + 104f, -34f, -6f);
        _pill.Pressed += () =>
        {
            _main.Panels.Toggle(_objectives);
            RefreshObjectives();
        };
        _main.Bar.AddChild(_pill);
        RefreshObjectives();
    }

    private void RefreshObjectives()
    {
        if (_objectives is null || _pill is null || _list is null)
        {
            return;
        }

        var world = _main.World;
        var all = ObjectiveCatalog.Default;
        _pill.Text = $"Objectifs {world.State.CompletedCount}/{all.Count}";
        var next = all.Where((_, i) => !world.State.IsCompleted(i)).FirstOrDefault();
        _pill.TooltipText = next is null ? "Tous les objectifs sont atteints." : $"Prochain objectif : {next.Title}";
        if (!_objectives.Visible)
        {
            return;
        }

        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        for (var i = 0; i < all.Count; i++)
        {
            var def = all[i];
            var done = world.State.IsCompleted(i);
            var row = new VBoxContainer();
            var head = new HBoxContainer();
            head.AddChild(new Label { Text = (done ? "✓ " : "○ ") + def.Title, ThemeTypeVariation = "UiName", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            head.AddChild(new Label { Text = done ? "fait" : $"{Math.Min(world.ObjectiveProgress(def), def.Threshold)} / {def.Threshold}", ThemeTypeVariation = "UiValue" });
            row.AddChild(head);
            var reward = new Label { Text = def.Text + " Récompense : " + UiNames.Reward(def, world.Difficulty.ObjectiveRewards.GetValueOrDefault(def.Id)) + ".", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            reward.CustomMinimumSize = new Vector2(PanelWidth - 30f, 0f);
            row.AddChild(reward);
            _list.AddChild(row);
        }

        var vouchers = Enum.GetValues<Sim.Buildings.BuildingKind>().Where(k => world.State.Vouchers(k) > 0).Select(k => $"{UiNames.BuildingName(k)} ×{world.State.Vouchers(k)}").ToList();
        if (vouchers.Count > 0)
        {
            _list.AddChild(new Label { Text = "Bons de pose : " + string.Join(", ", vouchers), ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(PanelWidth - 30f, 0f) });
        }

        var reportButton = new Button { Text = "Dernier bilan annuel", FocusMode = Control.FocusModeEnum.None, Disabled = world.State.Reports.Count == 0 };
        reportButton.Pressed += () => OpenReport(world.State.Reports[^1]);
        _list.AddChild(reportButton);
    }

    private void OnObjective(ObjectiveDef def)
    {
        var reward = UiNames.Reward(def, _main.World.Difficulty.ObjectiveRewards.GetValueOrDefault(def.Id));
        _main.Birdie.Post(Birdie.Club, $"Objectif atteint : {def.Title}. Récompense : {reward}.", "#objectif");
        _main.ShowMessage($"Objectif atteint : {def.Title}");
        RefreshObjectives();
    }

    // ---- Bilan annuel ----

    private void BuildReport()
    {
        _report = UiPanel.Create(UiIds.Panel.YearReport, "Bilan de l'année", "coin", PanelWidth);
        _reportBody = new VBoxContainer();
        _reportBody.AddThemeConstantOverride("separation", 8);
        _report.Content.AddChild(_reportBody);
        _main.Panels.Add(_report);
    }

    private void OnYear(YearReport report)
    {
        _main.Birdie.Post(Birdie.Club, $"Bilan de l'an {report.Year} : résultat {UiMoney.Format(report.Result, signed: true)}, solde {UiMoney.Format(report.ClosingBalance)}.", "#bilan");
        OpenReport(report);
    }

    private void OpenReport(YearReport report)
    {
        _report.Title.Text = $"Bilan de l'an {report.Year}";
        foreach (var child in _reportBody.GetChildren())
        {
            _reportBody.RemoveChild(child);
            child.QueueFree();
        }

        var rows = Enum.GetValues<LedgerCategory>().Where(c => report.Total(c) != 0).Select(c => new[] { Finances.CategoryName(c), UiMoney.Format(report.Total(c), signed: true) })
            .Append(["Résultat", UiMoney.Format(report.Result, signed: true)]).ToArray();
        _reportBody.AddChild(UiTable.Create("YearReportTable", ["", "Année"], rows, rows.Length - 1));
        var lines = new[]
        {
            $"Solde : {UiMoney.Format(report.OpeningBalance)} → {UiMoney.Format(report.ClosingBalance)}",
            $"Golfeurs : {report.Golfers} · membres : {report.Members}",
            $"Humeur moyenne à la sortie : {report.AverageMood:0.0}".Replace('.', ','),
            $"Objectifs accomplis cette année : {report.ObjectivesDone}",
        };
        foreach (var line in lines)
        {
            _reportBody.AddChild(new Label { Text = line, ThemeTypeVariation = "UiCaption" });
        }

        _main.Panels.Open(_report);
    }

    // ---- Fin de partie ----

    private void BuildEnd()
    {
        _end = new ColorRect { Name = UiIds.Menu.End, Color = new Color(0f, 0f, 0f, 0.6f), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _end.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _endPanel = UiPanel.Create("EndPanel", "Faillite", "coin", 460f);
        _endPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _endPanel.GrowHorizontal = _endPanel.GrowVertical = Control.GrowDirection.Both;
        _endPanel.CloseButton.Visible = false;
        _endText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420f, 0f) };
        _endPanel.Content.AddChild(_endText);
        AddEndButton(UiIds.Menu.EndLoad, "Charger", ShowSlots);
        AddEndButton(UiIds.Menu.EndTitle, "Menu titre", () => GetTree().ChangeSceneToFile(TitleMenu.Scene));
        AddEndButton(UiIds.Menu.EndContinue, "Continuer quand même", ContinueAnyway);
        _end.AddChild(_endPanel);
        _main.Ui.AddChild(_end);
    }

    private void AddEndButton(string id, string text, Action pressed)
    {
        var button = new Button { Name = id, Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 36f) };
        button.Pressed += pressed;
        _endPanel.Content.AddChild(button);
    }

    private void OnBankrupt()
    {
        _main.Birdie.Post(Birdie.Club, "Le club est en faillite : la caisse est restée trop longtemps dans le rouge.", "#faillite");
        OpenEnd();
    }

    /// <summary>Met la partie en pause et montre ses chiffres : jours de jeu, golfeurs accueillis, argent, objectifs.</summary>
    internal void OpenEnd()
    {
        var world = _main.World;
        var state = world.State;
        var objectives = state.ObjectivesOn ? $"\nObjectifs atteints : {state.CompletedCount} sur {ObjectiveCatalog.Default.Count}" : string.Empty;
        _endText.Text = $"Le solde est resté sous -{UiMoney.Format(world.Economy.BankruptcyThreshold)} pendant {world.Economy.BankruptcyMonths} mois de suite : le club ferme ses portes.\n\n"
            + $"Jours de jeu : {world.DayIndex + 1}\nGolfeurs accueillis : {state.TotalGolfers}\nArgent : {UiMoney.Format(world.Ledger.Balance)}{objectives}";
        _main.Paused = true;
        _endPanel.Visible = true;
        _end.Visible = true;
    }

    private void ContinueAnyway()
    {
        _main.World.ContinueAfterBankruptcy();
        _end.Visible = false;
        _main.Paused = false;
        _main.Birdie.Post(Birdie.Club, "Le club continue malgré la faillite : plus d'écran de fin, mais la caisse reste dans le rouge.", "#faillite");
    }

    private void ShowSlots()
    {
        _slots?.QueueFree();
        _slots = SlotsScreen.Create(false, slot =>
        {
            if (_main.LoadSlot(slot) is { } error)
            {
                _endText.Text += "\n" + error;
                CloseSlots();
            }
        }, CloseSlots);
        _endPanel.Visible = false;
        _end.AddChild(_slots);
    }

    private void CloseSlots()
    {
        _slots?.QueueFree();
        _slots = null;
        _endPanel.Visible = true;
    }
}
