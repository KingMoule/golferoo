using Godot;
using Golferoo.Game.Rendering;
using Golferoo.Game.Tools;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Game.Ui;

/// <summary>Les familles d'outils qui ouvrent le tiroir ; <see cref="None"/> : aucun outil pris.</summary>
public enum ToolFamily
{
    None,
    Terrain,
    Relief,
    Trees,
    Holes,
    Buildings,
    Bulldozer,
}

/// <summary>
/// Tiroir à onglets au-dessus de la barre du bas (maquette v2) : en-tête (titre, onglets Terrain, Relief, Arbres, Trous, taille du pinceau)
/// et une page par famille : vignettes des surfaces, vignettes des essences et actions d'arbres, actions de trous.
/// Ne fait qu'appeler les méthodes publiques des outils ; <see cref="Sync"/> recopie leur état, au changement ou à 4 Hz.
/// </summary>
public partial class ToolDrawer : PanelContainer
{
    private static readonly (string Id, Surface Surface, string Name, Color Top, string Text)[] Surfaces =
    [
        (UiIds.Thumb.Tee, Surface.Tee, "Départ", new(0.45f, 0.78f, 0.35f), "Gazon ras et plat d'où part chaque trou."),
        (UiIds.Thumb.Green, Surface.Green, "Green", new(0.56f, 0.90f, 0.46f), "Gazon tondu très court autour du drapeau."),
        (UiIds.Thumb.Fairway, Surface.Fairway, "Fairway", new(0.40f, 0.715f, 0.305f), "Herbe tondue court, où la balle roule loin."),
        (UiIds.Thumb.Rough, Surface.Rough, "Rough", new(0.24f, 0.48f, 0.21f), "Herbe haute qui freine la balle."),
        (UiIds.Thumb.Bunker, Surface.Bunker, "Bunker", new(0.96f, 0.91f, 0.68f), "Fosse de sable, piège des coups ratés."),
        (UiIds.Thumb.Water, Surface.Water, "Eau", new(0.20f, 0.42f, 0.55f), "Étang : la balle y est perdue."),
        (UiIds.Thumb.Wood, Surface.Trees, "Bois", new(0.17f, 0.33f, 0.15f), "Sous-bois où l'on ne joue pas."),
        (UiIds.Thumb.Path, Surface.Path, "Chemin", new(0.72f, 0.64f, 0.50f), "Allée où les golfeurs marchent vite."),
    ];

    private static readonly (ToolFamily Family, string Id, string Title)[] Tabs =
    [
        (ToolFamily.Terrain, UiIds.Drawer.TabTerrain, "Terrain"),
        (ToolFamily.Relief, UiIds.Drawer.TabRelief, "Relief"),
        (ToolFamily.Trees, UiIds.Drawer.TabTrees, "Arbres"),
        (ToolFamily.Holes, UiIds.Drawer.TabHoles, "Trous"),
        (ToolFamily.Buildings, UiIds.Drawer.TabBuildings, "Bâtiments"),
    ];

    private readonly Dictionary<ToolFamily, Control> _pages = [];
    private readonly Dictionary<ToolFamily, Button> _tabs = [];
    private readonly Dictionary<Surface, UiThumb> _surfaceThumbs = [];
    private readonly Dictionary<(TreeSpeciesId, TreePlantStage), UiThumb> _treeThumbs = [];
    private readonly Dictionary<string, Button> _actions = [];
    private readonly Dictionary<BuildingKind, UiThumb> _buildingThumbs = [];
    private ToolFamily _lastFamily;
    private Main _main = null!;
    private BottomBar _bar = null!;
    private Label _title = null!;
    private HBoxContainer _tabRow = null!;
    private Control _sizeRow = null!;
    private HSlider _size = null!;
    private Label _status = null!;

    public static ToolDrawer Create(Main main, BottomBar bar, ThumbnailRenderer thumbnails)
    {
        var drawer = new ToolDrawer { Name = UiIds.Drawer.Root, ThemeTypeVariation = "UiPanel", Visible = false, _main = main, _bar = bar };
        drawer.AddChild(UiTheme.GlassBackdrop());
        drawer.Build(thumbnails);
        return drawer;
    }

