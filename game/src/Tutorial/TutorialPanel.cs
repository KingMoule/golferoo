using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Tutorial;

namespace Golferoo.Game.Tutorial;

/// <summary>
/// Bulle de votre pro pour le tutoriel de C07 : proposition au démarrage (Oui / Plus tard), accueil, puis une étape à la fois, lue dans
/// <see cref="TutorialSteps.Current"/> (la sim garde l'état, la bulle ne retient que sa phase). Aucune règle ici.
/// </summary>
public partial class TutorialPanel : CanvasLayer
{
    public enum Mode
    {
        Off,
        Offer,
        Welcome,
        Running,
        Finished,
    }

    private const int StepCount = 6;

    private Main? _main;
    private PanelContainer? _panel;
    private RichTextLabel? _text;
    private Button? _yes;
    private Button? _later;
    private Button? _start;
    private Button? _skip;
    private Button? _close;
    private GhostOutline? _ghost;
    private string _shown = string.Empty;

    public Mode Phase { get; private set; }

    /// <summary>Texte affiché en ce moment (balises comprises), pour la fumée et les captures.</summary>
    public string Text => _shown;

    public void Init(Main main, Mode mode)
    {
        _main = main;
        _panel = new PanelContainer { Name = "TutorialBubble", Visible = false };
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.OffsetLeft = -270f;
        _panel.OffsetRight = 270f;
        _panel.OffsetTop = 92f;
        var box = new VBoxContainer();
        _text = new RichTextLabel { Name = "TutorialText", BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(520f, 0f) };
        var buttons = new HBoxContainer();
        _yes = Button("TutorialYes", "Oui", Accept, buttons);
        _later = Button("TutorialLater", "Plus tard", Dismiss, buttons);
        _start = Button("TutorialStart", "Commencer", Start, buttons);
        _skip = Button("TutorialSkip", "Passer le tutoriel", Dismiss, buttons);
        _close = Button("TutorialClose", "Fermer", Dismiss, buttons);
        box.AddChild(_text);
        box.AddChild(buttons);
        _panel.AddChild(box);
        AddChild(_panel);

        if (main.World.Terrain!.Blank)
        {
            // Contours du vrai coude, lus dans son fichier : on reconstruit le trou que le jeu montre d'habitude.
            _ghost = new GhostOutline { Name = "GhostOutline" };
            main.AddChild(_ghost);
            _ghost.Init(main, HoleLoader.Parse(Main.ReadHoleJson("data/holes/dogleg-par4.json"), main.Factor));
        }

        SetPhase(mode);
    }

    public override void _Process(double delta)
    {
        if (Phase != Mode.Running || _main is null)
        {
            return;
        }

        var world = _main.World;
        var step = TutorialSteps.Current(world);
        if (step == TutorialStep.Done)
        {
            SetPhase(Mode.Finished);
            return;
        }

        var terrain = world.Terrain!;
        var text = StepText(step, world, terrain);
        if (text != _shown)
        {
            Say(text);
        }

        _ghost?.Show(
            step switch
            {
                TutorialStep.PaintTee => [Surface.Tee],
                TutorialStep.PaintFairway => [Surface.Fairway],
                TutorialStep.PaintGreen => [Surface.Green],
                TutorialStep.PlaceTeeAndHole => [Surface.Tee, Surface.Green],
                _ => [],
            },
            step == TutorialStep.ClearTrees);
    }

