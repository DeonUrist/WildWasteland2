using System;
using System.Collections.Generic;
using ES3Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    // Catalog work runs once in slices. Placing a prop never clones its native scripts, tags or Rigidbody.
    /// <summary>
    /// POI editor catalog of vanilla meshes (sliced ES3/prefab MeshFilter scan) and the marker gizmo assets.
    /// Wave A (A6): Tools part of Towns/PoiPropCatalog.cs; runtime Resolve/Create/Pose in POI/_fromTowns/PoiPropCatalog.Runtime.cs.
    /// </summary>
    internal sealed partial class PoiPropCatalog
    {
        private readonly HashSet<ulong> _listed = new HashSet<ulong>();
        internal readonly List<Asset> All = new List<Asset>();
        private IEnumerator<KeyValuePair<long, Object>> _scan;
        internal bool Started, Complete;
        internal bool Busy { get { return _scan != null; } }
        internal void Start()
        {
            if (Started) return;
            foreach (string kind in PoiDressMarkers.Kinds) { var marker = Marker(kind); if (!All.Contains(marker)) All.Add(marker); }
            var manager = ES3ReferenceMgrBase.Current; if (manager == null) return;
            // ES3 registers new objects while the world runs. Take a stable reference snapshot only for the editor catalog.
            var snapshot = new List<KeyValuePair<long, Object>>(manager.idRef);
            // The ES3 table is not a complete scenery inventory. Also walk every loaded native prefab mesh,
            // including non-saveable child decorations (spikes, bones, bathrooms, etc.), with a portable source path.
            foreach (var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                if (filter == null || filter.gameObject.scene.IsValid()) continue;
                long id = manager.Get(filter); if (id < 0) id = SourceId(Source(filter));
                snapshot.Add(new KeyValuePair<long, Object>(id, filter));
            }
            _scan = snapshot.GetEnumerator(); Started = true;
        }
        internal bool Step()
        {
            if (_scan == null) return false;
            if (!_scan.MoveNext())
            {
                _scan.Dispose(); _scan = null; Complete = true;
                All.Sort(Compare); WildWasteland.Core.Log.Always("[Towns] POI dresser catalog: " + All.Count + " distinct vanilla mesh/material variants and markers; prefab children included, no size limit."); return false;
            }
            var pair = _scan.Current; var filter = pair.Value as MeshFilter;
            if (filter == null || filter.gameObject.scene.IsValid()) return true;
            Asset asset = Capture(pair.Key, filter); if (asset == null) return true;
            ulong key = unchecked((ulong)(uint)asset.Mesh.GetInstanceID() * 1099511628211UL);
            foreach (var material in asset.Materials) key = unchecked((key ^ (uint)(material == null ? 0 : material.GetInstanceID())) * 1099511628211UL);
            key = unchecked((key ^ (uint)asset.NativeScale.GetHashCode()) * 1099511628211UL);
            if (_listed.Add(key)) All.Add(asset);
            return true;
        }
        private static int Compare(Asset a, Asset b) { return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase); }
        private readonly Dictionary<string, Asset> _markers = new Dictionary<string, Asset>(StringComparer.Ordinal);
        private Asset Marker(string kind)
        {
            Asset asset; if (_markers.TryGetValue(kind, out asset)) return asset;
            if (!PoiDressMarkers.IsKind(kind)) return null;
            var mesh = new Mesh { name = "POI marker " + kind };
            mesh.vertices = new[] { new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f), new Vector3(.5f,0,.5f), new Vector3(-.5f,0,.5f),
                new Vector3(-.5f,1,-.5f), new Vector3(.5f,1,-.5f), new Vector3(.5f,1,.5f), new Vector3(-.5f,1,.5f), new Vector3(0,.1f,0), new Vector3(0,.1f,1.2f), new Vector3(-.2f,.1f,.9f), new Vector3(.2f,.1f,.9f) };
            mesh.SetIndices(new[] { 0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7,8,9,9,10,9,11 }, MeshTopology.Lines, 0); mesh.RecalculateBounds();
            var material = new Material(Shader.Find("Sprites/Default")) { color = kind == "Hide" ? Color.cyan : kind == "Exit" ? Color.green : kind == "Monsters" ? new Color(1,.3f,.25f) : Color.yellow };
            asset = new Asset { Mesh = mesh, MeshName = kind + " marker", Label = kind == "Hide" ? "Hiding area" : kind == "Exit" ? "Exit / doorway" : kind == "CarParts" ? "Car parts spawn" : kind == "Monsters" ? "Monster / NPC spawn" : "Item spawn",
                MarkerKind = kind, Category = "Markers", Materials = new[] { material }, NativeScale = Vector3.one, Box = new Bounds(Vector3.up * .5f, Vector3.one), Shadows = UnityEngine.Rendering.ShadowCastingMode.Off };
            _markers.Add(kind, asset); return asset;
        }
    }
}
