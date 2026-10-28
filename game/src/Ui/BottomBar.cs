using Godot;
using Golferoo.Game.Cameras;
using Golferoo.Sim.Weather;

namespace Golferoo.Game.Ui;

/// <summary>
/// La barre du bas (maquette v2), d'où part tout outil : étage haut, les outils par familles et les boutons ronds des panneaux et de la caméra ;
/// étage bas, l'état du club en pastilles ; à gauche, l'écusson du club. Le <see cref="ToolDrawer"/> monte au-dessus.
/// Les boutons n'appellent que les méthodes publiques des outils et de <see cref="Main"/> ; <see cref="Sync"/> recopie l'état
/// au changement (après chaque bouton) ou à 4 Hz (touches, temps qui passe) : aucune chaîne reconstruite à chaque image.
/// </summary>
public partial class BottomBar : Control
{
    public const float Height = TopHeight + BottomHeight;
    private const float TopHeight = 64f;
    private const float BottomHeight = 48f;
    private const float CrestGutter = 128f;
    private const double RefreshSeconds = 0.25;

    // Heures affichées : la journée de jeu va de l'ouverture (7 h) à la fermeture (19 h). Convention d'affichage, sans état de sim.
    private const double OpenHour = 7.0;
    private const double DayHours = 12.0;

    private readonly Dictionary<ToolFamily, UiToolButton> _families = [];
    private readonly Dictionary<int, Button> _speeds = [];
    private Main _main = null!;
    private UiToolButton _turf = null!;
    private UiToolButton _views = null!;
    private Button _league = null!;
    private Button _pro = null!;
    private Button _camera = null!;
    private Button _pause = null!;
    private Panel _clockFill = null!;
    private Label _clockTime = null!;
    private Label _clockDate = null!;
    private UiPill _weather = null!;
    private UiPill _members = null!;
    private double _sinceRefresh;

    public ToolDrawer Drawer { get; private set; } = null!;

    /// <summary>La famille d'outils prise, déduite des outils eux-mêmes (une touche peut l'avoir changée).</summary>
    public ToolFamily Current =>
        _main.PlaceTool.Active ? ToolFamily.Holes
        : _main.TreeTool.Active ? ToolFamily.Trees
        : !_main.Brush.Active ? ToolFamily.None
        : _main.Brush.BulldozerMode ? ToolFamily.Bulldozer
        : _main.Brush.ReliefMode ? ToolFamily.Relief
        : ToolFamily.Terrain;

    /// <summary>Construit la barre et son tiroir sous <paramref name="root"/>.</summary>
    public static BottomBar Create(Main main, UiRoot root, ThumbnailRenderer thumbnails)
    {
        var bar = new BottomBar { Name = UiIds.Hud.Bar, _main = main, MouseFilter = MouseFilterEnum.Ignore };
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        bar.OffsetTop = -Height;

        // Le tiroir : centré au-dessus de la barre ; la colonne qui le porte laisse passer la souris partout ailleurs.
        var dock = new VBoxContainer { Name = "DrawerDock", Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        dock.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dock.OffsetBottom = -Height - 12f;
        bar.Drawer = ToolDrawer.Create(main, bar, thumbnails);
        bar.Drawer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        dock.AddChild(bar.Drawer);
        root.AddChild(dock);

        bar.AddChild(bar.TopTier());
        bar.AddChild(bar.BottomTier());
        bar.AddChild(Crest(main));
        root.AddChild(bar);
        bar.Sync();
        return bar;
    }

    /// <summary>Prend la famille <paramref name="family"/> et éteint les autres outils ; un second clic sur la famille prise la repose si <paramref name="toggle"/>.</summary>
    public void Select(ToolFamily family, bool toggle = true)
    {
        if (toggle && family == Current)
        {
            family = ToolFamily.None;
        }

        var brush = _main.Brush;
        brush.SetActive(family is ToolFamily.Terrain or ToolFamily.Relief or ToolFamily.Bulldozer);
        switch (family)
        {
            case ToolFamily.Terrain:
                brush.SelectSurface(brush.PaintedSurface);
                break;
            case ToolFamily.Relief:
                brush.SelectRelief();
                break;
            case ToolFamily.Bulldozer:
                brush.SelectBulldozer();
                break;
            default:
                break;
        }

        _main.TreeTool.SetActive(family == ToolFamily.Trees);
        _main.PlaceTool.SetActive(family == ToolFamily.Holes);
        Sync();
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            Sync();
        }
    }

