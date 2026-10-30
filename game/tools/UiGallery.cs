using Godot;
using Golferoo.Game.Ui;

namespace Golferoo.Game.Tools;

/// <summary>
/// Écran témoin de C13 : les sept composants d'interface dans leurs états, sur un fond de terrain dessiné, en 1920×1080
/// (la fenêtre est mise à l'échelle). Lancer : <c>godot --path game res://tools/UiGallery.tscn</c> ; capturé par <c>Captures</c> (<c>c13-composants</c>).
/// </summary>
public partial class UiGallery : Node
{
    public static readonly Vector2I Reference = new(1920, 1080);

    private Vector2I _oldScaleSize;
    private Window.ContentScaleModeEnum _oldScaleMode;

    public UiRoot Root { get; private set; } = null!;

    public override void _Ready()
    {
        var window = GetTree().Root;
        (_oldScaleSize, _oldScaleMode) = (window.ContentScaleSize, window.ContentScaleMode);
        window.ContentScaleSize = Reference;
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;

        var back = new CanvasLayer { Name = "Field", Layer = 0 };
        var field = new Control { Name = "FieldDrawing", MouseFilter = Control.MouseFilterEnum.Ignore };
        field.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        field.Draw += () => DrawField(field);
        back.AddChild(field);
        AddChild(back);

        Root = UiTheme.CreateRoot(this);
        Build(Root);
    }

    public override void _ExitTree()
    {
        var window = GetTree().Root;
        window.ContentScaleSize = _oldScaleSize;
        window.ContentScaleMode = _oldScaleMode;
    }

