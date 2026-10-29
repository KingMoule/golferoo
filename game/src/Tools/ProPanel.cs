using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim.Pro;

namespace Golferoo.Game.Tools;

/// <summary>
/// Fiche de votre pro (bouton Votre pro) : chaque compétence en jauge (handicap, points, entraînement de l'année, progrès vers le point
/// suivant) et un bouton « Partie d'entraînement ». Un avis s'affiche quand une compétence gagne un point de handicap entier.
/// </summary>
public partial class ProPanel : Node
{
    private static readonly (ProSkill Skill, string Name)[] Skills =
    [
        (ProSkill.Driver, "Driver"),
        (ProSkill.Wood, "Bois"),
        (ProSkill.LongIrons, "Fers longs"),
        (ProSkill.MidIrons, "Fers moyens"),
        (ProSkill.ShortIrons, "Fers courts"),
        (ProSkill.Wedge, "Wedge"),
        (ProSkill.Putter, "Putter"),
        (ProSkill.Precision, "Précision"),
        (ProSkill.Dosage, "Dosage"),
        (ProSkill.Distance, "Distance"),
        (ProSkill.Draw, "Draw"),
        (ProSkill.Fade, "Fade"),
        (ProSkill.Chip, "Chip"),
        (ProSkill.RoughExit, "Sortie de rough"),
        (ProSkill.BunkerExit, "Sortie de bunker"),
        (ProSkill.Wind, "Jeu dans le vent"),
        (ProSkill.GreenReading, "Lecture du green"),
    ];

    private Main _main = null!;
    private ProSkills _skills = null!;
    private UiPanel _panel = null!;
    private Label _level = null!;
    private ScrollContainer _scroll = null!;
    private readonly UiGauge[] _gauges = new UiGauge[Skills.Length];
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
        list.AddThemeConstantOverride("separation", 8);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _scroll.AddChild(list);
        content.AddChild(_scroll);
        for (var i = 0; i < Skills.Length; i++)
        {
            if (i == 4)
            {
                list.AddChild(new Label { Text = "Spécialisées : coups réussis.", ThemeTypeVariation = "UiCaption" });
            }

            _gauges[i] = UiGauge.Create($"ProSkill{Skills[i].Skill}", Skills[i].Name, 0.0, "");
            list.AddChild(_gauges[i]);
            _seen[i] = Whole(Skills[i].Skill);
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
            var whole = Whole(Skills[i].Skill);
            if (whole < _seen[i])
            {
                _seen[i] = whole;
                _main.Birdie.Post(Birdie.Pro, $"Mon entraînement paie : {Skills[i].Name.ToLowerInvariant()}, niveau {-whole}.", "#progrès", _main.World.League?.Members.FirstOrDefault(m => m.IsPro)?.Id ?? -1);
                Refresh();
            }
        }
    }

    public void Refresh()
    {
        if (!_panel.Visible)
        {
            return;
        }

        var year = _main.World.Date.Year;
        _level.Text = $"Niveau de jeu : handicap {LeaguePanel.Decimal(_skills.OverallHandicap)}";
        for (var i = 0; i < Skills.Length; i++)
        {
            var s = Skills[i].Skill;
            _gauges[i].SetValue(_skills.LevelProgress(s), $"niv. {_skills.Level(s)} · {_skills.Experience(s) / 100} XP · entraînement {_skills.TrainingThisYear(s, year) / 100}/{_skills.TrainingCap / 100}");
        }
    }

    /// <summary>Moins le niveau : l'avis tombe quand elle baisse, donc quand un niveau est gagné.</summary>
    private int Whole(ProSkill skill) => -_skills.Level(skill);
}
