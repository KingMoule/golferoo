using Godot;
using Golferoo.Game.Tools;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;

namespace Golferoo.Game.Ui;

/// <summary>
/// Les golfeurs vus de près (C16) : une bulle de pensée de <see cref="BubbleMilliseconds"/> ms au-dessus du golfeur quand son humeur change (au plus
/// <see cref="MaxBubbles"/> à la fois, une option du panneau Birdie les coupe), l'info-bulle au survol, la fiche au clic sur un golfeur hors de la file,
/// et les messages de Birdie pour l'adhésion et l'ouverture de la ligue. L'historique des pensées de la fiche est celui de la session : il n'est pas sauvé.
/// </summary>
public partial class Thoughts : Node
{
    public const int MaxBubbles = 8;
    public const int HistoryLength = 5;
    public const ulong BubbleMilliseconds = 3000;
    private const float BubbleHeight = 2.3f;

    /// <summary>Les bulles sont coupées : préférence d'interface de la session, hors sauvegarde et somme de contrôle (comme la sourdine de Birdie).</summary>
    public static bool Off { get; private set; }

    /// <summary>Le bouton du panneau Birdie qui reflète le réglage.</summary>
    internal static Button? Mirror { get; set; }

    private sealed record Bubble(Control Node, int GolferId, ulong Until);

    private readonly List<Bubble> _bubbles = [];
    private readonly Dictionary<int, List<(string Text, int Delta)>> _history = [];
    private Main _main = null!;
    private PanelContainer _tip = null!;
    private UiPanel _card = null!;
    private int _cardVisitor = -1;
    private (int? Mood, int Points, int Games, MemberLevel Level) _cardKey;
    private bool _leagueOpen;

    /// <summary>Les bulles ouvertes.</summary>
    public int BubbleCount => _bubbles.Count;

    /// <summary>Les bulles ouvertes qui se voient : celles des golfeurs hors de l'écran sont cachées.</summary>
    public int VisibleBubbleCount => _bubbles.Count(b => b.Node.Visible);

    public bool TipVisible => _tip.Visible;

    public bool CardOpen => _card.Visible;

    public int CardVisitor => _cardVisitor;

    public static void SetOff(bool off)
    {
        Off = off;
        if (Mirror is { } button && GodotObject.IsInstanceValid(button))
        {
            button.SetPressedNoSignal(off);
            button.Text = off ? "Pensées coupées" : "Couper les pensées";
            button.TooltipText = off ? "Les golfeurs ne montrent plus leurs pensées ; elles restent dans leur fiche. Cliquer pour rétablir." : "Les golfeurs ne montrent plus leurs pensées au-dessus de leur tête ; elles restent dans leur fiche.";
        }
    }

    public void Init(Main main)
    {
        _main = main;
        var world = main.World;
        _leagueOpen = world.League?.Open ?? false;
        world.MoodChanged += OnMoodChanged;
        world.MemberJoined += OnJoined;
        world.MemberPromoted += OnPromoted;
        world.MemberResigned += OnResigned;

        _tip = new PanelContainer { Name = UiIds.Hud.GolferTip, ThemeTypeVariation = "TooltipPanel", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, ZIndex = 5 };
        main.Ui.AddChild(_tip);
        _card = UiPanel.Create(UiIds.Panel.Golfer, "Golfeur", "figurine", 320f);
        main.Panels.Add(_card);
        _card.Closed += () => _cardVisitor = -1;
    }

    public override void _ExitTree()
    {
        if (_main?.World is { } world)
        {
            world.MoodChanged -= OnMoodChanged;
            world.MemberJoined -= OnJoined;
            world.MemberPromoted -= OnPromoted;
            world.MemberResigned -= OnResigned;
        }
    }

    // ---- Bulles ----

    private void OnMoodChanged(MoodChange change)
    {
        if (change.Delta != 0 && change.Golfer.VisitorId >= 0) // l'humeur n'a pas bougé : ni bulle ni trace
        {
            Think(change.Golfer.Id, change.Golfer.VisitorId, _main.World.Moods.Definitions[(int)change.Event].Thought, change.Delta);
        }
    }

