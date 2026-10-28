using Godot;
using Golferoo.Sim.Turf;

namespace Golferoo.Game.Tools;

/// <summary>Les trois consignes d'entretien : trois listes, chacune émet <see cref="Golferoo.Sim.World.SetTurfSettings"/>. Ouvert par le bouton Gazon de la barre.</summary>
public partial class TurfPanel : CanvasLayer
{
    private readonly OptionButton[] _lists = new OptionButton[3];
    private PanelContainer _panel = null!;

    public bool IsOpen => _panel.Visible;

    public void Toggle() => _panel.Visible = !_panel.Visible;

    /// <summary>Construit le panneau, fermé, en bas à gauche au-dessus de la barre ; l'ordre des choix suit celui des énumérations de <see cref="TurfSettings"/>.</summary>
    public void Init(Main main)
    {
        const float bottom = -Ui.BottomBar.Height - 12f;
        var panel = new PanelContainer { Name = "TurfPanel", Visible = false, AnchorTop = 1f, AnchorBottom = 1f, OffsetLeft = 8f, OffsetTop = bottom - 40f, OffsetBottom = bottom };
        _panel = panel;

        var row = new HBoxContainer();
        string[][] choices =
        [
            ["greens lents", "greens normaux", "greens rapides"],
            ["fairways tendres", "fairways normaux", "fairways fermes"],
            ["rough court", "rough moyen", "rough épais"],
        ];
        for (var i = 0; i < _lists.Length; i++)
        {
            // Sans focus : espace et chiffres restent aux touches du jeu.
            var list = new OptionButton { Name = $"Turf{i}", FocusMode = Control.FocusModeEnum.None };
            foreach (var choice in choices[i])
            {
                list.AddItem(choice);
            }

            list.Selected = 1;
            list.ItemSelected += _ => main.World.SetTurfSettings(Current());
            _lists[i] = list;
            row.AddChild(list);
        }

        panel.AddChild(row);
        AddChild(panel);
    }

    private TurfSettings Current() => new((GreenSpeed)_lists[0].Selected, (FairwayFirmness)_lists[1].Selected, (RoughLength)_lists[2].Selected);
}
