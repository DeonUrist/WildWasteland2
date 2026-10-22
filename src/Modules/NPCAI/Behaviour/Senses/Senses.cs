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

namespace WildWasteland.Modules.NPC
{
    // How NPCs notice things ("the senses"). Replaces the game's SensorToolkit detection for every NPC that stands on its own.
    //
    // Vanilla (asset read 2026-10-02): the "Sensors" child carries a RangeSensor (100 m sphere, Player + Actor layers, a tag filter per
    // faction) feeding a LOSSensor (1 ray from the sensor at chest height, 240 deg wide, blockers Default/Item/Door/Ground, visible when the
    // moving average of 10 pulses is >= 0.2, pulsing every 0.5 s). The Detection FSM polls it every frame: notDetected -> inLOS (Attack +
    // RangedAttackWait on, "Alert" sent) -> searchRange (range sensor, through walls, 30 s) -> checkLOS -> notDetected. A health change
    // also jumps to inLOS. Nothing hears anything; only Sprokka's Alert FSM warns neighbours.
    //
    // Here: the Detection FSM's SensorGetDetections actions are answered by the mod (Harmony prefix: we write detectedObj and fire the
    // state's own events), the vanilla sensors are switched off, and what an NPC "knows" is one of:
    // - a target it sees: a 100 deg cone from the head, a ray from the head to the target's head or body (blockers Default/Item/Door/
    //   Ground), range SightRange in daylight shrinking to DarkSightRange in full darkness (Enviro's main light), full range for a player
    //   with the flashlight on; NoticeSeconds in view -> combat; LoseSeconds out of view -> a ghost at the last seen spot;
    // - a ghost: a hidden object at a position, fed to detectedObj so the game's own Attack FSM (and the brain) go there. Ghosts come from
    //   sight lost (VISION), being hit (HIT), a taunt (TAUNT: a human's combat shout tells same-faction humans within TauntRange where the
    //   shouter's target or ghost is, and tells its enemies where the shouter is), gunshots (GUNSHOT: every NPC within the gun's noise
    //   range gets a ghost at the shooter), a thrown item (THROWN, ThrowRange around where it lands) and the player's running engine
    //   (ENGINE, 50-150 m by horsepower, half while idling). A ghost is shared by everyone the same event alerted; an NPC only takes a
    //   ghost of equal or higher rank than the one it has (VISION > GUNSHOT (every sound) > ENGINE, newest wins at equal rank; newer news about the same subject always wins)
    //   and never while it sees its target. Arriving at the ghost (ArriveDistance) without seeing anything starts a SearchSeconds
    //   look-around (the brain turns the body), then the NPC drops to idle where it stands. A ghost nobody holds any more disappears.
    // Everything is saved per slot (ghosts, who holds which, combat targets, search time left) and restored after a load.
    // [Senses] Enabled = false restores the game's sensors and detection untouched.
    internal static partial class Senses
    {
        internal enum Src { None = 0, Engine = 1, Thrown = 2, Gunshot = 3, Shout = 4, Hit = 5, Vision = 6 }
        /// <summary>What an agent is doing: idle, fighting a seen target, going to check a ghost, searching around it, or hiding in cover (keeps its target, takes no ghosts).</summary>
        internal enum State { Idle, Combat, Investigate, Search, Hide }   // Hide (1.1.0): in cover - keeps its target, takes no ghosts until the brain ends the cover

        /// <summary>Ranks (1.1.0, two classes): SEEN (a sighting, a hit) beats HEARD (a gunshot, an explosion, a shout, an engine, a thrown item); within a class the newest news wins (Assign). A shout passes on a HEARD-class ghost.</summary>
        internal static int Rank(Src s)
        {
            switch (s) { case Src.None: return 0; case Src.Vision: case Src.Hit: return 2; default: return 1; }
        }

        internal sealed class Ghost
        {
            public int Id; public Vector3 Pos; public Src Src; public float Born, Moved; public GameObject Obj, Source; public string About = "";
            public GameObject Subject;               // who the ghost is about (the player, an NPC): newer news about the same subject always wins
            public readonly List<Agent> Holders = new List<Agent>();
            public bool Retired;                     // nobody is on the way any more (only searchers left): never handed out, merged into or relayed again
        }

        internal sealed class Agent : Core.IAnimalActor
        {
            GameObject Core.IAnimalActor.Owner { get { return Owner; } }
            Transform Core.IAnimalActor.T { get { return T; } }
            Collider Core.IAnimalActor.Col { get { return Col; } }
            GameObject Core.IAnimalActor.Target { get { return Target; } }
            bool Core.IAnimalActor.Combat { get { return State==Senses.State.Combat; } }
            bool Core.IAnimalActor.Quiet { get { return State==Senses.State.Idle; } }
            bool Core.IAnimalActor.HasGhost { get { return Ghost!=null; } }
            bool Core.IAnimalActor.Blown { get { return Blown; } }
            bool Core.IAnimalActor.Travel { get { return Travel; } set { Travel=value; } }

