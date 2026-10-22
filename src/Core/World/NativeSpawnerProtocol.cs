using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Detached native spawn group count and classification.</summary>
    public struct NativeSpawnBudget { public int Min, Max, Kind; }
    /// <summary>Committed one-shot marker progress.</summary>
    public sealed class NativeSpawnProgress { public int OnOff, Number, NumberEnd; }
    /// <summary>Native weighted selection available to marker authoring and spawning.</summary>
    public sealed class NativeSpawnProfile { public string Name, Kind; public PlayMakerFSM Template; public int[] Indices; public float[] Weights; public int Count; }
    /// <summary>Incremental native weighted-list discovery owned by Loot.</summary>
    public interface INativeSpawnCatalog
    {
        bool Ready { get; }
        List<NativeSpawnProfile> Profiles { get; }
        void Start(); bool Step(); void Clear();
        NativeSpawnProfile Find(string kind, string name);
        List<NativeSpawnProfile> For(string kind);
    }
    /// <summary>Loot-owned control of native ItemSpawner FSMs and their committed marker progress.</summary>
    public interface INativeSpawners
    {
        int Read(PlayMakerFSM fsm, string name);
        void Write(PlayMakerFSM fsm, string name, int value);
        NativeSpawnBudget Capture(PlayMakerFSM fsm);
        void Force(PlayMakerFSM fsm, int count, List<GameObject> pool);
        void Apply(PlayMakerFSM fsm, int count, List<GameObject> pool);
        void Select(PlayMakerFSM fsm, int[] indices, float[] weights);
        void Resume(PlayMakerFSM fsm);
        INativeSpawnCatalog CreateCatalog();
        NativeSpawnProgress Marker(string key);
        NativeSpawnProgress AddMarker(string key, int count, int onOff);
        void Track(string key, PlayMakerFSM fsm);
        void CaptureMarkers(); void ClearMarkers(bool reset);
        void SaveMarkers(string slot); void LoadMarkers(string slot);
        string ItemCategory(GameObject root);
    }
}
