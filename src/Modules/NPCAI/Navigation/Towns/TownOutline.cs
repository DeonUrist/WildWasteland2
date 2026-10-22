using UnityEngine;
namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Numeric polygon queries over the POI supplied town outline.</summary>
    internal sealed class TownOutline
    {
        internal struct Run { internal Vector3 A,B; }
        internal readonly Vector3[] Vertices; internal readonly Run[] Runs;
        internal TownOutline(Core.TownNavGeometry plan){Vertices=plan.Outline;Runs=new Run[plan.WallRuns.Length/2];for(int i=0;i<Runs.Length;i++)Runs[i]=new Run{A=plan.WallRuns[i*2],B=plan.WallRuns[i*2+1]};}
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
