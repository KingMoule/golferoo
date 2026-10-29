using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Palette, tailles et polices de <c>docs/design-interface.md</c> (qui fait foi), et le <see cref="Godot.Theme"/> construit une fois en code.
/// Tout élément d'interface descend de <see cref="UiRoot"/>, qui porte ce thème : styles et polices s'héritent.
/// </summary>
public static class UiTheme
{
    // Palette.
    public static readonly Color Glass = new("182823", 0.8f);
    public static readonly Color BarTop = new("243A32", 0.74f);
    public static readonly Color BarBottom = new("14221D", 0.9f);
    public static readonly Color Pill = new(0f, 0f, 0f, 0.38f);
    public static readonly Color Rim = new("F3ECD8", 0.2f);
    public static readonly Color Ink = new("1D2A24");
    public static readonly Color Cream = new("FFF8E6");
    public static readonly Color CreamIcon = new("E9E2CC");
    public static readonly Color CreamDim = new("C9C1AA");
    public static readonly Color Flag = new("F4C542");
    public static readonly Color Sky = new("9FD3F5");
    public static readonly Color SkyDeep = new("7CC6F2");
    public static readonly Color Grass = new("7BC86C");
    public static readonly Color Brick = new("E8735A");
    public static readonly Color BrickLight = new("F08B72");
    public static readonly Color TooltipBg = new("0E1815", 0.94f);

    // Formes, en pixels à 1080p (échelle 100 %).
    public const int GlassRadius = 16;
    public const int PillHeight = 34;
    public const int ToolSize = 52;
    public const int ToolRadius = 14;
    public const int RoundSize = 44;
    public const int HeaderButtonSize = 30;
    public const int ThumbWidth = 122;
    public const int ThumbArtHeight = 86;

    // Tailles de police (jamais moins de 12).
    public const int TitleSize = 19;
    public const int TextSize = 15;
    public const int CaptionSize = 13;
    public const int MinSize = 12;

    public const string FontDir = "res://assets/ui/fonts/";
    public const string IconDir = "res://assets/ui/icons/";

    public static readonly FontFile Fredoka = GD.Load<FontFile>(FontDir + "fredoka-latin-600-normal.woff2");
    public static readonly FontFile FredokaBold = GD.Load<FontFile>(FontDir + "fredoka-latin-700-normal.woff2");
    public static readonly FontFile Nunito = GD.Load<FontFile>(FontDir + "nunito-latin-500-normal.woff2");
    public static readonly FontFile NunitoBold = GD.Load<FontFile>(FontDir + "nunito-latin-700-normal.woff2");
    public static readonly FontFile NunitoHeavy = GD.Load<FontFile>(FontDir + "nunito-latin-800-normal.woff2");

    private static Theme? _theme;
    private static Shader? _glass;

    /// <summary>Le thème unique de l'interface.</summary>
    public static Theme Theme => _theme ??= Build();

    /// <summary>Icône en silhouette blanche (<c>assets/ui/icons</c>, PNG de Kenney ou SVG du métier), teintée à l'usage.</summary>
    public static Texture2D Icon(string name)
    {
        var png = IconDir + name + ".png";
        return GD.Load<Texture2D>(ResourceLoader.Exists(png) ? png : IconDir + name + ".svg");
    }

