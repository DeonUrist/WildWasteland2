namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Physics layer masks shared by every NPCAI navigation part (grid bake, POI walker ground checks, town street routing):
    /// one definition instead of three identical copies (Nav.BakeMask, PoiNavBake.Mask, TownNavigation.Mask).</summary>
    internal static class NavMasks
    {
        /// <summary>Solid for navigation: everything the feelers see minus Ignore Raycast, UI, the player, cars (8), loose items (9),
        /// actors (10) and the other non-blocking layers (1, 4, 7, 12, 13, 15, 17, 19, 22) - those move or never block a walking body.</summary>
        internal const int Solid = Core.Layers.NavSolid;
    }
}
