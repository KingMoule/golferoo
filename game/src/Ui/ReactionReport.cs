using Godot;
using Golferoo.Game.Rendering;
using Golferoo.Sim.Golfers;

namespace Golferoo.Game.Ui;

/// <summary>
/// Le rapport des réactions par trou (C16, bouton « Réactions ») : pour chaque trou ouvert, ce qui a fait réagir les golfeurs ce mois-ci et le mois précédent,
/// les mauvaises réactions d'abord (ce qu'il faut corriger), les bonnes ensuite, chacune triée par fréquence. Un bouton montre aussi la carte des réactions sur le terrain.
/// Les chiffres sont ceux de <see cref="Reactions"/>, rafraîchis à l'ouverture et quand ils changent.
/// </summary>
public partial class ReactionReport : Node
{
    public const float Width = 380f;

    private static readonly MoodEvent[] Events = Enum.GetValues<MoodEvent>();

    private Main _main = null!;
    private UiPanel _panel = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _holes = null!;
    private Button _mapButton = null!;
    private ReactionMap _map = null!;
    private string _key = string.Empty;

    public bool IsOpen => _panel.Visible;

    /// <summary>La carte des réactions est montrée sur le terrain.</summary>
    public bool MapOn => _map.Visible;

    /// <summary>Les disques de la carte.</summary>
    public int MapDiscs => _map.Discs;

    /// <summary>Les lignes du rapport affiché : un trou, bonne ou mauvaise réaction, texte, ce mois-ci, mois précédent.</summary>
    public IReadOnlyList<(int Hole, bool Good, string Text, int Now, int Before)> Rows { get; private set; } = [];

    public void Init(Main main)
    {
        _main = main;
        _panel = UiPanel.Create(UiIds.Panel.Reactions, "Réactions", "information", Width);
        _mapButton = new Button
        {
            Name = UiIds.Hud.ReactionMap,
            Text = "Montrer la carte sur le terrain",
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0f, 32f),
            TooltipText = "Un disque par réaction récente : rouge pour une contrariété, vert pour un plaisir. Là où ils s'accumulent, le parcours fait réagir.",
        };
        _mapButton.Pressed += () => SetMap(!_map.Visible);
        _panel.Content.AddChild(_mapButton);
        _panel.Content.AddChild(new Label { Text = "Rouge : à corriger. Vert : ce qui plaît. Les chiffres sont des réactions de golfeurs, ce mois-ci puis le mois précédent.", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Width - 40f, 0f) });
        _holes = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _scroll.AddChild(_holes);
        _panel.Content.AddChild(_scroll);
        main.Panels.Add(_panel);

        _map = new ReactionMap { Name = "ReactionMap", Visible = false };
        main.AddChild(_map);
    }

    public void Toggle()
    {
        _main.Panels.Toggle(_panel);
        UiTheme.FitList(_scroll);
        Refresh();
    }

    /// <summary>Montre ou cache la carte des réactions sur le terrain.</summary>
    public void SetMap(bool on)
    {
        _map.Visible = on;
        _mapButton.SetPressedNoSignal(on);
        Refresh();
    }

    /// <summary>Remet le rapport et la carte d'accord avec la sim ; sans effet sur ce qui est fermé ou inchangé.</summary>
    public void Refresh()
    {
        if (_map.Visible)
        {
            _map.Sync(_main.World);
        }

        if (!_panel.Visible)
        {
            return;
        }

        Rows = Collect();
        var key = string.Join('|', Rows.Select(r => $"{r.Hole}/{r.Good}/{r.Text}/{r.Now}/{r.Before}"));
        if (key == _key)
        {
            return;
        }

        _key = key;
        Rebuild();
    }

    /// <summary>Par trou ouvert, les mauvaises réactions puis les bonnes, chacune du plus fréquent au moins fréquent (ce mois-ci, puis le mois précédent).</summary>
    private List<(int Hole, bool Good, string Text, int Now, int Before)> Collect()
    {
        var world = _main.World;
        var rows = new List<(int, bool, string, int, int)>();
        var holes = Math.Min(world.Terrain?.Course.Count ?? 0, 18);
        for (var hole = 1; hole <= holes; hole++)
        {
            foreach (var good in new[] { false, true })
            {
                rows.AddRange(Events
                    .Where(e => (world.Moods.Definitions[(int)e].Value > 0) == good)
                    .Select(e => (Hole: hole, Good: good, Text: UiNames.Reactions[(int)e], Now: world.Reactions.Count(hole, e), Before: world.Reactions.Previous(hole, e)))
                    .Where(r => r.Now + r.Before > 0)
                    .OrderByDescending(r => r.Now).ThenByDescending(r => r.Before)
                    .Select(r => (r.Hole, r.Good, r.Text, r.Now, r.Before)));
            }
        }

        return rows;
    }

    private void Rebuild()
    {
        foreach (var child in _holes.GetChildren())
        {
            _holes.RemoveChild(child);
            child.QueueFree();
        }

        if (Rows.Count == 0)
        {
            _holes.AddChild(new Label { Text = "Aucune réaction pour l'instant : elles arrivent avec les premiers golfeurs.", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Width - 60f, 0f) });
            return;
        }

        foreach (var hole in Rows.GroupBy(r => r.Hole))
        {
            _holes.AddChild(new Label { Text = $"Trou {hole.Key}", ThemeTypeVariation = "UiName" });
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 12);
            foreach (var header in new[] { "", "Ce mois", "Précédent" })
            {
                grid.AddChild(new Label { Text = header, ThemeTypeVariation = "UiCaption", HorizontalAlignment = header.Length == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, SizeFlagsHorizontal = header.Length == 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill });
            }

            foreach (var (_, good, text, now, before) in hole)
            {
                var color = good ? UiTheme.Grass : UiTheme.Brick;
                var name = new Label { Text = $"{(good ? "+" : "−")} {UiNames.Capitalized(text)}", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(150f, 0f) };
                name.AddThemeColorOverride("font_color", color);
                grid.AddChild(name);
                grid.AddChild(Count($"×{now}", color));
                grid.AddChild(Count($"×{before}", color));
            }

            _holes.AddChild(grid);
        }
    }

    private static Label Count(string text, Color color)
    {
        var label = new Label { Text = text, ThemeTypeVariation = "UiValue", HorizontalAlignment = HorizontalAlignment.Right };
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
