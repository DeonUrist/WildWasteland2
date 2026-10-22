using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    // Distance travelled and boss kills (the world traffic's group unlocks), and the patrol editor's test spawns: the exact vehicle
    // roster and allowed cargo loaded from the editor, the spot ahead of (or behind) the player, and the staggered builds.
    // (WW) The old automatic spawn clock (cooldown raids) was removed: automatic raiders come from the world traffic.

    internal class Convoy : MonoBehaviour
    {
        private static Convoy _inst;
        private PlayMakerFSM _distanceFsm;
        private float _nextHeatScan;
        private float _km;
        private int _bosses;
        private Patrol _patrol;

        private void Awake() { _inst = this; }

        internal static void ResetForScene()
        {
            if (_inst == null) return;
            LastGroup = "";
            _inst._distanceFsm = null;
            _inst._km = 0f;
            _inst._bosses = 0;
        }

        // [Debug] AiOverlay with no raider car driving: the world traffic's state
        internal static string OverlayLine()
        {
            if (PatrolsModule.ConvoyEnabled.Value) return WorldTraffic.OverlayLine();
            return "Raiders: automatic spawns off";
        }

        internal float Km { get { return _km; } }
        internal int Bosses { get { return _bosses; } }

        // WW: was Update (Update job)
        internal void Tick()
        {
            try { Step(); }
            catch (Exception e) { Core.Log.Error("[Patrols] " + "Convoy: " + e); }
        }

        private void Step()
        {
            if (_patrol == null) _patrol = GetComponent<Patrol>();
            if (_patrol == null || !_patrol.InGame()) return;
            if (Time.unscaledTime >= _nextHeatScan) { _nextHeatScan = Time.unscaledTime + 5f; UpdateHeat(); }
        }

        // ------------------------------------------------------------ distance and bosses

        // The game's Distance Travelled (__GameManager__ [DistanceTravelled].distance, km from the starting area) and boss kills
        // (the game's Boss_* global flags).
        private void UpdateHeat()
        {
            if (_distanceFsm == null || _distanceFsm.gameObject == null)
            {
                _distanceFsm = null;
                _distanceFsm = Core.FsmUtil.Find(Core.GameRefs.GameManager, "DistanceTravelled");
            }
            float km = -1f;
            if (_distanceFsm != null && _distanceFsm.Fsm.Initialized)
            {
                var v = _distanceFsm.FsmVariables.GetFsmFloat("distance");
                if (v != null) km = v.Value;
            }
            if (km < 0f)
            {
                var pl = PlayerRef.Player; var st = GameObject.Find("Starter_Area");
                if (pl != null && st != null) km = Vector3.Distance(pl.position, st.transform.position) / 1000f;
            }
            if (km < 0f) return;
            _km = km;
            _bosses = BossKills();
        }

        // [General] Fury Road: every group's minimum distance / boss kills count as met
        private float RollKm { get { return PatrolsModule.FuryRoad.Value ? 1e6f : _km; } }
        private int RollBosses { get { return PatrolsModule.FuryRoad.Value ? 999 : _bosses; } }
        internal static SpawnGroup TrafficGroup(bool convoy,string faction=null)
        {
            if (_inst == null) return null;
            return SpawnGroup.Roll((convoy ? EditorStore.Data.Convoys : EditorStore.Data.Patrols).Where(g=>(faction==null||TrafficFactions.Normalize(g.Faction)==faction)
                &&(convoy||TrafficFactions.Normalize(g.Faction)!="Coyotes"||CoyoteRoster(g))),
                _inst.RollKm, _inst.RollBosses, () => UnityEngine.Random.value);
        }
        private static bool CoyoteRoster(SpawnGroup group)=>group.Templates.Any(n=>{var t=CarTemplate.Find(n);return t!=null&&t.Available&&CrewFactions.Compatible(t,"Coyotes");});
        internal static bool HasTrafficFaction(string faction)=>_inst!=null&&EditorStore.Data.Patrols.Any(g=>TrafficFactions.Normalize(g.Faction)==faction&&g.Eligible(_inst.RollKm,_inst.RollBosses)&&(faction!="Coyotes"||CoyoteRoster(g)));

        internal static int BossKills()
        {
            int n = 0;
            try
            {
                foreach (var b in FsmVariables.GlobalVariables.BoolVariables)
                    if (b != null && b.Name != null && b.Name.StartsWith("Boss_") && b.Value) n++;
            }
            catch (Exception) { }
            return n;
        }

        // Apocasaver (optional, by reflection): true while it autosaves, or when its next autosave is due within `within` s. An autosave
        // that is overdue (it waits for the player to be on foot) does not block spawns - the builds themselves wait out a running save.
        private static bool _asResolved;
        private static FieldInfo _asCurrent, _asLast, _asArmed;
        private static PropertyInfo _asSaving;
        private static BepInEx.Configuration.ConfigEntry<float> _asInterval;
        private static BepInEx.Configuration.ConfigEntry<bool> _asEnabled;
        internal static bool AutosaveSoon(float within)
        {
            try
            {
                if (!_asResolved)
                {
                    _asResolved = true;
                    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Apocasaver");
                    var plugin = asm != null ? asm.GetType("Apocasaver.Plugin") : null;
                    var runner = asm != null ? asm.GetType("Apocasaver.Runner") : null;
                    if (plugin == null || runner == null) return false;
                    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    _asCurrent = plugin.GetField("Current", S);
                    var iv = plugin.GetField("IntervalMinutes", S); _asInterval = iv != null ? iv.GetValue(null) as BepInEx.Configuration.ConfigEntry<float> : null;
                    var en = plugin.GetField("Enabled", S); _asEnabled = en != null ? en.GetValue(null) as BepInEx.Configuration.ConfigEntry<bool> : null;
                    _asLast = runner.GetField("_lastSave", I); _asArmed = runner.GetField("_armed", I); _asSaving = runner.GetProperty("IsAutosaving", I);
                    PatrolsModule.Verbose("Convoy: Apocasaver found, spawns wait for its autosaves" + (_asCurrent != null && _asLast != null && _asInterval != null ? "" : " (timer not readable)"));
                }
                if (_asCurrent == null) return false;
                var r = _asCurrent.GetValue(null) as UnityEngine.Object;
                if (r == null) return false;
                if (_asSaving != null && _asSaving.GetValue(r, null) is bool && (bool)_asSaving.GetValue(r, null)) return true;
                if (_asLast == null || _asInterval == null || (_asEnabled != null && !_asEnabled.Value)) return false;
                if (_asArmed != null && _asArmed.GetValue(r) is bool && !(bool)_asArmed.GetValue(r)) return false;
                float remaining = _asInterval.Value * 60f - (Time.realtimeSinceStartup - (float)_asLast.GetValue(r));
                return remaining > -3f && remaining < within;
            }
            catch (Exception e) { PatrolsModule.Verbose("Convoy: Apocasaver timer: " + e.Message); _asCurrent = null; return false; }
        }

        // the editor's random test spawn: 80 % patrols, 20 % convoys when both have eligible groups
        private const float DebugPatrolPercent = 80f;

        internal void DebugSpawn(SpawnGroup group, bool convoy)
        {
            if (!PatrolsModule.AllowDebugSpawns.Value) return;
            UpdateHeat(); Launch(group, convoy, true);
        }

        internal void DebugRoll()
        {
            if (!PatrolsModule.AllowDebugSpawns.Value) return;
            UpdateHeat(); bool convoy;
            var group = EditorStore.Roll(RollKm, RollBosses, DebugPatrolPercent, () => UnityEngine.Random.value, out convoy);
            if (group != null) Launch(group, convoy, true);
            else Core.Log.Info("[Patrols] " + "Test spawning: no eligible groups with a positive chance");
        }

        private bool Launch(SpawnGroup definition, bool convoy, bool debug)
        {
            CarTemplates.Refresh(false);
            var group = EditorStore.Compose(definition, convoy, () => UnityEngine.Random.value);
            string eventId=Guid.NewGuid().ToString("N");
            foreach(var t in group){t.SpawnEvent=eventId;t.ConvoyMember=convoy;}
            if (_patrol == null) _patrol = GetComponent<Patrol>();
            Vector3 origin, dir; bool driving;
            if (!Heading(out origin, out dir, out driving)) return false;
            List<Vector3> spots = null; Vector3 facing = Vector3.forward; string where = "";
            for (int attempt = 0; attempt < 6 && spots == null; attempt++)
            {
                float angle = UnityEngine.Random.Range(-45f, 45f);
                var d = Quaternion.Euler(0f, angle, 0f) * dir;
                var center = origin + d * PatrolsModule.ConvoySpawnDistance.Value;
                facing = -d;                                                   // toward the player
                spots = Formation(center, facing, group.Count, convoy || group.Any(t => t.IsTruck));
                where = (driving ? "ahead" : "behind") + (angle < -10f ? "-left" : angle > 10f ? "-right" : "") + " " + PatrolsModule.ConvoySpawnDistance.Value.ToString("0") + " m";
            }
            if (spots == null) { Core.Log.Warn("[Patrols] " + "Convoy: no ground for " + definition.Name + " around the player"); return false; }

            PatrolsModule.Verbose("Convoy: " + definition.Name + (debug ? " (debug)" : "") + " (km " + _km.ToString("0.0") + ", bosses " + _bosses
                + "), "
                + where + ": " + string.Join(", ", group.Select(t => t.Name).ToArray()));
            StartCoroutine(BuildAll(group, spots, Quaternion.LookRotation(facing)));
            return true;
        }

        // Where the player is and which way "ahead" is: the car's travel direction while driving; behind the camera on foot.
        private static bool Heading(out Vector3 origin, out Vector3 dir, out bool driving)
        {
            origin = Vector3.zero; dir = Vector3.forward; driving = false;
            var car = PlayerRef.PlayerCar;
            var pl = PlayerRef.Player;
            var cam = Core.GameRefs.MainCamera;
            if (car != null)
            {
                driving = true;
                origin = car.transform.position;
                var rb = car.GetComponent<Rigidbody>();
                dir = rb != null && rb.velocity.sqrMagnitude > 1f ? rb.velocity : car.transform.forward;
            }
            else if (pl != null || cam != null)
            {
                origin = pl != null ? pl.position : cam.transform.position;
                dir = -(cam != null ? cam.transform.forward : pl.forward);       // behind
            }
            else return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            return true;
        }

        // Rows of three facing the player (the first car - the truck in a convoy - in the middle of the first row), 7 m apart,
        // rows 9 m behind each other. Every spot needs open terrain under it; null when most of them have none.
        private static List<Vector3> Formation(Vector3 center, Vector3 facing, int count, bool truck)
        {
            var right = Vector3.Cross(Vector3.up, facing).normalized;
            var spots = new List<Vector3>();
            int missing = 0;
            for (int i = 0; i < count; i++)
            {
                int row = i / 3, col = i % 3;
                float side = col == 0 ? 0f : col == 1 ? -1f : 1f;
                var p = center + right * (side * 7f) - facing * (row * 9f);
                Vector3 g;
                if (GroundAt(p, out g, truck) || GroundAt(p + right * 4f, out g, truck) || GroundAt(p - right * 4f, out g, truck)) spots.Add(g + Vector3.up * 1.0f);
                else missing++;
            }
            if (missing > count / 2 || spots.Count == 0) return null;
            return spots;
        }

        // open terrain straight below: the nearest hit of a downward ray from high above must be the terrain (not a roof, rock or car)
        private static readonly RaycastHit[] GroundHits = new RaycastHit[64];
        private static bool GroundPoint(Vector3 p, float grade, out Vector3 ground)
        {
            ground = p;
            int count = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 3000f, p.z), Vector3.down, GroundHits, 6000f, ~0, QueryTriggerInteraction.Ignore);
            if (count == 0 || count == GroundHits.Length) { Array.Clear(GroundHits, 0, count); return false; }
            RaycastHit best = GroundHits[0];
            for (int i = 1; i < count; i++) if (GroundHits[i].distance < best.distance) best = GroundHits[i];
            Array.Clear(GroundHits, 0, count);
            if (!(best.collider is TerrainCollider)) return false;
            if (best.normal.y < 1f / Mathf.Sqrt(1f + grade * grade)) return false;
            ground = best.point;
            return true;
        }
        internal static bool GroundAt(Vector3 p, out Vector3 ground, bool truck = false)
        {
            float grade = truck ? .22f : .30f;
            if (!GroundPoint(p, grade, out ground)) return false;
            float reach = truck ? 7f : 5f;
            for (int i = 0; i < 4; i++)
            {
                var sample = ground + (i < 2 ? Vector3.right : Vector3.forward) * ((i % 2 == 0 ? 1 : -1) * reach);
                if (!GroundPoint(sample, grade, out var edge) || Mathf.Abs(edge.y - ground.y) > reach * grade) return false;
            }
            return true;
        }

        // One build every BuildInterval seconds (each build itself is spread over frames), the crews held; when the last build
        // has finished the whole group is released at once and departs together.
        private const float BuildInterval = 0.7f;

        private IEnumerator BuildAll(List<CarTemplate> group, List<Vector3> spots, Quaternion rot)
        {
            var cars = new List<GameObject>();
            int pending = 0, gen = Patrol.SceneGen;
            for (int i = 0; i < group.Count && i < spots.Count; i++)
            {
                // a save or the pause menu: the rest of the group waits (up to a minute) instead of being dropped; a load ends it
                float waitUntil = Time.realtimeSinceStartup + 60f;
                while (_patrol != null && !_patrol.InGame() && Patrol.SceneGen == gen && Time.realtimeSinceStartup < waitUntil) yield return null;
                if (_patrol == null || !_patrol.InGame() || Patrol.SceneGen != gen) break;
                pending++;
                _patrol.SpawnAt(group[i], spots[i], rot, true, car => { pending--; if (car != null) cars.Add(car); });
                yield return new WaitForSeconds(BuildInterval);
            }
            while (pending > 0 && _patrol != null && Patrol.SceneGen == gen) yield return null;
            if (_patrol == null || Patrol.SceneGen != gen) yield break;
            int released = 0;
            foreach (var car in cars)
            {
                if (car == null) continue;
                var crew = car.GetComponent<Crew>();
                if (crew != null) { crew.Release(); released++; }
            }
            PatrolsModule.Verbose("Convoy: " + released + " car(s) released");
        }

        // 2.5.3: the group the last automatic spawn used (saved with the clock): the next roll avoids it when another group is eligible, so
        // a save made just before a raid does not bring the same patrol on every load (the clock itself is kept as saved - on purpose)
        internal static string LastGroup = "";
    }
}
