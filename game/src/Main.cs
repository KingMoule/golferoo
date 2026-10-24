using Godot;
using Golferoo.Sim;

namespace Golferoo.Game;

/// <summary>
/// Scène principale : avance la simulation à tick fixe et affiche son état.
/// Le squelette (lot 1 de C01) n'affiche que le numéro de tick.
/// </summary>
public partial class Main : Node3D
{
    private double _accumulator;
    private Label? _tickLabel;

    public World World { get; } = new(seed: 1);

    public override void _Ready()
    {
        _tickLabel = GetNode<Label>("TickLabel");
    }

    public override void _Process(double delta)
    {
        _accumulator += delta;
        while (_accumulator >= World.TickSeconds)
        {
            World.Step();
            _accumulator -= World.TickSeconds;
        }

        if (_tickLabel is not null)
        {
            _tickLabel.Text = $"tick {World.Tick}";
        }
    }
}
