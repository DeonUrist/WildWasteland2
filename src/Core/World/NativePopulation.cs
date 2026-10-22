using System;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Stable location-wide arrival and readiness context for native population.</summary>
    public sealed class PopulationArea { public string Key; public Transform Anchor; public Vector3 Center; public float Radius; public Func<bool> Ready; public Action<GameObject> Spawned; }
    /// <summary>NPC-owned arrival pacing and native spawner registration.</summary>
    public interface INativePopulation
    {
        void Own(PlayMakerFSM fsm,PopulationArea area,bool suspended,Func<bool> dressingReady);
        bool Ready(PopulationArea area); void Forget(PopulationArea area);
        void Discover(); string[] CaptureNames(); void RestoreNames(string[] names);
        void Catalog(GameObject prefab,string faction);
    }
}
