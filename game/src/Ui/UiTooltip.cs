using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Contenu d'une info-bulle : titre, une phrase, lignes de coût. Le cadre vient du style <c>TooltipPanel</c> du thème.</summary>
public partial class UiTooltip : VBoxContainer
{
    public const float TextWidth = 232f;

    public static UiTooltip Create(string title, string text, params (string Label, string Value)[] costs)
    {
        var tip = new UiTooltip { MouseFilter = MouseFilterEnum.Ignore };
        tip.AddThemeConstantOverride("separation", 6);
        var name = new Label { Text = title, ThemeTypeVariation = "UiName" };
        name.AddThemeFontSizeOverride("font_size", 17);
        tip.AddChild(name);
        if (text.Length > 0)
        {
            var line = new Label { Text = text, ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(TextWidth, 0f) };
            line.AddThemeFontSizeOverride("font_size", 14);
            tip.AddChild(line);
        }

        foreach (var (label, value) in costs)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label, ThemeTypeVariation = "UiKey", SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var amount = new Label { Text = value, ThemeTypeVariation = "UiValue", HorizontalAlignment = HorizontalAlignment.Right };
            amount.AddThemeColorOverride("font_color", UiTheme.Brick);
            row.AddChild(amount);
            tip.AddChild(row);
        }

        return tip;
    }

    /// <summary>L'info-bulle dans son cadre, pour la montrer hors survol (écran témoin, tutoriel).</summary>
    public static PanelContainer Framed(UiTooltip tip)
    {
        var frame = new PanelContainer { ThemeTypeVariation = "TooltipPanel", MouseFilter = MouseFilterEnum.Ignore };
        frame.AddChild(tip);
        return frame;
    }
}
