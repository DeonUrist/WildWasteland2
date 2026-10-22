using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Town side of NPC navigation (README P6, A6-9), now inside NPCAI: the town waypoint providers (outside-wall detours, gate entrances,
    /// street graph, walker gates; then custom POI exit markers) registered with the Nav waypoint chain, the town overlay hooks, and the
    /// small queries POI/Loot/Tools code still calls (discover a building, register an actor, floor cells, structure root, tuning).
    /// No reflection and no Harmony patch on Nav any more; every call is a direct NPCAI call.
    /// </summary>
    internal static class TownBakeBridge
    {
        private static TownsModule _owner;
        private static int _townWaypoints, _exitWaypoints;

        /// <summary>True while NPC navigation runs (the NPC brain is on and the NPCAI entry owns navigation).</summary>
        internal static bool Enabled { get { return Nav.On; } }

        /// <summary>Navigation tuning read by POI town assets: NavMargin, NavCellSize, NavMaxStep (+ the other NavSettings); else fallback.</summary>
        internal static float Tuning(string name, float fallback)
        {
            switch (name)
            {
                case "NavMargin": return NavSettings.Margin;
                case "NavCellSize": return NavSettings.CellSize;
                case "NavMaxStep": return NavSettings.MaxStep;
                case "NavBakeRange": return NavSettings.BakeRange;
                case "NavBakeBudgetMs": return NavSettings.BakeBudgetMs;
                case "NavFieldSeconds": return NavSettings.FieldSeconds;
                default: return fallback;
            }
        }

        /// <summary>Towns start (TownsModule.InitNavigation): the town waypoint providers (order 0: town routing, order 100: custom POI exits)
        /// and the town overlay hooks.</summary>
        internal static void Install(TownsModule owner)
        {
            _owner = owner;
            _townWaypoints = Nav.AddWaypointProvider(owner, new TownWaypoints(owner), 0);
            _exitWaypoints = Nav.AddWaypointProvider(owner, new ExitWaypoints(), 100);
            TownOverlap.Install();
            Nav.AddMakeGate(PoiDressing.MapGate);
            Log.Main(owner, "NPC navigation bridge installed: per-building bakes -> validated street links -> destination building; no town-wide bake.");
        }

        /// <summary>Discovers a placed town building / custom POI at once (no-op while the NPC module is off).</summary>
        internal static void Discover(GameObject building) { if (building != null && ModApi.Get<INpcApi>() != null) Nav.Discover(building.transform); }

        /// <summary>Registers a spawned town actor with the NPC brain (no-op while the NPC module is off).</summary>
        internal static void Actor(GameObject actor) { if (actor != null && ModApi.Get<INpcApi>() != null) Brain.Spawned(actor); }

        /// <summary>Up to 256 floor points of root's map (see Nav.FloorCells); known/baked tell whether root is mapped / its map is ready.</summary>
        internal static int Cells(Transform root, bool roofed, List<Vector3> into, out bool known, out bool baked)
        { known = baked = false; into.Clear(); return Nav.FloorCells(root, roofed, into, 256, out known, out baked); }

        /// <summary>Root of the structure whose footprint contains position, or null.</summary>
        internal static Transform Root(Vector3 position) { return Nav.StructureRootAt(position); }

        /// <summary>Towns stop: providers and overlay hooks removed.</summary>
        internal static void Uninstall(TownsModule owner)
        {
            Nav.RemoveWaypointProvider(_townWaypoints); Nav.RemoveWaypointProvider(_exitWaypoints);
            _townWaypoints = _exitWaypoints = 0;
            TownOverlap.Uninstall();
            Nav.RemoveMakeGate(PoiDressing.MapGate);
            _owner = null;
        }

        /// <summary>Town routing (TownsModule.Waypoint): forced for approach detours/entrances (no building map reaches them).</summary>
        private sealed class TownWaypoints : IForcedWaypointProvider
        {
            private readonly TownsModule _towns;

            /// <summary>Provider answering for the given Towns module.</summary>
            internal TownWaypoints(TownsModule towns) { _towns = towns; }

            /// <summary>Town waypoint; force = an approach waypoint (detour around the wall / gate entrance).</summary>
            public bool TryNext(GameObject npc, Vector3 fromScene, Vector3 goalScene, out Vector3 waypointScene, out bool force)
            {
                force = false; waypointScene = goalScene;
                return _owner == _towns && _towns.Waypoint(npc, fromScene, goalScene, out waypointScene, out force);
            }

            /// <summary>Same waypoint without the force flag.</summary>
            public bool TryNext(GameObject npc, Vector3 fromScene, Vector3 goalScene, out Vector3 waypointScene)
            { bool force; return TryNext(npc, fromScene, goalScene, out waypointScene, out force); }
        }

        /// <summary>Custom POI exit markers (PoiDressing.Exit): taken only when the body's way to them is clear.</summary>
        private sealed class ExitWaypoints : IWaypointProvider
        {
            /// <summary>The cheapest exit marker of the custom POI the NPC stands in when its goal is outside.</summary>
            public bool TryNext(GameObject npc, Vector3 fromScene, Vector3 goalScene, out Vector3 waypointScene)
            { return PoiDressing.Exit(fromScene, goalScene, out waypointScene); }
        }
    }
}
