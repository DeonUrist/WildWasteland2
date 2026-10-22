using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Smooth weapon switch timing: slot events are held while the drawn weapon lowers (0.14 s) and replayed; the lowering pose is the Presentation part.</summary>
    internal static partial class WeaponTransition
    {
        private static PlayMakerFSM _handler, _switcher;
        // (WW perf) their plain Fsm objects, refreshed once a frame in Tick: the hooks (every FSM event / every ActivateGameObject in the
        // game) first compare against these by reference instead of two Unity null checks per call
        private static Fsm _handlerFsm, _switcherFsm;
        private static FsmEvent _pending;
        private static FsmEventData _data;
        private static Transform _posed;
        private static Vector3 _position;
        private static Quaternion _rotation;
        private static float _nextFind, _at, _weight;
        private static bool _replaying, _lowering, _holster;
        private const float Duration = .14f;

        /// <summary>Subscribes the switch event prefix, the activation postfix, the timing job and the (Presentation-part) lowering pose job.</summary>
        internal static void Init(IModule owner)
        {
            Hooks.ProcessEvent.Prefix(owner, BeforeEvent);
            Hooks.ActivateGameObjectEnter.Postfix(owner, AfterActivate);
            Scheduler.EveryFrame(owner, "Gunplay.WeaponTransition", Tick, Phase.EarlyUpdate);
            Scheduler.EveryFrame(owner, "Gunplay.WeaponTransitionPose", Pose, Phase.LateUpdateLast);   // refs-ok: Pose is the Presentation-part method of this partial class (REFACTOR-REQUEST B09-5), not the Presentation type Pose
        }
        /// <summary>The native WeaponInHand weaponType of the drawn weapon ("hands" or empty = none); null before the rig is found.</summary>
        internal static string CurrentWeaponType
        {
            get
            {
                if (_handler == null) return null;
                var w = _handler.FsmVariables.FindFsmString("weaponType");
                return w != null ? w.Value : null;
            }
        }
        /// <summary>A smooth weapon switch is lowering/raising (or an event waits for its replay).</summary>
        internal static bool Switching { get { return _lowering || _pending != null || _weight > .001f; } }
        /// <summary>Current lowering weight 0..1 of the switch.</summary>
        internal static float Weight { get { return _weight; } }

        /// <summary>Drops rig references and the switch state; the pose is restored.</summary>
        internal static void Reset()
        {
            Restore(); _handler = _switcher = null; _handlerFsm = _switcherFsm = null; _pending = null; _data = null;
            _weight = _nextFind = 0f; _lowering = _replaying = _holster = false;
        }
        /// <summary>World ready: weapons get holstered once the Weapons FSM runs.</summary>
        internal static void StartWorld() { _holster = true; }
        /// <summary>Restores the pose and replays a held-back switch event.</summary>
        internal static void Shutdown()
        {
            Restore();
            if (_pending != null && _handler != null && GameRefs.InPlay) Replay();
            Reset();
        }
        /// <summary>Sends the held-back slot event to the Weapons FSM (the switch continues).</summary>
        private static void Replay()
        {
            var ev = _pending; var data = _data; _pending = null; _data = null;
            _replaying = true;
            try { _switcher.Fsm.ProcessEvent(ev, data); }
            finally { _replaying = false; }
        }
        /// <summary>Core.Hooks.ProcessEvent prefix (every FSM event): holds a slot/back event of the Weapons FSM while the drawn weapon lowers; false = held. First filter: reference compare with the cached Weapons FSM.</summary>
        private static bool BeforeEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!ReferenceEquals(fsm, _switcherFsm) || _switcherFsm == null) return true;
            if (_switcher == null || _handler == null || !ReferenceEquals(fsm, _switcher.Fsm) || _replaying || !GunplaySettings.SmoothWeaponSwitch.Value || ev == null) return true;
            if (ev.Name != "weapon 1" && ev.Name != "weapon 2" && ev.Name != "weapon 3" && ev.Name != "back") return true;
            if (!fsm.ActiveStateName.StartsWith("Slot ", System.StringComparison.Ordinal)) return true;
            if (!GameRefs.InGame || PlayerState.WeaponsParent == null) return true;
            var weapon = _handler.FsmVariables.FindFsmString("weaponType");
            if (weapon == null || string.IsNullOrEmpty(weapon.Value) || weapon.Value == "hands") return true;
            _pending = ev; _data = data;
            if (!_lowering) { _at = Time.time; _lowering = true; }
            if (Log.L2) GunplayLog.Deep("Weapon transition: lower " + weapon.Value + " for " + ev.Name);
            return false;
        }
        /// <summary>Core.Hooks.ActivateGameObjectEnter postfix: WeaponInHand activated a new weapon model, it raises. First filter: reference compare.</summary>
        private static void AfterActivate(ActivateGameObject action)
        {
            if (!ReferenceEquals(action.Fsm, _handlerFsm) || _handlerFsm == null) return;
            if (_handler == null || !ReferenceEquals(action.Fsm, _handler.Fsm) || !GunplaySettings.SmoothWeaponSwitch.Value || !action.activate.Value) return;
            if (action.Fsm.ActiveStateName == "hands") return;
            _at = Time.time; _weight = 1f; _lowering = false;
            if (Log.L2) GunplayLog.Deep("Weapon transition: raise " + action.Fsm.ActiveStateName);
        }
        private static Core.Events.WeaponSwitchStage _lastStage = Core.Events.WeaponSwitchStage.Done;

        /// <summary>Job "Gunplay.WeaponTransition" (EarlyUpdate): switch timing, then the WeaponSwitchPhase event while it moves.</summary>
        private static void Tick()
        {
            TickSwitch();
            if (!ModEvents.HasListeners<Core.Events.WeaponSwitchPhase>()) return;
            var stage = _lowering ? Core.Events.WeaponSwitchStage.Lowering : _pending != null ? Core.Events.WeaponSwitchStage.Switching
                      : _weight > .001f ? Core.Events.WeaponSwitchStage.Raising : Core.Events.WeaponSwitchStage.Done;
            if (stage == Core.Events.WeaponSwitchStage.Done && _lastStage == stage) return;
            _lastStage = stage;
            CombatEvents.SwitchPhase(stage, _weight);
        }

        /// <summary>The switch work of one frame: rig lookup, world-ready holster, lowering/raising weight and the delayed event replay.</summary>
        private static void TickSwitch()
        {
            Restore();
            if (_handler == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var camera = PlayerState.PlayerCamera;
                var weapons = camera != null ? camera.Find("HandItemUse/Weapons") : null;
                if (weapons == null && camera != null) weapons = camera.Find("Weapons");
                if (weapons != null)
                {
                    _handler = FsmUtil.Find(weapons.gameObject, "WeaponInHand");
                    _switcher = FsmUtil.Find(weapons.gameObject, "Weapons");
                }
            }
            _handlerFsm = _handler != null ? _handler.Fsm : null;
            _switcherFsm = _switcher != null ? _switcher.Fsm : null;
            if (_holster && _switcher != null && _switcher.Fsm.Initialized && GameRefs.InPlay)
            {
                _holster = false; _pending = null; _data = null; _lowering = false; _weight = 0f;
                _at = Time.time - Duration;
                _switcher.SendEvent("Activate"); // Native off 2 also deactivates WeaponInHand and all arm models.
                if (Log.L2) GunplayLog.Deep("World ready: weapons holstered");
            }
            if (!GunplaySettings.SmoothWeaponSwitch.Value)
            {
                if (_pending != null && _handler != null) Replay();
                _weight = 0f; _lowering = false; return;
            }
            if (!GameRefs.InGame) return;
            float t = Mathf.Clamp01((Time.time - _at) / Duration);
            float ease = t * t * (3f - 2f * t);
            _weight = _lowering ? ease : 1f - ease;
            if (_lowering && t >= 1f && _pending != null && _handler != null)
            { _lowering = false; _at = Time.time; Replay(); }
        }
    }
}
