using Godot;
using Golferoo.Sim.Turf;

namespace Golferoo.Game.Tools;

/// <summary>Les trois consignes d'entretien : trois listes, chacune émet <see cref="Golferoo.Sim.World.SetTurfSettings"/>.</summary>
public partial class TurfPanel : CanvasLayer
{
    private readonly OptionButton[] _lists = new OptionButton[3];

    /// <summary>Construit le panneau (en bas à gauche) ; l'ordre des choix suit celui des énumérations de <see cref="TurfSettings"/>.</summary>
    public void Init(Main main)
    {
        var panel = new PanelContainer { Name = "TurfPanel", AnchorTop = 1f, AnchorBottom = 1f, OffsetLeft = 8f, OffsetTop = -48f, OffsetBottom = -8f };
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