            public GameObject Owner; public Transform T; public Collider Col; public Transform Head;
            public PlayMakerFSM Detection, Attack; public FsmGameObject DetectedVar;
            public string Tag; public HashSet<string> Hostile; public bool Human;
            public bool Travel;           // (1.4.0) a hunter travelling underground (Burrow): its senses pause, nobody can see it
            public bool Deaf, Interior;   // Towns' interior population policy; cleared when Towns stops
            public bool Spider, Lit, CaveWeb; public float CaveLight = 1f, NextCaveLight; public GameObject LightSource;
            public float AmbushDist; public int Cone; public bool Burrowed;      // (1.3.0) vision group (0 human, 1 insect, 2 beast, 3 brute, 4 boss, 5 burrower); a burrowed creature sees all around
            public HashSet<string> BaseHostile; public PlayMakerFSM PlayerIsEnemy;   // the prefab's own enemies, and its faction-relation FSM (Coyotes)
            public State State; public GameObject Target; public Ghost Ghost; public Src GhostPrio;
            public bool CrewBoarding; public GameObject CrewEnemy; public FsmFloat CrewEnemyHealth;
            public float SeenFor, UnseenFor, SearchUntil, NextLook, Stagger, LastLog, InvestigateUntil, InvestigateSince; public Vector3 LastSeen;
            public string SightMiss;
            public float UnseenSince = -1f;
            public GameObject Pursue; public int Pursuits, PursuitsTotal;     // the target it lost from sight, and how many more guesses it will make about where it went
            public bool Tracker;          // a hound / four-legged mutant: follows the real position, never a guess
            public float NextShout;       // the combat shout (taunt): not before this
            public Vector3 LastSeenVel;   // the target's velocity when last seen (the first guess goes that way)
            public Behaviour[] Sensors; public bool SensorsOff;
            public bool InStorm;          // inside a sandstorm (refreshed once a second)
            public float SeesAt = -10f; public GameObject SeesTarget; public bool Sees;   // (perf) SeesNow's line check, kept 0.1 s
            public bool Blown;            // within a tornado funnel's shove radius (refreshed once a second)
            public GameObject FlankObj; public Vector3 FlankPos; public bool Flanking;   // (1.1.0) a human goes to a ghost via a point to one side of it first
            public readonly Dictionary<int, float> Done = new Dictionary<int, float>();   // ghosts it reached (id -> the ghost's Moved then): never given again unless the ghost has newer news
            public readonly HashSet<int> Heard = new HashSet<int>();   // ghosts it already got from a friend's shout, and enemy shouters (instance ids, negated) it already went to: a shout never re-sends them
            public GameObject Fought; public float FoughtUntil; public bool MemTrack, Crew;   // (crew AI) whom it fought (the player), until when it knows where he is, sight lost but kept by that memory
            public Core.EntityId Id; public Core.NpcMode Published; public string PrefabName; public Vector3 LastPos;   // (WW) ModAPI events: entity id, last published mode, prefab and position for NpcDespawned
        }

        private static readonly Dictionary<int, Agent> _agents = new Dictionary<int, Agent>();
        internal static Dictionary<int, Agent>.ValueCollection AllAgents { get { return _agents.Values; } }   // read-only use (Idle)
        /// <summary>Faction tag of a registered agent (null when the object is not one).</summary>
        internal static string TagOf(GameObject owner) { Agent a; return owner != null && _agents.TryGetValue(owner.GetInstanceID(), out a) ? a.Tag : null; }
        /// <summary>True while the agent stands in a tornado funnel's shove radius (refreshed once a second).</summary>
        internal static bool Blown(GameObject owner) { Agent a; return owner != null && _agents.TryGetValue(owner.GetInstanceID(), out a) && a.Blown; }
        private static readonly HashSet<int> _ignored = new HashSet<int>();
        private static readonly List<Ghost> _ghosts = new List<Ghost>();
        private static readonly List<int> _dead = new List<int>();
        private static readonly List<Agent> _scratch = new List<Agent>();
        private static int _nextGhostId = 1, _created;
        private static float _nextIgnoredClear;
        private static HashSet<string> _presentTags = new HashSet<string>();   // factions with a registered NPC (Look skips the NPC loop when none is hostile)
        private static readonly Dictionary<int, string> _prefabOf = new Dictionary<int, string>();
        private static float _nextMaint, _nextEngine, _nextLightLog, _lightCache, _lightAt = -10f;
        private static GameObject _player, _playerHead, _playerCar; private static Transform _flashlight; private static float _nextPlayerFind;
        private static Vector3 _playerPrev; private static bool _playerMoving;   // (WW) footsteps: the player moved on foot since the last tick (horizontal speed over 0.3 m/s)
        private static bool _playerInStorm;
        private static PlayMakerFSM _inCarFsm, _grabFsm; private static FsmGameObject _inCarVar, _grabItemVar; private static string _grabState;
        private static Rigidbody _thrown; private static float _thrownRest, _thrownSince;
        private static Transform _ghostRoot;
        private const int Blockers = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 11) | (1 << 14);   // Default, Car, Item, Door, Ground (the game's bullet obstruction set)
        private const int SightBlockers = Blockers & ~(1 << 8);   // cars do not hide the player or enemy NPCs

        /// <summary>The player's root object as found by the senses (null in menus).</summary>
        internal static GameObject PlayerObj { get { return _player; } }
        /// <summary>True while the player drives a car (Pounce: no leaping at a car; Burrow: "behind the player" is behind the camera).</summary>
        internal static bool PlayerInCar { get { return PlayerCar() != null; } }       // (1.5.0) Pounce: no leaping at a car   // (1.4.0) Burrow: "behind the player" is behind the camera
        /// <summary>The senses run: module on and [NPCAI] EnableNpcDetection on.</summary>
        internal static bool On { get { return NpcaiRuntime.Running && NpcaiSettings.SensesEnabled.Value; } }

