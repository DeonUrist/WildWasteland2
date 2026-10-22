using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class WeaponTransition
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

        internal static void Init(GunsModule owner)
        {
            Hooks.ProcessEvent.Prefix(owner, BeforeEvent);
            Hooks.ActivateGameObjectEnter.Postfix(owner, AfterActivate);
            Scheduler.EveryFrame(owner, "WeaponTransition", Tick, Phase.EarlyUpdate);
            Scheduler.EveryFrame(owner, "WeaponTransitionPose", Pose, Phase.LateUpdateLast);
        }
        internal static void Reset()
        {
            Restore(); _handler = _switcher = null; _handlerFsm = _switcherFsm = null; _pending = null; _data = null;
            _weight = _nextFind = 0f; _lowering = _replaying = _holster = false;
        }
        internal static void StartWorld() { _holster = true; }
        internal static void Shutdown()
        {
            Restore();
            if (_pending != null && _handler != null && GameRefs.InPlay) Replay();
            Reset();
        }
        private static void Restore()
        {
            if (_posed != null) { _posed.localPosition = _position; _posed.localRotation = _rotation; }
            _posed = null;
        }
        private static void Replay()
        {
            var ev = _pending; var data = _data; _pending = null; _data = null;
            _replaying = true;
            try { _switcher.Fsm.ProcessEvent(ev, data); }
            finally { _replaying = false; }
        }
        private static bool BeforeEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!ReferenceEquals(fsm, _switcherFsm) || _switcherFsm == null) return true;
            if (_switcher == null || _handler == null || !ReferenceEquals(fsm, _switcher.Fsm) || _replaying || !GunsModule.SmoothWeaponSwitch.Value || ev == null) return true;
            if (ev.Name != "weapon 1" && ev.Name != "weapon 2" && ev.Name != "weapon 3" && ev.Name != "back") return true;
            if (!fsm.ActiveStateName.StartsWith("Slot ", System.StringComparison.Ordinal)) return true;
            if (!GameRefs.InGame || PlayerRig.Parent == null) return true;
            var weapon = _handler.FsmVariables.FindFsmString("weaponType");
            if (weapon == null || string.IsNullOrEmpty(weapon.Value) || weapon.Value == "hands") return true;
            _pending = ev; _data = data;
            if (!_lowering) { _at = Time.time; _lowering = true; }
            if (Log.Verbose) Log.Info("[Guns] Weapon transition: lower " + weapon.Value + " for " + ev.Name);
            return false;
        }
        private static void AfterActivate(ActivateGameObject action)
        {
            if (!ReferenceEquals(action.Fsm, _handlerFsm) || _handlerFsm == null) return;
            if (_handler == null || !ReferenceEquals(action.Fsm, _handler.Fsm) || !GunsModule.SmoothWeaponSwitch.Value || !action.activate.Value) return;
            if (action.Fsm.ActiveStateName == "hands") return;
            _at = Time.time; _weight = 1f; _lowering = false;
            if (Log.Verbose) Log.Info("[Guns] Weapon transition: raise " + action.Fsm.ActiveStateName);
        }
        private static void Tick()
        {
            Restore();
            if (_handler == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var camera = PlayerRig.Camera;
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
                if (Log.Verbose) Log.Info("[Guns] World ready: weapons holstered");
            }
            if (!GunsModule.SmoothWeaponSwitch.Value)
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
        private static void Pose()
        {
            if (!GunsModule.SmoothWeaponSwitch.Value || _handler == null || _weight <= .001f || !GameRefs.InPlay) return;
            var parent = PlayerRig.Parent;
            if (parent == null) return;
            _posed = parent; _position = parent.localPosition; _rotation = parent.localRotation;
            parent.localPosition += new Vector3(0f, -.45f, -.12f) * _weight;
            parent.localRotation *= Quaternion.Euler(25f * _weight, 0f, 0f);
        }
    }
}
