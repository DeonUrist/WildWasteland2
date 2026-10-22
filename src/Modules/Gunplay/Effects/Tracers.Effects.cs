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

        /// <summary>Finds RangedHit_Effect and MetalImpact among the loaded prefabs (once per scene).</summary>
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
            if (Log.L2) GunplayLog.Deep("Tracers: impact effects " + (_rangedHit != null ? "RangedHit_Effect" : "none") + (_metalImpact != null ? " + MetalImpact" : ""));
        }

        /// <summary>
        /// World ready (loading screen): looks the impact prefabs up now instead of at the first impact mid-combat (README 8 #5). When one
        /// of them is not loaded yet, the first impact looks again once, exactly as before.
        /// </summary>
        internal static void WarmEffects()
        {
            if (_effectsLooked) return;
            FindEffects();
            if (_rangedHit == null || _metalImpact == null) _effectsLooked = false;
        }

        /// <summary>An NPC bullet hit the world: the vanilla impact twice, a push on loose things, metal sparks on cars.</summary>
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

        /// <summary>Sparks/smoke/decal on car metal (not wheels) using the scaled template; destroyed after 4 s.</summary>
        private static void MetalSparks(Collider col, Vector3 point, Vector3 normal)
        {
            if (!GunplaySettings.MetalSparks.Value) return;
            FindEffects();
            if (_metalImpact == null) return;
            if (!IsCarMetal(col.transform)) return;
            float k = Mathf.Clamp(GunplaySettings.MetalSparksScale.Value, 0.05f, 4f);
            var source = Mathf.Abs(k - 1f) > 0.001f ? ScaledSparks(k) : _metalImpact;
            var fx = UnityEngine.Object.Instantiate(source, point + normal * 0.01f, Quaternion.LookRotation(normal));
            if (!fx.activeSelf) { fx.hideFlags = HideFlags.None; fx.SetActive(true); }   // clones of the inactive scaled template start inactive
            UnityEngine.Object.Destroy(fx, 4f);                            // the prefab has no auto-destroy of its own
        }

        // (WW perf) The asset-store prefab is sized for a demo scene: the whole effect (sizes, speeds, the decal) is scaled through the
        // hierarchy. Done once per scale value on an inactive hidden template instead of a GetComponentsInChildren + rescale per spark.
        private static GameObject _sparksTemplate, _sparksTemplateOf;
        private static float _sparksTemplateScale;

        /// <summary>Inactive copy of MetalImpact with hierarchy scaling and localScale x <paramref name="k"/> (rebuilt when k or the prefab changes).</summary>
        private static GameObject ScaledSparks(float k)
        {
            if (_sparksTemplate != null && _sparksTemplateOf == _metalImpact && Mathf.Approximately(_sparksTemplateScale, k)) return _sparksTemplate;
            DestroySparksTemplate();
            var staging = new GameObject("WildWasteland.Gunplay.SparksStaging");
            staging.SetActive(false);                                      // the copy made under an inactive parent never wakes (no stray play)
            var t = UnityEngine.Object.Instantiate(_metalImpact, staging.transform);
            t.SetActive(false);
            t.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(staging);
            t.name = _metalImpact.name;
            t.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(t);
            foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
            t.transform.localScale = _metalImpact.transform.localScale * k;
            _sparksTemplate = t; _sparksTemplateOf = _metalImpact; _sparksTemplateScale = k;
            return t;
        }

        /// <summary>Destroys the scaled sparks template (scene change, shutdown, scale change).</summary>
        private static void DestroySparksTemplate()
        {
            if (_sparksTemplate != null) UnityEngine.Object.Destroy(_sparksTemplate);
            _sparksTemplate = null; _sparksTemplateOf = null;
        }

        // Any part of a car (attached parts, the frame/body itself, loose parts) except wheels. A car = a root carrying NWH's VehicleController
        // (looked up by name, no reference to the NWH assembly); cached per root.
        private static readonly Dictionary<int, bool> _carRoots = new Dictionary<int, bool>();
        /// <summary>The collider belongs to a car (part or body) and is not a wheel or a creature riding in it.</summary>
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