    private static void Build(UiRoot root)
    {
        // Panneau et jauges.
        var pro = UiPanel.Create(UiIds.Panel.Pro, "Votre pro", "figurine", 340f);
        var name = new HBoxContainer();
        name.AddChild(new Label { Text = "Léa Fortin", ThemeTypeVariation = "UiName", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        name.AddChild(UiPill.Create("ProDivision", "division 3", "trophy", UiTheme.Flag));
        pro.Content.AddChild(name);
        pro.Content.AddChild(UiGauge.Create("GaugePower", "Puissance", 0.62, "62"));
        pro.Content.AddChild(UiGauge.Create("GaugeAccuracy", "Précision", 0.48, "48", UiTheme.SkyDeep));
        pro.Content.AddChild(UiGauge.Create("GaugeFatigue", "Fatigue", 0.81, "81 %", UiTheme.Brick));
        Place(root, pro, 40f, 40f, "UiPanel · UiGauge · UiPill");

        // Tableau.
        var league = UiPanel.Create(UiIds.Panel.League, "Ligue · division 1", "trophy", 400f);
        league.Content.AddChild(UiTable.Create(
            "LeagueTable",
            ["Membre", "Tournois", "Points"],
            [["1. Ginette Lacasse", "6", "42"], ["2. Marc-André B.", "6", "39"], ["3. Votre pro", "5", "35"], ["4. Paul Tremblay", "6", "31"]],
            highlight: 2));
        league.Content.AddChild(UiGauge.Create("GaugeSeason", "Saison", 0.4, "12 avril"));
        Place(root, league, 40f, 400f, "UiTable");

        // Boutons d'outil, en familles, dans l'étage haut de la barre.
        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 4);
        var terrain = UiToolButton.Create(UiIds.Tool.Terrain, "toolBrush", "Terrain", "Peindre les surfaces du parcours.");
        terrain.Chosen = true;
        var relief = UiToolButton.Create(UiIds.Tool.Relief, "relief", "Relief", "Monter et creuser le sol.");
        tools.AddChild(terrain);
        tools.AddChild(relief);
        tools.AddChild(UiToolButton.Create(UiIds.Tool.Trees, "trees", "Arbres", "Planter, raser, dégager."));
        tools.AddChild(UiToolButton.Create(UiIds.Tool.Holes, "flag", "Trous", "Poser départs et drapeaux."));
        tools.AddChild(Separator());
        foreach (var (id, icon, title) in new[] { (UiIds.Tool.Buildings, "building", "Bâtiments"), (UiIds.Tool.Amenities, "bench", "Aménagements") })
        {
            var later = UiToolButton.Create(id, icon, title, "Bientôt.");
            later.Disabled = true;
            tools.AddChild(later);
        }

        tools.AddChild(Separator());
        tools.AddChild(UiToolButton.Create(UiIds.Tool.Turf, "wrench", "Gazon", "Consignes d'entretien."));
        tools.AddChild(UiToolButton.Create(UiIds.Tool.Views, "layers", "Vues", "État du gazon."));
        tools.AddChild(Separator());
        var bulldozer = UiToolButton.Create(UiIds.Tool.Bulldozer, "bulldozer", "Bulldozer", "Efface ce qui a été posé.");
        bulldozer.AddThemeColorOverride("icon_normal_color", UiTheme.BrickLight);
        tools.AddChild(bulldozer);
        Place(root, UiTheme.GlassFrame("ToolStrip", tools, 6, UiTheme.BarTop), 480f, 40f, "UiToolButton : choisi, survol, repos, grisé");
        ShowHover(relief);

        // Pastilles de l'étage bas.
        var pills = new HBoxContainer();
        pills.AddThemeConstantOverride("separation", 10);
        pills.AddChild(UiPill.Create("PillSpeed", "×2", "fastForward"));
        pills.AddChild(UiPill.Create("PillClock", "3 avril, an 1"));
        pills.AddChild(UiPill.Create("PillWeather", "18 °C · Printemps", "cloud", UiTheme.CreamIcon));
        var club = UiPill.Create("PillClub", "Golf de Le neuf");
        club.Label.ThemeTypeVariation = "UiName";
        pills.AddChild(club);
        pills.AddChild(UiPill.Create("PillMembers", "24", "figurine"));
        pills.AddChild(UiPill.Create("PillMoney", "—", "coin", UiTheme.Grass));
        Place(root, UiTheme.GlassFrame("PillStrip", pills, 7, UiTheme.BarBottom), 480f, 160f, "UiPill");

        // Tiroir : onglets en pastilles et vignettes.
        var drawer = new VBoxContainer();
        drawer.AddThemeConstantOverride("separation", 12);
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        tabs.AddChild(new Label { Text = "Terrain", ThemeTypeVariation = "UiTitle" });
        tabs.AddChild(new Button { Text = "Surfaces", ToggleMode = true, ButtonPressed = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 32f) });
        tabs.AddChild(new Button { Text = "Pinceau", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 32f) });
        drawer.AddChild(tabs);
        var thumbs = new HBoxContainer();
        thumbs.AddThemeConstantOverride("separation", 12);
        var rough = new Color("5f9148");
        (string Id, string Name, Color Fill, Color Top, Color Edge)[] surfaces =
        [
            (UiIds.Thumb.Tee, "Départ", rough, new Color("b4ea94"), new Color("5a9a3f")),
            (UiIds.Thumb.Green, "Green", rough, new Color("9ee07a"), new Color("5a9a3f")),
            (UiIds.Thumb.Fairway, "Fairway", rough, new Color("8fcf6a"), new Color("3d6b2e")),
            (UiIds.Thumb.Rough, "Rough", new Color("4f7c3b"), rough, new Color("3d6b2e")),
            (UiIds.Thumb.Bunker, "Bunker", rough, new Color("f0dca0"), new Color("c7a95e")),
            (UiIds.Thumb.Water, "Eau", rough, new Color("59a3d6"), new Color("2f6f9e")),
            (UiIds.Thumb.Wood, "Bois", rough, new Color("3d7a33"), new Color("244d1f")),
            (UiIds.Thumb.Path, "Chemin", rough, new Color("d8c7a4"), new Color("9c8a64")),
        ];
        foreach (var s in surfaces)
        {
            var thumb = UiThumb.Create(s.Id, s.Name, s.Fill, s.Top, s.Edge);
            thumb.Chosen = s.Id == UiIds.Thumb.Fairway;
            thumb.Disabled = s.Id == UiIds.Thumb.Path;
            thumbs.AddChild(thumb);
        }

        drawer.AddChild(thumbs);
        Place(root, UiTheme.GlassFrame("Drawer", drawer, 14), 480f, 640f, "UiThumb : repos, choisi, survol (Rough), grisé (Chemin)");
        ShowHover(root.Find(UiIds.Thumb.Rough)!);

