using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.Patrols
{
    // Optional, read-only adapter to the existing NPC Overhaul bake. No assembly dependency or second bake.
    // Discovery also works without NPC Overhaul; unloaded roots never delete a remembered location.
    /// <summary>World locations discovery (camps, POIs, starting area, NPC structure maps, footprints/blocks); POI world-locations part (Wave A split of Patrols Traffic/TrafficLocations.cs).</summary>
    internal static partial class TrafficLocations
    {
        internal static readonly Dictionary<string, TrafficBase> Known = new Dictionary<string, TrafficBase>();
        internal static readonly Dictionary<string, TrafficPoi> Points = new Dictionary<string, TrafficPoi>();
        private static readonly Dictionary<string, Transform> Roots = new Dictionary<string, Transform>();
        private static readonly HashSet<string> Mapped = new HashSet<string>();
        private static readonly HashSet<string> ExaminedPoints = new HashSet<string>();
        private static readonly Stack<Transform> Scan = new Stack<Transform>();
        private sealed class MapRequest { internal Transform Root; internal Bounds? Box; internal object Structure; }
        private static readonly Queue<MapRequest> MapQueue=new Queue<MapRequest>();
        private static readonly HashSet<Transform> QueuedMaps=new HashSet<Transform>();
        private static float _nextScan, _nextBake;
        private static readonly List<Core.NavStructure> _structures = new List<Core.NavStructure>();   // WW: Core.Services.Npc.Structures (ex NPCAI.Nav._structures reflection)
        internal static int Revision { get; private set; }
        private static bool _discoveryReady;
        internal static bool DiscoveryReady=>_discoveryReady;
        internal static int DiscoveryLeft=>Scan.Count;   // diagnostics: transforms still queued for the deep camp scan
        internal static void Changed(){Revision++;}

        internal static bool TownInterior(Vector3 world)
        { Vector3 parking; return Modules.Towns.TownsApi.Provider != null && Modules.Towns.TownsApi.Provider.ParkingOutside(world, out parking); }
        internal static Bounds[] Blocks() => Known.Values.Select(c=>new Bounds(new Vector3(c.center.x,0f,c.center.z),new Vector3(c.size.x+16f,10000f,c.size.z+16f))).ToArray();
        internal static Bounds[] StartingBlocks() => Points.Values.Where(p => p.name.StartsWith("Starter_Area", StringComparison.Ordinal)).Select(p => new Bounds(new Vector3(p.position.x, 0, p.position.z), new Vector3(1000, 10000, 1000))).ToArray();
        internal static bool StartingAllowed(Vector3 point)
        { foreach (var p in Points.Values) if (p.name.StartsWith("Starter_Area", StringComparison.Ordinal) && Mathf.Abs(point.x - p.position.x) <= 500f && Mathf.Abs(point.z - p.position.z) <= 500f) return false; return true; }
        internal static int StartingRevision { get { int revision = 0; foreach (var p in Points.Values) if (p.name.StartsWith("Starter_Area", StringComparison.Ordinal)) revision ^= p.position.GetHashCode(); return revision; } }
        internal static bool Contains(TrafficBase camp,Vector3 point,float margin=35f)
        {return Mathf.Abs(point.x-camp.center.x)<=camp.size.x/2f+margin&&Mathf.Abs(point.z-camp.center.z)<=camp.size.z/2f+margin;}

        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        internal static void Reset()
        { Known.Clear(); Points.Clear(); ExaminedPoints.Clear(); Roots.Clear(); Mapped.Clear(); Scan.Clear(); Shallow.Clear(); MapQueue.Clear();QueuedMaps.Clear();_nextScan = _nextBake = _nextDeep = 0f; _tileApplied = false; _structures.Clear();_discoveryReady=false; Revision++; }

        // Perf: deep scene walk on scene load / tile apply / every 60 s (was every 15 s); the 15 s cadence keeps a roots-only pass
        // (ObserveRoot camp-resident candidates, root camps/POIs). Time-budgeted per frame instead of a fixed 200 nodes.
        private static readonly Stack<Transform> Shallow = new Stack<Transform>();
        private static readonly System.Diagnostics.Stopwatch Budget = new System.Diagnostics.Stopwatch();
        private static readonly long BudgetTicks = (long)(System.Diagnostics.Stopwatch.Frequency * 0.0003);
        private static float _nextDeep;
        private static volatile bool _tileApplied;
        private static readonly List<GameObject> RootBuffer = new List<GameObject>();
        internal static void TileApplied() { _tileApplied = true; }   // TrafficRoads forwards MapMagic TerrainTile.OnTileApplied
        private static bool MaybeCoyote(string n)
        {
            // exact pre-filter for PrefabName(n) == "Camp_1x": PrefabName only strips a trailing suffix and trims whitespace
            if (n == null) return false; int i = 0; while (i < n.Length && char.IsWhiteSpace(n[i])) i++;
            return n.Length - i >= 7 && string.CompareOrdinal(n, i, "Camp_1", 0, 6) == 0;
        }

        internal static void Tick()
        {
            if (_tileApplied)
            {
                // MapMagic applied a tile: deep rescan shortly (objects finish spawning), queued behind a scan already running
                _tileApplied = false; float due = Time.unscaledTime + 3f;
                if (_nextDeep > due) _nextDeep = due; if (_nextScan > due) _nextScan = due;
            }
            if (Scan.Count == 0 && Shallow.Count == 0 && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 15f;
                bool deep = Time.unscaledTime >= _nextDeep;
                if (deep) _nextDeep = Time.unscaledTime + 60f;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded) continue;
                    RootBuffer.Clear(); scene.GetRootGameObjects(RootBuffer);
                    foreach (var root in RootBuffer) (deep ? Scan : Shallow).Push(root.transform);
                }
                RootBuffer.Clear();
            }
            bool remembered=false;Budget.Reset();Budget.Start();
            while (Scan.Count > 0 || Shallow.Count > 0)
            {
                bool deep = Scan.Count > 0;
                var t = deep ? Scan.Pop() : Shallow.Pop(); if (t == null) continue;
                string name = t.name; bool root = t.parent == null;
                if(root)TrafficCamps.ObserveRoot(t);
                RememberPoi(t, name);
                if (IsCamp(name)) { Remember(t, null); remembered=true;break; }
                // vehicles (scene roots) never hold camps/POIs: skip their part hierarchies
                if (deep && !(root && t.GetComponent<NWH.VehiclePhysics2.VehicleController>() != null))
                    for (int c = 0; c < t.childCount; c++) Scan.Push(t.GetChild(c));
                if (Budget.ElapsedTicks >= BudgetTicks) break;
            }
            Budget.Stop();
            if(Scan.Count==0)_discoveryReady=true;
            if(!remembered&&MapQueue.Count>0)
            {
                var next=MapQueue.Dequeue();QueuedMaps.Remove(next.Root);
                if(next.Root!=null)Remember(next.Root,next.Box,next.Structure);
            }
            if (Time.unscaledTime < _nextBake) return;
            _nextBake = Time.unscaledTime + 5f;
            try
            {
                var npc = Core.Services.Npc;
                if (npc == null) return;   // NPC module off: no baked camp maps (the old "NPC Overhaul not installed" path)
                npc.Structures(_structures);
                foreach (var s in _structures)
                {
                    var root = s != null ? s.Root : null;
                    if (root == null || !IsCamp(root.name)) continue;
                    if(QueuedMaps.Add(root))MapQueue.Enqueue(new MapRequest{Root=root,Box=s.Box,Structure=s});
                }
                _structures.Clear();
            }
            catch (Exception e) { PatrolsModule.Verbose("Traffic: NPC Overhaul map adapter: " + e.Message); }
        }

        internal static bool IsCamp(string name)
        { return name != null && name.StartsWith("Camp_", StringComparison.OrdinalIgnoreCase) && name.Length > 5 && char.IsDigit(name[5]); }

        internal static bool CoyoteCamp(string name)
        {if(!MaybeCoyote(name))return false;var n=TemplateExporter.PrefabName(name);return n=="Camp_10"||n=="Camp_15"||n=="Camp_16";}
        internal static bool PoiName(string name)
        // The game's real world locations (its Codex lists Camps / Buildings / Wrecks / Caves): Building_1-7, Wreck_1-8, Cave_1-7,
        // Starter_Area and the Coyote camps (Camp_10/15/16). Raider camps are TrafficBase camps, not POIs. Every location is a target
        // for roaming Coyote patrols and foot patrols. (The old Apocapatrol "GasStation"/"Shop_" prefixes matched nothing in this game.)
        {return name!=null&&(CoyoteCamp(name)||Numbered(name,"Building_")||Numbered(name,"Wreck_")||Numbered(name,"Cave_")||name.StartsWith("Starter_Area",StringComparison.Ordinal));}
        private static bool Numbered(string name,string prefix)
        {return name.Length>prefix.Length&&name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&char.IsDigit(name[prefix.Length]);}
        private static void RememberPoi(Transform root,string name)
        {
            if (TownBuilding(root)) return;
            if(!PoiName(name))return;string id=Identity(root);bool starting=name.StartsWith("Starter_Area", StringComparison.Ordinal);
            if((Points.ContainsKey(id)&&!starting)||!ExaminedPoints.Add(id))return;
            // Inspect a POI only on first discovery, in the existing incremental scene scan.
            Vector3 point=root.position;
            var box=new Bounds(point,Vector3.zero);foreach(var collider in root.GetComponentsInChildren<Collider>(true))if(!collider.isTrigger)box.Encapsulate(collider.bounds);
            point=box.center+root.forward*(Mathf.Max(box.extents.x,box.extents.z)+25f);
            if (starting) point = box.center;
            if(Points.Count>=1024)return;
            Points[id]=new TrafficPoi{id=id,name=TemplateExporter.PrefabName(root.name),position=TrafficSpace.World(point)};
            if (starting) Revision++;
        }

        internal static string Identity(Transform t)
        {
            var p = TrafficSpace.World(t.position);
            return TemplateExporter.PrefabName(t.name) + ":" + Mathf.RoundToInt(p.x) + ":" + Mathf.RoundToInt(p.z);
        }

        private static void Remember(Transform root, Bounds? baked, object structure = null)
        {
            if (TownBuilding(root)) return;
            string id = Identity(root);
            TrafficCamps.Register(id,root);
            // Older NPC Overhaul grids can still contain pre-jump scene coordinates; use current collider geometry instead.
            if (baked.HasValue && !baked.Value.Contains(root.position)) { baked = null; structure = null; }
            bool existed = Known.TryGetValue(id, out var location);
            var nav = structure as Core.NavStructure;
            bool complete = nav != null && nav.Baked;
            if (existed && Roots.TryGetValue(id, out var same) && same == root && Mapped.Contains(id)) return;
            if (existed && Roots.TryGetValue(id, out same) && same == root && !complete) return;
            if (existed && Roots.TryGetValue(id, out var old) && old == root && !baked.HasValue) return;
            if (!existed && Known.Count >= 512) return;
            Roots[id] = root;
            Bounds box = baked ?? new Bounds(root.position, Vector3.one * 60f);
            if (!baked.HasValue)
            {
                bool first = true;
                foreach (var collider in root.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger || !collider.enabled) continue;
                    if (first) { box = collider.bounds; first = false; } else box.Encapsulate(collider.bounds);
                }
            }
            location = new TrafficBase { id = id, name = TemplateExporter.PrefabName(root.name),cleared=location!=null&&location.cleared,patrolCallAt=location!=null?location.patrolCallAt:-1f,faction=CoyoteCamp(root.name)?"Coyotes":"Scrapyard" };
            location.center = box.center; location.center.y = root.position.y;
            location.size = box.size;
            Vector3 interior = location.center;
            // Use a real floor from NPC Overhaul's bake, rather than assuming the prefab origin is driveable.
            // The pedestrian bake proposes an entrance anchor; the sphere corridor below certifies vehicle clearance.
            if (complete)
            {
                var floors = nav.FloorY;
                int w = nav.W;
                float cell = nav.Cell;
                if (floors != null && w > 0 && cell > 0f)
                {
                    float nearest = float.MaxValue;
                    for (int i = 0; i < floors.Length; i++)
                    {
                        if (float.IsNaN(floors[i])) continue;
                        var point = nav.FloorPoint(i);
                        float score = (point - root.position).sqrMagnitude;
                        if (score < nearest) { nearest = score; interior = point; }
                    }
                }
            }
            // Named camp entrances take precedence; clearance is independently tested for a vehicle.
            Transform entrance = null;
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name.IndexOf("entrance", StringComparison.OrdinalIgnoreCase) >= 0) { entrance = t; break; }
            float radius = Mathf.Max(box.extents.x, box.extents.z) + 16f;
            bool found = false; float best = float.MaxValue;
            Vector3 fallback = root.position + root.forward * radius;
            for (int i = 0; i < 16; i++)
            {
                var direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                var outer = location.center + direction * radius;
                if (!Convoy.GroundAt(outer, out var ground)) continue;
                var gate = entrance != null ? entrance.position : interior;
                float score = entrance != null ? FlatDistance(ground, gate) : FlatDistance(ground, fallback);
                bool clear = VehicleClear(null, ground + Vector3.up * 2f, gate + Vector3.up * 2f, 1.4f);
                if (clear) score -= 1000f;
                if (score >= best) continue;
                best = score; location.apron = ground; location.gate = clear ? gate : ground;
                location.mappedEntrance = clear; found = true;
            }
            if (!found) return; // terrain isn't loaded yet; keep the previous stable record and try later
            if (found)
            {
                location.center = TrafficSpace.World(location.center);
                location.apron = TrafficSpace.World(location.apron);
                location.gate = TrafficSpace.World(location.gate);
                Known[id] = location;
                Revision++;
                if (complete) Mapped.Add(id);
            }
        }

        internal static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        internal static bool TownBuilding(Transform root)
        { for (var t = root; t != null; t = t.parent) if (Modules.Towns.TownMarkers.Owns(t)) return true; return false; }
    }
}
