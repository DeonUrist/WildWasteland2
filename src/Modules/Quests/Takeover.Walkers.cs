using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Camp takeover, NPCAI part (Wave A, from Quests/Takeover.cs): spawning the winning faction's walkers and garrisons, raycast steering, the walker loop and the save restore of walks (B07; README 6.2: steer through INpcApi.Travel).</summary>
    internal static partial class Takeover
    {
        // one line per walker for the F7 dump: where it is, how far from the camp and the player
        public static List<string> DescribeWalkers(Vector3 playerWorld)
        {
            var res = new List<string>();
            for (int i = 0; i < walkers.Count; i++)
            {
                Walker w = walkers[i];
                if (w.Go == null) { res.Add((w.Name ?? "?") + ": gone (destroyed or unloaded)"); continue; }
                Vector3 p = w.Go.transform.position;
                Vector3 camp = Game.Abs(w.TargetRel);
                res.Add(w.Go.name + ": at (" + p.x.ToString("0") + ", " + p.y.ToString("0") + ", " + p.z.ToString("0") + ")  " + (int)Game.HDist(p, camp) + " m from camp, " + (int)Game.HDist(p, playerWorld) + " m from you");
            }
            return res;
        }

        static readonly List<Walker> walkers = new List<Walker>();

        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        // walks that were saved with the quests and still have to be picked up again after a load
        static readonly List<SavedWalk> pending = new List<SavedWalk>();

        static float pendingUntil, nextPending;

        /// <summary>Called whenever the quest book is cleared (new game, or a save being loaded): forgets the walkers and everything remembered about the world that was left, and gives the game a few seconds to bring its saved NPCs back before anything is re-spawned.</summary>
        public static void ClearWalks()
        {
            walkers.Clear();
            pending.Clear();
            campFirstNear.Clear();
            campLastTry.Clear();
            giverMissingSince.Clear();
            giverDisarmed.Clear();
            giverSeen.Clear();
            prefabMissUntil.Clear();
            settleUntil = Time.unscaledTime + SettleSeconds;
        }

        // nothing is spawned for this long after a save is loaded (the game restores its own NPCs first)
        const float SettleSeconds = 10f;

        static float settleUntil;

        /// <summary>lines for the save: object name and origin-relative target of everybody still walking</summary>
        public static List<SavedWalk> ActiveWalks()
        {
            var res = new List<SavedWalk>();
            for (int i = 0; i < walkers.Count; i++)
                if (walkers[i].Go != null) res.Add(new SavedWalk { Name = walkers[i].Go.name, TargetRel = walkers[i].TargetRel, CampKey = walkers[i].CampKey ?? "" });
            for (int i = 0; i < pending.Count; i++) res.Add(pending[i]);
            return res;
        }

        /// <summary>Load: saved walks are picked up again as the walkers' NPCs come back.</summary>
        public static void RestoreWalks(List<SavedWalk> saved)
        {
            walkers.Clear();
            pending.Clear();
            pending.AddRange(saved);
            pendingUntil = Time.unscaledTime + 90f;
            nextPending = 0f;
        }

        /// <summary>The game recreates saved NPCs a little after loading; pick the walking ones up by name when they appear.</summary>
        static void ResolvePending()
        {
            if (pending.Count == 0 || Time.unscaledTime < nextPending) return;
            nextPending = Time.unscaledTime + 2f;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                GameObject go = Game.FindLive(pending[i].Name) == null ? null : Game.FindLive(pending[i].Name).gameObject;
                if (go != null)
                {
                    var w = new Walker();
                    w.Go = go; w.Name = go.name; w.TargetRel = pending[i].TargetRel;
                    w.CampKey = string.IsNullOrEmpty(pending[i].CampKey) ? null : pending[i].CampKey;
                    w.StartAt = Time.time + 1f; w.Deadline = Time.time + WalkSeconds; w.LastPos = go.transform.position; w.NextStuckCheck = w.StartAt + 6f;
                    walkers.Add(w);
                    pending.RemoveAt(i);
                    Log("Takeover: " + w.Name + " resumed its walk after loading.");
                }
                else if (Time.unscaledTime > pendingUntil) pending.RemoveAt(i);     // gone (killed, or not saved)
            }
        }

        /// <summary>Sends a group of the winning faction's people from about SpawnDistance away to walk into the taken camp.</summary>
        static int SpawnGroup(Faction f, Vector3 campWorld, Vector3 campRel, System.Random rng, string campKey)
        {
            List<string> names = Game.FindNpcNames(f.Name);
            if (names.Count == 0) { Log("Takeover: no living " + f.Name + " people are loaded to copy, so nobody was sent."); return 0; }
            names = Fighters(names);

            // a start point about SpawnDistance away where the ground is loaded
            Vector3 start = Vector3.zero;
            bool found = false;
            float[] dists = { SpawnDistance, SpawnDistance * 0.7f, SpawnDistance * 0.4f, 60f };
            for (int di = 0; di < dists.Length && !found; di++)
            {
                for (int a = 0; a < 12 && !found; a++)
                {
                    double ang = rng.NextDouble() * Math.PI * 2.0;
                    float x = campWorld.x + (float)Math.Sin(ang) * dists[di];
                    float z = campWorld.z + (float)Math.Cos(ang) * dists[di];
                    Vector3 g;
                    if (Game.GroundAt(x, z, out g)) { start = g; found = true; }
                }
            }
            if (!found) { Log("Takeover: no loaded ground found around the camp to spawn on."); return 0; }

            Log("Takeover: group starts at (" + start.x.ToString("0") + ", " + start.y.ToString("0") + ", " + start.z.ToString("0") + "), camp is at (" + campWorld.x.ToString("0") + ", " + campWorld.y.ToString("0") + ", " + campWorld.z.ToString("0") + ").");
            int made = 0;
            for (int i = 0; i < GroupSize; i++)
            {
                string name = names[rng.Next(names.Count)];
                GameObject src = FindPrefab(name);
                if (src == null) { Log("Takeover: no prefab or living copy of " + name + "."); continue; }
                Vector3 pos = start + new Vector3((float)(rng.NextDouble() - 0.5) * 8f, 1f, (float)(rng.NextDouble() - 0.5) * 8f);
                Vector3 dir = campWorld - pos; dir.y = 0f;
                Quaternion rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
                GameObject go = UnityEngine.Object.Instantiate(src, pos, rot);
                go.SetActive(true);
                if (!Usable(go)) { Log("Takeover: the copy of " + name + " has no movement AI; dropped it."); UnityEngine.Object.Destroy(go); continue; }
                string status = Game.RegisterSpawned(go, name);
                Log("Takeover: " + name + " " + status);
                var w = new Walker();
                w.Go = go;
                w.Name = go.name;
                w.TargetRel = campRel;
                w.CampKey = campKey;
                w.StartAt = Time.time + 1.5f;
                w.Deadline = Time.time + WalkSeconds;
                w.LastPos = pos;
                w.NextStuckCheck = w.StartAt + 6f;
                walkers.Add(w);
                made++;
            }
            Log("Takeover: sent " + made + " " + f.Name + " people from " + (int)Game.HDist(start, campWorld) + " m away.");
            return made;
        }

        /// <summary>A real NPC has a Movement FSM. Some assets share an NPC's name but are only the model (no FSMs, no animator state), which stands in a T-pose and never moves, so those are never used.</summary>
        static bool Usable(GameObject go)
        {
            PlayMakerFSM[] fsms = QuestWorld.Fsms(go);
            for (int i = 0; i < fsms.Length; i++) if (fsms[i] != null && fsms[i].FsmName == "Movement") return true;
            return false;
        }

        // Copies of these are not used for walkers and garrisons while anyone else of the faction can be copied
        // (a camp held by four merchants would be odd). Quest givers are always brought back as what they were.
        static readonly string[] NonFighters = { "merchant", "trader", "elder", "chieftain" };

        /// <summary>Names without merchants / traders / elders / chieftains (unless nobody else is there).</summary>
        static List<string> Fighters(List<string> names)
        {
            var res = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string n = Util.Norm(names[i]);
                bool skip = false;
                for (int k = 0; k < NonFighters.Length && !skip; k++) if (n.Contains(NonFighters[k])) skip = true;
                if (!skip) res.Add(names[i]);
            }
            return res.Count > 0 ? res : names;
        }

        // a name that could not be found is not looked for again (a full scan of every object) for a while
        static readonly Dictionary<string, float> prefabMissUntil = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The prefab asset for a name, or (fallback) a living object with that name to copy.</summary>
        static GameObject FindPrefab(string name)
        {
            GameObject cached;
            if (prefabs.TryGetValue(name, out cached) && cached != null) return cached;
            float missUntil;
            if (prefabMissUntil.TryGetValue(name, out missUntil) && Time.unscaledTime < missUntil) return null;
            GameObject found = ScanForPrefab(name);
            if (found == null) prefabMissUntil[name] = Time.unscaledTime + 30f;
            else prefabMissUntil.Remove(name);
            return found;
        }

        /// <summary>Prefab asset for a name, else a living object with that name to copy.</summary>
        static GameObject ScanForPrefab(string name)
        {
            GameObject living = null;
            var all = QuestWorld.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                if (go == null || go.transform.parent != null) continue;
                string n = Game.BaseName(go.name);
                if (!string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Usable(go)) continue;          // model only, no AI: would T-pose
                if (!go.scene.IsValid())            // not in a scene = an asset / prefab
                {
                    if (go.name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0) continue;
                    prefabs[name] = go;
                    return go;
                }
                if (living == null && go.activeInHierarchy) living = go;
            }
            if (living != null) prefabs[name] = living;
            return living;
        }

        // ---- walking in ----------------------------------------------------------------------------

        /// <summary>The Movement FSM of an object (null when missing).</summary>
        static PlayMakerFSM MoveFsm(GameObject go)
        {
            PlayMakerFSM[] fsms = QuestWorld.Fsms(go);
            for (int i = 0; i < fsms.Length; i++) if (fsms[i] != null && fsms[i].FsmName == "Movement") return fsms[i];
            return null;
        }

        // ---- steering round walls ------------------------------------------------------------------
        // The Movement FSM only walks the NPC straight ahead, so anything in the way (a wall, a car, a fence) stops it dead.
        // Before walking, look ahead for something solid and turn away from it.
        static int solidMask;

        static bool solidMaskSet;

        /// <summary>Layers that block a walker (everything except actors, items, player, water, UI).</summary>
        static int SolidMask()
        {
            if (!solidMaskSet)
            {
                solidMaskSet = true;
                int m = ~0;
                string[] skip = { "Actor", "Item", "Ignore Raycast", "Player", "Water", "UI" };
                for (int i = 0; i < skip.Length; i++)
                {
                    int l = LayerMask.NameToLayer(skip[i]);
                    if (l >= 0) m &= ~(1 << l);
                }
                solidMask = m;
            }
            return solidMask;
        }

        /// <summary>is something solid (not a slope) within dist metres in direction dir, at knee or chest height?</summary>
        static bool Blocked(Transform t, Vector3 dir, float dist)
        {
            for (int h = 0; h < 2; h++)
            {
                Vector3 from = t.position + Vector3.up * (h == 0 ? 0.5f : 1.3f);
                RaycastHit hit;
                if (!Physics.Raycast(from, dir, out hit, dist, SolidMask(), QueryTriggerInteraction.Ignore)) continue;
                if (hit.transform == t || hit.transform.IsChildOf(t)) continue;
                if (hit.normal.y > 0.7f) continue;
                return true;
            }
            return false;
        }

        /// <summary>the free heading closest to `want` (turning up to 140 degrees either way), or zero when every way is blocked</summary>
        internal static Vector3 Steer(Transform t, Vector3 want, float look)
        {
            float[] turns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 125f, -125f };
            for (int i = 0; i < turns.Length; i++)
            {
                Vector3 d = Quaternion.Euler(0f, turns[i], 0f) * want;
                if (!Blocked(t, d, look)) return d;
            }
            return Vector3.zero;
        }

        /// <summary>is there room to stand at this ground point (nothing solid where a person would be)?</summary>
        internal static bool RoomAt(Vector3 ground)
        {
            return !Physics.CheckCapsule(ground + Vector3.up * 0.5f, ground + Vector3.up * 1.6f, 0.4f, SolidMask(), QueryTriggerInteraction.Ignore);
        }

        /// <summary>every frame</summary>
        public static void Update()
        {
            ResolvePending();
            if (walkers.Count == 0) return;
            float now = Time.time;
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Walker w = walkers[i];
                if (w.Go == null)
                {
                    // destroyed while you are close = it was killed (far away it is just unloaded, which is not a death)
                    if (w.CampKey != null && Stamps != null)
                    {
                        Transform plr = Game.Player();
                        double gh = WildWasteland.Core.Clock.GameHours;
                        if (plr != null && gh >= 0.0 && Game.HDist(plr.position, Game.Abs(w.TargetRel)) < 200f)
                        {
                            Stamps[w.CampKey] = (long)(gh * 1000.0);
                            Log("Takeover: " + w.Name + " died at its camp; no refill for " + GarrisonCooldownHours + " game hours.");
                        }
                    }
                    walkers.RemoveAt(i); continue;
                }
                if (now < w.StartAt) continue;
                if (!w.Go.activeInHierarchy) continue;                  // switched off by the game while you are far away

                if (w.Move == null) w.Move = MoveFsm(w.Go);
                PlayMakerFSM move = w.Move;
                if (move != null && move.ActiveStateName == "Attack") continue;     // fighting: leave it to the game's own AI

                Vector3 target = Game.Abs(w.TargetRel);
                Vector3 pos = w.Go.transform.position;
                Vector3 to = target - pos; to.y = 0f;
                float dist = to.magnitude;

                if (w.Patrol)
                {
                    // at the camp: wander between random spots, pausing in between
                    if (!PatrolAtCamp)
                    {
                        if (move != null && move.ActiveStateName == "Run") move.Fsm.SetState("Idle");     // stand where it arrived
                        continue;
                    }
                    if (now < w.WaitUntil) continue;
                    if (!w.HasPatrolTarget)
                    {
                        w.PatrolTarget = RandomSpotNear(target, 4f, 14f);
                        w.HasPatrolTarget = true;
                        w.PatrolDeadline = now + 30f;
                        w.NextStuckCheck = now + 4f;
                        w.LastPos = pos;
                    }
                    Vector3 pto = w.PatrolTarget - pos; pto.y = 0f;
                    bool stuck = false;
                    if (now >= w.NextStuckCheck)
                    {
                        w.NextStuckCheck = now + 4f;
                        stuck = Game.HDist(pos, w.LastPos) < 1f;
                        w.LastPos = pos;
                    }
                    bool wall = pto.sqrMagnitude > 0.01f && Blocked(w.Go.transform, pto.normalized, 2.2f);   // a wall ahead: do not run into it, stop and wait
                    if (pto.magnitude <= 2.5f || now > w.PatrolDeadline || stuck || wall)
                    {
                        if (move != null && move.ActiveStateName == "Run") move.Fsm.SetState("Idle");
                        w.HasPatrolTarget = false;
                        w.WaitUntil = now + 6f + (float)prng.NextDouble() * 14f;
                        continue;
                    }
                    w.Go.transform.rotation = Quaternion.Slerp(w.Go.transform.rotation, Quaternion.LookRotation(pto.normalized), 6f * Time.deltaTime);
                    if (move != null && now >= w.NextState)
                    {
                        w.NextState = now + 0.25f;
                        string ps = move.ActiveStateName;
                        if (ps != "Run" && ps != "Attack") move.Fsm.SetState("Run");
                    }
                    continue;
                }

                if (dist <= 15f || now > w.Deadline)
                {
                    if (dist > 15f) PlaceNear(w.Go, target);            // took too long: pop in at the camp
                    if (move != null) move.Fsm.SetState("Idle");
                    Log("Takeover: " + w.Go.name + " arrived.");
                    w.Patrol = true;                                    // keep it walking around the camp
                    w.HasPatrolTarget = false;
                    w.WaitUntil = now + 2f + (float)prng.NextDouble() * 6f;
                    continue;
                }

                // face the camp (turning away from walls in the road) and keep the Movement FSM in its "Run" state (it moves the NPC forward at 5 m/s)
                if (now >= w.NextSteer)
                {
                    w.NextSteer = now + 0.3f;
                    w.Heading = Steer(w.Go.transform, to.normalized, 3.5f);
                }
                bool boxedIn = w.Heading.sqrMagnitude < 0.01f;
                Vector3 face = boxedIn ? to.normalized : w.Heading;
                w.Go.transform.rotation = Quaternion.Slerp(w.Go.transform.rotation, Quaternion.LookRotation(face), 8f * Time.deltaTime);
                if (move != null && now >= w.NextState)
                {
                    w.NextState = now + 0.25f;
                    if (boxedIn) { if (move.ActiveStateName == "Run") move.Fsm.SetState("Idle"); }     // nowhere to go: stop pushing against the wall
                    else if (move.ActiveStateName != "Run") move.Fsm.SetState("Run");
                }

                // stuck on a rock or wall: hop 40 m along the line
                if (now >= w.NextStuckCheck)
                {
                    w.NextStuckCheck = now + 5f;
                    if (Game.HDist(pos, w.LastPos) < 2f)
                    {
                        w.StuckCount++;
                        if (w.StuckCount >= 2)
                        {
                            w.StuckCount = 0;
                            // jump 25 m ahead, or to either side, onto free ground (never inside a wall)
                            float[] angs = { 0f, 40f, -40f, 80f, -80f };
                            for (int ai = 0; ai < angs.Length; ai++)
                            {
                                Vector3 hop = pos + Quaternion.Euler(0f, angs[ai], 0f) * to.normalized * 25f;
                                Vector3 g;
                                if (Game.GroundAt(hop.x, hop.z, out g) && RoomAt(g)) { w.Go.transform.position = g + Vector3.up; break; }
                            }
                        }
                    }
                    else w.StuckCount = 0;
                    w.LastPos = w.Go.transform.position;
                }
            }
        }

        /// <summary>Places an object on free ground 10-24 m around the camp.</summary>
        static void PlaceNear(GameObject go, Vector3 campWorld)
        {
            Vector3 g;
            for (int k = 0; k < 8; k++)
            {
                float ang = UnityEngine.Random.value * 6.2831f;
                float r = 10f + k * 2f;
                if (Game.GroundAt(campWorld.x + Mathf.Sin(ang) * r, campWorld.z + Mathf.Cos(ang) * r, out g) && RoomAt(g)) { go.transform.position = g + Vector3.up; return; }
            }
        }

        /// <summary>Spawns a garrison of the faction around the camp that patrols there.</summary>
        static int SpawnAround(Faction f, Vector3 campWorld, Vector3 campRel, int count, string campKey)
        {
            List<string> names = Game.FindNpcNames(f.Name);
            if (names.Count == 0)
            {
                if (!warnedNoNames) { warnedNoNames = true; Log("Takeover: no living " + f.Name + " people are loaded to copy for the garrison; will try again."); }
                return 0;
            }
            names = Fighters(names);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                string name = names[prng.Next(names.Count)];
                GameObject src = FindPrefab(name);
                if (src == null) continue;
                Vector3 spot = RandomSpotNear(campWorld, 6f, 25f);
                if (spot == campWorld) continue;        // no loaded ground found
                GameObject go = UnityEngine.Object.Instantiate(src, spot + Vector3.up, Quaternion.Euler(0f, (float)prng.NextDouble() * 360f, 0f));
                go.SetActive(true);
                if (!Usable(go)) { Log("Takeover: the copy of " + name + " has no movement AI; dropped it."); UnityEngine.Object.Destroy(go); continue; }
                string status = Game.RegisterSpawned(go, name);
                Log("Takeover: garrison " + name + " " + status);
                var w = new Walker();
                w.Go = go; w.Name = go.name; w.TargetRel = campRel; w.CampKey = campKey;
                w.StartAt = Time.time + 1.5f; w.Deadline = Time.time + WalkSeconds;
                w.LastPos = go.transform.position; w.NextStuckCheck = w.StartAt + 6f;
                w.Patrol = true; w.WaitUntil = Time.time + 2f + (float)prng.NextDouble() * 6f;
                walkers.Add(w);
                made++;
            }
            return made;
        }

        /// <summary>A free ground spot between two radii around a centre (the centre when none).</summary>
        static Vector3 RandomSpotNear(Vector3 centre, float minR, float maxR)
        {
            for (int k = 0; k < 6; k++)
            {
                double ang = prng.NextDouble() * Math.PI * 2.0;
                float r = minR + (float)prng.NextDouble() * (maxR - minR);
                Vector3 g;
                if (Game.GroundAt(centre.x + (float)Math.Sin(ang) * r, centre.z + (float)Math.Cos(ang) * r, out g) && RoomAt(g)) return g;
            }
            return centre;
        }
    }

    /// <summary>One walking person of a takeover group (spawned far from the camp, walks in, then patrols around it).</summary>
    internal class Walker
    {
        public GameObject Go;
        public string Name;
        public Vector3 TargetRel;       // origin-relative, because the world shifts
        public float StartAt;           // wait a moment so the new object's FSMs are running
        public float Deadline;
        public float NextStuckCheck;
        public Vector3 LastPos;
        public int StuckCount;
        public float NextState;
        public bool Patrol;             // arrived: wander around the camp
        public bool HasPatrolTarget;
        public Vector3 PatrolTarget;
        public float PatrolDeadline;
        public float WaitUntil;
        public string CampKey;          // "camp:<key>": if this one is destroyed near the player, the camp's refill cooldown starts
        public PlayMakerFSM Move;       // its Movement FSM, looked up once
        public Vector3 Heading;         // the way it is steering now (round a wall); zero = boxed in
        public float NextSteer;
    }
}
