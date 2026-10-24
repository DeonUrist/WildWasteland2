using System.Collections.Generic;
using Den.Tools.Matrices;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    internal static class TownVistaAccess
    {
        // Vista owns the connector surface and grading. Keep the route within one tile and outside the town's buildings.
        internal static bool Plan(TownPlan plan, MatrixWorld heights, float scale, List<Vector3> avoid, TownWorldRoads roads, float reach)
        {
            plan.AccessPath = new Vector3[0]; plan.GradeAccess = false;
            Vector3 road; if (!roads.Nearest(plan.WorldCenter, reach, out road)) return false;
            foreach (var lot in plan.Lots) if (lot.IsParking)
            {
                var start = plan.World(lot.LocalPosition); var heading = Quaternion.Euler(0, plan.Yaw + lot.Yaw, 0) * Vector3.forward;
                var radial = start - plan.WorldCenter; radial.y = 0;
                float radius = plan.Radius + 24, along = Vector3.Dot(radial, heading);
                var exit = start + heading * (-along + Mathf.Sqrt(Mathf.Max(0, along * along + radius * radius - radial.sqrMagnitude)));
                var end = road - plan.WorldCenter; end.y = 0;
                if (end.magnitude < radius + 12) return false;
                float from = Mathf.Atan2(exit.z - plan.WorldCenter.z, exit.x - plan.WorldCenter.x) * Mathf.Rad2Deg;
                float turn = Mathf.DeltaAngle(from, Mathf.Atan2(end.z, end.x) * Mathf.Rad2Deg);
                var waypoints = new List<Vector3> { start, exit };
                int arcs = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(turn) * Mathf.Deg2Rad * radius / 16));
                for (int i = 1; i <= arcs; i++)
                {
                    float angle = (from + turn * i / arcs) * Mathf.Deg2Rad;
                    waypoints.Add(plan.WorldCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                }
                waypoints.Add(road);
                var path = new List<Vector3>(); var inverse = Quaternion.Euler(0, -plan.Yaw, 0);
                for (int s = 1; s < waypoints.Count; s++)
                {
                    float length = Vector3.Distance(new Vector3(waypoints[s - 1].x, 0, waypoints[s - 1].z), new Vector3(waypoints[s].x, 0, waypoints[s].z));
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length / 8));
                    for (int i = s == 1 ? 0 : 1; i <= steps; i++)
                    {
                        var p = Vector3.Lerp(waypoints[s - 1], waypoints[s], i / (float)steps);
                        if (!roads.Inside(p)) return false;
                        foreach (var poi in avoid) if (new Vector2(p.x - poi.x, p.z - poi.z).sqrMagnitude < 80 * 80) return false;
                        p.y = heights.GetWorldInterpolatedValue(p.x, p.z) * scale;
                        if (new Vector2(p.x - plan.WorldCenter.x, p.z - plan.WorldCenter.z).sqrMagnitude < plan.Radius * plan.Radius) p.y = plan.WorldCenter.y;
                        if (path.Count > 0)
                        {
                            var previous = plan.World(path[path.Count - 1]); float distance = new Vector2(p.x - previous.x, p.z - previous.z).magnitude;
                            if (Mathf.Abs(p.y - previous.y) > Mathf.Max(1, distance * .22f)) return false;
                        }
                        path.Add(inverse * (p - plan.WorldCenter));
                    }
                }
                plan.AccessPath = path.ToArray(); plan.AccessWidth = 9; return true;
            }
            return false;
        }
    }
}
