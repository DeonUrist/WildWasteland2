using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Town-specific grouping policy; native spawn timing remains with the NPC population owner.</summary>
    internal static class InteriorPopulation
    {
        internal static PopulationArea Area(TownSite s){if(s.PopulationArea==null)s.PopulationArea=new PopulationArea{Key=s.Plan.Key,Anchor=s.Anchor,Center=s.Plan.WorldCenter,Radius=s.Plan.Radius,Ready=()=>s.NextLot>=s.Plan.LotCount,Spawned=actor=>TownPopulation.Created(s,actor)};return s.PopulationArea;}
        internal static void OwnTown(TownSite s,int lot){var api=ModApi.Get<INativePopulation>();if(api==null)return;for(int n=0;n<s.Spawners[lot].Length;n++){var f=s.Spawners[lot][n];if(f==null)continue;bool merchant=s.Plan.Lots[lot].IsMerchant&&s.Plan.Faction==TownFaction.Coyotes&&f.name.StartsWith("CoyotesMerchantSpawn",StringComparison.Ordinal);bool enemy=f.name.StartsWith("EnemySpawn",StringComparison.Ordinal)&&(f.name.IndexOf("boss",StringComparison.OrdinalIgnoreCase)<0||f.name.IndexOf("bodyguard",StringComparison.OrdinalIgnoreCase)>=0);if(!merchant&&!enemy)continue;bool suspend=!merchant&&SpawnerControl.Required.Read(f,"OnOff")==0&&s.PopulationCounts[lot][n]>SpawnerControl.Required.Read(f,"number");api.Own(f,Area(s),suspend,null);}}
        internal static void OwnMarker(PlayMakerFSM f,PoiDressing.Site s){var area=s.Town==null?(s.PopulationArea??(s.PopulationArea=new PopulationArea{Key=s.Type+":"+s.Root.GetInstanceID(),Anchor=s.Root,Center=Vector3.zero,Radius=0,Ready=()=>s.Ready})):Area(s.Town);ModApi.Get<INativePopulation>()?.Own(f,area,true,()=>s.Ready);}
        internal static bool TownReady(TownSite s)=>ModApi.Get<INativePopulation>()?.Ready(Area(s))??true;
        internal static void DiscoverExisting()=>ModApi.Get<INativePopulation>()?.Discover();
        internal static string[] CaptureNames()=>ModApi.Get<INativePopulation>()?.CaptureNames()??new string[0];
        internal static void RestoreNames(string[] names)=>ModApi.Get<INativePopulation>()?.RestoreNames(names);
    }
}
