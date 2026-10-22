using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Copies town planning geometry into detached NPC navigation input.</summary>
    internal static class TownNavSnapshot
    {
        /// <summary>Builds or refreshes the detached live navigation context for the town.</summary>
        internal static TownNavContext Of(TownSite site)
        { if(site.NavContext==null){var p=site.Plan;var polygon=TownPerimeter.Outline(p);var runs=new List<Vector3>();foreach(var r in polygon.Runs){runs.Add(r.A);runs.Add(r.B);}var lots=new TownNavLot[p.LotCount];for(int i=0;i<lots.Length;i++){var l=p.Lots[i];lots[i]=new TownNavLot{Prefab=l.Prefab,IsParking=l.IsParking,Skipped=l.Skipped,HasEntry=l.HasEntry,EntryNode=l.EntryNode,Street=l.Street,LocalPosition=l.LocalPosition,BoundsCenter=l.BoundsCenter,Yaw=l.Yaw,HalfX=l.HalfX,HalfZ=l.HalfZ};}TownNavPatch[] patches=null;if(p.Overlay!=null){patches=new TownNavPatch[p.Overlay.Lots.Length];for(int i=0;i<patches.Length;i++){var q=p.Overlay.Lots[i];if(q!=null)patches[i]=new TownNavPatch{W=q.W,H=q.H,Cell=q.Cell,BaseSolid=q.BaseSolid,EdgeCells=q.EdgeCells,DressSolid=q.DressSolid,EdgeClear=q.EdgeClear};}}site.NavContext=new TownNavContext{Plan=new TownNavGeometry{Key=p.Key,WorldCenter=p.WorldCenter,Yaw=p.Yaw,Radius=p.Radius,Feather=p.Feather,StreetNodes=(Vector3[])p.StreetNodes.Clone(),StreetEdges=(int[])p.StreetEdges.Clone(),Lots=lots,Outline=(Vector3[])polygon.Vertices.Clone(),WallRuns=runs.ToArray(),Frame=TownPerimeter.Frame(p),Overlay=patches},Anchor=site.Anchor,GroundTerrain=site.GroundTerrain,Buildings=site.Buildings,DebrisBlockers=site.DebrisBlockers};}site.NavContext.Anchor=site.Anchor;site.NavContext.GroundTerrain=site.GroundTerrain;site.NavContext.Buildings=site.Buildings;site.NavContext.DebrisBlockers=site.DebrisBlockers;site.NavContext.HasWalls=site.Walls!=null;site.NavContext.Boundary=site.Walls?.WalkerRoutes;for(int i=0;i<site.Plan.LotCount;i++)site.NavContext.Plan.Lots[i].Skipped=site.Plan.Lots[i].Skipped;return site.NavContext; }
    }
}
