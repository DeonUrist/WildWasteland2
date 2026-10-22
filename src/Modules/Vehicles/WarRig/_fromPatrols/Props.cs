using System.Collections.Generic;
using ES3Internal;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    internal static class Props
    {
        internal sealed class Placement
        {
            public readonly string Node, Name, Hint;
            public readonly Vector3 Position, Rotation;
            public Placement(string node, string name, string hint, Vector3 position, Vector3 rotation)
            { Node = node; Name = name; Hint = hint; Position = position; Rotation = rotation; }
        }
        internal static readonly Placement[] Placed =
        {
            new Placement("bed_rubber_1_1", "bed_rubber_1", "Rustliner/parts", new Vector3(0f, 0.66f, -10.2f), new Vector3(0f, 0f, 0f)),
            new Placement("fridge_2_1", "fridge_2", "Rustliner/parts", new Vector3(1.25f, 0f, -6.5f), new Vector3(0f, 90f, 0f)),
            new Placement("half_barrel_shelf_1_1", "half_barrel_shelf_1", "Rustliner/parts", new Vector3(-1.1f, 0.7f, 1.65f), new Vector3(0f, 0f, 0f)),
            new Placement("floor_metal_4_1", "floor_metal_4", "Rustliner/parts", new Vector3(1f, 2f, -10f), new Vector3(0f, 0f, 0f)),
            new Placement("floor_metal_4_2", "floor_metal_4", "Rustliner/parts", new Vector3(-1f, 2f, -10f), new Vector3(0f, 0f, 0f)),
            new Placement("floor_metal_7_1", "floor_metal_7", "Rustliner/parts", new Vector3(1.8f, 1f, -9f), new Vector3(0f, 90f, 0f)),
            new Placement("interior_car_light_1", "interior_car_light", "Rustliner/parts", new Vector3(0f, 2f, -6f), new Vector3(0f, 0f, 180f)),
            new Placement("interior_car_light_2", "interior_car_light", "Rustliner/parts", new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 180f)),
            new Placement("interior_car_light_3", "interior_car_light", "Rustliner/parts", new Vector3(0f, 2f, 6f), new Vector3(0f, 0f, 180f)),
            new Placement("shelf_1_1", "shelf_1", "decor", new Vector3(1.25f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new Placement("shelf_2_1", "shelf_2", "decor", new Vector3(1.25f, 0f, 2f), new Vector3(0f, 0f, 0f)),
            new Placement("locker_2_1", "locker_2", "decor", new Vector3(1.18f, 0f, -1.5f), new Vector3(0f, 0f, 0f)),
            new Placement("sink_1", "sink", "decor", new Vector3(1.22f, 0f, -7.35f), new Vector3(0f, -90f, 0f)),
            new Placement("half_barrel_shelf_3_1", "half_barrel_shelf_3", "decor", new Vector3(1.2f, 0.08f, -5f), new Vector3(0f, 0f, 0f)),
            new Placement("half_barrel_shelf_3_2", "half_barrel_shelf_3", "decor", new Vector3(1.2f, 0.08f, -3f), new Vector3(0f, 0f, 0f)),
            new Placement("chair_2_1", "chair_2", "decor", new Vector3(-0.7f, 0f, -8.32f), new Vector3(0f, 0f, 0f)),
            new Placement("sofa_3_1", "sofa_3", "decor", new Vector3(-1.25f, -0.05f, 4.9f), new Vector3(0f, 180f, 0f)),
            new Placement("table_1_1", "table_1", "decor", new Vector3(-1.2f, -0.05f, -8.7f), new Vector3(0f, 0f, 0f)),
        };
        public const string Root = "warrig_props", LadderNode = "ladder_1";
        private static Transform templateRoot;
        private static readonly Dictionary<string, float> bottom = new Dictionary<string, float>();
        private static Bounds ladderBounds;
        private const string LadderSource = "ladder_M";
        private static readonly Vector3 LadderPosition = new Vector3(-0.57f, -0.05f, 1.66f);
        private const float LadderYaw = -90f, LadderLength = 2.8f, LadderGrab = 0.5f;

        private static void PreparePhysics(GameObject go)
        { foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) if (!mc.convex) mc.convex = true; }
        // ---------- template ----------
        private static string Path(Transform t) { var s = t.name; for (t = t.parent; t != null; t = t.parent) s = t.name + "/" + s; return s; }
        private static string Base(string n) { int p = n.IndexOf(" ("); return p > 0 ? n.Substring(0, p) : n; }

        private static Dictionary<string, Transform> FindSources()
        {
            var want = new Dictionary<string, Placement>();
            foreach (var k in Placed) want[k.Name] = k;
            var best = new Dictionary<string, Transform>(); var score = new Dictionary<string, int>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null) continue;
                Placement k;
                if (!want.TryGetValue(Base(t.name), out k)) continue;
                if (t.GetComponent<MeshFilter>() == null) continue;
                if (t.parent != null && Base(t.parent.name) == k.Name) continue;           // inner LOD/duplicate mesh of the same prop
                if (t.root.name.StartsWith("WarRig")) continue;                           // our own copies
                var path = Path(t);
                int s = 0;
                if (k.Hint.Length > 0 && path.Contains(k.Hint)) s += 8;
                if (!t.gameObject.scene.IsValid()) s += 4;                                 // a prefab asset beats a scene object
                if (t.GetComponentInChildren<Collider>(true) != null) s += 2;
                if (t.name == k.Name) s += 1;
                int old;
                if (!score.TryGetValue(k.Name, out old) || s > old) { score[k.Name] = s; best[k.Name] = t; }
            }
            return best;
        }

        // Height of the pivot above the lowest point of the prop (renderers + colliders), in the prop's own upright frame, world units.
        private static float Bottom(Transform src)
        {
            float min = float.MaxValue; var inv = Quaternion.Inverse(src.rotation);
            foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var w = inv * (mf.transform.TransformPoint(p) - src.position);
                    if (w.y < min) min = w.y;
                }
            }
            return min == float.MaxValue ? 0f : -min;
        }

        public static void Build(GameObject copy, ES3Prefab es3)
        {
            var root = new GameObject(Root);
            root.transform.SetParent(copy.transform, false);
            templateRoot = root.transform;
            Factory.RefTree(es3, root.transform, "props");
            var src = FindSources();
            int made = 0; var missing = new List<string>();
            foreach (var item in Placed)
            {
                Transform source;
                if (!src.TryGetValue(item.Name, out source)) { missing.Add(item.Name); continue; }
                bottom[item.Name] = Bottom(source);
                var go = Object.Instantiate(source.gameObject, root.transform, false);
                go.name = item.Node; go.transform.localScale = source.lossyScale;
                PreparePhysics(go); go.SetActive(true);
                Factory.RefTree(es3, go.transform, "props/" + go.name);
                Core.Log.Info("[Patrols] " + "War Rig prop " + item.Node + " from " + Path(source));
                made++;
            }
            BuildLadder(root.transform, es3);
            if (missing.Count > 0) Core.Log.Warn("[Patrols] " + "War Rig props not found (not loaded in this game?): " + string.Join(", ", missing.ToArray()));
            Core.Log.Info("[Patrols] " + "War Rig props: " + made + " objects.");
        }

        private static void BuildLadder(Transform root, ES3Prefab es3)
        {
            Transform src = null; int best = -1;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || Base(t.name) != LadderSource || t.GetComponent<MeshFilter>() == null) continue;
                if (t.root.name.StartsWith("WarRig")) continue;
                int sc = (t.Find("ladder_trigger") != null ? 4 : 0) + (!t.gameObject.scene.IsValid() ? 2 : 0) + (t.name == LadderSource ? 1 : 0);
                if (sc > best) { best = sc; src = t; }
            }
            if (src == null) { Core.Log.Warn("[Patrols] " + "War Rig ladder: no '" + LadderSource + "' object loaded."); return; }
            var go = Object.Instantiate(src.gameObject, root, false);
            go.name = LadderNode;
            go.SetActive(true);
            var mf = go.GetComponent<MeshFilter>();
            ladderBounds = mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.73f, 3.7f, 0.08f));
            var trigger = EnableLadderTrigger(go.transform, null);
            Factory.RefTree(es3, go.transform, "props/" + LadderNode);
            Core.Log.Info("[Patrols] " + "War Rig ladder from " + Path(src) + ", mesh height " + ladderBounds.size.y.ToString("0.00") + " m, climb trigger active=" + trigger.gameObject.activeSelf);
        }

        // Some camp prefabs keep this child disabled. The mesh alone cannot activate UseLadder.
        private static BoxCollider EnableLadderTrigger(Transform ladder, ES3Prefab es3)
        {
            var trig = ladder.Find("ladder_trigger");
            bool added = trig == null;
            if (trig == null) { trig = new GameObject("ladder_trigger").transform; trig.SetParent(ladder, false); }
            var bc = trig.GetComponent<BoxCollider>();
            if (bc == null) { bc = trig.gameObject.AddComponent<BoxCollider>(); added = true; }
            trig.gameObject.tag = "Ladder"; trig.gameObject.layer = 2;
            trig.localPosition = Vector3.zero; trig.localRotation = Quaternion.identity; trig.localScale = Vector3.one;
            bc.isTrigger = true; bc.enabled = true;
            trig.gameObject.SetActive(true);
            if (added && es3 != null) Factory.RefTree(es3, ladder, "props/" + LadderNode);
            return bc;
        }

        private static void ApplyLadder(Transform root)
        {
            var t = root.Find(LadderNode);
            if (t == null) return;
            t.gameObject.SetActive(true);
            Vector3 p = LadderPosition; float yaw = LadderYaw;
            var b = ladderBounds; float h = Mathf.Max(0.1f, b.size.y);
            float sy = LadderLength / h;
            t.localScale = new Vector3(1f, sy, 1f);
            t.localRotation = Quaternion.Euler(0f, S.Yaw + yaw, 0f);
            t.localPosition = Layout.M(p) + Vector3.up * (-b.min.y * sy);
            var bc = EnableLadderTrigger(t, root.parent.GetComponent<ES3Prefab>());
            float top = b.max.y + LadderGrab / sy;
            bc.center = new Vector3(b.center.x, (b.min.y + top) * 0.5f, b.center.z);
            bc.size = new Vector3(Mathf.Max(0.3f, b.size.x), top - b.min.y, Mathf.Max(0.13f, b.size.z + 0.05f));
        }

        // ---------- live placement ----------
        private static void RemoveCopy(GameObject rig, Transform t)
        {
            t.gameObject.SetActive(false);
            var es3 = rig.GetComponent<ES3Prefab>();
            if (es3 != null)
                foreach (var child in t.GetComponentsInChildren<Transform>(true))
                {
                    es3.localRefs.Remove(child.gameObject);
                    foreach (var c in child.GetComponents<Component>()) es3.localRefs.Remove(c);
                }
            Object.DestroyImmediate(t.gameObject);
        }

        private static Transform AddCopy(GameObject rig, Transform root, string name)
        {
            var source = templateRoot != null ? templateRoot.Find(name) : null;
            if (source == null) return null;
            // Keep scripts/colliders inactive until the copy has its refs and placement.
            bool active = source.gameObject.activeSelf;
            GameObject go;
            source.gameObject.SetActive(false);
            try { go = Object.Instantiate(source.gameObject, root, false); }
            finally { source.gameObject.SetActive(active); }
            go.name = name;
            var es3 = rig.GetComponent<ES3Prefab>();
            if (es3 != null) Factory.RefTree(es3, go.transform, "props/" + name);
            return go.transform;
        }

        public static void Apply(GameObject rig)
        {
            var root = rig.transform.Find(Root);
            if (root == null) return;
            ApplyLadder(root);
            var keep = new HashSet<string>();
            keep.Add(LadderNode);
            foreach (var item in Placed)
            {
                keep.Add(item.Node);
                var t = root.Find(item.Node);
                if (t == null) t = AddCopy(rig, root, item.Node);
                if (t == null) continue;
                float lift; bottom.TryGetValue(item.Name, out lift);
                t.localPosition = Layout.M(item.Position) + Vector3.up * lift;
                t.localRotation = Layout.Rot * Quaternion.Euler(item.Rotation);
                t.gameObject.SetActive(true);
            }
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var t = root.GetChild(i);
                if (!keep.Contains(t.name)) RemoveCopy(rig, t);
            }
        }

        // After a save load the props' meshes/materials may come back unresolved (camp meshes are not in the save's reference list):
        // copy them from the template's identical hierarchy.
        public static void Rebind(GameObject rig)
        {
            var root = rig.transform.Find(Root); var troot = Factory.Template().transform.Find(Root);
            if (root == null || troot == null) return;
            foreach (Transform prop in root)
            {
                var tp = troot.Find(prop.name);
                if (tp == null) continue;
                var a = prop.GetComponentsInChildren<Transform>(true); var b = tp.GetComponentsInChildren<Transform>(true);
                for (int n = 0; n < a.Length && n < b.Length; n++)
                {
                    var mf = a[n].GetComponent<MeshFilter>(); var tmf = b[n].GetComponent<MeshFilter>();
                    if (mf != null && tmf != null && mf.sharedMesh != tmf.sharedMesh) mf.sharedMesh = tmf.sharedMesh;
                    var mr = a[n].GetComponent<MeshRenderer>(); var tmr = b[n].GetComponent<MeshRenderer>();
                    if (mr != null && tmr != null) { mr.sharedMaterials = tmr.sharedMaterials; mr.enabled = tmr.enabled; }
                    var mc = a[n].GetComponent<MeshCollider>(); var tmc = b[n].GetComponent<MeshCollider>();
                    if (mc != null && tmc != null)
                    {
                        mc.convex = tmc.convex;
                        if (mc.sharedMesh != tmc.sharedMesh) mc.sharedMesh = tmc.sharedMesh;
                    }
                }
            }
        }
    }
}
