using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Faction actors (TrafficActor tagging of crews and walkers, foot targets, nearest opposed actor); NPCAI part of TrafficFactions (Wave A split of Patrols Traffic/TrafficFactions.cs).</summary>
    internal static partial class TrafficFactions
    {
        internal static readonly List<TrafficActor> Actors=new List<TrafficActor>();

        internal static void Actor(GameObject npc,string faction)
        {
            if(npc==null)return;faction=Normalize(faction);npc.tag=faction;
            var actor=npc.GetComponent<TrafficActor>()??npc.AddComponent<TrafficActor>();actor.Faction=faction;
            if(npc.transform.parent==null)SimpleBanditBridge.Target(npc,FootTarget(faction,npc.transform.position));   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        }
        internal static void Crew(GameObject car,PatrolMarker marker)   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        {
            Actor(marker.Driver,marker.Faction);Actor(marker.Passenger,marker.Faction);
            foreach(var rider in marker.Riders)Actor(rider,marker.Faction);
            var battle=car.GetComponent<BattleCar>();if(battle!=null)foreach(var actor in car.GetComponentsInChildren<TrafficActor>(true)){actor.Battle=battle.Battle;actor.Side=battle.Side;}   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        }
        internal static void Dismount(GameObject npc,GameObject car)
        {
            Actor(npc,car!=null?car.GetComponent<PatrolMarker>()?.Faction:"Scrapyard");   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
            var battle=car!=null?car.GetComponent<BattleCar>():null;var actor=npc!=null?npc.GetComponent<TrafficActor>():null;   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
            if(battle!=null&&actor!=null){actor.Battle=battle.Battle;actor.Side=battle.Side;}
        }

        /// <summary>On-foot crew / foot patrols (simple bandits): faction targets as above, but the player is only offered as a goal while hostile and within FootTargetRange; the NPC module then decides whether the bandit actually knows about him (senses or fought-memory).</summary>
        internal static GameObject FootTarget(string faction,Vector3 position)
        {
            var best=NearestOpposed(faction,position);
            if(best!=null)return best;
            var player=Core.PlayerState.Transform;if(player==null||Core.PlayerState.GodMode||!PlayerHostile(faction))return null;
            float range=PatrolsModule.FootTargetRange!=null?PatrolsModule.FootTargetRange.Value:350f;
            return (player.position-position).sqrMagnitude<=range*range?player.gameObject:null;
        }
        private static GameObject NearestOpposed(string faction,Vector3 position)
        {
            GameObject best=null;float distance=350f*350f;
            for(int i=0;i<Actors.Count;i++)
            {
                var actor=Actors[i];
                if(actor==null||!Opposed(faction,actor.Faction)||(actor.Health!=null&&actor.Health.Value<=0f))continue;
                float d=(actor.transform.position-position).sqrMagnitude;if(d>=distance)continue;
                distance=d;best=actor.gameObject;
            }
            return best;
        }
    }
}
