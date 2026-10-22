using System;
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.POI
{
    // Applies to native interior groups throughout the world while the addon is loaded.
    // Native FSMs still own creation, registration, names and one-shot progress.
    internal static class InteriorPopulation
    {
        private sealed class Location { internal Transform Root; internal readonly List<Group> Groups = new List<Group>(); internal bool Arrived; }
        /// <summary>A native interior spawn group (ItemSpawner FSM): its town / dressing site, location, start attempts, suspension and the deferred native event.</summary>
        private sealed class Group { internal PlayMakerFSM Fsm; internal TownSite Town; internal PoiDressing.Site Dressing; internal Location Location; internal bool Started, Suspended; internal int Attempts; internal float StartedAt; internal FsmEvent Deferred; internal FsmState DeferredState; }   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
        private static readonly Dictionary<object, Location> Locations = new Dictionary<object, Location>();
        /// <summary>A population member: the spawned NPC, its health, its senses agent and saved name.</summary>
        private sealed class Member { internal GameObject Owner; internal FsmFloat Health; internal Senses.Agent Agent; internal string Name; }
        private static readonly Dictionary<PlayMakerFSM, Group> Groups = new Dictionary<PlayMakerFSM, Group>();
        private static readonly List<Group> Pending = new List<Group>();
        private static readonly Dictionary<int, Member> Members = new Dictionary<int, Member>();
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> Fresh = new HashSet<int>();
        private static readonly List<int> Dead = new List<int>();
        private static bool _releaseNative;
        private static int _next, _near;
        private static Vector3 _player;
        private static bool _havePlayer;
        /// <summary>Installs the interior and native-POI population rules: ProcessEvent / FsmEnable / CreateObject hooks and the pre-spawn and hearing jobs (owner NPCAI).</summary>
        internal static void Install(IModule module)
        {
            NativePoiPopulation.Install(module);
            _releaseNative = false;
            Hooks.ProcessEvent.Prefix(module, SpawnEvent);
            Hooks.FsmEnable.Postfix(module, Enabled); Hooks.CreateObjectEnter.Postfix(module, Created);
            Scheduler.Sliced(module, "NPCAI.Interior pre-spawn", 700, Step);
            Scheduler.Interval(module, "NPCAI.Interior hearing", .25f, Hearing);
        }
        /// <summary>Registers every interior group already enabled in the loaded scene.</summary>
        internal static void DiscoverExisting()
        { foreach (var f in PlayMakerFSM.FsmList) if (f != null) Enabled(f); }
        /// <summary>True for an interior enemy spawner: an ItemSpawner on an EnemySpawn object with a small enabled trigger collider.</summary>
        private static bool Interior(PlayMakerFSM f)
        {
            if (f == null || f.FsmName != "ItemSpawner" || !f.name.StartsWith("EnemySpawn", StringComparison.Ordinal)) return false;
            foreach (var collider in f.GetComponents<Collider>())
                if (collider.isTrigger && collider.enabled && (!(collider is SphereCollider) || collider.bounds.extents.magnitude < 70)) return true;
            return false;
        }
        /// <summary>FsmEnable postfix: a newly enabled interior spawner outside a town building joins its location.</summary>
        private static void Enabled(PlayMakerFSM f)
        {
            if (!f.gameObject.scene.IsValid() || !Interior(f)) return;
            for (var t = f.transform; t != null; t = t.parent) if (NativePoiPopulation.TownBuilding(t)) return;
            Add(f, null);
        }
        /// <summary>Adds a spawner group to its location (town, dressing marker root or the enclosing native structure) and queues it for the arrival pre-spawn.</summary>
        private static void Add(PlayMakerFSM f, TownSite town, Transform markerRoot = null)   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
        {
            Transform root = town == null ? f.transform : town.Anchor;
            if (town == null) for (var t = f.transform.parent; t != null; t = t.parent)
                if (t.name.StartsWith("Building_", StringComparison.Ordinal) || t.name.StartsWith("Camp_", StringComparison.Ordinal)
                    || t.name.StartsWith("Cave_", StringComparison.Ordinal) || t.name.StartsWith("Wreck_", StringComparison.Ordinal)) root = t;
            if (markerRoot != null) root = markerRoot;
            object key = town == null ? (object)root : town;
            if (town != null || markerRoot != null) WorldIndex.QueueRoot(root);   // built at run time: its "Hide" cover objects join the brain's WorldIndex watch
            Location location; if (!Locations.TryGetValue(key, out location)) Locations.Add(key, location = new Location { Root = root });
            Group group;
            if (Groups.TryGetValue(f, out group))
            {
                if (town != null || markerRoot != null) {
                    group.Town = town;
                    if (group.Location != location) { location.Arrived |= group.Location.Arrived; group.Location.Groups.Remove(group); group.Location = location; location.Groups.Add(group); }
                }
                if (!Pending.Contains(group)) { group.Started = false; Pending.Add(group); }
                return;
            }
            group = new Group { Fsm = f, Town = town, Location = location }; location.Groups.Add(group); Groups.Add(f, group); Pending.Add(group);
        }
        /// <summary>IPopulation.RegisterInterior: every interior EnemySpawn ItemSpawner under <paramref name="root"/> joins one location keyed by the root.</summary>
        internal static void RegisterRoot(Transform root)
        {
            if (root == null) return;
            foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f != null && f.FsmName == "ItemSpawner" && f.name.StartsWith("EnemySpawn", StringComparison.Ordinal)) Add(f, null, root);
        }

        /// <summary>IPopulation.UnregisterInterior: drops the location of <paramref name="root"/> and its pending groups (native FSMs keep their state).</summary>
        internal static void UnregisterRoot(Transform root)
        {
            Location location;
            if (root == null || !Locations.TryGetValue(root, out location)) return;
            foreach (var group in location.Groups) { Groups.Remove(group.Fsm); Pending.Remove(group); if (group.Suspended && group.Fsm != null) group.Fsm.enabled = true; }
            Locations.Remove(root);
        }

        /// <summary>IPopulation.IsPopulated: the root's location exists and none of its groups is still waiting to spawn.</summary>
        internal static bool IsPopulated(Transform root)
        {
            Location location;
            if (root == null || !Locations.TryGetValue(root, out location)) return false;
            foreach (var group in location.Groups) if (Pending.Contains(group)) return false;
            return true;
        }

        /// <summary>A POI dressing marker spawner joins the population (suspended until the dressing is ready).</summary>
        internal static void OwnMarker(PlayMakerFSM f, PoiDressing.Site site)
        { Add(f, site.Town, site.Town == null ? site.Root : null); Groups[f].Dressing = site; Groups[f].Suspended = true; f.enabled = false; }
        /// <summary>The enemy spawners of a town lot join the population (suspended when the lot's saved count is already spawned; Coyote shopkeepers too).</summary>
        internal static void OwnTown(TownSite site, int lot)   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
        {
            for (int n = 0; n < site.Spawners[lot].Length; n++)
            {
                var f = site.Spawners[lot][n];
                if (f != null && site.Plan.Lots[lot].IsMerchant && site.Plan.Faction == TownFaction.Coyotes   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    && f.gameObject.name.StartsWith("CoyotesMerchantSpawn", StringComparison.Ordinal))
                { Add(f, site); continue; } // Shopkeepers use the same native trigger sequence as town enemies.
                if (f != null && f.gameObject.name.StartsWith("EnemySpawn", StringComparison.Ordinal)
                    && (f.gameObject.name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) < 0 || f.gameObject.name.IndexOf("bodyguard", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    Add(f, site); var group = Groups[f];
                    group.Suspended = Value(f, "OnOff") == 0 && site.PopulationCounts[lot][n] > Value(f, "number");
                    if (group.Suspended) f.enabled = false;
                }
            }
        }
        /// <summary>Integer FSM variable value (0 when missing).</summary>
        private static int Value(PlayMakerFSM f, string name)
        { var value = f.FsmVariables.FindFsmInt(name); return value == null ? 0 : value.Value; }
        /// <summary>True when the player has arrived at the spawner's location (town radius + 50 m, else 50 m); arrival sticks per location.</summary>
        private static bool Near(PlayMakerFSM f)
        {
            Group group; if (Groups.TryGetValue(f, out group) && group.Location.Arrived) return true;
            var player = GameRefs.Player; if (player == null) return false;
            bool near;
            if (group != null && group.Town != null)
            {
                var d = group.Town.Scene(Vector3.zero) - player.transform.position; d.y = 0;
                float radius = group.Town.Plan.Radius + 50f; near = d.sqrMagnitude <= radius * radius;
            }
            else near = (f.transform.position - player.transform.position).sqrMagnitude <= 2500f;
            if (near && group != null) group.Location.Arrived = true;
            return near;
        }
        /// <summary>ProcessEvent prefix: a tracked spawner's trigger / spawn transition waits until the player arrived and a world-work slot is free (one actor per 0.15 s).</summary>
        private static bool SpawnEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (Groups.Count == 0 || _releaseNative || ev == null || fsm.Name != "ItemSpawner") return true;   // (perf, README 8 #1) no tracked group: nothing to defer
            var state = fsm.ActiveState; if (state == null || state.Name != "trigger" && state.Name != "spawn") return true;
            var f = fsm.Owner as PlayMakerFSM; Group group; if (f == null || !Groups.TryGetValue(f, out group)) return true;
            foreach (var transition in state.Transitions)
            {
                if (transition.FsmEvent == null || transition.FsmEvent.Name != ev.Name || transition.ToState != "getRandom int" && transition.ToState != "getItem" && transition.ToState != "spawn") continue;
                if (CanRun() && Near(f) && Time.unscaledTime >= _actorAfter && _actorFrame != Time.frameCount && WorldWork.Try(WorldWork.Population))
                { _actorAfter = Time.unscaledTime + .15f; _actorFrame = Time.frameCount; return true; }
                group.Deferred = ev; group.DeferredState = state; return false;
            }
            return true;
        }
        /// <summary>True when none of the town's tracked spawner groups is still waiting near the player.</summary>
        internal static bool TownReady(TownSite site)   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
        {
            foreach (var group in Pending) if (group.Town == site && group.Fsm != null && Near(group.Fsm) && group.Fsm.gameObject.activeInHierarchy && (group.Fsm.enabled || group.Suspended) && Value(group.Fsm, "OnOff") == 0) return false;
            return true;
        }
        internal static bool Ready
        {
            get { foreach (var group in Pending) if (group.Fsm != null && Near(group.Fsm) && group.Fsm.gameObject.activeInHierarchy && (group.Fsm.enabled || group.Suspended) && Value(group.Fsm, "OnOff") == 0) return false; return true; }
        }
        /// <summary>SaveSystem.Busy (the WW save transaction lock) has no exact contract equivalent: ISaves.Busy / SaveBus.Busy also cover loading and "Wait for Terrain Gen", where population must keep running (B04-8 answered in reports/B07.md)</summary>
        private static bool CanRun()
        { return !SaveSystem.Busy && !SaveBus.Saving && (GameRefs.InGame || GameRefs.SaveLoadState == "Wait for Terrain Gen"); }   // refs-ok: REFACTOR-REQUEST B07-6
        private static float _spawnAfter, _actorAfter;
        private static int _actorFrame = -1;
        /// <summary>Sliced job "NPCAI.Interior pre-spawn": runs the authored native trigger sequence of arrived groups, one group per 0.15 s, honouring completed bosses and stalled groups.</summary>
        private static bool Step()
        {
            if (Pending.Count == 0 || !CanRun() || Time.unscaledTime < _spawnAfter) return false;
            if (_next >= Pending.Count) _next = 0;
            for (int i = 0; i < Math.Min(Pending.Count, 8); i++)
            {
                int at = _next++ % Pending.Count; var group = Pending[at]; var f = group.Fsm;
                if (f == null || !f.gameObject.activeInHierarchy || !f.enabled && !group.Suspended || Value(f, "OnOff") > 0) { Pending.RemoveAt(at); return true; }
                if (!Near(f)) continue;
                if (group.Town != null && group.Town.NextLot < group.Town.Plan.LotCount) continue;
                if (group.Dressing != null && !group.Dressing.Ready) continue;
                if (!WorldWork.Try(WorldWork.Population)) return false;
                if (group.Suspended) { group.Suspended = false; f.enabled = true; }
                if (group.Deferred != null)
                {
                    if (Time.unscaledTime < _actorAfter || _actorFrame == Time.frameCount) continue;
                    var ev = group.Deferred; var state = group.DeferredState; group.Deferred = null; group.DeferredState = null;
                    if (f.Fsm.ActiveState == state) f.Fsm.Event(ev);
                    return true;
                }
                if (group.Started)
                {
                    if (f.ActiveStateName != "wait" && f.ActiveStateName != "trigger") continue;
                    if (Time.unscaledTime - group.StartedAt < 2) continue;
                    if (group.Attempts >= 2)
                    { f.enabled = false; Pending.RemoveAt(at); NpcaiRuntime.Warn("stopped stalled interior spawn group " + f.gameObject.name + "; existing actors retained"); return true; }
                    group.Started = false;
                }
                if (!f.Fsm.Initialized || f.ActiveStateName != "wait" && f.ActiveStateName != "trigger") continue;
                // Respect authored completed-boss flags before bypassing the wait/entry trigger.
                bool completed = false;
                foreach (var state in f.FsmStates) if (state.Name == "wait") foreach (var action in state.Actions)
                {
                    var test = action as BoolTest;
                    if (test != null && test.boolVariable.Value && test.isTrue != null && test.isTrue.Name == "back") completed = true;
                }
                if (completed) { f.Fsm.SetState("spawned"); return true; }
                TriggerEvent trigger = null;
                foreach (var state in f.FsmStates) if (state.Name == "trigger") foreach (var action in state.Actions)
                    if (action is TriggerEvent) trigger = (TriggerEvent)action;
                if (trigger == null || trigger.sendEvent == null) continue;
                group.Started = true; group.Attempts++; group.StartedAt = Time.unscaledTime;
                // Run the authored random roll and its actual trigger transition, not a guessed spawn count/state.
                f.Fsm.SetState("trigger"); f.Fsm.Event(trigger.sendEvent);
                _spawnAfter = Time.unscaledTime + .15f;
                if (Core.Log.L1) NpcaiRuntime.Main("location population queued on arrival: " + f.gameObject.name + "; actors spaced by 0.15 s between world-build slices, native sequence retained.");
                return true;
            }
            return false;
        }
        /// <summary>CreateObject postfix: a spawned interior NPC is tracked (town population, health, agent; interior hearing set at once).</summary>
        private static void Created(CreateObject action)
        {
            if (Groups.Count == 0 || action.Fsm == null || action.storeObject == null || action.Fsm.Name != "ItemSpawner") return;   // (perf) cheap filters first
            var f = action.Fsm.Owner as PlayMakerFSM; Group group;
            if (f == null || !Groups.TryGetValue(f, out group)) return;
            var made = action.storeObject.Value; if (made == null || FsmUtil.Find(made, "Health") == null || FsmUtil.Find(made, "Detection") == null) return;
            Fresh.Add(made.GetInstanceID());
            if (group.Town != null) TownPopulation.Created(group.Town, made);
            var health = FsmUtil.Find(made, "Health"); var agent = Senses.AgentFor(made);
            Members[made.GetInstanceID()] = new Member { Owner = made, Health = health.FsmVariables.FindFsmFloat("Health"), Agent = agent };
            if (agent != null) { agent.Interior = true; agent.AmbushDist = 0f; agent.Deaf = !_havePlayer || _near >= 3 || (made.transform.position - _player).sqrMagnitude > 2500f; }
        }
        /// <summary>The population member of a senses agent (created on first sight).</summary>
        private static Member Observe(Senses.Agent agent)
        {
            var owner = agent.Owner; if (owner == null) return null;
            int id = owner.GetInstanceID(); Member member;
            if (!Members.TryGetValue(id, out member))
            {
                var health = FsmUtil.Find(owner, "Health");
                member = new Member { Owner = owner, Health = health == null ? null : health.FsmVariables.FindFsmFloat("Health") };
                Members[id] = member;
            }
            if (member.Name == null) member.Name = owner.name;
            member.Agent = agent; return member;
        }
        /// <summary>True for NPCs spawned by tracked interior groups (this session or by saved name).</summary>
        private static bool Tracked(Member member)
        { return member.Owner != null && (Fresh.Contains(member.Owner.GetInstanceID()) || member.Name != null && Names.Contains(member.Name)); }
        /// <summary>A tracked NPC is deaf unless the player is close and fewer than 3 hostiles are near him.</summary>
        private static bool Deaf(Member member)
        {
            if (!Tracked(member)) return false;
            // Far interior groups remain quiet; only the player's immediate encounter unlocks hearing.
            return !_havePlayer || _near >= 3 || (member.Owner.transform.position - _player).sqrMagnitude > 2500;
        }
        /// <summary>Job "NPCAI.Interior hearing" (0.25 s): counts hostiles near the player and applies the interior deafness to tracked NPCs.</summary>
        private static void Hearing()
        {
            var player = GameRefs.Player; _havePlayer = player != null;
            int previous = _near; if (_havePlayer) _player = player.transform.position; _near = 0; Dead.Clear();
            foreach (var agent in Senses.AllAgents)
            {
                var member = Observe(agent); if (member == null) continue;
                agent.Interior = Tracked(member);
                if (agent.Interior) { Names.Add(member.Name); agent.AmbushDist = 0f; }
                if (_havePlayer && _near < 3 && member.Owner.activeInHierarchy && member.Health != null && member.Health.Value > 0
                    && agent.Hostile != null && agent.Hostile.Contains("Player") && (member.Owner.transform.position - _player).sqrMagnitude <= 2500) _near++;
            }
            foreach (var pair in Members) if (pair.Value.Owner == null) Dead.Add(pair.Key);
            foreach (int id in Dead) { Members.Remove(id); Fresh.Remove(id); }
            foreach (var member in Members.Values)
            {
                if (member.Agent == null) continue;
                member.Agent.Deaf = Deaf(member);
                Senses.ClearDeafInvestigation(member.Agent);
            }
            if ((previous >= 3) != (_near >= 3) && Names.Count > 0)
                if (Core.Log.L1) NpcaiRuntime.Main("interior hearing: " + (_near >= 3 ? "3+ enemies within 50 m; sounds ignored" : _near + " enemies within 50 m; nearby hearing enabled"));
        }
        /// <summary>Forgets every group, location and member (deafness / interior flags of known agents cleared).</summary>
        internal static void Clear()
        {
            foreach (var member in Members.Values) if (member.Agent != null) { member.Agent.Deaf = false; member.Agent.Interior = false; }
            NativePoiPopulation.Clear(); Groups.Clear(); Locations.Clear(); Pending.Clear(); Members.Clear(); Fresh.Clear(); Names.Clear(); _next = _near = 0; _havePlayer = false;
            _spawnAfter = _actorAfter = 0f; _actorFrame = -1;
        }
        /// <summary>Module stop: deferred native spawn events run now so no spawner is left waiting.</summary>
        internal static void ReleaseDeferred()
        {
            _releaseNative = true;
            foreach (var group in new List<Group>(Groups.Values)) if (group.Deferred != null && group.Fsm != null && group.Fsm.enabled && group.Fsm.Fsm.ActiveState == group.DeferredState)
            { var ev = group.Deferred; group.Deferred = null; group.Fsm.Fsm.Event(ev); }
        }
        /// <summary>Load: the saved names of interior NPCs (kept in the POI town record) become tracked again.</summary>
        internal static void RestoreNames(string[] names)
        { Clear(); foreach (string name in names) if (!string.IsNullOrEmpty(name)) Names.Add(name); }
        /// <summary>Save: names of the tracked interior NPCs, sorted.</summary>
        internal static string[] CaptureNames()
        {
            foreach (var member in Members.Values) if (Tracked(member)) Names.Add(member.Owner.name);
            var rows = new List<string>(Names); rows.Sort(StringComparer.Ordinal); return rows.ToArray();
        }
    }
}
