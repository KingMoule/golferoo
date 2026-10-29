using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim.Turf;

namespace Golferoo.Game.Tools;

/// <summary>Les trois consignes d'entretien : trois listes, chacune émet <see cref="Golferoo.Sim.World.SetTurfSettings"/>. Ouvert par le bouton Gazon de la barre.</summary>
public partial class TurfPanel : Node
{
    private readonly OptionButton[] _lists = new OptionButton[3];
    private Main _main = null!;
    private UiPanel _panel = null!;

    public bool IsOpen => _panel.Visible;

    public void Toggle() => _main.Panels.Toggle(_panel);

    /// <summary>Construit le panneau, fermé, dans la pile ; l'ordre des choix suit celui des énumérations de <see cref="TurfSettings"/>.</summary>
    public void Init(Main main)
    {
        _main = main;
        _panel = UiPanel.Create(UiIds.Panel.Turf, "Gazon", "wrench", 240f);
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
            _panel.Content.AddChild(list);
        }

        main.Panels.Add(_panel);
    }

    private TurfSettings Current() => new((GreenSpeed)_lists[0].Selected, (FairwayFirmness)_lists[1].Selected, (RoughLength)_lists[2].Selected);
}
