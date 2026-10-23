using System;
using System.Collections.Generic;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.ObjectsGenerators;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Tools
{
    // (WW) Developer: "Setup POI pathfinding". One native POI type at a time (MapMagic's object outputs: Building_/Camp_/Cave_/Wreck_)
    // is placed 200 m from the starting camp, stripped to its geometry (no FSMs or scripts: no spawners, loot or save
    // entries), never saved, removed when the option goes off. Core.Services.PoiNavSetup stops Towns generating new towns meanwhile.
    // Also runs the bake queue (setup copies or any POI the developer asks for).
    internal static class PoiNavSetup
    {
        private static GameObject _holder, _staging;
        private static readonly List<GameObject> _prefabs = new List<GameObject>();
        private static readonly List<string> _types = new List<string>();
        private static IPoiNavigationGeometry _geometry;
        private static readonly List<Transform> _placed = new List<Transform>();
        private static int _next;
        private static Vector3 _cursor, _rowDir, _colDir;
        internal static string Status = "";
        private static readonly Queue<Transform> _queue = new Queue<Transform>();
        internal static PoiNavBake Baking;
        internal static int Baked, Failed;

        /// <summary>Sets the real navigation generation hold while unsaved setup copies are active.</summary>
        private static void SetSuspended(bool value) { var navigation = ModApi.Get<INavApi>(); if (navigation != null) navigation.WorldGenerationSuspended = value; }
        /// <summary>Appends live unsaved setup roots to native navigation discovery.</summary>
        internal static void Roots(List<Transform> into) { foreach (var t in _placed) if (t != null) into.Add(t); }
        /// <summary>Reports the number of retained setup copies.</summary>
        internal static int Placed { get { return _placed.Count; } }
        internal static bool Active { get { return _holder != null; } }
        internal static bool Preparing { get { return Active && _placed.Count == 0 && _next < _types.Count; } }
        internal static Transform Current { get { return _placed.Count == 0 ? null : _placed[0]; } }

        /// <summary>Replaces the setup copy with the previous or next native POI type.</summary>
        internal static Transform Cycle(int direction)
        {
            if (!Active || _types.Count == 0 || Queued != 0 || _geometry != null) return Current;
            _next = (_next - 1 + direction + _types.Count) % _types.Count;
            ClearTests();
            foreach (var t in _placed) if (t != null) { t.gameObject.SetActive(false); Object.Destroy(t.gameObject); }
            _placed.Clear(); PlaceOne(); return Current;
        }

        // every frame: follows the setting, retains one setup POI, runs the bake queue
        /// <summary>Reconciles setup activation and advances placement and sliced baking.</summary>
        internal static void Tick()
        {
            if (NavigationTools.Source == null) { Reset(); return; }
            bool want = NavigationTools.Setup != null && NavigationTools.Setup.Value && Plugin.EnableDebug.Value && Plugin.DeveloperMode.Value && GameRefs.InPlay;
            SetSuspended(NavigationTools.Setup != null && NavigationTools.Setup.Value);
            if (want && _holder == null && !SaveBus.Busy) Start();
            else if (!want && _holder != null && !(NavigationTools.Setup != null && NavigationTools.Setup.Value && !GameRefs.InPlay)) Clear();
            if (_holder != null && _placed.Count == 0 && _next < _types.Count && GameRefs.InGame) PlaceOne();
            BakeTick();
        }

        /// <summary>Collects native structure prefabs and prepares the unsaved setup area.</summary>
        static void Start()
        {
            _prefabs.Clear(); _types.Clear(); _placed.Clear(); _next = 0;
            var mm = Object.FindObjectOfType<MapMagicObject>();
            if (mm == null || mm.graph == null) { Status = "MapMagic not found"; return; }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gen in mm.graph.generators)
            {
                var output = gen as ObjectsOutput; if (output == null) continue;
                foreach (var prefab in output.prefabs)
                {
                    if (prefab == null) continue;
                    string n = prefab.name;
                    if (!(n.StartsWith("Building_", StringComparison.Ordinal) || n.StartsWith("Camp_", StringComparison.Ordinal)
                        || n.StartsWith("Cave_", StringComparison.Ordinal) || n.StartsWith("Wreck_", StringComparison.Ordinal))) continue;
                    if (names.Add(n)) _prefabs.Add(prefab);
                }
            }
            var authoring = ModApi.Get<IPoiAuthoring>();
            if (authoring != null) { var extra = new List<string>(); authoring.Types(extra); foreach (string name in extra) names.Add(name); }
            _types.AddRange(names); _types.Sort(StringComparer.Ordinal);
            // the starting camp (or the player) is the anchor; rows run away from it
            Vector3 camp = GameRefs.Player != null ? GameRefs.Player.transform.position : Vector3.zero;
            var roots = new List<Transform>(); NavigationTools.Source.Roots(roots);
            foreach (var r in roots) if (r != null && r.name.StartsWith("Starter_Area", StringComparison.Ordinal)) { camp = r.position; break; }
            Vector3 away = GameRefs.Player != null ? GameRefs.Player.transform.position - camp : Vector3.forward; away.y = 0f;
            if (away.sqrMagnitude < 1f) away = Vector3.forward;
            _rowDir = Vector3.Cross(Vector3.up, away.normalized); _colDir = away.normalized;
            _cursor = camp + _colDir * 200f;
            _holder = new GameObject("WW.PoiNavSetup");
            _staging = new GameObject("staging (inactive)"); _staging.SetActive(false); _staging.transform.SetParent(_holder.transform, false);
            Status = "selecting one of " + _types.Count + " POI types";
            Log.Always(null,"[NPC] POI pathfinding setup: one native POI at a time from " + _cursor.ToString("F0") + "; new towns paused.");
        }

        /// <summary>Places and strips one native structure copy for explicit authoring.</summary>
        static void PlaceOne()
        {
            string type = _types[_next];
            GameObject go = null;
            try
            {
                var authoring = ModApi.Get<IPoiAuthoring>();
                if (_geometry == null && authoring != null) _geometry = authoring.CreateNavigationGeometry(type, _staging.transform);
                if (_geometry != null)
                {
                    Status = "Preparing " + type;
                    if (!_geometry.Step()) return;
                    go = _geometry.Root; _geometry = null;
                }
                else
                {
                    var prefab = _prefabs.Find(p => p != null && p.name == type);
                    if (prefab == null) throw new InvalidOperationException("POI provider unavailable");
                    go = Object.Instantiate(prefab, _staging.transform, false);
                }
                _next++;
                go.SetActive(false);
                go.name = type + " (WW POI nav setup)";
                Strip(go);
                // footprint from the colliders, centred on the shared setup position
                Bounds b = new Bounds(); bool any = false;
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger) continue;
                    var cb = Local(c, go.transform);
                    if (!any) { b = cb; any = true; } else b.Encapsulate(cb);
                }
                if (!any) b = new Bounds(Vector3.zero, Vector3.one * 10f);
                Vector3 at = _cursor - _rowDir * b.center.x - _colDir * b.center.z;
                RaycastHit h;
                at.y = Physics.Raycast(at + Vector3.up * 2000f, Vector3.down, out h, 4000f, 1 << 14, QueryTriggerInteraction.Ignore) ? h.point.y : at.y;
                go.transform.SetParent(_holder.transform, false);
                go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(_colDir));
                go.SetActive(true);
                _placed.Add(go.transform);
                Status = type + " (" + _next + " / " + _types.Count + ")";
                Log.Always(null,"[NPC] POI pathfinding setup: showing " + Status + "; one setup copy.");
            }
            catch (Exception e)
            {
                if (_geometry != null) { go = _geometry.Root; _geometry = null; }
                if (go != null) Object.Destroy(go);
                _next = _types.IndexOf(type) + 1;
                Status = type + ": " + e.Message;
                Log.Warn(null,"[NPC] POI pathfinding setup: " + Status);
            }
        }

        /// <summary>Converts collider bounds into setup-root local coordinates.</summary>
        static Bounds Local(Collider c, Transform root)
        {
            var box = c as BoxCollider;
            Vector3 center, size;
            if (box != null) { center = box.center; size = box.size; }
            else { var m = c as MeshCollider; if (m != null && m.sharedMesh != null) { center = m.sharedMesh.bounds.center; size = m.sharedMesh.bounds.size; } else { center = Vector3.zero; size = Vector3.one; } }
            var t = c.transform; Bounds r = new Bounds(); bool first = true;
            for (int k = 0; k < 8; k++)
            {
                var p = root.InverseTransformPoint(t.TransformPoint(center + Vector3.Scale(size * .5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
                if (first) { r = new Bounds(p, Vector3.zero); first = false; } else r.Encapsulate(p);
            }
            return r;
        }

        // geometry only: every script / FSM / rigidbody / audio / particle goes (the copy is inactive while this runs, so nothing woke)
        /// <summary>Removes gameplay scripts from an unsaved geometry-only setup copy.</summary>
        static void Strip(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is Collider || c is MeshFilter || c is Renderer || c is LODGroup) continue;
                try { Object.DestroyImmediate(c); } catch (Exception) { }
            }
        }

        /// <summary>Destroys setup copies and clears queued work and generation suspension.</summary>
        static void Clear()
        {
            if (_holder != null) Object.Destroy(_holder);
            _holder = null; _placed.Clear(); _prefabs.Clear(); _types.Clear(); _geometry = null; _next = 0; _queue.Clear(); Baking = null;
            Status = "off"; SetSuspended(false);
            Log.Always(null,"[NPC] POI pathfinding setup removed.");
        }

        // ------------------------------------------------------------------ baking
        /// <summary>Queues every placed root for rebaking and resets completion counters.</summary>
        internal static void BakeAll() { foreach (var t in _placed) if (t != null) _queue.Enqueue(t); Baked = Failed = 0; }
        /// <summary>Queues one requested root for authoring bake.</summary>
        internal static void Bake(Transform root) { if (root != null) _queue.Enqueue(root); }
        /// <summary>Counts queued roots and the currently active bake.</summary>
        internal static int Queued { get { return _queue.Count + (Baking != null ? 1 : 0); } }

        /// <summary>Advances one bounded bake and publishes successful data through its owner.</summary>
        static void BakeTick()
        {
            if (Baking == null)
            {
                if (_queue.Count == 0) return;
                var root = _queue.Dequeue(); if (root == null) return;
                Baking = new PoiNavBake(root, NavigationTools.Source.Get(NavigationTools.Source.TypeOf(root.name)));
            }
            Baking.Step(NavigationTools.BakeBudgetMs.Value);
            if (!Baking.Done) return;
            if (Baking.Result != null)
            {
                try
                {
                    NavigationTools.Source.Save(Baking.Result); Baked++;
                    NavigationTools.Source.Forget(Baking.Poi);
                    Log.Always(null,"[NPC] POI navigation baked: " + Baking.Poi + " " + Baking.Status + " -> " + NavigationTools.Source.ConfigPath(Baking.Poi));
                }
                catch (Exception e) { Failed++; Log.Warn(null,"[NPC] POI navigation " + Baking.Poi + " not saved: " + e.Message); }
            }
            else { Failed++; Log.Warn(null,"[NPC] POI navigation bake " + Baking.Poi + " " + Baking.Status); }
            Baking = null;
        }

        // ------------------------------------------------------------------ test NPCs (unsaved native Scraffa, Scrapyard melee)
        private static readonly List<GameObject> _tests = new List<GameObject>();
        /// <summary>Prunes destroyed test actors and reports the live count.</summary>
        internal static int Tests { get { _tests.RemoveAll(t => t == null); return _tests.Count; } }
        /// <summary>Creates an unsaved native actor and initializes its real NPC navigation.</summary>
        internal static string SpawnTest(Vector3 feet)
        {
            var manager = ES3Internal.ES3ReferenceMgrBase.Current;
            var prefab = manager == null ? null : manager.GetPrefab(2524626245796737373L, true);
            if (prefab == null) return "native Scraffa prefab unavailable";
            var actor = Object.Instantiate(prefab.gameObject, feet + Vector3.up * 1.05f, Quaternion.identity);
            actor.name = prefab.name + " (WW POI nav test)";
            var save = FsmUtil.Find(actor, "saveItemVar"); if (save != null) save.enabled = false;   // never written to the save
            NavigationTools.Source.NotifyTestSpawn(actor);
            _tests.Add(actor);
            return "Test NPC " + _tests.Count + " placed. Close the editor to watch; F11 (god mode) keeps it from seeing you.";
        }
        /// <summary>Requests normal NPC travel for all retained test actors.</summary>
        internal static void SendTests(Vector3 point)
        {
            foreach (var t in _tests) if (t != null) ModApi.Get<INpcApi>()?.Travel(t, point, .6f);
        }
        /// <summary>Destroys setup copies and clears queued work and generation suspension.</summary>
        internal static void ClearTests() { foreach (var t in _tests) if (t != null) Object.Destroy(t); _tests.Clear(); }

        /// <summary>Adjusts the setup placement cursor after an origin jump.</summary>
        internal static void Shift(Vector3 d) { _cursor += d; }
        /// <summary>Destroys every unsaved test actor and clears the registry.</summary>
        internal static void Reset() { ClearTests(); if (_holder != null) Object.Destroy(_holder); _holder = null; _placed.Clear(); _prefabs.Clear(); _types.Clear(); _geometry = null; _queue.Clear(); Baking = null; SetSuspended(false); }
    }
}
