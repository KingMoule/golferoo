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
        (ProSkill.LongGame, "Long jeu"),
        (ProSkill.Irons, "Fers"),
        (ProSkill.ShortGame, "Petit jeu"),
        (ProSkill.Putting, "Putting"),
        (ProSkill.Distance, "Distance"),
        (ProSkill.Precision, "Précision"),
        (ProSkill.Draw, "Draw"),
        (ProSkill.Fade, "Fade"),
        (ProSkill.Chip, "Chip"),
    ];

    private Main _main = null!;
    private ProSkills _skills = null!;
    private UiPanel _panel = null!;
    private Label _level = null!;
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
            Text = $"Générales : chaque coup compte (ligue ×2, entraînement {ProSkills.TrainingCapPerYear} points par an au plus).",
            ThemeTypeVariation = "UiCaption",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        for (var i = 0; i < Skills.Length; i++)
        {
            if (i == 4)
            {
                content.AddChild(new Label { Text = "Spécialisées : coups réussis.", ThemeTypeVariation = "UiCaption" });
            }

            _gauges[i] = UiGauge.Create($"ProSkill{Skills[i].Skill}", Skills[i].Name, 0.0, "");
            content.AddChild(_gauges[i]);
            _seen[i] = Whole(Skills[i].Skill);
        }

        var training = new Button { Name = "ProTraining", Text = "Partie d'entraînement", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        training.Pressed += () => _main.World.StartProRound();
        content.AddChild(training);
        main.Panels.Add(_panel);
    }

    public void Toggle()
    {
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
                _main.League?.Notify($"{Skills[i].Name} : handicap {whole}");
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
            _gauges[i].SetValue(_skills.Progress(s), $"{LeaguePanel.Decimal(_skills.Handicap(s))} · {_skills.Experience(s)} pts · entraînement {_skills.TrainingThisYear(s, year)}/{ProSkills.TrainingCapPerYear}");
        }
    }

    /// <summary>Handicap arrondi au point entier supérieur : l'avis tombe quand il baisse.</summary>
    private int Whole(ProSkill skill) => (int)Math.Ceiling(_skills.Handicap(skill) - 1e-9);
}
