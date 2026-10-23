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
        /// <summary>Binds this catalog view to the actual POI authoring provider.</summary>
        internal PoiEditorCatalog(IPoiAuthoring source) { _source = source; }
        /// <summary>Reports whether the owner finished collecting available static assets.</summary>
        internal bool Complete { get { return _source.CatalogComplete; } }
        /// <summary>Requests the owner catalog and refreshes its detached metadata.</summary>
        internal void Start() { _source.StartCatalog(); Refresh(); }
        /// <summary>Replaces local catalog metadata from the current owner.</summary>
        internal void Refresh() { _source.Catalog(All); }
        /// <summary>Matches a draft prop to a catalog asset by identity or portable source path.</summary>
        internal PoiAuthoringAsset Resolve(EditorProp prop)
        {
            foreach (var asset in All) if (prop.Marker ? asset.MarkerKind == prop.Kind : asset.Id == prop.Asset && asset.MeshName == prop.Mesh || !string.IsNullOrEmpty(prop.Source) && asset.Source == prop.Source && asset.MeshName == prop.Mesh) return asset;
            return null;
        }
        /// <summary>Requests an unsaved owner-created preview with the complete prop settings.</summary>
        internal GameObject Create(PoiAuthoringAsset asset, Transform parent, EditorProp prop, bool solid = true)
        {
            var layout = new EditorLayout { Type = "preview" }; layout.Props.Add(prop.Copy());
            var result = _source.CreatePreview(prop.Asset, prop.Kind, EditorLayoutJson.Serialize(layout), parent, solid);
            if (result == null) throw new InvalidOperationException("Native preview asset is unavailable: " + prop.Mesh);
            return result;
        }
        /// <summary>Applies draft position, Euler rotation and scale to a preview transform.</summary>
        internal static void Pose(Transform target, EditorProp prop) { target.localPosition = prop.Position; target.localRotation = Quaternion.Euler(prop.Rotation); target.localScale = prop.Scale; }
    }
}
