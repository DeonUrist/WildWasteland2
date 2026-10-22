using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Impact effects (Wave A split of Tracers.cs, B09): RangedHit/MetalImpact prefabs, world impacts and metal sparks on car parts.</summary>
    internal static partial class Tracers
    {
        // obstruction: a vehicle part loses condition, a bolted metal plate may come off
        // ---------- impact effects ----------
        // The player's own world hit (vanilla): RangedHit_Effect (sparks + its own sound) created twice - by the Attack hit state and again
        // by the HitEffect FSM - at hitPoint with rotation Euler(hitNormal), and 400 N along the camera forward on the thing hit.
        // NPC bullets that miss did nothing at all in vanilla; here they get exactly the same look. MetalImpact (an unused asset-store
        // prefab in the build: sparks, smoke, a bullet decal) is added on vehicle parts and metal plates when the game has it loaded.
        private static GameObject _rangedHit, _metalImpact;
        private static bool _effectsLooked;

        private static void FindEffects()
        {
            if (_effectsLooked) return;
            _effectsLooked = true;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.scene.IsValid()) continue;          // prefabs only, not scene instances
                if (_rangedHit == null && go.name == "RangedHit_Effect") _rangedHit = go;
                else if (_metalImpact == null && go.name == "MetalImpact" && go.transform.parent == null) _metalImpact = go;
                if (_rangedHit != null && _metalImpact != null) break;
            }
            if (Core.Log.Verbose) GunplayLog.Main("Tracers: impact effects " + (_rangedHit != null ? "RangedHit_Effect" : "none") + (_metalImpact != null ? " + MetalImpact" : ""));
        }

        private static void WorldImpact(Collider col, Vector3 point, Vector3 normal, Vector3 dir)
        {
            if (!GunplaySettings.ImpactEffects.Value) return;
            FindEffects();
            if (_rangedHit != null)
            {
                var rot = Quaternion.Euler(normal);                       // what the vanilla CreateObject does with {hitNormal}
                UnityEngine.Object.Instantiate(_rangedHit, point, rot);
                UnityEngine.Object.Instantiate(_rangedHit, point, rot);   // vanilla spawns it twice (hit state + HitEffect FSM)
            }
            var rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic && !OnCar(col, rb)) rb.AddForceAtPosition(dir * 400f, point, ForceMode.Force);   // loose things only: doors / hoods don't swing
            MetalSparks(col, point, normal);
        }

        private static void MetalSparks(Collider col, Vector3 point, Vector3 normal)
        {
            if (!GunplaySettings.MetalSparks.Value) return;
            FindEffects();
            if (_metalImpact == null) return;
            if (!IsCarMetal(col.transform)) return;
            var fx = UnityEngine.Object.Instantiate(_metalImpact, point + normal * 0.01f, Quaternion.LookRotation(normal));
            float k = Mathf.Clamp(GunplaySettings.MetalSparksScale.Value, 0.05f, 4f);
            if (Mathf.Abs(k - 1f) > 0.001f)
            {
                // the asset-store prefab is sized for a demo scene: scale the whole effect (sizes, speeds, the decal) through the hierarchy
                foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
                fx.transform.localScale *= k;
            }
            UnityEngine.Object.Destroy(fx, 4f);                            // the prefab has no auto-destroy of its own
        }

        // Any part of a car (attached parts, the frame/body itself, loose parts) except wheels. A car = a root carrying NWH's VehicleController
        // (looked up by name, no reference to the NWH assembly); cached per root.
        private static readonly Dictionary<int, bool> _carRoots = new Dictionary<int, bool>();
        private static bool IsCarMetal(Transform t)
        {
            if (Creature(t) != null) return false;          // (1.0.1) a creature riding in a car (Apocapatrol crews): flesh, not metal
            bool onCar = false;
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("tire", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("tyre", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;                                   // a wheel item, its hinge, the hub collider: rubber, no sparks
                if (!onCar && (p.CompareTag("vehPart") || p.CompareTag("vehPartRemoved"))) onCar = true;
            }
            if (onCar) return true;
            var root = t.root;
            int id = root.GetInstanceID();
            bool car;
            if (!_carRoots.TryGetValue(id, out car)) { car = root.GetComponent("VehicleController") != null; _carRoots[id] = car; }
            return car;
        }
    }
}
