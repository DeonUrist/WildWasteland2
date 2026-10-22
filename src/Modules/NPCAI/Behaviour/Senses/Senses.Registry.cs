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
    /// <summary>Senses part: the agent registry: registration, faction enemies and relations, vanilla sensors, the player and his car, prefab names, shutdown and origin shift.</summary>
    internal static partial class Senses
    {
        // ---------- registry ----------
        /// <summary>The registered agent of an object (null when not registered).</summary>
        internal static Agent AgentFor(GameObject owner) { return Get(owner); }
        /// <summary>Registered agent of an object or null (no registration attempt).</summary>
        private static Agent Get(GameObject owner)
        {
            Agent a;
            return owner != null && _agents.TryGetValue(owner.GetInstanceID(), out a) ? a : null;
        }

        /// <summary>Registered agent of an object, registering it on first sight of its Detection FSM (unsuitable objects are remembered as ignored).</summary>
        private static Agent Of(GameObject owner)
        {
            if (owner == null) return null;
            int id = owner.GetInstanceID();
            Agent a;
            if (_agents.TryGetValue(id, out a)) return a;
            if (_ignored.Contains(id)) return null;
            a = Make(owner);
            if (a == null) { _ignored.Add(id); return null; }
            Register(a);
            return a;
        }
        /// <summary>Adds an agent to the registry, publishes NpcSpawned and applies pending handover / remember / crew orders.</summary>
        private static void Register(Agent a)
        {
            _agents[a.Owner.GetInstanceID()] = a;
            PublishSpawned(a);
            Handover(a);
            Recall(a);
            RecallCrewOrder(a);
        }
        /// <summary>(crew AI) Remember: registered now if its FSMs are up; never marked ignored (asked again when its Detection FSM first runs)</summary>
        private static Agent TryRegister(GameObject owner)
        {
            if (!On || owner.transform.parent != null || _ignored.Contains(owner.GetInstanceID())) return null;
            var a = Make(owner);
            if (a != null) Register(a);
            return a;
        }

        /// <summary>Builds an agent: Detection and Attack FSMs, enemy tags, faction relation, human / spider / tracker / sight group, head bone, vanilla sensors switched off, animal registrations.</summary>
        private static Agent Make(GameObject owner)
        {
            if (owner.transform.parent != null) return null;
            PlayMakerFSM det = null, att = null;
            var fsms = owner.GetComponents<PlayMakerFSM>();
            foreach (var f in fsms)
            {
                if (f == null) continue;
                if (f.FsmName == "Detection") det = f; else if (f.FsmName == "Attack") att = f;
            }
            if (det == null || att == null || det.Fsm == null || !det.Fsm.Initialized) return null;
            var a = new Agent { Owner = owner, T = owner.transform, Col = owner.GetComponent<Collider>(), Detection = det, Attack = att, PrefabName = PrefabOf(owner), LastPos = owner.transform.position };
            a.DetectedVar = det.FsmVariables.FindFsmGameObject("detectedObj");
            a.Tag = owner.tag;
            var behaviours = owner.GetComponentsInChildren<Behaviour>(true);
            a.BaseHostile = HostileTags(owner, behaviours);
            a.Hostile = new HashSet<string>(a.BaseHostile);
            foreach (var f in fsms) if (f != null && f.FsmName == "PlayerIsEnemy") { a.PlayerIsEnemy = f; break; }
            Relation(a, false);
            a.Human = IsHuman(a.Tag);
            string prefab = PrefabOf(owner);
            a.Spider = prefab == "Spider_Small" || prefab == "Spider_Big";
            if (a.Spider) _hasSpiders = true;
            a.Cone = ConeGroupOf(owner);
            a.Tracker = IsTracker(PrefabOf(owner));
            foreach (var t in owner.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name; int i = n.LastIndexOf(':');
                if ((i >= 0 ? n.Substring(i + 1) : n) == "Head") { a.Head = t; break; }
            }
            var sensors = new List<Behaviour>();
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                string tn = b.GetType().Name;
                if (tn == "LOSSensor" || tn == "RangeSensor") sensors.Add(b);
            }
            a.Sensors = sensors.ToArray();
            Sensors(a, false);
            a.Stagger = (_created++ % 10) * 0.012f;
            a.NextLook = Time.time + a.Stagger;
            Burrow.Register(a);
            Pounce.Register(a);
            Nests.Register(a);
            Ambush.Register(a);
            DogBite.Register(a);
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: " + owner.name + " (" + a.Tag + (a.Human ? ", human" : "") + ") hunts " + string.Join("/", new List<string>(a.Hostile).ToArray()) + (a.Head != null ? ", eyes at the head" : ""));
            return a;
        }

        /// <summary>The game turns a faction against the player at run time: hitting a Coyote sends HitFriendly -&gt; CheckFriendly -&gt; FactionCoyotesEnemy, and every Coyote's PlayerIsEnemy FSM goes to state PlayerEnemy (and back to PlayerFriendly when the game makes peace). Our copy of the NPC's enemies follows that state (checked every second and when the NPC registers); the game's own sensors are off, so their tag lists can't be relied on.</summary>
        private static void Relation(Agent a, bool log)
        {
            bool hostile;
            var f = a.PlayerIsEnemy;
            if (f != null && f.Fsm != null && f.Fsm.Initialized) hostile = f.ActiveStateName == "PlayerEnemy";
            else hostile = a.Owner.CompareTag("Coyotes")?CoyoteHostile():a.BaseHostile.Contains("Player");
            if (hostile == a.Hostile.Contains("Player")) return;
            if (hostile) a.Hostile.Add("Player"); else a.Hostile.Remove("Player");
            if (log) Log(a, hostile ? "turns hostile to the player" : "makes peace with the player");
        }

        /// <summary>Switches the vanilla LOS / range sensors of an agent on or off.</summary>
        private static void Sensors(Agent a, bool on)
        {
            if (a.Sensors == null) return;
            foreach (var b in a.Sensors) if (b != null) b.enabled = on;
            a.SensorsOff = !on;
        }
        private static PlayMakerFSM _coyoteRelation;private static float _nextCoyoteRelation;
        /// <summary>The game manager's FactionRelation_Coyotes FSM (looked up at most every 2 s until found).</summary>
        private static PlayMakerFSM CoyoteRelation()
        {
            if(_coyoteRelation==null&&Time.time>=_nextCoyoteRelation)
            {_nextCoyoteRelation=Time.time+2f;var manager=Core.GameRefs.GameManager;if(manager!=null)foreach(var f in manager.GetComponents<PlayMakerFSM>())if(f.FsmName=="FactionRelation_Coyotes"){_coyoteRelation=f;break;}}
            return _coyoteRelation;
        }
        /// <summary>True when the Coyotes faction is hostile to the player.</summary>
        private static bool CoyoteHostile(){var f=CoyoteRelation();return f!=null&&f.Fsm.Initialized&&(f.ActiveStateName=="PlayerEnemy"||f.FsmVariables.FindFsmInt("OnOff")?.Value>0);}

        private static string[] _trackers; private static string _trackersSrc;
        /// <summary>True when the prefab is a tracker ([Senses] Trackers name parts; the list is split once per value).</summary>
        private static bool IsTracker(string prefab)
        {
            string cfg = NpcaiSettings.Trackers.Value ?? "";
            if (_trackers == null || _trackersSrc != cfg) { _trackersSrc = cfg; _trackers = cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries); for (int i = 0; i < _trackers.Length; i++) _trackers[i] = _trackers[i].Trim(); }
            foreach (var t in _trackers) if (t.Length > 0 && prefab.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>True when the faction tag counts as human for taunts ([Senses] HumanFactions).</summary>
        private static bool IsHuman(string tag)
        {
            foreach (var p in (NpcaiSettings.HumanFactions.Value ?? "").Split(',')) if (p.Trim() == tag) return true;
            return false;
        }

        /// <summary>the faction's enemies: the RangeSensor's tag filter (SignalFilter.AllowedTags), as the game has it per prefab</summary>
        private static HashSet<string> HostileTags(GameObject owner, Behaviour[] behaviours)
        {
            var set = new HashSet<string>();
            if(owner.CompareTag("MutantSanity")){set.Add("Player");return set;}
            try
            {
                foreach (var b in behaviours)
                {
                    if (b == null || b.GetType().Name != "RangeSensor") continue;
                    object filter = null;
                    var f = Field(b.GetType(), "SignalFilter") ?? Field(b.GetType(), "signalFilter");
                    if (f != null) filter = f.GetValue(b);
                    if (filter == null) continue;
                    var tagsF = Field(filter.GetType(), "AllowedTags");
                    var tags = tagsF != null ? tagsF.GetValue(filter) as string[] : null;
                    if (tags != null) foreach (var t in tags) if (!string.IsNullOrEmpty(t)) set.Add(t);
                    break;
                }
            }
            catch (Exception e) { if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: tag filter of " + owner.name + ": " + e.Message); }
            if (set.Count == 0)
            {
                // the game's own table, for a prefab without a readable filter
                switch (owner.tag)
                {
                    case "Coyotes": set.Add("Scrapyard"); set.Add("Mutant"); set.Add("Carnivore"); break;
                    case "Carnivore": set.Add("Player"); set.Add("Mutant"); set.Add("Scrapyard"); set.Add("Coyotes"); break;
                    case "Mutant": set.Add("Player"); set.Add("Scrapyard"); set.Add("Carnivore"); set.Add("Coyotes"); set.Add("Herbivore"); break;
                    default: set.Add("Player"); set.Add("Mutant"); set.Add("Carnivore"); set.Add("Coyotes"); break;
                }
            }
            if(owner.CompareTag("Coyotes")){set.Remove("Coyotes");set.Add("Scrapyard");if(CoyoteHostile())set.Add("Player");else set.Remove("Player");}
            return set;
        }

        /// <summary>Finds the player (every 2 s while missing) and its head bone, and refreshes the player's car.</summary>
        private static void FindPlayer()
        {
            if (_player == null && Time.time >= _nextPlayerFind)
            {
                _nextPlayerFind = Time.time + 2f;
                _player = Core.GameRefs.Player;
                _playerHead = null; _inCarFsm = null;
                if (_player != null)
                    foreach (var t in _player.GetComponentsInChildren<Transform>(true)) if (t.name == "head") { _playerHead = t.gameObject; break; }
            }
            _playerCar = PlayerCar();
        }

        /// <summary>the car the player drives: the Player's InCar FSM (state InCar, variable Car)</summary>
        private static GameObject PlayerCar()
        {
            if (_player == null) return null;
            if (_inCarFsm == null)
            {
                foreach (var f in _player.GetComponents<PlayMakerFSM>()) if (f.FsmName == "InCar") { _inCarFsm = f; break; }
                if (_inCarFsm == null) return null;
                _inCarVar = _inCarFsm.FsmVariables.GetFsmGameObject("Car");
            }
            if (_inCarFsm.Fsm == null || !_inCarFsm.Fsm.Initialized || _inCarFsm.ActiveStateName != "InCar") return null;
            return _inCarVar != null ? _inCarVar.Value : null;
        }

        /// <summary>Short display name for logs: "the player" or the prefab name.</summary>
        private static string Name(GameObject go) { return go == null ? "?" : (go == _player ? "the player" : Prefab(go.name)); }
        /// <summary>Prefab name: the object name before "(".</summary>
        private static string Prefab(string n) { int c = n.IndexOf('('); return c > 0 ? n.Substring(0, c) : n; }
        /// <summary>the prefab name of an object, read once (GameObject.name is a native call + a new string every time)</summary>
        internal static string PrefabOf(GameObject go)
        {
            int id = go.GetInstanceID(); string n;
            if (_prefabOf.TryGetValue(id, out n)) return n;
            n = Prefab(go.name);
            if (_prefabOf.Count > 4096) _prefabOf.Clear();
            _prefabOf[id] = n;
            return n;
        }

        /// <summary>Number of registered agents.</summary>
        internal static int AgentCount { get { return _agents.Count; } }
        /// <summary>Number of live ghosts.</summary>
        internal static int GhostCount { get { return _ghosts.Count; } }
        internal static int AlertCount { get { int n = 0; foreach (var kv in _agents) if (kv.Value.Owner != null && kv.Value.State != State.Idle) n++; return n; } }

        /// <summary>(WW) module shutdown: every NPC gets the game's own sensors and detection back. Detection targets that are our ghost / flank objects are cleared (the objects are destroyed); the vanilla sensors find the real target again on their next pulse.</summary>
        internal static void Shutdown()
        {
            Persist.Shutdown();
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner == null) continue;
                if (a.SensorsOff) Sensors(a, true);
                if (a.DetectedVar != null)
                {
                    var v = a.DetectedVar.Value;
                    if (v != null && (v == a.FlankObj || (_ghostRoot != null && v.transform.parent == _ghostRoot))) a.DetectedVar.Value = null;
                }
                a.Travel = false; a.Burrowed = false; a.AmbushDist = 0f;
            }
            foreach (var g in _ghosts) if (g.Obj != null) UnityEngine.Object.Destroy(g.Obj);
            foreach (var kv in _agents) if (kv.Value.FlankObj != null) UnityEngine.Object.Destroy(kv.Value.FlankObj);
            _ghosts.Clear(); _agents.Clear();
            if (_ghostRoot != null) { UnityEngine.Object.Destroy(_ghostRoot.gameObject); _ghostRoot = null; }
            if (_voiceSrc != null) { UnityEngine.Object.Destroy(_voiceSrc.gameObject); _voiceSrc = null; }
            _handover.Clear(); _remember.Clear(); _crewOrders.Clear(); _thrown = null; _grabFsm = null; _grabState = null;
        }

        /// <summary>(WW) floating origin: ghost / flank objects live under the DontDestroyOnLoad root the game does not move; positions are scene space</summary>
        internal static void Shift(Vector3 d)
        {
            foreach (var g in _ghosts) { g.Pos += d; if (g.Obj != null) g.Obj.transform.position = g.Pos; }
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                a.LastSeen += d;
                if (a.Flanking || a.FlankObj != null) { a.FlankPos += d; if (a.FlankObj != null) a.FlankObj.transform.position = a.FlankPos; }
            }
        }

    }
}