        // Info-bulle, telle qu'elle monte au-dessus de la vignette survolée.
        var tip = UiTooltip.Create("Fairway", "Herbe tondue court, où la balle roule loin.", ("Construction", "— / m²"), ("Entretien", "— / mois"));
        Place(root, UiTooltip.Framed(tip), 830f, 400f, "UiTooltip");
    }

    /// <summary>Place <paramref name="control"/> en position absolue, avec une légende au-dessus.</summary>
    private static void Place(UiRoot root, Control control, float x, float y, string caption)
    {
        var label = new Label { Text = caption, ThemeTypeVariation = "UiCaption", Position = new Vector2(x + 4f, y - 22f) };
        label.AddThemeColorOverride("font_color", UiTheme.Cream);
        label.AddThemeColorOverride("font_outline_color", UiTheme.Ink);
        label.AddThemeConstantOverride("outline_size", 5);
        root.AddChild(label);
        control.Position = new Vector2(x, y);
        root.AddChild(control);
    }

    /// <summary>Montre l'état de survol sans souris : le style de repos prend celui du survol.</summary>
    private static void ShowHover(Control button)
    {
        button.AddThemeStyleboxOverride("normal", button.GetThemeStylebox("hover"));
        button.AddThemeColorOverride("icon_normal_color", button.GetThemeColor("icon_hover_color"));
    }

    private static Control Separator()
    {
        var line = new ColorRect { Color = new Color("F3ECD8", 0.18f), CustomMinimumSize = new Vector2(1f, 36f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var box = new MarginContainer();
        box.AddThemeConstantOverride("margin_left", 8);
        box.AddThemeConstantOverride("margin_right", 8);
        box.AddChild(line);
        return box;
    }

    /// <summary>Le fond : rough moucheté, fairway, green et son drapeau, étang, bunker, bosquets cernés (d'après la maquette v2).</summary>
    private static void DrawField(Control c)
    {
        c.DrawRect(new Rect2(Vector2.Zero, Reference), new Color("5f9148"));
        var rng = new RandomNumberGenerator { Seed = 13 };
        for (var i = 0; i < 2600; i++)
        {
            c.DrawCircle(new Vector2(rng.RandfRange(0f, 1920f), rng.RandfRange(0f, 1080f)), 1.6f, i % 2 == 0 ? new Color("56873f") : new Color("6a9c51"));
        }

        var curve = new Curve2D();
        curve.AddPoint(new Vector2(360f, 860f), Vector2.Zero, new Vector2(200f, -140f));
        curve.AddPoint(new Vector2(1040f, 520f), new Vector2(-240f, 90f), new Vector2(240f, -90f));
        curve.AddPoint(new Vector2(1540f, 320f), new Vector2(-160f, 40f), Vector2.Zero);
        var path = curve.Tessellate();
        c.DrawPolyline(path, new Color("3d6b2e", 0.35f), 176f, true);
        c.DrawPolyline(path, new Color("8fcf6a"), 160f, true);
        for (var k = -3; k <= 3; k += 2)
        {
            c.DrawPolyline(Offset(path, k * 20f), new Color("84c562"), 20f, true);
        }

        Ellipse(c, new Vector2(1590f, 300f), new Vector2(124f, 88f), new Color("9ee07a"), new Color("5a9a3f"), 5f);
        Ellipse(c, new Vector2(1430f, 400f), new Vector2(66f, 26f), new Color("f0dca0"), new Color("c7a95e"), 5f);
        Ellipse(c, new Vector2(980f, 700f), new Vector2(130f, 70f), new Color("59a3d6"), new Color("2f6f9e"), 5f);
        c.DrawLine(new Vector2(1612f, 286f), new Vector2(1612f, 214f), UiTheme.Ink, 5f, true);
        c.DrawColoredPolygon([new(1612f, 214f), new(1652f, 228f), new(1612f, 242f)], UiTheme.Flag);
        c.DrawPolyline([new(1612f, 214f), new(1652f, 228f), new(1612f, 242f), new(1612f, 214f)], UiTheme.Ink, 4f, true);
        float[][] trees = [[250, 440, 54], [330, 500, 46], [214, 548, 44], [150, 430, 40], [1240, 170, 50], [1320, 130, 42], [1170, 214, 38], [700, 440, 46], [770, 396, 36], [1760, 560, 52], [1690, 620, 42], [1820, 900, 60], [1700, 960, 44]];
        foreach (var t in trees)
        {
            var at = new Vector2(t[0], t[1]);
            c.DrawCircle(at, t[2], new Color("3d7a33"));
            c.DrawArc(at, t[2], 0f, Mathf.Tau, 48, new Color("244d1f"), 5f, true);
            c.DrawCircle(at - new Vector2(t[2] * 0.25f, t[2] * 0.3f), t[2] * 0.33f, new Color(1f, 1f, 1f, 0.18f));
        }
    }

    private static Vector2[] Offset(Vector2[] path, float distance)
    {
        var result = new Vector2[path.Length];
        for (var i = 0; i < path.Length; i++)
        {
            var dir = (path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            result[i] = path[i] + new Vector2(-dir.Y, dir.X) * distance;
        }

        return result;
    }

    private static void Ellipse(Control c, Vector2 center, Vector2 radii, Color fill, Color edge, float width)
    {
        var points = new Vector2[49];
        for (var i = 0; i < points.Length; i++)
        {
            var a = Mathf.Tau * i / (points.Length - 1);
            points[i] = center + new Vector2(Mathf.Cos(a) * radii.X, Mathf.Sin(a) * radii.Y);
        }

        c.DrawColoredPolygon(points[..^1], fill);
        c.DrawPolyline(points, edge, width, true);
    }
}
