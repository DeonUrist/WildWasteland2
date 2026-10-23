using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    internal sealed partial class PoiDressing
    {
        private readonly List<GameObject> _workspaces = new List<GameObject>();
        private readonly Dictionary<string, PoiDressLayout> _worldLayouts = new Dictionary<string, PoiDressLayout>(StringComparer.Ordinal);
        internal void Types(List<string> into) { _owner.AuthoringTypes(into); }
        internal GameObject NativeTemplate(string type)
        {
            var prefab = PrefabCatalog.Exact(type);
            if (prefab == null) TownPrefabCatalog.Prefabs.TryGetValue(type, out prefab);
            if (prefab == null && type == "TownsGasStation") prefab = TownGasStation.Current?.CatalogPrefab;
            return prefab ?? _owner.NativeTemplate(type);
        }
        internal PoiDressLayout StoredLayout(string type)
        { PoiDressLayout layout; if (_worldLayouts.TryGetValue(type, out layout)) return layout.Copy(); return Authored.Layout(type) ?? PoiDressStore.Load(type); }
        internal void ResetWorldLayouts() { _worldLayouts.Clear(); }
        internal void RememberLayout(PoiDressLayout layout)
        {
            var normalized = PoiDressStore.Parse(PoiDressStore.Serialize(layout), layout.Type);
            InstallLayoutMap(normalized); _worldLayouts[layout.Type] = normalized;
        }
        private static void InstallLayoutMap(PoiDressLayout layout)
        {
            object value; string map = layout.Metadata.TryGetValue("navigation", out value) ? value as string : null;
            if (map == null) return;
            string name = layout.Metadata.TryGetValue("navigationName", out value) ? value as string : null;
            if (name == null || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || Path.GetFileName(name) != name || !name.StartsWith(layout.Type + "-", StringComparison.Ordinal) || !name.EndsWith(".wwnav", StringComparison.Ordinal)) throw new FormatException("POI layout navigation name");
            byte[] bytes = Convert.FromBase64String(map); if (bytes.Length < 40 || bytes.Length > 8 * 1024 * 1024) throw new FormatException("POI layout navigation size");
            using (var reader = new BinaryReader(new MemoryStream(bytes, false))) if (reader.ReadInt32() != 0x574E4156 || reader.ReadInt32() != 1) throw new FormatException("POI layout navigation format");
            string directory = Path.Combine(Assets.ModuleConfigDir("NPCAI"), "NavMaps", "Custom"); Directory.CreateDirectory(directory); string path = Path.Combine(directory, name);
            if (File.Exists(path) && Convert.ToBase64String(File.ReadAllBytes(path)) == map) return;
            File.WriteAllBytes(path + ".tmp", bytes); AtomicFile.Replace(path + ".tmp", path, path + ".bak");
        }
        internal List<object> SnapshotLayouts()
        {
            var layouts = new Dictionary<string, PoiDressLayout>(_worldLayouts, StringComparer.Ordinal);
            if (Directory.Exists(PoiDressStore.DirectoryPath)) foreach (string file in Directory.GetFiles(PoiDressStore.DirectoryPath, "*.json"))
            {
                var data = Json.ParseObject(File.ReadAllText(file)); object type;
                if (!data.TryGetValue("poi", out type) || !(type is string)) throw new FormatException("POI layout identity");
                if (!layouts.ContainsKey((string)type)) layouts[(string)type] = PoiDressStore.Parse(Json.Write(data), (string)type);
            }
            var result = new List<object>(); foreach (var layout in layouts.Values) result.Add(Json.ParseObject(PoiDressStore.Serialize(layout))); return result;
        }
        internal static BuildingSpec PlanningSpec(GameObject prefab)
        {
            if (_current == null) return null;
            var layout = _current.StoredLayout(prefab.name); object value;
            if (!layout.Metadata.TryGetValue("townBounds", out value)) return null;
            var bounds = value as System.Collections.IList; if (bounds == null || bounds.Count != 6) throw new FormatException("POI town footprint");
            var n = new float[6]; for (int i = 0; i < n.Length; i++) { double number = Convert.ToDouble(bounds[i], System.Globalization.CultureInfo.InvariantCulture); if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > 1000000) throw new FormatException("POI footprint range"); n[i] = (float)number; }
            if (n[3] <= 0 || n[4] <= 0 || n[3] > 1000 || n[4] > 1000 || n[5] < 1 || n[5] != Mathf.Floor(n[5])) throw new FormatException("POI footprint dimensions");
            InstallLayoutMap(layout);
            return new BuildingSpec { Name = prefab.name, Center = new Vector3(n[0], n[1], n[2]), HalfX = n[3], HalfZ = n[4], ColliderCount = (int)n[5] };
        }
        internal static string PlanningMap(string type)
        {
            if (_current == null) return null;
            var layout = _current.StoredLayout(type); object value;
            string name = layout.Metadata.TryGetValue("navigationName", out value) ? value as string : null;
            if (name == null) { var custom = _current.Authored.Get(type); name = custom?.NavigationName; }
            if (name == null || Path.GetFileName(name) != name || !name.StartsWith(type + "-", StringComparison.Ordinal)) return null;
            return Path.Combine(Assets.ModuleConfigDir("NPCAI"), "NavMaps", "Custom", name);
        }
        internal void PrepareEdit(Site site)
        {
            site.EditLayout = site.Layout == null ? new PoiDressLayout { Type = site.Type } : site.Layout.Copy();
            if (Authored.Contains(site.Type) || site.EditLayout.Native.Count > 0) return;
            var prefab = NativeTemplate(site.Type);
            if (prefab == null || prefab.scene.IsValid()) return;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null || filter.GetComponentInParent<Rigidbody>() != null || filter.name == "mapIcon" || filter.gameObject.layer == 22) continue;
                bool active = true; for (var t = filter.transform; t != prefab.transform; t = t.parent) if (!t.gameObject.activeSelf || t.GetComponent<Animation>() != null || t.GetComponent<Animator>() != null) { active = false; break; }
                if (!active) continue;
                var prop = Catalog.NativeProp(filter, prefab.transform); if (prop == null) continue;
                if (site.EditLayout.Props.Count >= PoiDressLayout.Limit) throw new InvalidOperationException("This POI exceeds the 8192-prop editor limit.");
                site.EditLayout.Props.Add(prop); site.EditLayout.Native.Add(IndexPath(prefab.transform, filter.transform));
            }
        }
        private static string IndexPath(Transform root, Transform node)
        { var parts = new List<string>(); for (var t = node; t != root; t = t.parent) parts.Insert(0, t.GetSiblingIndex().ToString(System.Globalization.CultureInfo.InvariantCulture)); return string.Join("/", parts.ToArray()); }
        private static Transform NativeNode(Transform root, string path)
        {
            if (path.Length == 0) return root;
            foreach (string part in path.Split('/')) { int i; if (!int.TryParse(part, out i) || i < 0 || i >= root.childCount) return null; root = root.GetChild(i); }
            return root;
        }
        private static void SuppressNative(Site site, List<string> paths)
        {
            if (site.Root == null) return;
            foreach (string path in paths)
            {
                var node = NativeNode(site.Root, path); if (node == null) continue;
                var renderer = node.GetComponent<MeshRenderer>(); if (renderer != null) { if (!site.NativeRenderers.ContainsKey(renderer)) site.NativeRenderers[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
                foreach (var collider in node.GetComponents<Collider>()) if (!collider.isTrigger && collider.attachedRigidbody == null) { if (!site.NativeColliders.ContainsKey(collider)) site.NativeColliders[collider] = collider.enabled; collider.enabled = false; }
            }
        }
        private static void RestoreNative(Site site)
        { foreach (var row in site.NativeRenderers) if (row.Key != null) row.Key.forceRenderingOff = row.Value; foreach (var row in site.NativeColliders) if (row.Key != null) row.Key.enabled = row.Value; site.NativeRenderers.Clear(); site.NativeColliders.Clear(); }
        internal void EditVisibility(Site site, bool visible)
        {
            RestoreNative(site);
            SuppressNative(site, !visible && site.EditLayout != null ? site.EditLayout.Native : site.Layout.Native);
            if (visible) site.EditLayout = null;
        }
        internal void HideTemplateScenery(Transform root, PoiDressLayout layout) { SuppressNative(new Site { Root = root }, layout.Native); }
        internal Site OpenNative(string type)
        {
            if (!GameRefs.InPlay || GameRefs.MainCamera == null) throw new InvalidOperationException("Load a world first.");
            foreach (var site in Sites) if (site.Root != null && site.Type == type && site.Root.gameObject.activeInHierarchy) return site;
            _structures.Clear(); ModApi.Get<INavApi>()?.Structures(_structures);
            foreach (var row in _structures) if (row.Root != null && TypeName(row.Root) == type) return Attach(row.Root);
            var prefab = NativeTemplate(type);
            if (prefab == null) throw new InvalidOperationException("POI prefab is not loaded: " + type);
            var holder = new GameObject("POI native authoring workspace"); holder.SetActive(false);
            GameObject copy = null;
            try
            {
                copy = Object.Instantiate(prefab, holder.transform, false);
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) Object.DestroyImmediate(behaviour);
                foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                copy.name = type + " (Authoring workspace)"; copy.tag = "SpawnerStatic";
                var camera = GameRefs.MainCamera.transform; Vector3 forward = camera.forward; forward.y = 0; forward.Normalize();
                var spec = TownAssets.Measure(copy); float radius = spec == null ? 35 : Mathf.Sqrt(spec.HalfX * spec.HalfX + spec.HalfZ * spec.HalfZ) + 8;
                Vector3 point = camera.position + forward * (radius + 20); Terrain terrain = null;
                foreach (var t in Terrain.activeTerrains) if (t != null && t.terrainData != null)
                { var p = point - t.transform.position; var size = t.terrainData.size; if (p.x > radius + 8 && p.z > radius + 8 && p.x < size.x - radius - 8 && p.z < size.z - radius - 8) { terrain = t; break; } }
                if (terrain == null) throw new InvalidOperationException("Move into open desert to load an editable copy of this POI.");
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                holder.transform.position = point;
                var padSite = new TownSite { Anchor = holder.transform, Plan = new TownPlan { Key = "POI editor/" + type, Radius = radius, Feather = 8 } };
                TownGroundPad.Prepare(padSite, terrain);
                copy.transform.SetParent(terrain.transform, false); copy.transform.position = point; copy.SetActive(true); _workspaces.Add(copy);
                return Attach(copy.transform);
            }
            catch { if (copy != null) { copy.SetActive(false); Object.Destroy(copy); } throw; }
            finally { Object.Destroy(holder); }
        }
        private void ClearWorkspaces() { foreach (var root in _workspaces) if (root != null) { ModApi.Get<INavApi>()?.ForgetStructure(root.transform); root.SetActive(false); Object.Destroy(root); } _workspaces.Clear(); }
    }
}
