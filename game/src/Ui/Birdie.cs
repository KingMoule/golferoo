using Godot;

namespace Golferoo.Game.Ui;

/// <summary>
/// Birdie (principe 8), le fil des messages du jeu à la Chirper : un panneau de la pile ouvert par son bouton rond, qui garde les
/// <see cref="Capacity"/> derniers messages, et un aperçu du dernier en haut à gauche pendant <see cref="PeekMilliseconds"/> ms.
/// Le bouton porte le compteur de non lus, remis à zéro à l'ouverture. Un clic sur un message lié à un membre suit son golfeur.
/// </summary>
public partial class Birdie : Node
{
    public const int Capacity = 50;
    public const float Width = 380f;
    private const ulong PeekMilliseconds = 8000;

    /// <summary>Qui parle : nom, pseudo et couleur de l'avatar.</summary>
    public readonly record struct Author(string Name, string Handle, Color Avatar);

    public static readonly Author Club = new("Le club", "@le_club", UiTheme.Sky);
    public static readonly Author Pro = new("Votre pro", "@pro_du_club", UiTheme.Grass);

    /// <summary>Un message ; <paramref name="MemberId"/> est le membre de la ligue qu'un clic suit, ou -1.</summary>
    public sealed record Chirp(Author Author, string Text, string Tag, string When, int MemberId);

    private readonly List<Chirp> _chirps = [];
    private Main _main = null!;
    private UiPanel _panel = null!;
    private VBoxContainer _feed = null!;
    private PanelContainer _peek = null!;
    private Container _peekSlot = null!;
    private Label _unread = null!;
    private ulong _peekUntil;

    /// <summary>Les messages gardés, du plus ancien au plus récent.</summary>
    public IReadOnlyList<Chirp> Chirps => _chirps;

    public int Unread { get; private set; }

    public bool IsOpen => _panel.Visible;

    /// <summary>Le panneau dans la pile (fermé), l'aperçu en haut à gauche (caché) et le compteur sur le bouton rond de la barre.</summary>
    public void Init(Main main)
    {
        _main = main;
        _panel = UiPanel.Create(UiIds.Panel.Birdie, "Birdie", "birdie", Width);
        _feed = new VBoxContainer { Name = UiIds.Hud.BirdieFeed, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _feed.AddThemeConstantOverride("separation", 0);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 340f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(_feed);
        _panel.Content.AddChild(scroll);
        main.Panels.Add(_panel);

        _peekSlot = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _peek = UiTheme.GlassFrame(UiIds.Hud.BirdiePeek, _peekSlot, 4);
        _peek.Visible = false;
        _peek.CustomMinimumSize = new Vector2(420f, 0f);
        (_peek.Position, _peek.MouseFilter) = (new Vector2(16f, 16f), Control.MouseFilterEnum.Ignore);
        main.Ui.AddChild(_peek);

        var button = main.Ui.Find(UiIds.Round.Birdie)!;
        _unread = new Label { Name = UiIds.Hud.BirdieUnread, Visible = false, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _unread.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.Flag, 9, UiTheme.Ink, 2));
        _unread.AddThemeColorOverride("font_color", UiTheme.Ink);
        _unread.AddThemeFontSizeOverride("font_size", UiTheme.MinSize);
        _unread.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        (_unread.OffsetLeft, _unread.OffsetTop, _unread.CustomMinimumSize) = (-12f, -6f, new Vector2(20f, 18f));
        button.AddChild(_unread);
    }

    /// <summary>Publie un message court et toujours informatif ; il s'affiche en aperçu, et compte comme non lu si le fil est fermé.</summary>
    public void Post(Author author, string text, string tag, int memberId = -1)
    {
        var date = _main.World.Date;
        var chirp = new Chirp(author, text, tag, $"{date.Day} {UiNames.Months[date.Month - 1]}", memberId);
        _chirps.Add(chirp);
        _feed.AddChild(Row(chirp));
        _feed.MoveChild(_feed.GetChild(-1), 0);
        if (_chirps.Count > Capacity)
        {
            _chirps.RemoveAt(0);
            Drop(_feed);
        }

        GD.Print($"BIRDIE {author.Handle} {text} {tag}");
        if (IsOpen)
        {
            return;
        }

        SetUnread(Unread + 1);
        Drop(_peekSlot);
        _peekSlot.AddChild(Row(chirp));
        _peek.Visible = true;
        _peekUntil = Time.GetTicksMsec() + PeekMilliseconds;
    }

    /// <summary>Bouton rond : ouvre le fil (les messages sont lus, l'aperçu disparaît) ou le ferme.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            _panel.EmitSignal(UiPanel.SignalName.Closed);
            return;
        }

        _main.Panels.Open(_panel);
        _peek.Visible = false;
        SetUnread(0);
    }

    public override void _Process(double delta)
    {
        if (_peek.Visible && Time.GetTicksMsec() >= _peekUntil)
        {
            _peek.Visible = false;
        }
    }

    /// <summary>Un clic sur un message : suit le golfeur du membre s'il est sur le parcours, sinon ouvre le fil.</summary>
    public void Click(Chirp chirp)
    {
        if (chirp.MemberId >= 0 && _main.FollowMember(chirp.MemberId))
        {
            return;
        }

        if (!IsOpen)
        {
            Toggle();
        }
    }

    /// <summary>Retire tout de suite le dernier enfant de <paramref name="parent"/>, s'il y en a un.</summary>
    private static void Drop(Node parent)
    {
        if (parent.GetChildCount() > 0)
        {
            var last = parent.GetChild(-1);
            parent.RemoveChild(last);
            last.QueueFree();
        }
    }

    private void SetUnread(int count)
    {
        Unread = count;
        _unread.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _unread.Visible = count > 0;
    }

    /// <summary>Avatar rond, nom, pseudo et date, puis le texte et son mot-clic en jaune (maquette v2).</summary>
    private PanelContainer Row(Chirp chirp)
    {
        var style = UiTheme.Box(Colors.Transparent, 0, new Color("F3ECD8", 0.08f), 0);
        (style.BorderWidthBottom, style.ContentMarginTop, style.ContentMarginBottom, style.ContentMarginLeft, style.ContentMarginRight) = (1, 10f, 10f, 4f, 4f);
        var row = new PanelContainer { Name = "Chirp", MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = chirp.MemberId >= 0 ? $"Suivre {chirp.Author.Name}" : string.Empty };
        row.AddThemeStyleboxOverride("panel", style);
        row.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                Click(chirp);
            }
        };

        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 12);
        row.AddChild(line);
        var avatar = new Panel { CustomMinimumSize = new Vector2(40f, 40f), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore };
        avatar.AddThemeStyleboxOverride("panel", UiTheme.Box(chirp.Author.Avatar, 20, UiTheme.Ink, 2));
        line.AddChild(avatar);

        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 3);
        line.AddChild(text);
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 6);
        text.AddChild(head);
        head.AddChild(new Label { Text = chirp.Author.Name, ThemeTypeVariation = "UiName" });
        var handle = new Label { Text = chirp.Author.Handle };
        handle.AddThemeColorOverride("font_color", UiTheme.Sky);
        head.AddChild(handle);
        head.AddChild(new Label { Text = "· " + chirp.When, ThemeTypeVariation = "UiCaption", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });

        var body = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{chirp.Text.Replace("[", "[lb]")} [color=#{UiTheme.Flag.ToHtml(false)}]{chirp.Tag}[/color]",
        };
        body.AddThemeColorOverride("default_color", new Color("E7DFC8"));
        text.AddChild(body);
        return row;
    }
}
