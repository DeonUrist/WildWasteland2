using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Tools
{
    /// <summary>Tools-owned catalog view over detached metadata from the active POI authoring provider.</summary>
    internal sealed class PoiEditorCatalog
    {
        private readonly IPoiAuthoring _source;
        internal readonly List<PoiAuthoringAsset> All = new List<PoiAuthoringAsset>();
        internal PoiEditorCatalog(IPoiAuthoring source) { _source = source; }
        internal bool Complete { get { return _source.CatalogComplete; } }
        internal void Start() { _source.StartCatalog(); Refresh(); }
        internal void Refresh() { _source.Catalog(All); }
        internal PoiAuthoringAsset Resolve(EditorProp prop)
        {
            foreach (var asset in All) if (prop.Marker ? asset.MarkerKind == prop.Kind : asset.Id == prop.Asset && asset.MeshName == prop.Mesh || !string.IsNullOrEmpty(prop.Source) && asset.Source == prop.Source && asset.MeshName == prop.Mesh) return asset;
            return null;
        }
        internal GameObject Create(PoiAuthoringAsset asset, Transform parent, EditorProp prop)
        {
            var layout = new EditorLayout { Type = "preview" }; layout.Props.Add(prop.Copy());
            var result = _source.CreatePreview(prop.Asset, prop.Kind, EditorLayoutJson.Serialize(layout), parent);
            if (result == null) throw new InvalidOperationException("Native preview asset is unavailable: " + prop.Mesh);
            return result;
        }
        internal static void Pose(Transform target, EditorProp prop) { target.localPosition = prop.Position; target.localRotation = Quaternion.Euler(prop.Rotation); target.localScale = prop.Scale; }
    }
}
