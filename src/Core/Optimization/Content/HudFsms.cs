using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// CT7 (plan §7, research B §1.3, §3.5; Sources fsm/scene1_game/__GameManager__.md):
    ///  - Ammo (L74-L127): 10 IntClamp + 20 UiTextSetText every frame; ArmorBackpack (L129-L142): FloatClamp + 5 texts. The texts
    ///    that target PlayerSheet_Canvas (shown only while the player sheet is open: PlayerSheet FSM L734-L742 toggles that Canvas
    ///    component) are written every frame while the sheet canvas is enabled and twice a second while it is hidden. The clamps
    ///    (patched by WW Guns ammo/armor overflow every frame) and the HUD texts on Canvas (ArmorText/ArmorMaxText, read by WW CleanHUD
    ///    Bindings.cs:L50-L51) stay every frame, unchanged.
    ///  - UI_Manager (L861-L889) writes only always-visible HUD texts (Canvas/SurvivalCanvas, NutsText) that CleanHUD reads
    ///    (Bindings.cs:L49-L59): left untouched on purpose (the plan's "only while PlayerSheet open" assumed PlayerSheet texts; the
    ///    string allocations there are PM10's job).
    ///  - DayTime (L382-L403): the "day" state is meant to run every 0.2 s ("wait" Wait 0.2) but its everyFrame CallMethods keep it
    ///    there, so it polls Azure by reflection every frame (systems/world_ui.md:L54). The FSM is paused while in "day" and stepped
    ///    with its own Fsm.Update() every 0.2 s: same CallMethods, same {day}/{Time}/{TimeFloat}, same PlayerSheet text, same
    ///    IntChanged -> "day changed" (Day+1, text, BroadcastAll DayChanged) -> wait2 path, which runs unpaused as vanilla. The day
    ///    change is noticed up to 0.2 s later than vanilla; readers of DayTime.Time (save, WW Patrols clocks) see it 0.2 s stale.
    /// </summary>
    public sealed class HudFsmFeature : Feature
    {
        public override string PlanId { get { return "CT7"; } }
        public override string Key { get { return "HUD FSMs"; } }
        public override string Title { get { return "Player sheet texts and day clock"; } }
        public override string Category { get { return Categories.Content; } }
        public override int Order { get { return 7; } }
        public override string Help { get { return "Hidden player-sheet ammo/armor texts refresh twice a second while the sheet is closed; the day clock runs at its intended 0.2 s instead of every frame."; } }

        internal static bool On;
        readonly UndoLog _undo = new UndoLog();
        Job _job;
        PlayMakerFSM _day; FsmState _dayState; float _nextDay;
        internal static long DaySteps, TextSkips;

        protected override void OnEnable(FeatureMode mode)
        {
            On = true;
            FsmEdits.Watch(this, "Ammo", "__GameManager__", ReadyAmmo);
            FsmEdits.Watch(this, "ArmorBackpack", "__GameManager__", ReadyArmor);
            FsmEdits.Watch(this, "DayTime", "__GameManager__", ReadyDay);
            _job = Scheduler.EveryFrame(Module, "Performance.DayTime", DayTick);
        }

        protected override void OnDisable()
        {
            On = false;
            FsmEdits.Unwatch(this);
            if (_job != null) { _job.Remove(); _job = null; }
            if (_day != null) FsmPause.Clear(_day.Fsm, FsmPause.Stepped);
            _day = null; _dayState = null;
            _undo.RevertAll();
        }

        public override void OnSceneLoaded() { _undo.Prune(); }

        void ReadyAmmo(PlayMakerFSM f)
        {
            var st = FsmEdits.State(f.Fsm, "ammo");
            if (st == null) { FsmEdits.Warn(this, "Ammo/ammo", "state missing"); return; }
            WrapSheetTexts(f, st, "Ammo/ammo", 20);
        }

        void ReadyArmor(PlayMakerFSM f)
        {
            var st = FsmEdits.State(f.Fsm, "armor_backpack");
            if (st == null) { FsmEdits.Warn(this, "ArmorBackpack/armor_backpack", "state missing"); return; }
            WrapSheetTexts(f, st, "ArmorBackpack/armor_backpack", 2);
        }

        /// <summary>Wraps every everyFrame UiTextSetText of the state whose fixed target lives under PlayerSheet_Canvas.</summary>
        void WrapSheetTexts(PlayMakerFSM f, FsmState st, string key, int expected)
        {
            var acts = st.Actions;
            int n = 0;
            for (int i = 0; i < acts.Length; i++)
            {
                var t = acts[i] as UiTextSetText;
                if (t == null || !t.everyFrame || !t.Enabled || t.gameObject == null || t.gameObject.OwnerOption == OwnerDefaultOption.UseOwner) continue;
                var go = t.gameObject.GameObject.Value;
                if (go == null || go.transform.root.name != "PlayerSheet_Canvas") continue;
                FsmEdits.Replace(_undo, f, st, i + 1, new SheetText(t, go.transform.root.GetComponent<Canvas>()));
                n++;
            }
            if (n != expected) FsmEdits.Warn(this, key, "wrapped " + n + " player-sheet texts, expected " + expected + " (kept what matched)");
        }

        /// <summary>UiTextSetText that refreshes every frame while its canvas is enabled, else every 30th frame (staggered).</summary>
        sealed class SheetText : ProxyAction
        {
            readonly Canvas _canvas; readonly int _phase;
            static int _seq;
            public SheetText(UiTextSetText t, Canvas c) : base(t) { _canvas = c; _phase = (_seq++ * 7) % 30; }
            protected override void Tick()
            {
                if (On && _canvas != null && !_canvas.enabled && (Time.frameCount + _phase) % 30 != 0) { TextSkips++; return; }
                base.Tick();
            }
        }

        void ReadyDay(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var gd = FsmEdits.Act<CallMethod>(this, fsm, "day", 1, "DayTime/day#1");
            var gt = FsmEdits.Act<CallMethod>(this, fsm, "day", 2, "DayTime/day#2");
            var ic = FsmEdits.Act<IntChanged>(this, fsm, "day", 5, "DayTime/day#5");
            var w = FsmEdits.Act<Wait>(this, fsm, "wait", 1, "DayTime/wait#1");
            if (gd == null || gt == null || ic == null || w == null) return;
            if (gd.methodName.Value != "GetDay" || gt.methodName.Value != "GetTimeline") { FsmEdits.Warn(this, "DayTime/day", "calls " + gd.methodName.Value + "/" + gt.methodName.Value); return; }
            _day = f; _dayState = FsmEdits.State(fsm, "day"); _nextDay = Time.unscaledTime;
        }

        void DayTick()
        {
            if (!On || _day == null) return;
            if (_day == null) { _day = null; return; }
            var fsm = _day.Fsm;
            bool held = FsmPause.Held(fsm, FsmPause.Stepped);
            bool inDay = fsm.Started && !fsm.Finished && fsm.ActiveState == _dayState && !fsm.IsSwitchingState && fsm.DelayedEvents.Count == 0;
            if (!inDay) { if (held) FsmPause.Clear(fsm, FsmPause.Stepped); return; }
            if (!held) { if (!FsmPause.Set(fsm, FsmPause.Stepped)) return; _nextDay = Time.unscaledTime + 0.2f; return; }   // it just ran this frame's vanilla update
            float now = Time.unscaledTime;
            if (now < _nextDay) return;
            _nextDay += 0.2f;
            if (_nextDay < now) _nextDay = now + 0.2f;
            if (!_day.isActiveAndEnabled) return;
            fsm.Update(); DaySteps++;
            if (fsm.ActiveState != _dayState) FsmPause.Clear(fsm, FsmPause.Stepped);   // day changed: wait2 etc. run vanilla
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Player-sheet text writes skipped", TextSkips.ToString());
            l.Value("Day clock steps", DaySteps + (_day != null && FsmPause.Held(_day.Fsm, FsmPause.Stepped) ? " (stepping)" : ""));
        }
    }
}
