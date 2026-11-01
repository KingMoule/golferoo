using Godot;
using Golferoo.Sim.Save;

namespace Golferoo.Game.Ui;

/// <summary>
/// Écran « Charger » et « Sauvegarder » (C18), le même au menu titre et au menu pause : un emplacement par ligne, avec vignette, club, date de jeu, argent et trous ouverts.
/// Charger montre les cinq emplacements (les vides grisés), Sauvegarder les trois du joueur. <paramref name="pick"/> reçoit l'emplacement choisi.
/// </summary>
public static class SlotsScreen
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["slot1"] = "Emplacement 1",
        ["slot2"] = "Emplacement 2",
        ["slot3"] = "Emplacement 3",
        [SaveSlotStore.Auto] = "Sauvegarde automatique",
        [SaveSlotStore.Quick] = "Sauvegarde rapide",
    };

    public static UiPanel Create(bool saving, Action<string> pick, Action close)
    {
        var panel = UiPanel.Create(UiIds.Menu.Slots, saving ? "Sauvegarder" : "Charger", saving ? "pause" : "flag", 560f);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = panel.GrowVertical = Control.GrowDirection.Both;
        panel.Closed += () => close();
        foreach (var slot in saving ? SaveSlotStore.Manual : SaveSlotStore.All)
        {
            panel.Content.AddChild(Row(slot, saving, pick));
        }

        return panel;
    }

    private static HBoxContainer Row(string slot, bool saving, Action<string> pick)
    {
        var meta = SaveSlots.Meta(slot);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var picture = new TextureRect
        {
            Texture = meta is null ? null : SaveSlots.Thumbnail(slot),
            CustomMinimumSize = new Vector2(160f, 90f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        row.AddChild(picture);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddChild(new Label { Text = Names[slot], ThemeTypeVariation = "UiName" });
        text.AddChild(new Label { Text = meta is null ? "Vide" : Details(meta), ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        row.AddChild(text);
        var button = new Button
        {
            Name = UiIds.Menu.Slot(slot),
            Text = saving ? "Sauvegarder" : "Charger",
            Disabled = !saving && meta is null,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        button.Pressed += () => pick(slot);
        row.AddChild(button);
        return row;
    }

    /// <summary>« Le neuf · 3 avril, an 1 · 150 000 $ · 9 trous ouverts ».</summary>
    private static string Details(SlotMeta meta) =>
        $"{meta.Club} · {meta.Day} {UiNames.Months[meta.Month - 1]}, an {meta.Year} · {UiMoney.Format(meta.Money)} · {meta.OpenHoles} {(meta.OpenHoles > 1 ? "trous ouverts" : "trou ouvert")}";
}
