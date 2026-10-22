using WildWasteland.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    internal static partial class Binoculars
    {
        private static Camera _camera;
        private static Transform _overlay, _scope;
        private static float _nextFind, _scopeSlope, _zoom = 1f;
        private static GlyphText _caption;
        private static Transform _subject;
        private static string _name;
        private static Color _color;
        private static float _score, _roundUntil, _seenUntil;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        internal static bool Raised { get { return _overlay != null && _overlay.gameObject.activeInHierarchy; } }
        internal static bool Scoped { get { return !Raised && _scope != null && _scope.gameObject.activeInHierarchy; } }
        /// <summary>Returns viewing for the binocular discovery.</summary>
        internal static bool Viewing { get { return Raised || Scoped; } }
        /// <summary>Returns range scale for the binocular discovery.</summary>
        internal static float RangeScale { get { return Raised ? 2f : Scoped ? 1.5f : 1f; } }
        /// <summary>Resolves tan for the binocular discovery.</summary>
        private static float Slope { get { return Raised ? Mathf.Tan(10f * Mathf.Deg2Rad) / _zoom : _scopeSlope; } }
        internal static Vector3 Forward { get { return _camera != null ? _camera.transform.forward : Vector3.zero; } }
        internal static float HalfAngle { get { return _camera != null ? Mathf.Atan(Slope * _camera.aspect) : 0f; } }

        /// <summary>Initializes the binocular discovery.</summary>
        internal static void Init(HudModule owner)
        {
            Reset();
            _projectionOwner = owner; CameraArbiter.AddProjection(owner, "HUD.Binoculars", 300, WriteProjection);
            Scheduler.EveryFrame(owner, "Binocular zoom", Tick, Phase.LateUpdateLast);
        }
        /// <summary>Clears transient binocular discovery state so the next scene or session starts cleanly.</summary>
        internal static void Reset()
        {
            if (_caption != null) UnityEngine.Object.Destroy(_caption.gameObject);
            _caption = null; _subject = null; _name = null; _roundUntil = _seenUntil = 0f;
            Restore(); _camera = null; _overlay = _scope = null; _nextFind = 0f;
            _scopeSlope = Mathf.Tan(12.5f * Mathf.Deg2Rad);
            _zoom = HudModule.BinocularZoom.Value;
            System.Array.Clear(Hits, 0, Hits.Length);
        }
        /// <summary>Restores native behavior and releases owned binocular discovery resources.</summary>
        internal static void Shutdown()
        {
            CameraArbiter.Remove(_projectionOwner, "HUD.Binoculars"); Reset();
        }
        /// <summary>Updates the binocular discovery.</summary>
        private static void Tick()
        {
            if ((_camera == null || _overlay == null || _scope == null) && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var eye = SceneCache.FindPath("PlayerCameraHolder/PlayerCamera");
                _camera = eye != null ? eye.GetComponent<Camera>() : null;
                _overlay = SceneCache.FindPath("Canvas/BinocularUI");
                _scope = SceneCache.FindPath("Canvas/redmark_m11_ScopeUI");
            }
            Caption();
            if (!Raised) { _zoom = HudModule.BinocularZoom.Value; return; }
            if (!GameRefs.InGame || ApocasetterBridge.InputBlocked || !Application.isFocused) return;
            float wheel = Services.Map != null && Services.Map.Open ? 0f : Input.mouseScrollDelta.y;
            if (wheel != 0f)
            {
                HudModule.BinocularZoom.Value = Mathf.Clamp(HudModule.BinocularZoom.Value * Mathf.Pow(1f / .82f, wheel), 1f, 2f);
                if (Core.Log.Verbose) Core.Log.Info("[CleanHUD] Binocular zoom target: " + HudModule.BinocularZoom.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x");
            }
            _zoom = Mathf.Lerp(_zoom, HudModule.BinocularZoom.Value, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            if (Mathf.Abs(_zoom - HudModule.BinocularZoom.Value) < .0001f) _zoom = HudModule.BinocularZoom.Value;
        }
        /// <summary>Observes the binocular discovery.</summary>
        internal static bool Observe(Transform target, string name, Color color)
        {
            if (!GameRefs.InGame || Services.Map != null && Services.Map.Open || target == null || !Sees(target)) return false;
            if (!HudModule.CleanHudEnabled.Value) return true;
            float now = Time.unscaledTime;
            if (now >= _roundUntil) { _roundUntil = now + .25f; _score = float.MaxValue; }
            var local = _camera.transform.InverseTransformPoint(target.position + Vector3.up * 2f);
            float score = (local.x * local.x + local.y * local.y) / (local.z * local.z);
            if (score <= _score)
            {
                _score = score; _subject = target; _name = name; _color = color; _seenUntil = now + .65f;
            }
            return true;
        }
        /// <summary>Resolves caption for the binocular discovery.</summary>
        private static void Caption()
        {
            bool shown = Raised && HudModule.CleanHudEnabled.Value && GameRefs.InGame && _subject != null
                && Time.unscaledTime < _seenUntil && (Services.Map == null || !Services.Map.Open);
            if (shown)
            {
                var local = _camera.transform.InverseTransformPoint(_subject.position + Vector3.up * 2f);
                float slope = Slope;
                shown = local.z > 0f && Mathf.Abs(local.x) <= local.z * slope * _camera.aspect && Mathf.Abs(local.y) <= local.z * slope;
            }
            if (shown && _caption == null)
            {
                var rect = DialogueWidgets.Rect(_overlay.parent, "CleanHUD.BinocularCaption", 0, 0, 1000, 44);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .12f); rect.pivot = new Vector2(.5f, .5f);
                rect.SetAsLastSibling();
                _caption = rect.gameObject.AddComponent<GlyphText>(); _caption.Size = 30f; _caption.Align = TextAnchor.MiddleCenter; _caption.raycastTarget = false;
                var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(1, -2);
            }
            if (_caption == null) return;
            if (_caption.gameObject.activeSelf != shown) _caption.gameObject.SetActive(shown);
            if (shown)
            {
                if (_caption.Text != _name && Core.Log.Verbose) Core.Log.Info("[CleanHUD] Optic caption: " + _name + " (binoculars, range 2x)");
                _caption.Set(_name); if (_caption.color != _color) _caption.color = _color;
            }
        }
        /// <summary>Tests the binocular discovery.</summary>
        internal static bool Sees(Transform root)
        {
            if (!Raised || _camera == null) return false;
            var origin = _camera.transform.position; var target = root.position + Vector3.up * 2f; var delta = target - origin;
            float distance = delta.magnitude;
            if (distance <= .01f || distance > MapFog.RevealRadius * RangeScale) return false;
            var local = _camera.transform.InverseTransformDirection(delta / distance);
            float slope = Slope;
            if (local.z <= 0f || Mathf.Abs(local.x) > local.z * slope * _camera.aspect || Mathf.Abs(local.y) > local.z * slope) return false;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, Hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == Hits.Length) return false;
            var player = GameRefs.Player;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i].transform;
                if (hit.IsChildOf(root) || hit.IsChildOf(_camera.transform) || player != null && hit.IsChildOf(player.transform)) continue;
                return false;
            }
            return true;
        }
    }
}
