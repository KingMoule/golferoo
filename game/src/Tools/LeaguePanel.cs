using System.Globalization;
using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Calendar = Golferoo.Sim.Time.Calendar;

namespace Golferoo.Game.Tools;

/// <summary>
/// La ligue à l'écran (bouton Ligue) : classement de chaque division, carte de score d'un joueur au clic sur son nom,
/// avis de journée et de fin de saison dans Birdie, et ligne du pro pour le HUD. Rafraîchi à l'ouverture et au changement de jour, jamais par image.
/// </summary>
public partial class LeaguePanel : Node
{
    private static readonly string[] FormatNames = ["stroke play", "stableford", "match play", "contre le par"];
    /// <summary>Style de jeu d'un membre (C09), dans l'ordre de <see cref="Archetype"/> ; le neutre n'est attribué à personne.</summary>
    private static readonly string[] StyleNames = ["", "prudent", "long frappeur", "joueur de draw", "joueur de fade", "artiste", "fonceur"];

    private static readonly Color[] Avatars = [UiTheme.SkyDeep, UiTheme.Flag, UiTheme.Brick, UiTheme.Grass];

    private Main _main = null!;
    private LeagueSeason _league = null!;
    private UiPanel _panel = null!;
    private UiPanel _cardPanel = null!;
    private Label _title = null!;
    private Label _rating = null!;
    private VBoxContainer _divisions = null!;
    private ScrollContainer _scroll = null!;
    private Label _champion = null!;
    private VBoxContainer _cardBody = null!;
    private int _eventsSeen;
    private Member? _shown;
    private ((int Cards, int Division, int Year) Key, string Text) _pro = ((-1, -1, -1), string.Empty);

    /// <summary>Largeurs du panneau et de la carte (les captures vérifient qu'ils n'en débordent pas).</summary>
    public const float Width = 380f;
    public const float CardWidth = 340f;

    public bool IsOpen => _panel.Visible;

