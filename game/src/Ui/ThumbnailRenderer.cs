using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Vignettes rendues à partir des modèles 3D : un <see cref="SubViewport"/> hors écran par modèle (monde à part, lumière, fond rough),
/// dessiné une seule fois (<see cref="SubViewport.UpdateModeEnum.Once"/>) ; la texture reste en mémoire tant que le moteur de rendu vit.
/// Sert aux essences du tiroir Arbres, et servira aux bâtiments de C17.
/// </summary>
public partial class ThumbnailRenderer : Node
{
    public static readonly Vector2I Size = new(UiTheme.ThumbWidth * 2, UiTheme.ThumbArtHeight * 2);

    private static readonly Color Background = new("5f9148");

    private readonly Dictionary<string, Texture2D> _cache = [];

    /// <summary>La vignette du modèle <paramref name="scenePath"/> (un <c>.glb</c>), cadrée de trois quarts ; rendue au premier appel seulement.</summary>
    public Texture2D Render(string scenePath)
    {
        if (_cache.TryGetValue(scenePath, out var cached))
        {
            return cached;
        }

        var viewport = new SubViewport
        {
            Name = "Thumb" + _cache.Count,
            Size = Size,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Background,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.8f, 0.85f, 0.9f),
                AmbientLightEnergy = 0.8f,
            },
        });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50f, -35f, 0f), LightEnergy = 1.1f });

        var model = GD.Load<PackedScene>(scenePath).Instantiate<Node3D>();
        viewport.AddChild(model);
        var box = Bounds(model, Transform3D.Identity);
        var center = box.GetCenter();
        var radius = Math.Max(box.Size.Length() * 0.5f, 0.1f);

        // Trois quarts, un peu en plongée ; la sphère englobante est large : le modèle remplit à peu près la hauteur de l'image.
        const float fov = 30f;
        var eye = center + (new Vector3(0.6f, 0.45f, 1f).Normalized() * 0.75f * radius / Mathf.Sin(Mathf.DegToRad(fov * 0.5f)));
        viewport.AddChild(new Camera3D { Fov = fov, Transform = new Transform3D(Basis.LookingAt(center - eye, Vector3.Up), eye) });
        AddChild(viewport);

        var texture = viewport.GetTexture();
        _cache[scenePath] = texture;
        return texture;
    }

    /// <summary>Boîte englobante de tous les maillages sous <paramref name="node"/>, dans le repère de la racine.</summary>
    private static Aabb Bounds(Node node, Transform3D parent)
    {
        var transform = node is Node3D spatial ? parent * spatial.Transform : parent;
        Aabb? box = node is MeshInstance3D mesh ? transform * mesh.GetAabb() : null;
        foreach (var child in node.GetChildren())
        {
            var inner = Bounds(child, transform);
            if (inner.Size != Vector3.Zero)
            {
                box = box is { } b ? b.Merge(inner) : inner;
            }
        }

        return box ?? new Aabb();
    }
}
