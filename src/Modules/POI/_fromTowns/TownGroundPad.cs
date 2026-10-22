using System;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    // Applied before construction. Retain only owned samples so later Vistas grading cannot bury the buildings.
    internal sealed class TownGroundPad
    {
        private Terrain _terrain; private TerrainData _data;
        private int _x, _z, _resolution; private Vector3 _size;
        private float[,] _target;

        internal static TownGroundPad Prepare(TownSite site, Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Town foundation terrain is missing.");
            var data = terrain.terrainData; var size = data.size; var origin = terrain.transform.position;
            Vector3 center = site.Scene(Vector3.zero) - origin;
            float radius = site.Plan.Radius, feather = Mathf.Max(8f, site.Plan.Feather), outer = radius + feather;
            if (center.x - outer < 0 || center.z - outer < 0 || center.x + outer > size.x || center.z + outer > size.z)
                throw new InvalidOperationException("Planned town foundation crosses the terrain border.");
            if (center.y < 0 || center.y > size.y) throw new InvalidOperationException("Planned town foundation exceeds the terrain height range.");
            int res = data.heightmapResolution;
            float sx = size.x / (res - 1), sz = size.z / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - outer) / sx), 0, res - 1), z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - outer) / sz), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + outer) / sx), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + outer) / sz), 0, res - 1);
            var target = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float max = 0f;
            for (int z = 0; z < target.GetLength(0); z++) for (int x = 0; x < target.GetLength(1); x++)
            {
                float distance = new Vector2((x + x0) * sx - center.x, (z + z0) * sz - center.z).magnitude;
                if (distance >= outer) { target[z, x] = float.NaN; continue; }
                float weight = Mathf.Clamp01((outer - distance) / feather); weight = weight * weight * (3f - 2f * weight);
                float old = target[z, x]; target[z, x] = Mathf.Lerp(old, center.y / size.y, weight);
                max = Mathf.Max(max, Mathf.Abs(target[z, x] - old) * size.y);
            }
            var pad = new TownGroundPad { _terrain = terrain, _data = data, _size = size, _resolution = res, _x = x0, _z = z0, _target = target };
            pad.Apply(); site.Plan.Feather = feather; site.Plan.MaximumEarthwork = max;
            Core.Log.Always("[Towns] foundation prepared before buildings: " + site.Plan.Key + "; radius " + radius.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m");
            return pad;
        }

        internal void Apply()
        {
            if (_terrain == null || _terrain.terrainData != _data || _data.heightmapResolution != _resolution || _data.size != _size) return;
            var heights = _data.GetHeights(_x, _z, _target.GetLength(1), _target.GetLength(0)); bool changed = false;
            for (int z = 0; z < heights.GetLength(0); z++) for (int x = 0; x < heights.GetLength(1); x++)
            {
                float target = _target[z, x]; if (float.IsNaN(target) || Mathf.Abs(heights[z, x] - target) < .0000001f) continue;
                heights[z, x] = target; changed = true;
            }
            if (changed) _data.SetHeights(_x, _z, heights);
        }
    }
}