    /// <summary>Construit le panneau et la carte de score dans la pile, fermés.</summary>
    public void Init(Main main)
    {
        _main = main;
        _league = main.World.League!;
        _eventsSeen = _league.Events.Count;

        _panel = UiPanel.Create(UiIds.Panel.League, "Ligue", "trophy", Width);
        var box = _panel.Content;
        // Renvoi à la ligne : « Ligue, an 1 | Prochaine journée : 5 avril, stroke play » élargissait le panneau.
        _title = new Label { ThemeTypeVariation = "UiName", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _rating = new Label { ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _champion = new Label { ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _divisions = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _scroll.AddChild(_divisions);
        box.AddChild(_title);
        box.AddChild(_rating);
        box.AddChild(_scroll);
        box.AddChild(_champion);
        _panel.Closed += () => main.Panels.Close(_cardPanel);
        main.Panels.Add(_panel);

        _cardPanel = UiPanel.Create(UiIds.Panel.LeagueCard, "Carte de score", "flag", CardWidth);
        _cardBody = _cardPanel.Content;
        main.Panels.Add(_cardPanel);
    }

    /// <summary>Décimale à la française : « 31,4 ».</summary>
    public static string Decimal(double value) => value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

    public void Toggle()
    {
        if (_panel.Visible)
        {
            _panel.EmitSignal(UiPanel.SignalName.Closed);
            return;
        }

        _main.Panels.Open(_panel);
        UiTheme.FitList(_scroll);
        Refresh();
    }

    /// <summary>Nouveau jour : avis dans Birdie (journée commencée, événements de fin de saison) puis rafraîchissement si le panneau est ouvert.</summary>
    public void OnDayChanged()
    {
        var today = _league.Rounds.Append(_league.Final).FirstOrDefault(r => r.StartDay == _main.World.DayIndex);
        if (today is not null)
        {
            _main.Birdie.Post(
                Birdie.Club,
                today == _league.Final
                    ? $"Finale de la ligue aujourd'hui, en {FormatNames[(int)today.Format]}. Les meilleurs de chaque division sont au départ."
                    : $"Journée de ligue aujourd'hui, en {FormatNames[(int)today.Format]}. Les membres partent du 1.",
                today == _league.Final ? "#finale" : "#ligue");
        }

        for (; _eventsSeen < _league.Events.Count; _eventsSeen++)
        {
            Announce(_league.Events[_eventsSeen]);
        }

        if (_panel.Visible)
        {
            Refresh();
        }
    }

    /// <summary>« Votre pro : division 2, handicap 31,4 », recalculé seulement quand ses cartes, sa division ou la saison changent.</summary>
    public string ProText()
    {
        var pro = _league.Members.FirstOrDefault(m => m.IsPro);
        var division = pro is null ? -1 : DivisionOf(pro);
        if (division < 0)
        {
            return "Votre pro : hors ligue";
        }

        var key = (pro!.Cards.Count, division, _league.Year);
        if (_pro.Key != key)
        {
            _pro = (key, $"Votre pro : division {division + 1}, {HandicapText(pro)}");
        }

        return _pro.Text;
    }

    /// <summary>Fin de saison dans Birdie, à la voix du membre : le champion, et la montée ou la descente de votre pro. Un clic suit son golfeur.</summary>
    public void Announce(LeagueEvent e)
    {
        var who = _league.Members[e.MemberId];
        var (text, tag) = e.Type switch
        {
            LeagueEventType.Champion when who.IsPro => ("Champion du club ! Je n'y crois pas encore. Merci à vous tous.", "#champion"),
            LeagueEventType.Champion => ($"Champion du club, an {e.Year + 1} ! La tournée est pour moi au chalet.", "#champion"),
            LeagueEventType.Promoted when who.IsPro => ($"Je monte en division {e.Division + 1}. La saison prochaine sera plus relevée.", "#promotion"),
            LeagueEventType.Relegated when who.IsPro => ($"Je descends en division {e.Division + 1}. On se reprend la saison prochaine.", "#relégation"),
            _ => (null, null),
        };
        if (text is not null)
        {
            _main.Birdie.Post(Voice(who), text, tag!, who.Id);
        }
    }

    /// <summary>L'auteur d'un message de membre : votre pro, ou le membre et un pseudo tiré de son nom (« @ginette_lacasse »).</summary>
    public static Birdie.Author Voice(Member member)
    {
        if (member.IsPro)
        {
            return Birdie.Pro;
        }

        var handle = new string(member.Name.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').Select(c => c == ' ' ? '_' : c).ToArray());
        return new Birdie.Author(member.Name, "@" + handle, Avatars[member.Id % Avatars.Length]);
    }

    private int DivisionOf(Member member)
    {
        for (var d = 0; d < _league.Divisions.Count; d++)
        {
            if (_league.Divisions[d].Contains(member))
            {
                return d;
            }
        }

        return -1;
    }

    private string HandicapText(Member member) =>
        member.Index(_main.World.CourseRating?.Holes ?? 1) is { } index ? $"handicap {Decimal(index)}" : "pas encore de handicap";

    private string HandicapShort(Member member) =>
        member.Index(_main.World.CourseRating?.Holes ?? 1) is { } index ? Decimal(index) : "-";

    /// <summary>Les journées tombent le 5 du mois ; <paramref name="nominalDay"/> compte depuis le 1er avril.</summary>
    private static string DateText(int nominalDay) =>
        $"{(nominalDay % Calendar.DaysPerMonth) + 1} {UiNames.Months[(Calendar.StartMonth - 1 + (nominalDay / Calendar.DaysPerMonth)) % 12]}";

    /// <summary>Reconstruit le contenu : en-tête, un tableau par division, champion de l'an passé.</summary>
    public void Refresh()
    {
        var next = _league.Rounds.Append(_league.Final).FirstOrDefault(r => r.StartDay < 0);
        _title.Text = $"Ligue, an {_league.Year + 1} | " + (next is null
            ? $"Prochaine journée : 5 avril, {FormatNames[(int)LeagueFormat.StrokePlay]}"
            : $"Prochaine journée : {DateText(next.NominalDay)}, {FormatNames[(int)next.Format]}");
        _rating.Text = _main.World.CourseRating is { } r ? $"Note du parcours {Decimal(r.Scratch18)} / slope {r.Slope}" : "Note du parcours : calcul en cours";

        foreach (var child in _divisions.GetChildren())
        {
            child.QueueFree();
        }

        for (var d = 0; d < _league.Divisions.Count; d++)
        {
            _divisions.AddChild(new Label { Text = $"Division {d + 1}", ThemeTypeVariation = "UiName" });
            // Pas de colonne « Style » (C09) : le style est sur la fiche.
            var grid = new GridContainer { Columns = 4 };
            foreach (var head in new[] { "Rang", "Nom", "Hcp", "Points" })
            {
                grid.AddChild(new Label { Text = head, ThemeTypeVariation = "UiTableHeader" });
            }

            var rank = 1;
            foreach (var m in _league.Divisions[d].OrderByDescending(m => m.SeasonPoints).ThenBy(m => m.Id))
            {
                grid.AddChild(Cell(rank++.ToString(CultureInfo.InvariantCulture), m));
                var name = new Button { Text = m.Name, Flat = true, FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
                name.Pressed += () => ShowCard(m);
                Tint(name, m);
                grid.AddChild(name);
                grid.AddChild(Cell(HandicapShort(m), m));
                grid.AddChild(Cell(Decimal(m.SeasonPoints), m));
            }

            _divisions.AddChild(grid);
        }

        _champion.Text = _league.Champions.Count > 0
            ? $"Champion an {_league.Champions[^1].Year + 1} : {_league.Members[_league.Champions[^1].MemberId].Name} (brut {_league.Champions[^1].Gross})"
            : string.Empty;
        if (_shown is not null && _cardPanel.Visible)
        {
            ShowCard(_shown);
        }
    }

    /// <summary>Nom d'un style de jeu : « joueur de draw ».</summary>
    public static string StyleName(Archetype archetype) => StyleNames[(int)archetype];

    /// <summary>Style d'un membre ; votre pro n'en a pas : il joue le coup du joueur.</summary>
    private static string Style(Member member) => member.IsPro ? "" : StyleName(member.Profile);

    private static Label Cell(string text, Member member)
    {
        var label = new Label { Text = text };
        Tint(label, member);
        return label;
    }

    private static void Tint(Control control, Member member)
    {
        if (member.IsPro)
        {
            control.AddThemeColorOverride("font_color", UiTheme.Flag);
        }
    }

    /// <summary>Carte de score d'un joueur, ouverte dans la pile : ses journées de la saison (journée, format, brut, écart).</summary>
    public void ShowCard(Member member)
    {
        _shown = member;
        _cardPanel.Title.Text = member.Name;
        foreach (var child in _cardBody.GetChildren())
        {
            child.QueueFree();
        }

        // Renvoi à la ligne : « Vera L. | pas encore de handicap | long frappeur » dépasse la largeur de la carte.
        _cardBody.AddChild(new Label { Text = HandicapText(member) + (member.IsPro ? "" : $" | {Style(member)}"), ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var grid = new GridContainer { Columns = 4 };
        foreach (var head in new[] { "Journée", "Format", "Brut", "Écart" })
        {
            grid.AddChild(new Label { Text = head, ThemeTypeVariation = "UiTableHeader" });
        }

        foreach (var round in _league.Rounds.Append(_league.Final))
        {
            if (!round.Cards.TryGetValue(member.Id, out var card))
            {
                continue;
            }

            var differential = member.Cards.Where(c => c.Round == round.Number).Select(c => c.Differential).LastOrDefault();
            grid.AddChild(new Label { Text = round == _league.Final ? "Finale" : (round.Number + 1).ToString(CultureInfo.InvariantCulture) });
            grid.AddChild(new Label { Text = FormatNames[(int)round.Format] });
            grid.AddChild(new Label { Text = $"{card.Gross} ({card.Gross - card.TotalPar:+0;-0;0})" });
            grid.AddChild(new Label { Text = differential is { } diff ? Decimal(diff) : "-" });
        }

        _cardBody.AddChild(grid);
        _main.Panels.Open(_cardPanel);
    }
}
