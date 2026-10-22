using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Input;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// CT2 (plan §7, research B §1.2-1.3 / C §2 Tier 1 #5): car simulation LOD for cars nobody uses.
    ///  1. Presentation FSMs paused beyond the preset distance (50 m Balanced): Pedal, gearChange, RpmGear, Handbrake, Start, TireWear,
    ///     Speed, CrashDamage, FuelConsumption, Shake - each only while in its resting state (Start "off", FuelConsumption "off",
    ///     TireWear "off", CrashDamage "event", Shake "off"/"idle", Handbrake On/Off, ...; Sources fsm/prefabs/Poloska.md L151-L322,
    ///     L1195-L1210, L1426-L1550, L1906-L2135, L2202-L2256, L3583-L3610) and only while the engine is off (no fuel use, no shake,
    ///     no exhaust to keep alive). Paused = Fsm.ManualUpdate: events and state changes still work; any state entry wakes it.
    ///     HingeFollow is not touched (its objects are inactive unless the adjust tool is out).
    ///  2. DistanceKinematic (L256-L271) threshold 2000 m -> preset (250 m Balanced) in both FloatCompares, per eligible car.
    ///  3. While such a car is kinematic and beyond that distance, its VehicleController is disabled and every wheel's AutoSimulate
    ///     is off (NWH WheelController.Step never sleeps, research C §0). Re-enabled in a prefix on SetIsKinematic.OnEnter right
    ///     before the FSM makes it dynamic again (and on approach, save, disable). NWH's VC_Disable resets input states and
    ///     transmission gear (VehicleInputHandler.cs:L378-L388, TransmissionComponent.cs:L230-L244): both are saved before and
    ///     written back after re-enabling, so the handbrake/gear the car was parked with are kept.
    /// Never touched: the car the player sits in or drove last, cars joined by a Joint (trailer pairs), cars carrying any WW Patrols
    /// component (crews/pilots drive NWH themselves: src/Modules/Patrols/Crews/Pilot.cs, Crew.cs) and cars carrying Engine Sim
    /// components; cars with the engine running. Visual/simulation change -> follows the preset (off in Vanilla).
    /// </summary>
    public sealed class CarLodFeature : Feature
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "CT2"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Car LOD"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Parked car simulation LOD"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 1; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Cars nobody uses stop running their dashboard/pedal FSMs when far away and their wheel physics while frozen."; } }

        Knob _kinDist, _presDist;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _kinDist = NewKnob(cfg, "Kinematic distance (m)", "Parked cars freeze (and stop simulating wheels) beyond this distance from the player (vanilla 2000).", 150f, 250f, 500f, 2000f);
            _presDist = NewKnob(cfg, "Presentation distance (m)", "Pedal/gauge/handbrake FSMs of parked cars pause beyond this distance.", 30f, 50f, 80f, 0f);
        }

        sealed class Car
        {
            public PlayMakerFSM Kin; public GameObject Go; public Transform T; public Rigidbody Rb; public VehicleController Vc;
            public FloatCompare OnCmp, OffCmp; public float OnOrig, OffOrig; public bool ThresholdSet;
            public readonly List<PlayMakerFSM> Pres = new List<PlayMakerFSM>(16);
            public bool PresPaused, NwhOff; public VehicleInputStates SavedInput; public int SavedGear;
            public float NextForeign; public bool Foreign;
        }

        static readonly string[] PresNames = { "Pedal", "gearChange", "RpmGear", "Handbrake", "Start", "TireWear", "Speed", "CrashDamage", "FuelConsumption", "Shake" };
        static readonly Dictionary<string, string[]> Rest = new Dictionary<string, string[]>
        {
            { "Pedal", new[] { "pedal" } }, { "gearChange", new[] { "H", "R", "1", "2", "3", "4", "5" } }, { "RpmGear", new[] { "RpmGear" } },
            { "Handbrake", new[] { "HandbrakeOn", "HandbrakeOff" } }, { "Start", new[] { "off" } }, { "TireWear", new[] { "off" } },
            { "Speed", new[] { "getSpeed" } }, { "CrashDamage", new[] { "event" } }, { "FuelConsumption", new[] { "off" } }, { "Shake", new[] { "off", "idle" } },
        };

        static readonly List<Car> _cars = new List<Car>(32);
        static readonly Dictionary<GameObject, Car> _byGo = new Dictionary<GameObject, Car>(RefComparer<GameObject>.Instance);
        static readonly HashSet<Fsm> _paused = new HashSet<Fsm>(RefComparer<Fsm>.Instance);
        internal static bool On;
        static int _nwhOffCount;
        Job _job;
        int _cursor;
        float _kin, _pres2;
        GameObject _lastDriven;
        FsmGameObject _player;
        static readonly List<Joint> _joints = new List<Joint>(4);
        static readonly List<PlayMakerFSM> _fsms = new List<PlayMakerFSM>(128);
        internal static long Paused, NwhSleeps, NwhWakes;

        /// <summary>Registers the feature's shared Core dispatcher handlers; handlers gate on the feature state.</summary>
        public override void SubscribeHooks() { Hooks.EnterState.Postfix(Module, AfterEnterState); }

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("SetIsKinematic.OnEnter patch failed");
            On = true;
            ReadKnobs();
            FsmEdits.Watch(this, "DistanceKinematic", null, Ready);
            _job = Scheduler.Interval(Module, "Core.Optimization.CarLod", 0.1f, Tick);
        }

        /// <summary>Caches clamped kinematic and presentation-distance thresholds from the active preset.</summary>
        void ReadKnobs() { _kin = Mathf.Max(50f, _kinDist.Value); float p = Mathf.Max(5f, _presDist.Value); _pres2 = p * p; }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged()
        {
            ReadKnobs();
            for (int i = 0; i < _cars.Count; i++) if (_cars[i].ThresholdSet) SetThreshold(_cars[i], true);
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            On = false;
            FsmEdits.Unwatch(this);
            if (_job != null) { _job.Remove(); _job = null; }
            for (int i = 0; i < _cars.Count; i++) Release(_cars[i]);
            _cars.Clear(); _byGo.Clear(); _paused.Clear(); _nwhOffCount = 0;
        }

        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { _cars.Clear(); _byGo.Clear(); _paused.Clear(); _nwhOffCount = 0; _lastDriven = null; _player = null; FsmPause.Prune(); }
        /// <summary>Restores this feature's saved-object state before the native save transaction.</summary>
        public override void OnSaveBegin() { for (int i = 0; i < _cars.Count; i++) { WakeNwh(_cars[i]); } }

        /// <summary>Validates the native DistanceKinematic FSM and captures thresholds/components for eligible parked-car tracking.</summary>
        void Ready(PlayMakerFSM f)
        {
            var fsm = f.Fsm;
            var on = FsmEdits.Act<FloatCompare>(this, fsm, "KinematicOn", 5, "DistanceKinematic/KinematicOn#5");
            var off = FsmEdits.Act<FloatCompare>(this, fsm, "KinematicOff", 3, "DistanceKinematic/KinematicOff#3");
            if (on == null || off == null) return;
            if (on.float2.UseVariable || off.float2.UseVariable) { FsmEdits.Warn(this, "DistanceKinematic", "threshold is a variable"); return; }
            var go = f.gameObject;
            if (_byGo.ContainsKey(go)) return;
            var car = new Car { Kin = f, Go = go, T = go.transform, Rb = go.GetComponent<Rigidbody>(), Vc = go.GetComponent<VehicleController>(), OnCmp = on, OffCmp = off, OnOrig = on.float2.Value, OffOrig = off.float2.Value };
            // presentation FSMs of this car (once per car; the car's own children only)
            _fsms.Clear();
            go.GetComponentsInChildren(true, _fsms);
            for (int i = 0; i < _fsms.Count; i++)
            {
                var c = _fsms[i];
                if (c != null && Array.IndexOf(PresNames, c.FsmName) >= 0) car.Pres.Add(c);
            }
            _fsms.Clear();
            _cars.Add(car); _byGo[go] = car;
        }

        // ---- per-car decision, a few cars per tick ----
        /// <summary>Processes a bounded round-robin set of tracked parked cars outside native save guards.</summary>
        void Tick()
        {
            if (!On || _cars.Count == 0) return;
            if (_player == null) _player = FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            var player = _player != null ? _player.Value : null;
            if (player == null) return;
            var pt = player.transform;
            Vector3 pp = pt.position;
            int n = Mathf.Min(_cars.Count, Mathf.Max(4, _cars.Count / 3));
            for (int k = 0; k < n; k++)
            {
                if (_cursor >= _cars.Count) _cursor = 0;
                var car = _cars[_cursor];
                if (car.Go == null) { _byGo.Remove(car.Go); ForgetPaused(car); _cars.RemoveAt(_cursor); continue; }
                _cursor++;
                try { Decide(car, pt, pp); }
                catch (Exception e) { Log.Error(OptimizationRuntime.Instance, "CT2 " + car.Go.name, e); Release(car); }
            }
        }

        /// <summary>Chooses presentation pause, native kinematic threshold and NWH sleep from player distance and current eligibility.</summary>
        void Decide(Car car, Transform pt, Vector3 pp)
        {
            if (pt.IsChildOf(car.T)) _lastDriven = car.Go;
            bool eligible = Eligible(car, pt);
            float d2 = (car.T.position - pp).sqrMagnitude;
            // 2. kinematic threshold
            SetThreshold(car, eligible);
            // 3. NWH off while frozen and beyond the threshold
            bool beyond = d2 > _kin * _kin;
            if (eligible && beyond && car.Rb != null && car.Rb.isKinematic && car.Vc != null && car.Vc.enabled && car.Go.activeInHierarchy) SleepNwh(car);
            else if (car.NwhOff && (!eligible || !beyond)) WakeNwh(car);
            // 1. presentation FSMs
            bool pres = eligible && d2 > _pres2;
            if (pres) PausePres(car); else if (car.PresPaused) ResumePres(car);
        }

        /// <summary>Excludes player-used, running-engine and typed module-owned/joint-coupled vehicles before any LOD mutation.</summary>
        bool Eligible(Car car, Transform pt)
        {
            if (car.Go == _lastDriven || pt.IsChildOf(car.T)) return false;
            if (car.Vc == null) return false;
            var eng = car.Vc.powertrain != null ? car.Vc.powertrain.engine : null;
            if (eng != null && (eng.IsRunning || eng.OutputRPM > 1f)) return false;
            if (Time.unscaledTime >= car.NextForeign) { car.NextForeign = Time.unscaledTime + 1f; car.Foreign = Foreign(car); }
            return !car.Foreign;
        }

        /// <summary>Typed module ownership or physical joint coupling excludes vehicles from native parked-car LOD.</summary>
        bool Foreign(Car car)
        {
            var ownership = ModApi.Get<IVehicleOwnership>();
            IModule owner;
            bool f = ownership != null && ownership.IsOwned(car.Go, out owner);
            if (!f)
            {
                _joints.Clear();
                car.Go.GetComponents(_joints);
                for (int i = 0; i < _joints.Count && !f; i++) if (_joints[i] != null && _joints[i].connectedBody != null) f = true;
                _joints.Clear();
                if (!f && car.Rb != null)
                    for (int i = 0; i < _cars.Count && !f; i++)
                    {
                        var o = _cars[i]; if (o == car || o.Go == null) continue;
                        _joints.Clear(); o.Go.GetComponents(_joints);
                        for (int j = 0; j < _joints.Count; j++) if (_joints[j] != null && _joints[j].connectedBody == car.Rb) { f = true; break; }
                        _joints.Clear();
                    }
            }
            return f;
        }

        /// <summary>Applies the configured native kinematic threshold or restores the exact captured thresholds when ineligible.</summary>
        void SetThreshold(Car car, bool eligible)
        {
            if (eligible)
            {
                car.OnCmp.float2.Value = _kin; car.OffCmp.float2.Value = _kin; car.ThresholdSet = true;
            }
            else if (car.ThresholdSet)
            {
                car.OnCmp.float2.Value = car.OnOrig; car.OffCmp.float2.Value = car.OffOrig; car.ThresholdSet = false;
            }
        }

        /// <summary>Pauses only validated resting presentation FSMs using this feature's shared reason bit.</summary>
        void PausePres(Car car)
        {
            car.PresPaused = true;
            for (int i = 0; i < car.Pres.Count; i++)
            {
                var c = car.Pres[i];
                if (c == null) continue;
                var fsm = c.Fsm;
                if (_paused.Contains(fsm)) continue;
                if (!fsm.Started || fsm.Finished || fsm.IsSwitchingState || fsm.DelayedEvents.Count != 0) continue;
                string[] ok;
                if (!Rest.TryGetValue(c.FsmName, out ok) || Array.IndexOf(ok, fsm.ActiveStateName) < 0) continue;
                if (FsmPause.Set(fsm, FsmPause.CarLod)) { _paused.Add(fsm); Paused++; }
            }
        }

        /// <summary>Releases this feature's presentation pause claims without disturbing other optimization reasons.</summary>
        void ResumePres(Car car)
        {
            car.PresPaused = false;
            for (int i = 0; i < car.Pres.Count; i++)
            {
                var c = car.Pres[i];
                if (c == null) continue;
                var fsm = c.Fsm;
                if (_paused.Remove(fsm)) FsmPause.Clear(fsm, FsmPause.CarLod);
            }
        }

        /// <summary>Drops stale per-car paused-FSM bookkeeping after native scene/component destruction.</summary>
        void ForgetPaused(Car car) { for (int i = 0; i < car.Pres.Count; i++) { var c = car.Pres[i]; if (!ReferenceEquals(c, null) && c.Fsm != null && _paused.Remove(c.Fsm)) FsmPause.Clear(c.Fsm, FsmPause.CarLod); } }

        // any state entry of a paused presentation FSM (event while far away) -> run it normally again
        /// <summary>Shared state-entry postfix: releases a parked-car presentation claim before native state behavior changes.</summary>
        static void AfterEnterState(Fsm fsm, FsmState st)
        {
            if (_paused.Count == 0 || !_paused.Contains(fsm)) return;
            _paused.Remove(fsm);
            FsmPause.Clear(fsm, FsmPause.CarLod);
        }

        /// <summary>Captures input/gear and wheel simulation state before suspending eligible distant frozen-vehicle physics.</summary>
        static void SleepNwh(Car car)
        {
            var vc = car.Vc;
            car.SavedInput = vc.input.states;
            car.SavedGear = vc.powertrain.transmission.Gear;
            vc.enabled = false;
            var wheels = vc.powertrain.wheels;
            for (int i = 0; i < wheels.Count; i++) { var w = wheels[i]; if (w != null && w.wheelUAPI != null) w.wheelUAPI.AutoSimulate = false; }
            car.NwhOff = true; _nwhOffCount++; NwhSleeps++;
        }

        /// <summary>Restores captured NWH/wheel simulation plus parked input and gear before native physics resumes.</summary>
        static void WakeNwh(Car car)
        {
            if (!car.NwhOff) return;
            car.NwhOff = false; _nwhOffCount--; NwhWakes++;
            var vc = car.Vc;
            if (vc == null) return;
            vc.enabled = true;                         // WheelComponent.VC_Enable sets AutoSimulate=false again (VC drives the wheels)
            vc.input.states = car.SavedInput;          // VC_Disable reset them
            vc.powertrain.transmission.Gear = car.SavedGear;
        }

        /// <summary>Restores native thresholds, presentation FSMs and vehicle physics for one tracked car.</summary>
        void Release(Car car)
        {
            if (car.Go == null) return;
            WakeNwh(car);
            ResumePres(car);
            SetThreshold(car, false);
        }

        [HarmonyPatch]
        static class Patch
        {
            // DistanceKinematic "KinematicOff" #1 makes the car dynamic: bring NWH back first (same frame, before the physics step).
            /// <summary>SetIsKinematic action prefix: wakes feature-owned NWH state before native code makes the body dynamic.</summary>
            [HarmonyPatch(typeof(SetIsKinematic), "OnEnter"), HarmonyPrefix]
            static void Pre(SetIsKinematic __instance)
            {
                if (_nwhOffCount <= 0 || __instance.isKinematic == null || __instance.isKinematic.Value) return;
                var go = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
                Car car;
                if (go != null && _byGo.TryGetValue(go, out car)) WakeNwh(car);
            }
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            int pres = 0, nwh = 0; for (int i = 0; i < _cars.Count; i++) { if (_cars[i].PresPaused) pres++; if (_cars[i].NwhOff) nwh++; }
            l.Value("Cars tracked / FSMs paused / NWH off", _cars.Count + " / " + _paused.Count + " / " + nwh);
            l.Value("NWH sleeps / wakes", NwhSleeps + " / " + NwhWakes);
        }
    }
}
