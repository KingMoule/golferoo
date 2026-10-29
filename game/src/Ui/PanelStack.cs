using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// La pile des panneaux (principe 9) : alignée de droite à gauche au-dessus de la barre, chaque panneau ouvert s'ajoute à gauche
/// des autres sans les fermer. Sa croix le ferme ; Échap (<see cref="CloseLast"/>) ferme le dernier ouvert.
/// Elle partage avec le tiroir d'outils la colonne <see cref="Dock"/> : posée sur la barre, ou sur le tiroir quand il est ouvert, sans le chevaucher.
/// </summary>
public partial class PanelStack : HBoxContainer
{
    private readonly List<UiPanel> _open = [];

    /// <summary>Les panneaux ouverts, du premier au dernier.</summary>
    public IReadOnlyList<UiPanel> Opened => _open;

    /// <summary>La colonne au-dessus de la barre : la pile, puis le tiroir d'outils (<see cref="BottomBar"/>).</summary>
    public VBoxContainer Dock => GetParent<VBoxContainer>();

    public static PanelStack Create(UiRoot root)
    {
        // La colonne laisse passer la souris partout ailleurs que sur ses panneaux et son tiroir.
        var dock = new VBoxContainer { Name = "DrawerDock", Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        dock.AddThemeConstantOverride("separation", 12);
        dock.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        (dock.OffsetLeft, dock.OffsetRight, dock.OffsetTop, dock.OffsetBottom) = (16f, -16f, 16f, -BottomBar.Height - 12f);
        var stack = new PanelStack { Name = "PanelStack", Alignment = AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 12);
        dock.AddChild(stack);
        root.AddChild(dock);
        return stack;
    }

    /// <summary>Range <paramref name="panel"/> dans la pile, fermé ; sa croix le fermera.</summary>
    public void Add(UiPanel panel)
    {
        panel.Visible = false;
        panel.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        panel.Closed += () => Close(panel);
        panel.UnfoldRequested += () =>
        {
            panel.Folded = false;
            Fit(panel);
        };
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
        panel.Folded = false;
        panel.Visible = true;
        Fit(panel);
    }

    /// <summary>
    /// Quand les panneaux ouverts ne tiennent plus en largeur (150 % : 1 248 px), le plus ancien se replie en onglet, puis le suivant,
    /// jusqu'à ce que la pile tienne ; <paramref name="keep"/>, le panneau qu'on vient d'ouvrir ou de déplier, ne se replie jamais.
    /// </summary>
    private void Fit(UiPanel keep)
    {
        var separation = GetThemeConstant("separation");
        var available = GetViewportRect().Size.X - 32f;
        float Needed() => _open.Sum(p => p.Folded ? UiPanel.FoldedWidth : p.FullWidth) + (separation * (_open.Count - 1));
        foreach (var old in _open.Where(p => p != keep && !p.Folded).ToList())
        {
            if (Needed() <= available)
            {
                break;
            }

            old.Folded = true;
        }
    }

    public void Close(UiPanel panel)
    {
        panel.Visible = false;
        panel.Folded = false;
        _open.Remove(panel);
    }

    /// <summary>Échap : ferme le dernier panneau ouvert, s'il y en a un.</summary>
    public void CloseLast()
    {
        if (_open.Count > 0)
        {
            Close(_open[^1]);
        }
    }
}
