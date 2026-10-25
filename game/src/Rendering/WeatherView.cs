using Godot;
using Sky = Golferoo.Sim.Weather.Sky;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Temps du jour à l'écran : couleur du ciel et force du soleil selon le ciel, et pluie en particules (GPUParticles3D)
/// au-dessus du point que regarde la caméra, sur toute la largeur de la vue. Coût fixe, quelle que soit la taille de la carte.
/// </summary>
public partial class WeatherView : Node3D
{
    /// <summary>Hauteur d'où tombent les gouttes au-dessus du sol visé, en mètres.</summary>
    private const float RainHeight = 30f;

    private const float DropSpeed = 22f;

    // Indexés par Sky : soleil, nuages, pluie, forte pluie.
    private static readonly Color[] SkyColors = [new(0.55f, 0.75f, 0.92f), new(0.63f, 0.69f, 0.76f), new(0.5f, 0.55f, 0.62f), new(0.4f, 0.44f, 0.5f)];
    private static readonly float[] SunFactors = [1f, 0.6f, 0.45f, 0.35f];
    private static readonly int[] Drops = [0, 0, 4000, 9000];

    private Godot.Environment? _environment;
    private DirectionalLight3D? _sun;
    private float _sunEnergy = 1f;
    private GpuParticles3D? _rain;
    private ParticleProcessMaterial? _process;
    private Sky? _shown;

    /// <summary>Ciel affiché (null avant le premier <see cref="Show"/>).</summary>
    public Sky? Shown => _shown;

    /// <summary>Gouttes émises (0 sans pluie).</summary>
    public int DropCount => _rain is { Emitting: true } r ? r.Amount : 0;

    public void Init(Godot.Environment environment, DirectionalLight3D? sun)
    {
        _environment = environment;
        _sun = sun;
        _sunEnergy = sun?.LightEnergy ?? 1f;
        _process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            Direction = Vector3.Down,
            Spread = 0f,
            InitialVelocityMin = DropSpeed,
            InitialVelocityMax = DropSpeed * 1.2f,
            Gravity = Vector3.Zero,
        };
        var drop = new QuadMesh
        {
            Size = new Vector2(0.04f, 0.8f),
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(0.82f, 0.86f, 0.95f, 0.45f),
                BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
            },
        };
        _rain = new GpuParticles3D
        {
            Name = "Rain",
            Emitting = false,
            Lifetime = RainHeight / DropSpeed,
            Preprocess = RainHeight / DropSpeed, // la pluie est déjà là au premier affichage
            ProcessMaterial = _process,
            DrawPass1 = drop,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-400, -RainHeight - 10, -400), new Vector3(800, RainHeight + 20, 800)),
        };
        AddChild(_rain);
    }

    /// <summary>Applique le ciel et le vent du moment ; la pluie couvre la vue centrée sur <paramref name="target"/>, large de <paramref name="viewSize"/> m.</summary>
    public void Show(Sky sky, Vector2 wind, Vector3 target, float viewSize)
    {
        if (_environment is null || _rain is null || _process is null)
        {
            return;
        }

        if (_shown != sky)
        {
            _shown = sky;
            _environment.BackgroundColor = SkyColors[(int)sky];
            if (_sun is not null)
            {
                _sun.LightEnergy = _sunEnergy * SunFactors[(int)sky];
            }

            _rain.Emitting = Drops[(int)sky] > 0;
            if (_rain.Emitting)
            {
                _rain.Amount = Drops[(int)sky];
            }
        }

        if (_rain.Emitting)
        {
            _rain.Position = target + new Vector3(0, RainHeight, 0);
            _process.EmissionBoxExtents = new Vector3(viewSize * 0.75f, 0.5f, viewSize * 0.75f);
            // Le vent penche les gouttes (renfort visuel ×1, en m/s).
            _process.Direction = new Vector3(wind.X / DropSpeed, -1f, wind.Y / DropSpeed).Normalized();
        }
    }
}
