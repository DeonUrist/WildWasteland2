using System;
using System.Collections.Generic;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.ObjectsGenerators;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.NPC
{
    // (WW) Developer: "Setup POI pathfinding". One copy of every native POI type (MapMagic's object outputs: Building_/Camp_/Cave_/Wreck_)
    // is placed in rows starting 200 m from the starting camp, stripped to its geometry (no FSMs or scripts: no spawners, loot or save
    // entries), never saved, removed when the option goes off. Core.Services.PoiNavSetup stops Towns generating new towns meanwhile.
    // Also runs the bake queue (setup copies or any POI the developer asks for).
    internal static class PoiNavSetup
    {
        private static GameObject _holder, _staging;
        private static readonly List<GameObject> _prefabs = new List<GameObject>();
        private static readonly List<Transform> _placed = new List<Transform>();
        private static int _next;
        private static Vector3 _cursor, _rowDir, _colDir;
        private static float _rowWidth, _rowDepth;
        internal static string Status = "";
        private static readonly Queue<Transform> _queue = new Queue<Transform>();
        internal static PoiNavBake Baking;
        internal static int Baked, Failed;

        internal static void Roots(List<Transform> into) { foreach (var t in _placed) if (t != null) into.Add(t); }
        internal static int Placed { get { return _placed.Count; } }
        internal static bool Active { get { return _holder != null; } }

        // every frame: follows the setting, places one POI per frame, runs the bake queue
        internal static void Tick()
        {
            bool want = PoiNav.Setup != null && PoiNav.Setup.Value && Plugin.EnableDebug.Value && Plugin.DeveloperMode.Value && GameRefs.InPlay;
            Services.PoiNavSetup = PoiNav.Setup != null && PoiNav.Setup.Value;
            if (want && _holder == null && !SaveBus.Busy) Start();
            else if (!want && _holder != null && !(PoiNav.Setup != null && PoiNav.Setup.Value && !GameRefs.InPlay)) Clear();
            if (_holder != null && _next < _prefabs.Count && GameRefs.InGame) PlaceOne();
            BakeTick();
        }

        static void Start()
        {
            _prefabs.Clear(); _placed.Clear(); _next = 0;
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
            _prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            // the starting camp (or the player) is the anchor; rows run away from it
            Vector3 camp = GameRefs.Player != null ? GameRefs.Player.transform.position : Vector3.zero;
            var roots = new List<Transform>(); Nav.Roots(roots);
            foreach (var r in roots) if (r != null && r.name.StartsWith("Starter_Area", StringComparison.Ordinal)) { camp = r.position; break; }
            Vector3 away = GameRefs.Player != null ? GameRefs.Player.transform.position - camp : Vector3.forward; away.y = 0f;
            if (away.sqrMagnitude < 1f) away = Vector3.forward;
            _rowDir = Vector3.Cross(Vector3.up, away.normalized); _colDir = away.normalized;
            _cursor = camp + _colDir * 200f; _rowWidth = 0f; _rowDepth = 0f;
            _holder = new GameObject("WW.PoiNavSetup");
            _staging = new GameObject("staging (inactive)"); _staging.SetActive(false); _staging.transform.SetParent(_holder.transform, false);
            Status = "placing " + _prefabs.Count + " POI types";
            Log.Always("[NPC] POI pathfinding setup: placing " + _prefabs.Count + " native POI types from " + _cursor.ToString("F0") + "; new towns paused.");
        }

        static void PlaceOne()
        {
            var prefab = _prefabs[_next++];
            try
            {
                var go = Object.Instantiate(prefab, _staging.transform, false);   // under an inactive parent: no Awake before stripping
                go.SetActive(false);
                go.name = prefab.name + " (WW POI nav setup)";
                Strip(go);
                // footprint from the colliders, rows of up to 600 m
                Bounds b = new Bounds(); bool any = false;
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger) continue;
                    var cb = Local(c, go.transform);
                    if (!any) { b = cb; any = true; } else b.Encapsulate(cb);
                }
                if (!any) b = new Bounds(Vector3.zero, Vector3.one * 10f);
                float w = b.size.x + 20f, d = b.size.z + 20f;
                if (_rowWidth + w > 600f) { _cursor += _colDir * (_rowDepth + 20f) - _rowDir * _rowWidth; _rowWidth = 0f; _rowDepth = 0f; }
                Vector3 at = _cursor + _rowDir * (w * .5f) - _rowDir * b.center.x - _colDir * b.center.z;
                _cursor += _rowDir * w; _rowWidth += w; _rowDepth = Mathf.Max(_rowDepth, d);
                RaycastHit h;
                at.y = Physics.Raycast(at + Vector3.up * 2000f, Vector3.down, out h, 4000f, 1 << 14, QueryTriggerInteraction.Ignore) ? h.point.y : at.y;
                go.transform.SetParent(_holder.transform, false);
                go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(_colDir));
                go.SetActive(true);
                _placed.Add(go.transform);
                Status = "placed " + _placed.Count + " / " + _prefabs.Count;
            }
            catch (Exception e) { Log.Warn("[NPC] POI pathfinding setup: " + prefab.name + ": " + e.Message); }
            if (_next >= _prefabs.Count) Log.Always("[NPC] POI pathfinding setup: " + _placed.Count + " POI types placed.");
        }

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
        static void Strip(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is Collider || c is MeshFilter || c is Renderer || c is LODGroup) continue;
                try { Object.DestroyImmediate(c); } catch (Exception) { }
            }
        }

        static void Clear()
        {
            if (_holder != null) Object.Destroy(_holder);
            _holder = null; _placed.Clear(); _prefabs.Clear(); _next = 0; _queue.Clear(); Baking = null;
            Status = "off"; Services.PoiNavSetup = false;
            Log.Always("[NPC] POI pathfinding setup removed.");
        }

        // ------------------------------------------------------------------ baking
        internal static void BakeAll() { foreach (var t in _placed) if (t != null) _queue.Enqueue(t); Baked = Failed = 0; }
        internal static void Bake(Transform root) { if (root != null) _queue.Enqueue(root); }
        internal static int Queued { get { return _queue.Count + (Baking != null ? 1 : 0); } }

        static void BakeTick()
        {
            if (Baking == null)
            {
                if (_queue.Count == 0) return;
                var root = _queue.Dequeue(); if (root == null) return;
                Baking = new PoiNavBake(root, PoiNavStore.Get(PoiNavStore.TypeOf(root.name)));
            }
            Baking.Step(PoiNav.BakeBudgetMs.Value);
            if (!Baking.Done) return;
            if (Baking.Result != null)
            {
                try
                {
                    PoiNavStore.Save(Baking.Result); Baked++;
                    PoiNav.Forget(Baking.Poi);
                    Log.Always("[NPC] POI navigation baked: " + Baking.Poi + " " + Baking.Status + " -> " + PoiNavStore.ConfigPath(Baking.Poi));
                }
                catch (Exception e) { Failed++; Log.Warn("[NPC] POI navigation " + Baking.Poi + " not saved: " + e.Message); }
            }
            else { Failed++; Log.Warn("[NPC] POI navigation bake " + Baking.Poi + " " + Baking.Status); }
            Baking = null;
        }

        // ------------------------------------------------------------------ test NPCs (unsaved native Scraffa, Scrapyard melee)
        private static readonly List<GameObject> _tests = new List<GameObject>();
        internal static int Tests { get { _tests.RemoveAll(t => t == null); return _tests.Count; } }
        internal static string SpawnTest(Vector3 feet)
        {
            var manager = ES3Internal.ES3ReferenceMgrBase.Current;
            var prefab = manager == null ? null : manager.GetPrefab(2524626245796737373L, true);
            if (prefab == null) return "native Scraffa prefab unavailable";
            var actor = Object.Instantiate(prefab.gameObject, feet + Vector3.up * 1.05f, Quaternion.identity);
            actor.name = prefab.name + " (WW POI nav test)";
            var save = FsmUtil.Find(actor, "saveItemVar"); if (save != null) save.enabled = false;   // never written to the save
            Brain.Spawned(actor);
            _tests.Add(actor);
            return "Test NPC " + _tests.Count + " placed. Close the editor to watch; F11 (god mode) keeps it from seeing you.";
        }
        internal static void SendTests(Vector3 point)
        {
            foreach (var t in _tests) if (t != null) Idle.Travel(t, point, .6f);
        }
        internal static void ClearTests() { foreach (var t in _tests) if (t != null) Object.Destroy(t); _tests.Clear(); }

        internal static void Shift(Vector3 d) { _cursor += d; }
        internal static void Reset() { ClearTests(); if (_holder != null) Object.Destroy(_holder); _holder = null; _placed.Clear(); _prefabs.Clear(); _queue.Clear(); Baking = null; Services.PoiNavSetup = false; }
    }
}