    /// <summary>Crée la racine de l'interface sous <paramref name="parent"/> : un <c>CanvasLayer</c> « Ui » et son <see cref="UiRoot"/> plein écran.</summary>
    public static UiRoot CreateRoot(Node parent)
    {
        var layer = new CanvasLayer { Name = "Ui", Layer = 10 };
        var root = new UiRoot { Name = "UiRoot", Theme = Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        parent.AddChild(layer);
        return root;
    }

    /// <summary>
    /// Hauteur d'une liste défilante de panneau (ligue, Birdie), réglée à l'ouverture : 340 px si l'écran logique le permet, moins en 720p
    /// ou à l'échelle 150 % (1280×720 logiques), pour que la colonne tiroir et panneau tienne au-dessus de la barre.
    /// </summary>
    public static void FitList(ScrollContainer list) =>
        list.CustomMinimumSize = new Vector2(0f, Mathf.Clamp(list.GetViewportRect().Size.Y - 520f, 160f, 340f));

    /// <summary>Fond de verre dépoli (flou de l'écran) à placer en premier enfant d'un panneau : il se dessine derrière son StyleBox.</summary>
    public static ColorRect GlassBackdrop(int radius = GlassRadius)
    {
        var material = new ShaderMaterial { Shader = _glass ??= GD.Load<Shader>("res://shaders/ui_glass.gdshader") };
        material.SetShaderParameter("radius", (float)radius);
        return new ColorRect { Name = "Glass", Material = material, ShowBehindParent = true, MouseFilter = Control.MouseFilterEnum.Ignore };
    }

    /// <summary>Cadre de verre dépoli sans en-tête (tiroir, barre) autour de <paramref name="content"/> ; <paramref name="tint"/> remplace le sapin.</summary>
    public static PanelContainer GlassFrame(string name, Control content, int padding, Color? tint = null)
    {
        var frame = new PanelContainer { Name = name, ThemeTypeVariation = "UiPanel" };
        if (tint is { } color)
        {
            var box = GlassBox(0);
            box.BgColor = color;
            frame.AddThemeStyleboxOverride("panel", box);
        }

        frame.AddChild(GlassBackdrop());
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, padding);
        }

