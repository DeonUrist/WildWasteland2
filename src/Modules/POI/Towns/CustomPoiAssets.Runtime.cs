using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    internal sealed partial class CustomPoiAssets
    {
        private sealed class Placement
        {
            internal string Key, Type;
            internal Vector3 World;
            internal GameObject Root;
            internal TownGroundPad Pad;
            internal bool Saved;
            internal float Yaw;
        }
        private sealed class Publication
        {
            internal Definition Definition;
            internal GameObject Geometry;
            internal int Next;
            internal IIsolatedNavWork Bake;
            internal bool Native;
        }
        private readonly List<Placement> _placements = new List<Placement>();
        private readonly List<NavStructure> _nearby = new List<NavStructure>();
        private Publication _publication;
        private int _buildFrame = -1;
        private float _nextPlacement;
        private static readonly SaveKey WorldKey = SaveBus.Key("WildWasteland.POI.Authored.v1");

        internal PoiDressing.Site Create(string name, float width, float depth, bool existing)
        {
            if (!GameRefs.InPlay || GameRefs.MainCamera == null) throw new InvalidOperationException("Load a world first.");
            ValidateName(name);
            if (_publication != null) throw new InvalidOperationException("Wait for the current POI save/bake to finish.");
            Definition d = Get(name);
            if (existing && d == null) throw new InvalidOperationException("Custom POI not found.");
            if (!existing && (d != null || PrefabCatalog.Exact(name) != null)) throw new InvalidOperationException("That POI name is already in use.");
            if (!existing)
            {
                if (float.IsNaN(width) || float.IsNaN(depth) || width < 8 || width > 1000 || depth < 8 || depth > 1000) throw new FormatException("Footprint must be 8-1000 metres.");
                d = new Definition { Id = name, Width = width, Depth = depth, Layout = new PoiDressLayout { Type = name } };
            }
            var camera = GameRefs.MainCamera.transform; Vector3 forward = camera.forward; forward.y = 0;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward; forward.Normalize();
            float radius = Mathf.Sqrt(d.Width * d.Width + d.Depth * d.Depth) * .5f + 4;
            Vector3 point = camera.position + forward * (radius + 15); Terrain terrain = TerrainAt(point, radius + 8);
            if (terrain == null) throw new InvalidOperationException("Move into open desert away from a terrain boundary.");
            ModApi.Get<INavApi>()?.Structures(_nearby);
            foreach (var row in _nearby) if (row.Root != null)
            { var delta = row.Root.position - point; delta.y = 0; var bounds = row.Box ?? new Bounds(row.Root.position, Vector3.one * 40); if (delta.magnitude < radius + Mathf.Max(bounds.extents.x, bounds.extents.z) + 10) throw new InvalidOperationException("This pad overlaps an existing POI. Move farther into empty desert."); }
            TownInfo town; if (_owner.TryTownAt(Origin.World(point), out town)) throw new InvalidOperationException("Move outside the town into open desert.");
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y + .02f;
            var placement = new Placement { Key = Guid.NewGuid().ToString("N"), Type = name, World = Origin.World(point) };
            if (!existing) _drafts[name] = d;
            Materialize(placement, d, terrain); _placements.Add(placement);
            Status = "Editing " + name + ". Save POI publishes it to the town catalog.";
            return _dressing.Attach(placement.Root.transform);
        }
        private static Terrain TerrainAt(Vector3 point, float margin)
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                var p = point - terrain.transform.position; var size = terrain.terrainData.size;
                if (p.x >= margin && p.z >= margin && p.x <= size.x - margin && p.z <= size.z - margin) return terrain;
            }
            return null;
        }
        private void Materialize(Placement p, Definition d, Terrain terrain)
        {
            Vector3 point = Origin.Scene(p.World);
            var anchor = new GameObject("POI authored foundation anchor"); anchor.transform.position = point;
            try
            {
                float radius = Mathf.Sqrt(d.Width * d.Width + d.Depth * d.Depth) * .5f + 4;
                var site = new TownSite { Anchor = anchor.transform, Plan = new TownPlan { Key = "Authored/" + p.Key, WorldCenter = Vector3.zero, Radius = radius, Feather = 8 } };
                p.Pad = TownGroundPad.Prepare(site, terrain);
                p.Root = Shell(d, false); p.Root.name = d.Id + " (Authored " + p.Key + ")";
                p.Root.transform.SetParent(terrain.transform, false); p.Root.transform.position = point;
                p.Root.transform.rotation = Quaternion.Euler(0, p.Yaw, 0);
                p.Root.SetActive(true);
                _dressing.Attach(p.Root.transform);
            }
            finally { Object.Destroy(anchor); }
        }
        internal void QueueSave(PoiDressing.Site site, PoiDressLayout layout)
        {
            if (_publication != null) throw new InvalidOperationException("A POI save/bake is already running.");
            var navigation = ModApi.Get<ITownNavigation>();
            if (navigation == null || ModApi.Get<INavigationAuthoring>()?.Enabled.Value != true) throw new InvalidOperationException("Enable NPC navigation to bake this POI before publishing it to towns.");
            var original = Get(site.Type); if (original == null) throw new InvalidOperationException("Custom POI definition missing.");
            var d = new Definition { Id = original.Id, Width = original.Width, Depth = original.Depth, Layout = layout.Copy() };
            var geometry = Shell(d, true); geometry.tag = "Untagged";
            _publication = new Publication { Definition = d, Geometry = geometry };
            Status = "Saving " + d.Id + ": preparing geometry for navigation bake...";
        }
        internal void QueueNativeSave(PoiDressing.Site site, PoiDressLayout layout)
        {
            if (_publication != null) throw new InvalidOperationException("Wait for the current POI save to finish.");
            if (ModApi.Get<ITownNavigation>() == null || ModApi.Get<INavigationAuthoring>()?.Enabled.Value != true) throw new InvalidOperationException("Enable NPC navigation to save and bake the edited POI.");
            var prefab = _dressing.NativeTemplate(site.Type); if (prefab == null) throw new InvalidOperationException("Native POI template is unavailable.");
            if (_templates == null) { _templates = new GameObject("POI authored templates"); _templates.SetActive(false); }
            var geometry = Object.Instantiate(prefab, _templates.transform, false); geometry.name = site.Type; geometry.tag = "Untagged";
            foreach (var behaviour in geometry.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) Object.DestroyImmediate(behaviour);
            foreach (var body in geometry.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            _dressing.HideTemplateScenery(geometry.transform, layout);
            _publication = new Publication { Native = true, Geometry = geometry, Definition = new Definition { Id = site.Type, Layout = layout.Copy() } };
            Status = "Saving " + site.Type + ": preparing edited scenery and navigation...";
        }
        internal bool Step()
        {
            var work = _publication;
            if (work == null) { RestorePlacements(); return false; }
            try
            {
                if (_buildFrame == Time.frameCount) return false;
                _buildFrame = Time.frameCount;
                if (work.Next < work.Definition.Layout.Props.Count)
                {
                    var prop = work.Definition.Layout.Props[work.Next++];
                    if (!prop.Marker) { var asset = _dressing.Catalog.Resolve(prop); if (asset == null) throw new InvalidOperationException("Missing native mesh: " + prop.Mesh); _dressing.Catalog.Create(asset, work.Geometry.transform, prop, prop.Collision); }
                    return true;
                }
                if (work.Bake == null)
                {
                    Bounds bounds; if (!TownAssets.ColliderBox(work.Geometry.transform, out bounds)) throw new InvalidOperationException("POI has no static geometry.");
                    if (!work.Native)
                    {
                        float width = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) * 2, depth = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)) * 2;
                        if (width > work.Definition.Width + .01f) work.Definition.Width = Mathf.Ceil(width + 2);
                        if (depth > work.Definition.Depth + .01f) work.Definition.Depth = Mathf.Ceil(depth + 2);
                        if (work.Definition.Width > 1000 || work.Definition.Depth > 1000) throw new InvalidOperationException("POI footprint exceeds 1000 metres. Move distant props closer before saving.");
                        var slab = work.Geometry.transform.GetChild(0); slab.localScale = new Vector3(work.Definition.Width, .25f, work.Definition.Depth);
                    }
                    var spec = TownAssets.Measure(work.Geometry); if (spec == null) throw new InvalidOperationException("POI has no navigation colliders.");
                    work.Definition.Layout.Metadata["townBounds"] = new object[] { spec.Center.x, spec.Center.y, spec.Center.z, spec.HalfX, spec.HalfZ, spec.ColliderCount };
                    var navigation = ModApi.Get<ITownNavigation>(); if (navigation == null) throw new InvalidOperationException("NPC navigation stopped during POI save.");
                    work.Bake = navigation.CreateIsolated(work.Geometry);
                }
                work.Bake.Step(); Status = "Saving " + work.Definition.Id + ": navigation " + Mathf.RoundToInt(work.Bake.Progress * 100) + "%";
                if (!work.Bake.Ready) return true;
                if (string.IsNullOrEmpty(work.Bake.MapFile)) throw new InvalidOperationException("Navigation was disabled before the map was published.");
                var d = work.Definition; d.NavigationName = Path.GetFileName(work.Bake.MapFile); d.Navigation = File.ReadAllBytes(work.Bake.MapFile);
                if (work.Native)
                {
                    d.Layout.Metadata["navigationName"] = d.NavigationName; d.Layout.Metadata["navigation"] = Convert.ToBase64String(d.Navigation);
                    PoiDressStore.Save(d.Layout); _dressing.RememberLayout(d.Layout); _owner.AuthoredChanged();
                    Log.Main(null, "[POI] edited native POI saved: " + d.Id + "; props=" + d.Layout.Props.Count + "; navigation baked");
                }
                else Publish(d);
                _dressing.ApplyLayout(d.Layout);
                foreach (var p in _placements) if (p.Type == d.Id) p.Saved = true;
                if (!work.Native) foreach (var p in _placements) if (p.Type == d.Id && p.Root != null) { var slab = p.Root.transform.Find("Authored POI foundation"); if (slab != null) slab.localScale = new Vector3(d.Width, .25f, d.Depth); }
                Status = "Saved POI " + d.Id + ". Available in Town templates; portable file: " + (work.Native ? PoiDressStore.FilePath(d.Id) : Path.Combine(DirectoryPath, d.Id + ".json"));
                CancelPublication();
            }
            catch (Exception e) { Status = "POI save failed; previous definition retained: " + e.Message; Log.Warn(null, "[POI] " + Status); CancelPublication(); }
            return _publication != null;
        }
        private void CancelPublication()
        { var work = _publication; _publication = null; if (work == null) return; work.Bake?.Dispose(); if (work.Geometry != null) Object.Destroy(work.Geometry); }
        private void RestorePlacements()
        {
            if (!GameRefs.InPlay || Time.unscaledTime < _nextPlacement) return; _nextPlacement = Time.unscaledTime + 1;
            foreach (var p in _placements)
            {
                if (p.Root != null || !p.Saved) continue; Definition d; if (!_definitions.TryGetValue(p.Type, out d)) continue;
                var terrain = TerrainAt(Origin.Scene(p.World), Mathf.Sqrt(d.Width * d.Width + d.Depth * d.Depth) * .5f + 12);
                if (terrain != null) { Materialize(p, d, terrain); break; }
            }
        }
        internal void SaveWorld(string slot)
        {
            if (_publication != null) throw new InvalidOperationException("Wait for the custom POI save/navigation bake before saving the world.");
            var definitions = new List<object>(); foreach (var d in _definitions.Values) definitions.Add(Json.ParseObject(Serialize(d)));
            var layouts = _dressing.SnapshotLayouts();
            var placements = new List<object>(); foreach (var p in _placements) if (p.Saved) placements.Add(new Dictionary<string, object> { { "key", p.Key }, { "type", p.Type }, { "x", p.World.x }, { "y", p.World.y }, { "z", p.World.z }, { "yaw", p.Yaw } });
            WorldKey.Write(_owner.Owner, slot, Json.Write(new Dictionary<string, object> { { "version", 1 }, { "definitions", definitions.ToArray() }, { "layouts", layouts.ToArray() }, { "placements", placements.ToArray() } }));
        }
        internal void LoadWorld(string slot)
        {
            ClearLive(); _placements.Clear(); Reload(); _dressing.ResetWorldLayouts();
            if (string.IsNullOrEmpty(slot)) return;
            var disk = new ES3Settings(slot) { location = ES3.Location.File };
            if (!File.Exists(disk.FullPath) || !ES3.KeyExists("WildWasteland.POI.Authored.v1", disk)) return;
            var data = Json.ParseObject(ES3.Load<string>("WildWasteland.POI.Authored.v1", disk));
            object version; if (!data.TryGetValue("version", out version) || !(version is double) || (double)version != 1) throw new FormatException("Authored POI world version");
            var definitions = data["definitions"] as List<object>; var placements = data["placements"] as List<object>;
            if (definitions == null || placements == null) throw new FormatException("Authored POI world data");
            foreach (var value in definitions) { var d = Parse(Json.Write(value)); _definitions[d.Id] = d; InstallMap(d); }
            object savedLayouts; var layouts = data.TryGetValue("layouts", out savedLayouts) ? savedLayouts as List<object> : null;
            if (layouts != null) foreach (var value in layouts) { var row = value as Dictionary<string, object>; if (row == null) throw new FormatException("POI layout snapshot"); var layout = PoiDressStore.Parse(Json.Write(value), row["poi"] as string); _dressing.RememberLayout(layout); }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in placements)
            {
                var row = value as Dictionary<string, object>; if (row == null) throw new FormatException("Authored POI placement");
                string key = row["key"] as string, type = row["type"] as string; Guid guid;
                if (key == null || !Guid.TryParseExact(key, "N", out guid) || !keys.Add(key) || type == null || !_definitions.ContainsKey(type)) throw new FormatException("Authored POI placement identity");
                var point = new Vector3(Coordinate(row, "x"), Coordinate(row, "y"), Coordinate(row, "z"));
                _placements.Add(new Placement { Key = key, Type = type, World = point, Saved = true, Yaw = row.ContainsKey("yaw") ? Coordinate(row, "yaw") : 0 });
            }
            Log.Main(null, "[POI] authored world restored: " + definitions.Count + " definitions, " + placements.Count + " desert POIs");
        }
        private static float Coordinate(Dictionary<string, object> data, string key)
        { object v; if (!data.TryGetValue(key, out v) || !(v is double) || double.IsNaN((double)v) || double.IsInfinity((double)v) || Math.Abs((double)v) > 100000000) throw new FormatException("POI coordinate"); return (float)(double)v; }
        internal void ClearLive()
        {
            _disabledNative.Clear();
            CancelPublication();
            foreach (var p in _placements) { if (p.Root != null) { ModApi.Get<INavApi>()?.ForgetStructure(p.Root.transform); p.Root.SetActive(false); Object.Destroy(p.Root); } p.Root = null; p.Pad = null; }
        }
    }
}
