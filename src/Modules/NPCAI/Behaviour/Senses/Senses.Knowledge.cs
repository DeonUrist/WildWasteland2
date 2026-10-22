using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Senses part: what an agent knows: engaging, losing and remembering targets, ghosts (rank, merge, holders), flanking, pursuit guesses and searches.</summary>
    internal static partial class Senses
    {
        // ---------- knowledge ----------
        /// <summary>The agent engages a target it sees: combat state, fresh shout timer, pursuit reset; a player target refreshes the fought memory.</summary>
        private static void Engage(Agent a, GameObject target)
        {
            if (a.Spider) a.AmbushDist = 0f;
            if (target != null && target == _player) FoughtFor(a, target, Time.time, "engages him");
            if ((a.State == State.Combat || a.State == State.Hide) && a.Target == target) return;
            if (a.State == State.Hide) { a.Target = target; a.UnseenFor = 0f; a.UnseenSince = -1f; return; }   // in cover: it knows its target, stays put
            Release(a);
            a.State = State.Combat; a.Target = target; a.UnseenFor = 0f; a.UnseenSince = -1f; a.Pursuits = 0; a.Pursue = null; a.Heard.RemoveWhere(x => x < 0);
            a.NextShout = 0f;       // a fresh sighting: the next shout comes at once
            if (WildWasteland.Core.Log.L2) Log(a, "sees " + Name(target) + " at " + Vector3.Distance(a.T.position, target.transform.position).ToString("0") + " m");
        }

        /// <summary>(1.7.0) Nests: a web was touched - the nest's spiders know where the target is and go for it (they chase / search as usual from here)</summary>
        internal static bool Alert(GameObject owner, GameObject target)
        {
            var a = Get(owner);
            if (a == null || target == null || !On) return false;
            a.AmbushDist = 0f;
            Engage(a, target);
            a.LastSeen = target.transform.position; a.LastSeenVel = Vector3.zero; a.UnseenFor = 0f; a.UnseenSince = -1f;
            return true;
        }

        /// <summary>Sight of the target is lost: a VISION ghost at the last seen spot, the agent goes there and will pursue (guess or track) a few times.</summary>
        private static void LoseTarget(Agent a, float now)
        {
            var target = a.Target;
            if (WildWasteland.Core.Log.L2 && target != null) Log(a, SightLossText(a, target));
            a.Target = null;
            var g = GetOrMake(Src.Vision, a.LastSeen, target, "last seen " + Name(target), 4f, 3f, now);
            g.Subject = target;
            a.State = State.Idle;
            Assign(a, g, Src.Vision, now);
            a.Pursue = target;
            a.Pursuits = a.PursuitsTotal = Mathf.Max(0, NpcaiSettings.Guesses.Value);
            if (WildWasteland.Core.Log.L2) Log(a, "lost sight of " + Name(target) + ", goes where it was (ghost #" + g.Id + ", will " + (a.Tracker ? "track it" : "guess") + " " + a.Pursuits + " more time(s))");
        }

        /// <summary>Diagnostic text of a sight loss (reason, distance, angle against the half cone).</summary>
        private static string SightLossText(Agent a, GameObject target)
        {
            Vector3 flat = target.transform.position - a.T.position; flat.y = 0f;
            return "sight loss: " + a.SightMiss + ", target " + Name(target) + ", unseen " + a.UnseenFor.ToString("0.0", CultureInfo.InvariantCulture) + " s, distance " + flat.magnitude.ToString("0.0", CultureInfo.InvariantCulture) + " m, angle " + Vector3.Angle(a.T.forward, flat).ToString("0.0", CultureInfo.InvariantCulture) + " deg, half-cone " + (Mathf.Clamp(ConeOf(a), 10f, 360f) * 0.5f).ToString("0.0", CultureInfo.InvariantCulture) + " deg";
        }

        // ---------- (crew AI) the fought-player memory ----------
        /// <summary>Set when an NPC engages the player, is hit by him, hears his shots while fighting him (convoy crews on foot: also when they hear them at all), or is told by Patrols (Core.ModApi.Get<Core.INpcApi>().Remember: its car's crew fought him); refreshed by every sight of him while fighting him. While it lasts ([Senses] FoughtMemorySeconds after the last contact) losing sight does not drop him.</summary>
        internal static float FoughtSeconds { get { return Mathf.Clamp(NpcaiSettings.FoughtMemorySeconds.Value, 10f, 600f); } }
        /// <summary>Refreshes the fought memory of a target for FoughtMemorySeconds from now.</summary>
        private static void FoughtFor(Agent a, GameObject target, float now, string why) { FoughtUntil(a, target, now + FoughtSeconds, why); }
        /// <summary>Sets the fought memory of a target until a time (keeps a longer existing one); logs a fresh memory at level 2.</summary>
        private static void FoughtUntil(Agent a, GameObject target, float until, string why)
        {
            if (target == null) return;
            float now = Time.time;
            bool fresh = !ReferenceEquals(a.Fought, target) || now >= a.FoughtUntil;
            a.Fought = target;
            if (fresh || until > a.FoughtUntil) a.FoughtUntil = until;
            if (fresh && WildWasteland.Core.Log.L2) Log(a, "remembers " + Name(target) + " for " + (a.FoughtUntil - now).ToString("0") + " s (" + why + ")");
        }
        /// <summary>True while the agent remembers fighting the target (never the player in God Mode).</summary>
        private static bool Remembers(Agent a, GameObject target, float now)
        {
            return target != null && ReferenceEquals(a.Fought, target) && now < a.FoughtUntil && !(target == _player && target.layer == 5);   // God Mode: never "known"
        }

        /// <summary>Hearing shots renews an existing fight; a new listener investigates through Noise without acquiring the shooter.</summary>
        private static void HeardPlayerShot(Vector3 pos, float radius, float now)
        {
            if (_player == null || radius <= 0f) return;
            float r2 = radius * radius;
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner == null || a.T.parent != null || a.Blown || a.Deaf || (a.T.position - pos).sqrMagnitude > r2) continue;
                if ((a.State == State.Combat || a.State == State.Hide) && a.Target == _player) { FoughtFor(a, _player, now, "hears his shots while fighting him"); continue; }
            }
        }

        // Core.ModApi.Get<Core.INpcApi>().Remember: the NPC (registered now if it can be, else the moment it registers) remembers target until now + seconds
        private static readonly Dictionary<int, KeyValuePair<GameObject, float>> _remember = new Dictionary<int, KeyValuePair<GameObject, float>>();
        /// <summary>INpcApi.Remember: the NPC (now, or the moment it registers) remembers the target for seconds (crew memory handed over by Patrols).</summary>
        internal static void Remember(GameObject npc, GameObject target, float seconds)
        {
            if (npc == null || target == null) return;
            FindPlayer();
            if (IsPlayer(target) || (_playerCar != null && target.transform.root == _playerCar.transform.root)) target = _player;
            else target = target.transform.root.gameObject;
            if (target == null) return;
            float until = Time.time + (seconds > 0f ? seconds : FoughtSeconds);
            var a = Get(npc) ?? TryRegister(npc);
            if (a == null) { _remember[npc.GetInstanceID()] = new KeyValuePair<GameObject, float>(target, until); return; }
            a.Crew = true; FoughtUntil(a, target, until, "told by its crew");
            if (SimpleBandits.Has(npc)) a.NextLook = Time.time;
        }
        /// <summary>Applies a Remember order that arrived before the agent registered.</summary>
        private static void Recall(Agent a)
        {
            if (_remember.Count == 0) return;
            int id = a.Owner.GetInstanceID();
            KeyValuePair<GameObject, float> r;
            if (!_remember.TryGetValue(id, out r)) return;
            _remember.Remove(id);
            if (r.Key != null && Time.time < r.Value) { a.Crew = true; FoughtUntil(a, r.Key, r.Value, "told by its crew"); if (SimpleBandits.Has(a.Owner)) a.NextLook = Time.time; }   // as Remember: it looks at once
        }

        /// <summary>for Core.ModApi.Get<Core.INpcApi>().IsIdle: no target, no ghost to check, no search (an NPC the senses do not know is idle)</summary>
        internal static bool IsIdle(GameObject npc)
        {
            if (npc == null) return false;
            var a = Get(npc);
            float now = Time.time;
            if (a == null)      // not registered yet: idle unless it was told it fought the player (it takes him at its first look)
            {
                KeyValuePair<GameObject, float> r;
                return !(_remember.Count > 0 && _remember.TryGetValue(npc.GetInstanceID(), out r) && r.Key != null && now < r.Value);
            }
            if (a.State != State.Idle || a.Target != null || a.Ghost != null) return false;
            // a convoy crew member on foot that remembers fighting the player goes for him at its next look (SimpleLook): not idle in between
            return !(_player != null && SimpleBandits.Fighting(npc) && Remembers(a, _player, now) && IsPlayer(SimpleBandits.Goal(npc)));
        }

        /// <summary>The agent reached its ghost: it pursues its lost target (tracker: the real position, human: a growing guess) while guesses remain, else searches for SearchSeconds.</summary>
        private static void Arrived(Agent a, float now)
        {
            // it lost its target from sight a moment ago: before searching it goes to where that target really is now, a few times -
            // stepping behind a barrel does not shake off a raider who was right behind you
            if (a.Ghost != null) a.Done[a.Ghost.Id] = a.Ghost.Moved;
            if (a.Pursuits > 0 && a.Pursue != null && a.Ghost != null && a.Ghost.Subject == a.Pursue)
            {
                a.Pursuits--;
                int k = a.PursuitsTotal - a.Pursuits;        // 1, 2, 3 ...: each guess is rougher than the last
                Vector3 real = a.Pursue.transform.position;
                // a tracker (hound, four-legged mutant) follows the real position; a human guesses: where the target was going when it was last
                // seen, plus a growing miss (GuessRadius x k), on the ground
                Vector3 spot = a.Tracker ? real : Guess(real, a.LastSeenVel, k);
                var g = a.Tracker ? GetOrMake(Src.Vision, spot, a.Pursue, "where " + Name(a.Pursue) + " really is", 2f, 1f, now)
                                  : GetOrMake(Src.Vision, spot, null, "guess " + k + " about " + Name(a.Pursue), 0f, 0f, now);   // its own guess, not shared, not merged
                g.Subject = a.Pursue;
                a.State = State.Idle;
                if (Assign(a, g, Src.Vision, now)) { if (WildWasteland.Core.Log.L2) Log(a, "reached ghost without seeing " + Name(a.Pursue) + ", " + (a.Tracker ? "follows to where it is now" : "guesses where it went (" + Vector3.Distance(spot, real).ToString("0") + " m off)") + " (ghost #" + g.Id + ", " + a.Pursuits + " left)"); return; }
            }
            a.State = State.Search; a.SearchUntil = now + Mathf.Max(0f, NpcaiSettings.SearchSeconds.Value);
            if (WildWasteland.Core.Log.L2) Log(a, "reached ghost #" + (a.Ghost != null ? a.Ghost.Id.ToString() : "?") + ", looks around for " + NpcaiSettings.SearchSeconds.Value.ToString("0") + " s");
            if (a.Ghost != null) RetireIfSearched(a.Ghost);
        }

        /// <summary>a guess k about where a lost target went: 1.5 s along the way it was going, then a random miss of GuessRadius x k, on the ground</summary>
        private static Vector3 Guess(Vector3 real, Vector3 vel, int k)
        {
            vel.y = 0f;
            if (vel.sqrMagnitude > 1f) real += vel.normalized * Mathf.Min(vel.magnitude, 8f) * 1.5f;
            float r = Mathf.Max(0f, NpcaiSettings.GuessRadius.Value) * Mathf.Max(1, k);
            Vector2 off = UnityEngine.Random.insideUnitCircle * r;
            return OnGround(real + new Vector3(off.x, 0f, off.y), real.y);
        }

        /// <summary>a sound / a hit is only roughly located: the direction is right, the distance is off by up to a share of itself</summary>
        private static Vector3 Fuzz(Vector3 pos, Vector3 from, float share)
        {
            Vector3 d = pos - from; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 3f || share <= 0f) return pos;
            Vector3 along = d / dist, side = new Vector3(along.z, 0f, -along.x);
            return OnGround(pos + along * dist * UnityEngine.Random.Range(-share, share) + side * dist * UnityEngine.Random.Range(-share, share), pos.y);
        }

        /// <summary>Ground point under a position (rays on Default and Ground layers), else the position at the reference height.</summary>
        private static Vector3 OnGround(Vector3 p, float refY)
        {
            RaycastHit h;
            if (Physics.Raycast(new Vector3(p.x, refY + 3f, p.z), Vector3.down, out h, 8f, (1 << 0) | (1 << 14), QueryTriggerInteraction.Ignore)) return h.point;
            return new Vector3(p.x, refY, p.z);
        }

        /// <summary>Velocity of an object from its Rigidbody or CharacterController (zero when neither).</summary>
        private static Vector3 VelocityOf(GameObject go)
        {
            var rb = go != null ? go.GetComponent<Rigidbody>() : null;
            if (rb != null) return rb.velocity;
            var cc = go != null ? go.GetComponent<CharacterController>() : null;
            return cc != null ? cc.velocity : Vector3.zero;
        }

        /// <summary>(1.4.10) a search that found nothing: the NPC gives up and tells its friends (same faction, humans, within AllClearRange) who are on the way to the same spot - or searching it - "there's nothing": they give up too and head back (never those fighting something they see)</summary>
        private static void SearchedNothing(Agent a, string why)
        {
            var g = a.Ghost;
            Vector3 spot = g != null ? g.Pos : a.T.position;
            GiveUp(a, why);
            float range = NpcaiSettings.AllClearRange.Value;
            if (!a.Human || g == null || range <= 0f) return;
            int told = 0;
            foreach (var kv in _agents)
            {
                var f = kv.Value;
                if (f == a || f.Owner == null || !f.Human || f.Tag != a.Tag || f.T.parent != null) continue;
                if (f.State != State.Investigate && f.State != State.Search) continue;
                var fg = f.Ghost;
                if (fg == null || (fg != g && (fg.Pos - spot).sqrMagnitude > 8f * 8f)) continue;
                if ((f.T.position - a.T.position).sqrMagnitude > range * range) continue;
                f.Done[fg.Id] = fg.Moved;           // been told about it: not taken again unless there is news (the ghost moves)
                GiveUp(f, "told by " + Name(a.Owner) + " there's nothing at ghost #" + fg.Id);
                told++;
            }
            if (told > 0 && WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: " + Name(a.Owner) + " found nothing at ghost #" + g.Id + ", tells " + told + " friend(s) to go back");
        }

        /// <summary>Idle: the search round is walked (or there was none and it looked around) - the search ends now, not when SearchSeconds run out</summary>
        internal static void EndSearch(GameObject owner, string why)
        {
            var a = Get(owner);
            if (a != null && a.State == State.Search) SearchedNothing(a, why);
        }

        /// <summary>The agent gives up: ghost released, idle, target and sight build-up cleared; an enemy shout is news again.</summary>
        private static void GiveUp(Agent a, string why)
        {
            Release(a);
            a.State = State.Idle; a.Target = null; a.SeenFor = 0f;
            a.Heard.RemoveWhere(x => x < 0);    // the alert is over: a new shout from the same enemy is news again
            if (WildWasteland.Core.Log.L2) Log(a, "gives up (" + why + "), idle");
        }

        /// <summary>An NPC takes a ghost of equal or higher rank than the one it has, or any ghost with newer news about the same subject (it went to check a shout about the player; the player now shoots elsewhere: that is where the player is). Never while it sees its target.</summary>
        private static bool Assign(Agent a, Ghost g, Src prio, float now)
        {
            if (a.Deaf && prio >= Src.Engine && prio <= Src.Shout) return false;
            if(a.CrewBoarding||a.Tag=="MutantSanity"||(SimpleBandits.Has(a.Owner)&&!SimpleBandits.Fighting(a.Owner)))return false;
            if (g == null || a.State == State.Combat || a.State == State.Hide || g.Retired || a.AmbushDist > 0f) return false;      // (1.3.2) an ambusher in wait ignores sounds
            float doneAt;
            if (a.Done.TryGetValue(g.Id, out doneAt) && g.Moved <= doneAt + 0.01f) return false;   // been there, nothing new about it
            if (a.Ghost == g)
            {
                if (Rank(prio) > Rank(a.GhostPrio)) a.GhostPrio = prio;
                if (a.State != State.Investigate) { a.State = State.Investigate; Budget(a, g); }   // Search, or Idle after Arrived() re-made the ghost it holds
                return true;
            }
            if (a.Ghost != null)
            {
                bool newerSameSubject = g.Subject != null && a.Ghost.Subject == g.Subject && g.Moved >= a.Ghost.Moved;
                if (!newerSameSubject && Rank(prio) < Rank(a.GhostPrio)) return false;
            }
            Release(a);
            a.Ghost = g; a.GhostPrio = prio; g.Holders.Add(a);
            a.State = State.Investigate; a.SeenFor = 0f;
            Budget(a, g);
            StartFlank(a, g);
            if(SimpleBandits.Fighting(a.Owner)&&WildWasteland.Core.Log.L2)Log(a,"investigates "+prio+" at ghost #"+g.Id);
            return true;
        }

        /// <summary>(1.1.0) flanking: a human going to check a spot more than FlankDistance away does not walk straight at it - it goes to a point FlankWidth m to one side first (the side alternates between NPCs, so two of them come around both sides), then to the spot. The Detection target meanwhile is a hidden object at that point (the brain / Idle walk to it like to a ghost). Trackers go straight.</summary>
        private static void StartFlank(Agent a, Ghost g)
        {
            a.Flanking = false;
            if (!a.Human || a.Tracker || NpcaiSettings.FlankWidth.Value <= 0f) return;
            Vector3 to = g.Pos - a.T.position; to.y = 0f;
            float d = to.magnitude;
            if (d < Mathf.Max(5f, NpcaiSettings.FlankDistance.Value)) return;
            Vector3 side = new Vector3(to.z, 0f, -to.x) / d * ((a.Owner.GetInstanceID() & 1) == 0 ? 1f : -1f);
            Vector3 p = OnGround(a.T.position + to * 0.6f + side * NpcaiSettings.FlankWidth.Value, g.Pos.y);
            if (a.FlankObj == null) { a.FlankObj = new GameObject("NPCAI.Flank") { hideFlags = HideFlags.HideAndDontSave }; if (_ghostRoot != null) a.FlankObj.transform.SetParent(_ghostRoot, false); }
            a.FlankObj.transform.position = p; a.FlankPos = p; a.Flanking = true;
            if (WildWasteland.Core.Log.L2) Log(a, "flanks ghost #" + g.Id + " via a point " + NpcaiSettings.FlankWidth.Value.ToString("0") + " m to the " + (side.x * to.z - side.z * to.x > 0f ? "right" : "left"));
        }
        /// <summary>Ends the flank detour (the ghost itself is next).</summary>
        private static void EndFlank(Agent a) { a.Flanking = false; }

        /// <summary>where the NPC is walking to right now: its flank point, else its ghost (Idle's ghost walk and the brain agree with Known())</summary>
        internal static bool GoalOf(GameObject owner, out Vector3 goal)
        {
            var a = Get(owner); goal = Vector3.zero;
            if (a == null || a.Ghost == null) return false;
            goal = a.Flanking ? a.FlankPos : a.Ghost.Pos;
            return true;
        }
        /// <summary>True while the NPC walks to its flank point.</summary>
        internal static bool IsFlanking(GameObject owner) { var a = Get(owner); return a != null && a.Flanking; }

        /// <summary>time allowed to reach a ghost before searching from wherever the NPC got to (unreachable spots, a cave with no way out)</summary>
        private static void Budget(Agent a, Ghost g)
        {
            a.InvestigateUntil = Time.time + Mathf.Max(5f, NpcaiSettings.GhostTimeout.Value);
            a.InvestigateSince = Time.time;
        }

        /// <summary>the brain gets nowhere on the way: the NPC does NOT start searching (only arrival or a stale ghost, GhostTimeout, ends the walk) - the brain rests a moment and tries again</summary>
        internal static bool CannotReach(GameObject owner)
        {
            var a = Get(owner);
            if (a == null || a.State != State.Investigate || a.Ghost == null) return false;
            if (Time.time - a.LastLog > 5f) { a.LastLog = Time.time; if (WildWasteland.Core.Log.L2) Log(a, "gets nowhere toward ghost #" + a.Ghost.Id + " for now, keeps trying"); }
            return false;
        }

        /// <summary>A deaf agent (interior population) drops a noise-ranked investigation it holds.</summary>
        internal static void ClearDeafInvestigation(Agent a)
        {
            if (!a.Deaf || a.Ghost == null || a.GhostPrio < Src.Engine || a.GhostPrio > Src.Shout || a.State != State.Investigate && a.State != State.Search) return;
            Release(a); a.State = State.Idle;
        }
        /// <summary>Releases the agent's ghost (a ghost without holders dies; one left only to searchers is retired).</summary>
        private static void Release(Agent a)
        {
            a.Flanking = false;
            var g = a.Ghost;
            a.Ghost = null; a.GhostPrio = Src.None;
            if (g == null) return;
            g.Holders.Remove(a);
            if (g.Holders.Count == 0) { int i = _ghosts.IndexOf(g); if (i >= 0) KillGhost(i); }
            else RetireIfSearched(g);
        }

        /// <summary>nobody is on the way to it any more, only searchers are left: the ghost is retired - never handed out, merged into or relayed again, and it dies with its last searcher (as every ghost dies with its last holder)</summary>
        private static void RetireIfSearched(Ghost g)
        {
            if (g.Retired) return;
            foreach (var h in g.Holders) if (h.State == State.Investigate) return;
            g.Retired = true;
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: ghost #" + g.Id + " searched by " + g.Holders.Count + ", retired");
        }

        /// <summary>Finds a live ghost of the same rank from the same source or place within the merge window and moves it, else makes a new hidden ghost object.</summary>
        private static Ghost GetOrMake(Src src, Vector3 pos, GameObject source, string about, float mergeRadius, float mergeSeconds, float now)
        {
            foreach (var g in _ghosts)
            {
                if (g.Src != src || g.Retired) continue;          // a searched spot: new news makes a new ghost
                bool sameSource = source != null && g.Source == source && now - g.Moved <= mergeSeconds;
                bool samePlace = mergeRadius > 0f && now - g.Moved <= mergeSeconds && (g.Pos - pos).sqrMagnitude <= mergeRadius * mergeRadius;
                if (sameSource || samePlace)
                {
                    g.Pos = pos; g.Moved = now; if (g.Obj != null) g.Obj.transform.position = pos;
                    return g;
                }
            }
            var n = new Ghost { Id = _nextGhostId++, Pos = pos, Src = src, Born = now, Moved = now, Source = source, About = about ?? "" };
            if (_ghostRoot == null) { var r = new GameObject("NPCAI.Ghosts") { hideFlags = HideFlags.HideAndDontSave }; UnityEngine.Object.DontDestroyOnLoad(r); _ghostRoot = r.transform; }
            n.Obj = new GameObject("NPCAI.Ghost#" + n.Id) { hideFlags = HideFlags.HideAndDontSave };
            n.Obj.transform.SetParent(_ghostRoot, false);
            n.Obj.transform.position = pos;
            _ghosts.Add(n);
            if (WildWasteland.Core.Log.L2 && (src == Src.Vision || src == Src.Hit)) NpcaiRuntime.Deep("Senses: ghost #" + n.Id + " " + src + " (" + n.About + ") at " + pos);
            return n;
        }

        /// <summary>Removes a ghost: its holders go idle, its object is destroyed.</summary>
        private static void KillGhost(int i)
        {
            var g = _ghosts[i];
            foreach (var h in g.Holders)
            {
                if (h.Ghost != g) continue;
                h.Ghost = null; h.GhostPrio = Src.None;
                if (h.State == State.Investigate || h.State == State.Search) { h.State = State.Idle; h.SeenFor = 0f; h.Heard.RemoveWhere(x => x < 0); }
            }
            g.Holders.Clear();
            if (g.Obj != null) UnityEngine.Object.Destroy(g.Obj);
            _ghosts.RemoveAt(i);
        }

    }
}
