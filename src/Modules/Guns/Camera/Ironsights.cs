using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class Ironsights
    {
        private static Camera _camera, _renderCamera;
        private static PlayMakerFSM _ads;
        private static HutongGames.PlayMaker.Fsm _adsFsm;   // (WW perf) _ads.Fsm, refreshed once a frame in Tick: the GetButton hook compares by reference first
        private static float _nextFind, _target = 1f, _zoom = 1f;
        private static int _buttonFrame = -1;
        private static bool _buttonValue, _transparentProjection, _resetAim;
        private static readonly System.Collections.Generic.Dictionary<Transform, PlayMakerFSM> _weaponSights = new System.Collections.Generic.Dictionary<Transform, PlayMakerFSM>();

        internal static void Init(GunsModule owner)
        {
            Hooks.GetButtonDo.Prefix(owner, BeforeGetButton);
            Scheduler.EveryFrame(owner, "Ironsights", Tick, Phase.EarlyUpdate);
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }
        internal static void Shutdown()
        {
            Camera.onPreCull -= PreCull; Camera.onPostRender -= PostRender;
            Reset();
        }
        internal static void Reset()
        {
            Restore(); _camera = null; _ads = null; _adsFsm = null; _nextFind = 0f;
            _target = _zoom = 1f; _buttonFrame = -1;
            _buttonValue = _resetAim = false;
            _weaponSights.Clear();
        }
        internal static void StartWorld()
        { _buttonValue = false; _buttonFrame = -1; _target = _zoom = 1f; _resetAim = true; }
        private static bool ThirdPerson { get { return Services.Animation != null && Services.Animation.PlayerThirdPerson; } }
        private static bool Aiming
        {
            get
            {
                if (_ads == null || !_ads.isActiveAndEnabled || _ads.ActiveStateName != "ads") return false;
                var parent = PlayerRig.Parent;
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

        private static void Tick()
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
                if (Log.Verbose) Log.Info("[Guns] World ready: ironsights off");
            }
            if (!GameRefs.InPlay || !GunsModule.ToggleIronsights.Value || _ads == null || !_ads.enabled || _ads.ActiveStateName == "reloading") _buttonValue = false;
            if (!GunsModule.IronsightWheelZoom.Value) { _target = _zoom = 1f; return; }
            if (!GameRefs.InGame || ApocasetterBridge.InputBlocked || !Application.isFocused) return;
            if (!Aiming || ThirdPerson) { _target = _zoom = 1f; return; }
            else
            {
                float wheel = Services.Map != null && Services.Map.Open ? 0f : Input.mouseScrollDelta.y;   // the map zooms with the wheel
                if (wheel != 0f)
                {
                    _target = Mathf.Clamp(_target * Mathf.Pow(.82f, wheel), 1f / 3f, 1f);
                    Log.Info("[Guns] Ironsight zoom target: " + (1f / _target).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x");
                }
            }
            _zoom = Mathf.Lerp(_zoom, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            if (Mathf.Abs(_zoom - _target) < .0001f) _zoom = _target;
        }
        private static bool BeforeGetButton(GetButton action)
        {
            if (!ReferenceEquals(action.Fsm, _adsFsm) || _adsFsm == null) return true;
            if (_ads == null || !ReferenceEquals(action.Fsm, _ads.Fsm) || !GunsModule.ToggleIronsights.Value) return true;
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
        private static void PreCull(Camera cam)
        {
            if (cam != _camera || !GunsModule.IronsightWheelZoom.Value || ThirdPerson || !GameRefs.InPlay || !Aiming || Mathf.Abs(_zoom - 1f) < .0001f) return;
            Restore();
            _renderCamera = cam; _transparentProjection = cam.useJitteredProjectionMatrixForTransparentRendering;
            var projection = cam.projectionMatrix;
            projection.m00 /= _zoom; projection.m11 /= _zoom;
            cam.projectionMatrix = projection;
            cam.useJitteredProjectionMatrixForTransparentRendering = true;
            cam.ResetCullingMatrix();
        }
        private static void PostRender(Camera cam) { if (cam == _renderCamera) Restore(); }
        private static void Restore()
        {
            if (_renderCamera == null) return;
            _renderCamera.useJitteredProjectionMatrixForTransparentRendering = _transparentProjection;
            _renderCamera.ResetProjectionMatrix(); _renderCamera.ResetCullingMatrix(); _renderCamera = null;
        }
    }
}
