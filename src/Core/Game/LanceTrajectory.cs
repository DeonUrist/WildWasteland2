using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Shared numeric launch and leap prediction; callers own attack decisions and actual projectiles.</summary>
    internal static class LanceTrajectory
    {
        // Intercept with gravity: the launch velocity (relative to the thrower's own velocity baseVel) of the given speed that meets a target
        // moving at targetVel. The flattest (earliest) solution. False when the speed cannot reach it - launch is then the best try.
        /// <summary>Launch velocity of a given speed (relative to the thrower's velocity) that meets a moving target under gravity; flattest solution, false = best try.</summary>
        internal static bool Solve(Vector3 from, Vector3 baseVel, Vector3 target, Vector3 targetVel, float speed, out Vector3 launch, out float time)
        {
            var g = Physics.gravity;
            float prevT = 0f, bestMag = float.MaxValue, bestT = 1f;
            for (float t = 0.04f; t <= 4f; t += 0.04f)
            {
                float mag = U(from, baseVel, target, targetVel, g, t).magnitude;
                if (mag < bestMag) { bestMag = mag; bestT = t; }
                if (mag <= speed)
                {
                    float a = prevT > 0f ? prevT : t * 0.5f, b = t;
                    for (int i = 0; i < 14; i++) { float m = (a + b) * 0.5f; if (U(from, baseVel, target, targetVel, g, m).magnitude <= speed) b = m; else a = m; }
                    launch = U(from, baseVel, target, targetVel, g, b); time = b;
                    return true;
                }
                prevT = t;
            }
            var u = U(from, baseVel, target, targetVel, g, bestT);
            launch = u.normalized * speed; time = bestT;
            return false;
        }
        /// <summary>Launch velocity that meets the target after time t.</summary>
        private static Vector3 U(Vector3 from, Vector3 baseVel, Vector3 target, Vector3 targetVel, Vector3 g, float t)
        {
            return (target + targetVel * t - from - baseVel * t - 0.5f * g * t * t) / t;
        }
        // 2.1.4 "Witness me!" leap: a long, flat jump - the ground speed relative to the thrower's car is fixed (hSpeed), the flight time is
        // when that ground track meets the target's predicted ground track, and the vertical part just has to land on it (a low arc: no lob).
        // false when the target runs away faster than that, the jump would take over 1.6 s, or it would need more than 35 degrees up.
        internal const float MaxLeapTime = 1.2f;   // 2.2.3: the longest "Witness me!" jump (was 1.6 s at 16 m/s - Denis: shorter)
        /// <summary>The "Witness me!" leap: flat jump of fixed ground speed meeting the target track; false when too long or too steep.</summary>
        internal static bool FlatLeap(Vector3 from, Vector3 baseVel, Vector3 target, Vector3 targetVel, float hSpeed, out Vector3 launch, out float time)
        {
            var r = target - from; r.y = 0f;
            var w = targetVel - baseVel; w.y = 0f;
            float a = Vector3.Dot(w, w) - hSpeed * hSpeed, b = 2f * Vector3.Dot(r, w), c = Vector3.Dot(r, r);
            time = -1f;
            if (Mathf.Abs(a) < 1e-4f) { if (b < 0f) time = -c / b; }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sq = Mathf.Sqrt(disc), t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
                    float lo = Mathf.Min(t1, t2), hi = Mathf.Max(t1, t2);
                    time = lo > 0.05f ? lo : hi > 0.05f ? hi : -1f;
                }
            }
            if (time <= 0f) { launch = Vector3.zero; return false; }
            time = Mathf.Max(time, 0.25f);
            launch = U(from, baseVel, target, targetVel, Physics.gravity, time);
            return time <= MaxLeapTime && Elevation(launch) <= 35f;
        }

        /// <summary>Elevation angle of a vector, degrees.</summary>
        internal static float Elevation(Vector3 v) { return Mathf.Asin(Mathf.Clamp(v.normalized.y, -1f, 1f)) * Mathf.Rad2Deg; }
    }
}
