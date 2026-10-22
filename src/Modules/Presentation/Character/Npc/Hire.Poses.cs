using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Hired follower, Presentation part (Wave A, from Quests/Hire.cs; B05): seated turn towards the target, muzzle-flash/arm parts scan, recoil applied before rendering, shot animation and the borrowed crew arm-pose template.</summary>
    internal static partial class Hire
    {
        /// <summary>the Animator state his Movement FSM plays to shoot on foot ("attack 2", "attack 4"...)</summary>
        static string ShootAnimName()
        {
            try
            {
                PlayMakerFSM mv = moveFsm ?? FindFsm(npc, "Movement");
                if (mv == null) return null;
                foreach (FsmState st in mv.FsmStates)
                {
                    if (st.Name != "AttackRanged") continue;
                    foreach (FsmStateAction a in st.Actions)
                    {
                        if (a == null) continue;
                        FieldInfo fi = a.GetType().GetField("stateName");
                        FsmString v = fi != null ? fi.GetValue(a) as FsmString : null;
                        if (v != null && !string.IsNullOrEmpty(v.Value)) return v.Value;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        // Turn in the seat to face the target (only around the vertical); back to the seat's front when there is none.
        // The game's own Attack FSM does this for most of them, but not for all (Bolter), and not while reloading.
        static Quaternion seatBase = Quaternion.identity;

        /// <summary>Turns the follower's seat toward his target (±150°, yaw only), or back to the seat's front without one.</summary>
        static void TurnToTarget()
        {
            if (npc == null || mountObj == null || mountObj.transform.parent == null) return;
            try
            {
                // turn the seat itself: Apocapatrol's pose sits him on it and its firing arc is measured from it, so he turns and can fire to the side
                Transform car = mountObj.transform.parent;
                Quaternion want = seatBase;
                if (target != null && Alive(target))
                {
                    Vector3 local = car.InverseTransformDirection(target.transform.position - mountObj.transform.position);
                    Vector3 baseFwd = seatBase * Vector3.forward;
                    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg - Mathf.Atan2(baseFwd.x, baseFwd.z) * Mathf.Rad2Deg;
                    yaw = Mathf.DeltaAngle(0f, yaw);
                    yaw = Mathf.Clamp(yaw, -150f, 150f);
                    want = Quaternion.AngleAxis(yaw, Vector3.up) * seatBase;
                }
                mountObj.transform.localRotation = Quaternion.RotateTowards(mountObj.transform.localRotation, want, 200f * Time.deltaTime);
            }
            catch (Exception e) { Plugin.LogOnce("HireTurn", e); }
        }

        static Animator npcAnim; static float animBack; static string lastAtkState;

        // The shooting animation (muzzle flash, recoil, reload) is played by the Movement FSM, which is muted while he rides.
        // Play the same Animator state ("attack 2") on every shot, then go back to idle.
        static List<GameObject> flashObjs; static float flashOff;

        /// <summary>his parts, once per ride: the muzzle flash objects, and the bones of the arm that holds the gun (for the recoil)</summary>
        static void ScanParts()
        {
            try
            {
                flashObjs = new List<GameObject>();
                var names = new System.Text.StringBuilder();
                foreach (Transform t in npc.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (names.Length < 1500) names.Append(t.name).Append(t.gameObject.activeSelf ? "" : "(off)").Append(", ");
                    // each gun carries a "fire_effect" (off) with the flash parts under it; the shoot clip switches it on for a moment
                    bool flashName = n.StartsWith("fire_effect") || n.Contains("muzzle") || n.Contains("flash");
                    bool parentToo = t.parent != null && t.parent.name.ToLowerInvariant().StartsWith("fire_effect");
                    if (flashName && !parentToo) flashObjs.Add(t.gameObject);
                }
                Plugin.Info("Hire: " + displayName + " parts: " + names);
                Plugin.Info("Hire: " + displayName + " muzzle flash objects found: " + flashObjs.Count);
                Animator a0 = npc.GetComponent<Animator>() ?? npc.GetComponentInChildren<Animator>(true);
                if (a0 != null && a0.runtimeAnimatorController != null)
                {
                    var cn = new System.Text.StringBuilder();
                    foreach (AnimationClip c in a0.runtimeAnimatorController.animationClips) cn.Append(c.name).Append(", ");
                    Plugin.Info("Hire: " + displayName + " animation clips: " + cn);
                }
                gunArm = null; gunHand = null;
                foreach (GameObject g in flashObjs)
                {
                    if (g == null || g.transform.parent == null || !g.transform.parent.gameObject.activeInHierarchy) continue;
                    for (Transform h = g.transform.parent; h != null && h != npc.transform; h = h.parent)
                        if (h.name.EndsWith("Hand")) { gunHand = h; gunArm = h.parent; break; }
                    if (gunHand != null) break;
                }
                if (gunHand == null)
                {
                    // no flash part (Bolter): take the hand that has a visible weapon model under it
                    foreach (Transform h in npc.GetComponentsInChildren<Transform>(true))
                    {
                        if (!h.name.EndsWith("Hand") || h.parent == null) continue;
                        foreach (Renderer r in h.GetComponentsInChildren<Renderer>(false))
                            if (r != null && r.enabled && r.transform != h) { gunHand = h; gunArm = h.parent; break; }
                        if (gunHand != null) break;
                    }
                }
                chestBone = null; kickBones.Clear();
                foreach (Transform b in npc.GetComponentsInChildren<Transform>(true))
                {
                    if (b.name.EndsWith("Spine2")) chestBone = b;
                    else if (b.name.EndsWith("LeftForeArm") || b.name.EndsWith("RightForeArm")) kickBones.Add(b);
                }
                if (kickBones.Count == 0 && gunArm != null) kickBones.Add(gunArm);
                ownDamage = 0f;
                PlayMakerFSM dr = FindFsm(npc, "Damage Ranged");
                FsmFloat dv = dr != null ? dr.FsmVariables.GetFsmFloat("Damage Value") : null;
                if (dv != null && Mathf.Abs(dv.Value) > 0.1f) ownDamage = Mathf.Abs(dv.Value);
                FindReloadClips();
                Plugin.Info("Hire: " + displayName + " holds his gun in " + (gunHand != null ? gunHand.name : "(no hand found)") + ", shot damage " + (ownDamage > 0f ? ownDamage.ToString("0") + " (his own)" : shotDamage.Value.ToString("0") + " (ShotDamage)") + ".");
            }
            catch (Exception e) { Plugin.LogOnce("HireScanParts", e); if (flashObjs == null) flashObjs = new List<GameObject>(); }
        }

        static Transform gunArm, gunHand, chestBone; static readonly List<Transform> kickBones = new List<Transform>(); static float recoilAt = -10f, ownDamage;

        /// <summary>a short kick of the gun arm, drawn after Apocapatrol's pose (onBeforeRender comes after every LateUpdate)</summary>
        static void Recoil()
        {
            try
            {
                if (npc == null || kickBones.Count == 0 || recoilDeg == null || recoilDeg.Value <= 0f) return;
                float t = Time.time - recoilAt;
                const float up = 0.04f, dur = 0.2f;
                if (t < 0f || t > dur) return;
                float k = t < up ? t / up : 1f - (t - up) / (dur - up);
                Vector3 axis = npc.transform.right;
                float a = recoilDeg.Value * k;
                // the chest rocks back a little and both forearms kick up, so the gun and both hands move together
                if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(-a * 0.25f, axis) * chestBone.rotation;
                foreach (Transform b in kickBones) if (b != null) b.rotation = Quaternion.AngleAxis(-a, axis) * b.rotation;
            }
            catch (Exception e) { Plugin.LogOnce("HireRecoil", e); }
        }

        /// <summary>One shot from the car: muzzle flash parts flicked, recoil started and (ShotAnimation on) the Movement FSM's shooting state played when the ModAPI body does not drive him.</summary>
        static void PlayShotAnim()
        {
            try
            {
                if (flashObjs == null) ScanParts();
                foreach (GameObject g in flashObjs)
                    if (g != null && g.transform.parent != null && g.transform.parent.gameObject.activeInHierarchy) { g.SetActive(false); g.SetActive(true); }   // only the gun he holds
                flashOff = Time.time + 0.1f;
                recoilAt = Time.time;
                if (shotAnim.Value)
                {
                    if (npcAnim == null) npcAnim = npc.GetComponent<Animator>() ?? npc.GetComponentInChildren<Animator>(true);
                    if (npcAnim != null) { if (ModAPI.Find(npcAnim) == null) npcAnim.Play("attack 2", 0, 0f); animBack = Time.time + 0.9f; }   // (B05) native clip only while the ModAPI body does not drive him (ex QuestFollower.Play)
                }
            }
            catch (Exception e) { Plugin.LogOnce("HireShotAnim", e); }
        }

        /// <summary>Apocapatrol's arm poses (aiming / shooting) exist only for its own humans: lend a follower a copy of one of them</summary>
        static string ArmTemplate(string name)
        {
            string like = "Lugnut";
            foreach (string part in (armPoseLike.Value ?? "").Split(','))
            {
                string p = part.Trim(); int eq = p.IndexOf('=');
                if (eq < 0) { if (p.Length > 0) like = p; }
                else if (string.Equals(p.Substring(0, eq).Trim(), name, StringComparison.OrdinalIgnoreCase)) return p.Substring(eq + 1).Trim();
            }
            return like;
        }
    }
}
