using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Les douze derniers mois du panneau Finances : deux barres par mois (revenus en vert, dépenses en brique, même échelle) et, par-dessus,
/// la ligne du solde en fin de mois (échelle propre, de son plus bas à son plus haut). Le dernier mois est celui en cours.
/// </summary>
public partial class FinanceChart : Control
{
    public const int Months = 12;
    private const int FontSize = UiTheme.MinSize;
    private const float LabelHeight = 16f;

    /// <summary>Revenus, dépenses (positives) et solde de fin de mois, en cents, par mois du plus ancien au plus récent.</summary>
    public long[] Revenue { get; } = new long[Months];

    public long[] Expense { get; } = new long[Months];

    public long[] Balance { get; } = new long[Months];

    /// <summary>Faux pour un mois d'avant la partie : rien n'est dessiné.</summary>
    public bool[] Known { get; } = new bool[Months];

    public string[] Labels { get; } = new string[Months];

    public override void _Draw()
    {
        var font = GetThemeFont("font", "Label");
        var plotHeight = Size.Y - LabelHeight - 4f;
        var slot = Size.X / Months;
        var floor = 4f + plotHeight;
        DrawLine(new Vector2(0f, floor), new Vector2(Size.X, floor), new Color("F3ECD8", 0.25f), 1f);

        var top = 1L;
        long low = long.MaxValue, high = long.MinValue;
        for (var i = 0; i < Months; i++)
        {
            if (Known[i])
            {
                top = Math.Max(top, Math.Max(Revenue[i], Expense[i]));
                low = Math.Min(low, Balance[i]);
                high = Math.Max(high, Balance[i]);
            }
        }

        var width = slot * 0.34f;
        var points = new List<Vector2>();
        for (var i = 0; i < Months; i++)
        {
            var middle = (i * slot) + (slot / 2f);
            DrawString(font, new Vector2(i * slot, Size.Y - 3f), Labels[i] ?? string.Empty, HorizontalAlignment.Center, slot, FontSize, i == Months - 1 ? UiTheme.Flag : UiTheme.CreamDim);
            if (!Known[i])
            {
                continue;
            }

            DrawBar(middle - width - 1f, width, floor, plotHeight * Revenue[i] / top, UiTheme.Grass);
            DrawBar(middle + 1f, width, floor, plotHeight * Expense[i] / top, UiTheme.Brick);
            var level = high == low ? 0.5f : (float)(Balance[i] - low) / (high - low);
            points.Add(new Vector2(middle, floor - 6f - (level * (plotHeight - 12f))));
        }

        if (points.Count > 1)
        {
            DrawPolyline([.. points], UiTheme.Flag, 2f, true);
        }

        foreach (var point in points)
        {
            DrawCircle(point, 3.5f, UiTheme.Ink);
            DrawCircle(point, 2.5f, UiTheme.Flag);
        }
    }

    private void DrawBar(float x, float width, float floor, float height, Color color)
    {
        if (height > 0f)
        {
            var h = Math.Max(2f, height);
            DrawRect(new Rect2(x, floor - h, width, h), color);
        }
    }
}
