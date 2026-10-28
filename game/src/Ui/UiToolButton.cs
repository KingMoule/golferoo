using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Bouton d'outil de 52 px en silhouette : repos, survol, choisi (jaune drapeau), grisé ; info-bulle <see cref="UiTooltip"/>.</summary>
public partial class UiToolButton : Button
{
    public string Description { get; set; } = "";
    public (string Label, string Value)[] Costs { get; set; } = [];

    public static UiToolButton Create(string id, string icon, string title, string description, params (string Label, string Value)[] costs) => new()
    {
        Name = id,
        ThemeTypeVariation = "UiToolButton",
        Icon = UiTheme.Icon(icon),
        IconAlignment = HorizontalAlignment.Center,
        ToggleMode = true,
        FocusMode = FocusModeEnum.None,
        CustomMinimumSize = new Vector2(UiTheme.ToolSize, UiTheme.ToolSize),
        TooltipText = title,
        Description = description,
        Costs = costs,
    };

    /// <summary>Choisi, sans émettre <c>Toggled</c> : c'est l'outil actif qui décide.</summary>
    public bool Chosen
    {
        get => ButtonPressed;
        set => SetPressedNoSignal(value);
    }

    public override GodotObject _MakeCustomTooltip(string forText) => UiTooltip.Create(forText, Description, Costs);
}