    private static Button Button(string name, string text, Action pressed, Container parent)
    {
        var button = new Button { Name = name, Text = text };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    private void SetPhase(Mode mode)
    {
        Phase = mode;
        _panel!.Visible = mode != Mode.Off;
        _yes!.Visible = mode == Mode.Offer;
        _later!.Visible = mode == Mode.Offer;
        _start!.Visible = mode == Mode.Welcome;
        _skip!.Visible = mode is Mode.Welcome or Mode.Running;
        _close!.Visible = mode == Mode.Finished;
        _ghost?.Show([], false);
        switch (mode)
        {
            case Mode.Offer:
                Say("Bonjour ! Je suis votre pro. Voulez-vous construire votre premier trou avec moi ? Je vous guide pas à pas, et vous pouvez aussi explorer le jeu seul.");
                break;
            case Mode.Welcome:
                Say("Bonjour, je suis votre pro. Ce terrain est vierge : construisons ensemble votre premier trou. Quand vous êtes prêt, cliquez sur Commencer.");
                break;
            case Mode.Finished:
                var terrain = _main!.World.Terrain!;
                var meters = Math.Sqrt(Math.Pow(terrain.Hole.X - terrain.Tee.X, 2) + Math.Pow(terrain.Hole.Z - terrain.Tee.Z, 2));
                Say($"[b]Le trou est valide ![/b] Par {terrain.Par}, {meters:0} m. Lancez des golfeurs avec [b]G[/b] et regardez la première paire le jouer. Bon jeu !");
                break;
            default:
                break;
        }
    }

    private void Say(string text)
    {
        _shown = text;
        _text!.Text = text;
    }

    private void Accept()
    {
        // Le tutoriel est un autre monde (la terre vierge) : on recharge la scène avec le mode demandé.
        Main.TutorialMode = true;
        GetTree().ChangeSceneToFile("res://Main.tscn");
    }

    private void Dismiss() => SetPhase(Mode.Off);

    /// <summary>Commence le tutoriel (le bouton « Commencer »).</summary>
    public void Start() => SetPhase(Mode.Running);

    private static string StepText(TutorialStep step, World world, TerrainGrid terrain)
    {
        var head = $"[b]Étape {(int)step + 1} sur {StepCount}[/b] — ";
        return head + step switch
        {
            TutorialStep.ClearTrees => $"des arbres encombrent le passage ({TutorialSteps.CorridorTreesLeft(world, terrain)} encerclés de rouge). Appuyez sur [b]T[/b], cliquez sur un arbre puis sur [b]R[/b] pour le raser ([b]X[/b] rase toute une zone).",
            TutorialStep.PlantSapling => "plantez une pousse. Avec l'outil arbres ([b]T[/b]), appuyez sur [b]P[/b] puis cliquez sur le rough ; vous pouvez en planter plusieurs ([b]Y[/b] change d'essence).",
            TutorialStep.PaintTee => "peignez le départ. Appuyez sur [b]B[/b], choisissez Tee avec [b]F1[/b], puis maintenez le clic gauche sur le contour blanc (il n'est qu'indicatif).",
            TutorialStep.PaintFairway => "peignez le fairway. Choisissez Fairway avec [b]F3[/b] et suivez le contour vert ; [b]\\[[/b] et [b]\\][/b] changent le rayon du pinceau.",
            TutorialStep.PaintGreen => "peignez le green. Choisissez Green avec [b]F2[/b] et couvrez le contour turquoise.",
            TutorialStep.PlaceTeeAndHole => "posez le départ et le trou. Appuyez sur [b]V[/b], cliquez sur le départ, puis sur le green." + Missing(terrain),
            _ => string.Empty,
        };
    }

    private static string Missing(TerrainGrid terrain)
    {
        var issues = HoleCheck.Check(terrain);
        return issues.Count == 0 ? string.Empty : " Il manque : " + string.Join(", ", issues.Select(Describe)) + ".";
    }

    private static string Describe(HoleIssue issue) => issue switch
    {
        HoleIssue.TeeTooSmall => "un départ plus grand",
        HoleIssue.FairwayTooShort => "plus de fairway",
        HoleIssue.GreenTooSmall => "un green plus grand",
        HoleIssue.TeeNotPlaced => "le départ posé sur le Tee",
        HoleIssue.HoleNotOnGreen => "le trou posé sur le green",
        HoleIssue.TooClose => $"le trou à {HoleCheck.MinDistance:0} m du départ au moins",
        _ => "un chemin de marche du départ au trou",
    };
}
