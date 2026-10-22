using System;
using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Self-destructing cars ([Self-destruct] SelfDestructingCars): once a raider car is fully vacated - the crew is dead, bailed out,
    // or the last passenger got out beside a dead driver - it "explodes": the frame goes almost black, the exploder zombie's harmless blast (fireball + bang)
    // goes off at the car, and every part pops off its hinge with condition 0 ([Scaling] ExplodedCarPartsPercent % of them keep theirs).
    // What is left is a dead chassis: nothing to enter, attach, adjust or fuel, and the Cleanup removes it beyond 1000 m.
    // Trucks (Rust* bodies) are a lootable wreck instead: no charring, the wheels stay on (same condition roll) and it sits on them
    // with the handbrake on, the rear doors still open and close, and the cargo stays locked in the bed where it spawned.
    //
    // Popping a part is what the wrench does: re-tag it vehPartRemoved on layer 9 (the part's own de_Attach state); the part's
    // CheckTag FSM then unparents it and adds a Rigidbody on its next frame, and we give that body a shove the frame after.
    internal static partial class Explode
    {
        internal const float RemoveDistance = 1000f;          // a dead chassis is removed beyond this (the game only deletes cars at 5 km)
        private const float Delay = 1f;                        // after the vacate, so the last carcass has left the seat
        private const string BlastPrefab = "Explosion_BlastZombie";   // what the exploder zombie's Health FSM spawns when it dies
        private static readonly Color Charred = new Color(0.06f, 0.06f, 0.06f, 1f);
        private static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");
        private static GameObject _blast; private static bool _blastLooked;
        // the blast prefab's harmful half: ExplosionRadius grows its trigger sphere, ExplosionDamage sends distance-scaled Damage to every
        // Bodypart its Range/LOS sensors detect. Switched off right after Instantiate, before they Start.
        private static readonly string[] HarmfulFsms = { "ExplosionRadius", "ExplosionDamage" };

        // Called by the Crew when the car is vacated. Re-checks after a short delay: a car the player took or sits in is never blown up.
        /// <summary>Called by the Crew when the car is vacated. Re-checks after a short delay: a car the player took or sits in is never blown up.</summary>
        internal static void Schedule(Crew crew, GameObject car, PatrolMarker marker, string why)
        {
            if (car == null || marker == null || marker.Exploded || !Eligible(marker)) return;
            crew.StartCoroutine(Routine(car, marker, why));
        }

        /// <summary>Runs the delayed vehicle destruction sequence under native lifetime gates.</summary>
        private static IEnumerator Routine(GameObject car, PatrolMarker marker, string why)
        {
            yield return new WaitForSeconds(Delay);
            if (car == null || marker == null || marker.Exploded || marker.PlayerEntered) yield break;
            if (!Eligible(marker)) yield break;
            while(car!=null&&marker!=null&&LivingOccupants(marker))yield return new WaitForSeconds(0.25f);
            if(car==null||marker==null||marker.Exploded||marker.PlayerEntered||!Eligible(marker))yield break;   // Exploded: a second routine (CrewReturn re-schedule) never blasts twice
            if(!marker.ExplodeRolled){marker.ExplodeRolled=true;marker.ExplodeSelected=Roll(marker.ExplodeChance,PatrolsModule.SelfDestruct.Value,UnityEngine.Random.value);}
            if(!marker.ExplodeSelected&&!PatrolsModule.SelfDestruct.Value)yield break;
            if (PlayerRef.PlayerCar == car) yield break;
            marker.Exploded = true;
            PatrolsModule.Verbose("Explode: " + car.name + " (" + why + ")");

            bool truck = IsTruck(marker.BodyPrefab);
            var crew = car.GetComponent<Crew>();
            if (crew != null) crew.OnExploded();
            for (int ri = 0; ri < marker.Riders.Length; ri++)
            {
                var r = marker.Riders[ri]; if (r == null) continue;
                var rg = r.GetComponent<RiderGuard>(); if (rg != null) rg.JumpOff("the car blows up");
                marker.Riders[ri] = null;
            }
            Core.ModApi.Get<Core.IVehicleDrive>().SetInput(car, 0f, 0f, truck ? 1f : 0f);
            if (truck) { Core.ModApi.Get<Core.IVehicleDrive>().StopEngine(car); Patrol.Handbrake(car, true); Core.ModApi.Get<Core.IVehicleDrive>().SetHandbrake(car, 1f); }

            if (!truck) Darken(car);
            Blast(car);

            // the hinges' engine wobble (Shake FSM: iTweenRotateTo on the part while the car's rpm is up) stops before anything pops,
            // so it cannot start a new tween on a part that is about to be loose
            QuietHinges(car);
            Patrol.Headlights(car, false);   // popped headlights would keep shining on the ground
            var parts = new List<Transform>();
            int kept = 0, wheels = 0;
            foreach (var p in Parts(car))
            {
                bool keep = KeepsCondition();
                if (!keep) SetCondition(p, 0f);
                else kept++;
                if (truck && OnWheelHinge(car, p)) { wheels++; continue; }   // a truck keeps its wheels (rolled like the rest)
                parts.Add(p);
                StopRpm(p);
                StopTweens(p);
                // the wrench's de_Attach: layer Item + tag vehPartRemoved -> the part's CheckTag FSM unparents it and adds a Rigidbody
                p.gameObject.layer = 9;
                try { p.tag = "vehPartRemoved"; } catch (Exception e) { Core.Log.Warn("[Patrols] " + "Explode: tag: " + e.Message); }
            }
            var cargo = truck ? new List<Transform>() : Cargo(car);   // a truck's cargo stays locked in the bed
            foreach (var c in cargo)
            {
                // a locked item: the vanilla pickup path (LockPhysics_OFF) gives it its Rigidbody back
                foreach (var f in c.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "LockPhysics") { f.enabled = true; f.SendEvent("LockPhysics_OFF"); }
                c.SetParent(null, true);
            }
            PatrolsModule.Verbose("Explode: " + parts.Count + " part(s) popped" + (truck ? ", " + wheels + " wheel(s) stay on" : "") + " (" + kept + " keep their condition), "
                + (truck ? "cargo stays in the bed" : cargo.Count + " cargo item(s) spilled"));

            yield return null;
            yield return null;   // CheckTag / LockPhysics have run: the Rigidbodies exist
            if (car == null) yield break;
            var centre = car.transform.position;
            foreach (var t in parts) { StopRpm(t); StopTweens(t); Shove(t, centre, 3f, 6f); }   // again: CheckTag has re-enabled OnOff / Rotate
            foreach (var t in cargo) Shove(t, centre, 2f, 4f);
            // a part the CheckTag FSM did not free (none expected): free it ourselves
            foreach (var t in parts)
                if (t != null && t.IsChildOf(car.transform))
                {
                    t.SetParent(null, true);
                    if (t.GetComponent<Rigidbody>() == null) t.gameObject.AddComponent<Rigidbody>();
                    Shove(t, centre, 3f, 6f);
                }

            Deaden(car, truck);
        }

        // ------------------------------------------------------------ pieces

        /// <summary>Returns the shipped default chance for the current explode.</summary>
        internal static float DefaultChance(string name)=>string.Equals(name,"War_rig",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"Truck_War_Rig",StringComparison.OrdinalIgnoreCase)?0f:50f;
        /// <summary>Selects the configured outcome using the caller random source.</summary>
        internal static bool Roll(float chance,bool always,float roll)=>always||roll*100f<Mathf.Clamp(chance,0f,100f);
        /// <summary>Checks current progression and chance gates for the authored spawn group.</summary>
        internal static bool Eligible(PatrolMarker marker)=>marker!=null&&marker.DriverDied&&!marker.DriverBailed&&!marker.PlayerEntered&&!CrewReturn.Holds(marker.gameObject);   // never while its crew may still walk back
        /// <summary>Applies the accepted battle wreck outcome without rerolling it.</summary>
        internal static void BattleWreck(GameObject car)
        {
            var marker=car.GetComponent<PatrolMarker>();if(marker==null||marker.PlayerEntered)return;
            marker.Exploded=true;marker.DriverDied=true;Core.ModApi.Get<Core.IVehicleDrive>().StopEngine(car);Core.ModApi.Get<Core.IVehicleDrive>().SetHandbrake(car,1f);
            foreach(var part in Parts(car))SetCondition(part,0f);
            bool truck=IsTruck(marker.BodyPrefab);if(!truck)Darken(car);Blast(car);QuietHinges(car);Patrol.Headlights(car,false);Deaden(car,truck);
        }
        /// <summary>Counts living occupants for the current explode.</summary>
        private static bool LivingOccupants(PatrolMarker marker)
        {
            if(SeatAlive(marker.Driver)||SeatAlive(marker.Passenger))return true;
            foreach(var rider in marker.Riders)if(SeatAlive(rider))return true;
            return false;
        }
        /// <summary>Checks the current seat alive condition for explode.</summary>
        private static bool SeatAlive(GameObject npc)
        {if(npc==null)return false;foreach(var f in npc.GetComponents<PlayMakerFSM>())if(f.FsmName=="Health"){var health=f.FsmVariables.GetFsmFloat("Health");if(health!=null)return health.Value>0f;}return !npc.name.Contains("_Dead");}

        // the cargo trucks (Rustcargo...): same test as the convoy / template code
        /// <summary>the cargo trucks (Rustcargo...): same test as the convoy / template code</summary>
        internal static bool IsTruck(string body) { return (body ?? "").StartsWith("Rust", StringComparison.OrdinalIgnoreCase); }

        /// <summary>Handles the qualifying native event for wheel hinge for the current explode.</summary>
        private static bool OnWheelHinge(GameObject car, Transform t)
        {
            for (var a = t.parent; a != null && a != car.transform; a = a.parent)
                if (a.name.StartsWith("hinge_wheel", StringComparison.Ordinal)) return true;
            return false;
        }

        // what a dead truck keeps working: the rear doors (parts/door_hinge_*: DoorOpen/DoorClose), the wheel hinges (Suspension,
        // checkWheel, attach - the NWH wheels read them) and the cargo in parts/PhysicsLock (its own LockPhysics/pickup FSMs)
        /// <summary>what a dead truck keeps working: the rear doors (parts/door_hinge_*: DoorOpen/DoorClose), the wheel hinges (Suspension, checkWheel, attach - the NWH wheels read them) and the cargo in parts/PhysicsLock (its own LockPhysics/pickup FSMs)</summary>
        private static bool KeptOnTruck(GameObject car, Transform t)
        {
            for (var a = t; a != null && a != car.transform; a = a.parent)
                if (a.name.StartsWith("door_hinge", StringComparison.Ordinal) || a.name.StartsWith("hinge_wheel", StringComparison.Ordinal)) return true;
            return false;
        }

        // Every attached part: a vehPart-tagged object under the car (not one nested in another part, not an occupant)
        /// <summary>Every attached part: a vehPart-tagged object under the car (not one nested in another part, not an occupant)</summary>
        private static List<Transform> Parts(GameObject car)
        {
            var list = new List<Transform>();
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
            {
                if (t == car.transform || !t.CompareTag("vehPart")) continue;
                bool nested = false;
                for (var a = t.parent; a != null && a != car.transform; a = a.parent) if (a.CompareTag("vehPart")) { nested = true; break; }
                if (!nested) list.Add(t);
            }
            return list;
        }

        // Cargo locked in the bed: children of parts/PhysicsLock that carry a LockPhysics FSM
        /// <summary>Cargo locked in the bed: children of parts/PhysicsLock that carry a LockPhysics FSM</summary>
        private static List<Transform> Cargo(GameObject car)
        {
            var list = new List<Transform>();
            var lockGo = Patrol.FindChild(car.transform, "PhysicsLock");
            if (lockGo == null) return list;
            for (int i = 0; i < lockGo.childCount; i++)
            {
                var c = lockGo.GetChild(i);
                foreach (var f in c.GetComponents<PlayMakerFSM>()) if (f.FsmName == "LockPhysics") { list.Add(c); break; }
            }
            return list;
        }

        // [Scaling] ExplodedCarPartsPercent (2.6.4): a plain chance per part to keep its condition (was a weighted roll against 12 = 20 %)
        /// <summary>[Scaling] ExplodedCarPartsPercent (2.6.4): a plain chance per part to keep its condition (was a weighted roll against 12 = 20 %)</summary>
        private static bool KeepsCondition()
        {
            float pct = Mathf.Clamp(PatrolsModule.ExplodedLootPercent.Value, 0f, 100f);
            if (pct <= 0f) return false;
            if (pct >= 100f) return true;
            return UnityEngine.Random.value * 100f < pct;
        }

        /// <summary>Applies the existing native destruction impulse to an eligible object.</summary>
        private static void Shove(Transform t, Vector3 centre, float min, float max)
        {
            if (t == null) return;
            var rb = t.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return;
            var dir = t.position - centre; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = UnityEngine.Random.insideUnitSphere;
            dir.y = 0f; dir.Normalize();
            float speed = UnityEngine.Random.Range(min, max);
            rb.WakeUp();
            rb.velocity = dir * speed + Vector3.up * (speed * 0.8f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
        }

        // The exploder zombie's blast without its damage: the game's own Explosion_BlastZombie prefab (AudioSource explosion_03, 0.7, 100 m,
        // play-on-awake; `Explosion` FSM: ParticleSystemPlay BigExplosion with children (fireball, smoke, debris, embers, light, shockwave) +
        // AudioPlay + a physics Explosion (force 3000, radius 5, which also helps throw the parts) + Wait 2 s + DestroySelf). Its damage FSMs,
        // sensors and trigger collider are disabled before they Start; it is not registered, so the game never saves it.
        /// <summary>The exploder zombie's blast without its damage: the game's own Explosion_BlastZombie prefab (AudioSource explosion_03, 0.7, 100 m, play-on-awake; `Explosion` FSM: ParticleSystemPlay BigExplosion with children (fireball, smoke, debris, embers, light, shockwave) + AudioPlay + a physics Explosion (force 3000, radius 5, which also helps throw the parts) + Wait 2 s + DestroySelf). Its damage FSMs, sensors and trigger collider are disabled before they Start; it is not registered, so the game never saves it.</summary>
        private static void Blast(GameObject car)
        {
            Core.Services.RaiseVehicleExploded(car);   // WW: the NPC module subscribes (ex a Harmony patch on Apocapatrol.Explode.Blast)
            if (!_blastLooked)
            {
                _blastLooked = true;
                _blast = Prefabs.FindAny(BlastPrefab);
                if (_blast == null) Core.Log.Warn("[Patrols] " + "Explode: " + BlastPrefab + " prefab not found; no blast effect");
            }
            if (_blast == null) return;
            var rb = car.GetComponent<Rigidbody>();
            BlastAt((rb != null ? rb.worldCenterOfMass : car.transform.position) + Vector3.up * 0.3f);
        }
    }
}
