using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Hired follower, Gunplay part (Wave A, from Quests/Hire.cs; B09): the follower's gun stats, magazine and reload timing, borrowed shot/reload sounds and the shots fired from the car.</summary>
    internal static partial class Hire
    {
        static float nextShot, realFireAt = -100f; static int shotsFired;

        static int magLeft = -1; internal static int magSize; internal static float reloadUntil; static float reloadSecs = 3f;

        /// <summary>Value of the first "name=value" entry whose name occurs in <paramref name="name"/> (config override lists), else the fallback.</summary>
        static float NamedValue(string list, string name, float fallback)
        {
            foreach (string part in (list ?? "").Split(','))
            {
                string[] kv = part.Split('=');
                float v;
                if (kv.Length == 2 && name.IndexOf(kv[0].Trim(), StringComparison.OrdinalIgnoreCase) >= 0 && kv[0].Trim().Length > 0
                    && float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            }
            return fallback;
        }

        /// <summary>The follower is reloading.</summary>
        static bool Reloading { get { return Time.time < reloadUntil; } }

        static readonly List<AudioClip> reloadClips = new List<AudioClip>();

        static readonly List<KeyValuePair<float, AudioClip>> soundQueue = new List<KeyValuePair<float, AudioClip>>();

        // The player's own copy of each gun (under the camera's WeaponsArm) has an AnimSound FSM with its reload sounds
        // (mag out, mag in, slide...). Find the one that matches the gun in his hand and borrow its clips.
        /// <summary>Finds the player's copy of the follower's gun on the weapon rack: its stats, shot sound (when needed) and reload sound clips.</summary>
        static void FindReloadClips()
        {
            try
            {
                Transform rack = WeaponRack();
                if (rack == null) { if (Log.L1) GunplayLog.Main("Hire: the player's weapon rack was not found; " + displayName + " keeps the gun data he had."); return; }
                reloadClips.Clear();
                var held = new List<string>();
                if (gunHand != null)
                    for (int i = 0; i < gunHand.childCount; i++)
                    {
                        Transform c = gunHand.GetChild(i);
                        if (c.gameObject.activeInHierarchy && c.GetComponentInChildren<Renderer>() != null) held.Add(c.name.ToLowerInvariant().Replace("_model", ""));
                    }
                Transform best = null; bool viaFallback = false;
                for (int i = 0; i < rack.childCount; i++)
                {
                    Transform w = rack.GetChild(i);
                    string wn = w.name.ToLowerInvariant();
                    foreach (string h in held)
                        if ((h == wn || h.Contains(wn)) && (best == null || wn.Length > best.name.Length)) best = w;
                }
                if (best == null)
                {
                    string want = "";
                    foreach (string part in (reloadSoundFrom.Value ?? "").Split(','))
                    {
                        string[] kv = part.Split('=');
                        if (kv.Length == 2 && kv[0].Trim().Length > 0 && Game.BaseName(npc.name).IndexOf(kv[0].Trim(), StringComparison.OrdinalIgnoreCase) >= 0) want = kv[1].Trim();
                    }
                    if (want.Length > 0) { best = rack.Find(want); viaFallback = best != null; }
                }
                if (best == null) { if (Log.L1) GunplayLog.Main("Hire: " + displayName + "'s gun (" + string.Join(", ", held.ToArray()) + ") has no player version; no reload sounds (set ReloadSoundFrom)."); return; }
                ReadGunStats(best, rack);
                if (viaFallback || FindFsm(npc, "WeaponType") == null) BorrowShotSound(best);     // Bolter has no WeaponType: the game never set his gun's sound
                PlayMakerFSM snd = null;
                foreach (PlayMakerFSM f in best.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "AnimSound") { snd = f; break; }
                if (snd == null) { if (Log.L1) GunplayLog.Main("Hire: " + best.name + " has no AnimSound; no reload sounds."); return; }
                var states = new List<FsmState>(snd.FsmStates);
                states.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
                foreach (FsmState st in states)
                {
                    if (!st.Name.StartsWith("sound")) continue;
                    foreach (FsmStateAction a in st.Actions)
                    {
                        if (a == null) continue;
                        AudioClip clip = null;
                        foreach (FieldInfo fi in a.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                        {
                            object v = fi.GetValue(a);
                            if (v is AudioClip) clip = (AudioClip)v;
                            else if (v is FsmObject && ((FsmObject)v).Value is AudioClip) clip = (AudioClip)((FsmObject)v).Value;
                            if (clip != null) break;
                        }
                        if (clip != null) { reloadClips.Add(clip); break; }
                    }
                }
                var names = new List<string>(); foreach (AudioClip c in reloadClips) names.Add(c.name);
                if (Log.L1) GunplayLog.Main("Hire: " + displayName + " reloads with the sounds of " + best.name + ": " + string.Join(", ", names.ToArray())
                    + (gunPerRound && reloadClips.Count > 0 ? " (" + reloadClips[InsertClip()].name + " for every round)" : "") + ".");
            }
            catch (Exception e) { Plugin.LogOnce("HireReloadClips", e); }
        }

        // Magazine size and reload time from the player's copy of the gun:
        //  - Reload FSM, state checkAmmo: the first IntCompare tests the rounds against a fixed number (29 for a 30-round AKM,
        //    4 for a 5-shot revolver; a crossbow compares the other way round, 1 against the bolts it has)
        //  - a Reload FSM with "checkAmmoInStore" loads one round at a time (revolver, shotguns); otherwise one magazine
        //  - the time is the length of the reload clips in the gun's Animator
        /// <summary>Magazine size (checkAmmo IntCompare) and reload time (reload clip lengths) from the player's copy of the gun.</summary>
        static void ReadGunStats(Transform gun, Transform rack)
        {
            try
            {
                gunPerRound = false;
                PlayMakerFSM rl = null;
                foreach (PlayMakerFSM f in gun.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Reload") { rl = f; break; }
                if (rl == null) { if (Log.L1) GunplayLog.Main("Hire: " + gun.name + " has no Reload FSM; magazine and reload time not known."); return; }
                int mag = 0; bool perRound = false;
                foreach (FsmState st in rl.FsmStates)
                {
                    if (st.Name == "checkAmmoInStore") perRound = true;
                    if (st.Name != "checkAmmo" || mag > 0) continue;
                    foreach (FsmStateAction a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "IntCompare") continue;
                        FsmInt i1 = a.GetType().GetField("integer1").GetValue(a) as FsmInt;
                        FsmInt i2 = a.GetType().GetField("integer2").GetValue(a) as FsmInt;
                        if (i1 == null || i2 == null) continue;
                        bool v1 = !string.IsNullOrEmpty(i1.Name), v2 = !string.IsNullOrEmpty(i2.Name);   // a named value is a variable
                        if (!v2 && v1) mag = i2.Value + 1;                          // rounds > 29: full
                        else if (!v1 && v2) mag = Math.Max(1, i1.Value);            // crossbow: 1 vs bolts
                        else mag = Math.Max(i1.Value, i2.Value) + 1;
                        break;
                    }
                }
                // reload clip lengths: the gun's own Animator, else the arms' Animator clips that name the gun
                var lens = new List<float>(); var clipNames = new List<string>();
                var anims = new List<Animator>();
                anims.AddRange(gun.GetComponentsInChildren<Animator>(true));
                bool own = anims.Count > 0;
                if (!own) { Animator ra = rack.GetComponent<Animator>() ?? (rack.parent != null ? rack.parent.GetComponent<Animator>() : null); if (ra != null) anims.Add(ra); }
                foreach (Animator an in anims)
                {
                    if (an == null || an.runtimeAnimatorController == null) continue;
                    foreach (AnimationClip c in an.runtimeAnimatorController.animationClips)
                    {
                        if (c == null) continue;
                        string cn = c.name.ToLowerInvariant();
                        if (!cn.Contains("reload")) continue;
                        if (!own && !cn.Contains(gun.name.ToLowerInvariant())) continue;
                        if (clipNames.Contains(c.name)) continue;
                        clipNames.Add(c.name); lens.Add(c.length);
                    }
                }
                if (lens.Count == 0)
                {
                    var all = new List<string>();
                    foreach (Animator an in anims) if (an != null && an.runtimeAnimatorController != null) foreach (AnimationClip c in an.runtimeAnimatorController.animationClips) if (c != null) all.Add(c.name + " " + c.length.ToString("0.0"));
                    if (Log.L1) GunplayLog.Main("Hire: " + gun.name + " has no clip named reload; its clips: " + string.Join(", ", all.ToArray()));
                }
                float secs = -1f;
                if (lens.Count > 0)
                {
                    float sum = 0f, min = float.MaxValue, max = 0f;
                    foreach (float l in lens) { sum += l; min = Mathf.Min(min, l); max = Mathf.Max(max, l); }
                    secs = perRound ? sum + min * Mathf.Max(0, mag - 1) : max;          // one round at a time: every round goes in
                    secs = Mathf.Clamp(secs, 0.5f, 10f);
                }
                gunPerRound = perRound;
                if (mag > 0) magSize = mag;
                if (secs > 0f) reloadSecs = secs;
                if (Log.L1) GunplayLog.Main("Hire: " + gun.name + ": " + (mag > 0 ? mag + " rounds" : "rounds unknown") + (perRound ? ", loaded one at a time" : "") + ", reload clips " + (clipNames.Count > 0 ? string.Join(", ", clipNames.ToArray()) : "none") + (secs > 0f ? " = " + secs.ToString("0.0") + " s" : "") + ".");
            }
            catch (Exception e) { Plugin.LogOnce("HireGunStats", e); }
        }

        static bool gunPerRound;

        // A magazine gun plays its sounds once, spread over the reload. A gun loaded one round at a time (revolver, shotguns)
        // plays the sounds before the "insert" sound once, the insert sound for every round, then the rest once.
        // his gun is not one the game gave him a sound for (Bolter's crossbow plays a gunshot): take the shot sound of the
        // player's version, which sits on the effect its Attack FSM spawns when it fires (GunShot_crossbow_Effect)
        /// <summary>Gives the follower the shot sound of the player's copy of his gun (its fire effect's AudioSource clip).</summary>
        static void BorrowShotSound(Transform gun)
        {
            try
            {
                AudioClip clip = null;
                foreach (PlayMakerFSM f in gun.GetComponents<PlayMakerFSM>())
                {
                    if (f == null || f.FsmName != "Attack") continue;
                    foreach (FsmState st in f.FsmStates)
                    {
                        if (st.Name != "fire" && st.Name != "shoot") continue;
                        foreach (FsmStateAction a in st.Actions)
                        {
                            if (a == null || a.GetType().Name != "CreateObject") continue;
                            FieldInfo fi = a.GetType().GetField("gameObject");
                            FsmGameObject go = fi != null ? fi.GetValue(a) as FsmGameObject : null;
                            AudioSource src = go != null && go.Value != null ? go.Value.GetComponentInChildren<AudioSource>(true) : null;
                            if (src != null && src.clip != null) { clip = src.clip; break; }
                        }
                        if (clip != null) break;
                    }
                }
                if (clip == null) { if (Log.L1) GunplayLog.Main("Hire: no shot sound found on " + gun.name + "; " + displayName + " keeps his own."); return; }
                PlayMakerFSM snd = FindFsm(npc, "Sound2");
                if (snd == null) return;
                FsmObject gs = snd.FsmVariables.GetFsmObject("GunSound");
                if (gs != null) gs.Value = clip;
                foreach (FsmState st in snd.FsmStates)
                    foreach (FsmStateAction a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "SetAudioClip") continue;
                        FieldInfo fi = a.GetType().GetField("audioClip");
                        FsmObject v = fi != null ? fi.GetValue(a) as FsmObject : null;
                        if (v != null) v.Value = clip;
                    }
                if (Log.L1) GunplayLog.Main("Hire: " + displayName + " shoots with the sound of " + gun.name + " (" + clip.name + ").");
            }
            catch (Exception e) { Plugin.LogOnce("HireShotSound", e); }
        }

        static readonly Dictionary<int, PlayMakerFSM[]> fsmSets = new Dictionary<int, PlayMakerFSM[]>();
        static readonly Dictionary<int, GameObject> fsmOwners = new Dictionary<int, GameObject>();

        /// <summary>The follower's PlayMaker FSMs, cached per object (same rule as the former QuestWorld.Fsms: refreshed when the id is reused).</summary>
        static PlayMakerFSM[] FsmsOf(GameObject gameObject)
        {
            if (gameObject == null) return new PlayMakerFSM[0];
            int id = gameObject.GetInstanceID(); PlayMakerFSM[] result; GameObject owner;
            if (fsmOwners.TryGetValue(id, out owner) && owner == gameObject && fsmSets.TryGetValue(id, out result)) return result;
            if (fsmSets.Count > 64) { fsmSets.Clear(); fsmOwners.Clear(); }
            result = gameObject.GetComponents<PlayMakerFSM>(); fsmSets[id] = result; fsmOwners[id] = gameObject; return result;
        }

        static Transform rackCache;

        // the player's own guns: Camera/WeaponsArm/Parent. Found from the camera first (the player's root can be the car or a big
        // scene object, too large to search), then from the player, and kept once found.
        /// <summary>The player's Camera/WeaponsArm/Parent (cached once found).</summary>
        static Transform WeaponRack()
        {
            if (rackCache != null) return rackCache;
            Transform arm = null;
            Camera cam = GameRefs.MainCamera;
            if (cam != null) arm = FindDeep(cam.transform, "WeaponsArm");
            if (arm == null) { GameObject g = GameRefs.PlayerCameraHolder != null ? GameRefs.PlayerCameraHolder : null; if (g != null) arm = g.transform; }
            if (arm == null) { Transform pl = Game.Player(); if (pl != null) arm = FindDeep(pl, "WeaponsArm") ?? FindDeep(pl.root, "WeaponsArm"); }
            Transform rack = arm != null ? (arm.Find("Parent") ?? FindDeep(arm, "Parent")) : null;
            if (rack != null) rackCache = rack;
            return rack;
        }

        /// <summary>Spreads the reload sounds over the reload (one-round-at-a-time guns repeat the insert sound per round).</summary>
        static void ScheduleReloadSounds()
        {
            soundQueue.Clear();
            int n = reloadClips.Count;
            if (n == 0) return;
            float start = Time.time + Mathf.Min(0.25f, reloadSecs * 0.1f), end = reloadUntil - Mathf.Min(0.35f, reloadSecs * 0.15f);
            if (!gunPerRound || magSize <= 1)
            {
                for (int i = 0; i < n; i++)
                    soundQueue.Add(new KeyValuePair<float, AudioClip>(n == 1 ? start : Mathf.Lerp(start, end, i / (float)(n - 1)), reloadClips[i]));
                return;
            }
            int ins = InsertClip();
            var seq = new List<AudioClip>();
            for (int i = 0; i < ins; i++) seq.Add(reloadClips[i]);
            for (int r = 0; r < magSize; r++) seq.Add(reloadClips[ins]);
            for (int i = ins + 1; i < n; i++) seq.Add(reloadClips[i]);
            for (int i = 0; i < seq.Count; i++)
                soundQueue.Add(new KeyValuePair<float, AudioClip>(seq.Count == 1 ? start : Mathf.Lerp(start, end, i / (float)(seq.Count - 1)), seq[i]));
        }

        // which of the gun's sounds is the round going in: by name if one says so, else the middle one
        /// <summary>Index of the "round goes in" sound by name, else the middle one.</summary>
        static int InsertClip()
        {
            string[] keys = { "insert", "ammo_in", "bullet", "round", "load", "mag_in", "_in", "shell" };
            foreach (string k in keys)
                for (int i = 0; i < reloadClips.Count; i++)
                {
                    string cn = reloadClips[i] != null ? reloadClips[i].name.ToLowerInvariant() : "";
                    if (cn.Contains(k) && !cn.Contains("empty")) return i;      // an empty shell is the one coming out
                }
            return reloadClips.Count >= 3 ? reloadClips.Count / 2 : reloadClips.Count - 1;
        }

        /// <summary>Plays the queued reload sounds whose time has come (on his Sound2 source).</summary>
        static void PlayDueSounds()
        {
            if (soundQueue.Count == 0 || npc == null) return;
            for (int i = soundQueue.Count - 1; i >= 0; i--)
            {
                if (Time.time < soundQueue[i].Key) continue;
                AudioClip clip = soundQueue[i].Value;
                soundQueue.RemoveAt(i);
                if (clip == null) continue;
                try
                {
                    Transform s2 = FindDeep(npc.transform, "Sound2");
                    AudioSource src = s2 != null ? s2.GetComponent<AudioSource>() : null;
                    if (src != null) src.PlayOneShot(clip, 0.9f);
                    else AudioSource.PlayClipAtPoint(clip, npc.transform.position, 0.9f);
                }
                catch (Exception e) { Plugin.LogOnce("HireReloadSound", e); }
            }
        }

        // one shot gone, real or backup; an empty magazine means a pause to reload
        /// <summary>One shot fired: magazine count, reload start with sounds, NPC shot pacing.</summary>
        static void CountShot()
        {
            if (magSize > 0)
            {
                if (magLeft < 0) magLeft = magSize;
                if (--magLeft <= 0)
                {
                    magLeft = magSize; reloadUntil = Time.time + reloadSecs; ScheduleReloadSounds();
                    if (Log.L2) GunplayLog.Deep("Hire: " + displayName + " reloads (" + reloadSecs.ToString("0.#") + " s).");
                }
            }
            if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Shot();   // refs-ok: REFACTOR-REQUEST B09-4 (follower AI shot pacing lives in NPCAI QuestFollower)
        }

        // while reloading, Apocapatrol's gunner is told it cannot fire, so his own Attack FSM stays off and the arms come down
        /// <summary>While reloading in a car the gunner gets no target, so the arms come down.</summary>
        static void HoldFire() { if (mounted) FeedTarget(); }

        // The follower's own sensors only look for the player, so the Attack/Damage Ranged FSMs never get a hit on one of your enemies.
        // While the gunner says it can fire, the mod lowers the target's Health the way the game's own Damage Ranged does (Bodypart -> Health).
        /// <summary>Backup shots while his own FSM cannot hit (seated): lowers the target's Health like Damage Ranged, at the configured pace.</summary>
        static void FireShots()
        {
            if (npc == null || guardComp == null || target == null || !Alive(target) || shotDamage.Value <= 0f || Reloading) return;
            float now = Time.time;
            if (now < nextShot) return;
            if (!ClearShot()) return;                            // a wall, hull or rock between them: no backup shot through it
            if (realFire.Value)
            {
                foreach (PlayMakerFSM f in FsmsOf(npc))
                {
                    if (f == null || !f.enabled) continue;
                    if ((f.FsmName == "Attack" && f.ActiveStateName == "attack_ranged") || (f.FsmName == "Damage Ranged" && f.ActiveStateName == "attack")) realFireAt = now;
                }
                if (now - realFireAt < 4f) return;                // its own shooting works; the backup stays out of it
            }
            try
            {
                bool can;
                can = Services.FollowerVehicles != null && Services.FollowerVehicles.CanFire(npc);   // the gunner did not take him as ranged: the mod judges range itself
                if (!can) return;
                nextShot = now + Mathf.Max(0.2f, shotInterval.Value) * UnityEngine.Random.Range(0.8f, 1.2f);
                PlayMakerFSM hf = FindFsm(target, "Health");
                FsmFloat hp = hf != null ? hf.FsmVariables.GetFsmFloat("Health") : null;
                if (hp == null) { Plugin.LogOnce("HireShotNoHealth", new Exception("target " + target.name + " has no Health variable")); return; }
                hp.Value -= ownDamage > 0f ? ownDamage : shotDamage.Value;     // his own weapon's damage when it can be read (Bolter hits harder)
                shotsFired++;
                if (shotsFired <= 6 && Log.L2) GunplayLog.Deep("Hire: " + displayName + " shoots " + target.name + " (health now " + hp.Value.ToString("0") + ").");
                PlayShotAnim();
                CountShot();
                PlayMakerFSM snd = FindFsm(npc, "Sound2");
                if (snd != null) { selfShotAt = Time.time; try { snd.Fsm.SetState("playAudio"); } catch (Exception) { } }
            }
            catch (Exception e) { Plugin.LogOnce("HireShoot", e); }
        }

        /// <summary>Watches his own Attack FSM: real shots count toward the magazine and show the shot animation/flash.</summary>
        static void WatchShots()
        {
            if (npc == null) return;
            try
            {
                string st = null;
                foreach (PlayMakerFSM f in FsmsOf(npc))
                    if (f != null && f.enabled && f.FsmName == "Attack") { st = f.ActiveStateName; break; }
                if (st == "attack_ranged" && lastAtkState != "attack_ranged") realFireAt = Time.time;
                if (RealShotFired()) { realFireAt = Time.time; PlayShotAnim(); CountShot(); }
                lastAtkState = st;
                if (flashOff > 0f && Time.time > flashOff) { flashOff = 0f; if (flashObjs != null) foreach (GameObject g in flashObjs) if (g != null) g.SetActive(false); }
                if (animBack > 0f && Time.time > animBack && npcAnim != null) { animBack = 0f; if (ModApi.Get<IQuestFollowerBody>() != null) ModApi.Get<IQuestFollowerBody>().Play(npcAnim, "idle", 0, 0f); else npcAnim.Play("idle", 0, 0f); }   // refs-ok: REFACTOR-REQUEST B09-4
            }
            catch (Exception e) { Plugin.LogOnce("HireWatchShots", e); }
        }

        // his gun: flash parts, gun hand, damage, magazine, reload time and sounds (car and foot alike)
        /// <summary>Reads his gun (parts, damage, magazine, reload time, sounds) with config overrides.</summary>
        static void GunSetup()
        {
            int prevMag = magSize; float prevReload = reloadSecs;
            magSize = 0; reloadSecs = 3f;
            ScanParts();
            if (magSize <= 0 && prevMag > 0) { magSize = prevMag; reloadSecs = prevReload; }   // could not read his gun this time: keep what he had
            string bn = Game.BaseName(npc.name);
            float mo = NamedValue(magSizes.Value, bn, -1f), ro = NamedValue(reloadTimes.Value, bn, -1f);
            if (mo >= 0f) magSize = Mathf.RoundToInt(mo);
            if (ro > 0f) reloadSecs = Mathf.Max(0.2f, ro);
            if (magLeft < 0 || magLeft > magSize) magLeft = magSize;
            if (Log.L1) GunplayLog.Main("Hire: " + displayName + " magazine " + (magSize > 0 ? magSize.ToString() : "none (never reloads)") + ", reload " + reloadSecs.ToString("0.0") + " s" + (mo >= 0f || ro > 0f ? " (override)" : "") + ".");
        }
    }
}
