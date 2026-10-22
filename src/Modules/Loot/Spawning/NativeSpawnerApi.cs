using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Loot
{
    /// <summary>Publishes the native spawner implementation without exposing Loot internals.</summary>
    internal sealed class NativeSpawnerApi : INativeSpawners
    {
        public int Read(PlayMakerFSM f, string n) { return NativeSpawners.Read(f,n); }
        public void Write(PlayMakerFSM f, string n, int v) { NativeSpawners.Write(f,n,v); }
        public NativeSpawnBudget Capture(PlayMakerFSM f) { var b=NativeSpawners.Capture(f); return new NativeSpawnBudget { Min=b.Min, Max=b.Max, Kind=b.Kind }; }
        public void Force(PlayMakerFSM f, int n, List<GameObject> p) { NativeSpawners.Force(f,n,p); }
        public void Apply(PlayMakerFSM f, int n, List<GameObject> p) { NativeSpawners.Apply(f,n,p); }
        public void Select(PlayMakerFSM f, int[] i, float[] w) { NativeSpawners.Select(f,i,w); }
        public void Resume(PlayMakerFSM f) { NativeSpawners.Resume(f); }
        public INativeSpawnCatalog CreateCatalog() { return new PoiSpawnCatalog(); }
        public NativeSpawnProgress Marker(string k) { return SpawnerProgress.Marker(k); }
        public NativeSpawnProgress AddMarker(string k,int n,int on) { return SpawnerProgress.AddMarker(k,n,on); }
        public void Track(string k,PlayMakerFSM f) { SpawnerProgress.Track(k,f); }
        public void CaptureMarkers() { SpawnerProgress.CaptureMarkers(); }
        public void ClearMarkers(bool reset) { SpawnerProgress.ClearMarkers(reset); }
        public void SaveMarkers(string slot) { SpawnerProgress.SaveMarkers(slot); }
        public void LoadMarkers(string slot) { SpawnerProgress.LoadMarkers(slot); }
        public string ItemCategory(GameObject root) { return PoiSpawnCatalog.ItemCategory(root); }
    }
}
