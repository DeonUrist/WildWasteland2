using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Ironsights: optional toggle aiming and the wheel zoom target while aiming down sights (projection written through Core CameraArbiter).</summary>
    internal static partial class Ironsights
    {
        private static Camera _camera;
        private static PlayMakerFSM _ads;
        private static HutongGames.PlayMaker.Fsm _adsFsm;   // (WW perf) _ads.Fsm, refreshed once a frame in Tick: the GetButton hook compares by reference first
        private static float _nextFind, _target = 1f, _zoom = 1f;
        private static int _buttonFrame = -1;
        private static bool _buttonValue, _resetAim;
        private static readonly System.Collections.Generic.Dictionary<Transform, PlayMakerFSM> _weaponSights = new System.Collections.Generic.Dictionary<Transform, PlayMakerFSM>();

        /// <summary>Subscribes the ADS toggle (GetButton prefix), the per-frame job and the projection writer (Core CameraArbiter).</summary>
        internal static void Init(IModule owner)
        {
            Hooks.GetButtonDo.Prefix(owner, BeforeGetButton);
            Scheduler.EveryFrame(owner, "Gunplay.Ironsights", Tick, Phase.EarlyUpdate);
            _projectionOwner = owner; CameraArbiter.AddProjection(owner, "Gunplay.Ironsights", 100, WriteProjection);
        }
        /// <summary>Removes the projection writer and resets the state.</summary>
        internal static void Shutdown()
        {
            CameraArbiter.Remove(_projectionOwner, "Gunplay.Ironsights");
            Reset();
        }
        /// <summary>Drops rig references, zoom and toggle state (scene change).</summary>
        internal static void Reset()
        {
            Restore(); _camera = null; _ads = null; _adsFsm = null; _nextFind = 0f;
            _target = _zoom = 1f; _buttonFrame = -1;
            _buttonValue = _resetAim = false;
            _weaponSights.Clear();
        }
        /// <summary>World ready: toggle off, zoom 1, the native ADS FSM is reset once it runs.</summary>
        internal static void StartWorld()
        { _buttonValue = false; _buttonFrame = -1; _target = _zoom = 1f; _resetAim = true; }
        private static bool ThirdPerson { get { return Core.ModApi.Get<Core.IAnimationApi>() != null && Core.ModApi.Get<Core.IAnimationApi>().PlayerThirdPerson; } }
        /// <summary>Current zoom factor of the wheel zoom (1 = none, up to 3).</summary>
        internal static float ZoomFactor { get { return _zoom > 0.0001f ? 1f / _zoom : 1f; } }
        /// <summary>Aiming down sights with a drawn first-person weapon whose AimDownSights FSM is "on".</summary>
        internal static bool Aiming
        {
            get
            {
                if (_ads == null || !_ads.isActiveAndEnabled || _ads.ActiveStateName != "ads") return false;
                var parent = PlayerState.WeaponsParent;
                if (parent == null || !parent.gameObject.activeInHierarchy) return false;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var weapon = parent.GetChild(i);
                    if (!weapon.gameObject.activeInHierarchy) continue;
                    PlayMakerFSM sights;
                    if (!_weaponSights.TryGetValue(weapon, out sights))
                    {
                        sights = FsmUtil.Find(weapon.gameObject, "AimDownSights");
                        _weaponSights[weapon] = sights;
                    }
                    if (sights != null && sights.isActiveAndEnabled && sights.ActiveStateName == "on") return true;
                }
                return false;
            }
        }

        private static bool _lastAiming;
        private static float _lastTarget = 1f;

        /// <summary>Job "Gunplay.Ironsights" (EarlyUpdate): rig lookup, toggle state, wheel zoom; then the AdsChanged event when it changed.</summary>
        private static void Tick()
        {
            TickZoom();
            if (!ModEvents.HasListeners<Core.Events.AdsChanged>()) return;
            bool aiming = Aiming;
            if (aiming == _lastAiming && _target == _lastTarget) return;
            _lastAiming = aiming; _lastTarget = _target;
            WeaponInfo info;
            bool scoped = aiming && WeaponCatalog.Instance.TryGet(WeaponState.Instance.WeaponKey, out info) && info.Scoped;
            CombatEvents.Ads(aiming, 1f / Mathf.Max(0.0001f, _target), scoped);
        }

        /// <summary>The ironsight work of one frame (rig lookup, world-ready reset, toggle state, wheel zoom target and smoothing).</summary>
        private static void TickZoom()
        {
            if (_camera == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var holder = GameRefs.PlayerCameraHolder;
                var eye = holder != null ? holder.transform.Find("PlayerCamera") : null;
                if (eye != null)
                {
                    _camera = eye.GetComponent<Camera>();
                    _ads = FsmUtil.Find(eye.gameObject, "AimDownSIghts_Hold") ?? FsmUtil.Find(eye.gameObject, "AimDownSights_Hold");
                }
            }
            _adsFsm = _ads != null ? _ads.Fsm : null;
            if (_resetAim && _ads != null && _ads.Fsm.Initialized && GameRefs.InPlay)
            {
                _resetAim = false; _ads.SendEvent("Activate");
                if (Log.L2) GunplayLog.Deep("World ready: ironsights off");
            }
            if (!GameRefs.InPlay || !GunplaySettings.ToggleIronsights.Value || _ads == null || !_ads.enabled || _ads.ActiveStateName == "reloading") _buttonValue = false;
            if (!GunplaySettings.IronsightWheelZoom.Value) { _target = _zoom = 1f; return; }
            if (!GameRefs.InGame || ApocasetterBridge.InputBlocked || !Application.isFocused) return;
            if (!Aiming || ThirdPerson) { _target = _zoom = 1f; return; }
            else
            {
                float wheel = Core.ModApi.Get<Core.IMapMarkers>() != null && Core.ModApi.Get<Core.IMapMarkers>().Open ? 0f : Input.mouseScrollDelta.y;   // the map zooms with the wheel
                if (wheel != 0f)
                {
                    _target = Mathf.Clamp(_target * Mathf.Pow(.82f, wheel), 1f / 3f, 1f);
                    if (Log.L2) GunplayLog.Deep("Ironsight zoom target: " + (1f / _target).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x");
                }
            }
            _zoom = Mathf.Lerp(_zoom, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            if (Mathf.Abs(_zoom - _target) < .0001f) _zoom = _target;
        }
        /// <summary>Core.Hooks.GetButtonDo prefix (every GetButton each frame): with ToggleIronsights, the AimDownSights_Hold FSM's "Aim Down Sights" button becomes a toggle. First filter: reference compare with the cached ADS FSM.</summary>
        private static bool BeforeGetButton(GetButton action)
        {
            if (!ReferenceEquals(action.Fsm, _adsFsm) || _adsFsm == null) return true;
            if (_ads == null || !ReferenceEquals(action.Fsm, _ads.Fsm) || !GunplaySettings.ToggleIronsights.Value) return true;
            if (action.buttonName.Value != "Aim Down Sights") return true;
            if (ApocasetterBridge.InputBlocked || !GameRefs.InGame || !Application.isFocused)
            { action.storeResult.Value = _buttonValue; return false; }
            if (_buttonFrame != Time.frameCount)
            {
                _buttonFrame = Time.frameCount;
                if (Input.GetButtonDown(action.buttonName.Value)) _buttonValue = !_buttonValue;
            }
            action.storeResult.Value = _buttonValue;
            return false;
        }
    }
}
