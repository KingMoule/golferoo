using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim.Pro;

namespace Golferoo.Game.Tools;

/// <summary>
/// Fiche de votre pro (bouton Votre pro, C25) : les 17 habiletés en quatre groupes, une ligne compacte chacune (nom, niveau, mini-barre
/// d'XP du niveau, effet actuel) ; l'effet du niveau suivant et l'XP sont dans l'info-bulle de la ligne. Un bouton « Partie d'entraînement »
/// et un avis Birdie à chaque niveau gagné. Aucune règle ici : noms, groupes et effets viennent de <see cref="ProEffects"/>.
/// </summary>
public partial class ProPanel : Node
{
    private static readonly ProSkill[] Skills = [.. Enum.GetValues<ProSkill>().OrderBy(s => (int)ProEffects.GroupOf(s)).ThenBy(s => (int)s)];

    /// <summary>Hauteur de la liste en écran bas (720 px logiques : 150 %) : huit lignes et deux en-têtes au moins, sans dépasser sous le tiroir Terrain.</summary>
    private const float ShortList = 270f;

    private Main _main = null!;
    private ProSkills _skills = null!;
    private UiPanel _panel = null!;
    private Label _level = null!;
    private ScrollContainer _scroll = null!;
    private readonly Control[] _rows = new Control[Skills.Length];
    private readonly Label[] _levels = new Label[Skills.Length];
    private readonly ProgressBar[] _bars = new ProgressBar[Skills.Length];
    private readonly Label[] _effects = new Label[Skills.Length];
    private readonly int[] _seen = new int[Skills.Length];

    public bool IsOpen => _panel.Visible;

    /// <summary>Nombre de lignes d'habileté entièrement visibles dans la liste défilante (captures).</summary>
    public int VisibleRows()
    {
        var area = _scroll.GetGlobalRect().Grow(1f);
        return _rows.Count(r => r.IsVisibleInTree() && area.Encloses(r.GetGlobalRect()));
    }

    public void Init(Main main)
    {
        _main = main;
        _skills = main.World.Pro!;
        _panel = UiPanel.Create(UiIds.Panel.Pro, "Votre pro", "figurine", 380f);
        var content = _panel.Content;
        var top = new HBoxContainer();
        _level = new Label
        {
            ThemeTypeVariation = "UiName",
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = $"Chaque coup nourrit les habiletés sollicitées selon sa qualité (ligue ×2, entraînement {_skills.TrainingCap / 100} XP par an au plus).",
        };
        top.AddChild(_level);
        var training = new Button { Name = "ProTraining", Text = "Partie d'entraînement", FocusMode = Control.FocusModeEnum.None };
        training.Pressed += () => _main.World.StartProRound();
        top.AddChild(training);
        content.AddChild(top);

        // La liste défile quand l'écran logique est bas (150 % : 720 px), pour que le panneau ne passe pas sous la barre.
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scroll.AddChild(list);
        content.AddChild(_scroll);
        for (var i = 0; i < Skills.Length; i++)
        {
            var skill = Skills[i];
            if (i == 0 || ProEffects.GroupOf(skill) != ProEffects.GroupOf(Skills[i - 1]))
            {
                list.AddChild(new Label { Text = ProEffects.GroupName(ProEffects.GroupOf(skill)), ThemeTypeVariation = "UiCaption" });
            }

            var row = new HBoxContainer { Name = $"ProSkill{skill}", MouseFilter = Control.MouseFilterEnum.Stop, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new Label { Text = ProEffects.Name(skill), ThemeTypeVariation = "UiKey", CustomMinimumSize = new Vector2(112f, 0f), ClipText = true });
            _levels[i] = new Label { ThemeTypeVariation = "UiValue", CustomMinimumSize = new Vector2(46f, 0f), HorizontalAlignment = HorizontalAlignment.Right };
            row.AddChild(_levels[i]);
            _bars[i] = new ProgressBar
            {
                MinValue = 0.0,
                MaxValue = 1.0,
                Step = 0.0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(40f, 6f),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddChild(_bars[i]);
            _effects[i] = new Label
            {
                Name = $"ProEffect{skill}",
                ThemeTypeVariation = "UiCaption",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                ClipText = true,
            };
            row.AddChild(_effects[i]);
            list.AddChild(row);
            _rows[i] = row;
            _seen[i] = _skills.Level(skill);
        }

        main.Panels.Add(_panel);
    }

    public void Toggle()
    {
        UiTheme.FitList(_scroll, 600f);
        _scroll.CustomMinimumSize = new Vector2(0f, _scroll.GetViewportRect().Size.Y < 900f ? ShortList : 520f);
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
        _level.Text = $"Handicap {LeaguePanel.Decimal(_skills.OverallHandicap)}";
        for (var i = 0; i < Skills.Length; i++)
        {
            var s = Skills[i];
            var level = _skills.Level(s);
            _levels[i].Text = $"niv. {level}";
            _bars[i].Value = _skills.LevelProgress(s);
            _effects[i].Text = ProEffects.Compact(s, level);
            _rows[i].TooltipText = $"{ProEffects.Name(s)} · {ProEffects.Line(s, level)}\n{_skills.Experience(s) / 100} XP · entraînement {_skills.TrainingThisYear(s, year) / 100}/{_skills.TrainingCap / 100}";
        }
    }
}
