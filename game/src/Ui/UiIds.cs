namespace Golferoo.Game.Ui;

/// <summary>
/// Identifiants stables des éléments d'interface, portés par leur <c>Name</c> (sans point) et retrouvés par <see cref="UiRoot.Find"/>.
/// Uniques dans tout l'arbre (<see cref="UiRoot.Find"/> rend le premier trouvé) : les vignettes et actions sont préfixées par leur tiroir.
/// Le tutoriel et la fumée les ciblent : on n'en renomme pas un sans eux.
/// </summary>
public static class UiIds
{
    /// <summary>Boutons d'outil de l'étage haut de la barre.</summary>
    public static class Tool
    {
        public const string Terrain = "ToolTerrain";
        public const string Relief = "ToolRelief";
        public const string Trees = "ToolTrees";
        public const string Holes = "ToolHoles";
        public const string Buildings = "ToolBuildings";
        public const string Amenities = "ToolAmenities";
        public const string Turf = "ToolTurf";
        public const string Views = "ToolViews";
        public const string Bulldozer = "ToolBulldozer";
    }

    /// <summary>Boutons ronds à droite de l'étage haut.</summary>
    public static class Round
    {
        public const string League = "RoundLeague";
        public const string Pro = "RoundPro";
        public const string Finances = "RoundFinances";
        public const string Reactions = "RoundReactions";
        public const string Birdie = "RoundBirdie";
        public const string Camera = "RoundCamera";
    }

    /// <summary>Pastilles et commandes de l'étage bas, éléments fixes de l'écran.</summary>
    public static class Hud
    {
        public const string Bar = "BottomBar";
        public const string Crest = "ClubCrest";
        public const string Pause = "HudPause";
        public const string Clock = "HudClock";
        public const string Weather = "HudWeather";
        public const string Club = "HudClub";
        public const string Members = "HudMembers";
        public const string Money = "HudMoney";
        public const string Mood = "HudMood";
        public const string Hole = "HudHole";
        public const string Message = "HudMessage";
        public const string BirdiePeek = "BirdiePeek";
        public const string BirdieUnread = "BirdieUnread";
        public const string BirdieFeed = "BirdieFeed";
        public const string BirdieMute = "BirdieMute";
        public const string ThoughtsToggle = "ThoughtsToggle";
        public const string ReactionMap = "ReactionMapToggle";
        public const string ThoughtBubble = "ThoughtBubble";
        public const string GolferTip = "GolferTip";
        public const string StrokeCost = "HudStrokeCost";

        /// <summary>Bouton de vitesse ×<paramref name="speed"/> (1, 2, 4, 8 ou 16).</summary>
        public static string Speed(int speed) => $"HudSpeed{speed}";
    }

    /// <summary>Le tiroir d'outils et ses onglets.</summary>
    public static class Drawer
    {
        public const string Root = "ToolDrawer";
        public const string BrushSize = "DrawerBrushSize";
        public const string Status = "DrawerStatus";
        public const string TabTerrain = "TabTerrain";
        public const string TabRelief = "TabRelief";
        public const string TabTrees = "TabTrees";
        public const string TabHoles = "TabHoles";
        public const string TabBuildings = "TabBuildings";
    }

    public static class Thumb
    {
        public const string Tee = "ThumbTerrainTee";
        public const string Green = "ThumbTerrainGreen";
        public const string Fairway = "ThumbTerrainFairway";
        public const string Rough = "ThumbTerrainRough";
        public const string Bunker = "ThumbTerrainBunker";
        public const string Water = "ThumbTerrainWater";
        public const string Wood = "ThumbTerrainWood";
        public const string Path = "ThumbTerrainPath";

        /// <summary>Vignette d'essence du tiroir Arbres (<paramref name="species"/> et <paramref name="stage"/> : noms des énumérations de la sim).</summary>
        public static string Tree(string species, string stage) => $"ThumbTrees{species}{stage}";
    }

    /// <summary>Boutons d'action des tiroirs.</summary>
    public static class Action
    {
        public const string TreeSelect = "ActionTreesSelect";
        public const string TreeRaze = "ActionTreesRaze";
        public const string TreeRazeArea = "ActionTreesRazeArea";
        public const string TreeKeepRemarkable = "ActionTreesKeepRemarkable";
        public const string TreeClearFallen = "ActionTreesClearFallen";
        public const string HolePose = "ActionHolesPose";
        public const string HoleNew = "ActionHolesNew";
        public const string HoleRemove = "ActionHolesRemove";
        public const string ClubhouseMove = "ActionHolesClubhouseMove";
        public const string HolePrevious = "ActionHolesPrevious";
        public const string HoleNext = "ActionHolesNext";
        public const string Undo = "ActionUndo";

        /// <summary>Bouton du tiroir Bâtiments (<paramref name="kind"/> : nom du genre dans la sim).</summary>
        public static string Building(string kind) => $"ActionBuildings{kind}";
    }

    public static class Panel
    {
        public const string Turf = "PanelTurf";
        public const string League = "PanelLeague";
        public const string Pro = "PanelPro";
        public const string LeagueCard = "PanelLeagueCard";
        public const string Birdie = "PanelBirdie";
        public const string Golfer = "PanelGolfer";
        public const string Finances = "PanelFinances";
        public const string Reactions = "PanelReactions";
        public const string Overdraft = "PanelOverdraft";

        /// <summary>La visée du pro, hors de la pile : elle suit le tour du pro.</summary>
        public const string Aim = "PanelAim";
    }

    /// <summary>Le panneau Finances et la confirmation du découvert.</summary>
    public static class Finance
    {
        public const string Balance = "FinancesBalance";
        public const string Chart = "FinancesChart";
        public const string Table = "FinancesTable";
        public const string OverdraftYes = "FinancesOverdraftYes";
        public const string OverdraftNo = "FinancesOverdraftNo";
    }

    /// <summary>La bulle de votre pro (tutoriel), ses boutons et l'anneau qui entoure le bouton attendu.</summary>
    public static class Tutorial
    {
        public const string Bubble = "TutorialBubble";
        public const string Text = "TutorialText";
        public const string Yes = "TutorialYes";
        public const string Later = "TutorialLater";
        public const string Start = "TutorialStart";
        public const string Skip = "TutorialSkip";
        public const string Golfers = "TutorialGolfers";
        public const string Close = "TutorialClose";
        public const string Ring = "TutorialRing";
    }
}
