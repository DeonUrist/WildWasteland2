using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>Called once for each PlayMakerFSM instance a feature watches: when its actions are loaded (postfix on
    /// PlayMakerFSM.Awake, i.e. before its start state is entered) and, when the watch is added mid-game, for every instance that
    /// already woke up. Handlers verify what they edit (<see cref="FsmEdits.Act{T}"/>) and record undo entries.</summary>
    internal delegate void FsmReadyHandler(PlayMakerFSM fsm);

    /// <summary>
    /// Shared helper for content edits (plan §2 "Content edits"): features register watches keyed by FSM name (+ optional owner-root
    /// name pattern); edits are located by (state name, action number as printed in Sources fsm dumps = 1-based array index, expected
    /// action type) and verified before anything is changed. A mismatch (game update changed the FSM) is logged once per key and the
    /// edit is skipped. Every change goes into the feature's <see cref="UndoLog"/>, which restores it on disable.
    /// Hook point: postfix on the private PlayMakerFSM.Awake (after Init -> Fsm.Init -> InitData loaded the actions, before Start
    /// enters the start state). No WW module patches PlayMakerFSM.Awake (grep src/, 2026-10-15). Already-awake instances are found
    /// with one Resources.FindObjectsOfTypeAll scan when a watch is added (shared within a frame).
    /// </summary>
    internal static class FsmEdits
    {
        sealed class WatchEntry { public Feature Owner; public string Root; public FsmReadyHandler Ready; }
        static readonly Dictionary<string, List<WatchEntry>> _byName = new Dictionary<string, List<WatchEntry>>(StringComparer.Ordinal);
        static readonly List<WatchEntry> _any = new List<WatchEntry>();
        static int _count;
        static PlayMakerFSM[] _live; static int _liveFrame = -1;
        static readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        internal static long Delivered, Mismatches;

        /// <summary>Watch every FSM named <paramref name="fsmName"/> ("*" = every FSM; keep the handler cheap then) whose owner's root
        /// object name matches <paramref name="rootPattern"/> (null = any; '*' wildcards, '|' alternatives).</summary>
        internal static void Watch(Feature owner, string fsmName, string rootPattern, FsmReadyHandler ready)
        {
            if (!owner.Module.EnsurePatched(typeof(AwakePatch))) throw new Exception("PlayMakerFSM.Awake patch failed");
            var w = new WatchEntry { Owner = owner, Root = rootPattern, Ready = ready };
            if (fsmName == "*") _any.Add(w);
            else
            {
                List<WatchEntry> l;
                if (!_byName.TryGetValue(fsmName, out l)) { l = new List<WatchEntry>(2); _byName[fsmName] = l; }
                l.Add(w);
            }
            _count++;
            // instances that already ran Awake (mid-game switch-on, scene objects awake before the module started)
            var live = LiveFsms();
            for (int i = 0; i < live.Length; i++)
            {
                var f = live[i];
                if (f == null) continue;
                var fsm = f.Fsm;
                if (fsm == null || !fsm.Initialized) continue;          // not awake yet: the Awake postfix delivers it later
                if (fsmName != "*" && fsm.Name != fsmName) continue;
                Deliver(w, f);
            }
        }

        internal static void Unwatch(Feature owner)
        {
            foreach (var kv in _byName) _count -= kv.Value.RemoveAll(w => w.Owner == owner);
            _count -= _any.RemoveAll(w => w.Owner == owner);
            if (_count < 0) _count = 0;
        }

        /// <summary>Scene instances (prefab assets excluded), one scan per frame shared by all watches added in that frame.</summary>
        internal static PlayMakerFSM[] LiveFsms()
        {
            if (_liveFrame == Time.frameCount && _live != null) return _live;
            var all = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var f = all[i];
                if (f == null) continue;
                GameObject go;
                try { go = f.gameObject; } catch { continue; }
                if (go == null || !go.scene.IsValid()) continue;       // prefab asset / not in a scene
                all[n++] = f;
            }
            Array.Resize(ref all, n);
            _live = all; _liveFrame = Time.frameCount;
            return all;
        }

        static void Deliver(WatchEntry w, PlayMakerFSM f)
        {
            if (w.Root != null && !RootMatches(f, w.Root)) return;
            Delivered++;
            try { w.Ready(f); }
            catch (Exception e) { Warn(w.Owner, "handler:" + f.FsmName, e.GetType().Name + ": " + e.Message); }
        }

        static void Dispatch(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            if (fsm == null) return;
            List<WatchEntry> l;
            if (_byName.Count > 0 && _byName.TryGetValue(fsm.Name, out l))
                for (int i = 0; i < l.Count; i++) Deliver(l[i], f);
            for (int i = 0; i < _any.Count; i++) Deliver(_any[i], f);
        }

        /// <summary>'*' wildcard match of the root object's name; '|' separates alternatives ("Poloska*|Rustallion*").</summary>
        internal static bool RootMatches(PlayMakerFSM f, string pattern)
        {
            string name = f.transform.root.gameObject.name;     // allocates; only on Awake of a watched FSM name
            int start = 0;
            while (start <= pattern.Length)
            {
                int bar = pattern.IndexOf('|', start);
                string alt = bar < 0 ? pattern.Substring(start) : pattern.Substring(start, bar - start);
                if (Wild(name, alt)) return true;
                if (bar < 0) break;
                start = bar + 1;
            }
            return false;
        }

        static bool Wild(string s, string p)
        {
            if (p == "*") return true;
            bool lead = p.StartsWith("*", StringComparison.Ordinal), trail = p.EndsWith("*", StringComparison.Ordinal);
            string core = p.Trim('*');
            if (lead && trail) return s.IndexOf(core, StringComparison.Ordinal) >= 0;
            if (lead) return s.EndsWith(core, StringComparison.Ordinal);
            if (trail) return s.StartsWith(core, StringComparison.Ordinal);
            return s == core;
        }

        internal static FsmState State(Fsm fsm, string name)
        {
            var st = fsm.States;
            if (st == null) return null;
            for (int i = 0; i < st.Length; i++) if (st[i] != null && st[i].Name == name) return st[i];
            return null;
        }

        /// <summary>The action printed as "<paramref name="number"/>. <typeparamref name="T"/>" under STATE "<paramref name="state"/>" in the
        /// Sources dump (1-based, disabled actions included), or null after logging a mismatch once for <paramref name="key"/>.</summary>
        internal static T Act<T>(Feature owner, Fsm fsm, string state, int number, string key) where T : FsmStateAction
        {
            var s = State(fsm, state);
            if (s == null) { Warn(owner, key, "state \"" + state + "\" not found"); return null; }
            var acts = s.Actions;
            if (acts == null || number < 1 || number > acts.Length) { Warn(owner, key, "state \"" + state + "\" has " + (acts == null ? 0 : acts.Length) + " actions, expected #" + number); return null; }
            var a = acts[number - 1];
            var t = a as T;
            if (t == null) { Warn(owner, key, "action #" + number + " in \"" + state + "\" is " + (a == null ? "null" : a.GetType().Name) + ", expected " + typeof(T).Name); return null; }
            return t;
        }

        internal static void Warn(Feature owner, string key, string why)
        {
            Mismatches++;
            string k = owner.PlanId + "|" + key + "|" + why;
            if (_warned.Count > 500 || !_warned.Add(k)) return;
            Log.Warn("[Performance] " + owner.PlanId + " " + key + ": " + why + " - left vanilla (game updated?)");
        }

        /// <summary>Swaps the action at <paramref name="number"/> (1-based) of <paramref name="state"/> for <paramref name="wrapper"/> and
        /// records the undo. The wrapper must keep the original action's behaviour when its feature is off (it may still be in
        /// ActiveActions when the swap is undone).</summary>
        internal static void Replace(UndoLog log, PlayMakerFSM comp, FsmState state, int number, FsmStateAction wrapper)
        {
            var acts = state.Actions;
            var original = acts[number - 1];
            acts[number - 1] = wrapper;
            log.Add(new ReplaceUndo(comp, state, number - 1, original, wrapper));
        }

        [HarmonyPatch]
        internal static class AwakePatch
        {
            [HarmonyPatch(typeof(PlayMakerFSM), "Awake"), HarmonyPostfix]
            static void Post(PlayMakerFSM __instance)
            {
                // vanilla never sets ManualUpdate (research A): a fresh FSM carrying it was Instantiate-copied from one we had paused
                // (the flag is serialized); nobody would ever resume the copy
                if (FsmPause.Count > 0) { var f = __instance.Fsm; if (f != null && f.ManualUpdate) f.ManualUpdate = false; }
                if (_count == 0) return;
                try { Dispatch(__instance); }
                catch (Exception e) { Log.Error("[Performance] FsmEdits dispatch", e); }
            }
        }

        // ---- undo records ----
        sealed class ReplaceUndo : Undo
        {
            readonly PlayMakerFSM _c; readonly FsmState _s; readonly int _i; readonly FsmStateAction _orig, _wrap;
            public ReplaceUndo(PlayMakerFSM c, FsmState s, int i, FsmStateAction o, FsmStateAction w) { _c = c; _s = s; _i = i; _orig = o; _wrap = w; }
            internal override bool Alive { get { return _c != null; } }
            internal override void Revert()
            {
                var a = _s.Actions;
                if (a != null && _i < a.Length && ReferenceEquals(a[_i], _wrap)) a[_i] = _orig;
            }
        }
    }

    /// <summary>One reversible change.</summary>
    internal abstract class Undo
    {
        internal abstract bool Alive { get; }
        internal abstract void Revert();
    }

    /// <summary>Per-feature list of changes, reverted newest first. Entries of destroyed objects are pruned as the log grows and on
    /// scene load.</summary>
    internal sealed class UndoLog
    {
        readonly List<Undo> _items = new List<Undo>(64);
        int _pruneAt = 1024;
        internal int Count { get { return _items.Count; } }
        internal void Add(Undo u)
        {
            _items.Add(u);
            if (_items.Count >= _pruneAt) { Prune(); _pruneAt = Math.Max(1024, _items.Count * 2); }
        }
        internal void Prune()
        {
            int n = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                bool alive;
                try { alive = _items[i].Alive; } catch { alive = false; }
                if (alive) _items[n++] = _items[i];
            }
            _items.RemoveRange(n, _items.Count - n);
        }
        internal void RevertAll()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                try { if (_items[i].Alive) _items[i].Revert(); }
                catch (Exception e) { Log.Error("[Performance] undo", e); }
            }
            _items.Clear(); _pruneAt = 1024;
        }
        internal void Clear() { _items.Clear(); _pruneAt = 1024; }
    }

    internal sealed class EnabledUndo : Undo
    {
        readonly PlayMakerFSM _c; readonly FsmStateAction _a; readonly bool _was;
        public EnabledUndo(PlayMakerFSM c, FsmStateAction a, bool was) { _c = c; _a = a; _was = was; }
        internal override bool Alive { get { return _c != null; } }
        internal override void Revert() { _a.Enabled = _was; }
    }

    internal sealed class FloatUndo : Undo
    {
        readonly PlayMakerFSM _c; readonly FsmFloat _v; readonly float _was;
        public FloatUndo(PlayMakerFSM c, FsmFloat v, float was) { _c = c; _v = v; _was = was; }
        internal override bool Alive { get { return _c != null; } }
        internal override void Revert() { _v.Value = _was; }
    }

    /// <summary>
    /// Ownership of Fsm.ManualUpdate (PlayMakerFSM.Update skips Fsm.Update while it is set; events, trigger/collision callbacks and
    /// state changes still run). Several features may pause the same FSM for different reasons (bits); it runs again only when all
    /// reasons are gone. An FSM whose ManualUpdate was already set by someone else (another feature/module/mod) is never touched,
    /// and if someone clears it while we hold it, <see cref="Held"/> notices and drops our claim.
    /// </summary>
    internal static class FsmPause
    {
        internal const int CarLod = 1, PollSleep = 2, Stepped = 4;
        static readonly Dictionary<Fsm, int> _bits = new Dictionary<Fsm, int>(RefComparer<Fsm>.Instance);
        static readonly List<Fsm> _tmp = new List<Fsm>();
        internal static int Count { get { return _bits.Count; } }

        /// <summary>Claims <paramref name="bit"/>; false if the FSM is paused by somebody else.</summary>
        internal static bool Set(Fsm f, int bit)
        {
            int b;
            if (_bits.TryGetValue(f, out b))
            {
                if (!f.ManualUpdate) { _bits.Remove(f); b = 0; }    // released by someone else: start over
                else { _bits[f] = b | bit; return true; }
            }
            if (f.ManualUpdate) return false;
            f.ManualUpdate = true;
            _bits[f] = bit;
            return true;
        }

        internal static void Clear(Fsm f, int bit)
        {
            int b;
            if (!_bits.TryGetValue(f, out b) || (b & bit) == 0) return;
            b &= ~bit;
            if (b == 0) { _bits.Remove(f); f.ManualUpdate = false; }
            else _bits[f] = b;
        }

        /// <summary>True while our <paramref name="bit"/> still holds the FSM paused.</summary>
        internal static bool Held(Fsm f, int bit)
        {
            int b;
            if (!_bits.TryGetValue(f, out b) || (b & bit) == 0) return false;
            if (!f.ManualUpdate) { _bits.Remove(f); return false; }
            return true;
        }

        internal static void ClearAll(int bit)
        {
            _tmp.Clear();
            foreach (var kv in _bits) if ((kv.Value & bit) != 0) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) Clear(_tmp[i], bit);
            _tmp.Clear();
        }

        /// <summary>Forget FSMs whose component is gone (scene load, periodic).</summary>
        internal static void Prune()
        {
            _tmp.Clear();
            foreach (var kv in _bits) { var o = kv.Key.Owner; if (o == null) _tmp.Add(kv.Key); }
            for (int i = 0; i < _tmp.Count; i++) _bits.Remove(_tmp[i]);
            _tmp.Clear();
        }
    }
}
