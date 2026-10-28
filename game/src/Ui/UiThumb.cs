using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Vignette d'un tiroir : image (ou aplat de couleur dessiné), pastille de prix en coin, nom ; anneau jaune quand elle est choisie.
/// </summary>
public partial class UiThumb : Button
{
    private const int Border = 3;

    public string Description { get; set; } = "";
    public (string Label, string Value)[] Costs { get; set; } = [];
    public Label Price { get; private set; } = null!;

    /// <summary>Vignette dessinée : aplat <paramref name="fill"/>, et au centre une motte <paramref name="top"/> cernée de <paramref name="edge"/>.</summary>
    public static UiThumb Create(string id, string name, Color fill, Color top, Color edge, string price = "—") =>
        Build(id, name, price, fill, art =>
        {
            var blob = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            blob.AddThemeStyleboxOverride("panel", UiTheme.Box(top, 14, edge, 3));
            blob.SetAnchorsPreset(LayoutPreset.BottomWide);
            (blob.OffsetLeft, blob.OffsetRight, blob.OffsetTop, blob.OffsetBottom) = (14f, -14f, -46f, -12f);
            art.AddChild(blob);
        });

    /// <summary>Vignette à image (rendu d'un modèle, par exemple), recadrée dans les coins arrondis.</summary>
    public static UiThumb Create(string id, string name, Texture2D image, string price = "—") =>
        Build(id, name, price, UiTheme.Ink, art =>
        {
            art.ClipChildren = ClipChildrenMode.Only;
            var picture = new TextureRect
            {
                Texture = image,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            picture.SetAnchorsPreset(LayoutPreset.FullRect);
            art.AddChild(picture);
        });

    private static UiThumb Build(string id, string name, string price, Color fill, System.Action<Panel> drawArt)
    {
        var thumb = new UiThumb
        {
            Name = id,
            ThemeTypeVariation = "UiThumb",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(UiTheme.ThumbWidth, UiTheme.ThumbArtHeight + 34),
            TooltipText = name,
        };
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        (column.OffsetLeft, column.OffsetTop, column.OffsetRight, column.OffsetBottom) = (Border, Border, -Border, -Border);
        thumb.AddChild(column);

        var artStyle = UiTheme.Box(fill, 0);
        artStyle.CornerRadiusTopLeft = artStyle.CornerRadiusTopRight = UiTheme.ToolRadius - Border;
        var art = new Panel { CustomMinimumSize = new Vector2(0f, UiTheme.ThumbArtHeight - Border), MouseFilter = MouseFilterEnum.Ignore };
        art.AddThemeStyleboxOverride("panel", artStyle);
        column.AddChild(art);
        drawArt(art);

        thumb.Price = new Label { Text = price, ThemeTypeVariation = "UiPrice", MouseFilter = MouseFilterEnum.Ignore };
        thumb.Price.SetAnchorsPreset(LayoutPreset.TopRight);
        thumb.Price.GrowHorizontal = GrowDirection.Begin;
        (thumb.Price.OffsetRight, thumb.Price.OffsetTop) = (-6f, 6f);
        art.AddChild(thumb.Price);

        column.AddChild(new Label { Text = name, ThemeTypeVariation = "UiName", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore });
        return thumb;
    }

    public bool Chosen
    {
        get => ButtonPressed;
        set => SetPressedNoSignal(value);
    }

    public override GodotObject _MakeCustomTooltip(string forText) => UiTooltip.Create(forText, Description, Costs);
}