        /// <summary>Scene load: every agent, ghost, cache and storm reading is dropped, the senses persistence is reset.</summary>
        public static void OnSceneLoaded()
        {
            SimpleBandits.Clear();
            foreach (var g in _ghosts) if (g.Obj != null) UnityEngine.Object.Destroy(g.Obj);
            foreach (var kv in _agents) if (kv.Value.FlankObj != null) UnityEngine.Object.Destroy(kv.Value.FlankObj);
            _ghosts.Clear(); _agents.Clear(); _ignored.Clear(); _remember.Clear(); _hpOf.Clear(); _prefabOf.Clear(); _presentTags.Clear(); _presentNext.Clear(); _maintKeys.Clear(); _maintPos = 0; _engineAbout.Clear(); _engineRbCar = null; _engineRb = null; _flashMissUntil = 0f; Nwh.Clear();
            _player = null; _playerHead = null; _playerCar = null; _flashlight = null; _inCarFsm = null; _grabFsm = null; _thrown = null;
            foreach (var kv in _byTag) kv.Value.Clear(); _byTagFrame = -1;
            Storm.Reset();
            ResetSpiderLights();
            _hornFsm = null; _azure = null; _nextAzureFind = 0f;
            Persist.ResetForScene();
        }

        // ---------- per frame ----------
        /// <summary>Job "NPCAI.Senses" (every frame): the once-a-second maintenance (storms, ghosts, memories, factions present), the capped per-NPC looks, the engine / horn and the thrown-item noises.</summary>
        public static void Tick()
        {
            float now = Time.time, dt = Time.deltaTime;
            bool on = On;
            if (now >= _nextMaint)
            {
                _nextMaint = now + 1f;
                if (_player != null && NpcaiSettings.StormsEnabled.Value) Storm.Refresh(); else Storm.Reset();      // menu / loading / (WW) storms toggle off: no storms, no lookups
                _playerInStorm = _player != null && Storm.InForSenses(_player.transform.position);
                // (perf) the per-agent part runs as a round robin over the next 8 frames (MaintStep): every agent is still visited once a
                // second, the factions present are swapped in when the round is complete
                if (_maintPos < _maintKeys.Count) { _maintPer = _maintKeys.Count; MaintStep(on); }     // below 8 fps: the last round finishes first (no agent / faction swap starved)
                _maintKeys.Clear(); foreach (var kv in _agents) _maintKeys.Add(kv.Key);
                _maintPos = 0; _maintPer = Mathf.Max(1, (_maintKeys.Count + 7) / 8); _presentNext.Clear();
                if (now >= _nextIgnoredClear) { _nextIgnoredClear = now + 120f; _ignored.Clear(); }   // ids of parented / unfinished NPCs: re-evaluated, never kept for the whole session
                if (_handover.Count > 0) { _dead.Clear(); foreach (var kv in _handover) if (now - kv.Value.Value > 30f) _dead.Add(kv.Key); foreach (var k in _dead) _handover.Remove(k); }
                if (_remember.Count > 0) { _dead.Clear(); foreach (var kv in _remember) if (now >= kv.Value.Value || kv.Value.Key == null) _dead.Add(kv.Key); foreach (var k in _dead) _remember.Remove(k); }
                PruneCrewOrders(now);
                for (int i = _ghosts.Count - 1; i >= 0; i--)
                {
                    var g = _ghosts[i];
                    g.Holders.RemoveAll(h => h.Owner == null);
                    // nobody wants it, or no news about it for 10 min (a restore in progress keeps its ghosts until their NPCs register)
                    if ((g.Holders.Count == 0 && !Persist.Restoring) || now - g.Moved > 600f) KillGhost(i);
                }
                FindPlayer();
                if (on && _player != null) RefreshSpiderLights();
            }
            if (_maintPos < _maintKeys.Count) MaintStep(on);
            if (on && _player != null && _spiderItemPos >= 0) SpiderItemStep();
            // (WW) the save / load watcher is SaveBus.StateChanged (OnSaveLoadState), the Apocapatrol crew hooks are Core.Services events
            // (NpcModule), the shout key is a Core.Hotkeys binding (ShoutPressed)
            if (!on) return;
            if (_player == null) return;

            _sightChecks = 0;
            float baseLook = Mathf.Max(0.05f, NpcaiSettings.LookInterval.Value);
            int looks = 0, lookCap = Mathf.Max(1, Mathf.CeilToInt(_agents.Count * dt / baseLook) + 1);
            Vector3 ppos = _player.transform.position;
            { Vector3 mv = ppos - _playerPrev; mv.y = 0f; float sp = dt > 0f ? mv.magnitude / dt : 0f; _playerMoving = sp > 0.3f && sp < 30f && _playerCar == null; _playerPrev = ppos; }   // (WW) footsteps: a teleport / origin jump does not count, nor driving
            float sr = Mathf.Max(NpcaiSettings.SightRange.Value, NpcaiSettings.DarkSightRange.Value), f2 = Mathf.Max(80f, sr + 20f), f4 = Mathf.Max(150f, sr + 50f), far2 = f2 * f2, far4 = f4 * f4;
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner == null || a.T.parent != null) continue;       // seated in a car (Apocapatrol): not ours
                if (a.Travel || now < a.NextLook) continue;                 // (1.4.0) underground: keeps its target, sees nothing
                if (looks >= lookCap || _sightChecks >= SightCheckBudget) continue;   // (perf) over this frame's look / NPC-sight cap: looks next frame (NextLook stays overdue)
                looks++;
                // (perf) the stagger is the creation phase only (it used to be added to every look: 0.15-0.26 s); far beyond the player's
                // sight range (SightRange + 20 m / + 50 m, at least 80 / 150 m: nothing there can see the player before its next look) an
                // NPC looks half / a quarter as often - NPC vs NPC only, so the player is noticed exactly as before
                float interval = baseLook;
                float pd = (a.T.position - ppos).sqrMagnitude;
                if (pd > far4) interval *= 4f; else if (pd > far2) interval *= 2f;
                a.NextLook += interval + a.Stagger; if (a.NextLook < now) a.NextLook = now + UnityEngine.Random.value * (interval + a.Stagger);     // long skipped (seated, underground): a fresh phase (the stagger stays in the period, as before: same look rate, same SeenFor build-up)
                try { Look(a, interval, now); PublishState(a); }
                catch (Exception e) { NpcaiRuntime.Error("Senses: " + e); }
            }
            if (now >= _nextEngine) { _nextEngine = now + 0.5f; try { Engine(now); } catch (Exception e) { NpcaiRuntime.Error("Senses engine: " + e); } }
            try { Thrown(now, dt); } catch (Exception e) { NpcaiRuntime.Error("Senses throw: " + e); }
        }

        // (perf) D5: the per-agent second's maintenance, _maintPer agents a frame (was all of them in one frame)
        private static readonly List<int> _maintKeys = new List<int>();
        private static int _maintPos, _maintPer = 1;
        private static HashSet<string> _presentNext = new HashSet<string>();
        /// <summary>One slice of the per-agent maintenance round (vanilla sensors on/off, Coyote relation, done-ghost pruning, storm and funnel flags, mode events).</summary>
        private static void MaintStep(bool on)
        {
            int end = Mathf.Min(_maintKeys.Count, _maintPos + _maintPer);
            _dead.Clear();
            for (; _maintPos < end; _maintPos++)
            {
                int key = _maintKeys[_maintPos];
                Agent a;
                if (!_agents.TryGetValue(key, out a)) continue;
                if (a.Owner == null) { if (a.Ghost != null) { a.Ghost.Holders.Remove(a); } if (a.FlankObj != null) UnityEngine.Object.Destroy(a.FlankObj); _dead.Add(key); PublishDespawned(a); }
                else
                {
                    if(SimpleBandits.Has(a.Owner)){if(on&&!a.SensorsOff)Sensors(a,false);_presentNext.Add(a.Tag);continue;}
                    if (!on && a.SensorsOff) Sensors(a, true);
                    else if (on && !a.SensorsOff) Sensors(a, false);        // switched back on: the game's sensors go quiet again
                    if (a.PlayerIsEnemy != null||a.Owner.CompareTag("Coyotes")) Relation(a, true);
                    if (a.Done.Count > 16) PruneDone(a);
                    a.LastPos = a.T.position; PublishState(a);
                    _presentNext.Add(a.Tag);
                    a.InStorm = Storm.AnyForSenses && Storm.InForSenses(a.T.position);
                    bool blown = Storm.AnyFunnel && Storm.InBlast(a.T.position);
                    if (blown != a.Blown) { a.Blown = blown; if (blown) Log(a, "is shoved by a tornado"); }
                }
            }
            foreach (var k in _dead) _agents.Remove(k);
            if (_maintPos >= _maintKeys.Count)
            {
                // the round is complete: the factions present at its start (an NPC registered since counts from the next round, as before)
                var t = _presentTags; _presentTags = _presentNext; _presentNext = t;
            }
        }

        // ---------- sight ----------
        /// <summary>One look of an NPC: crew orders and simple bandits first, tornado reset, search/investigate progress, then sight of the player and of hostile NPCs (nearest visible first) with the notice delay and the ambush hold, and target loss after LoseSeconds.</summary>
        private static void Look(Agent a, float interval, float now)
        {
            if (a.Interior) a.AmbushDist = 0f;
            if(a.CrewBoarding)return;
            if(!ReferenceEquals(a.CrewEnemy,null))
            {
                if(a.CrewEnemy==null||!a.CrewEnemy.activeInHierarchy||(a.CrewEnemyHealth!=null&&a.CrewEnemyHealth.Value<=0f))CrewOrder(a.Owner,null,false);
                else{Engage(a,a.CrewEnemy);if(a.DetectedVar!=null)a.DetectedVar.Value=a.CrewEnemy;return;}
            }
            if(a.Tag=="MutantSanity"||(SimpleBandits.Has(a.Owner)&&a.Ghost==null&&a.State!=State.Search)){SimpleLook(a,now);return;}
            // shoved by a tornado: it forgets what it was doing unless it sees the player right now (sight still works, hearing doesn't)
            if (a.Blown && a.State != State.Idle && !(a.State == State.Combat && a.Target == _player && a.UnseenFor < 0.5f))
            {
                Release(a);
                a.State = State.Idle; a.Target = null; a.SeenFor = 0f; a.Pursue = null;
                Log(a, "blown about by a tornado, forgets what it was doing");
            }
            if (a.State == State.Search && now >= a.SearchUntil) { SearchedNothing(a, "nothing here"); }
            if (a.State == State.Investigate && a.Ghost != null)
            {
                Vector3 to = a.Ghost.Pos - a.T.position; to.y = 0f;
                if (to.magnitude <= Brain.ArriveFor(a.Owner)) Arrived(a, now);
                // no searching on the way: only a ghost nobody has refreshed for GhostTimeout seconds (since it was given to this NPC or last
                // moved/renewed) lets the NPC settle for searching where it got to
                else if (now >= Mathf.Max(a.InvestigateSince, a.Ghost.Moved) + Mathf.Max(5f, NpcaiSettings.GhostTimeout.Value))
                { if (WildWasteland.Core.Log.L2) Log(a, "ghost #" + a.Ghost.Id + " went stale (" + NpcaiSettings.GhostTimeout.Value.ToString("0") + " s without news, " + to.magnitude.ToString("0") + " m left), searches from here"); Arrived(a, now); }
            }
            if ((a.State == State.Combat || a.State == State.Hide) && a.Target == null) { a.State = State.Idle; Log(a, "target gone"); }
            if (a.Flanking && a.State == State.Investigate && (a.FlankPos - a.T.position).sqrMagnitude <= 9f) EndFlank(a);   // at the flank point: on to the ghost itself
            if (a.Spider) SpiderLook(a, now);

            // candidates: the player (if this faction hunts players) and every other NPC of a hostile faction
            float d2max = NpcaiSettings.SightRange.Value * NpcaiSettings.SightRange.Value;
            a.SightMiss = "outside maximum sight range or no hostile candidate";
            Vector3 eye = Eye(a);
            GameObject best = null; float bestD = float.MaxValue;
            if (a.Hostile.Contains("Player") && _player != null && (_player.transform.position - a.T.position).sqrMagnitude <= d2max)
            {
                if (Visible(a, eye, _player, true, _playerInStorm)) { best = _player; bestD = (_player.transform.position - a.T.position).sqrMagnitude; }
            }
            if (!a.Blown && HostileNpcAround(a))
            {
                // (perf, README 8 #2) only the agents of the hostile factions (grouped once per frame), nearest first: the first visible one
                // is the nearest visible one, so one or two line checks usually decide it (was: every agent tested, every closer one ray-cast)
                Vector3 ap = a.T.position;
                if (_byTagFrame != Time.frameCount) GroupByTag();
                _cands.Clear();
                foreach (var tag in a.Hostile)
                {
                    List<Agent> group;
                    if (tag == "Player" || !_byTag.TryGetValue(tag, out group)) continue;
                    for (int i = 0; i < group.Count; i++)
                    {
                        var b = group[i];
                        if (b == a || b.Owner == null) continue;
                        float d2 = (b.T.position - ap).sqrMagnitude;
                        if (d2 > d2max || d2 >= bestD) continue;
                        _cands.Add(new KeyValuePair<float, Agent>(d2, b));
                    }
                }
                if (_cands.Count > 1) _cands.Sort(NearestFirst);
                for (int i = 0; i < _cands.Count; i++)
                {
                    var b = _cands[i].Value;
                    _sightChecks++;
                    if (!Visible(a, eye, b.Owner, false, b.InStorm)) continue;
                    best = b.Owner; bestD = _cands[i].Key; break;
                }
            }
            if (best != null)
            {
                a.SeenFor += interval;
                if (a.State != State.Combat || a.Target != best)
                {
                    // noticing takes longer at a distance and in the dark: NoticeSeconds up close (<= 15 m), NoticeFar at the edge of sight,
                    // and up to twice that in full darkness
                    float need = NoticeNeed(Mathf.Sqrt(bestD));
                    // (1.3.2) an ambusher lying in wait (a burrowed scorpion) watches you come and only engages inside its ambush distance
                    bool holding = false;
                    if (a.AmbushDist > 0f && (!a.Spider || SpiderDark(a))) { Vector3 fl = best.transform.position - a.T.position; fl.y = 0f; holding = fl.sqrMagnitude > a.AmbushDist * a.AmbushDist; }
                    if (a.CaveWeb) { need = 0f; holding = false; }
                    if (a.SeenFor >= need && !holding) Engage(a, best);
                }
            }
            else
            {
                a.SeenFor = Mathf.Max(0f, a.SeenFor - interval);
            }
            if ((a.State == State.Combat || a.State == State.Hide) && a.Target != null)
            {
                bool seen = best == a.Target || Visible(a, eye, a.Target, a.Target == _player, InStormOf(a.Target));
                if (seen) { a.UnseenSince = -1f; a.UnseenFor = 0f; a.LastSeen = a.Target.transform.position; a.LastSeenVel = VelocityOf(a.Target); if (a.Target == _player) { FoughtFor(a, _player, now, "fights him"); a.MemTrack = false; } }
                else { if (a.UnseenSince < 0f) a.UnseenSince = now; a.UnseenFor = now - a.UnseenSince; }
                if (!seen && a.UnseenFor >= Mathf.Max(0f, NpcaiSettings.LoseSeconds.Value))
                {
                    // (crew AI) it fought the player: out of sight he is not dropped while it remembers - it follows his real position (as a
                    // tracker does), out of a cave too; the memory over, the usual loss (ghost where it last had him, guesses). Not a cave-web spider
                    // (it keeps to its web's 3 m rule).
                    if (a.Target == _player && !a.CaveWeb && (a.Crew || SimpleBandits.Has(a.Owner)) && a.Hostile.Contains("Player") && Remembers(a, _player, now))   // patrol crew only: camp NPCs keep their ghost/search on sight loss
                    {
                        a.LastSeen = _player.transform.position;
                        if (!a.MemTrack) { a.MemTrack = true; if (WildWasteland.Core.Log.L2) Log(a, SightLossText(a, _player) + " - kept: remembers fighting the player (" + (a.FoughtUntil - now).ToString("0") + " s more), follows his real position"); }
                    }
                    else { if (a.MemTrack && WildWasteland.Core.Log.L2) Log(a, "its memory of fighting the player is over"); a.MemTrack = false; LoseTarget(a, now); }
                }
            }
        }

        /// <summary>noticing takes longer at a distance and in the dark: NoticeSeconds up close (&lt;= 15 m), NoticeFar at the edge of sight, and up to twice that in full darkness</summary>
        private static float NoticeNeed(float d)
        {
            float range = Mathf.Max(16f, NpcaiSettings.SightRange.Value);
            float need = Mathf.Lerp(Mathf.Max(0f, NpcaiSettings.NoticeSeconds.Value), Mathf.Max(NpcaiSettings.NoticeSeconds.Value, NpcaiSettings.NoticeFar.Value), Mathf.Clamp01((d - 15f) / (range - 15f)));
            return need * (2f - LightLevel());
        }

        /// <summary>True when the object (player, agent or anything) stands inside a sandstorm for the senses.</summary>
        private static bool InStormOf(GameObject go)
        {
            if (go == null || !Storm.AnyForSenses) return false;
            if (go == _player) return _playerInStorm;
            var b = Get(go.transform.root.gameObject);
            return b != null ? b.InStorm : Storm.InForSenses(go.transform.position);
        }

        private static readonly List<int> _pruneIds = new List<int>();
        /// <summary>Forgets reached-ghost entries of ghosts that no longer exist.</summary>
        private static void PruneDone(Agent a)
        {
            _pruneIds.Clear();
            foreach (var kv in a.Done)
            {
                bool alive = false;
                foreach (var g in _ghosts) if (g.Id == kv.Key) { alive = true; break; }
                if (!alive) _pruneIds.Add(kv.Key);
            }
            foreach (var k in _pruneIds) a.Done.Remove(k);
        }

        // (perf) NPC-vs-NPC sight: agents grouped by faction tag once per frame, candidates sorted nearest first, a per-frame line-check budget
        private static readonly Dictionary<string, List<Agent>> _byTag = new Dictionary<string, List<Agent>>();
        private static readonly List<KeyValuePair<float, Agent>> _cands = new List<KeyValuePair<float, Agent>>();
        private static readonly Comparison<KeyValuePair<float, Agent>> NearestFirst = CompareNearest;
        private static int _byTagFrame = -1, _sightChecks;
        private const int SightCheckBudget = 256;     // NPC-vs-NPC line checks per frame before further looks wait a frame

        /// <summary>Candidate order: nearest first.</summary>
        private static int CompareNearest(KeyValuePair<float, Agent> x, KeyValuePair<float, Agent> y) { return x.Key.CompareTo(y.Key); }

        /// <summary>Groups the live agents by faction tag for this frame (pooled lists).</summary>
        private static void GroupByTag()
        {
            _byTagFrame = Time.frameCount;
            foreach (var kv in _byTag) kv.Value.Clear();
            foreach (var kv in _agents)
            {
                var b = kv.Value;
                if (b.Owner == null || b.Tag == null) continue;
                List<Agent> group;
                if (!_byTag.TryGetValue(b.Tag, out group)) _byTag[b.Tag] = group = new List<Agent>();
                group.Add(b);
            }
        }

        /// <summary>True when a faction this agent hunts has a registered NPC around (last maintenance round).</summary>
        private static bool HostileNpcAround(Agent a)
        {
            foreach (var t in a.Hostile) if (t != "Player" && _presentTags.Contains(t)) return true;
            return false;
        }

        /// <summary>Sight test: God Mode, light / flashlight / spider cave light / storm range, burrowed or travelling targets, cave-web reach, footsteps, the field of view and a clear line from the eyes to the target's head or body.</summary>
        private static bool Visible(Agent a, Vector3 eye, GameObject target, bool isPlayer, bool targetInStorm)
        {
            // the game's God Mode (F11, Player [GODMODE]) moves the Player, camera holder and head to layer 5 (UI) - the vanilla sensors never
            // see that layer, so neither does this sight. Hearing, ghosts and hits are untouched.
            bool tracked = (a.State == State.Combat || a.State == State.Hide) && target == a.Target;
            if (isPlayer && target.layer == 5) { if (tracked) a.SightMiss = "God Mode"; return false; }
            Vector3 tp = target.transform.position;
            Vector3 flat = tp - a.T.position; flat.y = 0f;
            float d = flat.magnitude;
            float range = isPlayer && FlashlightOn() ? NpcaiSettings.SightRange.Value : SightRange();
            if (a.Spider)
            {
                float light = LightLevel() * Mathf.Min(a.CaveLight, SpiderTargetLight(target));
                range = Mathf.Lerp(NpcaiSettings.DarkSightRange.Value, NpcaiSettings.SightRange.Value, light);
                if (a.Lit || (isPlayer && FlashlightOn())) range = NpcaiSettings.SightRange.Value;
            }
            if (a.InStorm || targetInStorm) range *= Storm.SightFactor;     // sand in the air between them
            // (1.3.3) a burrowed creature (only its tail shows) is noticed by other NPCs only from close by
            // (1.4.0) looked up only while any NPC is burrowed or travelling (no cost otherwise); a travelling one is not seen at all
            if (!isPlayer && Burrow.Hidden > 0)
            {
                Agent tb;
                if (_agents.TryGetValue(target.GetInstanceID(), out tb))
                {
                    if (tb.Travel) return false;
                    if (tb.Burrowed) range = Mathf.Min(range, NpcaiSettings.BurrowSeenFrom.Value);
                }
            }
            if (a.CaveWeb) { if ((tp - a.T.position).sqrMagnitude > 9f) return false; range = Mathf.Max(range, 3f); }
            // (WW) footsteps: the player walking within [Detection] FootstepRadius is heard whatever way the NPC faces and whatever the light,
            // as long as the usual sight lines are clear (the same obstruction check: a wall still hides him)
            bool steps = isPlayer && _playerMoving && d <= NpcaiSettings.FootstepRadius.Value * (WeaponsPackCompat.ChainsawDrawn ? 3f : 1f) && !a.CaveWeb;   // refs-ok: REFACTOR-REQUEST B07-11/B07-13 (Compatibility / Presentation status query without contract member)
            if (d > range && !steps) { if (tracked) a.SightMiss = "outside light/storm sight range"; return false; }
            if (!tracked && !steps && Vector3.Angle(a.T.forward, flat) > Mathf.Clamp(ConeOf(a), 10f, 360f) * 0.5f) return false;
            Transform troot = target.transform.root;
            Vector3 head, body;
            Points(target, isPlayer, out head, out body);
            bool clear = Clear(eye, head, troot) || Clear(eye, body, troot);
            if (tracked) a.SightMiss = clear ? null : "head and body sight lines blocked";
            return clear;
        }

        /// <summary>True when nothing but the target (or the player's car around the player) blocks the line from one point to another.</summary>
        private static bool Clear(Vector3 from, Vector3 to, Transform targetRoot)
        {
            RaycastHit hit;
            Vector3 d = to - from; float len = d.magnitude;
            if (len < 0.05f) return true;
            if (!Physics.Raycast(from, d / len, out hit, len, SightBlockers, QueryTriggerInteraction.Ignore)) return true;
            var r = hit.collider.transform.root;
            if (r == targetRoot) return true;
            if (_playerCar != null && r == _playerCar.transform.root && targetRoot == _player.transform.root) return true;   // the player in a car: the car does not hide them
            return false;
        }

        /// <summary>(1.2.1) the eye / head point sits `want` m below the top of the collider (15 cm for a human), but never lower than a quarter of the body height: a 14 cm scorpion's "eye" was at its feet (top - 15 cm), grass and bumps blocked every sight line and it lost the player 1 m in front of it. Creatures get the same distance from the top, kept between the top and the bottom of their height.</summary>
        private static float TopOffset(float height, float want) { return Mathf.Min(want, Mathf.Max(0f, height) * 0.25f); }

        /// <summary>(1.3.0) the arc of vision: humans and zombies [Detection] SightCone (Apocasetter); insects, beasts, brutes, bosses, burrowers have their own</summary>
        internal static float ConeOf(Agent a)
        {
            if (a.Burrowed || a.Spider) return 360f;
            switch (a.Cone)
            {
                case 1: return NpcaiSettings.SightConeInsects.Value;
                case 2: return NpcaiSettings.SightConeBeasts.Value;
                case 3: return NpcaiSettings.SightConeBrutes.Value;
                case 4: return NpcaiSettings.SightConeBosses.Value;
                case 5: return NpcaiSettings.SightConeBurrowers.Value;
                default: return NpcaiSettings.SightCone.Value;
            }
        }

        private static readonly Dictionary<string, string[]> _words = new Dictionary<string, string[]>();
        /// <summary>True when the prefab name contains one of the comma/semicolon-separated words of a config list (split once per list).</summary>
        private static bool NameIn(string prefab, string list)
        {
            if (string.IsNullOrEmpty(list)) return false;
            string[] w;
            if (!_words.TryGetValue(list, out w)) { w = list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries); for (int i = 0; i < w.Length; i++) w[i] = w[i].Trim(); _words[list] = w; }
            foreach (var x in w) if (x.Length > 0 && prefab.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>boss (BossUI FSM) &gt; burrower &gt; brute &gt; insect &gt; beast &gt; the human setting</summary>
        private static int ConeGroupOf(GameObject owner)
        {
            foreach (var f in owner.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "BossUI") return 4;
            string p = PrefabOf(owner);
            if (NameIn(p, NpcaiSettings.ConeBurrowers.Value)) return 5;
            if (NameIn(p, NpcaiSettings.ConeBrutes.Value)) return 3;
            if (NameIn(p, NpcaiSettings.ConeInsects.Value)) return 1;
            if (NameIn(p, NpcaiSettings.ConeBeasts.Value)) return 2;
            return 0;
        }

        /// <summary>Eye position of an agent: its head bone, else near the top of its collider, else 1.5 m up.</summary>
        private static Vector3 Eye(Agent a)
        {
            if (a.Head != null) return a.Head.position;
            if (a.Col != null) { var b = a.Col.bounds; return new Vector3(b.center.x, b.max.y - TopOffset(b.size.y, 0.15f), b.center.z); }
            return a.T.position + Vector3.up * 1.5f;
        }

        /// <summary>Head and body aim points of a sight target (player head bone, agent head / collider centre, else fixed heights).</summary>
        private static void Points(GameObject target, bool isPlayer, out Vector3 head, out Vector3 body)
        {
            if (isPlayer)
            {
                head = _playerHead != null ? _playerHead.transform.position : target.transform.position + Vector3.up * 1.6f;
                body = target.transform.position + Vector3.up * 0.9f;
                return;
            }
            Agent b;
            if (_agents.TryGetValue(target.GetInstanceID(), out b))
            {
                head = b.Head != null ? b.Head.position : (b.Col != null ? new Vector3(b.Col.bounds.center.x, b.Col.bounds.max.y - TopOffset(b.Col.bounds.size.y, 0.1f), b.Col.bounds.center.z) : target.transform.position + Vector3.up * 1.5f);
                body = b.Col != null ? b.Col.bounds.center : target.transform.position + Vector3.up * 0.8f;
                return;
            }
            head = target.transform.position + Vector3.up * 1.5f; body = target.transform.position + Vector3.up * 0.8f;
        }

        /// <summary>sight range for the light right now: DarkSightRange in full darkness, SightRange in daylight</summary>
        internal static float SightRange()
        {
            float L = LightLevel();
            float dark = Mathf.Max(0.5f, NpcaiSettings.DarkSightRange.Value), max = Mathf.Max(dark, NpcaiSettings.SightRange.Value);
            return dark + (max - dark) * L;
        }

        // 0 = full night, 1 = full day, by the game's clock (Azure[Sky]'s AzureTimeController.GetTimeline(), hours 0-24; found in the
        // scene once per 10 s until it is, reset per scene). [Senses] NightHours is a list of hour=percent points, linear in between,
        // wrapping at midnight (Denis, 2026-10-06: 21 h a bit darker, 22 twilight, 23 night, 4 twilight, 5 like 21, 6 bright). No Azure:
        // full day. Cached for 0.5 s; VerboseLog prints the reading every minute.
        private static Type _azureType; private static MethodInfo _azureTime; private static Func<object, float> _azureCall; private static Behaviour _azure; private static bool _azureTried; private static float _nextAzureFind;
        private static float[] _nightH, _nightV; private static string _nightSrc;
        /// <summary>Light level 0 (night) .. 1 (day) from the game clock through the [Senses] NightHours table; cached for 0.5 s.</summary>
        internal static float LightLevel()
        {
            if (Time.time - _lightAt < 0.5f) return _lightCache;
            _lightAt = Time.time;
            float level = 1f, hour = -1f;
            try
            {
                var az = Azure();
                if (az != null) { hour = _azureCall != null ? _azureCall(az) : Convert.ToSingle(_azureTime.Invoke(az, null)); level = NightTable(hour); }
            }
            catch (Exception e) { if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: clock read failed: " + e.Message); }
            _lightCache = level;
            if (WildWasteland.Core.Log.L2 && Time.time >= _nextLightLog)
            {
                _nextLightLog = Time.time + 60f;
                NpcaiRuntime.Deep("Senses: light " + level.ToString("0.00") + (hour >= 0f ? " at " + hour.ToString("0.0") + " h" : " (no clock)") + " -> sight " + SightRange().ToString("0") + " m");
            }
            return level;
        }

        /// <summary>Light level for an hour from the NightHours points (linear in between, wrapping at midnight); the table is parsed once per config value.</summary>
        private static float NightTable(float hour)
        {
            string cfg = NpcaiSettings.NightHours.Value ?? "";
            if (_nightH == null || _nightSrc != cfg)
            {
                _nightSrc = cfg;
                var pts = new List<KeyValuePair<float, float>>();
                foreach (var part in cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOfAny(new[] { '=', ':' }); float h, v;
                    if (eq > 0 && float.TryParse(part.Substring(0, eq).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out h) && float.TryParse(part.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        pts.Add(new KeyValuePair<float, float>(Mathf.Repeat(h, 24f), Mathf.Clamp01(v / 100f)));
                }
                pts.Sort((x, y) => x.Key.CompareTo(y.Key));
                _nightH = new float[pts.Count]; _nightV = new float[pts.Count];
                for (int i = 0; i < pts.Count; i++) { _nightH[i] = pts[i].Key; _nightV[i] = pts[i].Value; }
            }
            int n = _nightH.Length;
            if (n == 0) return 1f;
            if (n == 1) return _nightV[0];
            hour = Mathf.Repeat(hour, 24f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float h0 = _nightH[i], h1 = _nightH[j], span = h1 - h0; if (span <= 0f) span += 24f;     // the last segment wraps midnight
                float t = hour - h0; if (t < 0f) t += 24f;
                if (t <= span) return Mathf.Lerp(_nightV[i], _nightV[j], span > 0f ? t / span : 0f);
            }
            return _nightV[0];
        }

        /// <summary>The scene's AzureTimeController (the game clock), resolved once by reflection and looked up at most every 10 s until found.</summary>
        private static Behaviour Azure()
        {
            if (_azure != null) return _azure;
            if (!_azureTried)
            {
                _azureTried = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { _azureType = asm.GetType("UnityEngine.AzureSky.AzureTimeController"); if (_azureType != null) break; }
                if (_azureType != null) _azureTime = _azureType.GetMethod("GetTimeline", Type.EmptyTypes);
                if (_azureTime == null) _azureType = null;
                else
                    try   // (perf) a compiled call instead of MethodInfo.Invoke + boxing (the Invoke stays as the fallback)
                    {
                        var p = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
                        var call = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Convert(p, _azureType), _azureTime);
                        _azureCall = System.Linq.Expressions.Expression.Lambda<Func<object, float>>(System.Linq.Expressions.Expression.Convert(call, typeof(float)), p).Compile();
                    }
                    catch (Exception e) { _azureCall = null; if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: clock call not compiled (" + e.Message + "), reflection used"); }
            }
            if (_azureType == null || Time.unscaledTime < _nextAzureFind) return null;
            _nextAzureFind = Time.unscaledTime + 10f;
            foreach (var o in Resources.FindObjectsOfTypeAll(_azureType))
            {
                var b = o as Behaviour;
                if (b != null && b.gameObject.scene.IsValid() && b.isActiveAndEnabled) { _azure = b; break; }
            }
            return _azure;
        }

        /// <summary>A field of a type or one of its base types (public or not), for the sensor tag-filter reflection.</summary>
        private static FieldInfo Field(Type t, string name)
        {
            for (var c = t; c != null; c = c.BaseType)
            {
                var f = c.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>(1.7.3) Webs: the player's flashlight while it is on, else null</summary>
        internal static Transform Flashlight { get { return FlashlightOn() ? _flashlight : null; } }

        private static float _flashMissUntil;
        /// <summary>True while the player's flashlight is on (the camera child "Flashlight"; looked up again at most every 2 s when missing).</summary>
        private static bool FlashlightOn()
        {
            if (_flashlight == null)
            {
                if (Time.time < _flashMissUntil) return false;     // (perf) not there: looked for again after 2 s, not on every sight check
                var cam = Core.GameRefs.MainCamera;
                if (cam != null) _flashlight = cam.transform.Find("Flashlight");
                if (_flashlight == null) { _flashMissUntil = Time.time + 2f; return false; }
            }
            return _flashlight.gameObject.activeInHierarchy;
        }

    }
}
