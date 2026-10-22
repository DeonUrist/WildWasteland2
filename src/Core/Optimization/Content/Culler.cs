using System;
using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// CT1 (plan §7, research B §3.2): sliced, de-phased replacement of the four culling sweeps. Vanilla (all Sources
    /// fsm/scene1_game/__GameManager__.md and NewGO_ArrayList.md, read completely):
    ///  - __GameManager__ [DeactivateFarAwayItems] L405: every 5 s game time, each ArrayList_Items entry: layer 22 (Map icons) ->
    ///    SetActive(true); else GetDistance({G:Player}, item) &lt; 1000 -> SetActive(true), &gt; 1000 -> SetActive(false)
    ///    (ActivateGameObject recursive=false = GameObject.SetActive). Exactly 1000 (or NaN) leaves the FSM stuck in "check" for the
    ///    rest of the session (no FINISHED transition) - the culler just skips that entry.
    ///  - [DeleteFarAwayItems] L449: every 10 s: Items not on layer 22 farther than 5000 m, Cars farther than 5000 m (no layer check),
    ///    Sandstorms farther than 50000 m -> Destroy. "Perma base" (Gameplay_Canvas.md:L236-L251) is EnableFSM(DeleteFarAwayItems,
    ///    false): this pass runs only while that component is enabled, exactly like vanilla. WW Patrols' War Rig protection on that
    ///    FSM's GetDistance is honoured through <see cref="PatrolsBridge"/> (skip protected, detach tankers before deleting a truck).
    ///  - [DeleteFallingItems] L428: every 10 s: Items and Cars whose world Y is not above -10000 (FloatCompare greaterThan=back, so
    ///    equal and NaN are destroyed too) -> Destroy.
    ///  - NewGO_ArrayList [DeleteDestroyedItems] L11: every 5 s remove destroyed entries from Cars, Items, Sandstorms (vanilla skips the
    ///    entry after each removal, NewGO_ArrayList.md:L19-L21; the culler removes all of them - same list content one cycle earlier).
    /// Null/destroyed entries are no-ops in every sweep (vanilla actions bail on a null GameObject). Same lists (proxies on
    /// NewGO_ArrayList by referenceName, as ArrayListGetNext resolves them), same thresholds, same player ({G:Player}).
    /// Differences (intended): entries are processed over several frames (64 per frame, at most 8 SetActive/Destroy per frame), the
    /// timers start at random phases (no 10 s phase lock), there is no 10000-entry loop limit that silently disabled the vanilla FSM
    /// (Fsm.cs:L2742), work pauses while the game is paused or a save/load is running, Destroy happens in the frame the entry is
    /// reached instead of all in one frame. Patrols' raider cars (in ArrayList_Cars) get exactly the vanilla car treatment.
    /// Vanilla sweeps are neutralised by disabling the Wait action of their idle state (the FSM then idles in it forever; the
    /// component stays enabled so perma base keeps controlling DeleteFarAwayItems); undo re-enables the Wait and restarts the state.
    /// </summary>
    public sealed class CullerFeature : Feature
    {
        public override string PlanId { get { return "CT1"; } }
        public override string Key { get { return "Culler"; } }
        public override string Title { get { return "Sliced item culling"; } }
        public override string Category { get { return Categories.Content; } }
        public override int Order { get { return 1; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "The game's 5/10-second item culling and cleanup sweeps run a few entries per frame instead of the whole list in one frame (same rules)."; } }

        internal static bool On;
        static CullerFeature _self;
        readonly UndoLog _undo = new UndoLog();
        Job _job;

        // vanilla components neutralised (index = Kind)
        enum Kind { Deactivate, DeleteFar, Falling, Compact }
        static readonly PlayMakerFSM[] _owned = new PlayMakerFSM[4];
        static readonly float[] _period = { 5f, 10f, 10f, 5f };
        static readonly float[] _due = new float[4];
        static readonly bool[] _queued = new bool[4];
        static readonly Queue<Kind> _queue = new Queue<Kind>();

        // current pass
        static bool _running; static Kind _kind; static int _list; static int _cursor;
        static bool _patrols;
        const int EntriesPerFrame = 64, ChangesPerFrame = 8;

        PlayMakerArrayListProxy _items, _cars, _sand; GameObject _arraysGo;
        FsmGameObject _player;

        internal static long Activated, Deactivated, Destroyed, Compacted, Passes, Skipped1000;
        static int _lastPassFrames, _passStartFrame;

        protected override void OnEnable(FeatureMode mode)
        {
            _self = this; On = true;
            for (int i = 0; i < 4; i++) { _due[i] = _period[i] * UnityEngine.Random.value; _queued[i] = false; _owned[i] = null; }
            _queue.Clear(); _running = false;
            FsmEdits.Watch(this, "DeactivateFarAwayItems", "__GameManager__", ReadyDeactivate);
            FsmEdits.Watch(this, "DeleteFarAwayItems", "__GameManager__", ReadyDeleteFar);
            FsmEdits.Watch(this, "DeleteFallingItems", "__GameManager__", ReadyFalling);
            FsmEdits.Watch(this, "DeleteDestroyedItems", "NewGO_ArrayList", ReadyCompact);
            _job = Scheduler.Sliced(Module, "Performance.Culler", 1000, Step);
        }

        protected override void OnDisable()
        {
            On = false;
            FsmEdits.Unwatch(this);
            if (_job != null) { _job.Remove(); _job = null; }
            _undo.RevertAll();
            for (int i = 0; i < 4; i++) _owned[i] = null;
            _running = false; _queue.Clear();
        }

        public override void OnSceneLoaded()
        {
            _undo.Prune();
            _items = _cars = _sand = null; _arraysGo = null; _running = false; _queue.Clear();
            for (int i = 0; i < 4; i++) _queued[i] = false;
        }

        public override void OnSaveBegin() { _running = false; _queue.Clear(); for (int i = 0; i < 4; i++) _queued[i] = false; }

        // ---- neutralising the vanilla sweeps ----
        void ReadyDeactivate(PlayMakerFSM f) { Neutralise(f, Kind.Deactivate, "wait", 1, 5f); }
        void ReadyDeleteFar(PlayMakerFSM f) { Neutralise(f, Kind.DeleteFar, "wait", 1, 10f); }
        void ReadyFalling(PlayMakerFSM f) { Neutralise(f, Kind.Falling, "wait", 1, 10f); }
        void ReadyCompact(PlayMakerFSM f) { Neutralise(f, Kind.Compact, "off", 2, 5f); }

        void Neutralise(PlayMakerFSM comp, Kind k, string state, int number, float seconds)
        {
            var fsm = comp.Fsm;
            string key = fsm.Name + "/" + state + "#" + number;
            var wait = FsmEdits.Act<Wait>(this, fsm, state, number, key);
            if (wait == null) return;
            if (wait.realTime || wait.time == null || wait.time.UseVariable || Math.Abs(wait.time.Value - seconds) > 0.001f || wait.finishEvent == null || wait.finishEvent.Name != "next")
            { FsmEdits.Warn(this, key, "Wait is not " + seconds + " s -> next"); return; }
            if (!VerifySweep(fsm, k)) return;
            if (!wait.Enabled) return;     // already off (someone else): leave it
            wait.Enabled = false;
            _undo.Add(new SweepUndo(comp, wait, state));
            _owned[(int)k] = comp;
            // mid-game: the Wait may already be running in the idle state -> re-enter so the disabled Wait is skipped
            if (comp.enabled && fsm.Started && !fsm.Finished && fsm.ActiveStateName == state) fsm.SetState(state);
        }

        /// <summary>Checks the thresholds the culler hard-codes are still the game's (game-update safety).</summary>
        bool VerifySweep(Fsm fsm, Kind k)
        {
            switch (k)
            {
                case Kind.Deactivate:
                    return CheckCompare(fsm, "check", 4, 1000f, "DeactivateFarAwayItems/check#4") && CheckLayer(fsm, "check", 2, "DeactivateFarAwayItems/check#2")
                        && CheckList(fsm, "ArrayList Items", "ArrayList_Items");
                case Kind.DeleteFar:
                    return CheckCompare(fsm, "check", 4, 5000f, "DeleteFarAwayItems/check#4") && CheckCompare(fsm, "check 2", 2, 5000f, "DeleteFarAwayItems/check 2#2")
                        && CheckCompare(fsm, "check 3", 2, 50000f, "DeleteFarAwayItems/check 3#2") && CheckLayer(fsm, "check", 2, "DeleteFarAwayItems/check#2")
                        && CheckList(fsm, "ArrayList Items", "ArrayList_Items") && CheckList(fsm, "ArrayList Cars", "ArrayList_Cars") && CheckList(fsm, "ArrayList Sandstorms", "ArrayList_Sandstorms");
                case Kind.Falling:
                    return CheckCompare(fsm, "check", 2, -10000f, "DeleteFallingItems/check#2") && CheckCompare(fsm, "check 2", 2, -10000f, "DeleteFallingItems/check 2#2")
                        && CheckList(fsm, "ArrayList Items", "ArrayList_Items") && CheckList(fsm, "ArrayList Cars", "ArrayList_Cars");
                default:
                    return CheckList(fsm, "Items", "ArrayList_Items") && CheckList(fsm, "cars", "ArrayList_Cars") && CheckList(fsm, "Sandstorms", "ArrayList_Sandstorms");
            }
        }
        bool CheckCompare(Fsm fsm, string state, int n, float v, string key)
        {
            var c = FsmEdits.Act<FloatCompare>(this, fsm, state, n, key);
            if (c == null) return false;
            if (c.float2.UseVariable || Math.Abs(c.float2.Value - v) > 0.01f) { FsmEdits.Warn(this, key, "threshold is " + c.float2.Value + ", expected " + v); return false; }
            return true;
        }
        bool CheckLayer(Fsm fsm, string state, int n, string key)
        {
            var c = FsmEdits.Act<IntCompare>(this, fsm, state, n, key);
            if (c == null) return false;
            if (c.integer2.UseVariable || c.integer2.Value != 22) { FsmEdits.Warn(this, key, "layer is " + c.integer2.Value + ", expected 22"); return false; }
            return true;
        }
        bool CheckList(Fsm fsm, string state, string reference)
        {
            var g = FsmEdits.Act<ArrayListGetNext>(this, fsm, state, 1, fsm.Name + "/" + state + "#1");
            if (g == null) return false;
            if (g.reference == null || g.reference.Value != reference) { FsmEdits.Warn(this, fsm.Name + "/" + state, "list is " + (g.reference != null ? g.reference.Value : "null") + ", expected " + reference); return false; }
            return true;
        }

        sealed class SweepUndo : Undo
        {
            readonly PlayMakerFSM _c; readonly Wait _w; readonly string _state;
            public SweepUndo(PlayMakerFSM c, Wait w, string state) { _c = c; _w = w; _state = state; }
            internal override bool Alive { get { return _c != null; } }
            internal override void Revert()
            {
                _w.Enabled = true;
                var fsm = _c.Fsm;
                if (_c.enabled && fsm.Started && !fsm.Finished && fsm.ActiveStateName == _state) fsm.SetState(_state);   // restart the Wait
            }
        }

        // ---- the sliced job ----
        bool Resolve()
        {
            if (_player == null) _player = FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            var go = GameRefs.NewGOArrayList;
            if (go == null) return false;
            if (go != _arraysGo || _items == null || _cars == null || _sand == null)
            {
                _arraysGo = go; _items = _cars = _sand = null;
                var proxies = go.GetComponents<PlayMakerArrayListProxy>();   // once per scene
                for (int i = 0; i < proxies.Length; i++)
                {
                    var p = proxies[i]; if (p == null) continue;
                    if (_items == null && p.referenceName == "ArrayList_Items") _items = p;
                    else if (_cars == null && p.referenceName == "ArrayList_Cars") _cars = p;
                    else if (_sand == null && p.referenceName == "ArrayList_Sandstorms") _sand = p;
                }
            }
            return _items != null && _cars != null && _sand != null;
        }

        static bool ComponentRuns(PlayMakerFSM c) { return c != null && c.enabled && c.gameObject.activeInHierarchy; }

        bool Step()
        {
            if (!On) return false;
            if (Time.timeScale <= 0f || SaveBus.Busy) return false;
            int frame = Time.frameCount;
            if (_lastTimerFrame != frame)
            {
                // timers in game time (like the vanilla Wait realTime=false), random initial phase; one queued pass per kind
                _lastTimerFrame = frame;
                float dt = Time.deltaTime;
                for (int i = 0; i < 4; i++)
                {
                    if (_owned[i] == null) continue;
                    _due[i] -= dt;
                    if (_due[i] > 0f) continue;
                    _due[i] += _period[i];
                    if (_due[i] < 0f) _due[i] = _period[i];
                    if (!_queued[i]) { _queued[i] = true; _queue.Enqueue((Kind)i); }
                }
            }
            if (!_running)
            {
                if (_queue.Count == 0) return false;
                var k = _queue.Dequeue(); _queued[(int)k] = false;
                if (!ComponentRuns(_owned[(int)k]) || !Resolve()) return true;     // vanilla component off (perma base) or lists missing: no pass
                var p = _player != null ? _player.Value : null;
                if (p == null && k != Kind.Compact && k != Kind.Falling) return true;   // vanilla would compare stale distances: skip
                _running = true; _kind = k; _list = 0; _cursor = 0; _passStartFrame = frame; Passes++;
                _patrols = k == Kind.DeleteFar && PatrolsBridge.Active();
                if (k == Kind.Compact) { CompactAll(); _running = false; _lastPassFrames = 1; return true; }
            }
            if (_lastSliceFrame == frame) return false;      // one slice of entries per frame
            _lastSliceFrame = frame;
            RunSlice();
            return !_running && _queue.Count > 0;
        }

        static int _lastTimerFrame = -1, _lastSliceFrame = -1;

        void RunSlice()
        {
            Telemetry.Mark("cull");
            var player = _player != null ? _player.Value : null;
            // scene teardown mid-pass: never compare against a missing player (would read as distance from the origin)
            if (_owned[(int)_kind] == null || (player == null && _kind != Kind.Falling)) { _running = false; return; }
            Vector3 pp = player != null ? player.transform.position : Vector3.zero;
            int entries = 0, changes = 0;
            while (entries < EntriesPerFrame && changes < ChangesPerFrame)
            {
                ArrayList list = CurrentList();
                if (list == null) { _running = false; _lastPassFrames = Time.frameCount - _passStartFrame + 1; return; }
                if (_cursor >= list.Count) { _list++; _cursor = 0; continue; }
                var go = list[_cursor] as GameObject;
                _cursor++; entries++;
                if (go == null) continue;
                switch (_kind)
                {
                    case Kind.Deactivate:
                    {
                        if (go.layer == 22) { if (!go.activeSelf) { go.SetActive(true); Activated++; changes++; } break; }
                        float d = Vector3.Distance(pp, go.transform.position);
                        if (d < 1000f) { if (!go.activeSelf) { go.SetActive(true); Activated++; changes++; } }
                        else if (d > 1000f) { if (go.activeSelf) { go.SetActive(false); Deactivated++; changes++; } }
                        else Skipped1000++;
                        break;
                    }
                    case Kind.DeleteFar:
                    {
                        if (_list == 0 && go.layer == 22) break;
                        float limit = _list == 2 ? 50000f : 5000f;
                        float d = Vector3.Distance(pp, go.transform.position);
                        if (!(d > limit)) break;
                        if (_patrols) { if (PatrolsBridge.Protected(go)) break; PatrolsBridge.BeforeFarDelete(go); }
                        UnityEngine.Object.Destroy(go); Destroyed++; changes++;
                        break;
                    }
                    case Kind.Falling:
                    {
                        float y = go.transform.position.y;
                        if (!(y > -10000f)) { UnityEngine.Object.Destroy(go); Destroyed++; changes++; }
                        break;
                    }
                }
            }
        }

        /// <summary>Lists of the current pass in vanilla order: Deactivate = Items; DeleteFar = Items, Cars, Sandstorms; Falling = Items, Cars.</summary>
        ArrayList CurrentList()
        {
            switch (_kind)
            {
                case Kind.Deactivate: return _list == 0 ? _items.arrayList : null;
                case Kind.DeleteFar: return _list == 0 ? _items.arrayList : _list == 1 ? _cars.arrayList : _list == 2 ? _sand.arrayList : null;
                case Kind.Falling: return _list == 0 ? _items.arrayList : _list == 1 ? _cars.arrayList : null;
            }
            return null;
        }

        void CompactAll()
        {
            Compact(_cars.arrayList); Compact(_items.arrayList); Compact(_sand.arrayList);
        }

        static void Compact(ArrayList l)
        {
            if (l == null) return;
            int n = 0, count = l.Count;
            for (int i = 0; i < count; i++)
            {
                object o = l[i];
                if (o is UnityEngine.Object && (UnityEngine.Object)o == null) continue;   // destroyed (vanilla GameObjectIsNull)
                if (o == null) continue;
                if (n != i) l[n] = o;
                n++;
            }
            if (n < count) { l.RemoveRange(n, count - n); Compacted += count - n; }
        }

        public override void DrawStats(ToolLayout l)
        {
            if (_items != null) l.Value("Items / cars / sandstorms", _items.arrayList.Count + " / " + _cars.arrayList.Count + " / " + _sand.arrayList.Count);
            l.Value("Vanilla sweeps replaced", (_owned[0] != null ? "deactivate " : "") + (_owned[1] != null ? "far-delete " : "") + (_owned[2] != null ? "falling " : "") + (_owned[3] != null ? "compact" : ""));
            l.Value("Passes / last pass (frames)", Passes + " / " + _lastPassFrames);
            l.Value("Activated / deactivated / destroyed / compacted", Activated + " / " + Deactivated + " / " + Destroyed + " / " + Compacted);
            if (Skipped1000 > 0) l.Value("Entries at exactly 1000 m (vanilla FSM would stall)", Skipped1000.ToString());
        }
    }
}
