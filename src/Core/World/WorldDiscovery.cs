using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Remembered camp geometry, detached from POI runtime ownership.</summary>
    public sealed class CampDiscovery { public string id,name,faction; public Vector3 center,gate,apron,size; public bool mappedEntrance,cleared; }
    /// <summary>Remembered native point of interest.</summary>
    public sealed class PoiDiscovery { public string id,name; public Vector3 position; }
    /// <summary>Live camp resident fighting target for Patrols reinforcement decisions.</summary>
    public struct CampResidentInfo { public string Camp; public GameObject Actor,Target; public float Health; public bool CallRolled; }
    /// <summary>POI-owned discovery and native camp population state; consumers keep their own journey policy.</summary>
    public interface IWorldDiscovery
    {
        int Revision { get; } bool Ready { get; } int Left { get; }
        void Camps(List<CampDiscovery> into); void Points(List<PoiDiscovery> into);
        bool Loaded(string id); bool CampReady(string id); bool Check(string id);
        void Bind(string id,GameObject actor); void Residents(List<CampResidentInfo> into); void MarkCallRolled(GameObject actor);
    }
}
