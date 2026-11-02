using Godot;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Ui;

/// <summary>
/// Menu titre (C18), la scène de lancement : Nouvelle partie (graine et aperçu de la propriété générée, difficulté Facile), Charger, Parcours de démonstration (« Le neuf »), Options, Quitter.
/// Il dépose sa demande dans <see cref="Main"/> puis charge la scène du jeu. `--course`, `--tutorial`, `--load`, `--pro` et la mesure FPS le contournent (<see cref="Main.SkipsTitleMenu"/>).
/// </summary>
public partial class TitleMenu : Control
{
    public const string Scene = "res://TitleMenu.tscn";
    private const string MainScene = "res://Main.tscn";

    private UiPanel _menu = null!;
    private UiPanel? _other;
    private Label _status = null!;
    private SpinBox _seed = null!;
    private TextureRect _preview = null!;
    private Label _previewNote = null!;
    private Button _start = null!;
    private GeneratedProperty? _property;
    private Task<(GeneratedProperty?, byte[]?)>? _pending;

    public override void _Ready()
    {
        if (Main.SkipsTitleMenu)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, MainScene);
            return;
        }

        GameOptions.Apply(GetWindow());
        Sound.Audio.Sfx.Music("music_menu");
        Theme = UiTheme.Theme;
        var sky = new ColorRect { Color = new Color(0.55f, 0.75f, 0.92f), MouseFilter = MouseFilterEnum.Ignore };
        sky.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(sky);

        _menu = UiPanel.Create(UiIds.Menu.Title, "Golferoo", "flag", 340f);
        _menu.CloseButton.Visible = false;
        Center(_menu);
        Add(UiIds.Menu.NewGame, "Nouvelle partie", ShowNewGame);
        Add(UiIds.Menu.Load, "Charger", ShowLoad);
        Add(UiIds.Menu.Demo, "Parcours de démonstration", () => Launch(Main.GameRequest.Demo));
        Add(UiIds.Menu.Options, "Options", () => Swap(OptionsScreen.Create(Back)));
        Add(UiIds.Menu.Quit, "Quitter", () => GetTree().Quit());
        _status = new Label { Name = UiIds.Menu.Error, ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _menu.Content.AddChild(_status);
        AddChild(_menu);
    }

    private static void Center(Control panel)
    {
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.GrowHorizontal = panel.GrowVertical = GrowDirection.Both;
    }

    private Button Add(string id, string text, Action pressed)
    {
        var button = new Button { Name = id, Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 38f) };
        button.Pressed += pressed;
        _menu.Content.AddChild(button);
        return button;
    }

    /// <summary>Remplace le menu par un autre panneau ; sa croix revient au menu.</summary>
    private void Swap(UiPanel other)
    {
        _other?.QueueFree();
        _other = other;
        _menu.Visible = false;
        AddChild(other);
    }

    private void Back()
    {
        _other?.QueueFree();
        _other = null;
        _menu.Visible = true;
    }

    private void ShowNewGame()
    {
        _status.Visible = false;
        var panel = UiPanel.Create("NewGamePanel", "Nouvelle partie", "flag", 340f);
        Center(panel);
        panel.Closed += Back;
        panel.Content.AddChild(new Label { Text = "Difficulté : Facile", ThemeTypeVariation = "UiName" });
        panel.Content.AddChild(new Label { Text = "Une propriété vierge, tirée de la graine, et votre pavillon au centre. À vous d'en faire un club.", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _preview = new TextureRect
        {
            Name = UiIds.Menu.Preview,
            CustomMinimumSize = new Vector2(300f, 200f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        panel.Content.AddChild(_preview);
        _previewNote = new Label { Name = UiIds.Menu.PreviewNote, ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.Content.AddChild(_previewNote);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = "Graine", ThemeTypeVariation = "UiKey", SizeFlagsVertical = SizeFlags.ShrinkCenter });
        _seed = new SpinBox { Name = UiIds.Menu.Seed, MinValue = 1, MaxValue = 999_999, Step = 1, Value = Random.Shared.Next(1, 1_000_000), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_seed);
        var other = new Button { Name = UiIds.Menu.OtherSeed, Text = "Autre graine", FocusMode = FocusModeEnum.None };
        other.Pressed += () => _seed.Value = Random.Shared.Next(1, 1_000_000);
        row.AddChild(other);
        panel.Content.AddChild(row);
        _start = new Button { Name = UiIds.Menu.Start, Text = "Commencer", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 38f), Disabled = true };
        _start.Pressed += () =>
        {
            if (_property is null)
            {
                return;
            }

            Main.NewGameSeed = (ulong)_seed.Value;
            Main.NewGameProperty = _property;
            _property = null;
            Launch(Main.GameRequest.NewGame);
        };
        panel.Content.AddChild(_start);
        _seed.ValueChanged += _ => RefreshPreview();
        Swap(panel);
        RefreshPreview();
    }

    /// <summary>
    /// Génère la propriété de la graine saisie hors du fil principal (une cinquantaine de millisecondes) puis dessine la miniature ; « Commencer » attend le résultat.
    /// Une graine qui ne donne aucune propriété valide (huit essais ratés) est remplacée par un autre tirage, avec un message.
    /// </summary>
    private void RefreshPreview()
    {
        var seed = (ulong)_seed.Value;
        _property = null;
        _start.Disabled = true;
        _previewNote.Text = "Génération de la propriété…";
        _pending = Task.Run<(GeneratedProperty?, byte[]?)>(() =>
        {
            try
            {
                var property = PropertyGenerator.CreateWithTrees(seed);
                return (property, PropertyPreview.Pixels(property.Terrain));
            }
            catch (InvalidOperationException)
            {
                return (null, null);
            }
        });
    }

    /// <summary>Le résultat de la génération se prend ici, sur le fil principal : aucun appel à Godot depuis le fil de calcul.</summary>
    public override void _Process(double delta)
    {
        if (_pending is { IsCompleted: true } done)
        {
            _pending = null;
            ShowPreview(done.Result.Item1, done.Result.Item2);
        }
    }

    private void ShowPreview(GeneratedProperty? property, byte[]? pixels)
    {
        if (property is null || pixels is null)
        {
            _seed.Value = Random.Shared.Next(1, 1_000_000); // relance l'aperçu
            _previewNote.Text = "Cette graine ne donne pas de propriété valide : une autre est tirée.";
            return;
        }

        _property = property;
        _preview.Texture = PropertyPreview.Texture(property.Terrain, pixels);
        _previewNote.Text = "Relief, étangs et ruisseau, bois, pavillon au centre (en rouge).";
        _start.Disabled = false;
    }

    private void ShowLoad()
    {
        _status.Visible = false;
        var panel = SlotsScreen.Create(saving: false, slot =>
        {
            if (Main.TryLoad(SaveSlots.PathOf(slot)) is { } error)
            {
                Back();
                _status.Text = error;
                _status.Visible = true;
                return;
            }

            GetTree().ChangeSceneToFile(MainScene);
        }, Back);
        Swap(panel);
    }

    private void Launch(Main.GameRequest request)
    {
        Main.Request = request;
        if (request == Main.GameRequest.NewGame)
        {
            GD.Print($"DIFFICULTY facile golferDemand={DifficultyParams.Facile.GolferDemand} moodDrain={DifficultyParams.Facile.MoodDrain} seed={Main.NewGameSeed}");
        }

        GetTree().ChangeSceneToFile(MainScene);
    }
}
