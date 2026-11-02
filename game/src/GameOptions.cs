using Godot;

namespace Golferoo.Game;

/// <summary>
/// Options du joueur (C18 lot 3), lues et écrites dans <c>user://options.cfg</c> : résolution, plein écran, échelle d'interface, pause automatique.
/// Ce sont des préférences de la machine, pas un état de partie : rien n'entre dans la sim, la sauvegarde ni la somme de contrôle.
/// Le fichier se lit avec tolérance : absent, partiel ou corrompu, chaque valeur manquante ou invalide retombe sur sa valeur par défaut.
/// </summary>
public static class GameOptions
{
    private const string Section = "options";

    public static readonly Vector2I[] Resolutions = [new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440)];

    /// <summary>Échelles de l'interface : le facteur de C13 (150 % : panneaux repliés).</summary>
    public static readonly float[] Scales = [1f, 1.25f, 1.5f];

    /// <summary>Messages importants qui peuvent mettre la partie en pause : identifiant de l'évènement de la sim, texte affiché. Liste fermée.</summary>
    public static readonly (string Id, string Label)[] PauseMessages =
    [
        ("abandon", "Un golfeur abandonne"),
        ("faillite", "Le club est en faillite"),
        ("objectif", "Un objectif est accompli"),
        ("tresorerie", "La trésorerie est trop basse pour un achat"),
        ("conseil", "Votre pro donne un conseil"),
    ];

    public static string FilePath { get; set; } = "user://options.cfg";

    public static Vector2I Resolution { get; set; } = Resolutions[0];

    public static bool Fullscreen { get; set; }

    public static float UiScale { get; set; } = 1f;

    public static bool AutoPause { get; set; } = true;

    /// <summary>Les conseils du pro s'affichent (bulle et pause automatique) ; la sim les évalue de toute façon (C19).</summary>
    public static bool ProAdvice { get; set; } = true;

    /// <summary>Volumes de 0 à 100 % (C20) : général, puis les bus Musique, Effets et Ambiance.</summary>
    public static int MasterVolume { get; set; } = 100;

    public static int MusicVolume { get; set; } = 50;

    public static int SfxVolume { get; set; } = 80;

    public static int AmbienceVolume { get; set; } = 60;

    static GameOptions() => Load();

    /// <summary>Lit le fichier ; toute valeur absente ou invalide reprend sa valeur par défaut.</summary>
    public static void Load()
    {
        (Resolution, Fullscreen, UiScale, AutoPause, ProAdvice) = (Resolutions[0], false, 1f, true, true);
        (MasterVolume, MusicVolume, SfxVolume, AmbienceVolume) = (100, 50, 80, 60);
        var file = new ConfigFile();
        if (file.Load(FilePath) != Error.Ok)
        {
            return;
        }

        var size = new Vector2I(Int(file, "width"), Int(file, "height"));
        if (Array.IndexOf(Resolutions, size) >= 0)
        {
            Resolution = size;
        }

        var scale = file.GetValue(Section, "ui_scale", 1f);
        if (scale.VariantType is Variant.Type.Float or Variant.Type.Int && Array.IndexOf(Scales, scale.AsSingle()) >= 0)
        {
            UiScale = scale.AsSingle();
        }

        Fullscreen = Flag(file, "fullscreen", false);
        AutoPause = Flag(file, "auto_pause", true);
        ProAdvice = Flag(file, "pro_advice", true);
        MasterVolume = Volume(file, "master_volume", 100);
        MusicVolume = Volume(file, "music_volume", 50);
        SfxVolume = Volume(file, "sfx_volume", 80);
        AmbienceVolume = Volume(file, "ambience_volume", 60);
    }

    public static void Save()
    {
        var file = new ConfigFile();
        file.SetValue(Section, "width", Resolution.X);
        file.SetValue(Section, "height", Resolution.Y);
        file.SetValue(Section, "fullscreen", Fullscreen);
        file.SetValue(Section, "ui_scale", UiScale);
        file.SetValue(Section, "auto_pause", AutoPause);
        file.SetValue(Section, "pro_advice", ProAdvice);
        file.SetValue(Section, "master_volume", MasterVolume);
        file.SetValue(Section, "music_volume", MusicVolume);
        file.SetValue(Section, "sfx_volume", SfxVolume);
        file.SetValue(Section, "ambience_volume", AmbienceVolume);
        if (file.Save(FilePath) != Error.Ok)
        {
            GD.PrintErr($"Options non écrites : {FilePath}");
        }
    }

    /// <summary>Résolution, plein écran et échelle sur la fenêtre. En fenêtre seulement, la taille suit la résolution choisie.</summary>
    public static void Apply(Window window)
    {
        window.Mode = Fullscreen ? Window.ModeEnum.ExclusiveFullscreen : Window.ModeEnum.Windowed;
        if (!Fullscreen)
        {
            window.Size = Resolution;
        }

        window.ContentScaleFactor = UiScale;
        ApplyAudio();
    }

    /// <summary>Règle les bus Master, Musique, Effets et Ambiance (un bus absent est ignoré).</summary>
    public static void ApplyAudio()
    {
        foreach (var (bus, percent) in new[] { ("Master", MasterVolume), ("Musique", MusicVolume), ("Effets", SfxVolume), ("Ambiance", AmbienceVolume) })
        {
            var index = AudioServer.GetBusIndex(bus);
            if (index >= 0)
            {
                AudioServer.SetBusVolumeDb(index, percent == 0 ? -80f : Mathf.LinearToDb(percent / 100f));
            }
        }
    }

    /// <summary>Vrai si l'évènement est dans la liste fermée et que la pause automatique est active.</summary>
    public static bool PausesOn(string id) => AutoPause && Array.Exists(PauseMessages, m => m.Id == id);

    private static int Int(ConfigFile file, string key)
    {
        var value = file.GetValue(Section, key, 0);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static int Volume(ConfigFile file, string key, int fallback)
    {
        var value = file.GetValue(Section, key, fallback);
        return value.VariantType == Variant.Type.Int && value.AsInt32() is >= 0 and <= 100 ? value.AsInt32() : fallback;
    }

    private static bool Flag(ConfigFile file, string key, bool fallback)
    {
        var value = file.GetValue(Section, key, fallback);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }
}
