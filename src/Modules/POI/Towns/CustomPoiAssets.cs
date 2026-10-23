using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    /// <summary>Portable authored POIs: layout, flat footprint and the existing baker's reusable navigation map.</summary>
    internal sealed partial class CustomPoiAssets : IDisposable
    {
        internal sealed class Definition
        {
            internal string Id, NavigationName;
            internal float Width, Depth;
            internal PoiDressLayout Layout;
            internal byte[] Navigation;
            internal GameObject Prefab;
        }
        internal static CustomPoiAssets Current;
        private readonly PoiDressing _dressing;
        private readonly TownRuntime _owner;
        private readonly Dictionary<string, Definition> _definitions = new Dictionary<string, Definition>(StringComparer.Ordinal);
        private readonly Dictionary<string, Definition> _drafts = new Dictionary<string, Definition>(StringComparer.Ordinal);
        private GameObject _templates;
        private Material _ground;
        internal string Status = "";
        internal static string DirectoryPath { get { return Path.Combine(Assets.ModuleDir("POI"), "Custom"); } }
        internal CustomPoiAssets(PoiDressing dressing, TownRuntime owner) { _dressing = dressing; _owner = owner; Current = this; Reload(); LoadSpawnPolicy(); MapMagic.Nodes.Graph.OnOutputFinalized += FilterNativeSpawns; }
        internal bool Contains(string id) { return _definitions.ContainsKey(id) || _drafts.ContainsKey(id); }
        internal void Names(List<string> into) { into.Clear(); into.AddRange(_definitions.Keys); into.Sort(StringComparer.Ordinal); }
        internal Definition Get(string id) { Definition d; return _definitions.TryGetValue(id, out d) || _drafts.TryGetValue(id, out d) ? d : null; }
        internal static void ValidateName(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) throw new FormatException("Use a POI name of 1-64 letters, numbers, _ or -.");
            string device = id.ToUpperInvariant(); if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" || device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal)) && device[3] >= '0' && device[3] <= '9') throw new FormatException("Choose a name that is not a Windows device name.");
            foreach (char c in id) if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') throw new FormatException("Use letters, numbers, _ or - in the POI name.");
            if (id == "Starter_Area" || id.StartsWith("Towns", StringComparison.Ordinal) || id.StartsWith("Building_", StringComparison.Ordinal) || id.StartsWith("Camp_", StringComparison.Ordinal) || id.StartsWith("Cave_", StringComparison.Ordinal) || id.StartsWith("Wreck_", StringComparison.Ordinal)) throw new FormatException("Choose a name outside the reserved native POI names.");
        }
        private static float Dimension(Dictionary<string, object> data, string key)
        { object value; if (!data.TryGetValue(key, out value) || !(value is double)) throw new FormatException("Missing " + key); double n = (double)value; if (double.IsNaN(n) || double.IsInfinity(n) || n < 8 || n > 1000) throw new FormatException("POI dimensions must be 8-1000 metres."); return (float)n; }
        private static Definition Parse(string json)
        {
            var data = Json.ParseObject(json); object value;
            if (data == null || !data.TryGetValue("version", out value) || !(value is double) || (double)value != 1) throw new FormatException("Custom POI version");
            string id = data.TryGetValue("id", out value) ? value as string : null; ValidateName(id);
            var d = new Definition { Id = id, Width = Dimension(data, "width"), Depth = Dimension(data, "depth") };
            if (!data.TryGetValue("layout", out value) || !(value is Dictionary<string, object>)) throw new FormatException("Custom POI layout");
            d.Layout = PoiDressStore.Parse(Json.Write(value), id);
            string map = data.TryGetValue("navigation", out value) ? value as string : null;
            d.NavigationName = data.TryGetValue("navigationName", out value) ? value as string : null;
            if (string.IsNullOrEmpty(map) || string.IsNullOrEmpty(d.NavigationName) || d.NavigationName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || Path.GetFileName(d.NavigationName) != d.NavigationName || !d.NavigationName.StartsWith(id + "-", StringComparison.Ordinal) || !d.NavigationName.EndsWith(".wwnav", StringComparison.Ordinal)) throw new FormatException("Custom POI navigation map");
            d.Navigation = Convert.FromBase64String(map);
            if (d.Navigation.Length < 40 || d.Navigation.Length > 8 * 1024 * 1024) throw new FormatException("Custom POI navigation size");
            using (var reader = new BinaryReader(new MemoryStream(d.Navigation, false))) if (reader.ReadInt32() != 0x574E4156 || reader.ReadInt32() != 1) throw new FormatException("Custom POI navigation format");
            return d;
        }
        private static string Serialize(Definition d)
        { return Json.Write(new Dictionary<string, object> { { "version", 1 }, { "id", d.Id }, { "width", d.Width }, { "depth", d.Depth }, { "layout", Json.ParseObject(PoiDressStore.Serialize(d.Layout)) }, { "navigationName", d.NavigationName }, { "navigation", Convert.ToBase64String(d.Navigation) } }); }
        private void Reload()
        {
            ReleaseTemplates(); _definitions.Clear(); _drafts.Clear();
            foreach (string resource in DiskResources.Names)
                if (resource.StartsWith("POI.Custom.", StringComparison.Ordinal) && resource.EndsWith(".json", StringComparison.Ordinal))
                    try { using (var stream = DiskResources.Open(resource)) using (var reader = new StreamReader(stream)) { var d = Parse(reader.ReadToEnd()); _definitions[d.Id] = d; } }
                    catch (Exception e) { Log.Warn(null, "[POI] bundled custom POI rejected: " + resource + "; " + e.Message); }
            if (Directory.Exists(DirectoryPath)) foreach (string file in Directory.GetFiles(DirectoryPath, "*.json"))
                try { if (new FileInfo(file).Length > 16 * 1024 * 1024) throw new FormatException("Custom POI exceeds 16 MB"); var d = Parse(File.ReadAllText(file)); if (Path.GetFileNameWithoutExtension(file) != d.Id) throw new FormatException("Filename must match POI id"); _definitions[d.Id] = d; }
                catch (Exception e) { Log.Warn(null, "[POI] local custom POI rejected: " + Path.GetFileName(file) + "; " + e.Message); }
        }
        internal void Prefabs(List<GameObject> into)
        { foreach (var d in _definitions.Values) { if (d.Prefab == null) d.Prefab = Shell(d, true); InstallMap(d); into.Add(d.Prefab); } }
        private static void InstallMap(Definition d)
        {
            string directory = Path.Combine(Assets.ModuleConfigDir("NPCAI"), "NavMaps", "Custom"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, d.NavigationName);
            if (File.Exists(path) && Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(d.Navigation)) return;
            File.WriteAllBytes(path + ".tmp", d.Navigation); AtomicFile.Replace(path + ".tmp", path, path + ".bak");
        }
        internal GameObject Shell(Definition d, bool template)
        {
            if (_templates == null) { _templates = new GameObject("POI authored templates"); _templates.SetActive(false); }
            if (_ground == null) { _ground = new Material(Shader.Find("Standard")); _ground.color = new Color(.54f, .46f, .34f); _ground.SetFloat("_Glossiness", 0); }
            var root = new GameObject(d.Id); root.transform.SetParent(_templates.transform, false); root.tag = "SpawnerStatic";
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); slab.name = "Authored POI foundation"; slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0, -.125f, 0); slab.transform.localScale = new Vector3(d.Width, .25f, d.Depth); slab.GetComponent<Renderer>().sharedMaterial = _ground;
            if (!template) root.transform.SetParent(null, false);
            return root;
        }
        internal PoiDressLayout Layout(string id) { var d = Get(id); return d == null ? null : d.Layout.Copy(); }
        internal void Publish(Definition d)
        {
            Directory.CreateDirectory(DirectoryPath); string path = Path.Combine(DirectoryPath, d.Id + ".json");
            string json = Serialize(d); Parse(json);
            File.WriteAllText(path + ".tmp", json); AtomicFile.Replace(path + ".tmp", path, path + ".bak");
            Definition previous; if (_definitions.TryGetValue(d.Id, out previous) && previous.Prefab != null) Object.Destroy(previous.Prefab);
            _definitions[d.Id] = d; _drafts.Remove(d.Id); d.Prefab = Shell(d, true); InstallMap(d);
            _owner.AuthoredChanged();
            Log.Main(null, "[POI] custom POI saved: " + d.Id + "; props=" + d.Layout.Props.Count + "; footprint=" + d.Width + "x" + d.Depth + "; town catalog refreshed");
        }
        private void ReleaseTemplates()
        { foreach (var d in _definitions.Values) d.Prefab = null; if (_templates != null) { _templates.SetActive(false); Object.Destroy(_templates); } _templates = null; }
        public void Dispose() { MapMagic.Nodes.Graph.OnOutputFinalized -= FilterNativeSpawns; ClearLive(); ReleaseTemplates(); if (_ground != null) Object.Destroy(_ground); if (ReferenceEquals(Current, this)) Current = null; }
    }
}