    /// <summary>Recopie l'état du jeu dans la barre et le tiroir ; les textes ne changent que s'ils ont changé.</summary>
    public void Sync()
    {
        _sinceRefresh = 0;
        var current = Current;
        foreach (var (family, button) in _families)
        {
            button.Chosen = family == current;
        }

        Drawer.Sync(current);
        _turf.Chosen = _main.Turf.IsOpen;
        _views.Chosen = _main.TurfOverlay;
        _league.SetPressedNoSignal(_main.League?.IsOpen == true);
        _pro.SetPressedNoSignal(_main.ProPanel?.IsOpen == true);
        _camera.SetPressedNoSignal(_main.Camera.Mode == CameraMode.Free);
        _pause.SetPressedNoSignal(_main.Paused);
        foreach (var (speed, button) in _speeds)
        {
            button.SetPressedNoSignal(!_main.Paused && speed == _main.Speed);
        }

        var world = _main.World;
        var date = world.Date;
        var hours = OpenHour + (Math.Clamp(date.DayFraction, 0.0, 1.0) * DayHours);
        SetText(_clockTime, $"{(int)hours:00}:{(int)((hours - Math.Floor(hours)) * 60):00}");
        SetText(_clockDate, $"{date.Day} {UiNames.Months[date.Month - 1]}, an {date.Year}");
        _clockFill.Size = new Vector2(Mathf.Max(18f, _clockFill.GetParentControl().Size.X * (float)date.DayFraction), UiTheme.PillHeight);
        _weather.SetText($"{UiNames.Capitalized(UiNames.Skies[(int)world.Weather.Sky])} · — °C · {UiNames.Capitalized(UiNames.Seasons[(int)date.Season])} · demain : {UiNames.Skies[(int)world.Forecast(1)]}");
        _members.SetText(world.League is { } league ? league.Members.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—");
    }

    private static void SetText(Label label, string text)
    {
        if (label.Text != text)
        {
            label.Text = text;
        }
    }

    /// <summary>Fond de verre pleine largeur d'un étage, filet clair en haut.</summary>
    private static PanelContainer Tier(string name, Color tint, float top, float height, Control content)
    {
        // Marges nulles : le fond flou couvre tout l'étage ; la marge de l'écusson est dans le contenu.
        var style = UiTheme.Box(tint, 0, UiTheme.Rim, 0);
        style.BorderWidthTop = 1;
        var tier = new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Stop };
        tier.AddThemeStyleboxOverride("panel", style);
        tier.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        (tier.OffsetTop, tier.OffsetBottom) = (top, top + height);
        tier.AddChild(UiTheme.GlassBackdrop(0));
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)CrestGutter);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddChild(content);
        tier.AddChild(margin);
        return tier;
    }

    private PanelContainer TopTier()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var tools = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        tools.AddThemeConstantOverride("separation", 0);
        row.AddChild(tools);

        var build = Group(tools, first: true);
        build.AddChild(Family(ToolFamily.Terrain, UiIds.Tool.Terrain, "toolBrush", "Terrain", "Peindre les surfaces du parcours : départs, fairways, greens, bunkers, eau."));
        build.AddChild(Family(ToolFamily.Relief, UiIds.Tool.Relief, "relief", "Relief", "Monter et creuser le sol."));
        build.AddChild(Family(ToolFamily.Trees, UiIds.Tool.Trees, "trees", "Arbres", "Planter, sélectionner, raser, dégager."));
        build.AddChild(Family(ToolFamily.Holes, UiIds.Tool.Holes, "flag", "Trous", "Poser départs et drapeaux, ajouter ou retirer un trou."));

        var club = Group(tools);
        foreach (var (id, icon, title) in new[] { (UiIds.Tool.Buildings, "building", "Bâtiments"), (UiIds.Tool.Amenities, "bench", "Aménagements") })
        {
            var later = UiToolButton.Create(id, icon, title, "Bientôt.");
            later.Disabled = true;
            club.AddChild(later);
        }

        var care = Group(tools);
        _turf = UiToolButton.Create(UiIds.Tool.Turf, "wrench", "Gazon", "Consignes d'entretien : vitesse des greens, fermeté des fairways, hauteur du rough.");
        _turf.Pressed += () => Run(_main.Turf.Toggle);
        care.AddChild(_turf);
        _views = UiToolButton.Create(UiIds.Tool.Views, "layers", "Vues", "Montrer l'état du gazon : usure et repousse.");
        _views.Pressed += () => Run(_main.ToggleTurfOverlay);
        care.AddChild(_views);

        var bulldozer = Family(ToolFamily.Bulldozer, UiIds.Tool.Bulldozer, "bulldozer", "Bulldozer", "Efface ce qui a été posé sous le pinceau : la surface revient au rough, les arbres sont rasés.");
        bulldozer.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(UiTheme.Brick, 0.18f), UiTheme.ToolRadius));
        bulldozer.AddThemeColorOverride("icon_normal_color", UiTheme.BrickLight);
        Group(tools).AddChild(bulldozer);

        var round = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        round.AddThemeConstantOverride("separation", 8);
        row.AddChild(round);
        _league = Round(round, UiIds.Round.League, "trophy", "Ligue", () => _main.League?.Toggle(), enabled: _main.League is not null);
        _pro = Round(round, UiIds.Round.Pro, "figurine", "Votre pro", () => _main.ProPanel?.Toggle(), enabled: _main.ProPanel is not null);
        Round(round, UiIds.Round.Finances, "coin", "Finances : bientôt", null, enabled: false);
        Round(round, UiIds.Round.Birdie, "birdie", "Birdie : bientôt", null, enabled: false);
        _camera = Round(round, UiIds.Round.Camera, "video", "Caméra libre (clic du milieu glissé pour tourner et incliner)", () => _main.Camera.ToggleMode(), enabled: true);
        return Tier("BarTop", UiTheme.BarTop, 0f, TopHeight, row);
    }

    private PanelContainer BottomTier()
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);

        var speed = new PanelContainer { ThemeTypeVariation = "UiPill", CustomMinimumSize = new Vector2(0f, UiTheme.PillHeight), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        speed.AddThemeStyleboxOverride("panel", PillStyle(6f));
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 2);
        speed.AddChild(buttons);
        _pause = PillButton(UiIds.Hud.Pause, "", "Pause", () => _main.Paused = !_main.Paused);
        _pause.Icon = UiTheme.Icon("pause");
        buttons.AddChild(_pause);
        foreach (var s in Main.Speeds)
        {
            var button = PillButton(UiIds.Hud.Speed(s), $"×{s}", $"Vitesse ×{s}", () => _main.SetSpeed(s));
            _speeds[s] = button;
            buttons.AddChild(button);
        }

        row.AddChild(speed);
        row.AddChild(Clock());
        _weather = UiPill.Create(UiIds.Hud.Weather, "", "cloud", UiTheme.CreamIcon);
        _weather.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_weather);

        var name = _main.World.Terrain!.Name;
        var club = UiPill.Create(UiIds.Hud.Club, name.StartsWith("Golf", StringComparison.Ordinal) ? name : "Golf " + name);
        club.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        club.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        club.Label.ThemeTypeVariation = "UiName";
        club.Label.AddThemeFontSizeOverride("font_size", 16);
        club.Label.ClipText = true;
        club.Label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        club.Label.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(club);

        _members = UiPill.Create(UiIds.Hud.Members, "—", "figurine", UiTheme.CreamIcon);
        _members.TooltipText = "Membres de la ligue du club";
        row.AddChild(_members);
        var money = UiPill.Create(UiIds.Hud.Money, "—", "coin", UiTheme.Grass);
        money.TooltipText = "Argent : bientôt";
        row.AddChild(money);
        var mood = UiPill.Create(UiIds.Hud.Mood, "—");
        mood.Visible = false; // C16 : visages d'humeur.
        row.AddChild(mood);
        foreach (var pill in new Control[] { _members, money })
        {
            pill.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        return Tier("BarBottom", UiTheme.BarBottom, TopHeight, BottomHeight, row);
    }

    /// <summary>Heure et date sur l'avancée du jour (aplat bleu ciel de gauche à droite).</summary>
    private Control Clock()
    {
        var pill = new Panel { Name = UiIds.Hud.Clock, CustomMinimumSize = new Vector2(196f, UiTheme.PillHeight), SizeFlagsVertical = SizeFlags.ShrinkCenter, ClipContents = true, TooltipText = "Heure et date ; l'aplat montre l'avancée du jour" };
        pill.AddThemeStyleboxOverride("panel", PillStyle(0f));
        var fillStyle = UiTheme.Box(new Color(UiTheme.Sky, 0.22f), 0);
        fillStyle.CornerRadiusTopLeft = fillStyle.CornerRadiusBottomLeft = UiTheme.PillHeight / 2;
        _clockFill = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _clockFill.AddThemeStyleboxOverride("panel", fillStyle);
        pill.AddChild(_clockFill);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _clockTime = new Label { ThemeTypeVariation = "UiValue", VerticalAlignment = VerticalAlignment.Center };
        _clockTime.AddThemeColorOverride("font_color", UiTheme.Sky);
        _clockDate = new Label { VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_clockTime);
        row.AddChild(_clockDate);
        pill.AddChild(row);
        return pill;
    }

    private static StyleBoxFlat PillStyle(float padding)
    {
        var style = UiTheme.Box(UiTheme.Pill, 999);
        style.ContentMarginLeft = style.ContentMarginRight = padding;
        return style;
    }

    /// <summary>Un groupe d'outils, séparé du précédent par un filet clair.</summary>
    private static HBoxContainer Group(HBoxContainer tools, bool first = false)
    {
        if (!first)
        {
            tools.AddChild(new ColorRect { Color = new Color("F3ECD8", 0.18f), CustomMinimumSize = new Vector2(1f, 36f), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        }

        var group = new HBoxContainer();
        group.AddThemeConstantOverride("separation", 4);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddChild(group);
        tools.AddChild(margin);
        return group;
    }

    private UiToolButton Family(ToolFamily family, string id, string icon, string title, string description)
    {
        var button = UiToolButton.Create(id, icon, title, description);
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.Pressed += () => Select(family);
        _families[family] = button;
        return button;
    }

    private Button Round(HBoxContainer row, string id, string icon, string tip, System.Action? pressed, bool enabled)
    {
        var button = new Button
        {
            Name = id,
            ThemeTypeVariation = "UiRoundButton",
            Icon = UiTheme.Icon(icon),
            IconAlignment = HorizontalAlignment.Center,
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(UiTheme.RoundSize, UiTheme.RoundSize),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = tip,
            Disabled = !enabled,
        };
        button.AddThemeConstantOverride("icon_max_width", 22);
        if (pressed is not null)
        {
            button.Pressed += () => Run(pressed);
        }

        row.AddChild(button);
        return button;
    }

    private Button PillButton(string id, string text, string tip, System.Action pressed)
    {
        var button = new Button { Name = id, Text = text, ThemeTypeVariation = "UiPillButton", ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = tip, CustomMinimumSize = new Vector2(28f, 26f) };
        button.Pressed += () => Run(pressed);
        return button;
    }

    private void Run(System.Action action)
    {
        action();
        Sync();
    }

    /// <summary>L'écusson du club (SVG dans l'esprit SimGolf), qui déborde au-dessus de la barre ; l'objectif du club viendra dessous (C18).</summary>
    private static TextureRect Crest(Main main)
    {
        var crest = new TextureRect
        {
            Name = UiIds.Hud.Crest,
            Texture = GD.Load<Texture2D>("res://assets/ui/crest.svg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = main.World.Terrain!.Name,
        };
        crest.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        (crest.OffsetLeft, crest.OffsetRight, crest.OffsetTop, crest.OffsetBottom) = (14f, 14f + 104f, -132f, -8f);
        return crest;
    }
}
