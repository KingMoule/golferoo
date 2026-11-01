using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Tutorial;

namespace Golferoo.Game.Tutorial;

/// <summary>
/// Bulle de votre pro pour le tutoriel de C07, hors de la pile de panneaux : proposition au démarrage (Oui / Plus tard), accueil, puis une
/// étape à la fois, lue dans <see cref="TutorialSteps.Current"/> (la sim garde l'état, la bulle ne retient que sa phase). Chaque étape se fait
/// aux boutons : le texte ne cite aucune touche, et un anneau jaune qui pulse entoure le bouton attendu (<see cref="Highlighted"/>). Aucune règle ici.
/// </summary>
public partial class TutorialPanel : Node
{
    public enum Mode
    {
        Off,
        Offer,
        Welcome,
        Running,
        Finished,
    }

    private const int StepCount = 11;
    private const float RingPad = 5f;

    private Main? _main;
    private PanelContainer? _panel;
    private RichTextLabel? _text;
    private Button? _yes;
    private Button? _later;
    private Button? _start;
    private Button? _skip;
    private Button? _close;
    private Panel? _ring;
    private GhostOutline? _ghost;
    private string _shown = string.Empty;

    public Mode Phase { get; private set; }

    /// <summary>Texte affiché en ce moment (balises comprises), pour la fumée et les captures.</summary>
    public string Text => _shown;

    /// <summary>L'identifiant (<see cref="UiIds"/>) du bouton que l'anneau entoure, ou <c>null</c>.</summary>
    public string? Highlighted { get; private set; }

