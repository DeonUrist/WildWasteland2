using System;
using System.Collections.Generic;
using ES3Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Runtime prop instantiation for POI layouts: resolve a layout prop to a native mesh/material/collider asset
    /// (ES3 reference id, else the portable source path), build hidden templates and place clones.
    /// Wave A (A6): partial split of Towns/PoiPropCatalog.cs (POI part, B13). Capture/Sources stay here because Resolve needs them
    /// at runtime (file-map listed them for Tools); the marker gizmo assets (Marker) stay in the Tools part.
    /// </summary>
    internal sealed partial class PoiPropCatalog : IDisposable
    {
        internal sealed class Shape
        {
            internal int Kind, Direction;
            internal Vector3 Center, Size;
            internal float Radius, Height;
            internal Mesh Mesh;
        }
        internal sealed class Asset
        {
            internal long Id;
            internal string Label, MeshName, Source;
            internal string Category, MarkerKind;
            internal Mesh Mesh;
            internal Material[] Materials;
            internal Vector3 NativeScale, Offset;
            internal Bounds Box;
            internal UnityEngine.Rendering.ShadowCastingMode Shadows;
            internal bool ReceiveShadows;
            internal readonly List<Shape> Shapes = new List<Shape>();
            internal GameObject Visual, Solid;
        }
        private readonly Dictionary<long, Asset> _assets = new Dictionary<long, Asset>();
        private GameObject _templates;
        internal Asset Resolve(PoiDressProp prop)
        {
            if (prop.Marker) return Marker(prop.Kind);
            Asset asset;
            if (_assets.TryGetValue(prop.Asset, out asset)) return asset.MeshName == prop.Mesh ? asset : null;
            var manager = ES3ReferenceMgrBase.Current;
            var filter = prop.Asset < 0 || manager == null ? null : manager.Get(prop.Asset, true) as MeshFilter;
            if ((filter == null || filter.sharedMesh == null || filter.sharedMesh.name != prop.Mesh) && !string.IsNullOrEmpty(prop.Source))
            { Sources(); _sources.TryGetValue(prop.Source, out filter); }
            if (filter == null || filter.gameObject.scene.IsValid() || filter.sharedMesh == null || filter.sharedMesh.name != prop.Mesh) return null;
            return Capture(prop.Asset, filter);
        }
        private Asset Capture(long id, MeshFilter filter)
        {
            Asset asset; if (_assets.TryGetValue(id, out asset)) return asset;
            var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || mesh.vertexCount == 0) return null;
            var scale = filter.transform.lossyScale;
            if (scale.x == 0 || scale.y == 0 || scale.z == 0) return null;
            var size = Vector3.Scale(mesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var root = filter.transform; while (root.parent != null) root = root.parent;
            asset = new Asset { Id = id, Mesh = mesh, MeshName = mesh.name, Source = Source(filter),
                Label = filter.name + " · " + mesh.name + " · " + root.name, Materials = renderer.sharedMaterials,
                NativeScale = scale, Offset = -Vector3.Scale(mesh.bounds.center, scale) + Vector3.up * size.y * .5f,
                Box = new Bounds(Vector3.up * size.y * .5f, size), Shadows = renderer.shadowCastingMode, ReceiveShadows = renderer.receiveShadows };
            asset.Category = PoiDressMarkers.Category(root.gameObject, filter.name + " " + mesh.name);
            foreach (var collider in filter.GetComponents<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                var box = collider as BoxCollider; var sphere = collider as SphereCollider; var capsule = collider as CapsuleCollider; var surface = collider as MeshCollider;
                if (box != null) asset.Shapes.Add(new Shape { Kind = 0, Center = box.center, Size = box.size });
                else if (sphere != null) asset.Shapes.Add(new Shape { Kind = 1, Center = sphere.center, Radius = sphere.radius });
                else if (capsule != null) asset.Shapes.Add(new Shape { Kind = 2, Center = capsule.center, Radius = capsule.radius, Height = capsule.height, Direction = capsule.direction });
                else if (surface != null && surface.sharedMesh != null) asset.Shapes.Add(new Shape { Kind = 3, Mesh = surface.sharedMesh });
            }
            _assets.Add(id, asset); return asset;
        }
        internal GameObject Create(Asset asset, Transform parent, PoiDressProp prop, bool solid)
        {
            if (_templates == null) { _templates = new GameObject("POI dressing static templates"); _templates.SetActive(false); }
            var template = solid ? asset.Solid : asset.Visual;
            if (template == null)
            {
                template = new GameObject("POI prop " + asset.MeshName); template.transform.SetParent(_templates.transform, false);
                var geometry = new GameObject("Geometry"); geometry.transform.SetParent(template.transform, false);
                geometry.transform.localPosition = asset.Offset; geometry.transform.localScale = asset.NativeScale;
                geometry.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                var renderer = geometry.AddComponent<MeshRenderer>(); renderer.sharedMaterials = asset.Materials;
                renderer.shadowCastingMode = asset.Shadows; renderer.receiveShadows = asset.ReceiveShadows;
                if (solid)
                {
                    foreach (var shape in asset.Shapes)
                    {
                        if (shape.Kind == 0) { var c = geometry.AddComponent<BoxCollider>(); c.center = shape.Center; c.size = shape.Size; }
                        else if (shape.Kind == 1) { var c = geometry.AddComponent<SphereCollider>(); c.center = shape.Center; c.radius = shape.Radius; }
                        else if (shape.Kind == 2) { var c = geometry.AddComponent<CapsuleCollider>(); c.center = shape.Center; c.radius = shape.Radius; c.height = shape.Height; c.direction = shape.Direction; }
                        else { var c = geometry.AddComponent<MeshCollider>(); c.sharedMesh = shape.Mesh; c.convex = false; }
                    }
                    if (asset.Shapes.Count == 0) { var c = geometry.AddComponent<MeshCollider>(); c.sharedMesh = asset.Mesh; c.convex = false; }
                }
                if (solid) asset.Solid = template; else asset.Visual = template;
            }
            var go = Object.Instantiate(template, parent, false); Pose(go.transform, prop); return go;
        }
        internal static void Pose(Transform transform, PoiDressProp prop)
        { transform.localPosition = prop.Position; transform.localRotation = Quaternion.Euler(prop.Rotation); transform.localScale = prop.Scale; }
        public void Dispose()
        {
            if (_scan != null) _scan.Dispose(); _scan = null;
            if (_templates != null) { _templates.SetActive(false); Object.Destroy(_templates); } _templates = null;
            _assets.Clear(); All.Clear(); _listed.Clear(); Started = Complete = false;
            _sources.Clear(); _sourcesReady = false;
            foreach (var asset in _markers.Values) { if (asset.Mesh != null) Object.Destroy(asset.Mesh); foreach (var m in asset.Materials) if (m != null) Object.Destroy(m); } _markers.Clear();
        }
        private readonly Dictionary<string, MeshFilter> _sources = new Dictionary<string, MeshFilter>(StringComparer.Ordinal);
        private bool _sourcesReady;
        private void Sources()
        {
            if (_sourcesReady) return; _sourcesReady = true;
            foreach (var filter in Resources.FindObjectsOfTypeAll<MeshFilter>()) if (filter != null && !filter.gameObject.scene.IsValid()) _sources[Source(filter)] = filter;
        }
        private static string Source(MeshFilter filter)
        {
            var path = new System.Text.StringBuilder(); var root = filter.transform;
            for (var t = filter.transform; t.parent != null; t = t.parent) { path.Insert(0, "/" + t.GetSiblingIndex() + ":" + t.name); root = t.parent; }
            path.Insert(0, root.name); var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null) foreach (var material in renderer.sharedMaterials) path.Append('|').Append(material == null ? "" : material.name);
            return path.ToString();
        }
        private static long SourceId(string path)
        { ulong hash = 14695981039346656037UL; foreach (char c in path) hash = unchecked((hash ^ c) * 1099511628211UL); return unchecked((long)(hash | 0x8000000000000000UL)); }
    }
}
