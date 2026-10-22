using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    internal static class ClimbPath
    {
        /// <summary>Smoothstep on 0..1.</summary>
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        // Lift before crossing the lip so the actual capsule never enters the wall.
        /// <summary>Body position along the climb path at a phase (lift before crossing the lip so the capsule never enters the wall).</summary>
        public static Vector3 At(ClimbTarget target, float phase, bool high)
        {
            float split = high ? .72f : .6f;
            if (phase < split) return Vector3.Lerp(target.Start, target.Lift, Smooth(phase / split));
            return Vector3.Lerp(target.Lift, target.End, Smooth((phase - split) / (1 - split)));
        }
    }
}
