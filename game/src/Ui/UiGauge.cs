using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Jauge : libellé et valeur sur une ligne, barre arrondie de 10 px dessous (vert gazon par défaut).</summary>
public partial class UiGauge : VBoxContainer
{
    public ProgressBar Bar { get; private set; } = null!;
    public Label Value { get; private set; } = null!;

    public static UiGauge Create(string id, string label, double fraction, string value, Color? color = null)
    {
        var gauge = new UiGauge { Name = id };
        gauge.AddThemeConstantOverride("separation", 5);
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, ThemeTypeVariation = "UiKey", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        gauge.Value = new Label { ThemeTypeVariation = "UiValue", HorizontalAlignment = HorizontalAlignment.Right };
        row.AddChild(gauge.Value);
        gauge.AddChild(row);
        gauge.Bar = new ProgressBar { MinValue = 0.0, MaxValue = 1.0, Step = 0.0, ShowPercentage = false, CustomMinimumSize = new Vector2(0f, 10f) };
        if (color is { } c)
        {
            gauge.Bar.AddThemeStyleboxOverride("fill", UiTheme.Box(c, 999));
        }

        gauge.AddChild(gauge.Bar);
        gauge.SetValue(fraction, value);
        return gauge;
    }

    public void SetValue(double fraction, string value)
    {
        Bar.Value = Mathf.Clamp(fraction, 0.0, 1.0);
        if (Value.Text != value)
        {
            Value.Text = value;
        }
    }
}
