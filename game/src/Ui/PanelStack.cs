using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// La pile des panneaux (principe 9) : alignée de droite à gauche au-dessus de la barre, chaque panneau ouvert s'ajoute à gauche
/// des autres sans les fermer. Sa croix le ferme ; Échap (<see cref="CloseLast"/>) ferme le dernier ouvert.
/// </summary>
public partial class PanelStack : HBoxContainer
{
    private readonly List<UiPanel> _open = [];

    /// <summary>Les panneaux ouverts, du premier au dernier.</summary>
    public IReadOnlyList<UiPanel> Opened => _open;

    public static PanelStack Create(UiRoot root)
    {
        var stack = new PanelStack { Name = "PanelStack", Alignment = AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 12);
        stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        (stack.OffsetLeft, stack.OffsetRight, stack.OffsetTop, stack.OffsetBottom) = (16f, -16f, 16f, -BottomBar.Height - 12f);
        root.AddChild(stack);
        return stack;
    }

    /// <summary>Range <paramref name="panel"/> dans la pile, fermé ; sa croix le fermera.</summary>
    public void Add(UiPanel panel)
    {
        panel.Visible = false;
        panel.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        panel.Closed += () => Close(panel);
        AddChild(panel);
    }

    public void Toggle(UiPanel panel)
    {
        if (panel.Visible)
        {
            Close(panel);
        }
        else
        {
            Open(panel);
        }
    }

    /// <summary>Ouvre <paramref name="panel"/> à gauche des panneaux déjà ouverts ; il monte de la barre. Sans effet s'il est ouvert.</summary>
    public void Open(UiPanel panel)
    {
        if (panel.Visible)
        {
            return;
        }

        _open.Add(panel);
        MoveChild(panel, 0);
        panel.Visible = true;
    }

    public void Close(UiPanel panel)
    {
        panel.Visible = false;
        _open.Remove(panel);
    }

    /// <summary>Échap : ferme le dernier panneau ouvert ; faux s'il n'y en a aucun.</summary>
    public bool CloseLast()
    {
        if (_open.Count == 0)
        {
            return false;
        }

        Close(_open[^1]);
        return true;
    }
}
