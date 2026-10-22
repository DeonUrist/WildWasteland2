using System;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Nodes.ObjectsGenerators;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    internal static class TownLateTerrain
    {
        internal static void Apply(TileData data, TownPlan plan, TownAssets assets, StopToken stop)
        {
            if (Core.VistasWorldGen.Present) { ClearObjects(data, plan, assets, stop); return; }
            var heights = new MatrixWorld(data.heights);
            Flatten200.StampLevel(heights, plan.WorldCenter.y / data.globals.height,
                new Vector2D(plan.WorldCenter.x, plan.WorldCenter.z), plan.Radius + plan.Feather,
                plan.Radius / (plan.Radius + plan.Feather));
            if (plan.GradeAccess) GradeAccess(heights, plan, data.globals.height, stop);
            if (stop != null && stop.stop) return;
            // Re-export only the finished height buffer using the native interpolation/encoding.
            // The graph, its output products and Patrols' original elevation snapshot stay untouched.
            Matrix matrix = heights;
            int upscale = data.globals.heightInterpolation == HeightOutput200.Interpolation.Scale2X ? 2
                : data.globals.heightInterpolation == HeightOutput200.Interpolation.Scale4X ? 4 : 1;
            if (data.globals.heightInterpolation == HeightOutput200.Interpolation.Smooth)
            { matrix = new Matrix(heights); MatrixOps.GaussianBlur(matrix, .5f); }
            else if (upscale > 1)
            {
                int size = (heights.rect.size.x - 1) * upscale + 1;
                matrix = new Matrix(new CoordRect(heights.rect.offset, new Coord(size)));
                MatrixOps.Resize(heights, matrix);
            }
            matrix.Clamp01();
            int margin = data.area.Margins * upscale;
            var offset = matrix.rect.offset + margin;
            var set = data.ApplyOfType<HeightOutput200.ApplySetData>();
            var split = data.ApplyOfType<HeightOutput200.ApplySplitData>();
            var tex = data.ApplyOfType<HeightOutput200.ApplyTexData>();
            if (set != null) matrix.ExportHeights(set.heights2D, offset);
            else if (split != null)
            {
                int z = 0;
                foreach (var part in split.heights2DSplits)
                { matrix.ExportHeights(part, new Coord(offset.x, offset.z + z)); z += part.GetLength(0); }
            }
            else if (tex != null) matrix.ExportRawFloat(tex.texBytes, offset, new Coord(tex.res), .5f - 1f / 65535f);
            else throw new InvalidOperationException("Native height apply buffer missing.");
            data.heights = heights;
            ClearObjects(data, plan, assets, stop);
        }
        private static void ClearObjects(TileData data, TownPlan plan, TownAssets assets, StopToken stop)
        {
            var objects = data.ApplyOfType<ObjectsOutput.ApplyObjectsData>();
            if (objects == null) return;
            for (int p = 0; p < objects.prototypes.Length; p++)
            {
                bool preserve = false;
                foreach (var prefab in assets.NativePoiPrefabs)
                    if (ReferenceEquals(prefab, objects.prototypes[p].prefab)) { preserve = true; break; }
                if (preserve) continue;
                var points = objects.transitions[p];
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    if (stop != null && stop.stop) return;
                    var world = points[i].pos + (Vector3)data.area.active.worldPos;
                    var d = world - plan.WorldCenter; d.y = 0;
                    bool clear = d.sqrMagnitude <= (plan.Radius + plan.Feather) * (plan.Radius + plan.Feather);
                    for (int s = 1; !clear && s < plan.AccessPath.Length; s++)
                    {
                        var a = plan.World(plan.AccessPath[s - 1]); var b = plan.World(plan.AccessPath[s]);
                        a.y = b.y = world.y = 0; var delta = b - a;
                        float t = Mathf.Clamp01(Vector3.Dot(world - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                        clear = (world - a - delta * t).sqrMagnitude <= (plan.AccessWidth * .5f + 3) * (plan.AccessWidth * .5f + 3);
                    }
                    if (clear) points.RemoveAt(i);
                }
            }
        }
        private static void GradeAccess(MatrixWorld heights, TownPlan plan, float scale, StopToken stop)
        {
            var amount = new float[heights.arr.Length]; var target = new float[heights.arr.Length];
            float radius = plan.AccessWidth * .5f, feather = 4;
            float sx = heights.worldSize.x / (heights.rect.size.x - 1), sz = heights.worldSize.z / (heights.rect.size.z - 1);
            for (int s = 1; s < plan.AccessPath.Length; s++)
            {
                if (stop != null && stop.stop) return;
                var a = plan.World(plan.AccessPath[s - 1]); var b = plan.World(plan.AccessPath[s]);
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - feather - heights.worldPos.x) / sx), 0, heights.rect.size.x - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, b.z) - radius - feather - heights.worldPos.z) / sz), 0, heights.rect.size.z - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius + feather - heights.worldPos.x) / sx), 0, heights.rect.size.x - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, b.z) + radius + feather - heights.worldPos.z) / sz), 0, heights.rect.size.z - 1);
                var direction = b - a; direction.y = 0;
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    var p = heights.PixelToWorld(x + heights.rect.offset.x, z + heights.rect.offset.z); p.y = 0;
                    if (new Vector2(p.x - plan.WorldCenter.x, p.z - plan.WorldCenter.z).sqrMagnitude <= plan.Radius * plan.Radius) continue;
                    var delta = p - a; delta.y = 0;
                    float t = Mathf.Clamp01(Vector3.Dot(delta, direction) / Mathf.Max(.0001f, direction.sqrMagnitude));
                    float weight = Mathf.Clamp01((radius + feather - (delta - direction * t).magnitude) / feather);
                    weight = weight * weight * (3 - 2 * weight);
                    int i = z * heights.rect.size.x + x;
                    if (weight <= amount[i]) continue;
                    amount[i] = weight; target[i] = Mathf.Lerp(a.y, b.y, t) / scale;
                }
            }
            for (int i = 0; i < amount.Length; i++) if (amount[i] > 0) heights.arr[i] = Mathf.Lerp(heights.arr[i], target[i], amount[i]);
        }
    }
}
