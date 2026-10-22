using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Numeric gate and clipping helpers for detached town navigation geometry.</summary>
    internal static class TownPerimeter
    {
        internal static TownOutline Outline(TownNavGeometry p)=>new TownOutline(p);
        internal static Bounds Frame(TownNavGeometry p)=>p.Frame;
        internal static Vector3 Normal(int side)
        { return side == 0 ? Vector3.right : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.back; }
        internal static Vector3 Gate(Bounds frame, int side)
        {
            var n = Normal(side);
            return frame.center + new Vector3(n.x * frame.extents.x, 0, n.z * frame.extents.z);
        }
        internal static Vector3 Corner(Bounds b, int corner, float offset)
        { return b.center + new Vector3(((corner == 0 || corner == 3) ? 1 : -1) * (b.extents.x + offset), 0,
            (corner < 2 ? 1 : -1) * (b.extents.z + offset)); }
        internal static bool Clip(float p, float d, float extent, ref float lo, ref float hi)
        {
            if (Mathf.Abs(d) < .0001f) return Mathf.Abs(p) <= extent;
            float a = (-extent - p) / d, b = (extent - p) / d;
            lo = Mathf.Max(lo, Mathf.Min(a, b)); hi = Mathf.Min(hi, Mathf.Max(a, b)); return lo <= hi;
        }
    }
}
