using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// Every config entry of NPCAI navigation (README 6.2, D12 fresh start: new sections, no migration from the old [NPC / Nav] keys):
    /// grid map bake/route tuning in tools.cfg [NPCAI / Navigation], the POI pathfinding switch in settings.cfg [NPCAI / Pathfinding] and the
    /// NPC ladder speed in tools.cfg [NPCAI / POI pathfinding]. Readers use the typed properties, which fall back to the defaults while the
    /// entries are not bound (module never bound / unit tests), so the hot paths never see a null entry.
    /// </summary>
    internal static class NavSettings
    {
        /// <summary>[NPCAI / Navigation] BakeRange: a structure is mapped when the player comes within this distance, m.</summary>
        internal static ConfigEntry<float> BakeRangeEntry;
        /// <summary>[NPCAI / Navigation] CellSize: map resolution, m.</summary>
        internal static ConfigEntry<float> CellSizeEntry;
        /// <summary>[NPCAI / Navigation] Margin: open ground mapped around a structure, m.</summary>
        internal static ConfigEntry<float> MarginEntry;
        /// <summary>[NPCAI / Navigation] MaxStep: largest walkable step between neighbouring cells, m.</summary>
        internal static ConfigEntry<float> MaxStepEntry;
        /// <summary>[NPCAI / Navigation] BakeBudgetMs: CPU time per frame for mapping, ms.</summary>
        internal static ConfigEntry<float> BakeBudgetMsEntry;
        /// <summary>[NPCAI / Navigation] FieldSeconds: how long a route field is shared, s.</summary>
        internal static ConfigEntry<float> FieldSecondsEntry;

        /// <summary>Binds the navigation entries of the NPCAI module (called from NPCAIModule.BindNavigation, once per start-up).</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            BakeRangeEntry = cfg.Tool("Navigation", "BakeRange", 200f, "A structure is mapped when you come within this distance of it, m.", new AcceptableValueRange<float>(30f, 1000f));
            CellSizeEntry = cfg.Tool("Navigation", "CellSize", 0.5f, "Map resolution, m (smaller = narrower gaps found, slower mapping). Very large structures get coarser cells automatically.", new AcceptableValueRange<float>(0.25f, 2f));
            MarginEntry = cfg.Tool("Navigation", "Margin", 4f, "Open ground mapped around a structure's outline, m.", new AcceptableValueRange<float>(1f, 20f));
            MaxStepEntry = cfg.Tool("Navigation", "MaxStep", 0.25f, "Largest height step between neighbouring map cells (0.5 m apart) an NPC can walk, m. An NPC's body can't climb much more than a kerb.", new AcceptableValueRange<float>(0.1f, 2f));
            BakeBudgetMsEntry = cfg.Tool("Navigation", "BakeBudgetMs", 1f, "CPU time per frame spent mapping a structure, ms.", new AcceptableValueRange<float>(0.2f, 10f));
            FieldSecondsEntry = cfg.Tool("Navigation", "FieldSeconds", 1f, "How long a computed route to one goal is reused by every NPC heading there, s.", new AcceptableValueRange<float>(0.2f, 10f));
            PoiNav.Enabled = cfg.Player("Pathfinding", "PoiNavigation", true,
                "Human NPCs use the pre-recorded navigation of POIs (every floor, roof and walkway; ladders and climbs) where it exists. Off = the grid maps only.");
            // README 9 / A3-8: the old bind rewrote a stored 1 to 2 on every start, overriding the user's choice. The default is 2; any stored value is kept.
            PoiNav.LadderSpeed = cfg.Tool("POI pathfinding", "NpcLadderSpeed", 2f, "How fast NPCs climb ladders up, m/s (down they slide like the player).", new AcceptableValueRange<float>(.3f, 4f));
        }

        /// <summary>Bake range, m (default 200 while unbound).</summary>
        internal static float BakeRange { get { return BakeRangeEntry != null ? BakeRangeEntry.Value : 200f; } }
        /// <summary>Configured cell size, m (default 0.5 while unbound).</summary>
        internal static float CellSize { get { return CellSizeEntry != null ? CellSizeEntry.Value : 0.5f; } }
        /// <summary>Footprint margin, m (default 4 while unbound).</summary>
        internal static float Margin { get { return MarginEntry != null ? MarginEntry.Value : 4f; } }
        /// <summary>Configured max step, m (default 0.25 while unbound).</summary>
        internal static float MaxStep { get { return MaxStepEntry != null ? MaxStepEntry.Value : 0.25f; } }
        /// <summary>Bake budget per frame, ms (default 1 while unbound).</summary>
        internal static float BakeBudgetMs { get { return BakeBudgetMsEntry != null ? BakeBudgetMsEntry.Value : 1f; } }
        /// <summary>Route field life, s (default 1 while unbound).</summary>
        internal static float FieldSeconds { get { return FieldSecondsEntry != null ? FieldSecondsEntry.Value : 1f; } }

        /// <summary>The step rule every map query uses: MaxStep clamped to at least 0.1 m.</summary>
        internal static float Step { get { return Mathf.Max(0.1f, MaxStep); } }
        /// <summary>The cell size rule of the bake: CellSize clamped to at least 0.25 m.</summary>
        internal static float Cell { get { return Mathf.Max(0.25f, CellSize); } }
    }
}
