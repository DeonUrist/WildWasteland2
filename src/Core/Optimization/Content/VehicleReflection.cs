using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.VehiclePhysics2;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// CT4 (plan §7, research B §2 #2 #8 #9, §3.3): the per-car FSMs Pedal (x3), RpmGear and Handbrake read/write NWH through
    /// GetProperty/SetProperty, i.e. FsmProperty reflection with boxing on every call (FsmProperty.cs:L246-L370), every frame on every
    /// car (Sources fsm/prefabs/Poloska.md: Pedal L3583-L3610, RpmGear L294-L301, Handbrake L1524-L1531). Those actions are swapped for
    /// stand-ins that do the same member access directly: same target (the action's own TargetObject, read on every call like
    /// FsmProperty.CheckForReinitialize), same members (input.Brakes/Clutch/Throttle, powertrain.engine.OutputRPM,
    /// powertrain.transmission.Gear, input.Handbrake setter with its clamp), same store variables, same OnEnter/OnUpdate/everyFrame
    /// lifecycle in the same slot of the state. State flow and values are identical; only reflection and boxing are gone.
    /// Each edit is verified (FSM, state, action number, GetProperty/SetProperty, property path, VehicleController target type).
    /// </summary>
    public sealed class VehicleReflectionFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "CT4"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Vehicle reflection"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Direct NWH access for car FSMs"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Content; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 4; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Pedal, RPM/gear and handbrake FSMs read and write the vehicle physics directly instead of by reflection (exact)."; } }

        internal static bool On;
        readonly UndoLog _undo = new UndoLog();
        internal static long Swapped;

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            On = true;
            FsmEdits.Watch(this, "Pedal", null, ReadyPedal);
            FsmEdits.Watch(this, "RpmGear", null, ReadyRpmGear);
            FsmEdits.Watch(this, "Handbrake", null, ReadyHandbrake);
        }
        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable() { On = false; FsmEdits.Unwatch(this); _undo.RevertAll(); }
        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { _undo.Prune(); }
        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l) { l.Value("Actions replaced (live / total)", _undo.Count + " / " + Swapped); }

        // Member ids
        internal const int Brakes = 0, Clutch = 1, Throttle = 2, Rpm = 3, Gear = 4, Handbrake = 5;

        /// <summary>Validates native Pedal property actions before replacing their reflection with typed NWH reads.</summary>
        void ReadyPedal(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var g = FsmEdits.Act<GetProperty>(this, fsm, "pedal", 1, "Pedal/pedal#1");
            if (g == null) return;
            string p = g.targetProperty != null ? g.targetProperty.PropertyName : null;
            int which = p == "input.Brakes" ? Brakes : p == "input.Clutch" ? Clutch : p == "input.Throttle" ? Throttle : -1;
            if (which < 0 || !IsVc(g.targetProperty)) { FsmEdits.Warn(this, "Pedal/pedal#1", "reads " + p); return; }
            Swap(f, "pedal", 1, new NwhGet(g, which));
        }

        /// <summary>Validates native RPM/gear property actions before replacing their reflection with typed NWH reads.</summary>
        void ReadyRpmGear(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var r = FsmEdits.Act<GetProperty>(this, fsm, "RpmGear", 2, "RpmGear/RpmGear#2");
            var g = FsmEdits.Act<GetProperty>(this, fsm, "RpmGear", 3, "RpmGear/RpmGear#3");
            if (r == null || g == null) return;
            if (r.targetProperty.PropertyName != "powertrain.engine.OutputRPM" || !IsVc(r.targetProperty)) { FsmEdits.Warn(this, "RpmGear#2", "reads " + r.targetProperty.PropertyName); return; }
            if (g.targetProperty.PropertyName != "powertrain.transmission.Gear" || !IsVc(g.targetProperty)) { FsmEdits.Warn(this, "RpmGear#3", "reads " + g.targetProperty.PropertyName); return; }
            Swap(f, "RpmGear", 2, new NwhGet(r, Rpm));
            Swap(f, "RpmGear", 3, new NwhGet(g, Gear));
        }

        /// <summary>Validates native handbrake property actions before replacing their reflection with typed NWH writes.</summary>
        void ReadyHandbrake(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var on = FsmEdits.Act<SetProperty>(this, fsm, "HandbrakeOn", 3, "Handbrake/HandbrakeOn#3");
            var off = FsmEdits.Act<SetProperty>(this, fsm, "HandbrakeOff", 3, "Handbrake/HandbrakeOff#3");
            if (on != null && on.targetProperty.PropertyName == "input.Handbrake" && IsVc(on.targetProperty)) Swap(f, "HandbrakeOn", 3, new NwhSetHandbrake(on));
            else if (on != null) FsmEdits.Warn(this, "Handbrake/HandbrakeOn#3", "writes " + on.targetProperty.PropertyName);
            if (off != null && off.targetProperty.PropertyName == "input.Handbrake" && IsVc(off.targetProperty)) Swap(f, "HandbrakeOff", 3, new NwhSetHandbrake(off));
            else if (off != null) FsmEdits.Warn(this, "Handbrake/HandbrakeOff#3", "writes " + off.targetProperty.PropertyName);
        }

        /// <summary>Checks that a reflected property targets the native VehicleController component.</summary>
        static bool IsVc(FsmProperty p)
        {
            if (p == null || p.TargetObject == null) return false;
            string t = p.TargetObject.UseVariable ? p.TargetObject.TypeName : (p.TargetObject.Value != null ? p.TargetObject.Value.GetType().FullName : p.TargetTypeName);
            return t == "NWH.VehiclePhysics2.VehicleController";
        }

        /// <summary>Replaces a validated native action and records the original slot for feature shutdown.</summary>
        void Swap(PlayMakerFSM f, string state, int number, ProxyAction w)
        {
            var st = FsmEdits.State(f.Fsm, state);
            if (st == null) return;
            FsmEdits.Replace(_undo, f, st, number, w);
            Swapped++;
        }

        /// <summary>GetProperty of one known VehicleController member, read directly. Off -> the original GetProperty.</summary>
        sealed class NwhGet : ProxyAction
        {
            readonly GetProperty _g; readonly int _which;
            /// <summary>Retains the native getter and selects the equivalent NWH value to read.</summary>
            public NwhGet(GetProperty g, int which) : base(g) { _g = g; _which = which; }
            /// <summary>Runs the direct NWH operation at native action entry and preserves its every-frame completion rule.</summary>
            protected override void Enter()
            {
                if (!On) { base.Enter(); return; }
                Do();
                if (!_g.everyFrame) FinishInner();
            }
            /// <summary>Repeats the direct NWH operation only when the original action requested every-frame execution.</summary>
            protected override void Tick() { if (!On) { base.Tick(); return; } Do(); }
            /// <summary>Performs the wrapper's typed NWH operation, falling back to the retained native action when unavailable.</summary>
            void Do()
            {
                // FsmProperty.GetValue: target = TargetObject.Value (re-read every call); null target -> nothing written
                var vc = _g.targetProperty.TargetObject.Value as VehicleController;
                if (ReferenceEquals(vc, null)) return;
                var p = _g.targetProperty;
                switch (_which)
                {
                    case Brakes: p.FloatParameter.Value = vc.input.Brakes; break;
                    case Clutch: p.FloatParameter.Value = vc.input.Clutch; break;
                    case Throttle: p.FloatParameter.Value = vc.input.Throttle; break;
                    case Rpm: p.FloatParameter.Value = vc.powertrain.engine.OutputRPM; break;
                    case Gear: p.IntParameter.Value = vc.powertrain.transmission.Gear; break;
                }
            }
        }

        /// <summary>SetProperty input.Handbrake = FloatParameter (the setter clamps to 0..1 like the reflective write).</summary>
        sealed class NwhSetHandbrake : ProxyAction
        {
            readonly SetProperty _s;
            /// <summary>Retains the native setter used by the direct NWH handbrake wrapper.</summary>
            public NwhSetHandbrake(SetProperty s) : base(s) { _s = s; }
            /// <summary>Runs the direct NWH operation at native action entry and preserves its every-frame completion rule.</summary>
            protected override void Enter()
            {
                if (!On) { base.Enter(); return; }
                Do();
                if (!_s.everyFrame) FinishInner();
            }
            /// <summary>Repeats the direct NWH operation only when the original action requested every-frame execution.</summary>
            protected override void Tick() { if (!On) { base.Tick(); return; } Do(); }
            /// <summary>Performs the wrapper's typed NWH operation, falling back to the retained native action when unavailable.</summary>
            void Do()
            {
                var vc = _s.targetProperty.TargetObject.Value as VehicleController;
                if (ReferenceEquals(vc, null)) return;
                var v = _s.targetProperty.FloatParameter;
                if (v == null || v.IsNone) return;              // FsmProperty.SetValue skips a None parameter
                vc.input.Handbrake = v.Value;
            }
        }
    }
}
