using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Single owner for discovered native camps, POIs, custom registrations and town summaries.</summary>
    internal sealed class WorldLocations : IWorldLocations, IWorldDiscovery, ICampStatus
    {
        private readonly ITownsApi _towns;
        private readonly Dictionary<string,WorldLocation> _custom=new Dictionary<string,WorldLocation>();
        private readonly Dictionary<string,IModule> _owners=new Dictionary<string,IModule>();
        private readonly List<TownInfo> _townBuffer=new List<TownInfo>();
        private readonly List<WorldLocation> _buffer=new List<WorldLocation>();
        private int _revision;
        /// <summary>Initializes world locations with the supplied owning state and immutable inputs.</summary>
        internal WorldLocations(ITownsApi towns){_towns=towns;}
        /// <summary>Returns unchecked for the current owner state.</summary>
        public int Revision {get{return unchecked(TrafficLocations.Revision*397^_towns.Revision^_revision);}}
        /// <summary>Reports whether the current preparation or discovery stage has completed.</summary>
        public bool Ready {get{return TrafficLocations.DiscoveryReady;}}
        /// <summary>Reports pending discovery work.</summary>
        public int Left {get{return TrafficLocations.DiscoveryLeft;}}
        /// <summary>Copies detached camp geometry and state into the caller buffer.</summary>
        public void Camps(List<CampDiscovery> into){into.Clear();foreach(var c in TrafficLocations.Known.Values)into.Add(new CampDiscovery{id=c.id,name=c.name,faction=c.faction,center=c.center,size=c.size,apron=c.apron,gate=c.gate,mappedEntrance=c.mappedEntrance,cleared=c.cleared});}
        /// <summary>Copies remembered points of interest into the caller buffer.</summary>
        public void Points(List<PoiDiscovery> into){into.Clear();foreach(var p in TrafficLocations.Points.Values)into.Add(new PoiDiscovery{id=p.id,name=p.name,position=p.position});}
        /// <summary>Tests whether a remembered camp root is loaded and active.</summary>
        public bool Loaded(string id)=>TrafficCamps.Loaded(id);
        /// <summary>Queries completion of the loaded camp native spawn groups.</summary>
        public bool CampReady(string id)=>TrafficCamps.Ready(id);
        /// <summary>Marks a camp cleared only after its native groups and observed residents are conclusively finished.</summary>
        public bool Check(string id)=>TrafficCamps.Check(id);
        /// <summary>Attaches managed population observations without adding components to saved actors.</summary>
        public void Bind(string id,GameObject actor)=>TrafficCamps.Bind(id,actor);
        /// <summary>Copies managed resident observations into detached consumer records.</summary>
        public void Residents(List<CampResidentInfo> into)=>TrafficCamps.Residents(into);
        /// <summary>Prevents another reinforcement roll for this observed resident.</summary>
        public void MarkCallRolled(GameObject actor)=>TrafficCamps.MarkCallRolled(actor);
        /// <summary>Queries camp state near a stable world position.</summary>
        public CampState At(Vector3 world,float radius)=>StateAt(world,radius);
        /// <summary>Returns the nearest known camp state without interpreting unloaded camps as empty.</summary>
        public CampState StateAt(Vector3 world,float radius){CampDiscovery nearest=null;foreach(var c in TrafficLocations.Known.Values){float d=TrafficLocations.FlatDistance(c.center,world);if(d<=radius){radius=d;nearest=c;}}return nearest==null?CampState.Unknown:nearest.cleared?CampState.Cleared:!Loaded(nearest.id)?CampState.Unloaded:CampReady(nearest.id)?CampState.Ready:CampState.Spawning;}
        /// <summary>Copies native locations, planned towns and active custom registrations.</summary>
        public void All(List<WorldLocation> into)
        {into.Clear();foreach(var c in TrafficLocations.Known.Values)into.Add(new WorldLocation{Key=c.id,Name=c.name,Faction=c.faction,Kind=WorldLocationKind.Camp,WorldPos=c.center,Radius=Mathf.Max(c.size.x,c.size.z)*.5f,State=c.cleared?CampState.Cleared:!Loaded(c.id)?CampState.Unloaded:CampReady(c.id)?CampState.Ready:CampState.Spawning});foreach(var p in TrafficLocations.Points.Values)into.Add(new WorldLocation{Key=p.id,Name=p.name,Kind=WorldLocationKind.Poi,WorldPos=p.position});_towns.Towns(_townBuffer);foreach(var t in _townBuffer)into.Add(new WorldLocation{Key=t.Key,Name=t.Key,Faction=t.Faction,Kind=WorldLocationKind.Town,WorldPos=t.WorldCenter,Radius=t.Radius});foreach(var pair in _custom){IModule owner;if(_owners.TryGetValue(pair.Key,out owner)&&owner!=null&&ModuleHost.IsRunning(owner))into.Add(pair.Value);}}
        /// <summary>Finds a registered location by its stable key.</summary>
        public bool TryGet(string key,out WorldLocation location){All(_buffer);foreach(var row in _buffer)if(row.Key==key){location=row;return true;}location=default(WorldLocation);return false;}
        /// <summary>Finds the nearest location of the requested kind inside the supplied radius.</summary>
        public bool TryNearest(Vector3 world,float radius,WorldLocationKind kind,out WorldLocation location){All(_buffer);location=default(WorldLocation);bool found=false;foreach(var row in _buffer){float d=TrafficLocations.FlatDistance(row.WorldPos,world);if(row.Kind==kind&&d<=radius){radius=d;location=row;found=true;}}return found;}
        /// <summary>Registers validated data under its owning module.</summary>
        public void Register(IModule owner,WorldLocation location){if(owner==null||string.IsNullOrEmpty(location.Key))throw new ArgumentException("Location owner and key required");_custom[location.Key]=location;_owners[location.Key]=owner;_revision++;}
        /// <summary>Removes the matching registration and releases its live presentation state.</summary>
        public void Remove(string key){if(_custom.Remove(key)){_owners.Remove(key);_revision++;}}
        /// <summary>Drops per-world observations and invalidates cached query revisions.</summary>
        internal void Reset(){TrafficLocations.Reset();TrafficCamps.Reset();_custom.Clear();_owners.Clear();_revision++;}
        /// <summary>Releases live watchers without forgetting unsaved persistent location decisions on a module restart.</summary>
        internal void Stop(){TrafficLocations.Stop();TrafficCamps.Reset();_revision++;}
        /// <summary>Writes the current validated state through its owning persistence transaction.</summary>
        internal void Save(IModule owner,string slot)
        {var rows=new List<object>();foreach(var c in TrafficLocations.Known.Values)rows.Add(new Dictionary<string,object>{{"id",c.id},{"name",c.name},{"faction",c.faction},{"center",V(c.center)},{"size",V(c.size)},{"gate",V(c.gate)},{"apron",V(c.apron)},{"mapped",c.mappedEntrance},{"cleared",c.cleared},{"captured",TrafficLocations.Captured.Contains(c.id)}});var pois=new List<object>();foreach(var p in TrafficLocations.Points.Values)pois.Add(new Dictionary<string,object>{{"id",p.id},{"name",p.name},{"position",V(p.position)}});SaveBus.Key("WildWasteland.POI.Locations.v1").Write(owner,slot,Json.Write(new Dictionary<string,object>{{"camps",rows},{"pois",pois}}));}
        /// <summary>Restores the current data format and rejects incompatible or corrupt input.</summary>
        internal void Load(string slot)
        {Reset();if(string.IsNullOrEmpty(slot))return;string json=SaveBus.Key("WildWasteland.POI.Locations.v1").Read(slot);if(json==null)return;var root=Json.ParseObject(json);var camps=root["camps"] as List<object>;var pois=root["pois"] as List<object>;if(camps==null||pois==null||camps.Count>512||pois.Count>1024)throw new FormatException("POI location record");foreach(Dictionary<string,object> d in camps){var c=new CampDiscovery{id=(string)d["id"],name=(string)d["name"],faction=(string)d["faction"],center=ReadV(d["center"]),size=ReadV(d["size"]),gate=ReadV(d["gate"]),apron=ReadV(d["apron"]),mappedEntrance=(bool)d["mapped"],cleared=(bool)d["cleared"]};TrafficLocations.Known.Add(c.id,c);if(d.ContainsKey("captured")&&(bool)d["captured"])TrafficLocations.Captured.Add(c.id);}foreach(Dictionary<string,object>d in pois){var p=new PoiDiscovery{id=(string)d["id"],name=(string)d["name"],position=ReadV(d["position"])};TrafficLocations.Points.Add(p.id,p);}TrafficLocations.Changed();}
        /// <summary>Encodes or decodes a three-component vector in the current record.</summary>
        private static object[] V(Vector3 p)=>new object[]{p.x,p.y,p.z};
        /// <summary>Reads exactly three finite coordinates from the location record.</summary>
        private static Vector3 ReadV(object raw){var a=raw as List<object>;if(a==null||a.Count!=3)throw new FormatException("Location vector");var p=new Vector3(Convert.ToSingle(a[0]),Convert.ToSingle(a[1]),Convert.ToSingle(a[2]));if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))throw new FormatException("Location coordinates");return p;}
    }
}
