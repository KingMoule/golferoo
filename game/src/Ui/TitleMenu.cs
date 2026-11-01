using Godot;
using Golferoo.Sim.Economy;

namespace Golferoo.Game.Ui;

/// <summary>
/// Menu titre (C18), la scène de lancement : Nouvelle partie (graine, difficulté Facile), Charger, Parcours de démonstration (« Le neuf »), Options (grisé jusqu'au lot 3), Quitter.
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

    public override void _Ready()
    {
        if (Main.SkipsTitleMenu)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, MainScene);
            return;
        }

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
        var options = Add(UiIds.Menu.Options, "Options", () => { });
        options.Disabled = true;
        options.TooltipText = "Bientôt disponible";
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
        panel.Content.AddChild(new Label { Text = "Une propriété vierge, un premier trou et votre pavillon. À vous d'en faire un club.", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = "Graine", ThemeTypeVariation = "UiKey", SizeFlagsVertical = SizeFlags.ShrinkCenter });
        _seed = new SpinBox { MinValue = 1, MaxValue = 999_999, Step = 1, Value = Random.Shared.Next(1, 1_000_000), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_seed);
        panel.Content.AddChild(row);
        var start = new Button { Name = UiIds.Menu.Start, Text = "Commencer", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 38f) };
        start.Pressed += () =>
        {
            Main.NewGameSeed = (ulong)_seed.Value;
            Launch(Main.GameRequest.NewGame);
        };
        panel.Content.AddChild(start);
        Swap(panel);
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
