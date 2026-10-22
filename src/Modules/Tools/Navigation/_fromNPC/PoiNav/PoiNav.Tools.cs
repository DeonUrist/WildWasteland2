using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>PoiNav part owned by Tools: the developer "Setup POI pathfinding" / "Display navigation data" switches, the POI navigation
    /// editor key and the offline baker tuning (bound from PoiNav.Bind in the original order).</summary>
    internal static partial class PoiNav
    {
        internal static ConfigEntry<bool> Setup, Display;
        internal static ConfigEntry<float> BakeBudgetMs, BakeCell;
        internal static ConfigEntry<UnityEngine.InputSystem.Key> EditorKey;

        /// <summary>Binds the Tools-owned POI navigation entries (between [Pathfinding] PoiNavigation and NpcLadderSpeed, as before).</summary>
        static partial void BindTools(ModuleConfig cfg)
        {
            Setup = cfg.Main.Bind(cfg.Section("POI pathfinding setup"), "Setup POI pathfinding", false,
                "Developer: one copy of every native POI type is placed near the starting camp (not saved, removed when off) and new towns are not generated, so each POI can be baked and edited.");
            Display = cfg.Main.Bind(cfg.Section("POI pathfinding setup"), "Display navigation data", false,
                "Developer: draws the POI navigation near you: surfaces (one colour each), ladders (cyan), climbs (orange), drops (red), editor links brighter, switched-off links grey.");
            EditorKey = cfg.Main.Bind(cfg.Section("Editors"), "POI navigation editor", UnityEngine.InputSystem.Key.None,
                "Opens the POI navigation editor for the nearest POI (Debug + Developer mode).");
            BakeBudgetMs = cfg.Tool("POI pathfinding setup", "BakeBudgetMs", 8f, "Milliseconds per frame the POI navigation baker may use.", new AcceptableValueRange<float>(1f, 50f));
            BakeCell = cfg.Tool("POI pathfinding setup", "BakeCellSize", .33f, "Sample spacing of the POI navigation bake, m (smaller = narrower walkways found, slower bake).", new AcceptableValueRange<float>(.2f, .5f));
        }
    }
}
