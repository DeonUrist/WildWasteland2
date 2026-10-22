using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Blast lance of a human turret: the hand prop and the thrown projectile on its intercept course; Gunplay (Wave A split of Crews/Rider.cs).</summary>
    internal sealed partial class RiderGuard
    {
        // the blast lance item as a prop in the throwing hand: no FSMs, no physics, no colliders
        /// <summary>The lance prop in the throwing hand (no FSMs, physics or colliders).</summary>
        private void BuildLance()
        {
            if (_hand == null) return;
            var prefab = LancePrefabs.FindAny(Rider.LanceItem);
            if (prefab == null) { if (Log.L2) GunplayLog.Deep("Rider: no " + Rider.LanceItem + " prefab for the hand prop"); return; }
            var go = UnityEngine.Object.Instantiate(prefab, _hand.position, _hand.rotation);
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true)) UnityEngine.Object.DestroyImmediate(f);
            foreach (var j in go.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null) UnityEngine.Object.DestroyImmediate(b);
            go.name = "RiderLance";
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            go.SetActive(true);
            go.transform.SetParent(_hand, false);
            Lance = go.transform;
            ApplyLanceOffset();
        }

        // the lance leaves the hand: the player's projectile prefab on the intercept course (predicted target, gravity, the car's speed)
        /// <summary>Throws the projectile prefab on the intercept course with a LanceFuse.</summary>
        private void Release(Vector3 tpos, Vector3 tvel)
        {
            try
            {
                if (Lance != null) Lance.gameObject.SetActive(false);
                var prefab = LancePrefabs.FindAny(Rider.Projectile);
                if (prefab == null) { GunplayLog.Warn("Rider: projectile prefab not found: " + Rider.Projectile); return; }
                float speed = PatrolsModule.RiderLanceSpeed.Value;
                var from = HandPos(); var carVel = CarVel();
                Vector3 launch; float flight;
                if (!Rider.Solve(from, carVel, tpos + Vector3.up * 0.9f, tvel, speed, out launch, out flight) && Log.L2) GunplayLog.Deep("Rider: target moved out of reach during the swing - thrown at the best angle");
                var dir = launch.normalized;
                var go = UnityEngine.Object.Instantiate(prefab, from + dir * 0.6f, Quaternion.LookRotation(dir));
                go.SetActive(true);
                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.velocity = launch + carVel;
                IgnoreOwn(go);
                go.AddComponent<LanceFuse>();   // explodes on any hit, whatever angle it lands at
                if (Log.L2) GunplayLog.Deep("Rider: " + name + " threw a blast lance, " + flight.ToString("0.00") + " s flight, " + Rider.Elevation(launch).ToString("0") + "° up");
            }
            catch (Exception e) { GunplayLog.Warn("Rider: throw: " + e.Message); }
        }
    }
}
