using Godot;
using Golferoo.Sim.Golfers;

namespace Golferoo.Game.Ui;

/// <summary>
/// Pause automatique (C18 lot 3) : met la partie en pause quand la sim lève l'un des évènements de <see cref="GameOptions.PauseMessages"/>
/// (abandon d'un golfeur, faillite, objectif accompli, achat refusé faute de trésorerie), si l'option est active. Ne crée aucun état de sim.
/// </summary>
public partial class AutoPause : Node
{
    private Main _main = null!;

    public void Init(Main main)
    {
        _main = main;
        main.World.GolferQuit += OnQuit;
        main.World.ObjectiveCompleted += OnObjective;
        main.World.Bankrupt += OnBankrupt;
        main.World.Ledger.PurchaseRefused += OnRefused;
    }

    public override void _ExitTree()
    {
        if (_main?.World is { } world)
        {
            world.GolferQuit -= OnQuit;
            world.ObjectiveCompleted -= OnObjective;
            world.Bankrupt -= OnBankrupt;
            world.Ledger.PurchaseRefused -= OnRefused;
        }
    }

    /// <summary>Met en pause si <paramref name="id"/> est dans la liste et l'option active ; Birdie dit pourquoi. Rend vrai si la partie vient d'être mise en pause.</summary>
    public bool Notify(string id)
    {
        if (_main.Paused || !GameOptions.PausesOn(id))
        {
            return false;
        }

        _main.Paused = true;
        var label = GameOptions.PauseMessages.First(m => m.Id == id).Label;
        _main.Birdie.Post(Birdie.Club, $"Partie en pause : {label.ToLowerInvariant()}.", "#pause");
        return true;
    }

    private void OnQuit(QuitInfo info) => Notify("abandon");

    private void OnObjective(Sim.Objectives.ObjectiveDef def) => Notify("objectif");

    private void OnBankrupt() => Notify("faillite");

    private void OnRefused(Sim.Economy.LedgerCategory category, long cost) => Notify("tresorerie");
}
