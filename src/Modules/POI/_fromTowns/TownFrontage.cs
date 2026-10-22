using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    // Immutable silhouette bands measured once per prefab/facing, shared by every lot and retry.
    internal sealed class TownFrontage
    {
        internal float Protrusion;
        internal Vector4 Envelope;
        internal readonly Dictionary<int, float> Min = new Dictionary<int, float>(), Max = new Dictionary<int, float>();
        internal static TownFrontage Make(BuildingSpec spec, int row, float tilt = 0)
        {
            int side = row == 0 ? -1 : 1;
            var rotation = Quaternion.Euler(0, (row == 0 ? 0 : 180) - Mathf.Atan2(spec.Outward.x, spec.Outward.z) * Mathf.Rad2Deg + tilt, 0);
            var result = new TownFrontage(); float padding = spec.Bake.Cell * .71f;
            result.Envelope = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int c = 0; c < 4; c++)
            {
                var p = rotation * (spec.Bake.Min + new Vector3((c & 1) == 0 ? 0 : spec.Bake.W * spec.Bake.Cell, 0,
                    (c & 2) == 0 ? 0 : spec.Bake.H * spec.Bake.Cell) - spec.Entry);
                result.Envelope.x = Mathf.Min(result.Envelope.x, p.x); result.Envelope.y = Mathf.Min(result.Envelope.y, p.z);
                result.Envelope.z = Mathf.Max(result.Envelope.z, p.x); result.Envelope.w = Mathf.Max(result.Envelope.w, p.z);
            }
            for (int i = 0; i < spec.Bake.Floor.Length; i++) if (spec.Bake.Protected(i))
            {
                var p = rotation * (spec.Bake.Point(i) - spec.Entry);
                result.Protrusion = Mathf.Max(result.Protrusion, -side * p.z + padding);
                // The extra band covers the fractional part of each instance's setback.
                int first = Mathf.FloorToInt(p.z - padding - .75f), last = Mathf.FloorToInt(p.z + padding + .75f) + 1;
                for (int z = first; z <= last; z++)
                {
                    float value;
                    if (!result.Min.TryGetValue(z, out value) || p.x - padding < value) result.Min[z] = p.x - padding;
                    if (!result.Max.TryGetValue(z, out value) || p.x + padding > value) result.Max[z] = p.x + padding;
                }
            }
            return result;
        }
    }
}
