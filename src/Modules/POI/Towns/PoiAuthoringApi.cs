using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Publishes validated authoring snapshots and static previews without exposing runtime site/catalog types.</summary>
    internal sealed class PoiAuthoringApi : IPoiAuthoring
    {
        private readonly PoiDressing _runtime;
        private IModule _sessionOwner; private Func<bool> _build; private Action _close;
        internal PoiAuthoringApi(PoiDressing runtime){_runtime=runtime;}
        public void Discover()=>_runtime.DiscoverExisting();
        public void Sites(List<PoiAuthoringSite> into){into.Clear();foreach(var s in _runtime.Sites)if(s.Root!=null)into.Add(new PoiAuthoringSite{Id=s.Root.GetInstanceID(),Root=s.Root,Type=s.Type,Error=s.Error,LayoutJson=s.Layout==null?null:PoiDressStore.Serialize(s.Layout),Ready=s.Ready,Bounds=s.Bounds});}
        public bool CatalogComplete=>_runtime.Catalog.Complete;
        public void StartCatalog(){_runtime.Catalog.Start();_runtime.Markers.Catalog.Start();}
        public void Catalog(List<PoiAuthoringAsset> into){into.Clear();foreach(var a in _runtime.Catalog.All)into.Add(new PoiAuthoringAsset{Id=a.Id,Label=a.Label,MeshName=a.MeshName,Source=a.Source,Category=a.Category,MarkerKind=a.MarkerKind,NativeScale=a.NativeScale,Offset=a.Offset,Box=a.Box,HasCollision=a.Shapes.Count>0});}
        public GameObject CreatePreview(long id,string marker,string json,Transform parent)
        {var layout=PoiDressStore.Parse(json,"preview");if(layout.Props.Count!=1)throw new FormatException("One preview prop required");var prop=layout.Props[0];if(prop.Asset!=id||prop.Kind!=marker)throw new FormatException("Preview identity mismatch");var a=_runtime.Catalog.Resolve(prop);return a==null?null:_runtime.Catalog.Create(a,parent,prop,false);}
        private PoiDressing.Site Site(int id){foreach(var s in _runtime.Sites)if(s.Root!=null&&s.Root.GetInstanceID()==id)return s;throw new InvalidOperationException("POI instance no longer exists");}
        public void SetDressingVisible(int id,bool visible){var s=Site(id);if(s.Dressing!=null)s.Dressing.SetActive(visible);}
        public bool Save(int id,string json,out string error){try{var s=Site(id);_runtime.Save(s,PoiDressStore.Parse(json,s.Type));error=null;return true;}catch(Exception e){error=e.Message;return false;}}
        public void Profiles(string kind,List<string> into){into.Clear();_runtime.Markers.Catalog.Start();foreach(var p in _runtime.Markers.Catalog.For(kind))into.Add(p.Name);}
        public int ProfileCount(string kind,string name)=>_runtime.Markers.Catalog.Find(kind,name)?.Count??0;
        public string LayoutPath(string type)=>PoiDressStore.FilePath(type);
        public string LayoutDirectory=>PoiDressStore.DirectoryPath;
        public void RegisterSession(IModule owner,Func<bool> build,Action close){if(owner==null)throw new ArgumentNullException(nameof(owner));if(_sessionOwner!=null&&_sessionOwner!=owner)throw new InvalidOperationException("Another POI authoring session is active");_sessionOwner=owner;_build=build;_close=close;}
        public void ReleaseSession(IModule owner){if(_sessionOwner!=owner)return;_sessionOwner=null;_build=null;_close=null;}
        internal bool BuildPreview(){if(_sessionOwner==null||!ModuleHost.IsRunning(_sessionOwner))return false;return _build!=null&&_build();}
        internal void Close(){var close=_close;_sessionOwner=null;_build=null;_close=null;close?.Invoke();}
    }
}
