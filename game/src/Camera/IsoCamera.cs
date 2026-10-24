using Godot;

namespace Golferoo.Game.CameraRig;

/// <summary>
/// Caméra orthographique isométrique : rotation par quarts (Q/E, animée) et libre (clic du milieu glissé),
/// zoom à la molette, déplacement ZQSD/WASD et flèches.
/// Elle ne lit jamais le clic gauche (réservé au pinceau, voir Tools/Brush.cs) ni le clic droit.
/// </summary>
public partial class IsoCamera : Node3D
{
    private const float PitchDegrees = 35.264f;
    private const float MinSize = 6f;
    private const float MaxSize = 400f;

    private Camera3D? _camera;
    private float _yaw = Mathf.DegToRad(45f);
    private float _yawTarget = Mathf.DegToRad(45f);
    private float _size = 140f;
    private Vector3 _target;
    private Rect2 _bounds = new(0, 0, 100, 100);

    public float YawDegrees => Mathf.RadToDeg(_yaw);

    public float Size => _size;

    public Vector3 Target => _target;

    /// <summary>Caméra 3D sous-jacente (projection de la souris sur le terrain).</summary>
    public Camera3D? View => _camera;

    public override void _Ready()
    {
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Near = 0.1f, Far = 1500f, Current = true, Position = new Vector3(0, 0, 600f) };
        AddChild(_camera);
        Apply();
    }

    /// <summary>Zone (x, z) dans laquelle la cible reste.</summary>
    public void SetBounds(Rect2 bounds) => _bounds = bounds;

    /// <summary>Place la vue sans animation (captures).</summary>
    public void SetView(float yawDegrees, float size, Vector3 target)
    {
        _yaw = _yawTarget = Mathf.DegToRad(yawDegrees);
        _size = Mathf.Clamp(size, MinSize, MaxSize);
        _target = target;
        Apply();
    }

    public void RotateQuarter(int direction) => _yawTarget += direction * Mathf.Pi / 2f;

    public void Zoom(float factor) => _size = Mathf.Clamp(_size * factor, MinSize, MaxSize);

    public void RotateFree(float radians)
    {
        _yaw += radians;
        _yawTarget += radians;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _yaw = Mathf.LerpAngle(_yaw, _yawTarget, 1f - Mathf.Exp(-12f * dt));
        if (Mathf.Abs(_yaw - _yawTarget) < 0.0005f)
        {
            _yaw = _yawTarget;
        }

        var move = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Z) || Input.IsKeyPressed(Key.Up))
        {
            move.Y -= 1f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            move.Y += 1f;
        }

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            move.X -= 1f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            move.X += 1f;
        }

        if (move != Vector2.Zero)
        {
            // Dans le repère de la caméra, à plat : avant = -Z local, droite = +X local.
            var basis = new Basis(Vector3.Up, _yaw);
            var world = (basis.X * move.X) + (basis.Z * move.Y);
            _target += world.Normalized() * (_size * 0.9f * dt);
        }

        Apply();
    }

    private void Apply()
    {
        _target.X = Mathf.Clamp(_target.X, _bounds.Position.X, _bounds.End.X);
        _target.Z = Mathf.Clamp(_target.Z, _bounds.Position.Y, _bounds.End.Y);
        Position = _target;
        Rotation = new Vector3(-Mathf.DegToRad(PitchDegrees), _yaw, 0f);
        if (_camera is not null)
        {
            _camera.Size = _size;
        }
    }
}
