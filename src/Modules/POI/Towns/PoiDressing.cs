using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    // A custom POI is registered before Nav captures geometry. Only one static prop is activated per frame globally.
    /// <summary>
    /// Runtime static dressing of custom POIs from their layouts (one prop activation per frame), site registry and save of layouts.
    /// Wave A (A6): POI part of Towns/PoiDressing.cs; the Nav.Make gate, Exit hints and PoiDressNavigation are in NPCAI/Navigation/_fromTowns/PoiDressing.Navigation.cs.
    /// </summary>
    internal sealed partial class PoiDressing : IDisposable
    {
        internal sealed class Site
        {
            internal Transform Root;
            internal TownSite Town;
            internal List<GameObject> Population;
            internal string Type, Error;
            internal PoiDressLayout Layout;
            internal GameObject Dressing;
            internal int Next;
            internal bool Ready;
            internal Bounds Bounds;
            internal PoiDressProp[] Exits = new PoiDressProp[0];
        }
        private static PoiDressing _current;
        private TownRuntime _owner;
        internal readonly PoiPropCatalog Catalog = new PoiPropCatalog();
        internal readonly PoiDressMarkers Markers = new PoiDressMarkers();
        internal readonly List<Site> Sites = new List<Site>();
        private readonly Dictionary<int, Site> _roots = new Dictionary<int, Site>();
        private readonly Dictionary<string, PoiDressLayout> _layouts = new Dictionary<string, PoiDressLayout>();
        private readonly Queue<Site> _pending = new Queue<Site>();
        private readonly List<NavStructure> _structures = new List<NavStructure>();
        private int _frame = -1;
        internal PoiDresser Editor;
        internal void Install(TownRuntime owner)
        {
            _current = this;
            _owner = owner;
            var nav = typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.Nav", true);
            if (!Patches.Patch(owner.Owner, Patches.Method(nav, "Make"), prefix: typeof(PoiDressing).GetMethod("BeforeMap", BindingFlags.Static | BindingFlags.NonPublic)))
                throw new InvalidOperationException("POI dressing navigation gate could not be installed");
            Scheduler.Sliced(owner.Owner, "POI static dressing", 500, Step);
            Scheduler.Interval(owner.Owner, "POI dressing cleanup", 5f, Sweep, Phase.Update, false);
            Scheduler.Sliced(owner.Owner, "POI marker catalog", 500, Markers.Catalog.Step);
        }
        private static bool Custom(Transform root)
        {
            if (root == null || !root.CompareTag("SpawnerStatic") || root.GetComponent<Rigidbody>() != null) return false;
            string name = TypeName(root);
            return name != "Starter_Area" && !NativeNumber(name, "Camp_") && !NativeNumber(name, "Building_")
                && !NativeNumber(name, "Cave_") && !NativeNumber(name, "Wreck_") && !name.StartsWith("TownsTower", StringComparison.Ordinal);
        }
        private static bool NativeNumber(string name, string prefix)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length) return false;
            for (int i = prefix.Length; i < name.Length; i++) if (!char.IsDigit(name[i])) return false;
            return true;
        }
        private static string TypeName(Transform root)
        { string name = root.name; int end = name.IndexOf(' '), clone = name.IndexOf('('); if (end < 0 || clone >= 0 && clone < end) end = clone; return end < 0 ? name : name.Substring(0, end); }
        internal void DiscoverExisting()
        {
            if (Services.Npc == null) return;
            _structures.Clear(); Services.Npc.Structures(_structures);
            foreach (var row in _structures)
            {
                if (!Custom(row.Root) || _roots.ContainsKey(row.Root.GetInstanceID())) continue;
                var site = Attach(row.Root); if (!site.Ready) PoiDressNavigation.Forget(row.Root);
            }
            _structures.Clear();
        }
        internal Site Attach(Transform root)
        {
            if (root == null) return null;
            Site site; if (_roots.TryGetValue(root.GetInstanceID(), out site)) return site;
            site = new Site { Root = root, Type = TypeName(root) };
            var render = root.GetComponent<Renderer>(); site.Bounds = render == null ? new Bounds(Vector3.zero, Vector3.one * 40) : new Bounds(root.InverseTransformPoint(render.bounds.center), Vector3.Scale(render.bounds.size, new Vector3(1 / Mathf.Abs(root.lossyScale.x), 1 / Mathf.Abs(root.lossyScale.y), 1 / Mathf.Abs(root.lossyScale.z))));
            try
            {
                if (site.Type == "TownsGasStation") site.Town = _owner.MarkerTown(root, out site.Population);
                PoiDressLayout layout;
                if (!_layouts.TryGetValue(site.Type, out layout)) { layout = PoiDressStore.Load(site.Type); _layouts.Add(site.Type, layout); }
                site.Layout = layout; site.Ready = layout.Props.Count == 0;
                site.Exits = layout.Props.FindAll(p => p.Kind == "Exit").ToArray();
                foreach (var prop in layout.Props) if (prop.Marker) { Markers.Catalog.Start(); break; }
            }
            catch (Exception e) { site.Error = e.Message; site.Ready = true; Log.Warn("[Towns] POI layout rejected for " + site.Type + ": " + e.Message); }
            _roots.Add(root.GetInstanceID(), site); Sites.Add(site); if (!site.Ready) _pending.Enqueue(site); return site;
        }
        private bool Step()
        {
            // Catalog enumeration is cheap and may use the rest of the scheduler slice. Object activation is capped separately.
            if (Editor != null && Editor.Opened && Editor.BuildPreview()) return true;
            if (_frame != Time.frameCount && !SaveBus.Saving)
            {
                int left = _pending.Count;
                while (left-- > 0)
                {
                    var site = _pending.Dequeue();
                    if (site.Root == null || site.Ready) continue;
                    if (!site.Root.gameObject.activeInHierarchy) { _pending.Enqueue(site); continue; }
                    _frame = Time.frameCount; Build(site); if (!site.Ready) _pending.Enqueue(site); return true;
                }
            }
            return Catalog.Step();
        }
        private void Build(Site site)
        {
            try
            {
                if (site.Dressing == null)
                {
                    site.Dressing = new GameObject("POI static dressing"); site.Dressing.transform.SetParent(site.Root, false);
                }
                if (site.Next < site.Layout.Props.Count)
                {
                    var prop = site.Layout.Props[site.Next]; var asset = Catalog.Resolve(prop);
                    if (prop.Marker && prop.Kind != "Hide" && prop.Kind != "Exit" && !Markers.Catalog.Ready) { Markers.Catalog.Start(); return; }
                    if (asset == null) throw new InvalidOperationException("Native mesh unavailable: " + prop.Mesh + " (reference " + prop.Asset + ")");
                    if (!Markers.Build(site, prop)) Catalog.Create(asset, site.Dressing.transform, prop, prop.Collision); site.Next++;
                }
                if (site.Next == site.Layout.Props.Count)
                { site.Ready = true; Physics.SyncTransforms(); TownBakeBridge.Discover(site.Root.gameObject); }
            }
            catch (Exception e)
            {
                if (site.Dressing != null) { site.Dressing.SetActive(false); Object.Destroy(site.Dressing); site.Dressing = null; }
                site.Error = e.Message; site.Ready = true;
                Log.Warn("[Towns] POI dressing skipped for " + site.Type + ": " + e.Message);
                TownBakeBridge.Discover(site.Root.gameObject);
            }
        }
        internal void Save(Site site, PoiDressLayout layout)
        {
            // Resolve every reference before publication: never silently replace an existing layout with missing props.
            foreach (var prop in layout.Props) if (Catalog.Resolve(prop) == null) throw new InvalidOperationException("Missing native mesh: " + prop.Mesh);
            foreach (var prop in layout.Props) if (prop.Marker && prop.Kind != "Hide" && prop.Kind != "Exit" && Markers.Catalog.Find(prop.Kind, prop.Profile) == null) throw new InvalidOperationException("Choose an available spawn list for " + prop.Mesh);
            Markers.Capture();
            PoiDressStore.Save(layout); _layouts[site.Type] = layout.Copy();
            foreach (var row in Sites)
            {
                if (row.Root == null || row.Type != site.Type) continue;
                PoiDressNavigation.Forget(row.Root);
                if (row.Dressing != null) { row.Dressing.SetActive(false); Object.Destroy(row.Dressing); row.Dressing = null; }
                bool queued = !row.Ready;
                row.Layout = _layouts[site.Type]; row.Next = 0; row.Error = null; row.Ready = false;
                row.Exits = row.Layout.Props.FindAll(p => p.Kind == "Exit").ToArray();
                if (!queued) _pending.Enqueue(row);
            }
        }
        private void Sweep()
        {
            for (int i = Sites.Count - 1; i >= 0; i--)
                if (Sites[i].Root == null) Sites.RemoveAt(i);
            _roots.Clear(); foreach (var site in Sites) if (site.Root != null) _roots[site.Root.GetInstanceID()] = site;
        }
        internal void Clear()
        {
            if (Editor != null) Editor.Close(true);
            Markers.Clear(false);
            foreach (var site in Sites) if (site.Dressing != null)
            { if (site.Root != null) PoiDressNavigation.Forget(site.Root); site.Dressing.SetActive(false); Object.Destroy(site.Dressing); }
            Sites.Clear(); _roots.Clear(); _layouts.Clear(); _pending.Clear(); _structures.Clear(); Catalog.Dispose();
        }
        public void Dispose() { Clear(); if (ReferenceEquals(_current, this)) _current = null; }
    }
}
