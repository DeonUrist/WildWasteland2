using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class VantageDiscovery
    {
        private static float _scale = 1f;
        private static int _sample;
        private static Vector3 _origin;
        private static float _lowest, _height;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        internal static float Scale { get { return _scale; } }
        internal static void Reset() { _scale = 1f; _sample = 0; }
        internal static void OriginShifted(Vector3 delta) { _origin += delta; _height += delta.y; _lowest += delta.y; }
        internal static void Tick()
        {
            if (!GameRefs.InGame || SaveBus.Busy || GameRefs.Player == null) return;
            var player = GameRefs.Player.transform.position;
            if ((player - _origin).sqrMagnitude > 90000f) Reset();
            if (_sample == 0)
            { _origin = player; _lowest = player.y; _height = player.y; _sample = 0; }
            // One terrain-only probe per tick; a complete ring takes 1.8 seconds.
            float angle = _sample * Mathf.PI * .25f;
            var probe = _sample == 0 ? _origin : _origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1000f;
            RaycastHit hit;
            if (Physics.Raycast(probe + Vector3.up * 3000f, Vector3.down, out hit, 6000f, 1 << 14, QueryTriggerInteraction.Ignore))
            {
                if (_sample == 0) _height = hit.point.y;
                else _lowest = Mathf.Min(_lowest, hit.point.y);
            }
            if (++_sample <= 8) return;
            _sample = 0;
            float elevation = Mathf.Max(_height - _lowest, _origin.y - _height - 3f);
            float scale = 1f + Mathf.Clamp(elevation / 150f, 0f, 2f);
            if (Core.Log.Verbose && Mathf.Abs(scale - _scale) > .05f) Core.Log.Info("[CleanHUD] Vantage discovery: elevation " + elevation.ToString("0") + " m, range " + scale.ToString("0.00") + "x");
            _scale = scale;
        }
        internal static bool Sees(Transform root)
        {
            var camera = GameRefs.MainCamera;
            if (_scale <= 1.05f || root == null || camera == null || !GameRefs.InGame || SaveBus.Busy) return false;
            if (Services.Map != null && Services.Map.Open) return false;
            var origin = camera.transform.position; var delta = root.position + Vector3.up * 2f - origin;
            float distance = delta.magnitude;
            if (distance < .01f || distance > MapFog.RevealRadius) return false;
            var viewport = camera.WorldToViewportPoint(origin + delta);
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, Hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool clear = count < Hits.Length; var player = GameRefs.Player;
            for (int i = 0; i < count; i++)
            {
                var target = Hits[i].transform;
                if (target.IsChildOf(root) || target.IsChildOf(camera.transform) || player != null && target.IsChildOf(player.transform)) continue;
                clear = false; break;
            }
            System.Array.Clear(Hits, 0, count); return clear;
        }
    }
}
