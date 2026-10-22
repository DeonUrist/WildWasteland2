using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Town-specific grouping policy; native spawn timing remains with the NPC population owner.</summary>
    internal static class InteriorPopulation
    {
        /// <summary>Creates the stable location-wide population readiness and arrival context.</summary>
        internal static PopulationArea Area(TownSite s){if(s.PopulationArea==null)s.PopulationArea=new PopulationArea{Key=s.Plan.Key,Anchor=s.Anchor,Center=s.Plan.WorldCenter,Radius=s.Plan.Radius,Ready=()=>s.NextLot>=s.Plan.LotCount,Spawned=actor=>TownPopulation.Created(s,actor)};return s.PopulationArea;}
        /// <summary>Registers town native groups with the NPC population service using the committed budgets.</summary>
        internal static void OwnTown(TownSite s,int lot){var api=ModApi.Get<INativePopulation>();if(api==null)return;for(int n=0;n<s.Spawners[lot].Length;n++){var f=s.Spawners[lot][n];if(f==null)continue;bool merchant=s.Plan.Lots[lot].IsMerchant&&s.Plan.Faction==TownFaction.Coyotes&&f.name.StartsWith("CoyotesMerchantSpawn",StringComparison.Ordinal);bool enemy=f.name.StartsWith("EnemySpawn",StringComparison.Ordinal)&&(f.name.IndexOf("boss",StringComparison.OrdinalIgnoreCase)<0||f.name.IndexOf("bodyguard",StringComparison.OrdinalIgnoreCase)>=0);if(!merchant&&!enemy)continue;bool suspend=!merchant&&SpawnerControl.Required.Read(f,"OnOff")==0&&s.PopulationCounts[lot][n]>SpawnerControl.Required.Read(f,"number");api.Own(f,Area(s),suspend,null);}}
        /// <summary>Registers a marker group behind its location and dressing readiness gates.</summary>
        internal static void OwnMarker(PlayMakerFSM f,PoiDressing.Site s){var area=s.Town==null?(s.PopulationArea??(s.PopulationArea=new PopulationArea{Key=s.Type+":"+s.Root.GetInstanceID(),Anchor=s.Root,Center=Vector3.zero,Radius=0,Ready=()=>s.Ready})):Area(s.Town);ModApi.Get<INativePopulation>()?.Own(f,area,true,()=>s.Ready);}
        /// <summary>Queries whether arrived town groups have finished their admitted spawns.</summary>
        internal static bool TownReady(TownSite s)=>ModApi.Get<INativePopulation>()?.Ready(Area(s))??true;
        /// <summary>Offers already loaded roots to the owning discovery provider.</summary>
        internal static void DiscoverExisting()=>ModApi.Get<INativePopulation>()?.Discover();
        /// <summary>Captures names of native population members that persist in ES3.</summary>
        internal static string[] CaptureNames()=>ModApi.Get<INativePopulation>()?.CaptureNames()??new string[0];
        /// <summary>Restores the native population member-name record through its owning service.</summary>
        internal static void RestoreNames(string[] names)=>ModApi.Get<INativePopulation>()?.RestoreNames(names);
    }
}
