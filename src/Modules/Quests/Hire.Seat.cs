using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Hired follower, crew-seat part (Wave A, from Quests/Hire.cs; B11/B12 crew-seat service): mounting into the player's passenger seat, the seat lock each late frame, Patrols PassengerGuard attach/detach with the combat-wiring snapshot, collision ignores and dismounting.</summary>
    internal static partial class Hire
    {
        /// <summary>Keeps the mounted follower aligned with its seat and current combat target.</summary>
        public static void LateUpdate()
        {
            if (!mounted || npc == null || mountObj == null) return;
            if (guardComp != null) FeedTarget();
            // keep the passenger in the seat: other mods and the NPC's own AI may try to move it
            Transform t = npc.transform;
            if (t.parent != mountObj.transform) t.SetParent(mountObj.transform, false);
            if (poseComp == null) { t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; }
        }

        // puts the car back in the NPC's way once it has had time to walk off
        /// <summary>Restores delayed follower/car collision pairs after dismount clearance.</summary>
        static void RestoreCollisions()
        {
            if (lateOwn.Count == 0 || Time.time < restoreAt) return;
            for (int i = 0; i < lateOwn.Count; i++)
                for (int k = 0; k < lateCar.Count; k++)
                    if (lateOwn[i] != null && lateCar[k] != null) Physics.IgnoreCollision(lateOwn[i], lateCar[k], false);
            lateOwn.Clear(); lateCar.Clear();
        }

        // ---- the car -----------------------------------------------------------------------------------------------
        // The player's FSM "InCar": state "InCar" with the car in its variable "Car" (the same thing Apocapatrol reads).
        /// <summary>Reads the player native InCar state using a bounded cached FSM lookup.</summary>
        static GameObject PlayerCar(Transform pl, out bool inCar)
        {
            inCar = false;
            if (inCarFsm == null)
            {
                if (Time.unscaledTime < nextCarFind) return null;
                nextCarFind = Time.unscaledTime + 2f;
                PlayMakerFSM[] all = pl.root.GetComponentsInChildren<PlayMakerFSM>(true);
                for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].FsmName == "InCar") { inCarFsm = all[i]; break; }
                if (inCarFsm == null) return null;
            }
            if (inCarFsm.Fsm == null || !inCarFsm.Fsm.Initialized || inCarFsm.ActiveStateName != "InCar") return null;
            FsmGameObject v = inCarFsm.FsmVariables.GetFsmGameObject("Car");
            GameObject car = v != null ? v.Value : null;
            if (car == null) return null;
            inCar = true;
            return car;
        }

        // is something already sitting in the passenger seat (Apocapatrol's crews use a child called Apocapatrol.PassengerPos)?
        /// <summary>Checks existing passenger anchors before reserving a follower seat.</summary>
        static bool SeatTaken(Transform car)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(car);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 4000)
            {
                Transform t = queue.Dequeue();
                if ((t.name == "Apocapatrol.PassengerPos" || t.name == "ApocalypterQuests.HireSeat") && t.childCount > 0) return true;
                for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
            }
            return false;
        }

        // Apocapatrol's [Apocapatrol] ShootDistance: occupants only shoot at targets closer than this
        /// <summary>Uses the active follower-vehicle combat provider range or the native fallback.</summary>
        static float PatrolShootDistance() { return ModApi.Get<IFollowerVehicles>() != null ? ModApi.Get<IFollowerVehicles>().ShootDistance : 60f; }

        // The follower's own sensors look for the player's enemies the wrong way round, so while it rides the mod writes the target
        // it chose into the variable the game's Detection and Attack FSMs use (detectedObj); Apocapatrol's gunner reads it from there.
        /// <summary>Supplies the actual hired follower target and reload state to seated combat.</summary>
        static void FeedTarget() { if (ModApi.Get<IFollowerVehicles>() != null) ModApi.Get<IFollowerVehicles>().Combat(npc, target, reloadUntil); }

        static GameObject madeRaycast;

        // Apocapatrol changes the passenger for good: it rewires the events in his Attack FSM to ranged only, switches off the
        // actions of his Damage/FireDamage FSMs (melee and contact damage) and those FSMs themselves. Nothing puts that back when
        // its gunner is removed, so after the car he followed but no longer fought. All of it is recorded before and restored after.
        static readonly List<KeyValuePair<FsmStateAction, bool>> actSnap = new List<KeyValuePair<FsmStateAction, bool>>();

        static readonly List<object[]> evtSnap = new List<object[]>();          // { action, FieldInfo, value }

        static readonly List<KeyValuePair<PlayMakerFSM, bool>> preGuardFsms = new List<KeyValuePair<PlayMakerFSM, bool>>();

        /// <summary>Records native attack events, action flags and damage FSM state before guard attachment.</summary>
        static void SnapCombat()
        {
            actSnap.Clear(); evtSnap.Clear(); preGuardFsms.Clear();
            try
            {
                foreach (PlayMakerFSM f in QuestWorld.Fsms(npc))
                {
                    if (f == null) continue;
                    preGuardFsms.Add(new KeyValuePair<PlayMakerFSM, bool>(f, f.enabled));
                    if (f.FsmName != "Attack" && f.FsmName != "Damage" && f.FsmName != "FireDamage" && f.FsmName != "Damage Ranged") continue;
                    foreach (FsmState st in f.FsmStates)
                        foreach (FsmStateAction a in st.Actions)
                        {
                            if (a == null) continue;
                            actSnap.Add(new KeyValuePair<FsmStateAction, bool>(a, a.Enabled));
                            foreach (FieldInfo fi in a.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                                if (fi.FieldType == typeof(FsmEvent)) evtSnap.Add(new object[] { a, fi, fi.GetValue(a) });
                        }
                }
            }
            catch (Exception e) { Plugin.LogOnce("HireSnapCombat", e); }
        }

        /// <summary>Restores the pre-seat combat wiring and clears all captured records.</summary>
        static void RestoreCombat()
        {
            int n = 0;
            try
            {
                foreach (var kv in actSnap) if (kv.Key != null && kv.Key.Enabled != kv.Value) { kv.Key.Enabled = kv.Value; n++; }
                foreach (object[] e in evtSnap)
                {
                    FieldInfo fi = (FieldInfo)e[1];
                    if (!object.Equals(fi.GetValue(e[0]), e[2])) { fi.SetValue(e[0], e[2]); n++; }
                }
                foreach (var kv in preGuardFsms)
                    if (kv.Key != null && (kv.Key.FsmName == "Damage" || kv.Key.FsmName == "FireDamage") && kv.Key.enabled != kv.Value) { kv.Key.enabled = kv.Value; n++; }
                if (n > 0) Plugin.Info("Hire: " + displayName + "'s melee and attack wiring put back as before the car (" + n + " changes undone).");
            }
            catch (Exception ex) { Plugin.LogOnce("HireRestoreCombat", ex); }
            actSnap.Clear(); evtSnap.Clear(); preGuardFsms.Clear();
        }

        // a fresh start for his fighting AI on foot: forget the car's target and let Detection look again
        /// <summary>Clears stale seated targets and restarts native foot detection.</summary>
        static void ResetFight()
        {
            try
            {
                foreach (PlayMakerFSM f in QuestWorld.Fsms(npc))
                {
                    if (f == null) continue;
                    FsmGameObject v = f.FsmVariables.GetFsmGameObject("detectedObj");
                    if (v != null) v.Value = null;
                    if (f.FsmName == "Detection") { f.enabled = true; try { f.Fsm.SetState("notDetected"); } catch (Exception) { } }
                }
            }
            catch (Exception e) { Plugin.LogOnce("HireResetFight", e); }
        }

        /// <summary>Creates the native firing anchor when needed and attaches the typed seated-combat provider.</summary>
        static Component AttachGuard(GameObject car)
        {
            if (ModApi.Get<IFollowerVehicles>() == null) return null;
            if (FindDeep(npc.transform, "AttackRaycast_Ranged") == null)
            {
                madeRaycast = new GameObject("AttackRaycast_Ranged"); madeRaycast.transform.SetParent(npc.transform, false);
                madeRaycast.transform.localPosition = new Vector3(0, 1.4f, .4f);
            }
            SnapCombat();
            var guard = ModApi.Get<IFollowerVehicles>().SeatCombat(npc, car, mountObj.transform);
            GunSetup(); return guard;
        }

        /// <summary>Stops the guard, restores captured native state and releases visual/collider snapshots.</summary>
        static void DetachGuard()
        {
            if (flashObjs != null) foreach (GameObject g in flashObjs) if (g != null) g.SetActive(false);
            flashOff = 0f; gunArm = null; gunHand = null; soundQueue.Clear();
            if (madeRaycast != null) { UnityEngine.Object.Destroy(madeRaycast); madeRaycast = null; }
            Behaviour gb = guardComp as Behaviour;
            if (gb != null) gb.enabled = false;           // Destroy waits for the end of the frame; its Update must not mute his AI again before that
            if (guardComp != null) UnityEngine.Object.Destroy(guardComp);
            RestoreCombat();
            guardComp = null;
            foreach (var kv in rendSnap) if (kv.Key != null) kv.Key.enabled = kv.Value;
            foreach (var kv in colSnap) if (kv.Key != null) kv.Key.enabled = kv.Value;
            rendSnap.Clear(); colSnap.Clear();
        }

        static Animator seatAnim; static bool wasRootMotion; static float nextColCheck;

        // any collider of his that came on after he sat down (weapons, the fire-damage box...) must not touch the car either
        /// <summary>Adds newly enabled follower colliders to the existing car collision ignores.</summary>
        static void IgnoreNewColliders()
        {
            if (npc == null) return;
            try
            {
                foreach (Collider c in npc.GetComponentsInChildren<Collider>(false))
                {
                    if (c == null || !c.enabled || ownCols.Contains(c)) continue;
                    ownCols.Add(c);
                    for (int k = 0; k < ignoredCars.Count; k++) if (ignoredCars[k] != null) Physics.IgnoreCollision(c, ignoredCars[k], true);
                }
            }
            catch (Exception e) { Plugin.LogOnce("HireIgnoreCols", e); }
        }

        /// <summary>Reserves the passenger anchor and transfers the follower into reversible seated behavior.</summary>
        static bool TryMount(GameObject car)
        {
            Transform ct = car.transform;
            Transform sit = FindDeep(ct, "sitPos"), hd = FindDeep(ct, "hinge_seat_driver"), hp = FindDeep(ct, "hinge_seat_passenger");
            if (sit == null || hd == null || hp == null) { Plugin.Info("Hire: " + car.name + " has no passenger seat; the follower stays out."); return false; }
            if (SeatTaken(ct)) { Plugin.Info("Hire: the passenger seat of " + car.name + " is taken; the follower stays out."); return false; }

            mountObj = new GameObject("ApocalypterQuests.HireSeat");
            mountObj.transform.SetParent(ct, false);
            mountObj.transform.position = sit.position + (hp.position - hd.position);
            mountObj.transform.rotation = sit.rotation;
            seatBase = mountObj.transform.localRotation;

            // stop him first: a running NPC keeps his run animation (and its root motion) and shoves the car about
            EndFootShot();
            if (moveFsm != null && moveFsm.ActiveStateName == "Run") { try { moveFsm.Fsm.SetState("Idle"); } catch (Exception) { } }
            seatAnim = npc.GetComponent<Animator>() ?? npc.GetComponentInChildren<Animator>(true);
            if (seatAnim != null) { wasRootMotion = seatAnim.applyRootMotion; seatAnim.applyRootMotion = false; try { seatAnim.Play("idle", 0, 0f); } catch (Exception) { } }

            // switch the NPC's own brain off while it sits
            Ranged.Clear(); _nearestName = null;
            muted.Clear();
            PlayMakerFSM[] fsms = QuestWorld.Fsms(npc);
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] == null || !fsms[i].enabled) continue;
                if (Array.IndexOf(MuteNames, fsms[i].FsmName) < 0) continue;
                fsms[i].enabled = false;
                muted.Add(fsms[i]);
            }
            Rigidbody rb = npc.GetComponent<Rigidbody>();
            wasKinematic = rb != null && rb.isKinematic;
            if (rb != null) { wasInterp = rb.interpolation; wasDetect = rb.collisionDetectionMode; wasGravity = rb.useGravity; }
            if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }

            // it must not shove the car around
            ownCols.Clear(); ignoredCars.Clear();
            foreach (Collider c in npc.GetComponentsInChildren<Collider>(false)) if (c != null && c.enabled) ownCols.Add(c);
            foreach (Collider c in car.GetComponentsInChildren<Collider>(false)) if (c != null && c.enabled) ignoredCars.Add(c);
            for (int i = 0; i < ownCols.Count; i++)
                for (int k = 0; k < ignoredCars.Count; k++)
                    if (ownCols[i] != null && ignoredCars[k] != null) Physics.IgnoreCollision(ownCols[i], ignoredCars[k], true);

            origParent = npc.transform.parent;
            npc.transform.position = mountObj.transform.position;
            npc.transform.rotation = mountObj.transform.rotation;
            npc.transform.SetParent(mountObj.transform, true);

            poseComp = null;
            if (ModApi.Get<IFollowerVehicles>() != null)
                poseComp = ModApi.Get<IFollowerVehicles>().SeatPose(npc, mountObj.transform, Core.Names.Base(npc.name), ArmTemplate(Core.Names.Base(npc.name)));

            // ranged followers shoot from the seat with Apocapatrol's own passenger gunner
            guardComp = null;
            try
            {
                if (shootFromCar.Value && IsRangedNpc(npc)) guardComp = AttachGuard(car);
            }
            catch (Exception e) { Plugin.LogOnce("HireGuard", e.InnerException ?? e); guardComp = null; }

            IgnoreNewColliders();             // the gunner setup can switch some of his colliders back on
            mounted = true;
            carObj = car;
            target = null;
            Plugin.Info("Hire: " + displayName + " rides in " + car.name + ".");
            return true;
        }

        /// <summary>Releases seated behavior and restores the follower parent, physics, animation and foot AI.</summary>
        static void Dismount(bool place)
        {
            if (!mounted) return;
            mounted = false;
            outSince = -1f;
            GameObject carForExit = carObj;
            carObj = null;
            inCarSince = -1f;
            DetachGuard();
            footSince = -1f;                               // keeps the car's target: on foot he goes on shooting at it (FootWatch)
            Transform pl = Core.PlayerState.Transform;
            try
            {
                if (poseComp != null)
                {
                    if (ModApi.Get<IFollowerVehicles>() != null) ModApi.Get<IFollowerVehicles>().ReleasePose(poseComp);
                    UnityEngine.Object.Destroy(poseComp);
                }
            }
            catch (Exception) { }
            poseComp = null;

            if (npc != null)
            {
                // first find the spot (the car's colliders are still ignored, so nothing pushes the NPC while it moves)
                Vector3 spot = npc.transform.position;
                if (place && pl != null) spot = FindExit(pl, carForExit);
                npc.transform.SetParent(origParent, true);
                npc.transform.position = spot;
                npc.transform.rotation = Quaternion.Euler(0f, pl != null ? pl.eulerAngles.y : npc.transform.eulerAngles.y, 0f);
                if (place) LiftAbove(spot.y - 0.15f);
                Rigidbody rb = npc.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = wasKinematic;
                    rb.useGravity = wasGravity; rb.detectCollisions = true;
                    try { rb.collisionDetectionMode = wasKinematic ? CollisionDetectionMode.ContinuousSpeculative : wasDetect; rb.interpolation = wasInterp; } catch (Exception) { }
                    if (!wasKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.WakeUp(); }
                }
                foreach (Collider c in npc.GetComponentsInChildren<Collider>(true)) if (c != null && c.gameObject.activeInHierarchy && !c.enabled && ownCols.Contains(c)) c.enabled = true;
                Physics.SyncTransforms();
                LogBody("got out, placed at " + spot.ToString("0.0"));
                for (int i = 0; i < muted.Count; i++) if (muted[i] != null) muted[i].enabled = true;
                if (seatAnim != null) { seatAnim.applyRootMotion = wasRootMotion; seatAnim = null; }
                wakeLogged = false; WakeBrain(true);
                ResetFight();
                footSetup = false;
                settleUntil = Time.time + 2.5f;
                // the car stays "not solid" for this NPC for a few seconds more, in case it is still next to the doors
                lateOwn.Clear(); lateCar.Clear();
                lateOwn.AddRange(ownCols); lateCar.AddRange(ignoredCars);
                restoreAt = Time.time + 5f;
            }
            Ranged.Clear(); _nearestName = null;
            muted.Clear(); ignoredCars.Clear(); ownCols.Clear();
            if (mountObj != null) UnityEngine.Object.Destroy(mountObj);
            mountObj = null;
            Plugin.Info("Hire: " + displayName + " got out.");
        }
    }
}
