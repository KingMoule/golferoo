using Godot;

namespace Golferoo.Game.Cameras;

/// <summary>Modes de vue du <see cref="CameraRig"/> : isométrique orthographique, ou libre en perspective.</summary>
public enum CameraMode
{
    Iso,
    Free,
}

/// <summary>
/// Caméra à modes. Iso (par défaut) : orthographique, rotation par quarts (actions camera_rotate_*, animée) et libre (clic du milieu glissé),
/// zoom à la molette, déplacement par les actions camera_*. Free : perspective, mêmes commandes, plus l'inclinaison (clic du milieu, vertical) ;
/// la molette règle la distance à la cible. <see cref="KeepAbove"/> garde la caméra au-dessus du relief et dans la carte.
/// camera_toggle_mode passe de l'un à l'autre en 0,3 s autour de la même cible.
/// Les touches sont dans l'InputMap (project.godot), jamais codées ici. Suivi d'un golfeur : voir <see cref="Follow"/>.
/// Elle ne lit jamais le clic gauche (réservé au pinceau, voir Tools/Brush.cs) ni le clic droit.
/// </summary>
public partial class CameraRig : Node3D
{
    private const float IsoPitchDegrees = 35.264f;
    private const float MinSize = 6f;
    private const float MaxSize = 400f;
    private const float IsoDistance = 600f;
    private const float FovDegrees = 60f;
    private const float MinPitch = 5f;
    private const float MaxPitch = 80f;
    private const float MinDistance = 3f;
    private const float MaxDistance = 600f;
    private const float TransitionSeconds = 0.3f;

    /// <summary>Hauteur minimale de la caméra libre au-dessus du relief, en mètres.</summary>
    public const float Clearance = 1.5f;

    // Distance d'une caméra en perspective qui cadre la même hauteur qu'une caméra orthographique de taille 1.
    private static readonly float SizeToDistance = 1f / (2f * Mathf.Tan(Mathf.DegToRad(FovDegrees) / 2f));

    private Camera3D? _camera;
    private float _yaw = Mathf.DegToRad(45f);
    private float _yawTarget = Mathf.DegToRad(45f);
    private float _size = 140f;
    private Vector3 _target;
    private Rect2 _bounds = new(0, 0, 100, 100);
    private float _far = 1500f;
    private float _pitch = Mathf.DegToRad(45f);
    private float _distance = 120f;
    private float _blend;

    /// <summary>Position (x, y, z) du golfeur d'identifiant donné, ou null s'il a quitté le parcours (fournie par Main).</summary>
    public Func<int, Vector3?>? FollowSource { get; set; }

    /// <summary>Hauteur du relief en (x, z), fournie par Main ; sans elle le sol est à 0.</summary>
    public Func<float, float, float>? GroundHeight { get; set; }

    /// <summary>Mode voulu ; la vue le rejoint en <see cref="TransitionSeconds"/> secondes.</summary>
    public CameraMode Mode { get; private set; } = CameraMode.Iso;

    /// <summary>Vrai tant que la vue n'est pas arrivée au mode voulu.</summary>
    public bool Transitioning => _blend != (Mode == CameraMode.Free ? 1f : 0f);

    public float FreePitchDegrees => Mathf.RadToDeg(_pitch);

    /// <summary>Golfeur suivi, ou null.</summary>
    public int? FollowedId { get; private set; }

    public float YawDegrees => Mathf.RadToDeg(_yaw);

    /// <summary>Hauteur de scène visible à la cible, en mètres (taille orthographique en iso, équivalente en libre).</summary>
    public float Size => Mode == CameraMode.Free ? _distance / SizeToDistance : _size;

    public Vector3 Target => _target;

    /// <summary>Caméra 3D sous-jacente (projection de la souris sur le terrain).</summary>
    public Camera3D? View => _camera;

