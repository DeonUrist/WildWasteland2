using System;
using System.Linq;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Gunplay-owned lance explosion effects, native damage and detonation.</summary>
    internal static class LanceExplosions
    {
        internal const string Projectile="blastlance_1_explode";
        // The blast lance's own explosion: what the projectile's [Explosion] FSM creates in its "explode" state (CreateObject), read once from the
        // prefab's FSM. Spawned directly, it always goes off - no trigger contact or FSM event needed.
        private static GameObject _explosion; private static bool _explosionLooked;
        /// <summary>The blast lance explosion object its Explosion FSM creates (read once from the prefab).</summary>
        internal static GameObject ExplosionPrefab()
        {
            if (_explosionLooked) return _explosion;
            _explosionLooked = true;
            try
            {
                var lance = LancePrefabs.FindAny(Projectile);
                var fsm = lance != null ? lance.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Explosion") : null;
                if (fsm != null && fsm.Fsm != null)
                    foreach (var st in fsm.Fsm.States)
                    {
                        if (st.Name != "explode") continue;
                        var acts = st.Actions;
                        if (acts == null || acts.Length == 0) { st.LoadActions(); acts = st.Actions; }
                        if (acts != null) foreach (var a in acts) { var c = a as HutongGames.PlayMaker.Actions.CreateObject; if (c != null && c.gameObject != null && c.gameObject.Value != null) { _explosion = c.gameObject.Value; break; } }
                    }
                if (Log.L2) GunplayLog.Deep("Rider: blast lance explosion = " + (_explosion != null ? _explosion.name : "not found (falls back to setting off a lance)"));
            }
            catch (Exception e) { GunplayLog.Warn("Rider: explosion lookup: " + e.Message); }
            return _explosion;
        }

        // a blast-lance explosion at the point, every time: the explosion object itself, or (not found) a lance set off by its FSM
        /// <summary>A blast-lance explosion at the point (the explosion object, else a lance set off).</summary>
        internal static void Boom(Vector3 at)
        {
            var ex = ExplosionPrefab();
            if (ex != null) { var go = UnityEngine.Object.Instantiate(ex, at, Quaternion.identity); go.SetActive(true); return; }
            var prefab = LancePrefabs.FindAny(Projectile);
            if (prefab == null) return;
            var bomb = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            bomb.SetActive(true);
            var rb = bomb.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
            bomb.AddComponent<Detonator>();
        }

        // 2.1.4: the rider's own blast - no lance, no FSM event, nothing that can fail to go off: the exploder zombie's blast effect (sound,
        // fire, physics shove of loose things) at the spot, our damage to the player within RiderBlastRadius (full at the centre, a third at
        // the edge; in the car too) and a shove of the player's car away from it
        /// <summary>A rider's own blast: effect, damage to the player within the radius, shove of the player's car.</summary>
        internal static void Kaboom(Vector3 at, string who,float radius,float damage)
        {
            try { ModApi.Get<IVehicleWreck>()?.BlastAt(at); } catch (Exception e) { GunplayLog.Warn("Rider: blast effect: " + e.Message); }
            try
            {
                var player = PlayerState.Transform;
                if (player == null) return;
                float d = Vector3.Distance(player.position + Vector3.up * 0.9f, at);
                var car = PlayerState.Car;
                var crb = car != null ? car.GetComponent<Rigidbody>() : null;
                if (crb != null) d = Mathf.Min(d, Mathf.Max(0f, Vector3.Distance(crb.worldCenterOfMass, at) - 2f));   // the car's hull is ~2 m around its centre
                if (d <= radius) NativePlayerDamage.Apply(damage * Mathf.Lerp(1f, 0.33f, d / radius), "Rider " + who + " blast");
                if (crb != null && !crb.isKinematic && d <= radius) crb.AddExplosionForce(crb.mass * 5f, at, radius * 1.5f, 0.5f, ForceMode.Impulse);
                if (Log.L2) GunplayLog.Deep("Rider: " + who + " blast at " + at + ", the player " + d.ToString("0.0") + " m away");
            }
            catch (Exception e) { GunplayLog.Warn("Rider: blast damage: " + e.Message); }
        }

    }
}
