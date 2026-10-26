using Godot;
using Golferoo.Sim.Pro;

namespace Golferoo.Game.Tools;

/// <summary>
/// Fiche de votre pro (touche O) : chaque compétence avec son handicap et sa barre de progrès, les points d'entraînement de
/// l'année, et un bouton « Partie d'entraînement ». Un avis s'affiche quand une compétence gagne un point de handicap entier.
/// </summary>
public partial class ProPanel : CanvasLayer
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
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private readonly Label[] _labels = new Label[Skills.Length];
    private readonly ProgressBar[] _bars = new ProgressBar[Skills.Length];
    private readonly int[] _seen = new int[Skills.Length];

    public bool IsOpen => _panel.Visible;

    public void Init(Main main)
    {
        _main = main;
        _skills = main.World.Pro!;
        _panel = new PanelContainer { Name = "ProPanel", Visible = false, OffsetLeft = 8f, OffsetTop = 90f };
        var box = new VBoxContainer();
        _title = new Label();
        box.AddChild(_title);
        var grid = new GridContainer { Columns = 2 };
        for (var i = 0; i < Skills.Length; i++)
        {
            if (i == 4)
            {
                grid.AddChild(new Label { Text = "Spécialisées (coups réussis)" });
                grid.AddChild(new Control());
            }

            _labels[i] = new Label { CustomMinimumSize = new Vector2(250f, 0f) };
            _bars[i] = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(140f, 14f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            grid.AddChild(_labels[i]);
            grid.AddChild(_bars[i]);
            _seen[i] = Whole(Skills[i].Skill);
        }

        box.AddChild(grid);
        var training = new Button { Text = "Partie d'entraînement" };
        training.Pressed += () => _main.World.StartProRound();
        box.AddChild(training);
        _panel.AddChild(box);
        AddChild(_panel);
    }

    public void Toggle()
    {
        _panel.Visible = !_panel.Visible;
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
        _title.Text = $"Votre pro : niveau de jeu {LeaguePanel.Decimal(_skills.OverallHandicap)}\nGénérales (chaque coup ; ligue ×2, entraînement {ProSkills.TrainingCapPerYear} points par an au plus)";
        for (var i = 0; i < Skills.Length; i++)
        {
            var s = Skills[i].Skill;
            _labels[i].Text = $"{Skills[i].Name} : handicap {LeaguePanel.Decimal(_skills.Handicap(s))} ({_skills.Experience(s)} pts, entraînement {_skills.TrainingThisYear(s, year)}/{ProSkills.TrainingCapPerYear})";
            _bars[i].Value = _skills.Progress(s) * 100.0;
        }
    }

    /// <summary>Handicap arrondi au point entier supérieur : l'avis tombe quand il baisse.</summary>
    private int Whole(ProSkill skill) => (int)Math.Ceiling(_skills.Handicap(skill) - 1e-9);
}
