using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Pastille très arrondie de 34 px (étage bas de la barre) : icône facultative et texte.</summary>
public partial class UiPill : PanelContainer
{
    public Label Label { get; private set; } = null!;

    public static UiPill Create(string id, string text, string? icon = null, Color? iconColor = null)
    {
        var pill = new UiPill { Name = id, ThemeTypeVariation = "UiPill", CustomMinimumSize = new Vector2(0f, UiTheme.PillHeight), MouseFilter = MouseFilterEnum.Stop };
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 8);
        pill.AddChild(row);
        if (icon is not null)
        {
            row.AddChild(new TextureRect
            {
                Texture = UiTheme.Icon(icon),
                CustomMinimumSize = new Vector2(18f, 18f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SelfModulate = iconColor ?? UiTheme.Sky,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            });
        }

        pill.Label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(pill.Label);
        return pill;
    }

    /// <summary>Change le texte seulement s'il a changé : pas de redessin inutile.</summary>
    public void SetText(string text)
    {
        if (Label.Text != text)
        {
            Label.Text = text;
        }
    }
}
