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
    /// <summary>Hired follower, NPCAI part (Wave A, from Quests/Hire.cs; B07 follower AI behind IFollowers / INpcApi): waking and calming the NPC brain, following and catching up, threat picking, foot shooting and magazine, rescue/teleport when lost, ranged checks.</summary>
    internal static partial class Hire
    {
        static readonly List<KeyValuePair<PlayMakerFSM, bool>> fsmSnap = new List<KeyValuePair<PlayMakerFSM, bool>>();

        static float settleUntil;

        // After the car: Apocapatrol's crew code switches off his walking AI (Movement, Rotate, Unstuck...) and may do it once more
        // as he gets out. Without Movement he cannot follow, and Unstuck makes him hop on the spot. For a couple of seconds after
        // getting out, everything but the combat FSMs (which Detection runs) is put back the way it was when he was hired.
        static void WakeBrain(bool log)
        {
            if (npc == null) return;
            try
            {
                int fixedCount = 0;
                foreach (var kv in fsmSnap)
                {
                    PlayMakerFSM f = kv.Key;
                    if (f == null) continue;
                    string n = f.FsmName;
                    if (n == "Attack" || n == "RangedAttackWait" || n == "Damage Ranged" || n == "Damage") continue;
                    bool want = kv.Value || n == "Detection" || n == "Movement";
                    if (f.enabled != want) { f.enabled = want; fixedCount++; }
                }
                if (fixedCount > 0 && (log || !wakeLogged)) { wakeLogged = true; Plugin.Info("Hire: " + displayName + "'s walking AI switched back on (" + fixedCount + " FSMs)."); }
            }
            catch (Exception e) { Plugin.LogOnce("HireWakeBrain", e); }
        }

        static bool wakeLogged;

        // After a save is loaded the game rebuilds the follower from its own save: he can come back marked hostile to the player or with the
        // player as the thing he has detected, and then he shoots you. Hired help is always friendly, so the hostility switch is put back
        // and a player target is cleared (on finding him again, and every couple of seconds after that, quickly for the first 20 s).
        static float nextCalm, calmFast;

        static void Calm(Transform pl, bool log)
        {
            if (npc == null) return;
            try
            {
                if (log) calmFast = Time.time + 20f;
                bool did = false;
                if (Game.IsPlayerEnemy(npc) && Game.SetPlayerEnemy(npc, false)) did = true;
                foreach (PlayMakerFSM f in NpcWorld.Fsms(npc))
                {
                    if (f == null || (f.FsmName != "Detection" && f.FsmName != "Attack")) continue;
                    FsmGameObject v = f.FsmVariables.GetFsmGameObject("detectedObj");
                    if (v == null || v.Value == null || pl == null) continue;
                    if (v.Value == pl.gameObject || v.Value.transform.IsChildOf(pl) || pl.IsChildOf(v.Value.transform))
                    {
                        v.Value = null;
                        if (f.FsmName == "Detection") { try { f.Fsm.SetState("notDetected"); } catch (Exception) { } }
                        did = true;
                    }
                }
                if (did) Plugin.Info("Hire: " + displayName + " was hostile to you (after a load); made friendly again.");
            }
            catch (Exception e) { Plugin.LogOnce("HireCalm", e); }
        }

        static void ReleaseNpc()
        {
            if (npc == null) return;
            try
            {
                if (moveFsm != null && moveFsm.ActiveStateName == "Run") moveFsm.Fsm.SetState("Idle");
                SetExcluded(false);
            }
            catch (Exception) { }
            RestoreBrain();
        }

        // Put his own brain back the way it was when he was hired: the car, Apocapatrol's gunner and the target the mod fed him
        // can leave Detection or other FSMs switched off, and then he just stands there after being let go.
        static void RestoreBrain()
        {
            if (npc == null) return;
            try
            {
                var release = ModApi.Get<IFollowerRelease>();
                if (release != null) release.Release(npc);      // Patrols removes the crew / seat components it added (A8-8)
                else foreach (Component c in npc.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string tn = c.GetType().FullName;
                    if (tn.EndsWith(".Patrols.PassengerGuard", StringComparison.Ordinal) || tn.EndsWith(".Patrols.Pose", StringComparison.Ordinal)) UnityEngine.Object.Destroy(c);   // refs-ok: REFACTOR-REQUEST B07-14 (fallback until Patrols provides IFollowerRelease)
                }
                foreach (var kv in fsmSnap) if (kv.Key != null) kv.Key.enabled = kv.Value;
                foreach (PlayMakerFSM f in NpcWorld.Fsms(npc))
                {
                    if (f == null) continue;
                    if (f.FsmName == "Detection" || f.FsmName == "Attack")
                    {
                        FsmGameObject v = f.FsmVariables.GetFsmGameObject("detectedObj");
                        if (v != null) v.Value = null;
                    }
                    if (f.FsmName == "Detection")
                    {
                        f.enabled = true;
                        try { f.Fsm.SetState("notDetected"); } catch (Exception) { }
                    }
                    if (f.FsmName == "Movement" || f.FsmName == "Rotate" || f.FsmName == "Unstuck") f.enabled = true;
                }
                Plugin.Info("Hire: " + displayName + "'s own AI is back on.");
            }
            catch (Exception e) { Plugin.LogOnce("HireRestoreBrain", e); }
            fsmSnap.Clear();
        }

        // the loaded NPC with this object name that is closest to the player (names can repeat for NPCs the game did not number)
        static Transform NearestUncached(string name, Vector3 from)
        {
            Transform best = null;
            float bd = float.MaxValue;
            var all = NpcWorld.People;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "Health") continue;
                GameObject go = f.gameObject;
                if (go == null || go.name != name) continue;
                float d = Game.HDist(go.transform.position, from);
                if (d < bd) { bd = d; best = go.transform; }
            }
            return best;
        }

        // ---- the player's health: something hurt them --------------------------------------------------
        static void WatchPlayerHealth(Transform pl)
        {
            if (playerHealthVar == null)
            {
                if (Time.unscaledTime < nextHealthFind) return;
                nextHealthFind = Time.unscaledTime + 2f;
                PlayMakerFSM[] all = pl.GetComponentsInChildren<PlayMakerFSM>(true);
                if (all.Length == 0 || FindFsm(pl.gameObject, "Health") == null) all = pl.GetComponentsInParent<PlayMakerFSM>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || all[i].FsmName != "Health") continue;
                    FsmFloat v = Game.FindFloat(all[i], "Health");
                    if (v != null) { playerHealth = all[i]; playerHealthVar = v; break; }
                }
                if (playerHealthVar == null) return;
            }
            float h = playerHealthVar.Value;
            if (lastPlayerHealth >= 0f && h < lastPlayerHealth - 0.001f) hurtUntil = Time.time + 8f;
            lastPlayerHealth = h;
        }

        // ---- fighting ------------------------------------------------------------------------------------------
        static bool Alive(GameObject go)
        {
            if (go == null) return false;
            PlayMakerFSM h = FindFsm(go, "Health");
            return h == null || h.ActiveStateName == "health";
        }

        // an enemy of the player: the game's own switch says so, or it is a creature with no such switch
        static bool IsCritter(GameObject go)
        {
            string n = Game.BaseName(go.name);
            foreach (string part in (ignoreNames.Value ?? "").Split(','))
            {
                string q = part.Trim();
                if (q.Length > 0 && n.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        static float footSince = -1f, footShotAt; static GameObject ignoreTarget; static float ignoreUntil;

        // On foot, Follow waits while he has a target (NPCAI steers the fight). A target he cannot get at (in a car, out of sight)
        // would keep him standing there for good, so after 8 s without attacking he gives up on it for a while and follows again.
        static string lastFootAtk; static bool footHeld;

        static string lastSnd, lastDr; static float selfShotAt = -10f, lastRealShot = -10f;

        // One real shot of his own: the gunshot (Sound2 starting to play) or a hit/shot state of his Damage Ranged FSM.
        // Both usually come with the same shot, so signals closer than 40 ms count once. The mod's own shots are not counted here.
        static bool RealShotFired()
        {
            bool fired = false;
            try
            {
                PlayMakerFSM snd = FindFsm(npc, "Sound2"), dr = FindFsm(npc, "Damage Ranged");
                string ss = snd != null && snd.enabled ? snd.ActiveStateName : null;
                string ds = dr != null && dr.enabled ? dr.ActiveStateName : null;
                if (ss == "playAudio" && lastSnd != "playAudio" && Time.time - selfShotAt > 0.15f) fired = true;
                if ((ds == "attack" || ds == "shoot") && lastDr != ds) fired = true;
                lastSnd = ss; lastDr = ds;
                if (fired && Time.time - lastRealShot < 0.04f) fired = false;
                if (fired) lastRealShot = Time.time;
                if (fired && magSize > 0 && magLeft == 1 && dr != null) dr.enabled = false;     // last round: no second shot sneaks out before the reload starts
            }
            catch (Exception e) { Plugin.LogOnce("HireRealShot", e); }
            return fired;
        }

        // His own shooting on foot counts against the same magazine; while he reloads his Attack FSM is held off,
        // and given back (if Detection still sees an enemy) when the reload is done.
        static void FootMagazine(float now)
        {
            PlayDueSounds();
            try
            {
                PlayMakerFSM atk = null, det = null, dr = null, raw = null;
                foreach (PlayMakerFSM f in NpcWorld.Fsms(npc))
                {
                    if (f == null) continue;
                    if (f.FsmName == "Attack") atk = f; else if (f.FsmName == "Detection") det = f; else if (f.FsmName == "Damage Ranged") dr = f; else if (f.FsmName == "RangedAttackWait") raw = f;
                }
                string st = atk != null && atk.enabled ? atk.ActiveStateName : null;
                if (RealShotFired())
                {
                    if (!footSetup) { footSetup = true; GunSetup(); footShootAnim = ShootAnimName(); }
                    CountShot();
                }
                lastFootAtk = st;
                if (Reloading)
                {
                    if (atk != null && atk.enabled) { atk.enabled = false; footHeld = true; }
                    if (dr != null && dr.enabled) { dr.enabled = false; footHeld = true; }
                }
                else if (footHeld)
                {
                    footHeld = false;
                    if (det != null && det.enabled && det.ActiveStateName == "inLOS")
                    {
                        if (atk != null) atk.enabled = true;
                        if (raw != null) raw.enabled = true;
                    }
                }
            }
            catch (Exception e) { Plugin.LogOnce("HireFootMag", e); }
        }

        static void FootWatch(float now)
        {
            FootMagazine(now);
            if (target == null) { footSince = -1f; EndFootShot(); return; }
            if (!Alive(target)) { target = null; footSince = -1f; EndFootShot(); return; }
            if (footSince < 0f) { footSince = now; footShotAt = now; }
            foreach (PlayMakerFSM f in NpcWorld.Fsms(npc))
            {
                if (f == null || !f.enabled) continue;
                string st = f.ActiveStateName;
                if ((f.FsmName == "Attack" && (st == "attack_ranged" || st == "attack_melee")) || (f.FsmName == "Damage Ranged" && st == "attack")) footShotAt = now;
            }
            // a shooter whose own AI does not get the shot off (the target is in a car, his sensors do not see it...) shoots the way he does from the car
            bool ranged = shootFromCar.Value && IsRangedNpc(npc);
            float d = Vector3.Distance(target.transform.position, npc.transform.position);
            if (ranged && d <= PatrolShootDistance())
            {
                if (now - footShotAt < 3f && !footShooting) return;          // his own fighting works
                FootShoot(now);
                return;
            }
            EndFootShot();
            if (now - footShotAt < 8f) return;
            Plugin.Info("Hire: " + displayName + " cannot get at " + target.name + "; follows you again.");
            ignoreTarget = target; ignoreUntil = now + 20f;
            target = null; engagedUntil = 0f; footSince = -1f;
        }

        static bool footShooting, footSetup; static float footNextShot, footIdleAt; static string footShootAnim;

        static void FootShoot(float now)
        {
            if (!footShooting) { footShooting = true; Plugin.Info("Hire: " + displayName + " shoots at " + target.name + " on foot."); }
            if (!footSetup) { footSetup = true; GunSetup(); footShootAnim = ShootAnimName(); }
            // stand and face the target
            if (moveFsm != null && moveFsm.ActiveStateName == "Run") { try { moveFsm.Fsm.SetState("Idle"); } catch (Exception) { } }
            Vector3 dir = target.transform.position - npc.transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) npc.transform.rotation = Quaternion.Slerp(npc.transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
            PlayDueSounds();
            if (footIdleAt > 0f && now > footIdleAt) { footIdleAt = 0f; if (moveFsm != null) { try { moveFsm.Fsm.SetState("Idle"); } catch (Exception) { } } }
            if (Reloading || now < footNextShot || shotDamage.Value <= 0f) return;
            if (Vector3.Angle(npc.transform.forward, dir) > 25f) return;                 // still turning
            if (!ClearShot()) return;
            footNextShot = now + Mathf.Max(0.2f, shotInterval.Value) * UnityEngine.Random.Range(0.8f, 1.3f);
            try
            {
                PlayMakerFSM hf = FindFsm(target, "Health");
                FsmFloat hp = hf != null ? hf.FsmVariables.GetFsmFloat("Health") : null;
                if (hp == null) return;
                hp.Value -= ownDamage > 0f ? ownDamage : shotDamage.Value;
                shotsFired++;
                if (shotsFired <= 12) Plugin.Info("Hire: " + displayName + " shoots " + target.name + " on foot (health now " + hp.Value.ToString("0") + ").");
                if (npcAnim == null) npcAnim = npc.GetComponent<Animator>() ?? npc.GetComponentInChildren<Animator>(true);
                if (npcAnim != null && !string.IsNullOrEmpty(footShootAnim)) { QuestFollower.Play(npcAnim, footShootAnim, 0, 0f); footIdleAt = now + 0.9f; }
                PlayShotAnim();
                CountShot();
                PlayMakerFSM snd = FindFsm(npc, "Sound2");
                if (snd != null) { selfShotAt = Time.time; try { snd.Fsm.SetState("playAudio"); } catch (Exception) { } }
            }
            catch (Exception e) { Plugin.LogOnce("HireFootShoot", e); }
        }

        static void EndFootShot()
        {
            if (!footShooting) return;
            footShooting = false;
            if (footIdleAt > 0f && moveFsm != null) { try { moveFsm.Fsm.SetState("Idle"); } catch (Exception) { } }
            footIdleAt = 0f;
            if (flashObjs != null) foreach (GameObject g in flashObjs) if (g != null) g.SetActive(false);
        }

        // nothing solid between his chest and the target (the target's own car does not count)
        static bool ClearShot() { return ClearShot(target); }

        static bool ClearShot(GameObject target)
        {
            if (target != null && target == coverTarget && Time.time < coverUntil) return true;      // it hurt you from behind cover: shoot anyway
            try
            {
                Vector3 from = npc.transform.position + Vector3.up * 1.5f, to = target.transform.position + Vector3.up * 1.0f;
                Vector3 d = to - from; float len = d.magnitude;
                if (len < 0.5f) return true;
                Transform troot = target.transform.root;
                foreach (RaycastHit h in NpcWorld.RaycastAll(from, d / len, len))
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    Transform t = h.collider.transform;
                    if (t.IsChildOf(npc.transform) || t.IsChildOf(target.transform)) continue;
                    if (mounted && carObj != null && t.IsChildOf(carObj.transform)) continue;          // his own car
                    // what the target rides in (or sits in) only counts when the hit is at the target itself, not a hull or wall in front of him
                    if (t.IsChildOf(troot) && Vector3.Distance(h.point, to) < 0.7f) continue;
                    Transform pl = Game.Player();
                    if (pl != null && t.IsChildOf(pl.root)) continue;
                    return false;
                }
            }
            catch (Exception) { }
            return true;
        }

        static bool Hostile(GameObject go)
        {
            PlayMakerFSM[] fsms = NpcWorld.Fsms(go);
            for (int i = 0; i < fsms.Length; i++)
                if (fsms[i] != null && fsms[i].FsmName == "PlayerIsEnemy") return fsms[i].ActiveStateName == "PlayerEnemy";
            return true;
        }

        static void Inform(GameObject enemy)
        {
            if (npc == null || enemy == null || Services.Npc == null) return;
            Services.Npc.Inform(npc, enemy); target = enemy; lastInform = Time.time; engagedUntil = Time.time + 25f;
        }

        // Sends the follower after whatever hurt you (the closest living enemy near you) and keeps it on a target while one is close.
        static float hiddenSince = -1f, coverUntil; static GameObject coverTarget;

        static void Threats(Transform pl, float dist)
        {
            float now = Time.time;
            // still busy with a target?
            if (target != null && !Alive(target)) target = null;
            if (target != null && (now > engagedUntil || Game.HDist(target.transform.position, npc.transform.position) > 80f)) target = null;
            if (target != null && mounted && guardComp != null && Game.HDist(target.transform.position, pl.position) > PatrolShootDistance() * 1.25f) target = null;
            if (target != null && leash.Value > 0f && Game.HDist(target.transform.position, pl.position) > leash.Value) { target = null; if (!mounted && moveFsm != null && moveFsm.ActiveStateName != "Run") { try { moveFsm.Fsm.SetState("Run"); } catch (Exception) { } } }
            if (target != null && mounted && shootFromCar.Value && IsRangedNpc(npc))
            {
                if (ClearShot(target)) hiddenSince = -1f;
                else if (hiddenSince < 0f) hiddenSince = now;
                else if (now - hiddenSince > 1.2f) { target = null; hiddenSince = -1f; }          // behind a wall for a moment: stop aiming at it
            }
            if (target != null && now - lastInform > 4f) Inform(target);          // keep it on the job

            if (now < nextThreat) return;
            nextThreat = now + 0.7f;

            bool hurt = now < hurtUntil;
            if (target != null && !hurt) return;
            float radius = hurt ? 55f : 10f;
            if (mounted && guardComp != null) radius = PatrolShootDistance();          // in the car: only what Apocapatrol's gunner could shoot at
            List<EnemyInfo> foes = Game.FindEnemies(pl.position, radius);   // refs-ok: REFACTOR-REQUEST B07-14 (part of a Quests type; B18 Hire/Takeover split)
            bool shooter = shootFromCar.Value && IsRangedNpc(npc);
            GameObject best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < foes.Count; i++)
            {
                GameObject g = foes[i].Go;
                if (g == null || g == npc || g.transform.IsChildOf(npc.transform)) continue;
                if (g == ignoreTarget && now < ignoreUntil && !mounted) continue;
                if (!Hostile(g)) continue;
                if (!hurt && IsCritter(g)) continue;
                if (shooter && !hurt && !ClearShot(g)) continue;                        // a shooter does not pick (or aim at) someone behind a wall
                float d = Game.HDist(g.transform.position, pl.position);
                if (d < bd) { bd = d; best = g; }
            }
            if (best != null && hurt && shooter && !ClearShot(best)) { coverTarget = best; coverUntil = now + 8f; }      // hurt from behind cover: he still answers it
            else if (best != null && best == coverTarget && hurt) coverUntil = now + 8f;
            if (best != null && best != target) Inform(best);
        }

        // NPCAI's idle behaviour sends NPCs home; for the follower it is switched off (best effort, through NPCAI's own table)
        static void ExcludeFromIdle()
        {
            if (Time.unscaledTime < nextExclude) return;
            nextExclude = Time.unscaledTime + 1f;
            SetExcluded(true);
        }

        static void SetExcluded(bool value) { if (Services.Npc != null) Services.Npc.SetFollowerDriven(npc, value); }

        // ---- walking after you -----------------------------------------------------------------------------------
        static void Follow(Transform pl, float dist, float now)
        {
            if (moveFsm == null) { moveFsm = FindFsm(npc, "Movement"); if (moveFsm == null) return; }
            string ms = moveFsm.ActiveStateName;
            if (target != null || ms == "Attack") return;                 // fighting: NPCAI is steering

            Vector3 to = pl.position - npc.transform.position;
            to.y = 0f;
            float stop = Mathf.Max(2.5f, followDist.Value);
            if (dist <= stop)
            {
                if (now >= nextState && ms == "Run") { nextState = now + 0.25f; moveFsm.Fsm.SetState("Idle"); }
                return;
            }
            if (dist < stop + 1.5f && ms != "Run") return;                 // a little slack so it does not twitch at the edge
            if (to.sqrMagnitude < 0.01f) return;
            if (now >= nextSteer) { nextSteer = now + 0.3f; heading = Takeover.Steer(npc.transform, to.normalized, 3.5f); }
            bool boxedIn = heading.sqrMagnitude < 0.01f;
            Vector3 face = boxedIn ? to.normalized : heading;
            npc.transform.rotation = Quaternion.Slerp(npc.transform.rotation, Quaternion.LookRotation(face), 8f * Time.deltaTime);
            if (now >= nextState)
            {
                nextState = now + 0.25f;
                if (boxedIn) { if (ms == "Run") moveFsm.Fsm.SetState("Idle"); }
                else if (ms != "Run") moveFsm.Fsm.SetState("Run");
            }
        }

        // far behind for a few seconds: pop back next to the player
        static void CatchUp(Transform pl, float dist, float now)
        {
            float limit = teleportDist.Value;
            if (limit <= 0f) return;
            if (dist <= limit) { tooFarSince = -1f; return; }
            if (tooFarSince < 0f) { tooFarSince = now; return; }
            if (now - tooFarSince < 4f) return;
            tooFarSince = -1f;
            Vector3 back = -pl.forward; back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = Vector3.back;
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = pl.position + Quaternion.Euler(0f, i * 60f, 0f) * back.normalized * 4f;
                Vector3 g;
                if (Game.GroundAt(p.x, p.z, out g) && Takeover.RoomAt(g))
                {
                    npc.transform.position = g + Vector3.up * 0.2f;
                    LiftAbove(g.y);
                    Rigidbody rb = npc.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic) rb.velocity = Vector3.zero;
                    Plugin.Info("Hire: " + displayName + " caught up.");
                    return;
                }
            }
        }

        // a free spot on the ground a few metres from the player (not on the car, not inside anything)
        static Vector3 FindExit(Transform pl, GameObject car)
        {
            int myLayer = npc != null ? npc.layer : 0;
            float[] radii = { 2.5f, 3.5f, 5f, 7f };
            for (int r = 0; r < radii.Length; r++)
                for (int a = 0; a < 8; a++)
                {
                    Vector3 dir = Quaternion.Euler(0f, a * 45f, 0f) * pl.right;
                    Vector3 p = pl.position + dir * radii[r];
                    // from well above, so a rise in the ground cannot hide the surface; the first surface the NPC can actually stand on
                    RaycastHit[] hits = NpcWorld.RaycastAll(new Vector3(p.x, pl.position.y + 40f, p.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
                    Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                    for (int i = 0; i < hits.Length; i++)
                    {
                        RaycastHit h = hits[i];
                        Transform ht = h.collider.transform;
                        if (car != null && ht.IsChildOf(car.transform)) continue;
                        if (npc != null && ht.IsChildOf(npc.transform)) continue;
                        if (ht.root == pl.root) continue;
                        if (Physics.GetIgnoreLayerCollision(myLayer, h.collider.gameObject.layer)) continue;     // the NPC would fall straight through this
                        if (h.collider.GetType().Name == "TerrainCollider" || h.normal.y >= 0.6f)
                        {
                            if (!Takeover.RoomAt(h.point)) break;                                              // something stands there
                            if (h.point.y > pl.position.y + 4f || h.point.y < pl.position.y - 6f) break;       // a roof or a pit, not the ground you stand on
                            Plugin.Info("Hire: exit spot on " + h.collider.name + " (layer " + h.collider.gameObject.layer + ", " + h.collider.GetType().Name + "), npc layer " + myLayer + ", y " + h.point.y.ToString("0.0") + ", player y " + pl.position.y.ToString("0.0"));
                            return h.point + Vector3.up * 0.15f;
                        }
                        break;
                    }
                }
            Plugin.Info("Hire: no free exit spot found; using your position.");
            return pl.position + Vector3.up * 0.3f;
        }

        // the NPC's origin is not at its feet: lift it until its lowest solid collider is just above the ground
        // (a body that starts inside a terrain falls straight through it)
        static void LiftAbove(float groundY)
        {
            if (npc == null) return;
            Physics.SyncTransforms();
            float minY = float.MaxValue;
            foreach (Collider c in npc.GetComponentsInChildren<Collider>(false))
                if (c != null && c.enabled && !c.isTrigger) minY = Math.Min(minY, c.bounds.min.y);
            if (minY == float.MaxValue) { npc.transform.position += Vector3.up * 0.6f; return; }
            float want = groundY + 0.12f;
            if (minY < want) npc.transform.position += Vector3.up * (want - minY);
            Physics.SyncTransforms();
        }

        // fell through the ground (or off the world): back next to the player
        static void RescueIfLost(Transform pl)
        {
            if (npc.transform.position.y > pl.position.y - 40f) return;
            float now = Time.time;
            if (now - firstRescue > 60f) { rescues = 0; firstRescue = now; }
            rescues++;
            Plugin.Warn("Hire: " + displayName + " was " + (pl.position.y - npc.transform.position.y).ToString("0") + " m below you; bringing it back (" + rescues + ").");
            LogBody("fell");
            if (rescues >= 3)
            {
                // it keeps falling: end the hire instead of letting it die (no rep lost)
                string n = displayName;
                ReleaseNpc();
                Clear();
                toast(n + " got lost on the way. The hire is over and you keep your reputation.");
                return;
            }
            TeleportNear(pl);
        }

        static void TeleportNear(Transform pl)
        {
            Vector3 spot = FindExit(pl, null);
            npc.transform.position = spot;
            LiftAbove(spot.y - 0.15f);
            Rigidbody rb = npc.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        static bool RangedUncached(GameObject go)
        {
            foreach (PlayMakerFSM f in NpcWorld.Fsms(go))
                if (f != null && (f.FsmName == "RangedAttackWait" || f.FsmName == "Damage Ranged")) return true;
            if (FindDeep(go.transform, "AttackRaycast_Ranged") != null) return true;
            return NamedRanged(go);
        }

        static bool NamedRanged(GameObject go)
        {
            string n = Game.BaseName(go.name);
            foreach (string part in (rangedNames.Value ?? "").Split(','))
            {
                string q = part.Trim();
                if (q.Length > 0 && n.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
