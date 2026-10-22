using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class WorldTraffic
    {
        private readonly Dictionary<string,Departure> _calls=new Dictionary<string,Departure>();
        private int CallReservations=>_calls.Values.Count(c=>c.Redirect==null);
        private PlayMakerFSM _callClock;
        private float CallHour()
        {
            if(_callClock==null)_callClock=Core.FsmUtil.Find(Core.GameRefs.GameManager,"DayTime");
            if(_callClock==null||!_callClock.Fsm.Initialized)return -1f;
            var day=_callClock.FsmVariables.FindFsmInt("Day");var hour=_callClock.FsmVariables.FindFsmFloat("Time");
            return day!=null&&hour!=null?day.Value*24f+hour.Value:-1f;
        }
        internal static void CallPatrol(TrafficBase camp)
        {if(_instance!=null&&PatrolsModule.ConvoyEnabled.Value)_instance.QueuePatrolCall(camp);}
        private void QueuePatrolCall(TrafficBase camp)
        {
            float hour=CallHour();
            if(camp.cleared||hour<0f)return;
            if(_calls.ContainsKey(camp.id)||_journeys.Any(j=>j.reinforcement&&j.destination==camp.id)
                ||(camp.patrolCallAt>=0f&&hour-camp.patrolCallAt<24f))
            {PatrolsModule.Verbose("Patrol call suppressed: "+camp.id+" (pending, active or 24 game-hour cooldown)");return;}
            var redirect=_journeys.Where(j=>!j.convoy&&!j.merchant&&!j.foot&&!j.reinforcement&&string.IsNullOrEmpty(j.battle)&&j.faction==camp.faction
                &&!_calls.Values.Any(c=>c.Redirect==j)&&(!_live.TryGetValue(j.id,out var live)||!live.Building)
                &&TrafficLocations.FlatDistance(j.position,camp.apron)<=PatrolsModule.CampTrafficRange.Value)
                .OrderBy(j=>TrafficLocations.FlatDistance(j.position,camp.apron)).FirstOrDefault();
            TrafficVehicle[] vehicles=null;TrafficBase source;TrafficBase[] alternatives=null;string name=redirect!=null?redirect.name:"";
            if(redirect!=null)source=new TrafficBase{id=redirect.source,apron=redirect.position};
            else
            {
                if(RoleCount(0)+CallReservations>=PatrolsModule.MaxPatrols.Value||RoleCount(0)+RoleCount(1)+RoleCount(2)+CallReservations>=PatrolsModule.TrafficMaxGroups.Value)
                {PatrolsModule.Verbose("Patrol call skipped: "+camp.id+" (population full, no nearby patrol)");return;}
                var group=Convoy.TrafficGroup(false,camp.faction);if(group==null)return;name=group.Name;
                var roster=EditorStore.ComposeFull(group,false,()=>UnityEngine.Random.value);if(roster.Count==0||roster.Any(t=>!t.Available))return;
                vehicles=roster.Select(Freeze).ToArray();
                var nearby=TrafficLocations.Known.Values.Where(c=>c.id!=camp.id&&!c.cleared&&c.faction==camp.faction&&!TrafficLocations.PlayerAt(c)
                    &&TrafficCamps.Loaded(c.id)&&TrafficLocations.FlatDistance(c.apron,camp.apron)>=300f
                    &&TrafficLocations.FlatDistance(c.apron,camp.apron)<=PatrolsModule.CampTrafficRange.Value).ToArray();
                alternatives=nearby;
                float angle=UnityEngine.Random.Range(0f,Mathf.PI*2f);
                source=nearby.Length>0?nearby[UnityEngine.Random.Range(0,nearby.Length)]:new TrafficBase{id="",apron=OffscreenTrafficPoint(camp.apron,new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle)))};
            }
            _calls[camp.id]=new Departure{Source=source,Destination=camp,Name=name,Faction=camp.faction,Vehicles=vehicles,Redirect=redirect,Job=RoadPlanner.RequestRoute(source,camp,vehicles!=null&&vehicles.Any(v=>v.truck),false,alternatives)};
            PatrolsModule.Verbose("Patrol call planning: "+camp.id+" from "+(redirect!=null?"nearby patrol "+redirect.id:string.IsNullOrEmpty(source.id)?"outside map":source.id));
        }
        private void PumpPatrolCalls()
        {
            foreach(var pair in _calls.ToArray())
            {
                var pending=pair.Value;if(!pending.Job.Complete)continue;_calls.Remove(pair.Key);
                pending.Source=pending.Job.Source;
                float hour=CallHour();
                if(!TrafficLocations.Known.TryGetValue(pair.Key,out var camp)||!PatrolsModule.ConvoyEnabled.Value||camp.cleared||hour<0f||pending.Job.Route==null)continue;
                var journey=pending.Redirect;
                if(journey!=null)
                {
                    if(!_journeys.Contains(journey)||!string.IsNullOrEmpty(journey.battle))continue;
                    if(TrafficLocations.FlatDistance(journey.position,pending.Job.Route[0])>60f)
                    {pending.Source=new TrafficBase{id=journey.source,apron=journey.position};pending.Job=RoadPlanner.RequestRoute(pending.Source,camp,false);_calls[pair.Key]=pending;continue;}
                }
                else
                {
                    if(RoleCount(0)>=PatrolsModule.MaxPatrols.Value||_journeys.Count>=PatrolsModule.TrafficMaxGroups.Value||Cleared(pending.Source.id)
                        ||(!string.IsNullOrEmpty(pending.Source.id)&&TrafficLocations.PlayerAt(pending.Source)))continue;
                    journey=new TrafficJourney{id=Guid.NewGuid().ToString("N"),name=pending.Name,faction=pending.Faction,source=pending.Source.id,vehicles=pending.Vehicles,
                        position=pending.Job.Route[0],speed=PatrolsModule.TrafficPatrolSpeed.Value/3.6f};_journeys.Add(journey);
                }
                journey.destination=camp.id;journey.reinforcement=true;journey.freeRoute=true;journey.roaming=false;journey.arrived=false;
                journey.route=pending.Job.Route;journey.roadLegs=pending.Job.Roads;journey.TerrainRoute=pending.Job.Elevation;journey.leg=1;
                journey.destinationSeconds=journey.unseenSeconds=0f;_routeJobs.Remove(journey.id);_routeAttempts.Remove(journey.id);
                camp.patrolCallAt=hour;
                PatrolsModule.Verbose("Patrol called: "+journey.id+" -> "+camp.id+"; next call after "+(hour+24f).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" game hours");
            }
        }
    }
}
