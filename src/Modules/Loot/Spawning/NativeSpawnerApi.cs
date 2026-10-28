using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Loot
{
    /// <summary>Publishes the native spawner implementation without exposing Loot internals.</summary>
    internal sealed class NativeSpawnerApi : INativeSpawners
    {
        /// <summary>Returns read for the current owner state.</summary>
        public int Read(PlayMakerFSM f, string n) { return NativeSpawners.Read(f,n); }
        /// <summary>Returns write for the current owner state.</summary>
        public void Write(PlayMakerFSM f, string n, int v) { NativeSpawners.Write(f,n,v); }
        /// <summary>Captures immutable generation inputs or the current native progress.</summary>
        public NativeSpawnBudget Capture(PlayMakerFSM f) { var b=NativeSpawners.Capture(f); return new NativeSpawnBudget { Min=b.Min, Max=b.Max, Kind=b.Kind, Counts=b.Counts }; }
        /// <summary>Returns force for the current owner state.</summary>
        public void Force(PlayMakerFSM f, int n, List<GameObject> p) { NativeSpawners.Force(f,n,p); }
        /// <summary>Applies the captured numeric geometry changes to the current terrain instance.</summary>
        public void Apply(PlayMakerFSM f, int n, List<GameObject> p) { NativeSpawners.Apply(f,n,p); }
        /// <summary>Returns select for the current owner state.</summary>
        public void Select(PlayMakerFSM f, int[] i, float[] w) { NativeSpawners.Select(f,i,w); }
        /// <summary>Returns resume for the current owner state.</summary>
        public void Resume(PlayMakerFSM f) { NativeSpawners.Resume(f); }
        /// <summary>Returns create catalog for the current owner state.</summary>
        public INativeSpawnCatalog CreateCatalog() { return new PoiSpawnCatalog(); }
        /// <summary>Tests or resolves the requested marker kind.</summary>
        public NativeSpawnProgress Marker(string k) { return SpawnerProgress.Marker(k); }
        /// <summary>Returns add marker for the current owner state.</summary>
        public NativeSpawnProgress AddMarker(string k,int n,int on) { return SpawnerProgress.AddMarker(k,n,on); }
        /// <summary>Returns track for the current owner state.</summary>
        public void Track(string k,PlayMakerFSM f) { SpawnerProgress.Track(k,f); }
        /// <summary>Returns capture markers for the current owner state.</summary>
        public void CaptureMarkers() { SpawnerProgress.CaptureMarkers(); }
        /// <summary>Returns clear markers for the current owner state.</summary>
        public void ClearMarkers(bool reset) { SpawnerProgress.ClearMarkers(reset); }
        /// <summary>Returns save markers for the current owner state.</summary>
        public void SaveMarkers(string slot) { SpawnerProgress.SaveMarkers(slot); }
        /// <summary>Returns load markers for the current owner state.</summary>
        public void LoadMarkers(string slot) { SpawnerProgress.LoadMarkers(slot); }
        /// <summary>Returns item category for the current owner state.</summary>
        public string ItemCategory(GameObject root) { return PoiSpawnCatalog.ItemCategory(root); }
    }
}
