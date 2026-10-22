using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Performance
{
    /// <summary>Reference-identity comparer (UnityEngine.Object.Equals goes native; the registry only needs identity).</summary>
    internal sealed class RefComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly RefComparer<T> Instance = new RefComparer<T>();
        public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
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
        internal static bool Tracking { get { return _users > 0; } }
        static long _stamp;
        static readonly Dictionary<PlayMakerFSM, long> _stamps = new Dictionary<PlayMakerFSM, long>(RefComparer<PlayMakerFSM>.Instance);
        static readonly Dictionary<int, List<PlayMakerFSM>> _byGo = new Dictionary<int, List<PlayMakerFSM>>();
        static readonly Stack<List<PlayMakerFSM>> _freeLists = new Stack<List<PlayMakerFSM>>();
        internal static int Count { get { return _stamps.Count; } }
        internal static long Rebuilds;

        internal static void Acquire(PerformanceModule m)
        {
            if (!m.EnsurePatched(typeof(Patch))) throw new Exception("PlayMakerFSM OnEnable/OnDisable/OnDestroy patch failed");
            if (_users++ == 0) Rebuild();
        }
        /// <summary>OnEnable goes through the shared Core.Hooks.FsmEnable postfix (Patrols also uses it). Called from SubscribeHooks.</summary>
        static bool _subscribed;
        internal static void Subscribe(IModule m)
        {
            if (_subscribed) return;
            _subscribed = true;
            Hooks.FsmEnable.Postfix(m, OnEnablePost);
        }
        internal static void Unsubscribed() { _subscribed = false; }
        static void OnEnablePost(PlayMakerFSM f) { if (_users > 0) Add(f); }
        internal static void Release() { if (_users > 0 && --_users == 0) Clear(); }

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

        static void RemoveEverywhere(PlayMakerFSM f)
        {
            foreach (var kv in _byGo)
                for (int i = 0; i < kv.Value.Count; i++) if (ReferenceEquals(kv.Value[i], f)) { kv.Value.RemoveAt(i); return; }
        }

        internal static bool InList(PlayMakerFSM f) { return !ReferenceEquals(f, null) && _stamps.ContainsKey(f); }
        internal static long Stamp(PlayMakerFSM f) { long s; return _stamps.TryGetValue(f, out s) ? s : long.MaxValue; }

        /// <summary>Enabled FSMs on exactly this GameObject, in FsmList order (live list: snapshot before dispatching).</summary>
        internal static List<PlayMakerFSM> On(GameObject go)
        {
            List<PlayMakerFSM> l;
            return _byGo.TryGetValue(go.GetInstanceID(), out l) ? l : null;
        }

        // ---- PM3: fast FsmList removal (reference compare + RemoveAt; skips the guaranteed-miss scan in OnDestroy) ----
        internal static bool FastRemove;
        // Generated accessors (FastReflect / MonoMod). NOT HarmonyX FieldRefAccess: its ref-returning delegate read garbage on
        // this runtime (0.1.26 crashed natively in Fsm.OnDestroy -> List.GetEnumerator on a bogus subFsmList).
        static readonly Func<PlayMakerFSM, Fsm> _fsm = FastReflect.Getter<PlayMakerFSM, Fsm>(typeof(PlayMakerFSM), "fsm");
        static readonly Action<PlayMakerFSM, Fsm> _setFsm = FastReflect.Setter<PlayMakerFSM, Fsm>(typeof(PlayMakerFSM), "fsm");

        static void RemoveFromFsmList(PlayMakerFSM self, bool knownAbsent)
        {
            if (knownAbsent) return;
            var list = PlayMakerFSM.FsmList;
            for (int i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i], self)) { list.RemoveAt(i); Telemetry.FastRemovals++; return; }
        }

        [HarmonyPatch]
        internal static class Patch
        {
            // PlayMakerFSM.OnEnable appends before calling this. Start/OnExit may immediately broadcast events or enable
            // more FSMs: indexing only in the component postfix misses this receiver and stamps nested enables out of order.
            [HarmonyPatch(typeof(Fsm), "OnEnable"), HarmonyPrefix]
            static void OnEnablePre(Fsm __instance)
            {
                if (_users == 0) return;
                var component = __instance.FsmComponent;
                if (component != null && ReferenceEquals(_fsm(component), __instance)) Add(component);
            }

            // Exact copy of PlayMakerFSM.OnDisable with the O(N) Object.Equals Remove replaced (FastRemove on).
            [HarmonyPatch(typeof(PlayMakerFSM), "OnDisable"), HarmonyPrefix]
            static bool OnDisablePre(PlayMakerFSM __instance)
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
            [HarmonyPatch(typeof(PlayMakerFSM), "OnDestroy"), HarmonyPrefix]
            static bool OnDestroyPre(PlayMakerFSM __instance)
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
        public override string PlanId { get { return "PM3"; } }
        public override string Key { get { return "Fast FSM list removal"; } }
        public override string Title { get { return "Fast FSM disable/destroy"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 3; } }
        public override string Help { get { return "Disabling or destroying an FSM searched the whole list of enabled FSMs (slow Unity comparisons). Same result, found by reference."; } }
        protected override void OnEnable(FeatureMode mode) { FsmRegistry.Acquire(Module); FsmRegistry.FastRemove = true; }
        protected override void OnDisable() { FsmRegistry.FastRemove = false; FsmRegistry.Release(); }
        public override void SubscribeHooks() { FsmRegistry.Subscribe(Module); }
        public override void OnSceneLoaded() { FsmRegistry.Rebuild(); }
    }
}