    public void Init(Main main, Mode mode)
    {
        _main = main;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        var badge = UiPanel.Badge("figurine", 44);
        badge.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.AddChild(badge);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        row.AddChild(box);
        box.AddChild(new Label { Text = "Votre pro", ThemeTypeVariation = "UiTitle" });
        _text = new RichTextLabel { Name = UiIds.Tutorial.Text, BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(480f, 0f) };
        _text.AddThemeColorOverride("default_color", UiTheme.Cream);
        _text.AddThemeFontOverride("bold_font", UiTheme.NunitoHeavy);
        box.AddChild(_text);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        box.AddChild(buttons);
        _yes = Button(UiIds.Tutorial.Yes, "Oui", Accept, buttons);
        _later = Button(UiIds.Tutorial.Later, "Plus tard", Dismiss, buttons);
        _start = Button(UiIds.Tutorial.Start, "Commencer", Start, buttons);
        _skip = Button(UiIds.Tutorial.Skip, "Passer le tutoriel", Dismiss, buttons);
        _close = Button(UiIds.Tutorial.Close, "Fermer", Dismiss, buttons);

        _panel = UiTheme.GlassFrame(UiIds.Tutorial.Bubble, row, 16);
        _panel.Visible = false;
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.OffsetTop = 100f; // sous les pastilles du trou et des messages
        main.Ui.AddChild(_panel);

        var ringStyle = UiTheme.Box(Colors.Transparent, UiTheme.ToolRadius + (int)RingPad, UiTheme.Flag, 3);
        _ring = new Panel { Name = UiIds.Tutorial.Ring, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _ring.AddThemeStyleboxOverride("panel", ringStyle);
        main.Ui.AddChild(_ring);

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
        if (_main is null)
        {
            return;
        }

        if (Phase == Mode.Running)
        {
            var world = _main.World;
            var step = TutorialSteps.Current(world);
            if (step == TutorialStep.Done)
            {
                SetPhase(Mode.Finished);
            }
            else
            {
                var text = StepText(step, world, world.Terrain!);
                if (text != _shown)
                {
                    Say(text);
                }

                Highlighted = Target(step);
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
        }

        PlaceRing();
    }

    /// <summary>Le bouton attendu à l'étape <paramref name="step"/> : la famille d'outils d'abord, puis la vignette ou l'action du tiroir.</summary>
    private string? Target(TutorialStep step)
    {
        var main = _main!;
        var family = main.Bar.Current;
        var trees = main.TreeTool;
        return step switch
        {
            TutorialStep.OpenHole or TutorialStep.WatchPair or TutorialStep.FirstGreenFee or TutorialStep.ReadThought => null,
            TutorialStep.PlaceSnackBar => family != ToolFamily.Buildings ? UiIds.Tool.Buildings : UiIds.Action.Building(nameof(BuildingKind.SnackBar)),
            TutorialStep.ClearTrees => family != ToolFamily.Trees ? UiIds.Tool.Trees : trees.SelectedId < 0 ? UiIds.Action.TreeSelect : UiIds.Action.TreeRaze,
            TutorialStep.PlantSapling => family != ToolFamily.Trees ? UiIds.Tool.Trees : UiIds.Thumb.Tree(trees.Species.ToString(), nameof(TreePlantStage.Sapling)),
            TutorialStep.PaintTee => family != ToolFamily.Terrain ? UiIds.Tool.Terrain : UiIds.Thumb.Tee,
            TutorialStep.PaintFairway => family != ToolFamily.Terrain ? UiIds.Tool.Terrain : UiIds.Thumb.Fairway,
            TutorialStep.PaintGreen => family != ToolFamily.Terrain ? UiIds.Tool.Terrain : UiIds.Thumb.Green,
            _ => family != ToolFamily.Holes ? UiIds.Tool.Holes : UiIds.Action.HolePose,
        };
    }

    /// <summary>L'anneau jaune autour du bouton <see cref="Highlighted"/>, qui pulse ; caché si le bouton n'est pas à l'écran.</summary>
    private void PlaceRing()
    {
        var target = Highlighted is null ? null : _main!.Ui.Find(Highlighted);
        _ring!.Visible = target is not null && target.IsVisibleInTree();
        if (!_ring.Visible)
        {
            return;
        }

        var rect = target!.GetGlobalRect().Grow(RingPad);
        _ring.GlobalPosition = rect.Position;
        _ring.Size = rect.Size;
        _ring.Modulate = new Color(1f, 1f, 1f, 0.6f + (0.4f * Mathf.Sin((float)Time.GetTicksMsec() * 0.006f)));
    }

    private static Button Button(string name, string text, Action pressed, Container parent)
    {
        var button = new Button { Name = name, Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    private void SetPhase(Mode mode)
    {
        Phase = mode;
        Highlighted = mode == Mode.Finished ? UiIds.Tutorial.Close : null;
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
                Say(UiNames.TutorialDone);
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
            TutorialStep.ClearTrees => $"des arbres encombrent le passage ({TutorialSteps.CorridorTreesLeft(world, terrain)} encerclés de rouge). Cliquez sur [b]Arbres[/b], puis sur [b]Sélectionner[/b] et sur un arbre, enfin sur [b]Raser[/b] ([b]Raser une zone[/b] en abat plusieurs d'un coup).",
            TutorialStep.PlantSapling => "plantez une pousse. Dans le tiroir [b]Arbres[/b], cliquez sur un plant, puis sur le rough ; vous pouvez en planter plusieurs, de chaque essence.",
            TutorialStep.PaintTee => "peignez le départ. Cliquez sur [b]Terrain[/b], puis sur [b]Départ[/b], et maintenez le clic gauche sur le contour blanc (il n'est qu'indicatif).",
            TutorialStep.PaintFairway => "peignez le fairway. Choisissez [b]Fairway[/b] et suivez le contour vert ; le curseur [b]Taille du pinceau[/b] change le rayon.",
            TutorialStep.PaintGreen => "peignez le green. Choisissez [b]Green[/b] et couvrez le contour turquoise.",
            TutorialStep.PlaceTeeAndHole => "posez le départ et le trou. Cliquez sur [b]Trous[/b], puis sur le départ, enfin sur le green." + Missing(terrain),
            _ => UiNames.TutorialClub(step),
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
