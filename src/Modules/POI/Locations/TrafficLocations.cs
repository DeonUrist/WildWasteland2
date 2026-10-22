using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    // Optional, read-only adapter to the existing NPC Overhaul bake. No assembly dependency or second bake.
    // Discovery also works without NPC Overhaul; unloaded roots never delete a remembered location.
    /// <summary>World locations discovery (camps, POIs, starting area, NPC structure maps, footprints/blocks); POI world-locations part (Wave A split of Patrols Traffic/TrafficLocations.cs).</summary>
    internal static partial class TrafficLocations
    {
        internal static readonly Dictionary<string, CampDiscovery> Known = new Dictionary<string, CampDiscovery>();
        internal static readonly Dictionary<string, PoiDiscovery> Points = new Dictionary<string, PoiDiscovery>();
        private static readonly Dictionary<string, Transform> Roots = new Dictionary<string, Transform>();
        private static readonly HashSet<string> Mapped = new HashSet<string>();
        private static readonly HashSet<string> ExaminedPoints = new HashSet<string>();
        private static readonly Stack<Transform> Scan = new Stack<Transform>();
        private sealed class MapRequest { internal Transform Root; internal Bounds? Box; internal object Structure; }
        private static readonly Queue<MapRequest> MapQueue=new Queue<MapRequest>();
        private static readonly HashSet<Transform> QueuedMaps=new HashSet<Transform>();
        private static float _nextBake;
        private static readonly List<Core.NavStructure> _structures = new List<Core.NavStructure>();   // WW: Core.Services.Npc.Structures (ex NPCAI.Nav._structures reflection)
        internal static int Revision { get; private set; }
        private static bool _discoveryReady;
        internal static bool DiscoveryReady=>_discoveryReady;
        internal static int DiscoveryLeft=>Scan.Count+WorldIndex.Pending;   // diagnostics: transforms still queued for the deep camp scan
        internal static void Changed(){Revision++;}

        internal static void Reset()
        { Known.Clear(); Points.Clear(); ExaminedPoints.Clear(); Roots.Clear(); Mapped.Clear(); Scan.Clear(); MapQueue.Clear();QueuedMaps.Clear();_nextBake = 0f; _structures.Clear();_discoveryReady=false; Revision++; }

        private static WorldWatch _watch;
        private static readonly System.Diagnostics.Stopwatch Budget = new System.Diagnostics.Stopwatch();
        private static readonly long BudgetTicks = (long)(System.Diagnostics.Stopwatch.Frequency * .0003);
        internal static void Install(IModule owner)
        {_watch=WorldIndex.Watch(owner,(t,n)=>t.parent==null||IsCamp(n)||PoiName(n),(t,n,c)=>Scan.Push(t));}
        private static bool MaybeCoyote(string n)
        {
            // exact pre-filter for PrefabName(n) == "Camp_1x": PrefabName only strips a trailing suffix and trims whitespace
            if (n == null) return false; int i = 0; while (i < n.Length && char.IsWhiteSpace(n[i])) i++;
            return n.Length - i >= 7 && string.CompareOrdinal(n, i, "Camp_1", 0, 6) == 0;
        }

        internal static void Tick()
        {
            bool remembered=false;Budget.Reset();Budget.Start();
            while(Scan.Count>0)
            {var t=Scan.Pop();if(t==null)continue;string name=t.name;if(t.parent==null)TrafficCamps.ObserveRoot(t);RememberPoi(t,name);if(IsCamp(name)){Remember(t,null);remembered=true;break;}if(Budget.ElapsedTicks>=BudgetTicks)break;}
            Budget.Stop();
            if(Scan.Count==0&&!WorldIndex.Busy)_discoveryReady=true;
            if(!remembered&&MapQueue.Count>0)
            {
                var next=MapQueue.Dequeue();QueuedMaps.Remove(next.Root);
                if(next.Root!=null)Remember(next.Root,next.Box,next.Structure);
            }
            if (Time.unscaledTime < _nextBake) return;
            _nextBake = Time.unscaledTime + 5f;
            try
            {
                var npc = ModApi.Get<INavApi>();
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
            catch (Exception e) { Core.Log.Warn("Traffic: NPC Overhaul map adapter: " + e.Message); }
        }

        internal static bool IsCamp(string name)
        { return name != null && name.StartsWith("Camp_", StringComparison.OrdinalIgnoreCase) && name.Length > 5 && char.IsDigit(name[5]); }

        internal static bool CoyoteCamp(string name)
        {if(!MaybeCoyote(name))return false;var n=Names.Prefab(name);return n=="Camp_10"||n=="Camp_15"||n=="Camp_16";}
        internal static bool PoiName(string name)
        // The game's real world locations (its Codex lists Camps / Buildings / Wrecks / Caves): Building_1-7, Wreck_1-8, Cave_1-7,
        // Starter_Area and the Coyote camps (Camp_10/15/16). Raider camps are CampDiscovery camps, not POIs. Every location is a target
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
            Points[id]=new PoiDiscovery{id=id,name=Names.Prefab(root.name),position=Origin.World(point)};
            Revision++;
        }

        internal static string Identity(Transform t)
        {
            var p = Origin.World(t.position);
            return Names.Prefab(t.name) + ":" + Mathf.RoundToInt(p.x) + ":" + Mathf.RoundToInt(p.z);
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
            location = new CampDiscovery { id = id, name = Names.Prefab(root.name),cleared=location!=null&&location.cleared,faction=CoyoteCamp(root.name)?"Coyotes":"Scrapyard" };
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
                if (!GroundAt(outer, out var ground)) continue;
                var gate = entrance != null ? entrance.position : interior;
                float score = entrance != null ? FlatDistance(ground, gate) : FlatDistance(ground, fallback);
                bool clear = VehicleClear(ground + Vector3.up * 2f, gate + Vector3.up * 2f, 1.4f);
                if (clear) score -= 1000f;
                if (score >= best) continue;
                best = score; location.apron = ground; location.gate = clear ? gate : ground;
                location.mappedEntrance = clear; found = true;
            }
            if (!found) return; // terrain isn't loaded yet; keep the previous stable record and try later
            if (found)
            {
                location.center = Origin.World(location.center);
                location.apron = Origin.World(location.apron);
                location.gate = Origin.World(location.gate);
                Known[id] = location;
                Revision++;
                if (complete) Mapped.Add(id);
            }
        }

        internal static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        internal static bool TownBuilding(Transform root)
        { for (var t = root; t != null; t = t.parent) if (Modules.POI.TownMarkers.Owns(t)) return true; return false; }
    }
}
