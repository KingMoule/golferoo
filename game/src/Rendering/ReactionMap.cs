using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;

namespace Golferoo.Game.Rendering;

/// <summary>
/// La carte des réactions (C16) : un disque posé sur le terrain à la cellule de chacune des dernières réactions (<see cref="Reactions.Spots"/>), vert si elle plaît,
/// rouge sinon. Les disques se recouvrent et s'additionnent : une tache foncée montre où le parcours fait réagir. Cachée, elle ne coûte rien.
/// </summary>
public partial class ReactionMap : Node3D
{
    private readonly CylinderMesh _disc = new() { TopRadius = 6f, BottomRadius = 6f, Height = 0.1f, RadialSegments = 20, Rings = 1 };
    private readonly StandardMaterial3D _good = Disc(UiTheme.Grass);
    private readonly StandardMaterial3D _bad = Disc(UiTheme.Brick);
    private (int Count, ReactionSpot First, ReactionSpot Last) _built = (-1, default, default);

    /// <summary>Les disques posés.</summary>
    public int Discs => GetChildCount();

    private static StandardMaterial3D Disc(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(color, 0.4f),
        NoDepthTest = true,
        RenderPriority = 1,
    };

    /// <summary>Remet les disques d'accord avec les réactions de la sim (rien à faire si elles n'ont pas changé).</summary>
    public void Sync(World world)
    {
        var spots = world.Reactions.Spots;
        var key = (spots.Count, spots.Count > 0 ? spots[0] : default, spots.Count > 0 ? spots[^1] : default);
        if (key == _built || world.Terrain is not { } terrain)
        {
            return;
        }

        _built = key;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        foreach (var spot in spots)
        {
            var at = terrain.CellCenter(spot.X, spot.Z);
            AddChild(new MeshInstance3D
            {
                Mesh = _disc,
                MaterialOverride = world.Moods.Definitions[(int)spot.Event].Value > 0 ? _good : _bad,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3((float)at.X, (float)terrain.Sample(at.X, at.Z) + 0.4f, (float)at.Z),
            });
        }
    }
}
