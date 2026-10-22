using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// PM2 (plan §4, research A R2/R3): SendEvent with target GameObject / GameObjectFSM copied and scanned the whole FsmList
    /// (every enabled FSM; with "send to children" also every FSM's parent chain) for each event. These replacements resolve the
    /// receivers through <see cref="FsmRegistry"/> in exactly the same order (FsmList order = enable stamp), keep the EventData
    /// copies/restores, excludeSelf and snapshot semantics (receivers fixed before dispatch, re-entrant safe), and skip only
    /// the scan. Shadow: vanilla runs; the receiver list is computed both ways and compared.
    /// </summary>
    public sealed class TargetedEventsFeature : Feature
    {
        public override string PlanId { get { return "PM2"; } }
        public override string Key { get { return "Targeted events"; } }
        public override string Title { get { return "Fast targeted events"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 2; } }
        public override bool SupportsShadow { get { return true; } }
        public override string Help { get { return "Events sent to an object no longer search every FSM in the game (stalls on POI entry, save and load)."; } }

        internal static bool On, Shadow;
        static bool _acquired;
        static string _foreign;   // another mod's Harmony id already patching a dispatch method (PM2 then stays out)
        protected override void OnEnable(FeatureMode mode)
        {
            Instance = this;
            _foreign = ForeignPatcher();
            if (_foreign != null)
            {
                WildWasteland.Core.Log.Always("[Performance] PM2: Fsm event dispatch is already patched by " + _foreign + " (e.g. Burner's Utilities FastEvents); PM2 not patched");
                return;
            }
            FsmRegistry.Acquire(Module); _acquired = true;
            if (!EnsurePatched(typeof(Patch))) throw new Exception("Fsm event dispatch patch failed");
            Shadow = mode == FeatureMode.Shadow; On = mode == FeatureMode.On;
        }
        protected override void OnDisable() { On = Shadow = false; if (_acquired) { _acquired = false; FsmRegistry.Release(); } }
        public override void SubscribeHooks() { FsmRegistry.Subscribe(Module); }
        public override void OnSceneLoaded() { FsmRegistry.Rebuild(); }
        public override void DrawStats(ToolLayout l)
        {
            if (_foreign != null) l.Text("Not active: event dispatch is already patched by " + _foreign + ".");
            l.Value("Indexed FSMs", FsmRegistry.Count.ToString());
        }

        /// <summary>Harmony id of another mod with a prefix/transpiler on either dispatch method; null = none (WW's own ids ignored).</summary>
        static string ForeignPatcher()
        {
            var targets = new[]
            {
                AccessTools.Method(typeof(Fsm), "SendEventToFsmOnGameObject", new[] { typeof(GameObject), typeof(string), typeof(FsmEvent) }),
                AccessTools.Method(typeof(Fsm), "BroadcastEventToGameObject", new[] { typeof(GameObject), typeof(FsmEvent), typeof(FsmEventData), typeof(bool), typeof(bool) }),
            };
            foreach (var m in targets)
            {
                if (m == null) continue;
                var info = Harmony.GetPatchInfo(m);
                if (info == null) continue;
                foreach (var p in info.Prefixes) if (!p.owner.StartsWith(Plugin.GUID, StringComparison.Ordinal)) return p.owner;
                foreach (var p in info.Transpilers) if (!p.owner.StartsWith(Plugin.GUID, StringComparison.Ordinal)) return p.owner;
            }
            return null;
        }
        internal static TargetedEventsFeature Instance;

        // ---- pooled, re-entrant buffers ----
        static readonly Stack<List<PlayMakerFSM>> _pmPool = new Stack<List<PlayMakerFSM>>();
        static readonly Stack<List<Fsm>> _fsmPool = new Stack<List<Fsm>>();
        static List<PlayMakerFSM> RentPm() { return _pmPool.Count > 0 ? _pmPool.Pop() : new List<PlayMakerFSM>(16); }
        static void Return(List<PlayMakerFSM> l) { l.Clear(); _pmPool.Push(l); }
        static List<Fsm> RentFsm() { return _fsmPool.Count > 0 ? _fsmPool.Pop() : new List<Fsm>(16); }
        static void Return(List<Fsm> l) { l.Clear(); _fsmPool.Push(l); }
        static readonly List<PlayMakerFSM> _children = new List<PlayMakerFSM>(64);
        static readonly Comparison<PlayMakerFSM> _byStamp = (a, b) => FsmRegistry.Stamp(a).CompareTo(FsmRegistry.Stamp(b));

        /// <summary>Receivers of SendEventToFsmOnGameObject, in FsmList order (all FSMs on the object; caller picks by name).</summary>
        static void OnObject(GameObject go, List<PlayMakerFSM> into)
        {
            var l = FsmRegistry.On(go);
            if (l != null) into.AddRange(l);
        }

        /// <summary>Receivers of BroadcastEventToGameObject: FSMs on go (+ its subtree), FsmList order.</summary>
        static void Collect(GameObject go, bool children, List<Fsm> into)
        {
            if (!children)
            {
                var l = FsmRegistry.On(go);
                if (l != null) for (int i = 0; i < l.Count; i++) { var f = l[i]; if (!(f == null)) into.Add(f.Fsm); }
                return;
            }
            _children.Clear();
            go.GetComponentsInChildren(true, _children);
            int n = 0;
            for (int i = 0; i < _children.Count; i++) if (FsmRegistry.InList(_children[i])) _children[n++] = _children[i];
            _children.RemoveRange(n, _children.Count - n);
            if (n > 1) _children.Sort(_byStamp);
            for (int i = 0; i < _children.Count; i++) { var f = _children[i]; if (!(f == null)) into.Add(f.Fsm); }
            _children.Clear();
        }

        // ---- shadow: vanilla's selection, recomputed without side effects ----
        static void VanillaCollect(GameObject go, bool children, List<Fsm> into)
        {
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null) continue;
                if (fsm.gameObject == go) into.Add(fsm.Fsm);
                else if (children)
                {
                    for (var t = fsm.gameObject.transform; t != null; t = t.parent) if (t == go.transform) { into.Add(fsm.Fsm); break; }
                }
            }
        }

        static void Compare(List<Fsm> a, List<Fsm> b, string what)
        {
            bool same = a.Count == b.Count;
            for (int i = 0; same && i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) same = false;
            if (!same && Instance != null) Instance.Mismatch(what + ": vanilla " + a.Count + " receivers, optimized " + b.Count);
        }

        static bool _stoodDown;
        static void StandDown()
        {
            if (_stoodDown || (!On && !Shadow)) return;
            _stoodDown = true;
            WildWasteland.Core.Log.Always("[Performance] PM2: another mod already dispatches events sent to objects (e.g. Burner's Utilities FastEvents); PM2 leaves those to it");
        }

        [HarmonyPatch]
        static class Patch
        {
            // Fsm.SendEventToFsmOnGameObject(GameObject, string, FsmEvent)
            // Last + __runOriginal: backstop for a mod that patches after PM2 started (ForeignPatcher only sees earlier ones). Its prefix
            // already dispatched and skipped the original; dispatching again would deliver the event twice (toggle FSM pairs flip back).
            [HarmonyPatch(typeof(Fsm), "SendEventToFsmOnGameObject", typeof(GameObject), typeof(string), typeof(FsmEvent)), HarmonyPrefix, HarmonyPriority(Priority.Last)]
            static bool SendToFsm(GameObject gameObject, string fsmName, FsmEvent fsmEvent, bool __runOriginal)
            {
                if (!__runOriginal) { StandDown(); return false; }
                if (!On && !Shadow) return true;
                Telemetry.SendGoFsm++;
                if (Shadow)
                {
                    if (gameObject == null) return true;
                    var va = RentFsm(); var vb = RentFsm(); var pm = RentPm();
                    try
                    {
                        foreach (var f in PlayMakerFSM.FsmList) if (f != null && f.gameObject == gameObject && (string.IsNullOrEmpty(fsmName) || fsmName == f.Fsm.Name)) { va.Add(f.Fsm); if (!string.IsNullOrEmpty(fsmName)) break; }
                        OnObject(gameObject, pm);
                        for (int i = 0; i < pm.Count; i++) { var f = pm[i]; if (f != null && (string.IsNullOrEmpty(fsmName) || fsmName == f.Fsm.Name)) { vb.Add(f.Fsm); if (!string.IsNullOrEmpty(fsmName)) break; } }
                        Compare(va, vb, "SendEventToFsmOnGameObject " + gameObject.name + " fsm '" + fsmName + "' " + (fsmEvent != null ? fsmEvent.Name : ""));
                    }
                    finally { Return(va); Return(vb); Return(pm); }
                    return true;
                }
                if (gameObject == null) return false;
                Fsm.SetEventDataSentByInfo();
                var snap = RentPm();
                try
                {
                    OnObject(gameObject, snap);
                    if (string.IsNullOrEmpty(fsmName))
                    {
                        for (int i = 0; i < snap.Count; i++) { var item = snap[i]; if (item != null && item.gameObject == gameObject) item.Fsm.ProcessEvent(fsmEvent); }
                    }
                    else
                    {
                        for (int i = 0; i < snap.Count; i++)
                        {
                            var item = snap[i];
                            if (item != null && item.gameObject == gameObject && fsmName == item.Fsm.Name) { item.Fsm.ProcessEvent(fsmEvent); break; }
                        }
                    }
                }
                finally { Return(snap); }
                return false;
            }

            // Fsm.BroadcastEventToGameObject(GameObject, FsmEvent, FsmEventData, bool sendToChildren, bool excludeSelf)
            [HarmonyPatch(typeof(Fsm), "BroadcastEventToGameObject", typeof(GameObject), typeof(FsmEvent), typeof(FsmEventData), typeof(bool), typeof(bool)), HarmonyPrefix, HarmonyPriority(Priority.Last)]
            static bool SendToObject(Fsm __instance, GameObject go, FsmEvent fsmEvent, FsmEventData eventData, bool sendToChildren, bool excludeSelf, bool __runOriginal)
            {
                if (!__runOriginal) { StandDown(); return false; }
                if (!On && !Shadow) return true;
                if (sendToChildren) Telemetry.SendGoChildren++; else Telemetry.SendGo++;
                if (Shadow)
                {
                    if (go == null) return true;
                    var va = RentFsm(); var vb = RentFsm();
                    try
                    {
                        VanillaCollect(go, sendToChildren, va); Collect(go, sendToChildren, vb);
                        Compare(va, vb, "BroadcastEventToGameObject " + go.name + (sendToChildren ? " +children " : " ") + (fsmEvent != null ? fsmEvent.Name : ""));
                    }
                    finally { Return(va); Return(vb); }
                    return true;
                }
                if (go == null) return false;
                var eventData2 = new FsmEventData(Fsm.EventData);
                Fsm.SetEventDataSentByInfo();
                var list = RentFsm();
                try
                {
                    Collect(go, sendToChildren, list);
                    if (excludeSelf) list.Remove(__instance);
                    for (int i = 0; i < list.Count; i++)
                    {
                        Fsm.EventData = new FsmEventData(eventData);
                        list[i].ProcessEvent(fsmEvent);
                    }
                }
                finally { Return(list); }
                Fsm.EventData = eventData2;
                return false;
            }
        }
    }

    /// <summary>PM4: BroadcastAll without the throw-away copy of FsmList (pooled snapshot; per-receiver EventData copies kept).</summary>
    public sealed class BroadcastFeature : Feature
    {
        public override string PlanId { get { return "PM4"; } }
        public override string Key { get { return "Broadcast events"; } }
        public override string Title { get { return "Broadcast without list copies"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 4; } }
        public override string Help { get { return "Game-wide events (Save/Load settings, day change, faction changes) reuse one buffer instead of copying every FSM."; } }
        static bool On;
        protected override void OnEnable(FeatureMode mode) { if (!EnsurePatched(typeof(Patch))) throw new Exception("broadcast patch failed"); On = true; }
        protected override void OnDisable() { On = false; }

        static readonly Stack<PlayMakerFSM[]> _pool = new Stack<PlayMakerFSM[]>();
        static PlayMakerFSM[] Snapshot(out int n)
        {
            var src = PlayMakerFSM.FsmList; n = src.Count;
            var a = _pool.Count > 0 ? _pool.Pop() : null;
            if (a == null || a.Length < n) a = new PlayMakerFSM[Mathf.NextPowerOfTwo(Mathf.Max(256, n))];
            src.CopyTo(a, 0);
            return a;
        }
        static void Release(PlayMakerFSM[] a, int n) { Array.Clear(a, 0, n); _pool.Push(a); }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(Fsm), "BroadcastEvent", typeof(FsmEvent), typeof(bool)), HarmonyPrefix]
            static bool FsmBroadcast(Fsm __instance, FsmEvent fsmEvent, bool excludeSelf)
            {
                if (!On) return true;
                Telemetry.Broadcasts++;
                var eventData = new FsmEventData(Fsm.EventData);
                Fsm.SetEventDataSentByInfo();
                var source = new FsmEventData(Fsm.EventData);
                int n; var snap = Snapshot(out n);
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        var item = snap[i];
                        if (!(item == null) && item.Fsm != null && (!excludeSelf || item.Fsm != __instance))
                        {
                            Fsm.EventData = new FsmEventData(source);
                            item.Fsm.ProcessEvent(fsmEvent);
                        }
                    }
                }
                finally { Release(snap, n); }
                Fsm.EventData = eventData;
                return false;
            }

            [HarmonyPatch(typeof(PlayMakerFSM), "BroadcastEvent", typeof(FsmEvent)), HarmonyPrefix]
            static bool PmBroadcast(FsmEvent fsmEvent)
            {
                if (!On) return true;
                Telemetry.Broadcasts++;
                int n; var snap = Snapshot(out n);
                try
                {
                    for (int i = 0; i < n; i++) { var item = snap[i]; if (!(item == null) && item.Fsm != null) item.Fsm.ProcessEvent(fsmEvent); }
                }
                finally { Release(snap, n); }
                return false;
            }
        }
    }

}
