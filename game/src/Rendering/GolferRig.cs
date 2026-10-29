using Godot;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Le golfeur animé de tools/blender/golfer_anim.py (C23) : <see cref="ScenePath"/> chargé une fois, avec les données de ses extras.
/// Null si le fichier manque : <see cref="ActorLayer"/> ne montre alors que des silhouettes.
/// </summary>
public sealed class GolferModel
{
    public const string ScenePath = "res://assets/golfers/golfer.glb";
    public const string Walk = "walk";
    public const string Idle = "idle";
    public const string Swing = "golf_swing_2s";
    public const string Chip = "chip";
    public const string Putt = "putt";

    private readonly Dictionary<string, (double Impact, double Length)> _clips = [];

    private GolferModel(PackedScene scene) => Scene = scene;

    public PackedScene Scene { get; }

    /// <summary>Distance parcourue par cycle de marche, à l'échelle du modèle (m).</summary>
    public double WalkCycle { get; private set; }

    /// <summary>La balle à l'adresse, dans le repère du modèle (m).</summary>
    public Vector3 Ball { get; private set; }

    public int Bones { get; private set; }

    public double Impact(string clip) => _clips.TryGetValue(clip, out var c) ? c.Impact : 0.0;

    public double Length(string clip) => _clips.TryGetValue(clip, out var c) ? c.Length : 1.0;

    public bool Has(string clip) => _clips.ContainsKey(clip);

    public static GolferModel? Load()
    {
        if (!ResourceLoader.Exists(ScenePath) || GD.Load<PackedScene>(ScenePath) is not { } scene)
        {
            return null;
        }

        var root = scene.Instantiate();
        var model = new GolferModel(scene);
        var extras = root.FindChildren("*", "", true, false).FirstOrDefault(n => n.HasMeta("extras"))?.GetMeta("extras").AsGodotDictionary();
        var player = root.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (extras is not null && player is not null)
        {
            double Extra(string key) => extras.TryGetValue(key, out var v) ? v.AsDouble() : 0.0;
            model.WalkCycle = Extra("walk_cycle_m");
            model.Ball = new Vector3((float)Extra("ball_x"), 0f, (float)Extra("ball_z"));
            foreach (var name in player.GetAnimationList())
            {
                model._clips[name] = (Extra("impact_" + name), player.GetAnimation(name).Length);
            }
        }

        model.Bones = root.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault()?.GetBoneCount() ?? 0;
        root.Free();
        return model;
    }
}

/// <summary>
/// Un golfeur animé du pool d'<see cref="ActorLayer"/> : une instance de <see cref="GolferModel"/> dont l'AnimationPlayer est en mode manuel.
/// La couche lui donne un clip et un instant à chaque image ; rendu au pool, il est caché et ne coûte rien.
/// </summary>
public partial class GolferRig : Node3D
{
    private AnimationPlayer? _player;

    public string Clip { get; private set; } = string.Empty;

    public double Time { get; private set; }

    public static GolferRig New(GolferModel model, int index)
    {
        var rig = new GolferRig { Name = $"GolferRig{index}" };
        var root = model.Scene.Instantiate<Node3D>();
        rig.AddChild(root);
        rig._player = root.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (rig._player is not null)
        {
            rig._player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        }

        foreach (var mesh in root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; // comme les silhouettes du MultiMesh
        }

        return rig;
    }

    /// <summary>Joue <paramref name="clip"/> à l'instant <paramref name="time"/> (secondes).</summary>
    public void Pose(string clip, double time)
    {
        if (_player is null)
        {
            return;
        }

        if (clip != Clip)
        {
            _player.Play(clip);
            Clip = clip;
        }

        Time = time;
        _player.Seek(time, true);
    }
}
