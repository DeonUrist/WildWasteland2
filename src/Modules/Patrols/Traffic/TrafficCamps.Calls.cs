using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols part of TrafficCamps: residents of a loaded camp call a patrol (reinforcements) when they fight the player or an opposed faction (Wave A split of Traffic/TrafficCamps.cs).</summary>
    internal static partial class TrafficCamps
    {
        /// <summary>Rolls the 10 % reinforcement call of each living camp resident that targets the player or an opposed faction (once per resident).</summary>
        static partial void RollPatrolCalls(string id, TrafficBase camp)
        {
            if(PatrolsModule.ConvoyEnabled.Value)
                foreach(var resident in Sites[id].Residents)
                {
                    if(resident==null||resident.CallRolled||resident.Health==null||resident.Health.Value<=0f||resident.tag!=camp.faction)continue;
                    var target=resident.Target!=null?resident.Target.Value:null;var player=PlayerRef.Player;var car=PlayerRef.PlayerCar;
                    if(target==null)continue;
                    bool playerTarget=player!=null&&(target.transform.IsChildOf(player)||(car!=null&&target.transform.IsChildOf(car.transform)));
                    var attacker=target.GetComponentInParent<TrafficActor>();
                    if(playerTarget?PlayerRef.GodMode||!TrafficFactions.PlayerHostile(camp.faction):attacker==null||!TrafficFactions.Opposed(camp.faction,attacker.Faction))continue;
                    resident.CallRolled=true;bool success=UnityEngine.Random.value<.1f;
                    if(Core.Log.Verbose)PatrolsModule.Verbose("Patrol call roll: "+resident.name+" at "+id+" "+(success?"passed":"failed"));
                    if(success)WorldTraffic.CallPatrol(camp);
                }
        }
    }
}
