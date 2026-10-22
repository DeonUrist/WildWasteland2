using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols part of TrafficCamps: residents of a loaded camp call a patrol (reinforcements) when they fight the player or an opposed faction (Wave A split of Traffic/TrafficCamps.cs).</summary>
    internal static class TrafficCamps
    {
        private static readonly List<Core.CampResidentInfo> Residents=new List<Core.CampResidentInfo>();
        internal sealed class Status : Core.ICampStatus { public Core.CampState At(Vector3 p,float radius)=>Core.ModApi.Get<Core.IWorldLocations>()?.StateAt(p,radius)??Core.CampState.Unknown; }
        /// <summary>Registers native callbacks under the current owning runtime lifecycle.</summary>
        internal static void Install(Core.IModule owner) { }
        /// <summary>Clears retained runtime state at the owning lifecycle boundary.</summary>
        internal static void Reset() { Residents.Clear(); }
        /// <summary>Loads ed for the current traffic camps.</summary>
        internal static bool Loaded(string id)=>Core.ModApi.Get<Core.IWorldDiscovery>()?.Loaded(id)??false;
        /// <summary>Returns the current bind used by traffic camps.</summary>
        internal static void Bind(string id,GameObject actor)=>Core.ModApi.Get<Core.IWorldDiscovery>()?.Bind(id,actor);
        /// <summary>Advances scheduled work under the current save, pause and admission gates.</summary>
        internal static void Tick()
        {
            var provider=Core.ModApi.Get<Core.IWorldDiscovery>();if(provider==null||!PatrolsModule.ConvoyEnabled.Value)return;
            provider.Residents(Residents);
            foreach(var resident in Residents)
            {
                TrafficBase camp;if(!TrafficLocations.Known.TryGetValue(resident.Camp,out camp)||resident.Actor==null||resident.CallRolled||resident.Health<=0||resident.Actor.tag!=camp.faction)continue;
                var target=resident.Target;var player=PlayerRef.Player;var car=PlayerRef.PlayerCar;if(target==null)continue;
                bool playerTarget=player!=null&&(target.transform.IsChildOf(player)||(car!=null&&target.transform.IsChildOf(car.transform)));
                var attacker=target.GetComponentInParent<TrafficActor>();
                if(playerTarget?PlayerRef.GodMode||!TrafficFactions.PlayerHostile(camp.faction):attacker==null||!TrafficFactions.Opposed(camp.faction,attacker.Faction))continue;
                provider.MarkCallRolled(resident.Actor);bool success=UnityEngine.Random.value<.1f;
                if(Core.Log.Verbose)PatrolsModule.Verbose("Patrol call roll: "+resident.Actor.name+" at "+camp.id+" "+(success?"passed":"failed"));
                if(success)WorldTraffic.CallPatrol(camp);
            }
        }
    }
}