        margin.AddChild(content);
        frame.AddChild(margin);
        return frame;
    }

    /// <summary>Rectangle arrondi plein, avec bord facultatif.</summary>
    public static StyleBoxFlat Box(Color bg, int radius, Color? border = null, int borderWidth = 0)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border ?? Colors.Transparent, AntiAliasing = true, CornerDetail = 8 };
        box.SetCornerRadiusAll(radius);
        box.SetBorderWidthAll(border is null ? 0 : borderWidth);
        return box;
    }

    private static StyleBoxFlat Padded(StyleBoxFlat box, float horizontal, float vertical)
    {
        box.ContentMarginLeft = box.ContentMarginRight = horizontal;
        box.ContentMarginTop = box.ContentMarginBottom = vertical;
        return box;
    }

    private static StyleBoxFlat GlassBox(int padding)
    {
        var box = Padded(Box(Glass, GlassRadius, Rim, 1), padding, padding);
        box.ShadowColor = new Color(0f, 0f, 0f, 0.3f);
        box.ShadowSize = 18;
        box.ShadowOffset = new Vector2(0f, 8f);
        return box;
    }

    private static Theme Build()
    {
        var t = new Theme { DefaultFont = Nunito, DefaultFontSize = TextSize };

        t.SetStylebox("panel", "PanelContainer", GlassBox(14));
        t.SetStylebox("panel", "Panel", GlassBox(0));

        // Panneau à verre dépoli : marges nulles, pour que le fond flou couvre tout le panneau ; l'en-tête et le contenu ont les leurs.
        t.SetTypeVariation("UiPanel", "PanelContainer");
        t.SetStylebox("panel", "UiPanel", GlassBox(0));

        t.SetColor("font_color", "Label", Cream);
        t.SetTypeVariation("UiTitle", "Label");
        t.SetFont("font", "UiTitle", Fredoka);
        t.SetFontSize("font_size", "UiTitle", TitleSize);
        t.SetTypeVariation("UiCaption", "Label");
        t.SetFontSize("font_size", "UiCaption", CaptionSize);
        t.SetColor("font_color", "UiCaption", CreamDim);
        t.SetTypeVariation("UiKey", "Label");
        t.SetColor("font_color", "UiKey", CreamDim);
        t.SetTypeVariation("UiValue", "Label");
        t.SetFont("font", "UiValue", NunitoHeavy);
        t.SetTypeVariation("UiName", "Label");
        t.SetFont("font", "UiName", Fredoka);

        // Bouton par défaut : pastille Fredoka.
        t.SetFont("font", "Button", Fredoka);
        t.SetFontSize("font_size", "Button", TextSize);
        foreach (var (name, color) in new[] { ("font_color", Cream), ("font_hover_color", Cream), ("font_focus_color", Cream), ("font_pressed_color", Ink), ("font_hover_pressed_color", Ink), ("font_disabled_color", new Color(Cream, 0.35f)) })
        {
            t.SetColor(name, "Button", color);
        }

        t.SetStylebox("normal", "Button", Padded(Box(new Color(0f, 0f, 0f, 0.28f), 999, new Color("F3ECD8", 0.2f), 1), 16, 4));
        t.SetStylebox("hover", "Button", Padded(Box(new Color(0f, 0f, 0f, 0.4f), 999, new Color("F3ECD8", 0.35f), 1), 16, 4));
        t.SetStylebox("pressed", "Button", Padded(Box(Flag, 999, Ink, 2), 16, 4));
        t.SetStylebox("hover_pressed", "Button", Padded(Box(Flag, 999, Ink, 2), 16, 4));
        t.SetStylebox("disabled", "Button", Padded(Box(new Color(0f, 0f, 0f, 0.2f), 999), 16, 4));
        t.SetStylebox("focus", "Button", new StyleBoxEmpty());

        // Bouton d'outil : silhouette sur fond nu ; survol clair ; choisi en jaune drapeau cerné d'encre ; grisé à 35 %.
        t.SetTypeVariation("UiToolButton", "Button");
        t.SetStylebox("normal", "UiToolButton", Box(Colors.Transparent, ToolRadius));
        t.SetStylebox("hover", "UiToolButton", Box(new Color("F3ECD8", 0.12f), ToolRadius));
        t.SetStylebox("pressed", "UiToolButton", Box(Flag, ToolRadius, Ink, 2));
        t.SetStylebox("hover_pressed", "UiToolButton", Box(Flag, ToolRadius, Ink, 2));
        t.SetStylebox("disabled", "UiToolButton", Box(Colors.Transparent, ToolRadius));
        t.SetColor("icon_normal_color", "UiToolButton", CreamIcon);
        t.SetColor("icon_hover_color", "UiToolButton", Cream);
        t.SetColor("icon_focus_color", "UiToolButton", CreamIcon);
        t.SetColor("icon_pressed_color", "UiToolButton", Ink);
        t.SetColor("icon_hover_pressed_color", "UiToolButton", Ink);
        t.SetColor("icon_disabled_color", "UiToolButton", new Color(CreamIcon, 0.35f));
        t.SetConstant("icon_max_width", "UiToolButton", 30);

        // Bouton rond (croix d'un panneau, boutons de panneaux de la barre).
        t.SetTypeVariation("UiRoundButton", "Button");
        t.SetStylebox("normal", "UiRoundButton", Box(new Color(0f, 0f, 0f, 0.3f), 999, new Color("F3ECD8", 0.25f), 1));
        t.SetStylebox("hover", "UiRoundButton", Box(new Color(0f, 0f, 0f, 0.45f), 999, new Color("F3ECD8", 0.45f), 1));
        t.SetStylebox("pressed", "UiRoundButton", Box(Flag, 999, Ink, 2));
        t.SetStylebox("hover_pressed", "UiRoundButton", Box(Flag, 999, Ink, 2));
        t.SetStylebox("disabled", "UiRoundButton", Box(new Color(0f, 0f, 0f, 0.2f), 999, new Color("F3ECD8", 0.12f), 1));
        t.SetColor("icon_normal_color", "UiRoundButton", Cream);
        t.SetColor("icon_hover_color", "UiRoundButton", Cream);
        t.SetColor("icon_pressed_color", "UiRoundButton", Ink);
        t.SetColor("icon_hover_pressed_color", "UiRoundButton", Ink);
        t.SetColor("icon_disabled_color", "UiRoundButton", new Color(Cream, 0.35f));

        // Vignette : fond sombre, anneau jaune et lueur quand elle est choisie.
        t.SetTypeVariation("UiThumb", "Button");
        t.SetStylebox("normal", "UiThumb", Box(new Color(0f, 0f, 0f, 0.25f), ToolRadius, Rim, 1));
        t.SetStylebox("hover", "UiThumb", Box(new Color(0f, 0f, 0f, 0.35f), ToolRadius, new Color("F3ECD8", 0.55f), 2));
        var chosen = Box(new Color(0f, 0f, 0f, 0.25f), ToolRadius, Flag, 3);
        chosen.ShadowColor = new Color(Flag, 0.45f);
        chosen.ShadowSize = 8;
        t.SetStylebox("pressed", "UiThumb", chosen);
        t.SetStylebox("hover_pressed", "UiThumb", chosen);
        t.SetStylebox("disabled", "UiThumb", Box(new Color(0f, 0f, 0f, 0.15f), ToolRadius, Colors.Transparent, 3));

        // Pastille de l'étage bas et pastille de prix d'une vignette.
        t.SetTypeVariation("UiPill", "PanelContainer");
        t.SetStylebox("panel", "UiPill", Padded(Box(Pill, 999), 16, 0));
        t.SetTypeVariation("UiPrice", "Label");
        t.SetStylebox("normal", "UiPrice", Padded(Box(new Color("182823", 0.85f), 999), 8, 1));
        t.SetFont("font", "UiPrice", NunitoHeavy);
        t.SetFontSize("font_size", "UiPrice", MinSize);

        // Info-bulle : aussi celle des TooltipText ordinaires.
        t.SetStylebox("panel", "TooltipPanel", Padded(Box(TooltipBg, 12, new Color("F3ECD8", 0.25f), 1), 14, 12));
        t.SetColor("font_color", "TooltipLabel", Cream);
        t.SetFont("font", "TooltipLabel", Nunito);
        t.SetFontSize("font_size", "TooltipLabel", 14);

        // Bouton nu dans une pastille (vitesses, pause) : texte crème, choisi en jaune drapeau.
        t.SetTypeVariation("UiPillButton", "Button");
        t.SetFontSize("font_size", "UiPillButton", CaptionSize + 1);
        t.SetStylebox("normal", "UiPillButton", Padded(Box(Colors.Transparent, 999), 7, 2));
        t.SetStylebox("hover", "UiPillButton", Padded(Box(new Color("F3ECD8", 0.14f), 999), 7, 2));
        t.SetStylebox("pressed", "UiPillButton", Padded(Box(Flag, 999), 7, 2));
        t.SetStylebox("hover_pressed", "UiPillButton", Padded(Box(Flag, 999), 7, 2));
        t.SetColor("icon_normal_color", "UiPillButton", CreamIcon);
        t.SetColor("icon_pressed_color", "UiPillButton", Ink);
        t.SetColor("icon_hover_pressed_color", "UiPillButton", Ink);
        t.SetConstant("icon_max_width", "UiPillButton", 16);

        // Curseur (taille du pinceau) : rail sombre, partie remplie en jaune drapeau, poignée crème cernée d'encre.
        var rail = Box(new Color(0f, 0f, 0f, 0.35f), 999);
        rail.ContentMarginTop = rail.ContentMarginBottom = 4f;
        t.SetStylebox("slider", "HSlider", rail);
        var filled = Box(Flag, 999);
        filled.ContentMarginTop = filled.ContentMarginBottom = 4f;
        t.SetStylebox("grabber_area", "HSlider", filled);
        t.SetStylebox("grabber_area_highlight", "HSlider", filled);
        var grabber = GD.Load<Texture2D>("res://assets/ui/grabber.svg");
        t.SetIcon("grabber", "HSlider", grabber);
        t.SetIcon("grabber_highlight", "HSlider", grabber);

        // Jauge.
        t.SetStylebox("background", "ProgressBar", Box(new Color(0f, 0f, 0f, 0.35f), 999));
        t.SetStylebox("fill", "ProgressBar", Box(Grass, 999));

        // Tableau : en-têtes en légende, lignes séparées.
        t.SetTypeVariation("UiTableHeader", "Label");
        t.SetFont("font", "UiTableHeader", NunitoBold);
        t.SetFontSize("font_size", "UiTableHeader", CaptionSize);
        t.SetColor("font_color", "UiTableHeader", CreamDim);
        t.SetConstant("h_separation", "GridContainer", 18);
        t.SetConstant("v_separation", "GridContainer", 6);

        t.SetStylebox("normal", "LineEdit", Padded(Box(Pill, 10, Rim, 1), 10, 4));
        t.SetColor("font_color", "LineEdit", Cream);
        return t;
    }
}
