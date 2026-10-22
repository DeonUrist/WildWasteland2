using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Nodes.ObjectsGenerators;
using Den.Tools.Splines;
using Den.Tools.Matrices;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal sealed class BuildingAsset
    {
        internal GameObject Prefab;
        internal BuildingSpec Spec;
        internal string[] Spawners;
        internal string[] Markers;
        internal TownPopulation.NativeBudget[] Budgets;
        internal string[] Strip = new string[0];   // (WW) a camp's perimeter fences/walls, switched off on town clones
    }
    internal sealed class TownAssets
    {
        internal readonly Dictionary<string, BuildingAsset> Buildings = new Dictionary<string, BuildingAsset>(StringComparer.Ordinal);
        internal BuildingSpec[] Specs;
        internal Material StreetMaterial;
        internal Material PavementMaterial;
        internal Vector2 TextureSize;
        internal string RoadName;
        internal readonly Dictionary<TownFaction, List<GameObject>> Population = new Dictionary<TownFaction, List<GameObject>>();
        internal readonly List<GameObject> Hounds = new List<GameObject>();
        internal IOutlet<SplineSys> Roads;
        internal TerrainLayer RoadLayer;
        internal readonly List<GameObject> NativePoiPrefabs = new List<GameObject>();
        internal readonly TownWallKit WallKit = new TownWallKit();
        internal sealed class CarShell { internal Mesh Mesh; internal Material[] Materials; }
        internal readonly List<CarShell> CarShells = new List<CarShell>();
        internal readonly List<Bounds> ShellBounds = new List<Bounds>(); // CarShells order; numeric copy for worker-side dressing/overlay plans
        internal float NavMaxStep = .45f;
        private readonly Dictionary<GameObject, bool> _bosses = new Dictionary<GameObject, bool>();
        // Root-local AABB of every enabled non-trigger collider under root (inactive prefabs included), composed like Measure.
        internal static bool ColliderBox(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(); bool any = false;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (!c.enabled || c.isTrigger || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) continue;
                bool active = true;
                for (var t = c.transform; t != root; t = t.parent) if (!t.gameObject.activeSelf) { active = false; break; }
                if (!active) continue;
                Bounds shape; var mesh = c as MeshCollider; var box = c as BoxCollider;
                var sphere = c as SphereCollider; var capsule = c as CapsuleCollider;
                if (mesh != null && mesh.sharedMesh != null) shape = mesh.sharedMesh.bounds;
                else if (box != null) shape = new Bounds(box.center, box.size);
                else if (sphere != null) shape = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
                else if (capsule != null)
                { var size = Vector3.one * capsule.radius * 2; size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2); shape = new Bounds(capsule.center, size); }
                else continue;
                var matrix = Matrix4x4.identity;
                for (var t = c.transform; t != root; t = t.parent) matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
                matrix = Matrix4x4.Scale(root.localScale) * matrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(shape.center + Vector3.Scale(shape.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            }
            return any;
        }
        internal bool IsBoss(GameObject prefab)
        {
            bool boss; if (_bosses.TryGetValue(prefab, out boss)) return boss;
            boss = prefab.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var fsm in prefab.GetComponentsInChildren<PlayMakerFSM>(true)) if (fsm.FsmName == "BossUI") { boss = true; break; }
            _bosses[prefab] = boss; return boss;
        }
        internal void Load(Graph graph, TownSettings settings)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            float margin = Mathf.Max(1, TownBakeBridge.Tuning("NavMargin", 4)), cell = Mathf.Max(.25f, TownBakeBridge.Tuning("NavCellSize", .5f));
            NavMaxStep = Mathf.Max(.1f, TownBakeBridge.Tuning("NavMaxStep", .45f));
            foreach (string part in settings.Prefabs.Value.Split(',')) if (part.Trim().Length > 0) allowed.Add(part.Trim());
            foreach (var generator in graph.generators)
            {
                var output = generator as ObjectsOutput;
                if (output == null) continue;
                foreach (var prefab in output.prefabs)
                {
                    if (prefab != null && (prefab.name.StartsWith("Building_", StringComparison.Ordinal) || prefab.name.StartsWith("Camp_", StringComparison.Ordinal)
                        || prefab.name.StartsWith("Cave_", StringComparison.Ordinal) || prefab.name.StartsWith("Wreck_", StringComparison.Ordinal))) NativePoiPrefabs.Add(prefab);
                    if (prefab != null) CatalogPopulation(prefab);
                    bool wreck = prefab != null && prefab.name.StartsWith("Wreck_", StringComparison.Ordinal);
                    if (wreck) foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh = filter.sharedMesh; if (mesh == null || !(mesh.name == "car wreck 4" || mesh.name == "car wreck 5" || mesh.name == "car_wreck_6.001")) continue;
                        var renderer = filter.GetComponent<Renderer>(); if (renderer == null || CarShells.Exists(c => c.Mesh.name == mesh.name)) continue;
                        CarShells.Add(new CarShell { Mesh = mesh, Materials = renderer.sharedMaterials });
                    }
                    // Favor the ruined building and container compound over roadside vehicle piles.
                    // (WW) every native camp may stand in a town too (its perimeter walls stripped); camps with a merchant are merchant lots
                    bool camp = prefab != null && prefab.name.StartsWith("Camp_", StringComparison.Ordinal);
                    bool merchant = prefab != null && (prefab.name == "Camp_10" || camp && HasMerchant(prefab));
                    if (prefab == null || (!merchant && !camp && !prefab.name.StartsWith("Building_", StringComparison.Ordinal) && !(wreck && (prefab.name == "Wreck_8" || prefab.name == "Wreck_7"))) || Buildings.ContainsKey(prefab.name)) continue;
                    if (!merchant && !wreck && !camp && allowed.Count > 0 && !allowed.Contains(prefab.name)) continue;
                    if (camp && BossCamp(prefab)) { Log.Always("[Towns] camp " + prefab.name + " left out of towns: boss camp"); continue; }
                    var spec = Measure(prefab); if (spec == null) continue;
                    spec.Wreck = wreck; spec.Crane = prefab.name == "Building_4"; spec.Merchant = merchant; spec.Camp = camp;
                    if (merchant) TownLot.Merchants.Add(prefab.name);
                    foreach (var child in prefab.GetComponentsInChildren<Transform>(true)) if (child.name.IndexOf("crane_big", StringComparison.OrdinalIgnoreCase) >= 0) spec.Crane = true;
                    spec.Bake = TownBakeData.Load(spec, margin, cell);
                    if (spec.Bake == null || !spec.Bake.FindEntry(spec))
                    { Log.Warn("[Towns] template excluded: " + prefab.name + "; " + (spec.Bake == null ? "no matching bake (" + TownBakeData.LastMiss + ")" : "no measured outward entrance in " + spec.Bake.Source) + ". Bake this prefab with current NPC settings first."); continue; }
                    var paths = new List<string>();
                    var budgets = new List<TownPopulation.NativeBudget>();
                    var markers = new List<string>();
                    // One catalog scan at graph load; instantiated buildings resolve these cached paths.
                    foreach (var fsm in prefab.GetComponentsInChildren<PlayMakerFSM>(true))
                        if (fsm.FsmName == "ItemSpawner")
                        {
                            paths.Add(Path(prefab.transform, fsm.transform));
                            try { budgets.Add(TownPopulation.Capture(fsm)); }
                            catch (Exception e) { budgets.Add(new TownPopulation.NativeBudget { Kind = 6 }); Log.Warn("[Towns] unsupported spawn group disabled on town clones: " + prefab.name + "/" + fsm.gameObject.name + "; " + e.Message); }
                        }
                    foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) if (t.name == "mapIcon") markers.Add(Path(prefab.transform, t));
                    spec.Frontages = new[] { TownFrontage.Make(spec, 0), TownFrontage.Make(spec, 1) };
                    spec.TiltedFrontages = new[] { TownFrontage.Make(spec, 0, -9), TownFrontage.Make(spec, 0, 9), TownFrontage.Make(spec, 1, -9), TownFrontage.Make(spec, 1, 9) };
                    if (camp) foreach (var b in budgets) if (b.Kind == 6) { Log.Always("[Towns] camp " + prefab.name + " left out of towns: boss spawner"); goto nextPrefab; }
                    var strip = camp ? Walls(prefab, spec) : new string[0];
                    if (strip.Length > 0) Log.Always("[Towns] camp " + prefab.name + " in towns without its walls: " + string.Join(", ", strip));
                    Buildings.Add(prefab.name, new BuildingAsset { Prefab = prefab, Spec = spec, Spawners = paths.ToArray(), Markers = markers.ToArray(), Budgets = budgets.ToArray(), Strip = strip });
                    Log.Always("[Towns] entrance " + spec.Name + ": local=" + spec.Entry + ", outward=" + spec.Outward + "; " + spec.Bake.Source);
                nextPrefab:;
                }
            }
            WallKit.Load(NativePoiPrefabs);
            bool anyMerchant = false; foreach (var b in Buildings.Values) if (b.Spec.Merchant) anyMerchant = true;
            if (!anyMerchant) Log.Warn("[Towns] no merchant camp in the town catalog (Camp_10 needs a bake): Coyotes towns keep only their merchant bus.");
            if (Buildings.Count == 0) throw new InvalidOperationException("No configured Building_N prefabs with solid geometry in the current graph.");
            if (allowed.Count > 0) foreach (string name in allowed)
                if (!Buildings.ContainsKey(name)) throw new InvalidOperationException("Configured building missing from current graph: " + name);
            var names = new List<string>(Buildings.Keys); names.Sort(StringComparer.Ordinal);
            Specs = new BuildingSpec[names.Count]; for (int i = 0; i < names.Count; i++) Specs[i] = Buildings[names[i]].Spec;
            Material concrete = null;
            foreach (var asset in Buildings.Values) foreach (var renderer in asset.Prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && (material.name == "concrete_3" || concrete == null && material.name.IndexOf("concrete", StringComparison.OrdinalIgnoreCase) >= 0)
                        && Array.IndexOf(material.GetTexturePropertyNames(), "_MainTex") >= 0 && material.GetTexture("_MainTex") != null) concrete = material;
            if (concrete != null) { PavementMaterial = new Material(concrete) { name = "Towns native concrete parking" }; PavementMaterial.mainTextureScale = Vector2.one; }
            CarShells.Sort((a, b) => string.CompareOrdinal(a.Mesh.name, b.Mesh.name));
            ShellBounds.Clear(); foreach (var shell in CarShells) ShellBounds.Add(shell.Mesh.bounds);
            foreach (var pool in Population) Log.Always("[Towns] population catalog " + pool.Key + ": " + pool.Value.Count + " native prefabs");
            TerrainLayer road = null;
            foreach (var generator in graph.generators)
            {
                var output = generator as TexturesOutput200; if (output == null) continue;
                foreach (var layer in output.layers)
                {
                    if (layer.prototype == null) continue;
                    bool match;
                    if (settings.RoadLayer.Value.Length > 0) match = layer.prototype.name == settings.RoadLayer.Value;
                    else
                    {
                        IOutlet<object> source;
                        match = graph.links.TryGetValue(layer, out source) && RoadInput(source.Gen);
                    }
                    if (!match) continue;
                    if (road != null && road != layer.prototype) throw new InvalidOperationException("Multiple road textures; set Towns / Assets / Road terrain layer to an exact TerrainLayer name.");
                    road = layer.prototype;
                    RoadLayer = layer.prototype;
                    if (Roads == null)
                    {
                        IOutlet<object> source;
                        if (graph.links.TryGetValue(layer, out source)) Roads = FindRoads(graph, source.Gen);
                    }
                }
            }
            if (road == null || road.diffuseTexture == null) return;
            var shader = Shader.Find("Standard"); if (shader == null) throw new InvalidOperationException("Native Standard shader missing");
            StreetMaterial = new Material(shader) { name = "Towns streets (borrowed " + road.name + ")", mainTexture = road.diffuseTexture };
            StreetMaterial.SetFloat("_Glossiness", road.smoothness); StreetMaterial.SetFloat("_Metallic", road.metallic);
            if (road.normalMapTexture != null) { StreetMaterial.SetTexture("_BumpMap", road.normalMapTexture); StreetMaterial.EnableKeyword("_NORMALMAP"); }
            TextureSize = road.tileSize; RoadName = road.name;
        }
        private void CatalogPopulation(GameObject root)
        {
            foreach (var proxy in root.GetComponentsInChildren<PlayMakerArrayListProxy>(true))
                if (proxy.preFillGameObjectList != null) foreach (var prefab in proxy.preFillGameObjectList)
                {
                    if (prefab == null || IsBoss(prefab)) continue;
                    TownFaction faction;
                    if (prefab.CompareTag("Mutant")) faction = TownFaction.Mutants;
                    else if (prefab.CompareTag("Carnivore") || prefab.CompareTag("Herbivore")) faction = TownFaction.Animals;
                    else if (prefab.CompareTag("Scrapyard")) faction = TownFaction.Scrapyard;
                    else if (prefab.CompareTag("Coyotes")) faction = TownFaction.Coyotes;
                    else continue;
                    NativePoiPopulation.Catalog(prefab, faction);
                    if (faction == TownFaction.Scrapyard || faction == TownFaction.Coyotes)
                    {
                        // Keep accompanying Yard Hounds separate so a base cannot roll only dogs.
                        var animator = prefab.GetComponentInChildren<Animator>(true);
                        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                        {
                            if (faction == TownFaction.Scrapyard && prefab.name == "Yard Hound" && !Hounds.Contains(prefab)) Hounds.Add(prefab);
                            continue;
                        }
                    }
                    if (prefab.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0 || prefab.name.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0
                        || prefab.name.IndexOf("Mechanic", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    List<GameObject> pool; if (!Population.TryGetValue(faction, out pool)) Population.Add(faction, pool = new List<GameObject>());
                    if (!pool.Contains(prefab)) pool.Add(prefab);
                }
        }
        // (WW) a camp with a merchant (a merchant NPC among its spawn lists or a node named after one)
        private static bool HasMerchant(GameObject camp)
        {
            foreach (var proxy in camp.GetComponentsInChildren<PlayMakerArrayListProxy>(true))
                if (proxy.preFillGameObjectList != null) foreach (var p in proxy.preFillGameObjectList)
                    if (p != null && p.name.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (var t in camp.GetComponentsInChildren<Transform>(true)) if (t.name.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        private bool BossCamp(GameObject camp)
        {
            if (IsBoss(camp)) return true;
            foreach (var fsm in camp.GetComponentsInChildren<PlayMakerFSM>(true)) if (fsm.FsmName == "BossDoor" || fsm.FsmName == "BossUI") return true;
            foreach (var proxy in camp.GetComponentsInChildren<PlayMakerArrayListProxy>(true))
                if (proxy.preFillGameObjectList != null) foreach (var p in proxy.preFillGameObjectList) if (p != null && IsBoss(p)) return true;
            return false;
        }
        // The perimeter of a camp: long thin fence / wall / gate / barricade pieces standing on the ground in the outer band of its
        // footprint (railings on platforms and anything in the middle stay). Topmost matching nodes only, as paths from the root.
        private static string[] Walls(GameObject camp, BuildingSpec spec)
        {
            var root = camp.transform; var found = new List<Transform>();
            float ground = float.PositiveInfinity; var boxes = new Dictionary<Transform, Bounds>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                Bounds b; if (!Box(root, t, out b)) continue;
                boxes[t] = b; ground = Mathf.Min(ground, b.min.y);
            }
            foreach (var kv in boxes)
            {
                var t = kv.Key; string n = t.name.ToLowerInvariant();
                if (!(n.Contains("fence") || n.Contains("wall") || n.Contains("gate") || n.Contains("barricade") || n.Contains("palisade") || n.Contains("barrier"))) continue;
                if (n.Contains("small") || n.Contains("rail") || n.Contains("door") || n.Contains("ladder")) continue;
                bool under = false; for (var p = t.parent; p != null && p != root; p = p.parent) if (found.Contains(p)) { under = true; break; }
                if (under) continue;
                var b = kv.Value;
                if (b.min.y > ground + 1.2f) continue;                                   // up on a platform or roof
                float wide = Mathf.Max(b.size.x, b.size.z), thin = Mathf.Min(b.size.x, b.size.z);
                if (wide < 2.5f || thin > 2f) continue;                                    // a long thin piece, not a hut
                float dx = Mathf.Abs(b.center.x - spec.Center.x) / Mathf.Max(1f, spec.HalfX), dz = Mathf.Abs(b.center.z - spec.Center.z) / Mathf.Max(1f, spec.HalfZ);
                if (Mathf.Max(dx, dz) < .6f) continue;                                    // inside the camp, not its edge
                found.RemoveAll(f => f.IsChildOf(t));
                found.Add(t);
            }
            var paths = new string[found.Count];
            for (int i = 0; i < paths.Length; i++) paths[i] = Path(root, found[i]);
            return paths;
        }
        // root-local bounds of a node's own renderer mesh and solid colliders
        private static bool Box(Transform root, Transform t, out Bounds bounds)
        {
            bounds = new Bounds(); bool any = false;
            var shapes = new List<Bounds>();
            var mf = t.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) shapes.Add(mf.sharedMesh.bounds);
            foreach (var c in t.GetComponents<Collider>())
            {
                if (c.isTrigger) continue;
                var mesh = c as MeshCollider; var box = c as BoxCollider;
                if (mesh != null && mesh.sharedMesh != null) shapes.Add(mesh.sharedMesh.bounds); else if (box != null) shapes.Add(new Bounds(box.center, box.size));
            }
            if (shapes.Count == 0) return false;
            var matrix = Matrix4x4.identity;
            for (var p = t; p != root; p = p.parent) matrix = Matrix4x4.TRS(p.localPosition, p.localRotation, p.localScale) * matrix;
            matrix = Matrix4x4.Scale(root.localScale) * matrix;
            foreach (var shape in shapes)
                for (int i = 0; i < 8; i++)
                {
                    var q = matrix.MultiplyPoint3x4(shape.center + Vector3.Scale(shape.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(q, Vector3.zero); any = true; } else bounds.Encapsulate(q);
                }
            return any;
        }
        private static bool RoadInput(Generator generator)
        {
            var exit = generator as MatrixPortalExit;
            if (exit != null && exit.enter != null) return string.Equals(exit.enter.name, "roadStroke", StringComparison.OrdinalIgnoreCase);
            var enter = generator as MatrixPortalEnter;
            return enter != null && string.Equals(enter.name, "roadStroke", StringComparison.OrdinalIgnoreCase);
        }
        private static IOutlet<SplineSys> FindRoads(Graph graph, Generator start)
        {
            var queue = new Queue<Generator>(); var seen = new HashSet<Generator>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var gen = queue.Dequeue(); if (gen == null || !seen.Add(gen)) continue;
                if (gen is MapMagic.Nodes.SplinesGenerators.Stroke200)
                {
                    IOutlet<object> source;
                    if (graph.links.TryGetValue((IInlet<object>)gen, out source)) return source as IOutlet<SplineSys>;
                }
                var portal = gen as MatrixPortalExit; if (portal != null) queue.Enqueue(portal.enter);
                foreach (var link in graph.links) if (link.Key.Gen == gen) queue.Enqueue(link.Value.Gen);
            }
            return null;
        }
        private static BuildingSpec Measure(GameObject prefab)
        {
            Bounds bounds = new Bounds(); bool any = false; int count = 0;
            foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!c.enabled || c.isTrigger || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) continue;
                if (((1 << c.gameObject.layer) & TownNavigation.Mask) == 0) continue;
                bool active = true;
                for (var t = c.transform; t != prefab.transform; t = t.parent) if (!t.gameObject.activeSelf) { active = false; break; }
                if (!active) continue;
                Bounds shape; var mesh = c as MeshCollider; var box = c as BoxCollider;
                var sphere = c as SphereCollider; var capsule = c as CapsuleCollider;
                if (mesh != null && mesh.sharedMesh != null) shape = mesh.sharedMesh.bounds;
                else if (box != null) shape = new Bounds(box.center, box.size);
                else if (sphere != null) shape = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
                else if (capsule != null)
                { var size = Vector3.one * capsule.radius * 2; size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2); shape = new Bounds(capsule.center, size); }
                else continue;
                count++;
                var matrix = Matrix4x4.identity;
                for (var t = c.transform; t != prefab.transform; t = t.parent)
                    matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
                var scale = prefab.transform.localScale;
                for (int axis = 0; axis < 3; axis++) scale[axis] = Mathf.Round(scale[axis] * 10000f) / 10000f;
                matrix = Matrix4x4.Scale(scale) * matrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(shape.center + Vector3.Scale(shape.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            }
            var min = bounds.min; var max = bounds.max;
            for (int axis = 0; axis < 3; axis++) { min[axis] = Mathf.Round(min[axis] * 1000) / 1000; max[axis] = Mathf.Round(max[axis] * 1000) / 1000; }
            bounds.SetMinMax(min, max);
            return !any ? null : new BuildingSpec { Name = prefab.name, Center = bounds.center, HalfX = bounds.extents.x, HalfZ = bounds.extents.z, ColliderCount = count };
        }
        internal static string Path(Transform root, Transform node)
        {
            if (node == root) return "";
            string path = node.name;
            for (var t = node.parent; t != root; t = t.parent) path = t.name + "/" + path;
            return path;
        }
        internal void Dispose()
        { WallKit.Dispose(); if (StreetMaterial != null) UnityEngine.Object.Destroy(StreetMaterial); if (PavementMaterial != null) UnityEngine.Object.Destroy(PavementMaterial); Buildings.Clear(); }
    }
}
