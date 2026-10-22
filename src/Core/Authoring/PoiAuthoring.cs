using System;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Detached metadata of a live POI dressing instance.</summary>
    public sealed class PoiAuthoringSite { public int Id; public Transform Root; public string Type, Error, LayoutJson; public bool Ready; public Bounds Bounds; }
    /// <summary>Detached catalog entry; identifiers resolve only while the provider is alive.</summary>
    public sealed class PoiAuthoringAsset { public long Id; public string Label, MeshName, Source, Category, MarkerKind; public Vector3 NativeScale,Offset; public Bounds Box; }
    /// <summary>Reusable static POI authoring operations. Draft rendering and input belong to Tools.</summary>
    public interface IPoiAuthoring
    {
        void Discover(); void Sites(List<PoiAuthoringSite> into);
        bool CatalogComplete { get; }
        void StartCatalog(); void Catalog(List<PoiAuthoringAsset> into);
        GameObject CreatePreview(long assetId,string markerKind,string propJson,Transform parent);
        void SetDressingVisible(int siteId,bool visible);
        bool Save(int siteId,string layoutJson,out string error);
        void Profiles(string kind,List<string> into);
        string LayoutPath(string type);
        string LayoutDirectory { get; }
        void RegisterSession(IModule owner,Func<bool> buildPreview,Action close);
        void ReleaseSession(IModule owner);
    }
}
