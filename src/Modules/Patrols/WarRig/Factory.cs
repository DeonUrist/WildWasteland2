using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES3Internal;
using NWH.Common.CoM;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Powertrain;
using NWH.VehiclePhysics2.Powertrain.Wheel;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // War Rig = inactive clone of the vanilla Trailer_Big (attacher, leg, TrailerAttached/TireWear/DistanceKinematic FSMs,
    // NWH VehicleController, ES3Prefab) with the body hidden, the .glb mesh added, four extra wheel hinges, a stable prefab id
    // and deterministic ES3 local reference ids for everything added, so saves map back onto the same objects.
    public static class Factory
    {
        public const long PrefabId = 735710202610090001L;
        public const string Name = "WarRig";
        public const string ModelNode = "warrig_model";
        public static readonly string[] Hinges = { "hinge_wheel_L", "hinge_wheel_R", "hinge_wheel_L2", "hinge_wheel_R2", "hinge_wheel_L3", "hinge_wheel_R3" };
        private static GameObject template, holder;
        private static GlbModel model;
        private static int modelVertexCount, modelSubmeshCount;
        private static List<Mesh> hulls;
        private static Material[] materials;

        // WW: the template if it was built already (no build): the first-line reference filter of the shared CreateObject hook
        internal static GameObject Cached { get { return template; } }

        public static GameObject Source(string name)
        {
            var p = Resources.FindObjectsOfTypeAll<ES3Prefab>().FirstOrDefault(x => x != null && !x.gameObject.scene.IsValid() && x.name == name);
            if (p == null) throw new InvalidOperationException("Prefab '" + name + "' is not loaded. Load a game first.");
            return p.gameObject;
        }

        public static GameObject Template()
        {
            if (template != null) return template;
            var source = Source("Trailer_Big");
            if (holder == null)
            {
                holder = new GameObject("WarRig.InactiveTemplates") { hideFlags = HideFlags.HideAndDontSave };
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
            }
            var copy = Object.Instantiate(source, holder.transform, false);
            try
            {
                copy.SetActive(false);
                copy.name = Name;
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                Convert(copy);
                copy.GetComponent<ES3Prefab>().prefabId = PrefabId;
                template = copy;
                Core.Log.Info("[Patrols] " + "War Rig template created from Trailer_Big: " + model.Mesh.vertexCount + " vertices, " + model.Mesh.subMeshCount + " materials, " + Hulls().Count + " collider hulls, 6 wheel hinges.");
                return template;
            }
            catch { Object.Destroy(copy); throw; }
        }

        // ---------- model ----------
        internal static GlbModel Model()
        {
            // ES3 may update an existing shared Mesh in place. Reference equality alone cannot detect that.
            if (model != null && model.Mesh != null && model.Mesh.name == Name
                && model.Mesh.vertexCount == modelVertexCount && model.Mesh.subMeshCount == modelSubmeshCount
                && model.Colliders.All(x => x != null)) return model;
            var path = Path.Combine(WarRigFeature.Dir, S.ModelFile);
            if (!File.Exists(path)) throw new FileNotFoundException("War Rig model not found: " + path);
            model = Glb.Load(path);
            model.Mesh.name = "WarRig";
            modelVertexCount = model.Mesh.vertexCount; modelSubmeshCount = model.Mesh.subMeshCount;
            Object.DontDestroyOnLoad(model.Mesh);
            return model;
        }

        // Material.mainTexture logs an error for shaders without a _MainTex *texture* (e.g. Hidden/TerrainEngine/...), so ask the shader first.
        private static bool HasMainTex(Material m)
        {
            if (m == null || m.shader == null || m.shader.name.StartsWith("Hidden/")) return false;
            return System.Array.IndexOf(m.GetTexturePropertyNames(), "_MainTex") >= 0;
        }

        // Convex hulls: COL* objects in the model file + every mesh in [Colliders] File.
        internal static List<Mesh> Hulls()
        {
            if (hulls != null && hulls.All(x => x != null)) return hulls;
            hulls = new List<Mesh>(Model().Colliders);
            var f = S.ColliderFile;
            if (!string.IsNullOrEmpty(f))
            {
                var path = Path.Combine(WarRigFeature.Dir, f);
                if (File.Exists(path))
                {
                    var g = Glb.Load(path);
                    hulls.AddRange(g.Colliders);
                    if (g.Mesh != null && g.Mesh.vertexCount > 0)   // objects not named COL...: one hull per submesh
                        for (int i = 0; i < g.Mesh.subMeshCount; i++)
                        {
                            var tri = g.Mesh.GetTriangles(i); var vs = g.Mesh.vertices; var used = new List<Vector3>();
                            foreach (var t in tri) used.Add(vs[t]);
                            var m = new Mesh { name = "hull_" + i }; m.SetVertices(used);
                            var idx = new int[used.Count]; for (int k = 0; k < idx.Length; k++) idx[k] = k;
                            m.SetTriangles(idx, 0); hulls.Add(m);
                        }
                }
                else Core.Log.Warn("[Patrols] " + "Collider file not found: " + path + " (using the boxes)");
            }
            foreach (var h in hulls) Object.DontDestroyOnLoad(h);
            return hulls;
        }

        public static Material[] Materials(Material fallback)
        {
            if (materials != null && materials.All(x => x != null && x.shader != null && x.shader.name != "Hidden/InternalErrorShader")) return materials;
            var m = Model();
            var game = Resources.FindObjectsOfTypeAll<Material>();
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            materials = new Material[m.ImageNames.Count];
            var made = new Dictionary<string, Material>();
            for (int i = 0; i < materials.Length; i++)
            {
                string img = m.ImageNames[i] ?? "";
                Material mat;
                if (made.TryGetValue(img, out mat)) { materials[i] = mat; continue; }
                // 1) a game material that already uses this texture: native shader, normal map, smoothness
                mat = game.Where(g => HasMainTex(g) && g.mainTexture != null && g.mainTexture.name == img)
                          .OrderByDescending(g => g.shader != null && g.shader.name == "Standard")
                          .ThenByDescending(g => g.HasProperty("_BumpMap") && g.GetTexture("_BumpMap") != null).FirstOrDefault();
                if (mat == null)
                {
                    // 2) the game texture on a copy of the trailer's material; 3) the image embedded in the .glb
                    mat = fallback != null && fallback.shader != null && fallback.shader.name != "Hidden/InternalErrorShader"
                        ? new Material(fallback) : new Material(Shader.Find("Standard"));
                    mat.name = "WarRig_" + img;
                    var tex = textures.FirstOrDefault(t => t != null && t.name == img);
                    if (tex == null && m.ImageBytes[i] != null)
                    {
                        tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = img };
                        ImageConversion.LoadImage(tex, m.ImageBytes[i], true);
                        Object.DontDestroyOnLoad(tex);
                    }
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
                    Core.Log.Info("[Patrols] " + "Material " + m.MaterialNames[i] + ": no game material uses '" + img + "', " + (tex != null ? "built one" : "untextured"));
                }
                else mat = new Material(mat) { name = "WarRig_" + img };
                // Own the cached materials. A death/load must not leave us holding a scene-owned material.
                Object.DontDestroyOnLoad(mat);
                made[img] = mat; materials[i] = mat;
            }
            return materials;
        }

        // ---------- conversion of the inactive clone ----------
        private static void Convert(GameObject copy)
        {
            var vc = copy.GetComponent<VehicleController>();
            if (vc == null || vc.IsInitialized) throw new InvalidOperationException("Expected an uninitialised Trailer_Big controller.");
            var es3 = copy.GetComponent<ES3Prefab>();
            var parts = Required(copy, "parts");
            var fallbackMat = parts.GetComponent<MeshRenderer>() != null ? parts.GetComponent<MeshRenderer>().sharedMaterial : null;
            var hiddenRenderers = parts.GetComponentsInChildren<Renderer>(true)
                .Concat(Required(copy, "door_hinge").GetComponentsInChildren<Renderer>(true))
                .Concat(Required(copy, "attacher").GetComponents<Renderer>()).ToArray();

            // Hide the vanilla body; keep the objects (ES3 local refs / FSM targets stay valid).
            foreach (var r in parts.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var col in parts.GetComponentsInChildren<Collider>(true))
                if (col.transform.name != "codex_trigger (1)") col.enabled = false;
            Disable(Required(copy, "door_hinge"));
            var attacher = Required(copy, "attacher");
            var att = attacher.GetComponent<MeshRenderer>(); if (att != null) att.enabled = false;
            var leg = Required(copy, "leg");
            var legR = leg.GetComponent<MeshRenderer>(); if (legR != null) legR.enabled = true;

            // Model + colliders
            var m = Model();
            var node = new GameObject(ModelNode);
            node.transform.SetParent(copy.transform, false);
            node.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
            var mr = node.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Materials(fallbackMat);
            var cols = new GameObject("warrig_colliders");
            cols.layer = 8;
            cols.transform.SetParent(node.transform, false);
            for (int i = 0; i < 4; i++) cols.AddComponent<BoxCollider>();
            Ref(es3, node, "model"); Ref(es3, cols, "model/colliders");
            var hl = Hulls();
            if (hl.Count > 0)
            {
                var hg = new GameObject("warrig_hulls");
                hg.layer = 8;
                hg.transform.SetParent(node.transform, false);
                foreach (var h in hl) { var mc = hg.AddComponent<MeshCollider>(); mc.convex = true; mc.sharedMesh = h; }
                Ref(es3, hg, "model/hulls");
            }

            // second parking leg, mirrored; follows the vanilla leg (TrailerAttached toggles "leg")
            var leg2 = Object.Instantiate(leg.gameObject, copy.transform, false);
            leg2.name = "leg2";
            Ref(es3, leg2, "leg2");

            // Four more hinges: clones of the vanilla left/right hinge (never initialised: the clone is inactive).
            var l = Required(copy, "hinge_wheel_L"); var r0 = Required(copy, "hinge_wheel_R");
            var group0 = vc.powertrain.wheelGroups.Count > 0 ? vc.powertrain.wheelGroups[0] : new WheelGroup { name = "Rear Axle" };
            var wl = vc.powertrain.wheels.FirstOrDefault(w => w.wheelUAPI != null && w.wheelUAPI.transform == l);
            var wr = vc.powertrain.wheels.FirstOrDefault(w => w.wheelUAPI != null && w.wheelUAPI.transform == r0);
            if (wl == null || wr == null) throw new InvalidOperationException("Trailer_Big wheel components not found.");
            vc.powertrain.wheelGroups = new List<WheelGroup> { group0, CopyGroup(group0, "Axle 2"), CopyGroup(group0, "Axle 3") };
            group0.name = "Axle 1";
            wl.wheelGroupSelector = new WheelGroupSelector { index = 0 };
            wr.wheelGroupSelector = new WheelGroupSelector { index = 0 };
            for (int axle = 2; axle <= 3; axle++)
                foreach (var side in new[] { l, r0 })
                {
                    var clone = Object.Instantiate(side.gameObject, copy.transform, false).transform;
                    clone.name = side.name + axle;
                    MirrorRefs(es3, side, clone, axle);
                    var wc = clone.GetComponent<NWH.WheelController3D.WheelController>();
                    var comp = new WheelComponent { name = clone.name, wheelUAPI = wc, wheelGroupSelector = new WheelGroupSelector { index = axle - 1 } };
                    vc.powertrain.wheels.Add(comp);   // no Input: free-rolling, like the vanilla unpowered axle
                }
            foreach (var w in vc.powertrain.wheels)
            {
                var wc = w.wheelUAPI as NWH.WheelController3D.WheelController;
                if (wc != null) wc.loadContribution = 1f / 3f;
            }

            var com = copy.GetComponent<VariableCenterOfMass>();
            if (com != null) { com.useDefaultMass = false; com.useDefaultCenterOfMass = false; com.useDefaultInertia = false; }
            // Rigidbody collision detection / interpolation stay as the vanilla trailer has them (0.2.x forced ContinuousDynamic + Interpolate).

            // furniture inside the tank (copies of vehicle / camp decor, positions in [Props])
            Props.Build(copy, es3);

            copy.AddComponent<WarRigUnit>().SetHiddenRenderers(hiddenRenderers);
            Layout.Apply(copy, false);
        }

        private static WheelGroup CopyGroup(WheelGroup g, string name)
        {
            return new WheelGroup
            {
                name = name, addAckerman = false, steerCoefficient = 0f, antiRollBarForce = g.antiRollBarForce,
                brakeCoefficient = g.brakeCoefficient, handbrakeCoefficient = g.handbrakeCoefficient, isSolid = g.isSolid, trackWidth = g.trackWidth
            };
        }

        // ---------- ES3 local references ----------
        private static void Ref(ES3Prefab es3, GameObject go, string path)
        {
            Put(es3, go, Id(path + "#go"));
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++) Put(es3, comps[i], Id(path + "#" + comps[i].GetType().Name + "#" + i));
        }

        internal static void RefTree(ES3Prefab es3, Transform t, string path)
        {
            Ref(es3, t.gameObject, path);
            var seen = new Dictionary<string, int>();
            foreach (Transform c in t)
            {
                int n; seen.TryGetValue(c.name, out n); seen[c.name] = n + 1;
                RefTree(es3, c, path + "/" + c.name + (n > 0 ? "#" + n : ""));
            }
        }

        private static void MirrorRefs(ES3Prefab es3, Transform orig, Transform clone, int axle)
        {
            var a = orig.GetComponentsInChildren<Transform>(true); var b = clone.GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < a.Length && t < b.Length; t++)
            {
                MirrorOne(es3, a[t].gameObject, b[t].gameObject, axle);
                var ca = a[t].GetComponents<Component>(); var cb = b[t].GetComponents<Component>();
                for (int i = 0; i < ca.Length && i < cb.Length; i++) MirrorOne(es3, ca[i], cb[i], axle);
            }
        }

        private static void MirrorOne(ES3Prefab es3, Object a, Object b, int axle)
        {
            long id;
            if (a != null && b != null && es3.localRefs.TryGetValue(a, out id)) Put(es3, b, Mix(id, axle));
            else if (b != null) Put(es3, b, Id(clonePath(b) + "#" + axle));
        }

        private static string clonePath(Object o)
        {
            var c = o as Component; var t = c != null ? c.transform : ((GameObject)o).transform; var s = o.GetType().Name;
            for (; t != null; t = t.parent) s = t.name + "/" + s;
            return s;
        }

        private static void Put(ES3Prefab es3, Object o, long id) { if (o != null) es3.localRefs[o] = id; }

        private static long Id(string s)
        {
            ulong h = 1469598103934665603UL;
            foreach (char ch in "WarRig/" + s) { h ^= ch; h *= 1099511628211UL; }
            return (long)(h & 0x3FFFFFFFFFFFFFFFUL) | 0x1000000000000000L;
        }

        private static long Mix(long id, int k)
        {
            ulong h = (ulong)id ^ (0x9E3779B97F4A7C15UL * (ulong)(k + 7));
            h ^= h >> 31; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 29;
            return (long)(h & 0x3FFFFFFFFFFFFFFFUL) | 0x1000000000000000L;
        }

        internal static Transform Required(GameObject root, string path)
        {
            var t = root.transform.Find(path);
            if (t == null) throw new InvalidOperationException("Trailer_Big layout changed: missing " + path);
            return t;
        }

        private static void Disable(Transform node)
        {
            foreach (var r in node.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in node.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in node.GetComponentsInChildren<Behaviour>(true)) b.enabled = false;
            node.gameObject.SetActive(false);
        }

        // ---------- spawning ----------
        public static GameObject Create(Vector3 position, Quaternion rotation, bool equip, Func<int> nextId)
        {
            var prefab = Template();
            GameObject wheel = null;
            if (equip) wheel = Source(S.WheelItem);
            var rig = Object.Instantiate(prefab, holder.transform, false);
            try
            {
                rig.name = Name + "(Clone)" + (nextId != null ? nextId().ToString() : "");
                Props.Apply(rig); // Prune off/parked furniture before the rig enters the world.
                if (wheel != null)
                    foreach (var h in Hinges)
                    {
                        var part = Object.Instantiate(wheel, Required(rig, h), false);
                        part.name = wheel.name + "(Clone)" + nextId();
                        part.tag = "vehPart"; part.layer = 8;
                        part.transform.localPosition = Vector3.zero;
                        part.transform.localRotation = Quaternion.identity;
                        var body = part.GetComponent<Rigidbody>();
                        if (body != null) Object.DestroyImmediate(body);
                        part.SetActive(true);
                    }
                rig.transform.SetParent(null, false);
                rig.transform.SetPositionAndRotation(position, rotation);
                rig.SetActive(true);
                return rig;
            }
            catch { Object.Destroy(rig); throw; }
        }
    }
}
