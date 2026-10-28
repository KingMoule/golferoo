using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Panneau en verre dépoli : en-tête (icône ronde jaune, titre, croix ronde) et contenu. La croix émet <see cref="ClosedEventHandler"/>.</summary>
public partial class UiPanel : PanelContainer
{
    [Signal]
    public delegate void ClosedEventHandler();

    public Label Title { get; private set; } = null!;
    public Button CloseButton { get; private set; } = null!;

    /// <summary>Le contenu du panneau, à remplir par l'appelant.</summary>
    public VBoxContainer Content { get; private set; } = null!;

    public static UiPanel Create(string id, string title, string icon, float width)
    {
        var panel = new UiPanel { Name = id, ThemeTypeVariation = "UiPanel", CustomMinimumSize = new Vector2(width, 0f) };
        panel.AddChild(UiTheme.GlassBackdrop());
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);
        panel.AddChild(body);

        var headerStyle = UiTheme.Box(new Color("F3ECD8", 0.06f), 0, new Color("F3ECD8", 0.12f), 0);
        headerStyle.CornerRadiusTopLeft = headerStyle.CornerRadiusTopRight = UiTheme.GlassRadius;
        headerStyle.BorderWidthBottom = 1;
        headerStyle.ContentMarginLeft = 14f;
        headerStyle.ContentMarginRight = headerStyle.ContentMarginTop = headerStyle.ContentMarginBottom = 10f;
        var header = new PanelContainer { Name = "Header" };
        header.AddThemeStyleboxOverride("panel", headerStyle);
        body.AddChild(header);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        header.AddChild(row);
        row.AddChild(Badge(icon));
        panel.Title = new Label { Text = title, ThemeTypeVariation = "UiTitle", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(panel.Title);
        panel.CloseButton = new Button
        {
            Name = "Close",
            ThemeTypeVariation = "UiRoundButton",
            Icon = UiTheme.Icon("cross"),
            IconAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(UiTheme.HeaderButtonSize, UiTheme.HeaderButtonSize),
            FocusMode = FocusModeEnum.None,
            TooltipText = "Fermer",
        };
        panel.CloseButton.AddThemeConstantOverride("icon_max_width", 14);
        panel.CloseButton.Pressed += () => panel.EmitSignal(SignalName.Closed);
        row.AddChild(panel.CloseButton);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        body.AddChild(margin);
        panel.Content = new VBoxContainer();
        panel.Content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(panel.Content);
        return panel;
    }

    /// <summary>Pastille ronde jaune cernée d'encre, l'icône en encre au centre.</summary>
    public static Control Badge(string icon, int size = UiTheme.HeaderButtonSize)
    {
        var badge = new Panel { CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        badge.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Flag, size / 2, UiTheme.Ink, 2));
        var image = new TextureRect
        {
            Texture = UiTheme.Icon(icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SelfModulate = UiTheme.Ink,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        image.SetAnchorsPreset(LayoutPreset.FullRect);
        image.OffsetLeft = image.OffsetTop = size * 0.22f;
        image.OffsetRight = image.OffsetBottom = -size * 0.22f;
        badge.AddChild(image);
        return badge;
    }
}
