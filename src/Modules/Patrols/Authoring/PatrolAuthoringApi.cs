using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Validates authoring operations against the live patrol catalog without exposing runtime templates or groups.</summary>
    public sealed partial class PatrolsModule : IPatrolAuthoring
    {
        /// <summary>Reads the actual patrol marker faction for a recorded vehicle, with the native default for unowned cars.</summary>
        public string VehicleFaction(GameObject vehicle) { var marker = vehicle == null ? null : vehicle.GetComponent<PatrolMarker>(); return TrafficFactions.Normalize(marker == null ? "Scrapyard" : marker.Faction); }
        bool IPatrolAuthoring.InPlay { get { return Running && _patrol != null && _patrol.InGame(); } }
        /// <summary>Serializes the live authored group and cargo projection into a detached editor workspace.</summary>
        public string ReadData() { return EditorStore.Data.ToJson(); }
        /// <summary>Parses the complete proposed workspace and restores the prior runtime authority if publication fails.</summary>
        public void SaveData(string json) { var prior = EditorStore.Data; var parsed = EditorData.FromJson(json); EditorStore.Data = parsed; try { EditorStore.Save(); } catch { EditorStore.Data = prior; throw; } }
        /// <summary>Restores shipped authoring defaults using the requested custom-file recycling policy.</summary>
        public void Reset(bool recycleCustom) { EditorStore.Reset(recycleCustom); }
        /// <summary>Reconciles the actual authored template catalog with disk.</summary>
        public void Refresh(bool force) { CarTemplates.Refresh(force); }
        /// <summary>Reports the current runtime template catalog summary.</summary>
        public string Summary { get { return CarTemplates.Summary; } }
        /// <summary>Returns the Patrols authoring data directory.</summary>
        public string DataDirectory { get { return DataDir; } }
        /// <summary>Returns the current custom recorded-template directory.</summary>
        public string PlayerDirectory { get { return CarTemplates.PlayerFolder; } }
        /// <summary>Replaces the output list with detached metadata for every current template.</summary>
        public void Templates(List<PatrolTemplateInfo> into) { into.Clear(); foreach (var t in CarTemplate.Park) into.Add(Snapshot(t)); }
        /// <summary>Copies template metadata and crew arrays recursively without sharing runtime mutable objects.</summary>
        private static PatrolTemplateInfo Snapshot(CarTemplate t) { return new PatrolTemplateInfo { Name=t.Name, Body=t.Body, Driver=t.Driver, Passenger=t.Passenger, Faction=t.Faction, LootPreset=t.LootPreset, SourcePath=t.SourcePath, MissingPrefab=t.MissingPrefab, KindLabel=t.KindLabel, Available=t.Available, IsDefault=t.IsDefault, Favorite=t.Favorite, IsTruck=t.IsTruck, IsMotorcycle=t.IsMotorcycle, ExplodeChance=t.ExplodeChance, Turrets=(string[])t.Turrets.Clone(), TurretStanding=(bool[])t.TurretStanding.Clone(), Trailer=t.Trailer==null?null:Snapshot(t.Trailer) }; }
        /// <summary>Resolves a current template by name and rejects stale editor selections.</summary>
        private static CarTemplate Template(string name) { var value=CarTemplate.Find(name); if(value==null)throw new ArgumentException("Template is no longer available: "+name); return value; }
        /// <summary>Resolves a current template by name and rejects stale editor selections.</summary>
        public void Delete(string name) { CarTemplates.Delete(Template(name)); }
        /// <summary>Resolves a current template by name and rejects stale editor selections.</summary>
        public void Favorite(string name) { CarTemplates.Favorite(Template(name)); }
        /// <summary>Validates faction-compatible crew and atomically publishes current template metadata.</summary>
        public void Edit(PatrolTemplateEdit edit)
        {
            var t=Template(edit.Name); if(string.IsNullOrEmpty(t.SourcePath)||!File.Exists(t.SourcePath))throw new InvalidOperationException("Template file was not found");
            var file=TemplateFile.FromJson(File.ReadAllText(t.SourcePath)); string faction=TrafficFactions.Normalize(edit.Faction??t.Faction);
            if(new[]{edit.Driver,edit.Passenger}.Concat(edit.Turrets??new string[0]).Concat(edit.TrailerTurrets??new string[0]).Any(p=>!CrewFactions.Allowed(p,faction)))throw new ArgumentException("A template cannot mix Coyote and Scrapyard crew");
            file.faction=faction;if(file.trailer!=null)file.trailer.faction=faction;file.explodeChance=Mathf.Clamp(edit.ExplodeChance,0,100);
            file.driver=edit.Driver=="None"?"":edit.Driver;file.passenger=edit.Passenger=="None"?"":edit.Passenger;file.lootPreset=edit.Loot??"";
            for(int i=0;i<3;i++){file.SetTurret(i,edit.Turrets!=null&&i<edit.Turrets.Length?Rider.Stored(edit.Turrets[i]):"");file.SetTurretStanding(i,edit.Standing!=null&&i<edit.Standing.Length&&edit.Standing[i]&&!string.IsNullOrEmpty(file.Turret(i)));}
            if(file.trailer!=null)for(int i=0;i<4;i++){file.trailer.SetTurret(i,edit.TrailerTurrets!=null&&i<edit.TrailerTurrets.Length?Rider.Stored(edit.TrailerTurrets[i]):"");file.trailer.SetTurretStanding(i,edit.TrailerStanding!=null&&i<edit.TrailerStanding.Length&&edit.TrailerStanding[i]&&!string.IsNullOrEmpty(file.trailer.Turret(i)));}
            EditorStore.AtomicWrite(t.SourcePath,file.ToJson());CarTemplates.Refresh(true);
        }
        /// <summary>Reconciles the actual authored template catalog with disk.</summary>
        public void RemoveLootPreset(string id) { foreach(var t in CarTemplate.Park.Where(t=>string.Equals(t.LootPreset,id,StringComparison.OrdinalIgnoreCase))) { if(!File.Exists(t.SourcePath))continue;var file=TemplateFile.FromJson(File.ReadAllText(t.SourcePath));file.lootPreset="";EditorStore.AtomicWrite(t.SourcePath,file.ToJson()); }CarTemplates.Refresh(true); }
        /// <summary>Reconciles the actual authored template catalog with disk.</summary>
        public string SaveRecorded(string name,string json) { if(string.IsNullOrWhiteSpace(name)||name.Length>160||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new ArgumentException("Invalid template name");string path=EditorStore.SafePath(CarTemplates.PlayerFolder,name+".json");if(CarTemplate.Find(name)!=null||File.Exists(path))throw new IOException("A template with that name already exists");var parsed=CarTemplates.Parse(json,name,"player",path);if(parsed==null)throw new FormatException("Invalid recorded template");EditorStore.AtomicWrite(path,json);CarTemplates.Refresh(true);return path; }
        /// <summary>Resolves a current template by name and rejects stale editor selections.</summary>
        public void SpawnTemplate(string name,int role,string groupId,string[] cargoOptions) { if(!InPlay)return;var spawn=Template(name).CloneForSpawn();spawn.SetupOnly=true;spawn.ReleaseHandbrake=false;if(spawn.IsTruck&&role==1)spawn.SpawnCargoOptions=cargoOptions;var group=Groups(role).FirstOrDefault(g=>g.Id==groupId);if(role==2)MerchantRuntime.SpawnTemplate(_patrol,spawn,group);else _patrol.Spawn(spawn); }
        /// <summary>Starts an explicit departure through normal runtime group orchestration.</summary>
        public void SpawnGroup(string id,int role) { if(!InPlay)return;var group=Groups(role).FirstOrDefault(g=>g.Id==id);if(group!=null)WorldTraffic.DebugDeparture(group,role); }
        /// <summary>Selects the authoritative current group collection for the requested role.</summary>
        private static IEnumerable<SpawnGroup> Groups(int role) { return role==2?EditorStore.Data.Merchants:role==1?EditorStore.Data.Convoys:EditorStore.Data.Patrols; }
        /// <summary>Uses the runtime faction canonicalization rules for editor choices.</summary>
        public string NormalizeFaction(string faction) { return TrafficFactions.Normalize(faction); }
        /// <summary>Checks runtime crew eligibility against the selected faction.</summary>
        public bool CrewAllowed(string name,string faction) { return CrewFactions.Allowed(name,faction); }
        /// <summary>Checks the actual current template roster against the selected faction.</summary>
        public bool Compatible(string name,string faction) { var t=CarTemplate.Find(name);return t!=null&&CrewFactions.Compatible(t,faction); }
        /// <summary>Returns detached Coyote crew choices from the authoritative runtime catalog.</summary>
        public string[] CoyoteCrew { get { return (string[])CrewFactions.Coyotes.Clone(); } }
        /// <summary>Returns detached current turret occupant choices.</summary>
        public string[] TurretChoices { get { return (string[])Rider.TurretChoices.Clone(); } }
        /// <summary>Uses actual body and trailer rules to count supported turret slots.</summary>
        public int TurretSlots(string body,bool truck,bool trailer) { return Rider.Slots(body,truck,trailer); }
        /// <summary>Maps a persisted turret choice to its editor label.</summary>
        public string TurretDisplay(string stored) { return Rider.Display(stored); }
        /// <summary>Maps an editor label to its canonical stored turret choice.</summary>
        public string TurretStored(string shown) { return Rider.Stored(shown); }
        /// <summary>Returns detached supported merchant category identifiers.</summary>
        public string[] MerchantTypes { get { return (string[])MerchantCatalog.Types.Clone(); } }
        /// <summary>Returns the editor label for an actual merchant category.</summary>
        public string MerchantLabel(string type) { return MerchantCatalog.Label(type); }
        /// <summary>Normalizes an authored merchant selection using the runtime catalog.</summary>
        public string MerchantSelected(IEnumerable<string> types) { return MerchantCatalog.Selected(types); }
    }
}
