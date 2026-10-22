using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Core.Optimization
{
    /// <summary>Reference-identity comparer (UnityEngine.Object.Equals goes native; the registry only needs identity).</summary>
    internal sealed class RefComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly RefComparer<T> Instance = new RefComparer<T>();
        /// <summary>Compares managed reference identity without invoking Unity native equality.</summary>
        public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
        /// <summary>Hashes managed reference identity for the optimization lookup table.</summary>
        public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
    }

    /// <summary>
    /// PM1 (plan §4): side index of PlayMakerFSM.FsmList. FsmList only appends in OnEnable and List.Remove keeps relative order,
    /// so the list order == ascending "enable stamp", and a per-GameObject list maintained the same way keeps exactly the
    /// relative order of that object's entries in FsmList. Used by PM2 (targeted events) and PM3 (fast removal).
    /// The index is (re)built from FsmList whenever tracking starts, so it is valid when switched on mid-game.
    /// Hooks: Fsm.OnEnable prefix (after the component appended, before its callbacks), shared enable postfix,
    /// and OnDisable/OnDestroy removal at the native list's removal point.
    /// </summary>
    internal static class FsmRegistry
    {
        static int _users;
        /// <summary>Whether an active optimization currently needs the exact FSM side index.</summary>
        internal static bool Tracking { get { return _users > 0; } }
        static long _stamp;
        static readonly Dictionary<PlayMakerFSM, long> _stamps = new Dictionary<PlayMakerFSM, long>(RefComparer<PlayMakerFSM>.Instance);
        static readonly Dictionary<int, List<PlayMakerFSM>> _byGo = new Dictionary<int, List<PlayMakerFSM>>();
        static readonly Stack<List<PlayMakerFSM>> _freeLists = new Stack<List<PlayMakerFSM>>();
        /// <summary>Number of native components currently indexed by reference identity.</summary>
        internal static int Count { get { return _stamps.Count; } }
        internal static long Rebuilds;

        /// <summary>Acquires an index user and rebuilds native FsmList ordering when tracking first starts.</summary>
        internal static void Acquire(OptimizationRuntime m)
        {
            Subscribe(m);
            if (_users++ == 0) Rebuild();
        }
        /// <summary>OnEnable goes through the shared Core.Hooks.FsmEnable postfix (Patrols also uses it). Called from SubscribeHooks.</summary>
        static bool _subscribed;
        /// <summary>Installs shared lifecycle handlers and publishes the exact per-owner FsmIndex provider once.</summary>
        internal static void Subscribe(IModule m)
        {
            if (_subscribed) return;
            _subscribed = true;
            Hooks.FsmEnable.Postfix(m, OnEnablePost);
            Hooks.FsmInnerEnable.Prefix(m, Shared.OnEnablePre);
            Hooks.FsmDisable.Prefix(m, Shared.OnDisablePre);
            Hooks.FsmDestroy.Prefix(m, Shared.OnDestroyPre);
            FsmIndex.SetProvider(IndexProvider.Instance);   // W0B-4: Core FsmIndex.ByOwner answers from PM1 while tracking
        }
        /// <summary>Module shutdown: the shared hook handlers are gone; Core FsmIndex falls back to GetComponents.</summary>
        internal static void Unsubscribed() { _subscribed = false; FsmIndex.SetProvider(null); }

        /// <summary>Core <see cref="FsmIndex"/> provider over this registry (exact FsmList order; ready only while tracking).</summary>
        sealed class IndexProvider : IFsmIndexProvider
        {
            internal static readonly IndexProvider Instance = new IndexProvider();
            /// <summary>True while PM1 has users (the index is maintained by the hooks).</summary>
            public bool Ready { get { return Tracking; } }
            /// <summary>Live per-GameObject list in FsmList order, or null; no allocation.</summary>
            public List<PlayMakerFSM> On(GameObject go) { return FsmRegistry.On(go); }
        }
        /// <summary>Shared component-enable postfix: ensures an enabled component is indexed while tracking.</summary>
        static void OnEnablePost(PlayMakerFSM f) { if (_users > 0) Add(f); }
        /// <summary>Releases an index user and drops cached component references after the last user stops.</summary>
        internal static void Release() { if (_users > 0 && --_users == 0) Clear(); }

        /// <summary>Returns receiver lists to the pool and drops all indexed component stamps.</summary>
        static void Clear()
        {
            foreach (var kv in _byGo) { kv.Value.Clear(); _freeLists.Push(kv.Value); }
            _byGo.Clear(); _stamps.Clear();
        }

        /// <summary>Re-index from FsmList in list order (start of tracking, scene load, consistency repair).</summary>
        internal static void Rebuild()
        {
            Clear(); Rebuilds++;
            var list = PlayMakerFSM.FsmList;
            for (int i = 0; i < list.Count; i++) Add(list[i]);
        }

        /// <summary>Indexes one enabled component once, assigning its native enable-order stamp.</summary>
        static void Add(PlayMakerFSM f)
        {
            if (ReferenceEquals(f, null) || _stamps.ContainsKey(f)) return;
            GameObject go;
            try { go = f.gameObject; } catch { return; }   // destroyed component still in FsmList
            if (go == null) return;
            _stamps[f] = ++_stamp;
            int id = go.GetInstanceID();
            List<PlayMakerFSM> l;
            if (!_byGo.TryGetValue(id, out l)) { l = _freeLists.Count > 0 ? _freeLists.Pop() : new List<PlayMakerFSM>(4); _byGo[id] = l; }
            l.Add(f);
        }

        /// <summary>Drops one component from its owner list and stamp index while preserving surviving order.</summary>
        static void Remove(PlayMakerFSM f)
        {
            if (!_stamps.Remove(f)) return;
            GameObject go;
            try { go = f.gameObject; } catch { go = null; }
            if (ReferenceEquals(go, null)) { RemoveEverywhere(f); return; }
            int id = go.GetInstanceID();
            List<PlayMakerFSM> l;
            if (!_byGo.TryGetValue(id, out l)) return;
            for (int i = 0; i < l.Count; i++) if (ReferenceEquals(l[i], f)) { l.RemoveAt(i); break; }
            if (l.Count == 0) { _byGo.Remove(id); _freeLists.Push(l); }
        }

        /// <summary>Repairs an inconsistent owner index by removing the component from every cached list.</summary>
        static void RemoveEverywhere(PlayMakerFSM f)
        {
            foreach (var kv in _byGo)
                for (int i = 0; i < kv.Value.Count; i++) if (ReferenceEquals(kv.Value[i], f)) { kv.Value.RemoveAt(i); return; }
        }

        /// <summary>Checks exact indexed membership without invoking native Unity equality.</summary>
        internal static bool InList(PlayMakerFSM f) { return !ReferenceEquals(f, null) && _stamps.ContainsKey(f); }
        /// <summary>Returns the native enable-order stamp, placing untracked components last.</summary>
        internal static long Stamp(PlayMakerFSM f) { long s; return _stamps.TryGetValue(f, out s) ? s : long.MaxValue; }

        /// <summary>Enabled FSMs on exactly this GameObject, in FsmList order (live list: snapshot before dispatching).</summary>
        internal static List<PlayMakerFSM> On(GameObject go)
        {
            List<PlayMakerFSM> l;
            return _byGo.TryGetValue(go.GetInstanceID(), out l) ? l : null;
        }

        // ---- PM3: fast FsmList removal (reference compare + RemoveAt; skips the guaranteed-miss scan in OnDestroy) ----
        internal static bool FastRemove;
        // Generated accessors (Core Access / MonoMod). NOT HarmonyX FieldRefAccess: its ref-returning delegate read garbage on
        // this runtime (0.1.26 crashed natively in Fsm.OnDestroy -> List.GetEnumerator on a bogus subFsmList).
        static readonly Func<PlayMakerFSM, Fsm> _fsm = Access.Getter<PlayMakerFSM, Fsm>(typeof(PlayMakerFSM), "fsm");
        static readonly Action<PlayMakerFSM, Fsm> _setFsm = Access.Setter<PlayMakerFSM, Fsm>(typeof(PlayMakerFSM), "fsm");

        /// <summary>Removes a component by managed identity at the native removal point, avoiding Unity equality calls.</summary>
        static void RemoveFromFsmList(PlayMakerFSM self, bool knownAbsent)
        {
            if (knownAbsent) return;
            var list = PlayMakerFSM.FsmList;
            for (int i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i], self)) { list.RemoveAt(i); OptimizationMetrics.FastRemovals++; return; }
        }

        /// <summary>Registry handlers hosted by the shared Core lifecycle dispatchers.</summary>
        internal static class Shared
        {
            // PlayMakerFSM.OnEnable appends before calling this. Start/OnExit may immediately broadcast events or enable
            // more FSMs: indexing only in the component postfix misses this receiver and stamps nested enables out of order.
            /// <summary>Shared Fsm.OnEnable prefix: indexes before callbacks so reentrant event dispatch sees the native membership/order.</summary>
            internal static bool OnEnablePre(Fsm __instance)
            {
                if (_users == 0) return true;
                var component = __instance.FsmComponent;
                if (component != null && ReferenceEquals(_fsm(component), __instance)) Add(component);
                return true;
            }

            // Exact copy of PlayMakerFSM.OnDisable with the O(N) Object.Equals Remove replaced (FastRemove on).
            /// <summary>Shared component-disable prefix: preserves Disable callbacks before native list removal and replaces only tracked/fast removal paths.</summary>
            internal static bool OnDisablePre(PlayMakerFSM __instance)
            {
                bool tracked = _users > 0;
                if (!FastRemove && !tracked) return true;
                var fsm = _fsm(__instance);
                if (fsm.Started) fsm.Event(FsmEvent.Disable);
                if (FastRemove) RemoveFromFsmList(__instance, false);   // membership could have changed during Disable
                else PlayMakerFSM.FsmList.Remove(__instance);
                if (tracked) Remove(__instance);
                if (fsm != null && !fsm.Finished) fsm.OnDisable();
                return false;
            }

            // Exact copy of PlayMakerFSM.OnDestroy; after OnDisable the FSM is never in FsmList, so the scan is skipped when known.
            /// <summary>Shared component-destroy prefix: removes indexed membership before clearing the component's native FSM reference.</summary>
            internal static bool OnDestroyPre(PlayMakerFSM __instance)
            {
                bool tracked = _users > 0;
                bool inList = tracked && InList(__instance);
                if (tracked) Remove(__instance);
                if (!FastRemove) return true;
                RemoveFromFsmList(__instance, tracked && !inList);
                var fsm = _fsm(__instance);
                if (fsm != null) fsm.OnDestroy();
                _setFsm(__instance, null);
                return false;
            }
        }
    }

    /// <summary>PM3 feature switch (the registry is shared).</summary>
    public sealed class FastFsmRemovalFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "PM3"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Fast FSM list removal"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Fast FSM disable/destroy"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.PlayMaker; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 3; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Disabling or destroying an FSM searched the whole list of enabled FSMs (slow Unity comparisons). Same result, found by reference."; } }
        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode) { FsmRegistry.Acquire(Module); FsmRegistry.FastRemove = true; }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { FsmRegistry.FastRemove = false; FsmRegistry.Release(); }
        /// <summary>Registers the feature's shared Core dispatcher handlers; handlers gate on the feature state.</summary>
        public override void SubscribeHooks() { FsmRegistry.Subscribe(Module); }
        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { FsmRegistry.Rebuild(); }
    }
}
