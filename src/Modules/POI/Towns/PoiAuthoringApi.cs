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
        /// <summary>Initializes poi authoring api with the supplied owning state and immutable inputs.</summary>
        internal PoiAuthoringApi(PoiDressing runtime){_runtime=runtime;}
        /// <summary>Advances or requests world discovery through the owning runtime.</summary>
        public void Discover()=>_runtime.DiscoverExisting();
        public void Types(List<string> into)=>_runtime.Types(into);
        public void CustomTypes(List<string> into)=>_runtime.Authored.Names(into);
        public bool IsCustom(string type)=>_runtime.Authored.Contains(type);
        public void PrepareEdit(int siteId)=>_runtime.PrepareEdit(Site(siteId));
        public string AuthoringStatus=>_runtime.Authored.Status;
        public bool Spawns(string type)=>_runtime.Authored.Spawns(type);
        public bool SetSpawns(string type,bool enabled,out string error){try{_runtime.Authored.SetSpawns(type,enabled);error=null;return true;}catch(Exception e){error=e.Message;return false;}}
        public bool Create(string name,float width,float depth,bool existing,out int siteId,out string error)
        {try{var site=existing&&!_runtime.Authored.Contains(name)?_runtime.OpenNative(name):_runtime.Authored.Create(name,width,depth,existing);siteId=site.Root.GetInstanceID();error=null;return true;}catch(Exception e){siteId=0;error=e.Message;return false;}}
        /// <summary>Copies detached metadata and layout JSON for live authoring sites.</summary>
        public void Sites(List<PoiAuthoringSite> into){into.Clear();foreach(var s in _runtime.Sites)if(s.Root!=null)into.Add(new PoiAuthoringSite{Id=s.Root.GetInstanceID(),Root=s.Root,Type=s.Type,Error=s.Error,LayoutJson=s.EditLayout!=null?PoiDressStore.Serialize(s.EditLayout):s.Layout==null?null:PoiDressStore.Serialize(s.Layout),Ready=s.Ready,Bounds=s.Bounds});}
        /// <summary>Reports completion of the sliced native scenery catalog.</summary>
        public bool CatalogComplete=>_runtime.Catalog.Complete;
        /// <summary>Starts native scenery and spawn-profile discovery for authoring.</summary>
        public void StartCatalog(){_runtime.Catalog.Start();if(ModApi.Get<INativeSpawners>()!=null)_runtime.Markers.Catalog.Start();}
        /// <summary>Copies detached authoring assets without exposing runtime catalog objects.</summary>
        public void Catalog(List<PoiAuthoringAsset> into){into.Clear();foreach(var a in _runtime.Catalog.All)into.Add(new PoiAuthoringAsset{Id=a.Id,Label=a.Label,MeshName=a.MeshName,Source=a.Source,Category=a.Category,MarkerKind=a.MarkerKind,NativeScale=a.NativeScale,Offset=a.Offset,Box=a.Box,HasCollision=a.Shapes.Count>0});}
        /// <summary>Creates a static preview after validating its prop JSON and catalog identity.</summary>
        public GameObject CreatePreview(long id,string marker,string json,Transform parent,bool solid)
        {var layout=PoiDressStore.Parse(json,"preview");if(layout.Props.Count!=1)throw new FormatException("One preview prop required");var prop=layout.Props[0];if(prop.Asset!=id||prop.Kind!=marker)throw new FormatException("Preview identity mismatch");var a=_runtime.Catalog.Resolve(prop);return a==null?null:_runtime.Catalog.Create(a,parent,prop,solid&&prop.Collision&&!prop.Marker);}
        /// <summary>Resolves an authoring site identifier and fails if its live root disappeared.</summary>
        private PoiDressing.Site Site(int id){foreach(var s in _runtime.Sites)if(s.Root!=null&&s.Root.GetInstanceID()==id)return s;throw new InvalidOperationException("POI instance no longer exists");}
        /// <summary>Hides or restores committed dressing while Tools renders an editable draft.</summary>
        public void SetDressingVisible(int id,bool visible){var s=Site(id);s.Hidden=!visible;if(s.Dressing!=null)s.Dressing.SetActive(visible);_runtime.EditVisibility(s,visible);}
        /// <summary>Writes the current validated state through its owning persistence transaction.</summary>
        public bool Save(int id,string json,out string error){try{var s=Site(id);_runtime.Save(s,PoiDressStore.Parse(json,s.Type));error=null;return true;}catch(Exception e){error=e.Message;return false;}}
        /// <summary>Copies available native spawn-profile names of the requested marker kind.</summary>
        public void Profiles(string kind,List<string> into){into.Clear();if(ModApi.Get<INativeSpawners>()==null)return;_runtime.Markers.Catalog.Start();foreach(var p in _runtime.Markers.Catalog.For(kind))into.Add(p.Name);}
        /// <summary>Returns the exact number of native selection rows in a profile.</summary>
        public int ProfileCount(string kind,string name)=>ModApi.Get<INativeSpawners>()==null?0:_runtime.Markers.Catalog.Find(kind,name)?.Count??0;
        /// <summary>Returns the validated reusable layout path of a POI type.</summary>
        public string LayoutPath(string type)=>PoiDressStore.FilePath(type);
        /// <summary>Returns the module-owned reusable layout directory.</summary>
        public string LayoutDirectory=>PoiDressStore.DirectoryPath;
        /// <summary>Registers the Tools-owned preview and close callbacks for one authoring session.</summary>
        public void RegisterSession(IModule owner,Func<bool> build,Action close){if(owner==null)throw new ArgumentNullException(nameof(owner));if(_sessionOwner!=null&&_sessionOwner!=owner)throw new InvalidOperationException("Another POI authoring session is active");_sessionOwner=owner;_build=build;_close=close;}
        /// <summary>Releases callbacks belonging to the matching authoring owner.</summary>
        public void ReleaseSession(IModule owner){if(_sessionOwner!=owner)return;_sessionOwner=null;_build=null;_close=null;}
        /// <summary>Admits one preview construction step only while its session owner is running.</summary>
        internal bool BuildPreview(){if(_sessionOwner==null||!ModuleHost.IsRunning(_sessionOwner))return false;return _build!=null&&_build();}
        /// <summary>Releases session ownership before invoking the close callback.</summary>
        internal void Close(){var close=_close;_sessionOwner=null;_build=null;_close=null;close?.Invoke();}
    }
}
