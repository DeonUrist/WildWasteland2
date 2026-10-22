using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Idle part: the body - walk speed, stop and the per-frame drive through the Mover.</summary>
    internal static partial class Idle
    {
        // ---------- the body ----------
        /// <summary>Starts / changes the walk speed (run clip at a matching speed).</summary>
        private static void Go(Ctl c, float speed)
        {
            c.Speed = speed;
            if (c.Anim != null)
            {
                if (!c.Moving) c.Anim.Play("run", 0, UnityEngine.Random.value);
                c.Anim.speed = speed / 5f;
            }
            if (!c.Moving) { c.NextFan = 0f; c.NextRunCheck = Time.time + 0.25f * UnityEngine.Random.value; }   // a new walk: the feelers at once, the clip check on its own phase
            c.Moving = true;
        }

        /// <summary>Stops the walk (idle clip).</summary>
        private static void Stop(Ctl c) { Stop(c, true); }
        /// <summary>Stops the walk: POI guiding off, idle clip (optional), planar velocity zeroed, no leg.</summary>
        private static void Stop(Ctl c, bool playIdle)
        {
            if (c.Moving)
            {
                PoiNav.StopGuiding(c.A.Owner);
                if (c.Anim != null) { c.Anim.speed = 1f; if (playIdle) c.Anim.Play("idle", 0, 0f); }
                if (c.Rb != null) Mover.Halt(c.Rb);
            }
            c.Moving = false; c.Leg = Leg.None; c.Turning = false;
        }

        private static readonly float[] Fan = { 0f, -35f, 35f, -70f, 70f };
        /// <summary>(WW perf, D1) the run-clip check every 0.25 s (state hash, no string), the feeler fan (1-5 capsule casts) at 10 Hz per walker or at once when the wanted heading turns &gt; 15 deg; turning and velocity stay per frame. Worst case: the fan's answer is 0.1 s old (0.25-0.5 m of walking against a 1.2 m feeler), a run clip knocked off by something else comes back within 0.25 s.</summary>
        private static void Drive(Ctl c, float dt, float now)
        {
            if (c.Rb == null) { Stop(c); return; }
            if (PoiNavTraverse.Busy(c.A.Owner)) return;   // (WW) on a POI ladder / climb: the traversal moves the body
            var t = c.A.T;
            if (c.Anim != null && now >= c.NextRunCheck)
            {
                c.NextRunCheck = now + 0.25f;
                if (!c.Anim.IsInTransition(0) && c.Anim.GetCurrentAnimatorStateInfo(0).shortNameHash != RunHash) { c.Anim.Play("run", 0, 0f); c.Anim.speed = c.Speed / 5f; }
            }
            Vector3 to = c.Steer - t.position; to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            // three-way body feelers (only off a map route): the first free heading nearest the wanted one
            if (PoiNav.Guiding(c.A.Owner)) c.FanOff = 0f;                // (WW) a POI route: straight at its waypoint, no body feelers
            else if (now >= c.NextFan || Mathf.Abs(Mathf.DeltaAngle(want, c.FanWant)) > 15f)
            {
                c.NextFan = now + 0.08f + 0.04f * UnityEngine.Random.value; c.FanWant = want; c.FanOff = 0f;
                Vector3 low = t.position + Vector3.up * 0.6f, high = t.position + Vector3.up * 1.3f;
                for (int i = 0; i < Fan.Length; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, want + Fan[i], 0f) * Vector3.forward;
                    if (!Physics.CapsuleCast(low, high, 0.3f, d, 1.2f, FeelMask, QueryTriggerInteraction.Ignore)) { c.FanOff = Fan[i]; break; }
                }
            }
            float chosen = want + c.FanOff;
            float yaw = Mover.TurnToward(t, chosen, dt);
            Vector3 v = t.forward * (c.Speed * Mover.SpeedFactor(Mathf.Abs(Mathf.DeltaAngle(yaw, chosen))));   // slows down while turning: no arcs
            Vector3 poiDir; Mover.PoiDirection(c.A.Owner, t.position, out poiDir);
            if (Mover.CarryOnPoiRoute(c.A.Owner, c.Speed, poiDir)) return;          // (WW) a POI route: carried along it (no body physics), the turn only shown
            Mover.Push(c.Rb, v);
        }
        private const int FeelMask = (1 << 0) | (1 << 8) | (1 << 11);     // Default (walls, rock, props), Car, Door - not Ground (slopes)

        /// <summary>Horizontal length of a vector.</summary>
        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        /// <summary>Level-2 line about one controller (callers check Log.L2 before building the text).</summary>
        private static void Log(Ctl c, string msg)
        {
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Idle: " + (c.A.Owner != null ? c.A.Owner.name : "?") + " " + msg);
        }
    }
}
