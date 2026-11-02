using Godot;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;

namespace Golferoo.Game.Sound;

/// <summary>
/// Sons de gestion (C20 lot 1) : lit les évènements existants de la sim et joue le son qui leur répond. N'ajoute aucun état à la sim.
/// Les gestionnaires sont publics pour que la fumée les déclenche avec un lecteur factice.
/// </summary>
public partial class GameSounds : Node
{
    private Main _main = null!;
    private long _lastFees;
    private bool _quitHeard;

    public void Init(Main main)
    {
        _main = main;
        var world = main.World;
        _lastFees = world.Ledger.Current(LedgerCategory.GreenFees);
        world.Ledger.MoneyChanged += OnMoney;
        world.Ledger.PurchaseRefused += OnRefused;
        world.ObjectiveCompleted += OnObjective;
        world.AdviceGiven += OnAdvice;
        world.GolferQuit += OnQuit;
    }

    public override void _ExitTree()
    {
        if (_main?.World is { } world)
        {
            world.Ledger.MoneyChanged -= OnMoney;
            world.Ledger.PurchaseRefused -= OnRefused;
            world.ObjectiveCompleted -= OnObjective;
            world.AdviceGiven -= OnAdvice;
            world.GolferQuit -= OnQuit;
        }
    }

    /// <summary>Un green fee de plus ce mois-ci : la caisse (une clôture de mois fait baisser le total, sans son).</summary>
    public void OnMoney(long balance) => OnFees(_main.World.Ledger.Current(LedgerCategory.GreenFees));

    public void OnFees(long fees)
    {
        if (fees > _lastFees)
        {
            Audio.Play("cash");
        }

        _lastFees = fees;
    }

    public void OnRefused(LedgerCategory category, long cost) => Audio.Play("ui_refuse");

    public void OnObjective(Sim.Objectives.ObjectiveDef def) => Audio.Play("objective");

    /// <summary>Le ding accompagne la bulle du pro : muet si le joueur a coupé les conseils.</summary>
    public void OnAdvice(Sim.Pro.AdviceDef def, int hole)
    {
        if (GameOptions.ProAdvice)
        {
            Audio.Play("advice");
        }
    }

    /// <summary>Seul le premier abandon de la session sonne : les suivants sont dans le journal des pensées.</summary>
    public void OnQuit(QuitInfo info)
    {
        if (!_quitHeard)
        {
            _quitHeard = true;
            Audio.Play("quit");
        }
    }
}
