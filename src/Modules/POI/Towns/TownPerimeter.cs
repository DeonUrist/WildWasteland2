using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    // World-cardinal numeric footprint; immutable building transforms remain the source of truth.
    internal static class TownPerimeter
    {
        internal const float GateHalf = 7.5f, Clearance = 10f;
        /// <summary>Returns the cached convex town outline with cardinal gate openings.</summary>
        internal static TownOutline Outline(TownPlan plan)
        { if (plan.Outline == null) plan.Outline = new TownOutline(plan); return plan.Outline; }
        /// <summary>Tests whether planned human ownership requires a perimeter.</summary>
        internal static bool Allegiance(TownPlan plan)
        { return plan.Owned && (plan.Faction == TownFaction.Coyotes || plan.Faction == TownFaction.Scrapyard); }
        /// <summary>Measures the combined footprint of the interior town lots.</summary>
        internal static Bounds Buildings(TownPlan plan)
        {
            var bounds = new Bounds(); bool any = false;
            foreach (var lot in plan.Lots) if (!lot.IsParking && !lot.External)
                for (int c = 0; c < 4; c++)
                {
                    var p = Quaternion.Euler(0, plan.Yaw, 0) * (lot.LocalPosition + Quaternion.Euler(0, lot.Yaw, 0)
                        * (lot.BoundsCenter + new Vector3((c & 1) == 0 ? -lot.HalfX : lot.HalfX, 0, (c & 2) == 0 ? -lot.HalfZ : lot.HalfZ)));
                    p.y = 0;
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            return bounds;
        }
        /// <summary>Computes the padded cardinal wall frame around interior geometry.</summary>
        internal static Bounds Frame(TownPlan plan)
        {
            var b = Buildings(plan); b.Expand(new Vector3(Clearance * 2, 0, Clearance * 2));
            var size = b.size; size.x = Mathf.Max(48, size.x); size.z = Mathf.Max(48, size.z); b.size = size; return b;
        }
        /// <summary>Returns the outward normal of a cardinal side.</summary>
        internal static Vector3 Normal(int side)
        { return side == 0 ? Vector3.right : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.back; }
        /// <summary>Returns the center of a cardinal opening on the wall frame.</summary>
        internal static Vector3 Gate(Bounds frame, int side)
        {
            var n = Normal(side);
            return frame.center + new Vector3(n.x * frame.extents.x, 0, n.z * frame.extents.z);
        }
        /// <summary>Returns a frame corner with the requested outward offset.</summary>
        internal static Vector3 Corner(Bounds b, int corner, float offset)
        { return b.center + new Vector3(((corner == 0 || corner == 3) ? 1 : -1) * (b.extents.x + offset), 0,
            (corner < 2 ? 1 : -1) * (b.extents.z + offset)); }
        /// <summary>Tests a segment against the oriented lot footprints.</summary>
        internal static bool BuildingLine(TownPlan plan, Vector3 a, Vector3 b, float margin)
        {
            var inverse = Quaternion.Euler(0, -plan.Yaw, 0);
            a = inverse * a; b = inverse * b;
            foreach (var lot in plan.Lots) if (!lot.IsParking && !lot.Skipped)
            {
                var rot = Quaternion.Euler(0, -lot.Yaw, 0);
                var from = rot * (a - lot.LocalPosition) - lot.BoundsCenter;
                var to = rot * (b - lot.LocalPosition) - lot.BoundsCenter;
                float lo = 0, hi = 1;
                if (Clip(from.x, to.x - from.x, lot.HalfX + margin, ref lo, ref hi)
                    && Clip(from.z, to.z - from.z, lot.HalfZ + margin, ref lo, ref hi)) return true;
            }
            return false;
        }
        /// <summary>Clips a segment parameter interval against one axis-aligned slab.</summary>
        internal static bool Clip(float p, float d, float extent, ref float lo, ref float hi)
        {
            if (Mathf.Abs(d) < .0001f) return Mathf.Abs(p) <= extent;
            float a = (-extent - p) / d, b = (extent - p) / d;
            lo = Mathf.Max(lo, Mathf.Min(a, b)); hi = Mathf.Min(hi, Mathf.Max(a, b)); return lo <= hi;
        }
        /// <summary>Appends cardinal gate and outline waypoint candidates.</summary>
        internal static void WalkNodes(TownPlan plan, List<Vector3> nodes, List<int> outside)
        {
            var frame = Frame(plan); var inv = Quaternion.Euler(0, -plan.Yaw, 0);
            for (int side = 0; side < 4; side++)
                foreach (float offset in new[] { -5f, 16f })
                { outside.Add(nodes.Count); nodes.Add(inv * (Gate(frame, side) + Normal(side) * offset)); }
            var outline = Outline(plan);
            for (int c = 0; c < outline.Vertices.Length; c++) foreach (float offset in new[] { -3.2f, 10f })
            { outside.Add(nodes.Count); nodes.Add(inv * outline.Offset(c, offset)); }
        }
    }
}
