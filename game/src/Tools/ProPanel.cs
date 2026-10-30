using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim.Pro;

namespace Golferoo.Game.Tools;

/// <summary>
/// Fiche de votre pro (bouton Votre pro, C25) : les 17 habiletés en quatre groupes (clubs, trajectoires, qualités, situations). Pour chacune,
/// le niveau, la barre d'XP du niveau, l'effet actuel et celui du niveau suivant ; un bouton « Partie d'entraînement ». Un avis Birdie
/// s'affiche à chaque niveau gagné. Aucune règle ici : les noms, groupes et effets viennent de <see cref="ProEffects"/>.
/// </summary>
public partial class ProPanel : Node
{
    private static readonly ProSkill[] Skills = [.. Enum.GetValues<ProSkill>().OrderBy(s => (int)ProEffects.GroupOf(s)).ThenBy(s => (int)s)];

    private Main _main = null!;
    private ProSkills _skills = null!;
    private UiPanel _panel = null!;
    private Label _level = null!;
    private ScrollContainer _scroll = null!;
    private readonly UiGauge[] _gauges = new UiGauge[Skills.Length];
    private readonly Label[] _effects = new Label[Skills.Length];
    private readonly int[] _seen = new int[Skills.Length];

    public bool IsOpen => _panel.Visible;

    public void Init(Main main)
    {
        _main = main;
        _skills = main.World.Pro!;
        _panel = UiPanel.Create(UiIds.Panel.Pro, "Votre pro", "figurine", 380f);
        var content = _panel.Content;
        _level = new Label { ThemeTypeVariation = "UiName" };
        content.AddChild(_level);
        content.AddChild(new Label
        {
            Text = $"Chaque coup nourrit les habiletés sollicitées selon sa qualité (ligue ×2, entraînement {_skills.TrainingCap / 100} XP par an au plus).",
            ThemeTypeVariation = "UiCaption",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        // Les jauges défilent quand l'écran logique est bas (150 % : 720 px), pour que le panneau ne passe pas sous la barre.
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scroll.AddChild(list);
        content.AddChild(_scroll);
        for (var i = 0; i < Skills.Length; i++)
        {
            var skill = Skills[i];
            if (i == 0 || ProEffects.GroupOf(skill) != ProEffects.GroupOf(Skills[i - 1]))
            {
                list.AddChild(new Label { Text = ProEffects.GroupName(ProEffects.GroupOf(skill)), ThemeTypeVariation = "UiName" });
            }

            _gauges[i] = UiGauge.Create($"ProSkill{skill}", ProEffects.Name(skill), 0.0, "");
            _effects[i] = new Label { Name = $"ProEffect{skill}", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            var block = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            block.AddThemeConstantOverride("separation", 2);
            block.AddChild(_gauges[i]);
            block.AddChild(_effects[i]);
            list.AddChild(block);
            _seen[i] = _skills.Level(skill);
        }

        var training = new Button { Name = "ProTraining", Text = "Partie d'entraînement", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        training.Pressed += () => _main.World.StartProRound();
        content.AddChild(training);
        main.Panels.Add(_panel);
    }

    public void Toggle()
    {
        UiTheme.FitList(_scroll, 600f);
        _main.Panels.Toggle(_panel);
        Refresh();
    }

    public override void _Process(double delta)
    {
        for (var i = 0; i < Skills.Length; i++)
        {
            var level = _skills.Level(Skills[i]);
            if (level == _seen[i])
            {
                continue;
            }

            var gained = level > _seen[i];
            _seen[i] = level;
            if (gained)
            {
                _main.Birdie.Post(Birdie.Pro, ProEffects.LevelMessage(Skills[i], level), "#progrès", _main.World.League?.Members.FirstOrDefault(m => m.IsPro)?.Id ?? -1);
            }

            Refresh();
        }
    }

    public void Refresh()
    {
        if (!_panel.Visible)
        {
            return;
        }

        var year = _main.World.PlayYear;
        _level.Text = $"Niveau de jeu : handicap {LeaguePanel.Decimal(_skills.OverallHandicap)}";
        for (var i = 0; i < Skills.Length; i++)
        {
            var s = Skills[i];
            var level = _skills.Level(s);
            _gauges[i].SetValue(_skills.LevelProgress(s), $"niv. {level} · {_skills.Experience(s) / 100} XP");
            _effects[i].Text = $"{ProEffects.Line(s, level)} · entraînement {_skills.TrainingThisYear(s, year) / 100}/{_skills.TrainingCap / 100}";
        }
    }
}
