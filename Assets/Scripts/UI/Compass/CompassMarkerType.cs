namespace Karma.UI.Compass
{
    /// <summary>
    /// Semantic category for a compass marker. Each type maps to a default
    /// icon sprite on the CompassHUDController; CustomIcon uses the marker's
    /// overrideSprite instead.
    /// </summary>
    public enum CompassMarkerType
    {
        PrimaryQuest,
        SideQuest,
        NPC,
        Altar,
        Shop,
        Discovery,
        CustomIcon
    }
}
