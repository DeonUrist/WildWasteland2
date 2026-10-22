using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Vehicle targets of a patrol car (combat enemy, faction target, player drive target); Patrols part of TrafficFactions (Wave A split of Traffic/TrafficFactions.cs).</summary>
    internal static partial class TrafficFactions
    {
        // Cars (ForCar/DriveTarget): the nearest opposed actor within 350 m, else the player whenever hostile (vehicle pursuit has its own
        // detection in TrafficAgent.Goal / the pilot's give-up distance).
        internal static GameObject Target(string faction,Vector3 position)
        {
            var best=NearestOpposed(faction,position);
            if(best!=null)return best;
            return PlayerHostile(faction)&&!PlayerRef.GodMode&&PlayerRef.Player!=null?PlayerRef.Player.gameObject:null;
        }

        internal static bool DriveTarget(GameObject car,out Vector3 pos,out Vector3 vel,out GameObject targetCar)
        {return DriveTarget(car,car!=null?car.GetComponent<PatrolMarker>():null,out pos,out vel,out targetCar);}
        // marker = car's PatrolMarker, cached by the caller
        internal static bool DriveTarget(GameObject car,PatrolMarker marker,out Vector3 pos,out Vector3 vel,out GameObject targetCar)
        {
            pos=vel=Vector3.zero;targetCar=null;
            var target=marker!=null?ForCar(car,marker):null;
            if(target==null)return false;
            if(PlayerRef.Player!=null&&target==PlayerRef.Player.gameObject)return PlayerRef.Target(out pos,out vel,out targetCar);
            pos=target.transform.position;var body=target.GetComponentInParent<Rigidbody>();if(body!=null){vel=body.velocity;targetCar=body.gameObject;}return true;
        }
        internal static GameObject ForCar(GameObject car)
        {
            return ForCar(car,car!=null?car.GetComponent<PatrolMarker>():null);
        }
        internal static GameObject ForCar(GameObject car,PatrolMarker marker)
        {
            if(marker==null)return null;
            var enemy=car.GetComponent<Crew>()?.CombatEnemy;if(enemy!=null)return enemy;
            if(Time.time>=marker.FactionCheck){marker.FactionCheck=Time.time+0.4f;marker.FactionTarget=Target(marker.Faction,car.transform.position);}
            var target=marker.FactionTarget;
            return target!=null&&PlayerRef.Player!=null&&target==PlayerRef.Player.gameObject&&PatrolPoi.Ignored(car)?null:target;
        }
    }
}
