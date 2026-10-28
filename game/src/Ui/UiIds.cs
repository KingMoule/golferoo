namespace Golferoo.Game.Ui;

/// <summary>
/// Identifiants stables des éléments d'interface, portés par leur <c>Name</c> (sans point) et retrouvés par <see cref="UiRoot.Find"/>.
/// Le tutoriel et la fumée les ciblent : on n'en renomme pas un sans eux.
/// </summary>
public static class UiIds
{
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

    public static class Thumb
    {
        public const string Tee = "ThumbTee";
        public const string Green = "ThumbGreen";
        public const string Fairway = "ThumbFairway";
        public const string Rough = "ThumbRough";
        public const string Bunker = "ThumbBunker";
        public const string Water = "ThumbWater";
        public const string Wood = "ThumbWood";
        public const string Path = "ThumbPath";
    }

    public static class Panel
    {
        public const string Turf = "PanelTurf";
        public const string League = "PanelLeague";
        public const string Pro = "PanelPro";
    }
}
