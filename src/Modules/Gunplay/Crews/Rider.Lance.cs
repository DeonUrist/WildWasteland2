using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Blast-lance ballistics and blasts (intercept solvers, flat leap, lance explosion prefab, Boom/Kaboom); Gunplay part of Rider (Wave A split of Crews/Rider.cs).</summary>
    internal static partial class Rider
    {
        // Intercept with gravity: the launch velocity (relative to the thrower's own velocity baseVel) of the given speed that meets a target
        // moving at targetVel. The flattest (earliest) solution. False when the speed cannot reach it - launch is then the best try.
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
        private static Vector3 U(Vector3 from, Vector3 baseVel, Vector3 target, Vector3 targetVel, Vector3 g, float t)
        {
            return (target + targetVel * t - from - baseVel * t - 0.5f * g * t * t) / t;
        }
        // The blast lance's own explosion: what the projectile's [Explosion] FSM creates in its "explode" state (CreateObject), read once from the
        // prefab's FSM. Spawned directly, it always goes off - no trigger contact or FSM event needed.
        private static GameObject _explosion; private static bool _explosionLooked;
        internal static GameObject ExplosionPrefab()
        {
            if (_explosionLooked) return _explosion;
            _explosionLooked = true;
            try
            {
                var lance = Prefabs.FindAny(Projectile);
                var fsm = lance != null ? lance.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Explosion") : null;
                if (fsm != null && fsm.Fsm != null)
                    foreach (var st in fsm.Fsm.States)
                    {
                        if (st.Name != "explode") continue;
                        var acts = st.Actions;
                        if (acts == null || acts.Length == 0) { st.LoadActions(); acts = st.Actions; }
                        if (acts != null) foreach (var a in acts) { var c = a as HutongGames.PlayMaker.Actions.CreateObject; if (c != null && c.gameObject != null && c.gameObject.Value != null) { _explosion = c.gameObject.Value; break; } }
                    }
                PatrolsModule.Verbose("Rider: blast lance explosion = " + (_explosion != null ? _explosion.name : "not found (falls back to setting off a lance)"));
            }
            catch (Exception e) { Core.Log.Warn("[Patrols] " + "Rider: explosion lookup: " + e.Message); }
            return _explosion;
        }

        // a blast-lance explosion at the point, every time: the explosion object itself, or (not found) a lance set off by its FSM
        internal static void Boom(Vector3 at)
        {
            var ex = ExplosionPrefab();
            if (ex != null) { var go = UnityEngine.Object.Instantiate(ex, at, Quaternion.identity); go.SetActive(true); return; }
            var prefab = Prefabs.FindAny(Projectile);
            if (prefab == null) return;
            var bomb = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            bomb.SetActive(true);
            var rb = bomb.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
            bomb.AddComponent<Detonator>();
        }

        // 2.1.4 "Witness me!" leap: a long, flat jump - the ground speed relative to the thrower's car is fixed (hSpeed), the flight time is
        // when that ground track meets the target's predicted ground track, and the vertical part just has to land on it (a low arc: no lob).
        // false when the target runs away faster than that, the jump would take over 1.6 s, or it would need more than 35 degrees up.
        internal const float MaxLeapTime = 1.2f;   // 2.2.3: the longest "Witness me!" jump (was 1.6 s at 16 m/s - Denis: shorter)
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

        // 2.1.4: the rider's own blast - no lance, no FSM event, nothing that can fail to go off: the exploder zombie's blast effect (sound,
        // fire, physics shove of loose things) at the spot, our damage to the player within RiderBlastRadius (full at the centre, a third at
        // the edge; in the car too) and a shove of the player's car away from it
        internal static void Kaboom(Vector3 at, string who)
        {
            try { Explode.BlastAt(at); } catch (Exception e) { Core.Log.Warn("[Patrols] " + "Rider: blast effect: " + e.Message); }
            try
            {
                float radius = PatrolsModule.RiderBlastRadius.Value;
                var player = PlayerRef.Player;
                if (player == null) return;
                float d = Vector3.Distance(player.position + Vector3.up * 0.9f, at);
                var car = PlayerRef.PlayerCar;
                var crb = car != null ? car.GetComponent<Rigidbody>() : null;
                if (crb != null) d = Mathf.Min(d, Mathf.Max(0f, Vector3.Distance(crb.worldCenterOfMass, at) - 2f));   // the car's hull is ~2 m around its centre
                if (d <= radius) Ram.HurtPlayer(PatrolsModule.RiderBlastDamage.Value * Mathf.Lerp(1f, 0.33f, d / radius), "Rider " + who + " blast");
                if (crb != null && !crb.isKinematic && d <= radius) crb.AddExplosionForce(crb.mass * 5f, at, radius * 1.5f, 0.5f, ForceMode.Impulse);
                PatrolsModule.Verbose("Rider: " + who + " blast at " + at + ", the player " + d.ToString("0.0") + " m away");
            }
            catch (Exception e) { Core.Log.Warn("[Patrols] " + "Rider: blast damage: " + e.Message); }
        }

        internal static float Elevation(Vector3 v) { return Mathf.Asin(Mathf.Clamp(v.normalized.y, -1f, 1f)) * Mathf.Rad2Deg; }
    }
}
