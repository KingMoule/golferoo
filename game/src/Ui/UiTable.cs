using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Tableau : une ligne d'en-têtes en légende, puis des lignes ; la première colonne à gauche, les autres (chiffres) à droite.</summary>
public partial class UiTable : GridContainer
{
    public static UiTable Create(string id, string[] headers, string[][] rows, int highlight = -1)
    {
        var table = new UiTable { Name = id, Columns = headers.Length };
        for (var c = 0; c < headers.Length; c++)
        {
            table.AddChild(Cell(headers[c], c, "UiTableHeader"));
        }

        table.SetRows(rows, highlight);
        return table;
    }

    /// <summary>Remplace les lignes (les en-têtes restent) ; la ligne <paramref name="highlight"/> passe en jaune drapeau.</summary>
    public void SetRows(string[][] rows, int highlight = -1)
    {
        foreach (var child in GetChildren().Skip(Columns))
        {
            RemoveChild(child);
            child.QueueFree();
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < Columns; c++)
            {
                var cell = Cell(c < rows[r].Length ? rows[r][c] : "", c, c == 0 ? "" : "UiValue");
                if (r == highlight)
                {
                    cell.AddThemeColorOverride("font_color", UiTheme.Flag);
                }

                AddChild(cell);
            }
        }
    }

    private static Label Cell(string text, int column, string variation) => new()
    {
        Text = text,
        ThemeTypeVariation = variation,
        HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
        SizeFlagsHorizontal = column == 0 ? SizeFlags.ExpandFill : SizeFlags.Fill,
    };
}