    private void Build(ThumbnailRenderer thumbnails)
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);
        AddChild(body);

        // En-tête : titre, onglets, taille du pinceau.
        var headerStyle = UiTheme.Box(Colors.Transparent, 0, new Color("F3ECD8", 0.12f), 0);
        headerStyle.BorderWidthBottom = 1;
        headerStyle.ContentMarginLeft = headerStyle.ContentMarginRight = 12f;
        headerStyle.ContentMarginTop = headerStyle.ContentMarginBottom = 10f;
        var header = new PanelContainer { Name = "Header" };
        header.AddThemeStyleboxOverride("panel", headerStyle);
        body.AddChild(header);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        header.AddChild(row);
        _title = new Label { ThemeTypeVariation = "UiTitle", CustomMinimumSize = new Vector2(120f, 0f), VerticalAlignment = VerticalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(_title);
        _tabRow = new HBoxContainer();
        _tabRow.AddThemeConstantOverride("separation", 6);
        row.AddChild(_tabRow);
        foreach (var (family, id, title) in Tabs)
        {
            var tab = new Button { Name = id, Text = title, ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 32f) };
            tab.Pressed += () => _bar.Select(family, toggle: false);
            _tabs[family] = tab;
            _tabRow.AddChild(tab);
        }

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var undo = Action(UiIds.Action.Undo, "Annuler", "Annule le dernier coup de pinceau et rend son prix. Possible pendant cinq secondes ; les arbres rasés ne reviennent pas.", _main.Brush.Undo);
        undo.Visible = false;
        row.AddChild(undo);
        var sizeRow = new HBoxContainer();
        sizeRow.AddThemeConstantOverride("separation", 10);
        sizeRow.AddChild(new Label { Text = "Taille du pinceau", ThemeTypeVariation = "UiCaption", VerticalAlignment = VerticalAlignment.Center });
        _size = new HSlider
        {
            Name = UiIds.Drawer.BrushSize,
            MinValue = Brush.MinRadius,
            MaxValue = Brush.MaxRadius,
            Step = 1,
            CustomMinimumSize = new Vector2(140f, 20f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.None,
            TooltipText = "Rayon du pinceau, du bulldozer et de la zone à raser.",
        };
        _size.ValueChanged += value => _main.Brush.SetRadius((float)value);
        sizeRow.AddChild(_size);
        _sizeRow = sizeRow;
        row.AddChild(sizeRow);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 14);
        }

        body.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        _pages[ToolFamily.Terrain] = TerrainPage();
        _pages[ToolFamily.Trees] = TreesPage(thumbnails);
        _pages[ToolFamily.Holes] = HolesPage();
        _pages[ToolFamily.Buildings] = BuildingsPage(thumbnails);
        foreach (var page in _pages.Values)
        {
            page.Visible = false; // montrée par Sync : sinon toutes les pages empilées donnent au tiroir sa taille avant la première mise à jour
            content.AddChild(page);
        }

        _status = new Label { Name = UiIds.Drawer.Status, ThemeTypeVariation = "UiCaption" };
        content.AddChild(_status);
    }

    private HBoxContainer TerrainPage()
    {
        var thumbs = new HBoxContainer();
        thumbs.AddThemeConstantOverride("separation", 12);
        var rough = Surfaces[3].Top;
        foreach (var (id, surface, name, top, text) in Surfaces)
        {
            var thumb = UiThumb.Create(id, name, surface == Surface.Rough ? rough.Darkened(0.25f) : rough, top, top.Darkened(0.45f));
            thumb.Description = text;
            var perM2 = UiMoney.Format(_main.World.Economy.PaintCost(surface));
            thumb.Price.Text = perM2 + "/m²";
            thumb.Costs = [("Construction", perM2 + " / m²")];
            thumb.Pressed += () =>
            {
                _main.Brush.SelectSurface(surface);
                _bar.Sync();
            };
            _surfaceThumbs[surface] = thumb;
            thumbs.AddChild(thumb);
        }

        return thumbs;
    }

    private HBoxContainer TreesPage(ThumbnailRenderer thumbnails)
    {
        var page = new HBoxContainer();
        page.AddThemeConstantOverride("separation", 12);
        foreach (var species in Enum.GetValues<TreeSpeciesId>())
        {
            foreach (var stage in new[] { TreePlantStage.Sapling, TreePlantStage.Mound })
            {
                var name = TreeSpeciesTable.Get(species).Name;
                var thumb = UiThumb.Create(UiIds.Thumb.Tree(species.ToString(), stage.ToString()), $"{name}, {(stage == TreePlantStage.Mound ? "motte" : "plant")}", thumbnails.Render(TreeTool.ModelPath(species, stage)));
                thumb.Description = stage == TreePlantStage.Mound ? "Un jeune arbre déjà formé, planté avec sa motte." : "Un plant qui mettra des années à grandir.";
                var planting = UiMoney.Format(_main.World.Economy.PlantCost(stage));
                thumb.Price.Text = planting;
                thumb.Costs = [("Plantation", planting)];
                thumb.Pressed += () =>
                {
                    _main.TreeTool.SetPlanting(species, stage);
                    _bar.Sync();
                };
                thumb.SizeFlagsVertical = SizeFlags.ShrinkBegin;
                _treeThumbs[(species, stage)] = thumb;
                page.AddChild(thumb);
            }
        }

        var actions = new VBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        page.AddChild(actions);
        var tool = _main.TreeTool;
        actions.AddChild(Action(UiIds.Action.TreeSelect, "Sélectionner", "Un clic sur un arbre le sélectionne.", () => tool.SetMode(TreeTool.ClickMode.Select), toggle: true));
        var economy = _main.World.Economy;
        var raze = UiMoney.Format(economy.TreeRaze);
        actions.AddChild(Action(UiIds.Action.TreeRaze, "Raser", $"Abat l'arbre sélectionné : {raze}, {UiMoney.Format(economy.TreeRaze * economy.TreeRemarkableMultiplier)} pour un remarquable.", () => tool.RazeSelected()));
        actions.AddChild(Action(UiIds.Action.TreeRazeArea, "Raser une zone", $"Un clic rase les arbres et le sous-bois sous le pinceau : {raze} par arbre, {UiMoney.Format(economy.TreeClearPerM2)} par m² de sous-bois.", () => tool.SetMode(TreeTool.ClickMode.RazeArea), toggle: true));
        actions.AddChild(Action(UiIds.Action.TreeKeepRemarkable, "Garder les remarquables", "La zone rasée épargne les arbres remarquables.", () => tool.SetKeepRemarkable(!tool.KeepRemarkable), toggle: true));
        actions.AddChild(Action(UiIds.Action.TreeClearFallen, "Dégager le tronc", $"Dégage le tronc tombé sélectionné : {UiMoney.Format(economy.TreeClearFallen)}.", () => tool.ClearSelected()));
        return page;
    }

    private HBoxContainer BuildingsPage(ThumbnailRenderer thumbnails)
    {
        var page = new HBoxContainer();
        page.AddThemeConstantOverride("separation", 12);
        var world = _main.World;
        foreach (var kind in BuildingTool.Kinds)
        {
            var name = UiNames.BuildingName(kind);
            var thumb = UiThumb.Create(UiIds.Action.Building(kind.ToString()), name, thumbnails.Render(BuildingLayer.ModelPath(kind)));
            var cost = UiMoney.Format(world.BuildingCost(kind) ?? 0);
            thumb.Description = UiNames.BuildingTip(kind);
            thumb.Price.Text = cost;
            thumb.Costs = [("Construction", cost), ("Vente", UiMoney.Format((world.BuildingCost(kind) ?? 0) / 2))];
            thumb.Pressed += () =>
            {
                _main.BuildingTool.Select(kind);
                _bar.Sync();
            };
            thumb.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            _buildingThumbs[kind] = thumb;
            page.AddChild(thumb);
        }

        return page;
    }

    private HBoxContainer HolesPage()
    {
        var page = new HBoxContainer();
        page.AddThemeConstantOverride("separation", 8);
        var place = _main.PlaceTool;
        page.AddChild(Action(UiIds.Action.HolePose, "Poser départ et trou", "Un clic sur un départ le pose, un clic sur le green place le drapeau.", place.BeginPose, toggle: true));
        page.AddChild(Action(UiIds.Action.HoleNew, "Nouveau trou", $"Un clic sur un départ, puis un clic sur un green : un trou de plus, {UiMoney.Format(_main.World.Economy.NewHole)}. Une fois ouvert, il coûte {UiMoney.Format(_main.World.Economy.MaintenancePerHole)} d'entretien par mois.", place.BeginNewHole, toggle: true));
        page.AddChild(Action(UiIds.Action.HoleRemove, "Supprimer", "Retire le dernier trou du parcours.", place.RemoveLastHole));
        page.AddChild(Action(UiIds.Action.ClubhouseMove, $"Déplacer le pavillon ({UiMoney.Format(_main.World.Economy.ClubhouseMove)})", "Un clic sur le nouvel emplacement : l'aperçu est vert s'il convient, rouge sinon.", place.BeginMoveClubhouse, toggle: true));
        page.AddChild(Action(UiIds.Action.HolePrevious, "Trou précédent", "La caméra va au trou d'avant.", () => _main.CameraStepHole(-1)));
        page.AddChild(Action(UiIds.Action.HoleNext, "Trou suivant", "La caméra va au trou d'après.", () => _main.CameraStepHole(1)));
        return page;
    }

    private Button Action(string id, string text, string tip, System.Action pressed, bool toggle = false)
    {
        var button = new Button { Name = id, Text = text, TooltipText = tip, ToggleMode = toggle, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 32f) };
        button.Pressed += () =>
        {
            pressed();
            _bar.Sync();
        };
        _actions[id] = button;
        return button;
    }

    /// <summary>Recopie l'état des outils : page de <paramref name="family"/>, onglet, vignette et actions choisies, taille, phrase d'état.</summary>
    public void Sync(ToolFamily family)
    {
        Visible = family != ToolFamily.None;
        if (!Visible)
        {
            return;
        }

        var brush = _main.Brush;
        var trees = _main.TreeTool;
        var place = _main.PlaceTool;
        var buildings = _main.BuildingTool;
        _title.Text = family switch
        {
            ToolFamily.Bulldozer => "Bulldozer",
            ToolFamily.Holes => $"Trou {_main.CameraHole}",
            _ => "Construire",
        };
        _tabRow.Visible = family != ToolFamily.Bulldozer;
        foreach (var (f, tab) in _tabs)
        {
            tab.SetPressedNoSignal(f == family);
        }

        foreach (var (f, page) in _pages)
        {
            page.Visible = f == family;
        }

        if (family != _lastFamily)
        {
            _lastFamily = family;
            ResetSize(); // la mise en page ne passe qu'à l'image suivante : le tiroir prend tout de suite la taille de sa nouvelle page
        }

        _sizeRow.Visible = family is not (ToolFamily.Holes or ToolFamily.Buildings);
        var undo = _actions[UiIds.Action.Undo];
        undo.Visible = (family is ToolFamily.Terrain or ToolFamily.Relief or ToolFamily.Bulldozer) && brush.UndoOffered;
        if (undo.Visible)
        {
            undo.Disabled = _main.Paused; // en pause, le compte à rebours est arrêté et l'annulation attend la reprise
            var text = $"Annuler ({UiMoney.Format(_main.World.StrokeCost)})";
            if (undo.Text != text)
            {
                undo.Text = text;
            }
        }

        _size.SetValueNoSignal(brush.Radius);
        foreach (var (surface, thumb) in _surfaceThumbs)
        {
            thumb.Chosen = surface == brush.PaintedSurface;
        }

        foreach (var (kind, thumb) in _buildingThumbs)
        {
            var unlocked = _main.World.IsUnlocked(kind);
            var wanted = unlocked ? UiMoney.Format(_main.World.BuildingCost(kind) ?? 0) : $"{_main.World.BuildingRules.Spec(kind)!.UnlockHoles} trous ouverts";
            if (thumb.Price.Text != wanted)
            {
                thumb.Price.Text = wanted;
            }

            thumb.Disabled = !unlocked;
            thumb.Chosen = buildings.Kind == kind;
        }

        var planting = trees.Mode is TreeTool.ClickMode.Sapling or TreeTool.ClickMode.Mound;
        foreach (var ((species, stage), thumb) in _treeThumbs)
        {
            thumb.Chosen = planting && species == trees.Species && (stage == TreePlantStage.Mound) == (trees.Mode == TreeTool.ClickMode.Mound);
        }

        _actions[UiIds.Action.TreeSelect].SetPressedNoSignal(trees.Mode == TreeTool.ClickMode.Select);
        _actions[UiIds.Action.TreeRazeArea].SetPressedNoSignal(trees.Mode == TreeTool.ClickMode.RazeArea);
        _actions[UiIds.Action.TreeKeepRemarkable].SetPressedNoSignal(trees.KeepRemarkable);
        _actions[UiIds.Action.HolePose].SetPressedNoSignal(!place.NewHole && !place.MovingClubhouse);
        _actions[UiIds.Action.ClubhouseMove].SetPressedNoSignal(place.MovingClubhouse);
        _actions[UiIds.Action.HoleNew].SetPressedNoSignal(place.NewHole);

        var status = family switch
        {
            ToolFamily.Relief => $"Clic gauche : monter le sol. Clic droit : creuser. {UiMoney.Format(_main.World.Economy.ReliefPerM3)} par m³ de terre déplacée.",
            ToolFamily.Bulldozer => "Efface ce qui a été posé : la surface revient au rough, les arbres sont rasés.",
            ToolFamily.Trees => trees.Describe(),
            ToolFamily.Holes => place.Status,
            ToolFamily.Buildings => buildings.Kind is null ? "Choisissez un bâtiment, puis cliquez sur le rough pour le poser." : buildings.Status,
            _ => "",
        };
        if (_status.Text != status)
        {
            _status.Text = status;
        }

        _status.Visible = status.Length > 0;
    }
}
