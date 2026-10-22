using System;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Numeric lot geometry copied from a town plan.</summary>
    public sealed class TownNavLot { public string Prefab; public bool IsParking,Skipped,HasEntry; public int EntryNode,Street; public Vector3 LocalPosition,BoundsCenter; public float Yaw,HalfX,HalfZ; }
    /// <summary>Prepared cell/edge changes for an individual building map.</summary>
    public sealed class TownNavPatch { public int W,H; public float Cell; public int[] BaseSolid,EdgeCells,DressSolid; public byte[] EdgeClear; public bool Any(bool dressing)=>BaseSolid.Length+EdgeCells.Length+(dressing?DressSolid.Length:0)>0; }
    /// <summary>Read-only town geometry given to the NPC routing owner.</summary>
    public sealed class TownNavGeometry
    {
        public string Key; public Vector3 WorldCenter; public float Yaw,Radius,Feather; public Vector3[] StreetNodes,Outline; public Vector3[] WallRuns; public int[] StreetEdges; public Bounds Frame; public TownNavLot[] Lots; public TownNavPatch[] Overlay;
        public int LotCount=>Lots.Length;
        public Vector3 World(Vector3 local)=>WorldCenter+Quaternion.Euler(0,Yaw,0)*local;
    }
    /// <summary>Live instance handles beside a detached numeric town plan. Arrays/lists are observed on the main thread.</summary>
    public interface ITownBoundary { bool Waypoint(Vector3 from,Vector3 goal,out Vector3 point,out bool hold); }
    public sealed class TownNavContext { public TownNavGeometry Plan; public Transform Anchor; public Terrain GroundTerrain; public GameObject[] Buildings; public List<Bounds> DebrisBlockers; public bool HasWalls; public ITownBoundary Boundary; }
    /// <summary>Incrementally prepared navigation component owned by NPCAI.</summary>
    public interface ITownNavWork : IDisposable { bool Ready {get;} bool Failed {get;} float Progress {get;} bool Step(); }
    public interface ITownOverlayWork : ITownNavWork { void Reapply(bool dressing); void FinishPartial(); }
    /// <summary>NPC movement and per-instance overlay service. Town construction only supplies geometry and lifetime.</summary>
    public interface ITownNavigation
    {
        ITownNavWork CreateRouting(TownNavContext site); ITownOverlayWork CreateOverlay(TownNavContext site,bool dressing);
        bool Waypoint(TownNavContext site,GameObject actor,Vector3 from,Vector3 goal,out Vector3 point,out bool approach);
        void Release(TownNavContext site);
        void Forget(Transform building); float Tuning(string name,float fallback);
        int Cells(Transform root,bool roofed,List<Vector3> into,out bool known,out bool baked);
        Transform Root(Vector3 scene); void Actor(GameObject actor);
        void RegisterPoiGate(IModule owner,Func<Transform,bool> gate,IWaypointProvider exits);
        void RemovePoiGate(IModule owner);
    }
}
