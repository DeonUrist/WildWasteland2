using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    // Camp takeover: choosing a target camp, switching its owner, and sending a group of the winning
    // faction's people walking in from about a kilometre away.
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

    internal static class Takeover
    {
        static readonly Dictionary<GameObject, bool> Suppressed = new Dictionary<GameObject, bool>();
        static void Suppress(GameObject go) { if (!Suppressed.ContainsKey(go)) Suppressed[go] = go.activeSelf; go.SetActive(false); }
        internal static void Shutdown()
        {
            foreach (var w in walkers) if (w.Go != null && w.Move != null && w.Move.ActiveStateName == "Run") w.Move.Fsm.SetState("Idle");
            foreach (var item in Suppressed) if (item.Key != null) item.Key.SetActive(item.Value);
            Suppressed.Clear(); ClearWalks(); prefabs.Clear(); giverUnusable.Clear(); giverLogged.Clear();
        }
        public static float SpawnDistance = 250f;
        public static int GroupSize = 5;
        public static float WalkSeconds = 300f;
        public static Action<string> Log = delegate { };
        public static bool PatrolAtCamp = true;     // [Takeover] PatrolAtCamp: walk around the camp after arriving

        static readonly List<Walker> walkers = new List<Walker>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        static float nextNeutralize;

        // walks that were saved with the quests and still have to be picked up again after a load
        static readonly List<SavedWalk> pending = new List<SavedWalk>();
        static float pendingUntil, nextPending;

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

        // Called whenever the quest book is cleared (new game, or a save being loaded): forgets the walkers and everything
        // remembered about the world that was left, and gives the game a few seconds to bring its saved NPCs back before
        // anything is re-spawned.
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

        // lines for the save: object name and origin-relative target of everybody still walking
        public static List<SavedWalk> ActiveWalks()
        {
            var res = new List<SavedWalk>();
            for (int i = 0; i < walkers.Count; i++)
                if (walkers[i].Go != null) res.Add(new SavedWalk { Name = walkers[i].Go.name, TargetRel = walkers[i].TargetRel, CampKey = walkers[i].CampKey ?? "" });
            for (int i = 0; i < pending.Count; i++) res.Add(pending[i]);
            return res;
        }

        public static void RestoreWalks(List<SavedWalk> saved)
        {
            walkers.Clear();
            pending.Clear();
            pending.AddRange(saved);
            pendingUntil = Time.unscaledTime + 90f;
            nextPending = 0f;
        }

        // The game recreates saved NPCs a little after loading; pick the walking ones up by name when they appear.
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

        // ---- choosing and describing the target -----------------------------------------------

        // A camp owned by a faction that is not friendly with the giver's, within minD..maxD of the giver.
        public static Poi PickCamp(Faction giver, Vector3 giverWorld, string wantFaction, float minD, float maxD, List<Quest> active, System.Random rng, HashSet<string> blocked)
        {
            if (giver == null) return null;
            string want = Util.Norm(wantFaction);
            if (want == "*" || want == "any") want = "";
            var cands = new List<Poi>();
            foreach (Poi p in Game.FindPois(giverWorld, minD, maxD, "camp"))
            {
                if (!p.Name.StartsWith("Camp", StringComparison.OrdinalIgnoreCase)) continue;
                Faction owner = Factions.OwnerOfPoi(p);
                if (owner == null || Factions.Friendly(giver, owner)) continue;
                if (giver.War.Count > 0 && !Factions.AtWar(giver, owner)) continue;   // only take camps of a faction we are at war with
                if (want.Length > 0 && Util.Norm(owner.Name) != want) continue;
                Vector3 rel = Game.Rel(p.Pos);
                bool taken = false;
                for (int i = 0; i < active.Count; i++)
                    if (active[i].Type == QType.Takeover && active[i].HasPos && Game.HDist(active[i].Pos, rel) < 5f) { taken = true; break; }
                if (blocked != null && blocked.Contains(QuestGen.PosKey(rel))) taken = true;     // a neighbour is already offering this camp
                if (!taken) cands.Add(p);
            }
            // always the closest one
            Poi best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < cands.Count; i++)
            {
                float d = Game.HDist(cands[i].Pos, giverWorld);
                if (d < bd) { bd = d; best = cands[i]; }
            }
            return best;
        }

        // living people of `tag` within `radius` of the camp (only counts what is loaded right now)
        public static int CountDefenders(Vector3 campWorld, string tag, float radius)
        {
            int n = 0;
            foreach (EnemyInfo e in Game.FindEnemies(campWorld, radius))
                if (Util.Norm(e.Tag) == Util.Norm(tag)) n++;
            return n;
        }

        public static void Fill(Quest q, Poi camp, Faction owner, float radius)
        {
            q.Type = QType.Takeover;
            q.Target = owner.Name;
            q.Label = owner.Name + " camp";
            q.HasPos = true;
            q.Pos = Game.Rel(camp.Pos);
            q.Radius = radius;
            // The camp's people only exist while you are near it. When some are loaded, that is the head count; when none are
            // (the camp is too far away to tell), the count stays unknown (0) until you get close, and the quest log is updated then.
            int here = CountDefenders(camp.Pos, owner.Name, radius);
            if (here > 0) { if (q.Count <= 0 || q.Count > here) q.Count = here; }
            else if (!q.CountExplicit) q.Count = 0;
            q.ReturnToGiver = true;
            if (string.IsNullOrEmpty(q.Title) || q.Title == "Odd job" || q.Title == "Take the camp") q.Title = "Take the " + owner.Name + " camp";
        }

        // ---- the takeover itself ---------------------------------------------------------------

        // Called when the quest's kills are done. Returns how many people were sent.
        public static int Begin(Quest q, System.Random rng)
        {
            Faction winner = Factions.ByKey(q.Home);
            if (winner == null || !q.HasPos) return 0;
            Vector3 campWorld = Game.Abs(q.Pos);

            Poi camp = null;
            string campKey = null;
            float bd = 80f;
            foreach (Poi p in Game.FindPois(campWorld, 0f, 80f, "camp"))
            {
                float d = Game.HDist(p.Pos, campWorld);
                if (d < bd) { bd = d; camp = p; }
            }
            if (camp != null)
            {
                Factions.Overrides[Factions.KeyOf(camp)] = winner.Name;
                campWorld = camp.Pos;
                campKey = "camp:" + Factions.KeyOf(camp);
                Log("Camp " + camp.Name + " now belongs to " + winner.Name + ".");
            }
            else Log("Takeover: camp object not found near the target; the owner could not be switched.");

            return SpawnGroup(winner, campWorld, Game.Rel(campWorld), rng, campKey);
        }

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

        // A real NPC has a Movement FSM. Some assets share an NPC's name but are only the model (no FSMs, no animator
        // state), which stands in a T-pose and never moves, so those are never used.
        static bool Usable(GameObject go)
        {
            PlayMakerFSM[] fsms = QuestWorld.Fsms(go);
            for (int i = 0; i < fsms.Length; i++) if (fsms[i] != null && fsms[i].FsmName == "Movement") return true;
            return false;
        }

        // Copies of these are not used for walkers and garrisons while anyone else of the faction can be copied
        // (a camp held by four merchants would be odd). Quest givers are always brought back as what they were.
        static readonly string[] NonFighters = { "merchant", "trader", "elder", "chieftain" };

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

        // The prefab asset for a name, or (fallback) a living object with that name to copy.
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

        // is something solid (not a slope) within dist metres in direction dir, at knee or chest height?
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

        // the free heading closest to `want` (turning up to 140 degrees either way), or zero when every way is blocked
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

        // is there room to stand at this ground point (nothing solid where a person would be)?
        internal static bool RoomAt(Vector3 ground)
        {
            return !Physics.CheckCapsule(ground + Vector3.up * 0.5f, ground + Vector3.up * 1.6f, 0.4f, SolidMask(), QueryTriggerInteraction.Ignore);
        }

        // every frame
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
                        double gh = Game.GameHours();
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

        // ---- the camp's old garrison -------------------------------------------------------------

        // Switches off the old faction's spawners at camps that were taken over, so its people do not respawn.
        // ---- a garrison for the new owner -----------------------------------------------------------
        public static int GarrisonSize = 4;
        public static float GarrisonTrigger = 500f;
        public static float GarrisonCooldownHours = 12f;
        public static Dictionary<string, long> Stamps;      // shared with the quest book (saved): "camp:<key>" -> game hours * 1000
        static readonly System.Random prng = new System.Random();
        static bool warnedNoNames;

        // The game's own spawner objects cannot be cloned reliably, so the mod tops the camp up itself: when you come
        // near a taken camp and fewer than GarrisonSize of its owner's people are there, the missing ones are created
        // (and saved with the game like the walkers). At most once per GarrisonCooldownHours of game time.
        static readonly Dictionary<string, float> campFirstNear = new Dictionary<string, float>();
        static readonly Dictionary<string, float> campLastTry = new Dictionary<string, float>();

        // The flag on a taken camp: when the camp has loaded in around you (a few seconds within range) and none of its
        // owner's people are there, a fresh garrison is created. If any of them is ever killed (destroyed while you are
        // near) the camp is not refilled for GarrisonCooldownHours of game time. Unloading is not a death, so leaving
        // and coming back refills an empty camp straight away.
        static void TryGarrison(Poi p, string ownerName)
        {
            if (GarrisonSize <= 0 || Stamps == null) return;
            if (Time.unscaledTime < settleUntil) return;                        // a save was just loaded: the game first restores its own NPCs
            double now = Game.GameHours();
            if (now < 0.0) return;
            string key = "camp:" + Factions.KeyOf(p);
            float rt = Time.unscaledTime;
            float first;
            if (!campFirstNear.TryGetValue(key, out first)) { campFirstNear[key] = rt; return; }
            if (rt - first < 2f) return;                                        // let the camp finish loading
            long st;
            if (Stamps.TryGetValue(key, out st) && now >= st / 1000.0 && now - st / 1000.0 < GarrisonCooldownHours) return;
            float last;
            if (campLastTry.TryGetValue(key, out last) && rt - last < 60f) return;
            Vector3 campRel = Game.Rel(p.Pos);
            if (pending.Count > 0) return;
            for (int i = 0; i < walkers.Count; i++)
                if (!walkers[i].Patrol && Game.HDist(walkers[i].TargetRel, campRel) < 30f) return;     // the group is still on its way
            Faction f = Factions.Get(ownerName);
            if (f == null || CountDefenders(p.Pos, f.Name, 120f) > 0) return;  // somebody is already home
            int made = SpawnAround(f, p.Pos, campRel, GarrisonSize, key);
            if (made > 0)
            {
                campLastTry[key] = rt;
                Log("Takeover: " + p.Name + " was empty; garrisoned it with " + made + " " + f.Name + " people.");
            }
            else campLastTry[key] = rt - 45f;     // nobody could be made (no one loaded to copy, no ground yet): try again in about 15 s
        }

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

        // ---- quest givers come back ------------------------------------------------------------------

        public static float GiverRespawnRange = 500f;
        static float nextGiver;
        static readonly Dictionary<string, float> giverMissingSince = new Dictionary<string, float>();
        static readonly HashSet<string> giverDisarmed = new HashSet<string>();     // died/vanished while you were watching: wait until you have been away
        static readonly HashSet<string> giverSeen = new HashSet<string>();         // seen alive this visit
        static readonly HashSet<string> giverUnusable = new HashSet<string>();     // copies of these have no AI: never tried again
        static readonly HashSet<string> giverLogged = new HashSet<string>();       // "nothing to copy" already said once

        // Everyone who gave you a job you still have to hand in is remembered where you last saw them. Come back to that
        // spot (after having been away) and if the NPC is not there any more (killed, or unloaded and lost), a new copy is
        // created in the same place and takes over the job.
        public static void GiverTick(QuestBook book, Vector3 playerWorld)
        {
            if (book == null || book.Active.Count == 0 || Time.unscaledTime < nextGiver) return;
            nextGiver = Time.unscaledTime + 1f;
            if (Time.unscaledTime < settleUntil) return;                        // a save was just loaded: the game first restores its own NPCs
            float rt = Time.unscaledTime;
            Dictionary<string, Transform> live = Game.LiveNames();
            for (int qi = 0; qi < book.Active.Count; qi++)
            {
                Quest q = book.Active[qi];
                string id = q.GiverId;
                if (string.IsNullOrEmpty(id) || !q.HasGiverPos) continue;
                if (!q.ReturnToGiver || q.Type == QType.Talk || Plugin.Returning(q)) continue;        // not handed in to the giver: no need to bring them back
                Transform tr;
                if (live.TryGetValue(id, out tr) && tr != null)
                {
                    giverMissingSince.Remove(id);
                    if (Game.HDist(playerWorld, tr.position) <= GiverRespawnRange) { q.GiverPos = Game.Rel(tr.position); giverSeen.Add(id); }
                    continue;
                }
                Vector3 world = Game.Abs(q.GiverPos);
                float d = Game.HDist(playerWorld, world);
                if (d > GiverRespawnRange * 1.5f) { giverDisarmed.Remove(id); giverSeen.Remove(id); giverMissingSince.Remove(id); continue; }
                if (d > GiverRespawnRange) { giverMissingSince.Remove(id); continue; }
                if (giverSeen.Contains(id)) { giverDisarmed.Add(id); continue; }   // it was here a moment ago: killed in front of you, so no instant replacement
                if (giverDisarmed.Contains(id)) continue;
                Vector3 g;
                if (!Game.GroundAt(world.x, world.z, out g)) continue;          // ground not loaded yet
                float since;
                if (!giverMissingSince.TryGetValue(id, out since)) { giverMissingSince[id] = rt; continue; }
                if (rt - since < 3f) continue;                                  // give the game time to bring it back itself

                string baseName = Game.BaseName(id);
                if (giverUnusable.Contains(baseName)) continue;
                if (InactiveExists(id)) { giverDisarmed.Add(id); continue; }     // it is there, just switched off: leave it to the game
                GameObject src = FindPrefab(baseName);
                if (src == null)
                {
                    if (giverLogged.Add(baseName)) Log("Giver: no prefab or living copy of " + baseName + " to bring " + id + " back; will try again.");
                    giverMissingSince[id] = rt + 27f;                           // look again in about 30 s, not every second
                    continue;
                }
                GameObject go = UnityEngine.Object.Instantiate(src, g + Vector3.up, Quaternion.Euler(0f, (float)prng.NextDouble() * 360f, 0f));
                go.SetActive(true);
                if (!Usable(go)) { UnityEngine.Object.Destroy(go); giverUnusable.Add(baseName); Log("Giver: the copy of " + baseName + " has no movement AI; not used."); continue; }
                string status = Game.RegisterSpawned(go, baseName);
                Log("Giver: " + id + " was gone from where you last saw it; " + baseName + " " + status + ".");
                string newName = go.name;
                long stamp;
                if (book.GiverStamp.TryGetValue(id, out stamp)) { book.GiverStamp.Remove(id); book.GiverStamp[newName] = stamp; }
                for (int k = 0; k < book.Active.Count; k++)
                    if (book.Active[k].GiverId == id) book.Active[k].GiverId = newName;     // every job from that NPC now belongs to the copy
                giverMissingSince.Remove(id);
            }
        }

        static bool InactiveExists(string name)
        {
            var all = QuestWorld.Objects;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].name == name && all[i].scene.IsValid()) return true;
            return false;
        }

        public static void NeutralizeTick(Vector3 playerWorld)
        {
            if (Factions.Overrides.Count == 0 || Time.unscaledTime < nextNeutralize) return;
            nextNeutralize = Time.unscaledTime + 2f;
            foreach (Poi p in Game.FindPois(playerWorld, 0f, 3000f, "camp"))
            {
                string ownerName;
                if (!Factions.Overrides.TryGetValue(Factions.KeyOf(p), out ownerName) || p.Obj == null) continue;
                string keep = Util.Norm(ownerName);
                for (int c = 0; c < p.Obj.childCount; c++)
                {
                    Transform ch = p.Obj.GetChild(c);
                    if (!ch.gameObject.activeSelf) continue;
                    string n = Util.Norm(ch.name);
                    for (int h = 0; h < Factions.HumanFactions.Length; h++)
                    {
                        string hf = Factions.HumanFactions[h];
                        if (hf.Length > 0 && hf != keep && n.Contains(hf)) { Suppress(ch.gameObject); break; }
                    }
                }
                float pd = Game.HDist(playerWorld, p.Pos);
                if (pd <= GarrisonTrigger) TryGarrison(p, ownerName);
                else if (pd > GarrisonTrigger * 1.5f) campFirstNear.Remove("camp:" + Factions.KeyOf(p));
            }
        }
    }
}
