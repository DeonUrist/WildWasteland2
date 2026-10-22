using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Towns
{
    internal sealed class TownOutline
    {
        internal struct Run { internal Vector3 A, B; }
        internal readonly Run[] Runs;
        internal readonly Vector3[] Vertices;
        internal TownOutline(TownPlan plan)
        {
            var frame = TownPerimeter.Frame(plan);
            var points = new List<Vector3>();
            var rotation = Quaternion.Euler(0, plan.Yaw, 0);
            foreach (var lot in plan.Lots) if (!lot.IsParking)
                for (int corner = 0; corner < 4; corner++)
                {
                    var point = rotation * (lot.LocalPosition + Quaternion.Euler(0, lot.Yaw, 0) * (lot.BoundsCenter
                        + new Vector3((corner % 2 == 0 ? -1 : 1) * (lot.HalfX + 7), 0, (corner < 2 ? -1 : 1) * (lot.HalfZ + 7))));
                    point.y = 0; points.Add(point);
                }
            // Keep the cardinal openings and exterior parking fixed. Everything between them wraps
            // the actual rotated building corners, instead of four sides of a bounding rectangle.
            for (int side = 0; side < 4; side++)
            {
                var normal = TownPerimeter.Normal(side); var gate = TownPerimeter.Gate(frame, side);
                var tangent = Vector3.Cross(Vector3.up, normal);
                points.Add(gate - tangent * TownPerimeter.GateHalf); points.Add(gate + tangent * TownPerimeter.GateHalf);
            }
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            var hull = new List<Vector3>();
            foreach (var p in points)
            { while (hull.Count > 1 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= .001f) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            { var p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= .001f) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            hull.RemoveAt(hull.Count - 1); Vertices = hull.ToArray();
            var runs = new List<Run>();
            for (int i = 0; i < Vertices.Length; i++)
            {
                var a = Vertices[i]; var b = Vertices[(i + 1) % Vertices.Length]; bool opening = false;
                for (int side = 0; side < 4; side++)
                { var gate = TownPerimeter.Gate(frame, side); if (Distance(gate, a, b) < .001f && (gate - a).sqrMagnitude <= 57 && (gate - b).sqrMagnitude <= 57) opening = true; }
                if (!opening) runs.Add(new Run { A = a, B = b });
            }
            Runs = runs.ToArray();
        }
        internal Vector3 Offset(int index, float distance)
        {
            var p = Vertices[index]; var before = (p - Vertices[(index + Vertices.Length - 1) % Vertices.Length]).normalized;
            var after = (Vertices[(index + 1) % Vertices.Length] - p).normalized;
            var normal = new Vector3(before.z, 0, -before.x); var next = new Vector3(after.z, 0, -after.x);
            var outward = (normal + next).normalized;
            return p + outward * (distance / Mathf.Max(.4f, Vector3.Dot(outward, normal)));
        }
        internal bool Contains(Vector3 point, float margin)
        {
            for (int i = 0; i < Vertices.Length; i++)
            { var a = Vertices[i]; var b = Vertices[(i + 1) % Vertices.Length]; if (Cross(b - a, point - a) < margin * Vector3.Distance(a, b)) return false; }
            return true;
        }
        internal bool Tower(TownPlan plan, int corner, List<Bounds> occupied, out Vector3 result)
        {
            var frame = TownPerimeter.Frame(plan); var direction = TownPerimeter.Corner(frame, corner, 0) - frame.center;
            direction.x /= frame.extents.x; direction.z /= frame.extents.z;
            float best = float.NegativeInfinity; result = frame.center;
            for (int pass = 0; pass < 2 && float.IsNegativeInfinity(best); pass++)
            for (int i = 0; i < Vertices.Length; i++) for (int kind = 0; kind < 4; kind++)
            {
                var a = Vertices[i]; var b = Vertices[(i + 1) % Vertices.Length]; var edge = (b - a).normalized;
                float inset = pass == 0 ? 3.2f : 2.2f;
                var point = kind == 0 ? Offset(i, -inset) : Vector3.Lerp(a, b, kind * .25f) + new Vector3(-edge.z, 0, edge.x) * inset;
                if (!Contains(point, 1.8f) || TownPerimeter.BuildingLine(plan, point, point, 3)) continue;
                bool gate = false; for (int side = 0; side < 4; side++) if ((point - TownPerimeter.Gate(frame, side)).sqrMagnitude < 225) gate = true;
                if (gate) continue;
                bool overlap = false; foreach (var bounds in occupied)
                    if (Mathf.Abs(point.x - bounds.center.x) < bounds.extents.x + 4 && Mathf.Abs(point.z - bounds.center.z) < bounds.extents.z + 4) overlap = true;
                if (overlap) continue;
                float score = Vector3.Dot(point - frame.center, direction);
                if (score > best) { best = score; result = point; }
            }
            return !float.IsNegativeInfinity(best);
        }
        internal bool Crosses(Vector3 a, Vector3 b, float radius)
        {
            foreach (var run in Runs)
            {
                if (Mathf.Max(a.x, b.x) < Mathf.Min(run.A.x, run.B.x) - radius || Mathf.Min(a.x, b.x) > Mathf.Max(run.A.x, run.B.x) + radius
                    || Mathf.Max(a.z, b.z) < Mathf.Min(run.A.z, run.B.z) - radius || Mathf.Min(a.z, b.z) > Mathf.Max(run.A.z, run.B.z) + radius) continue;
                var delta = b - a; var wall = run.B - run.A; float cross = Cross(delta, wall);
                if (Mathf.Abs(cross) > .0001f)
                {
                    float t = Cross(run.A - a, wall) / cross, u = Cross(run.A - a, delta) / cross;
                    if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return true;
                }
                float from = Distance(a, run.A, run.B), to = Distance(b, run.A, run.B);
                if (from < radius * radius && to > from) continue; // Permit escaping an already tight margin.
                if (Mathf.Min(Mathf.Min(from, to), Mathf.Min(Distance(run.A, a, b), Distance(run.B, a, b))) < radius * radius) return true;
            }
            return false;
        }
        private static float Cross(Vector3 a, Vector3 b) { return a.x * b.z - a.z * b.x; }
        internal static float Distance(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0; var delta = b - a;
            return (p - a - delta * Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude))).sqrMagnitude;
        }
    }
}
