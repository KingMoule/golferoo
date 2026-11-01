using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Menu pause (C18), ouvert par Échap quand rien d'autre n'est à fermer : Reprendre, Sauvegarder, Charger, Options (grisé jusqu'au lot 3), Menu titre, Quitter.
/// Il met le jeu en pause et rend l'état de pause d'avant en se fermant. La vignette d'une sauvegarde est prise à l'ouverture, avant que le menu n'assombrisse l'écran.
/// </summary>
public partial class PauseMenu : Node
{
    private Main _main = null!;
    private Control _shade = null!;
    private UiPanel _menu = null!;
    private UiPanel? _slots;
    private Label _status = null!;
    private bool _wasPaused;
    private byte[]? _shot;

    public bool IsOpen => _shade.Visible;

    public void Init(Main main)
    {
        _main = main;
        _shade = new ColorRect { Name = UiIds.Menu.Pause, Color = new Color(0f, 0f, 0f, 0.45f), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _menu = UiPanel.Create("PauseMenuPanel", "Pause", "pause", 320f);
        _menu.SetAnchorsPreset(Control.LayoutPreset.Center);
        _menu.GrowHorizontal = _menu.GrowVertical = Control.GrowDirection.Both;
        _menu.Closed += Close;
        Add(UiIds.Menu.Resume, "Reprendre", Close);
        Add(UiIds.Menu.Save, "Sauvegarder", () => ShowSlots(saving: true)).Disabled = !main.CanSave;
        Add(UiIds.Menu.Load, "Charger", () => ShowSlots(saving: false));
        var options = Add(UiIds.Menu.Options, "Options", () => { });
        options.Disabled = true;
        options.TooltipText = "Bientôt disponible";
        Add(UiIds.Menu.ToTitle, "Menu titre", () => GetTree().ChangeSceneToFile(TitleMenu.Scene));
        Add(UiIds.Menu.Quit, "Quitter", () => GetTree().Quit());
        _status = new Label { Name = UiIds.Menu.Error, ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _menu.Content.AddChild(_status);
        _shade.AddChild(_menu);
        main.Ui.AddChild(_shade);
    }

    /// <summary>Échap : ouvre le menu, ou le ferme (l'écran Charger / Sauvegarder se ferme d'abord).</summary>
    public void Toggle()
    {
        if (!IsOpen)
        {
            Open();
        }
        else if (_slots is not null)
        {
            CloseSlots();
        }
        else
        {
            Close();
        }
    }

    private void Open()
    {
        _shot = ThumbnailRenderer.CaptureJpg(GetViewport());
        _wasPaused = _main.Paused;
        _main.Paused = true;
        _status.Visible = false;
        _shade.Visible = true;
    }

    private void Close()
    {
        CloseSlots();
        _shade.Visible = false;
        _main.Paused = _wasPaused;
    }

    private Button Add(string id, string text, Action pressed)
    {
        var button = new Button { Name = id, Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 36f) };
        button.Pressed += pressed;
        _menu.Content.AddChild(button);
        return button;
    }

    private void ShowSlots(bool saving)
    {
        CloseSlots();
        _slots = SlotsScreen.Create(saving, slot => Pick(slot, saving), CloseSlots);
        _menu.Visible = false;
        _shade.AddChild(_slots);
    }

    private void CloseSlots()
    {
        _slots?.QueueFree();
        _slots = null;
        _menu.Visible = true;
    }

    private void Pick(string slot, bool saving)
    {
        if (saving)
        {
            CloseSlots();
            Say(_main.SaveToSlot(slot, _shot) ? "Partie sauvegardée." : "Sauvegarde impossible.");
        }
        else if (_main.LoadSlot(slot) is { } error)
        {
            CloseSlots();
            Say(error);
        }
    }

    private void Say(string text)
    {
        _status.Text = text;
        _status.Visible = true;
    }
}
