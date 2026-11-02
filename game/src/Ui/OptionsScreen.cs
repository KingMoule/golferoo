using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Écran « Options » (C18 lot 3), le même au menu titre et au menu pause : résolution, plein écran, échelle d'interface, pause automatique.
/// Chaque changement s'applique à la fenêtre et s'écrit aussitôt dans <c>user://options.cfg</c>.
/// </summary>
public static class OptionsScreen
{
    public static UiPanel Create(Action close)
    {
        var panel = UiPanel.Create(UiIds.Menu.OptionsScreen, "Options", "wrench", 460f);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = panel.GrowVertical = Control.GrowDirection.Both;
        panel.Closed += () => close();

        var resolution = Choice(GameOptions.Resolutions.Select(r => $"{r.X} × {r.Y}"), Array.IndexOf(GameOptions.Resolutions, GameOptions.Resolution), UiIds.Menu.OptionsResolution);
        resolution.ItemSelected += i => Change(panel, () => GameOptions.Resolution = GameOptions.Resolutions[i]);
        panel.Content.AddChild(Row("Résolution", resolution));

        var fullscreen = Toggle(UiIds.Menu.OptionsFullscreen, GameOptions.Fullscreen);
        fullscreen.Toggled += on => Change(panel, () => GameOptions.Fullscreen = on);
        panel.Content.AddChild(Row("Plein écran", fullscreen));

        var scale = Choice(GameOptions.Scales.Select(s => $"{s * 100:0} %"), Array.IndexOf(GameOptions.Scales, GameOptions.UiScale), UiIds.Menu.OptionsScale);
        scale.ItemSelected += i => Change(panel, () => GameOptions.UiScale = GameOptions.Scales[i]);
        panel.Content.AddChild(Row("Échelle de l'interface", scale));

        var auto = Toggle(UiIds.Menu.OptionsAutoPause, GameOptions.AutoPause);
        auto.Toggled += on => Change(panel, () => GameOptions.AutoPause = on);
        panel.Content.AddChild(Row("Pause automatique", auto));
        panel.Content.AddChild(new Label
        {
            Text = "La partie se met en pause quand : " + string.Join(", ", GameOptions.PauseMessages.Select(m => m.Label.ToLowerInvariant())) + ".",
            ThemeTypeVariation = "UiCaption",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var advice = Toggle(UiIds.Menu.OptionsProAdvice, GameOptions.ProAdvice);
        advice.Toggled += on => Change(panel, () => GameOptions.ProAdvice = on);
        panel.Content.AddChild(Row("Conseils du pro", advice));

        panel.Content.AddChild(Volume(panel, "Volume général", UiIds.Menu.OptionsMasterVolume, GameOptions.MasterVolume, v => GameOptions.MasterVolume = v));
        panel.Content.AddChild(Volume(panel, "Musique", UiIds.Menu.OptionsMusicVolume, GameOptions.MusicVolume, v => GameOptions.MusicVolume = v));
        panel.Content.AddChild(Volume(panel, "Effets", UiIds.Menu.OptionsSfxVolume, GameOptions.SfxVolume, v => GameOptions.SfxVolume = v));
        panel.Content.AddChild(Volume(panel, "Ambiance", UiIds.Menu.OptionsAmbienceVolume, GameOptions.AmbienceVolume, v => GameOptions.AmbienceVolume = v));
        return panel;
    }

    /// <summary>Curseur de 0 à 100 % : le volume s'applique et s'écrit tout de suite.</summary>
    private static HBoxContainer Volume(Control panel, string label, string id, int value, Action<int> set)
    {
        var slider = new HSlider { Name = id, MinValue = 0, MaxValue = 100, Step = 5, Value = value, CustomMinimumSize = new Vector2(160f, 0f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, FocusMode = Control.FocusModeEnum.None };
        slider.ValueChanged += v =>
        {
            set((int)v);
            GameOptions.ApplyAudio();
            GameOptions.Save();
        };
        return Row(label, slider);
    }

    private static void Change(Control panel, Action set)
    {
        set();
        GameOptions.Save();
        GameOptions.Apply(panel.GetWindow());
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = label, ThemeTypeVariation = "UiKey", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        row.AddChild(control);
        return row;
    }

    private static OptionButton Choice(IEnumerable<string> items, int selected, string id)
    {
        var button = new OptionButton { Name = id, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(160f, 0f) };
        foreach (var item in items)
        {
            button.AddItem(item);
        }

        button.Select(selected);
        return button;
    }

    private static CheckButton Toggle(string id, bool on) => new() { Name = id, ButtonPressed = on, FocusMode = Control.FocusModeEnum.None };
}