    public override void _Ready()
    {
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Near = 0.1f, Far = _far, Fov = FovDegrees, Current = true };
        AddChild(_camera);
        Apply();
    }

    /// <summary>Zone (x, z) dans laquelle la cible reste.</summary>
    public void SetBounds(Rect2 bounds)
    {
        _bounds = bounds;

        // Plan lointain : la diagonale de la carte plus la distance maximale couvre toute la carte en vue rasante.
        _far = Mathf.Max(1500f, bounds.Size.Length() + MaxDistance + 50f);
    }

    /// <summary>Change de mode ; <paramref name="instant"/> saute la transition (captures, tests).</summary>
    public void SetMode(CameraMode mode, bool instant = false)
    {
        Mode = mode;
        if (instant)
        {
            _blend = mode == CameraMode.Free ? 1f : 0f;
            Apply();
        }
    }

    public void ToggleMode() => SetMode(Mode == CameraMode.Free ? CameraMode.Iso : CameraMode.Free);

    /// <summary>Incline la vue libre (positif : plus haut au-dessus de l'horizon), entre 5° et 80°.</summary>
    public void Tilt(float radians) => _pitch = Mathf.Clamp(_pitch + radians, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));

    /// <summary>
    /// Garde : ramène <paramref name="wanted"/> dans <paramref name="bounds"/> (x, z) puis au moins <see cref="Clearance"/> au-dessus
    /// du relief en ce point (<paramref name="ground"/> donne la hauteur en (x, z), 0 si null).
    /// </summary>
    public static Vector3 KeepAbove(Vector3 wanted, Func<float, float, float>? ground, Rect2 bounds)
    {
        var x = Mathf.Clamp(wanted.X, bounds.Position.X, bounds.End.X);
        var z = Mathf.Clamp(wanted.Z, bounds.Position.Y, bounds.End.Y);
        var floor = (ground?.Invoke(x, z) ?? 0f) + Clearance;
        return new Vector3(x, Mathf.Max(wanted.Y, floor), z);
    }

    /// <summary>Place la vue sans animation (captures).</summary>
    public void SetView(float yawDegrees, float size, Vector3 target)
    {
        _yaw = _yawTarget = Mathf.DegToRad(yawDegrees);
        _size = Mathf.Clamp(size, MinSize, MaxSize);
        _target = target;
        Apply();
    }

    /// <summary>Place la vue libre sans animation (captures) : cap, inclinaison, distance et cible.</summary>
    public void SetFreeView(float yawDegrees, float pitchDegrees, float distance, Vector3 target)
    {
        _yaw = _yawTarget = Mathf.DegToRad(yawDegrees);
        _pitch = Mathf.Clamp(Mathf.DegToRad(pitchDegrees), Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
        _distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        _target = target;
        SetMode(CameraMode.Free, instant: true);
    }

    /// <summary>Centre la cible sur un point et arrête le suivi.</summary>
    public void CenterOn(Vector3 point)
    {
        FollowedId = null;
        _target = point;
        Apply();
    }

    /// <summary>Suit le golfeur d'identifiant donné (null : arrête le suivi).</summary>
    public void Follow(int? id) => FollowedId = id;

    /// <summary>Golfeur suivant dans l'ordre des identifiants (le premier si aucun n'est suivi ou après le dernier) ; null si la liste est vide.</summary>
    public static int? NextGolfer(IEnumerable<int> ids, int? current)
    {
        int? first = null;
        int? next = null;
        foreach (var id in ids)
        {
            first = first is null || id < first ? id : first;
            if (current is null || id > current)
            {
                next = next is null || id < next ? id : next;
            }
        }

        return next ?? first;
    }

    public void RotateQuarter(int direction) => _yawTarget += direction * Mathf.Pi / 2f;

    public void Zoom(float factor)
    {
        if (Mode == CameraMode.Free)
        {
            _distance = Mathf.Clamp(_distance * factor, MinDistance, MaxDistance);
        }
        else
        {
            _size = Mathf.Clamp(_size * factor, MinSize, MaxSize);
        }
    }

    public void RotateFree(float radians)
    {
        _yaw += radians;
        _yawTarget += radians;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _blend = Mathf.MoveToward(_blend, Mode == CameraMode.Free ? 1f : 0f, dt / TransitionSeconds);
        _yaw = Mathf.LerpAngle(_yaw, _yawTarget, 1f - Mathf.Exp(-12f * dt));
        if (Mathf.Abs(_yaw - _yawTarget) < 0.0005f)
        {
            _yaw = _yawTarget;
        }

        var move = Input.GetVector("camera_left", "camera_right", "camera_forward", "camera_back");
        if (move != Vector2.Zero)
        {
            FollowedId = null;

            // Dans le repère de la caméra, à plat : avant = -Z local, droite = +X local.
            var basis = new Basis(Vector3.Up, _yaw);
            var world = (basis.X * move.X) + (basis.Z * move.Y);
            _target += world.Normalized() * (Size * 0.9f * dt);
        }
        else if (FollowedId is { } id)
        {
            if (FollowSource?.Invoke(id) is { } position)
            {
                _target = new Vector3(position.X, 0f, position.Z);
            }
            else
            {
                FollowedId = null;
            }
        }

        Apply();
    }

    private void Apply()
    {
        _target.X = Mathf.Clamp(_target.X, _bounds.Position.X, _bounds.End.X);
        _target.Z = Mathf.Clamp(_target.Z, _bounds.Position.Y, _bounds.End.Y);
        Position = _target;
        if (_camera is null)
        {
            return;
        }

        _camera.Far = _far;
        if (_blend <= 0f)
        {
            // Iso pur : orthographique, caméra à 600 m derrière la cible, inclinée de 35,264°.
            _camera.Projection = Camera3D.ProjectionType.Orthogonal;
            _camera.Size = _size;
            Rotation = new Vector3(-Mathf.DegToRad(IsoPitchDegrees), _yaw, 0f);
            _camera.Position = new Vector3(0, 0, IsoDistance);
            _camera.Rotation = Vector3.Zero;
            return;
        }

        // Perspective (libre ou en transition) : la vue part du cadrage iso (même hauteur visible) et rejoint inclinaison et distance libres.
        var t = _blend * _blend * (3f - (2f * _blend));
        var pitch = Mathf.Lerp(Mathf.DegToRad(IsoPitchDegrees), _pitch, t);
        var distance = Mathf.Lerp(_size * SizeToDistance, _distance, t);
        var ground = GroundHeight?.Invoke(_target.X, _target.Z) ?? 0f;
        var aim = new Vector3(_target.X, ground * t, _target.Z);
        var offset = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(_yaw) * Mathf.Cos(pitch)) * distance;
        _camera.Projection = Camera3D.ProjectionType.Perspective;
        _camera.Fov = FovDegrees;
        Rotation = new Vector3(0f, _yaw, 0f);
        _camera.GlobalPosition = KeepAbove(aim + offset, GroundHeight, _bounds);
        _camera.LookAt(aim, Vector3.Up);
    }
}
