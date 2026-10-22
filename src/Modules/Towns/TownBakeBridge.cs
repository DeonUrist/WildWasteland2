using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // The temporary add-on boundary is isolated here. Resolve once; hot paths use typed delegates.
    // At merge time these become ordinary calls in NPC's existing Nav.Next path, without a new baker.
    internal static class TownBakeBridge
    {
        private delegate int CellsDelegate(Transform root, bool roofedOnly, List<Vector3> into, int max, out bool known, out bool baked);
        private static Action<GameObject> _discover;
        private static Action<GameObject> _actor;
        private static CellsDelegate _cells;
        private static Func<Vector3, Transform> _root;
        private static Func<bool> _enabled;
        internal static bool Enabled { get { return _enabled != null && _enabled(); } }
        private static TownsModule _owner;
        internal static float Tuning(string name, float fallback)
        {
            var type = typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.NpcModule");
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var setting = field == null ? null : field.GetValue(null) as ConfigEntry<float>;
            return setting == null ? fallback : setting.Value;
        }
        internal static void Install(TownsModule owner)
        {
            var nav = typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.Nav", true);
            _enabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), Patches.Method(nav, "get_On"));
            _discover = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), Patches.Method(nav, "Spawned"));
            _actor = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), Patches.Method(typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.Brain", true), "Spawned"));
            _cells = (CellsDelegate)Delegate.CreateDelegate(typeof(CellsDelegate), Patches.Method(nav, "FloorCells"));
            _root = (Func<Vector3, Transform>)Delegate.CreateDelegate(typeof(Func<Vector3, Transform>), Patches.Method(nav, "StructureRootAt"));
            if (!Patches.Patch(owner, Patches.Method(nav, "Next"),
                prefix: typeof(TownBakeBridge).GetMethod("Before", BindingFlags.Static | BindingFlags.NonPublic),
                postfix: typeof(TownBakeBridge).GetMethod("After", BindingFlags.Static | BindingFlags.NonPublic)))
                throw new InvalidOperationException("Existing NPC Nav.Next bridge could not be installed.");
            _owner = owner;
            TownOverlap.Install(owner, nav);
            Log.Always("[Towns] NPC bridge installed: per-building bakes -> validated street links -> destination building; no town-wide bake.");
        }
        internal static void Discover(GameObject building) { if (_discover != null && Services.Npc != null) _discover(building); }
        internal static void Actor(GameObject actor) { if (_actor != null && Services.Npc != null) _actor(actor); }
        internal static int Cells(Transform root, bool roofed, List<Vector3> into, out bool known, out bool baked)
        { known = baked = false; into.Clear(); return _cells == null ? 0 : _cells(root, roofed, into, 256, out known, out baked); }
        internal static Transform Root(Vector3 position) { return _root == null ? null : _root(position); }
        internal struct Redirect
        {
            internal bool Active, Approach;
            internal Vector3 ActualGoal, Waypoint;
        }
        private static void Before(GameObject owner, Vector3 pos, ref Vector3 goal, out Redirect __state)
        {
            __state = new Redirect(); Vector3 next; bool approach;
            if (_owner == null || Services.Npc == null) return;
            if (!_owner.Waypoint(owner, pos, goal, out next, out approach))
            { approach = false; if (!PoiDressing.Exit(pos, goal, out next)) return; }
            __state.Active = true; __state.Approach = approach; __state.ActualGoal = goal; __state.Waypoint = next; goal = next;
        }
        private static void After(Vector3 pos, ref Vector3 goal, ref Vector3 next, ref float pathLeft, ref bool __result, Redirect __state)
        {
            if (!__state.Active) return;
            goal = __state.ActualGoal;
            if (!__result && (__state.Approach || TownNavigation.Clear(pos, __state.Waypoint)))
            { next = __state.Waypoint; pathLeft = Vector3.Distance(pos, next); __result = true; }
            if (__result) pathLeft += Vector3.Distance(__state.Waypoint, __state.ActualGoal);
        }
        internal static void Uninstall(TownsModule owner)
        {
            _owner = null; Patches.Unpatch(owner); _discover = null; _actor = null; _cells = null; _root = null;
        }
    }
}
