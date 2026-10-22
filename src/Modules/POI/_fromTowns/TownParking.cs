using UnityEngine;
using Den.Tools.Matrices;
using System.Collections.Generic;

namespace WildWasteland.Modules.Towns
{
    internal static class TownParking
    {
        internal static void Outside(TownPlan plan)
        {
            var frame = TownPerimeter.Frame(plan);
            var inverse = Quaternion.Euler(0, -plan.Yaw, 0);
            foreach (var lot in plan.Lots) if (lot.IsParking)
            {
                // Its near edge is six metres beyond the wall. Longitudinal bays face outward east.
                var center = new Vector3(frame.max.x + 26, 0, frame.center.z);
                lot.LocalPosition = inverse * center; lot.Yaw = 90 - plan.Yaw;
                plan.Radius = Mathf.Max(plan.Radius, center.magnitude + 24);
                // Reserve a 40 m station footprint beside the apron (the authored shell is 32 x 34 m).
                plan.Radius = Mathf.Max(plan.Radius, new Vector2(Mathf.Abs(center.x) + 20, Mathf.Abs(center.z + lot.HalfX + 22) + 20).magnitude + 3);
            }
            for (int c = 0; c < 4; c++) plan.Radius = Mathf.Max(plan.Radius, TownPerimeter.Corner(frame, c, 20).magnitude + 3);
        }
        internal static void Access(TownPlan plan, MatrixWorld heights, float scale, List<Vector3> avoid)
        {
            if (Core.VistasWorldGen.Present) { plan.AccessPath = new Vector3[0]; plan.GradeAccess = false; return; }
            foreach (var lot in plan.Lots) if (lot.IsParking)
            {
                var stop = new Patrols.TownStop { Parking = plan.World(lot.LocalPosition), Center = plan.WorldCenter, Radius = plan.Radius,
                    Heading = Quaternion.Euler(0, plan.Yaw + lot.Yaw, 0) * Vector3.forward };
                var end = stop.Approach; var start = stop.Parking;
                // A single unpainted ground approach, outside the buildings. No road/path meshes.
                // Keep the join on the real heightfield and avoid native POI footprints.
                if (end.x < heights.worldPos.x || end.z < heights.worldPos.z || end.x > heights.worldPos.x + heights.worldSize.x || end.z > heights.worldPos.z + heights.worldSize.z) return;
                foreach (var p in avoid)
                {
                    var d = end - start; d.y = 0; var delta = p - start; delta.y = 0;
                    if ((delta - d * Mathf.Clamp01(Vector3.Dot(delta, d) / Mathf.Max(1, d.sqrMagnitude))).sqrMagnitude < 80 * 80) return;
                }
                end.y = heights.GetWorldInterpolatedValue(end.x, end.z) * scale;
                var radial = start - plan.WorldCenter; radial.y = 0;
                float along = Vector3.Dot(radial, stop.Heading);
                float edge = -along + Mathf.Sqrt(Mathf.Max(0, along * along + plan.Radius * plan.Radius - radial.sqrMagnitude));
                start += stop.Heading * edge;
                float length = new Vector2(end.x - start.x, end.z - start.z).magnitude;
                if (Mathf.Abs(end.y - start.y) > length * .22f) return;
                var inverse = Quaternion.Euler(0, -plan.Yaw, 0);
                plan.AccessPath = new[] { inverse * (start - plan.WorldCenter), inverse * (end - plan.WorldCenter) };
                plan.AccessWidth = 9; plan.GradeAccess = true;
                return;
            }
        }
        internal static readonly BuildingSpec Spec = Make();
        private static BuildingSpec Make()
        {
            var spec = new BuildingSpec { Name = "Towns_Parking", HalfX = 12, HalfZ = 20,
                Entry = new Vector3(0, 0, -20), Outward = Vector3.back, Frontages = new TownFrontage[2] };
            for (int row = 0; row < 2; row++)
            {
                int side = row == 0 ? -1 : 1;
                var f = new TownFrontage { Envelope = new Vector4(-12, row == 0 ? -40 : 0, 12, row == 0 ? 0 : 40) };
                for (int z = -2; z <= 42; z++) { f.Min[z * side] = -12; f.Max[z * side] = 12; }
                spec.Frontages[row] = f;
            }
            return spec;
        }
    }
}
