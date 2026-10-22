using System;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    internal static class TownSurfacePaint
    {
        // Run only in OnTileFinalized, after every native generator/finalizer has finished.
        // Native terrain height/POI masks read texture outputs: feeding Towns back into that
        // texture generator makes a graph cycle and unbounded MapMagic recursion.
        internal static int Apply(TileData data, TownPlan plan, GenerationInput settings, TerrainLayer road, StopToken stop)
        {
            if (plan.PathsVersion == 0) return 0;
            var output = data.ApplyOfType<TexturesOutput200.ApplyData>();
            if (output == null || output.splats == null) throw new InvalidOperationException("Native terrain texture apply data is missing.");
            int channel = -1;
            for (int i = 0; i < output.prototypes.Length; i++) if (ReferenceEquals(output.prototypes[i], road)) { channel = i; break; }
            if (channel < 0) throw new InvalidOperationException("Native road layer absent from finalized terrain textures.");
            int h = output.splats.GetLength(0), w = output.splats.GetLength(1), layers = output.splats.GetLength(2);
            var coverage = new float[w * h];
            var area = data.area.active; float sx = area.worldSize.x / w, sz = area.worldSize.z / h;
            var ground = new TownGround(plan, settings.Buildings, null, 0, null);
            for (int e = 0; e < plan.StreetEdges.Length; e += 2)
                Stroke(coverage, w, h, area.worldPos.x, area.worldPos.z, sx, sz,
                    plan.World(plan.StreetNodes[plan.StreetEdges[e]]), plan.World(plan.StreetNodes[plan.StreetEdges[e + 1]]), plan.PathWidth, stop);
            for (int i = 1; i < plan.AccessPath.Length; i++)
            {
                var mid = (plan.AccessPath[i - 1] + plan.AccessPath[i]) * .5f;
                float width = new Vector2(mid.x, mid.z).magnitude < plan.Radius ? plan.PathWidth : plan.AccessWidth;
                Stroke(coverage, w, h, area.worldPos.x, area.worldPos.z, sx, sz, plan.World(plan.AccessPath[i - 1]), plan.World(plan.AccessPath[i]), width, stop);
            }
            int painted = 0;
            for (int z = 0; z < h; z++)
            {
                if (stop != null && stop.stop) return 0;
                for (int x = 0; x < w; x++)
                {
                    float amount = coverage[z * w + x], old = output.splats[z, x, channel];
                    if (amount <= old || ground.Protected(new Vector3(area.worldPos.x + (x + .5f) * sx, 0, area.worldPos.z + (z + .5f) * sz))) continue;
                    float keep = (1 - amount) / Mathf.Max(.00001f, 1 - old);
                    for (int l = 0; l < layers; l++) output.splats[z, x, l] = l == channel ? amount : output.splats[z, x, l] * keep;
                    painted++;
                }
            }
            return painted;
        }
        private static void Stroke(float[] mask, int w, int h, float originX, float originZ, float sx, float sz, Vector3 a, Vector3 b, float width, StopToken stop)
        {
            float radius = width * .5f, fringe = Mathf.Max(sx, sz) * .75f + .6f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - fringe - originX) / sx), 0, w - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, b.z) - radius - fringe - originZ) / sz), 0, h - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius + fringe - originX) / sx), 0, w - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, b.z) + radius + fringe - originZ) / sz), 0, h - 1);
            a.y = b.y = 0; var delta = b - a;
            for (int z = z0; z <= z1; z++)
            {
                if (stop != null && stop.stop) return;
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector3(originX + (x + .5f) * sx, 0, originZ + (z + .5f) * sz);
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                    float distance = (p - a - delta * t).magnitude;
                    float irregular = .12f * Mathf.Sin(p.x * .43f + p.z * .71f);
                    float value = Mathf.Clamp01((radius + fringe - distance + irregular) / Mathf.Max(.1f, fringe * 2));
                    value = value * value * (3 - 2 * value) * .82f;
                    int index = z * w + x; mask[index] = Mathf.Max(mask[index], value);
                }
            }
        }
    }
}
