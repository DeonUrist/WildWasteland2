using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    // The weapon in her hand in third person = the game's own NPC prop for that weapon (Flexa's akm_trash_model, Sprokka's 22_pipe_pistol,
    // Lugnut's slamberg_500_chopped ...), at the exact spot the NPC prefab holds it on its mixamorig:LeftHand (guns) / RightHand (blades).
    // Same skeleton, same animations -> it sits in her hand like in theirs.
    internal static partial class Props
    {
        /// <summary>Item = the weapon's own world model (the item you pick up), used when no NPC carries the weapon; Ref = the stand-in NPC prop that gives it its starting place in the hand</summary>
        internal sealed class Prop { public string Key; public GameObject Source; public string Hand; public string Owner; public bool Item; public Prop Ref; }
        private static readonly Dictionary<string, Prop> _items = new Dictionary<string, Prop>();

        private static readonly Dictionary<string, Prop> _catalog = new Dictionary<string, Prop>();
        private static bool _built;

        /// <summary>Prop catalog key of a weapon object name ("akm_trash_model (1)" -&gt; "akm_trash").</summary>
        internal static string Norm(string n) { return Core.Names.Weapon(n); }

        private static float _nextBuild;
        /// <summary>Catalogs the weapon models in the hands of NPC prefab assets.</summary>
        private static void Build()
        {
            _built = true;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.gameObject.scene.IsValid()) continue;   // prefab assets only
                if (t.name != "mixamorig:LeftHand" && t.name != "mixamorig:RightHand") continue;
                string owner = t.root != null ? t.root.name : "?";
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    if (c.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                    string key = Norm(c.name);
                    if (_catalog.ContainsKey(key)) continue;
                    _catalog[key] = new Prop { Key = key, Source = c.gameObject, Hand = t.name, Owner = owner };
                }
            }
            var keys = new List<string>(_catalog.Keys); keys.Sort();
            if (Core.Log.L1) Core.Log.Main(PresentationModule.Instance, "Props: " + _catalog.Count + " NPC weapon models found (" + string.Join(", ", keys.ToArray()) + ")");
        }

        /// <summary>NPC prop for the player's weapon (first-person object name), with a stand-in of the same kind when no NPC carries it</summary>
        public static Prop Find(string weapon)
        {
            // (B05 perf, README 8 #5) an empty catalog (prefabs not loaded yet) is rescanned at most every 2 s, not on every call
            if (!_built || _catalog.Count == 0 && Time.unscaledTime >= _nextBuild) { _nextBuild = Time.unscaledTime + 2f; Build(); }
            if (string.IsNullOrEmpty(weapon) || KindOf(weapon) == Kind.None) return null;   // bare hands: nothing in them
            string w = Norm(weapon);
            Prop p;
            if (_catalog.TryGetValue(w, out p)) return p;
            foreach (var kv in _catalog) if (kv.Key.StartsWith(w) || w.StartsWith(kv.Key)) return kv.Value;
            string[] standIn;
            switch (KindOf(weapon))
            {
                case Kind.Pistol: standIn = w.Contains("smg") || w.Contains("borz") ? new[] { "borz_smg", "22_pipe_smg" } : new[] { "22_pipe_pistol", "folk_17" }; break;
                case Kind.Melee: standIn = new[] { "machete", "old_knife", "shiv" }; break;
                case Kind.Throw: standIn = new[] { "machete", "pipe_wrench", "old_knife" }; break;   // blast lance: its own world model, held like a machete
                default:
                    if (w.Contains("slam") || w.Contains("rochester")) standIn = new[] { "slamberg_500_chopped", "rochester_m24_chopped" };
                    else if (w.Contains("smg") || w.Contains("borz")) standIn = new[] { "borz_smg", "22_pipe_smg" };
                    else if (w.Contains("crossbow")) standIn = new[] { "crossbow" };
                    else standIn = new[] { "akms", "m16a1", "akm_trash" };
                    break;
            }
            Prop stand = null;
            foreach (var s in standIn) if (_catalog.TryGetValue(s, out stand)) break;
            // no NPC carries it (redmark_m11, redmark_m11_scoped, the long rochester_m24 / slamberg_500): the weapon's own world model, placed
            // where the stand-in sits
            var item = ItemModel(w);
            if (item == null && KindOf(weapon) == Kind.Throw) item = SceneItem(w);
            if (KindOf(weapon) == Kind.Throw && item == null) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Props: no model for " + weapon); return null; }
            if (item != null && stand != null) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Props: no NPC model for " + weapon + ", using its world item model (placed like " + stand.Key + ")"); return new Prop { Key = w, Source = item, Hand = stand.Hand, Owner = "item", Item = true, Ref = stand }; }
            if (stand != null) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Props: no NPC model for " + weapon + ", using " + stand.Key); return stand; }
            return null;
        }

        /// <summary>the item prefab (a root prefab asset named like the weapon, with a mesh and a Rigidbody/Collider) - not the first-person model</summary>
        private static GameObject ItemModel(string w)
        {
            Prop cached;
            if (_items.TryGetValue(w, out cached)) return cached != null ? cached.Source : null;
            GameObject best = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.scene.IsValid() || go.transform.parent != null) continue;
                if (Norm(go.name) != w) continue;
                if (go.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                if (go.GetComponent<Rigidbody>() == null && go.GetComponentInChildren<Collider>(true) == null) continue;
                best = go; break;
            }
            _items[w] = best != null ? new Prop { Key = w, Source = best } : null;
            if (best == null) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Props: no world item model named " + w); }
            return best;
        }

        /// <summary>no prefab asset loaded: a copy of one lying in the world (blastlance_1(Clone)178 ...), not the player's own first-person one</summary>
        private static GameObject SceneItem(string w)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid() || go.transform.parent != null || Norm(go.name) != w) continue;
                if (go.GetComponentInChildren<MeshRenderer>(true) == null || go.GetComponent<Rigidbody>() == null) continue;
                return go;
            }
            return null;
        }

        /// <summary>a render-only copy of the prop, parented to the given hand bone at the NPC's local pose</summary>
        public static GameObject Instantiate(Prop p, Transform hand, bool mirrorToRightHand)
        {
            var holder = new GameObject("Apocaplayer.PropHolder");
            holder.SetActive(false);
            var go = UnityEngine.Object.Instantiate(p.Source, holder.transform, false);
            go.name = "ApocaplayerProp_" + p.Key;
            // skinned parts (the crossbow's bow limbs / string): kept when their bones are inside the copy, else turned into a plain mesh in
            // its bind pose (their bones would be the NPC's)
            var keep = new HashSet<Component>();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                bool inside = smr.rootBone == null || smr.rootBone.IsChildOf(go.transform);
                foreach (var b in smr.bones) if (b == null || !b.IsChildOf(go.transform)) { inside = false; break; }
                if (inside) { smr.updateWhenOffscreen = true; keep.Add(smr); continue; }
                var host = smr.gameObject; var mesh = smr.sharedMesh; var mats = smr.sharedMaterials;
                UnityEngine.Object.DestroyImmediate(smr);
                host.AddComponent<MeshFilter>().sharedMesh = mesh;
                host.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
            // LOD groups (item models): only the most detailed level is drawn, the others' renderers go
            foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                if (lods.Length < 2) continue;
                var keepR = new HashSet<Renderer>(lods[0].renderers);
                for (int i = 1; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers) if (r != null && !keepR.Contains(r)) UnityEngine.Object.DestroyImmediate(r);
            }
            // strip everything but transforms and mesh rendering (FSMs, lights, colliders, audio, animators ...) while nothing has woken up
            var comps = go.GetComponentsInChildren<Component>(true);
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in comps)
                {
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || keep.Contains(c)) continue;
                    try { UnityEngine.Object.DestroyImmediate(c); } catch (Exception) { }
                }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // Render-only held props: native flashlight excludes Ignore Raycast; the camera and sun include it.
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            var src = p.Source.transform;
            go.transform.SetParent(hand, false);
            go.transform.localPosition = src.localPosition;
            go.transform.localRotation = src.localRotation;
            if (mirrorToRightHand)
            {
                // the left-hand pose reflected across the body's middle plane (in Flexa's bind pose), expressed under the right hand
                var L = BindWorld("mixamorig:LeftHand"); var R = BindWorld("mixamorig:RightHand");
                var S = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
                var P = Matrix4x4.TRS(src.localPosition, src.localRotation, Vector3.one);
                var M = R.inverse * S * L * P * S;
                go.transform.localPosition = M.GetColumn(3);
                go.transform.localRotation = Quaternion.LookRotation(M.GetColumn(2), M.GetColumn(1));
            }
            go.transform.localScale = src.localScale;
            go.SetActive(true);
            UnityEngine.Object.Destroy(holder);
            return go;
        }

        /// <summary>Inventory renderers are hidden by the game; the stripped visual copy is visible.</summary>
        public static GameObject CopyForMount(GameObject item, Transform parent)
        {
            var copy = Instantiate(new Prop { Source = item, Key = Norm(item.name) }, parent, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            var scale = item.transform.lossyScale;
            copy.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true; r.forceRenderingOff = false;
                for (var t = r.transform; t != null && t != copy.transform; t = t.parent) t.gameObject.SetActive(true);
            }
            return copy;
        }

        /// <summary>The muzzle anchor of a prop and the muzzle point in its space.</summary>
        internal static bool TryMuzzle(GameObject prop, out Transform anchor, out Vector3 local)
        {
            anchor = null; local = Vector3.zero;
            if (prop == null) return false;
            foreach (var c in prop.GetComponentsInChildren<Transform>(true))
            {
                if (c.name == "arrow")
                {
                    var mf = c.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var b = mf.sharedMesh.bounds; anchor = c; local = new Vector3(b.center.x, b.center.y, b.max.z); return true;
                    }
                }
                if (anchor == null && c != prop.transform && (c.name.StartsWith("fire_effect", StringComparison.OrdinalIgnoreCase) || c.name.StartsWith("muzzle", StringComparison.OrdinalIgnoreCase))) anchor = c;
            }
            if (anchor != null) return true;
            var dir = Barrel(prop); if (dir == Vector3.zero) return false;
            bool any = false; var bounds = new Bounds();
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = prop.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            }
            if (!any) return false;
            int axis = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) && Mathf.Abs(dir.x) >= Mathf.Abs(dir.z) ? 0 : Mathf.Abs(dir.y) >= Mathf.Abs(dir.z) ? 1 : 2;
            anchor = prop.transform; local = bounds.center; local[axis] = dir[axis] >= 0f ? bounds.max[axis] : bounds.min[axis]; return true;
        }

        /// <summary>The muzzle direction, or the longest mesh axis toward the end farther from the grip.</summary>
        public static Vector3 Barrel(GameObject prop)
        {
            if (prop == null) return Vector3.zero;
            var t = prop.transform;
            foreach (var c in prop.GetComponentsInChildren<Transform>(true))
            {
                string n = c.name.ToLowerInvariant();
                if (c != t && (n.StartsWith("fire_effect") || n.StartsWith("muzzle")))
                {
                    var d = t.InverseTransformPoint(c.position);
                    if (d.sqrMagnitude > 1e-4f) return d.normalized;
                }
            }
            bool any = false; var b = new Bounds();
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = t.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
                }
            }
            if (!any) return Vector3.zero;
            var s = b.size; int axis = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
            var dir = Vector3.zero;
            dir[axis] = Mathf.Abs(b.max[axis]) >= Mathf.Abs(b.min[axis]) ? 1f : -1f;
            return dir;
        }

        /// <summary>(2.2.0, ModAPI) a left-hand local pose reflected across the body's middle plane, expressed under the right hand (as Instantiate does)</summary>
        internal static void MirrorToRight(Vector3 lp, Quaternion lr, out Vector3 pos, out Quaternion rot)
        {
            var L = BindWorld("mixamorig:LeftHand"); var R = BindWorld("mixamorig:RightHand");
            var S = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            var M = R.inverse * S * L * Matrix4x4.TRS(lp, lr, Vector3.one) * S;
            pos = M.GetColumn(3);
            rot = Quaternion.LookRotation(M.GetColumn(2), M.GetColumn(1));
        }

        /// <summary>World bind matrix of a mixamorig bone (identity when unknown).</summary>
        internal static Matrix4x4 BindWorld(string bone)
        {
            float[] v;
            var m = Matrix4x4.identity;
            if (!Bindposes.Human.TryGetValue(bone, out v)) return m;
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++) m[r, c] = v[r * 4 + c];
            return m.inverse;
        }

        /// <summary>Scene load: the catalogs are rebuilt on next use.</summary>
        public static void OnSceneLoaded() { _built = false; _catalog.Clear(); _items.Clear(); }
    }
}
