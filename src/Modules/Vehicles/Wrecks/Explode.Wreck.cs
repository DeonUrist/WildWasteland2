using System;
using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Patrols;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Vehicle wreck rules (part condition, tweens/rpm/hinge shake stopped, darkened frame, harmless blast effect, dead chassis, wreck test and restore after load); Vehicles IVehicleWreck part of Explode (Wave A split of Patrols Vehicles/Explode.cs).</summary>
    internal sealed class VehicleWreck : IVehicleWreck
    {
        private const string BlastPrefab="Explosion_BlastZombie";
        private static readonly Color Charred=new Color(.06f,.06f,.06f,1f);
        private static readonly int ColorId=Shader.PropertyToID("_Color"),EmissionId=Shader.PropertyToID("_EmissionColor");
        private GameObject _blast;private bool _blastLooked;
        private static readonly string[] HarmfulFsms={"ExplosionRadius","ExplosionDamage"};
        /// <summary>Preserves native truck door and wheel functionality on a dead chassis.</summary>
        private static bool KeptOnTruck(GameObject car,Transform part)
        {
            for(var p=part;p!=null && p!=car.transform;p=p.parent)
                if(p.name.StartsWith("door_hinge",StringComparison.Ordinal)||p.name.StartsWith("hinge_wheel",StringComparison.Ordinal))return true;
            return false;
        }
        private static MaterialPropertyBlock _wreckBlock;
        /// <summary>Sets the Condition (and Repair) value of every initialised Condition/Repair FSM under a part.</summary>
        public void SetCondition(Transform part, float value)
        {
            foreach (var f in part.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "Condition" && f.FsmName != "Repair") continue;
                if (!f.Fsm.Initialized) continue;
                var v = f.FsmVariables.GetFsmFloat("Condition");
                if (v != null) v.Value = value;
            }
        }

        // A popped engine kept the raider's running rpm: its OnOff FSM copies RpmGear.rpm only while it sits on a car's hinge, so once loose
        // the value never drops, and its Rotate FSM (engine rocking) keeps turning the loose engine's transform every frame - it crawled and
        // tumbled on its own, also after being fitted to another car and taken out again (FSM restarts keep variables). oilDamage,
        // engineOil_Temperature and the radiator / exhaust FSMs read the same value. Every "rpm" float on the part's FSMs goes to 0.
        // A running engine wobbles on its hinge: the hinge's Shake FSM runs iTweenRotateTo (looping) on the attached part. That puts an
        // iTween component ON THE PART, which keeps rotating it after it pops off - the loose engine kept moving and could not be turned
        // by hand (the tween overwrites its rotation every frame), also after being fitted to another car and taken out again. The game's
        // own exhaust CheckTag does an iTweenStop when removed; the engine's does not, and the player only removes engines from parked
        // cars. Every iTween on the part and its children is removed (what iTween.Stop does), found by type name (no compile reference).
        /// <summary>Removes every iTween on a part and its children (a popped engine kept wobbling/rotating otherwise).</summary>
        public void StopTweens(Transform part)
        {
            if (part == null) return;
            int n = 0;
            foreach (var b in part.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && b.GetType().Name == "iTween") { b.enabled = false; UnityEngine.Object.Destroy(b); n++; }
            if (n > 0 && Log.L2) Log.Deep(VehiclesModule.Instance, "Explode: " + n + " iTween(s) stopped on " + part.name);
        }

        /// <summary>Switches the frame's hinge Shake FSMs (engine, exhaust, radiator wobble) off for good before the parts pop.</summary>
        public void QuietHinges(GameObject car)
        {
            foreach (var f in car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.FsmName != "Shake") continue;
                bool onPart = false;
                for (var a = f.transform; a != null && a != car.transform; a = a.parent) if (a.CompareTag("vehPart")) { onPart = true; break; }
                if (onPart) continue;
                f.Fsm.RestartOnEnable = false;
                f.enabled = false;
            }
        }

        /// <summary>Sets every "rpm" float of a part's FSMs to 0 (a loose engine kept its running rpm).</summary>
        public void StopRpm(Transform part)
        {
            if (part == null) return;
            foreach (var f in part.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.Fsm == null || !f.Fsm.Initialized) continue;
                foreach (var v in f.FsmVariables.FloatVariables)
                    if (v != null && string.Equals(v.Name, "rpm", StringComparison.OrdinalIgnoreCase)) v.Value = 0f;
            }
        }

        /// <summary>Chars the frame's renderers (not parts or occupants) through a property block: _Color almost black, no emission (never saved).</summary>
        public void Darken(GameObject car)
        {
            int n = 0;
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.GetType().Name == "ParticleSystemRenderer") continue;   // by name: no ParticleSystemModule reference
                bool skip = false;
                for (var a = r.transform; a != null && a != car.transform; a = a.parent)
                    if (a.CompareTag("vehPart") || ModApi.Get<IVehicleCrewMetadata>()?.HasCrewController(a.gameObject) == true || a.name.IndexOf("(Driver)", StringComparison.Ordinal) >= 0
                        || a.name.IndexOf("(Passenger)", StringComparison.Ordinal) >= 0 || a.name.IndexOf("(Rider)", StringComparison.Ordinal) >= 0 || a.name.IndexOf("_Dead", StringComparison.Ordinal) >= 0
                        || a.name == "PhysicsLock") { skip = true; break; }
                if (skip) continue;
                try
                {
                    // a property block, not r.materials: instanced materials went into the save as references Easy Save can't resolve
                    // on load (a pink wreck); the block is not saved and keeps the paint's _MainTex
                    var mats = r.sharedMaterials;
                    var b = _wreckBlock ?? (_wreckBlock = new MaterialPropertyBlock());
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        r.GetPropertyBlock(b, i);
                        if (m.HasProperty("_Color")) { var c = m.color; b.SetColor(ColorId, new Color(Charred.r, Charred.g, Charred.b, c.a)); }
                        if (m.HasProperty("_EmissionColor")) b.SetColor(EmissionId, Color.black);
                        r.SetPropertyBlock(b, i);
                        n++;
                    }
                }
                catch (Exception) { }
            }
            if (Log.L2) Log.Deep(VehiclesModule.Instance, "Explode: " + n + " material(s) charred");
        }

        /// <summary>The harmless blast effect at a point (harmful FSMs, sensors and colliders off); also the "Witness me!" rider's explosion.</summary>
        public void BlastAt(Vector3 at)
        {
            if (!_blastLooked)
            {
                _blastLooked = true;
                _blast = PrefabCatalog.Find(BlastPrefab);
                if (_blast == null) Log.Warn(VehiclesModule.Instance, "Explode: " + BlastPrefab + " prefab not found; no blast effect");
            }
            if (_blast == null) return;
            var fx = UnityEngine.Object.Instantiate(_blast, at, Quaternion.identity);
            int off = 0;
            foreach (var f in fx.GetComponentsInChildren<PlayMakerFSM>(true))
                if (Array.IndexOf(HarmfulFsms, f.FsmName) >= 0) { f.enabled = false; off++; }
            foreach (var b in fx.GetComponentsInChildren<Behaviour>(true))
            {
                string n = b.GetType().Name;
                if (n == "RangeSensor" || n == "LOSSensor" || n.EndsWith("Sensor", StringComparison.Ordinal)) { b.enabled = false; off++; }
            }
            foreach (var c in fx.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            fx.SetActive(true);
            UnityEngine.Object.Destroy(fx, 8f);   // its own FSM destroys it after 2 s; this is only the safety net
            if (Log.L2) Log.Deep(VehiclesModule.Instance, "Explode: blast effect at " + at + " (" + off + " harmful component(s) off)");
        }

        /// <summary>A dead chassis: every FSM off (except DistanceKinematic), engine and controllers off (a truck keeps its wheels, braked), loops silenced, triggers off; also after a load.</summary>
        public void Deaden(GameObject car, bool truck)
        {
            if (car == null) return;
            foreach (var f in car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null) continue;
                var t = f.transform;
                bool occupant = false;
                for (var a = t; a != null && a != car.transform; a = a.parent)
                    if (a.CompareTag("vehPart") || a.name.IndexOf("_Dead", StringComparison.Ordinal) >= 0 || a.name == "PhysicsLock") { occupant = true; break; }
                if (occupant || (truck && KeptOnTruck(car, t))) continue;
                // the vanilla far-away freeze stays: dynamic (pushable) near the player, kinematic beyond its distance - also after a load,
                // where it restarts in KinematicOn and would otherwise stay stuck there
                if (f.FsmName == "DistanceKinematic") continue;
                f.Fsm.RestartOnEnable = false;
                f.enabled = false;
            }
            // the vehicle controller and the wheel controllers (NWH suspension raycasts would keep the frame floating on invisible wheels);
            // a truck keeps both - it stands on its real wheels, engine off, handbrake and brakes on
            Nwh.StopEngine(car);   // once: every start-up path stops for good via IsWreck, nothing polls the wreck afterwards
            if (truck) { Nwh.SetInput(car, 0f, 0f, 1f); Nwh.SetHandbrake(car, 1f); }
            else foreach (var c in car.GetComponentsInChildren<Component>(true))
            {
                if (!(c is Behaviour)) continue;
                string n = c.GetType().Name;
                if (n == "VehicleController" || n == "WheelController") ((Behaviour)c).enabled = false;
            }
            // engine sound scripts (SkrilStudio RealisticEngineSound, gearbox whine, muffler crackle) and every looping sound on the frame -
            // the START FSM's cranking loop kept playing when the car blew up mid-start
            foreach (var b in car.GetComponentsInChildren<Behaviour>(true))
                if (b != null && b.GetType().Namespace == "SkrilStudio") b.enabled = false;
            StopLoops(car);
            var dt = FindChild(car.transform, "DriveTrigger");
            if (dt != null) foreach (var col in dt.GetComponents<Collider>()) col.enabled = false;
            var start = FindChild(car.transform, "START");     // the ignition's click collider (its FSM is already off)
            if (start != null) foreach (var col in start.GetComponents<Collider>()) col.enabled = false;
            ModApi.Get<IVehicleCrewWreck>()?.Disable(car);
        }

        /// <summary>Stops every looping audio source on the frame.</summary>
        private static void StopLoops(GameObject car)
        {
            foreach (var a in car.GetComponentsInChildren<AudioSource>(true))
                if (a != null && a.loop && a.isPlaying) a.Stop();
        }

        /// <summary>After a load: a chassis that exploded before the save is charred (not trucks) and dead again.</summary>
        public void RestoreDead(GameObject car, string body)
        {
            bool truck = (body ?? "").StartsWith("Rust",StringComparison.OrdinalIgnoreCase);
            if (!truck) Darken(car);
            Deaden(car, truck);
        }

        /// <summary>Finds a named descendant, direct children first (same order as Patrols' helper).</summary>
        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c.name == name) return c; }
            for (int i = 0; i < root.childCount; i++) { var r = FindChild(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }
}