    /// <summary>Le golfeur pense <paramref name="text"/> : la pensée entre dans sa fiche, et une bulle s'ouvre sauf si les bulles sont coupées.</summary>
    internal void Think(int golferId, int visitorId, string text, int delta)
    {
        if (!_history.TryGetValue(visitorId, out var list))
        {
            _history[visitorId] = list = [];
        }

        list.Add((text, delta));
        if (list.Count > HistoryLength)
        {
            list.RemoveAt(0);
        }

        if (_card.Visible && _cardVisitor == visitorId)
        {
            BuildCard(visitorId);
        }

        if (!Off)
        {
            Show(golferId, text, delta);
        }
    }

    internal void ClearBubbles()
    {
        foreach (var bubble in _bubbles.ToList())
        {
            Remove(bubble);
        }
    }

    private bool OnScreen(int golferId) => ScreenOf(golferId) is { } at && _main.GetViewport().GetVisibleRect().HasPoint(at);

    private void Show(int golferId, string text, int delta)
    {
        foreach (var old in _bubbles.Where(b => b.GolferId == golferId).ToList())
        {
            Remove(old);
        }

        // Au plafond, une bulle cachée (golfeur hors de l'écran) cède sa place ; sinon la plus ancienne, sauf pour un golfeur qu'on ne voit pas.
        if (_bubbles.Count >= MaxBubbles)
        {
            var hidden = _bubbles.FirstOrDefault(b => !b.Node.Visible);
            if (hidden is null && !OnScreen(golferId))
            {
                return;
            }

            Remove(hidden ?? _bubbles[0]);
        }

        var color = delta > 0 ? UiTheme.Grass : UiTheme.Brick;
        var box = new PanelContainer { Name = UiIds.Hud.ThoughtBubble, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 4 };
        box.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTheme.TooltipBg, 0.92f), 12, color, 2));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        var sign = new Label { Text = delta > 0 ? $"+{delta}" : $"−{-delta}", ThemeTypeVariation = "UiValue" };
        sign.AddThemeColorOverride("font_color", color);
        row.AddChild(sign);
        row.AddChild(new Label { Text = text, ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(150f, 0f) });
        box.AddChild(row);
        _main.Ui.AddChild(box);
        _bubbles.Add(new Bubble(box, golferId, Time.GetTicksMsec() + BubbleMilliseconds));
        Place(_bubbles[^1]);
    }

    private void Remove(Bubble bubble)
    {
        _bubbles.Remove(bubble);
        bubble.Node.GetParent()?.RemoveChild(bubble.Node);
        bubble.Node.QueueFree();
    }

    private Vector2? ScreenOf(int golferId)
    {
        var view = _main.Camera.View;
        if (view is null || !view.IsInsideTree())
        {
            return null;
        }

        var ground = _main.Actors.PositionOf(golferId) ?? GolferPosition(golferId);
        var head = ground + (Vector3.Up * BubbleHeight);
        return view.IsPositionBehind(head) ? null : view.UnprojectPosition(head);
    }

    private Vector3 GolferPosition(int golferId)
    {
        var golfer = _main.World.Groups.SelectMany(g => g.Golfers).FirstOrDefault(g => g.Id == golferId);
        return golfer is null ? Vector3.Zero : new Vector3((float)golfer.Position.X, (float)_main.World.Terrain!.Sample(golfer.Position.X, golfer.Position.Z), (float)golfer.Position.Z);
    }

    private void Place(Bubble bubble)
    {
        var shown = OnScreen(bubble.GolferId);
        bubble.Node.Visible = shown;
        if (shown)
        {
            bubble.Node.Position = ScreenOf(bubble.GolferId)!.Value - new Vector2(bubble.Node.Size.X / 2f, bubble.Node.Size.Y + 6f);
        }
    }

    public override void _Process(double delta)
    {
        var now = Time.GetTicksMsec();
        foreach (var bubble in _bubbles.ToList())
        {
            if (now >= bubble.Until)
            {
                Remove(bubble);
            }
            else
            {
                Place(bubble);
            }
        }

        // Les bulles d'un même groupe se superposent : chacune monte au-dessus de celles déjà posées qu'elle recouvre.
        var placed = new List<Rect2>();
        foreach (var bubble in _bubbles.Where(b => b.Node.Visible))
        {
            var rect = new Rect2(bubble.Node.Position, bubble.Node.Size);
            while (placed.FirstOrDefault(p => p.Intersects(rect)) is { Size.X: > 0f } hit)
            {
                rect.Position = new Vector2(rect.Position.X, hit.Position.Y - rect.Size.Y - 2f);
            }

            bubble.Node.Position = rect.Position;
            placed.Add(rect);
        }

        // La jauge de la fiche suit l'humeur, qui baisse aussi sans évènement (un peu tous les quelques trous), et le niveau et les parties à la fin d'une partie.
        if (_card.Visible && _cardVisitor >= 0 && CardKey(_cardVisitor) != _cardKey)
        {
            BuildCard(_cardVisitor);
        }

        UpdateTip();
    }

    private (int? Mood, int Points, int Games, MemberLevel Level) CardKey(int visitorId)
    {
        var visitor = _main.World.Visitors.Visitors[visitorId];
        var golfer = _main.World.Groups.SelectMany(g => g.Golfers).FirstOrDefault(g => g.VisitorId == visitorId);
        return (golfer?.Mood, visitor.Points, visitor.Games, visitor.Level);
    }

    // ---- Info-bulle ----

    /// <summary>L'info-bulle reste là où <see cref="Tip"/> l'a mise (captures), au lieu de suivre la souris.</summary>
    internal bool Pinned { get; set; }

    private void UpdateTip()
    {
        if (Pinned)
        {
            return;
        }

        var viewport = _main.GetViewport();
        if (_main.Brush.Active || _main.TreeTool.Active || _main.ProAim.Active || _main.PlaceTool.Active || viewport.GuiGetHoveredControl() is not null
            || _main.PickGolfer(viewport.GetMousePosition(), _ => true) is not var (_, golfer))
        {
            _tip.Visible = false;
            return;
        }

        Tip(golfer, viewport.GetMousePosition());
    }

    /// <summary>Montre l'info-bulle de <paramref name="golfer"/> près de <paramref name="at"/> : nom, niveau d'adhésion, humeur.</summary>
    public void Tip(Golfer golfer, Vector2 at)
    {
        foreach (var child in _tip.GetChildren())
        {
            _tip.RemoveChild(child);
            child.QueueFree();
        }

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = NameOf(golfer), ThemeTypeVariation = "UiName" });
        if (VisitorOf(golfer) is { } visitor)
        {
            box.AddChild(new Label { Text = UiNames.Levels[(int)visitor.Level], ThemeTypeVariation = "UiCaption" });
            box.AddChild(MoodGauge(golfer.Mood));
        }

        _tip.AddChild(box);
        _tip.Visible = true;
        _tip.Position = at + new Vector2(18f, 18f);
        _tip.ResetSize();
    }

    // ---- Fiche ----

    /// <summary>Un clic sur un golfeur ouvre sa fiche (les golfeurs de la file gardent le clic de <c>JoinQueuedGroup</c>). Faux si le clic ne tombe sur aucun visiteur du vivier.</summary>
    public bool OpenCardAt(Vector2 screen)
    {
        if (_main.PickGolfer(screen, _ => true) is not var (_, golfer) || golfer.VisitorId < 0)
        {
            return false;
        }

        OpenCard(golfer.VisitorId);
        return true;
    }

    public void OpenCard(int visitorId)
    {
        _cardVisitor = visitorId;
        BuildCard(visitorId);
        _main.Panels.Open(_card);
    }

    private void BuildCard(int visitorId)
    {
        _cardKey = CardKey(visitorId);
        var visitor = _main.World.Visitors.Visitors[visitorId];
        var golfer = _main.World.Groups.SelectMany(g => g.Golfers).FirstOrDefault(g => g.VisitorId == visitorId);
        foreach (var child in _card.Content.GetChildren())
        {
            _card.Content.RemoveChild(child);
            child.QueueFree();
        }

        _card.Title.Text = visitor.Name;
        var body = _card.Content;
        body.AddChild(new Label { Text = visitor.Resigned ? "A démissionné" : UiNames.Levels[(int)visitor.Level], ThemeTypeVariation = "UiName" });
        var thresholds = _main.World.Memberships.Thresholds;
        var next = thresholds.Cast<int?>().FirstOrDefault(t => t > visitor.Points);
        body.AddChild(new Label { Text = next is { } n ? $"{visitor.Points} points d'adhésion (prochain niveau à {n})" : $"{visitor.Points} points d'adhésion", ThemeTypeVariation = "UiCaption" });
        body.AddChild(new Label { Text = visitor.Games == 1 ? "1 partie jouée" : $"{visitor.Games} parties jouées", ThemeTypeVariation = "UiCaption" });
        body.AddChild(golfer is null
            ? new Label { Text = "Pas sur le parcours : l'humeur reprend à l'arrivée.", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart }
            : MoodGauge(golfer.Mood));
        body.AddChild(new Label { Text = "Dernières pensées", ThemeTypeVariation = "UiKey" });
        if (!_history.TryGetValue(visitorId, out var list) || list.Count == 0)
        {
            body.AddChild(new Label { Text = "Rien à signaler pour l'instant.", ThemeTypeVariation = "UiCaption" });
            return;
        }

        foreach (var (text, delta) in Enumerable.Reverse(list))
        {
            var line = new Label { Text = $"{(delta > 0 ? "+" : "−")}{Math.Abs(delta)}  {text}", ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(280f, 0f) };
            line.AddThemeColorOverride("font_color", delta > 0 ? UiTheme.Grass : UiTheme.Brick);
            body.AddChild(line);
        }
    }

    /// <summary>Humeur de −10 à +10 : jauge rouge sous 2, jaune sous 5, verte au-delà (le golfeur abandonne sous 0).</summary>
    private static UiGauge MoodGauge(int mood) => UiGauge.Create(
        "MoodGauge",
        "Humeur",
        (mood + 10) / 20.0,
        mood.ToString(System.Globalization.CultureInfo.InvariantCulture),
        mood < 2 ? UiTheme.Brick : mood < 5 ? UiTheme.Flag : UiTheme.Grass);

    private Visitor? VisitorOf(Golfer g) => g.VisitorId >= 0 ? _main.World.Visitors.Visitors[g.VisitorId] : null;

    private string NameOf(Golfer g) =>
        VisitorOf(g)?.Name ?? _main.World.League?.Members.FirstOrDefault(m => m.Id == g.MemberId)?.Name ?? "Golfeur";

    // ---- Messages de Birdie ----

    private void OnJoined(Visitor v)
    {
        Say(v, "Je prends ma carte de membre ! À bientôt sur le parcours.", "#adhésion");
        var league = _main.World.League;
        if (league is null)
        {
            return;
        }

        if (league.Open && !_leagueOpen)
        {
            var count = league.Members.Count(m => !m.Resigned);
            _main.Birdie.Post(Birdie.Club, $"La ligue ouvre ses divisions : {count} inscrits, votre pro compris.", "#ligue");
        }
        else if (!league.Open)
        {
            var missing = Sim.League.LeagueSeason.DivisionSize - league.Members.Count(m => !m.Resigned);
            _main.Birdie.Post(Birdie.Club, $"La ligue est fermée : elle ouvre à {Sim.League.LeagueSeason.DivisionSize} inscrits, votre pro compris. Il en manque {missing}.", "#ligue");
        }

        _leagueOpen = league.Open;
    }

    private void OnPromoted(Visitor v) => Say(v, $"Je passe au niveau {UiNames.Levels[(int)v.Level].ToLowerInvariant()}. Merci pour l'accueil.", "#niveau");

    private void OnResigned(Visitor v) => Say(v, "Une trop mauvaise journée : je rends ma carte.", "#démission", follow: false);

    private void Say(Visitor v, string text, string tag, bool follow = true)
    {
        var member = _main.World.League?.Members.FirstOrDefault(m => m.VisitorId == v.Id);
        var author = member is null ? new Birdie.Author(v.Name, "@" + v.Name.ToLowerInvariant().Replace(' ', '_'), UiTheme.Sky) : LeaguePanel.Voice(member);
        _main.Birdie.Post(author, text, tag, follow && member is not null ? member.Id : -1);
    }
}
