using System;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Inactive geometry-only copy, built one scenery prop per step and owned by its caller.</summary>
    public interface IPoiNavigationGeometry
    {
        GameObject Root { get; }
        bool Step();
    }
    /// <summary>Detached metadata of a live POI dressing instance.</summary>
    public sealed class PoiAuthoringSite { public int Id; public Transform Root; public string Type, Error, LayoutJson; public bool Ready; public Bounds Bounds; }
    /// <summary>Detached catalog entry; identifiers resolve only while the provider is alive.</summary>
    public sealed class PoiAuthoringAsset { public long Id; public string Label, MeshName, Source, Category, MarkerKind; public Vector3 NativeScale,Offset; public Bounds Box; public bool HasCollision; }
    public interface IGasStationAuthoring { string Status {get;} int LoadedCount {get;} Transform TestRoot {get;} void Settings(List<BepInEx.Configuration.ConfigEntryBase> into); }
    /// <summary>Reusable static POI authoring operations. Draft rendering and input belong to Tools.</summary>
    public interface IPoiAuthoring
    {
        void Discover(); void Sites(List<PoiAuthoringSite> into);
        void Types(List<string> into);
        void CustomTypes(List<string> into);
        IPoiNavigationGeometry CreateNavigationGeometry(string type, Transform parent);
        bool Create(string name, float width, float depth, bool existing, out int siteId, out string error);
        bool IsCustom(string type);
        void PrepareEdit(int siteId);
        string AuthoringStatus { get; }
        bool Spawns(string type);
        bool SetSpawns(string type,bool enabled,out string error);
        bool CatalogComplete { get; }
        void StartCatalog(); void Catalog(List<PoiAuthoringAsset> into);
        GameObject CreatePreview(long assetId,string markerKind,string propJson,Transform parent,bool solid);
        void SetDressingVisible(int siteId,bool visible);
        bool Save(int siteId,string layoutJson,out string error);
        void Profiles(string kind,List<string> into);
        int ProfileCount(string kind,string name);
        string LayoutPath(string type);
        string LayoutDirectory { get; }
        void RegisterSession(IModule owner,Func<bool> buildPreview,Action close);
        void ReleaseSession(IModule owner);
    }
}
