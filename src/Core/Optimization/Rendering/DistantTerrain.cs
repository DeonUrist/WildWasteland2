using BepInEx.Configuration;
using Den.Tools;
using MapMagic.Core;
using UnityEngine;

namespace WildWasteland.Core.Optimization
{
    internal static class DistantTerrain
    {
        internal static ConfigEntry<bool> Entry;
        static MapMagicObject _mm;
        static int _range;
        static bool _applied;
        internal static void Scene() { _mm = Object.FindObjectOfType<MapMagicObject>(); _applied = false; }
        internal static void Tick()
        {
            if (!Entry.Value) { Restore(); return; }
            if (_mm == null || !_mm.isActiveAndEnabled || !_mm.draftsInPlaymode || Camera.main == null || _applied) return;
            _range = _mm.tiles.generateRange; _applied = true;
            _mm.tiles.generateRange = Mathf.Max(_range, Mathf.CeilToInt(20000f / Mathf.Min(_mm.tileSize.x, _mm.tileSize.z)));
            Refresh();
            Log.Always(null, "Distant terrain: ON, 20 km camera, draft range " + _mm.tiles.generateRange + ", main range " + _mm.mainRange);
        }
        internal static void Restore()
        {
            if (!_applied) return;
            if (_mm != null) { _mm.tiles.generateRange = _range; Refresh(); }
            _applied = false; Log.Always(null, "Distant terrain: OFF, native draft range restored");
        }
        static void Refresh()
        {
            if (_mm == null || !_mm.isActiveAndEnabled || Camera.main == null) return;
            var p = _mm.transform.InverseTransformPoint(Camera.main.transform.position);
            var coords = new[] { Coord.Floor(p.x / _mm.tileSize.x, p.z / _mm.tileSize.z) };
            _mm.tiles.Deploy(coords, _mm.tiles.pinned, _mm); _mm.tiles.ChangeDists(coords);
        }
    }
}
