using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using HutongGames.PlayMaker;
namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class WorldTraffic
    {
        private sealed class NearbyDeparture
        {internal int Role;internal string Name,Faction,Type;internal RoadJob Incoming;internal TrafficBase Source,Destination;internal TrafficVehicle[] Vehicles;}
        private readonly List<NearbyDeparture> _nearDepartures=new List<NearbyDeparture>();
        private float _nextMerchant;private PlayMakerFSM _hornFsm;private GameObject _hornCar;private bool _hornOn;
        /// <summary>Returns the current role count used by traffic merchants.</summary>
        private int RoleCount(int role,string faction=null)=>_journeys.Count(j=>!j.foot&&!j.townParked&&!j.townResident&&(j.merchant?2:j.convoy?1:0)==role&&(faction==null||TrafficFactions.Normalize(j.faction)==faction))
            +_nearDepartures.Count(d=>d.Role==role&&(faction==null||TrafficFactions.Normalize(d.Faction)==faction))
            +_townDepartures.Count(d=>(d.Journey.merchant?2:1)==role&&(!_journeys.Contains(d.Journey)||d.Journey.townResident)&&(faction==null||TrafficFactions.Normalize(d.Journey.faction)==faction))
            +(role==0?_calls.Values.Count(d=>d.Redirect==null&&(faction==null||TrafficFactions.Normalize(d.Faction)==faction)):0);
        /// <summary>Counts travelling groups and pending routes without charging parked settlement shops.</summary>
        private int PopulationCount=>RoleCount(0)+RoleCount(1)+RoleCount(2)+_journeys.Count(j=>j.foot);
        /// <summary>Returns the independent vehicle-patrol allowance for one faction.</summary>
        private static int PatrolLimit(string faction)=>TrafficFactions.Normalize(faction)=="Coyotes"?PatrolsModule.MaxCoyotePatrols.Value:PatrolsModule.MaxPatrols.Value;
        /// <summary>Checks the current merchant type in use condition for traffic merchants.</summary>
        internal static bool MerchantTypeInUse(string type)
        {return MerchantHost.All.Any(h=>h!=null&&h.Data!=null&&Find(h.GetComponent<PatrolMarker>()?.TrafficId)?.townResident!=true&&MerchantCatalog.Selected(h.Data.types)==type)||(_instance!=null&&(_instance._journeys.Any(j=>j.merchant&&!j.townResident&&j.vehicles.Any(v=>MerchantCatalog.Selected(v.merchantTypes)==type))||_instance._nearDepartures.Any(d=>d.Role==2&&d.Type==type)||_instance._townDepartures.Any(d=>d.Journey.merchant&&d.Journey.vehicles.Any(v=>MerchantCatalog.Selected(v.merchantTypes)==type))));}
        /// <summary>Checks the current debug departure condition for traffic merchants.</summary>
        internal static bool DebugDeparture(SpawnGroup group,int role)
        {if(_instance==null||group==null||role<0||role>2||!PatrolsModule.ConvoyEnabled.Value){Core.Log.Warn(null,"[Patrols] " + "Select New arrival or Fury road in Game > World > Enemy Vehicles and select a group before creating a route.");return false;}return _instance.QueueNearby(group,role);}
        /// <summary>Queues nearby for the current traffic merchants.</summary>
        private bool QueueNearby(SpawnGroup group,int role)
        {
            if(role==2&&PatrolsModule.MerchantSpawnChance.Value<=0f){Core.Log.Warn(null,"[Patrols] " + "Route not created: merchant spawn chance is zero.");return false;}
            string faction=TrafficFactions.Normalize(group.Faction);
            int cap=role==2?PatrolsModule.MaxMerchants.Value:role==1?PatrolsModule.MaxConvoys.Value:PatrolLimit(faction);
            int manual=role==2?MerchantHost.All.Count(h=>h!=null&&!Owns(h.GetComponent<PatrolMarker>())):0;
            if(RoleCount(role,role==0?faction:null)+manual>=cap||PopulationCount>=PatrolsModule.TrafficMaxGroups.Value){Core.Log.Warn(null,"[Patrols] " + "Route not created: maximum population reached.");return false;}
            var player=Core.PlayerState.Transform;if(player==null)return false;
            var type=MerchantCatalog.Selected(group.MerchantTypes);
            if(role==2&&MerchantTypeInUse(type)){Core.Log.Warn(null,"[Patrols] " + "A "+MerchantCatalog.Label(type)+" merchant already exists.");return false;}
            var roster=role==2?group.Templates.Select(CarTemplate.Find).Where(t=>t!=null&&t.Available&&CrewFactions.Compatible(t,group.Faction)).OrderBy(t=>UnityEngine.Random.value).Take(1).Select(t=>t.CloneForSpawn()).ToList():EditorStore.ComposeFull(group,role==1,()=>UnityEngine.Random.value);
            if(roster.Count==0)return false;
            if(role==2)foreach(var car in roster){MerchantRuntime.Configure(car,group);car.MerchantTravelling=true;car.ReleaseHandbrake=true;}
            if(TownTravel&&role>0&&QueueTownDeparture(group,role,roster))return true;
            var center=TrafficSpace.World(player.position);float angle=UnityEngine.Random.Range(0f,Mathf.PI*2f);
            var direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));var via=CrossingPoint(role==2);
            var entry=new TrafficBase{apron=OffscreenTrafficPoint(via,-direction)};var exit=new TrafficBase{apron=OffscreenTrafficPoint(via,direction)};
            var bases=TrafficLocations.PatrolBases().Where(b=>TrafficFactions.Normalize(b.faction)==faction).ToArray();
            var sources=bases.Where(b=>TrafficLocations.BaseLoaded(b.id)&&!TrafficLocations.PlayerAt(b)&&TrafficLocations.FlatDistance(b.apron,center)<=PatrolsModule.CampTrafficRange.Value).ToArray();
            if(sources.Length>0)entry=sources[UnityEngine.Random.Range(0,sources.Length)];
            var destinations=bases.Where(b=>b.id!=entry.id&&TrafficLocations.FlatDistance(b.apron,entry.apron)>=300f).ToArray();
            if(destinations.Length>0)exit=destinations[UnityEngine.Random.Range(0,destinations.Length)];
            _nearDepartures.Add(new NearbyDeparture{Role=role,Name=group.Name,Faction=group.Faction,Type=type,Source=entry,Destination=exit,Vehicles=roster.Select(Freeze).ToArray(),Incoming=RoadPlanner.RequestRoute(entry,exit,role!=0||roster.Any(t=>t.IsTruck),via:via)});
            Core.Log.Main(null,"[Patrols] Planning map crossing for "+group.Name+" ("+faction+", "+(role==2?"merchant":role==1?"convoy":"patrol")+"); via "+TrafficLocations.FlatDistance(via,center).ToString("0")+" m from player");return true;
        }
        /// <summary>Advances queued nearby routes for the current traffic merchants.</summary>
        private void PumpNearbyRoutes()
        {
            foreach(var departure in _nearDepartures.ToArray())
            {
                if(!departure.Incoming.Complete)continue;_nearDepartures.Remove(departure);
                int cap=departure.Role==2?PatrolsModule.MaxMerchants.Value:departure.Role==1?PatrolsModule.MaxConvoys.Value:PatrolLimit(departure.Faction);
                if(departure.Role==2&&PatrolsModule.MerchantSpawnChance.Value<=0f)continue;
                if(RoleCount(departure.Role,departure.Role==0?TrafficFactions.Normalize(departure.Faction):null)>=cap||PopulationCount>=PatrolsModule.TrafficMaxGroups.Value)continue;
                if(departure.Incoming.Route==null||!departure.Incoming.Elevation||Cleared(departure.Source.id)||Cleared(departure.Destination.id))
                {Core.Log.Main(null,"[Patrols] Map crossing deferred for "+departure.Name+": no certified terrain route or cleared endpoint");if(Core.Log.L2)Core.Log.Deep(null,"[Patrols] Crossing rejection: "+(departure.Incoming.Rejections??departure.Incoming.Error??"endpoint cleared"));continue;}
                var route=departure.Incoming.Route;
                _journeys.Add(new TrafficJourney{id=Guid.NewGuid().ToString("N"),name=departure.Name,faction=departure.Faction,source=departure.Source.id??"",destination=departure.Destination.id??"",merchant=departure.Role==2,convoy=departure.Role==1,freeRoute=string.IsNullOrEmpty(departure.Source.id),
                    route=route,roadLegs=departure.Incoming.Roads,TerrainRoute=departure.Incoming.Elevation,position=route[0],vehicles=departure.Vehicles,speed=(departure.Role==0?PatrolsModule.TrafficPatrolSpeed.Value:PatrolsModule.TrafficConvoySpeed.Value)/3.6f});
                Core.Log.Main(null,"[Patrols] Map crossing ready: "+departure.Name+"; "+route.Length+" waypoints");
            }
        }
        /// <summary>Populates merchants for the current traffic merchants.</summary>
        private void PopulateMerchants()
        {
            if(_nextMerchant<=0f){NextMerchant();return;}if(Time.time<_nextMerchant)return;
            NextMerchant();
            // WW: F6 [Patrols] Merchants off = the "Max merchants 0" path (no automatic merchant routes; a loaded bus finishes its visit)
            int desired=PatrolsModule.Merchants.Value?MerchantPopulation(PatrolsModule.MaxMerchants.Value,PatrolsModule.MerchantSpawnChance.Value,UnityEngine.Random.value):0;
            int manual=MerchantHost.All.Count(h=>h!=null&&!Owns(h.GetComponent<PatrolMarker>()));
            foreach(var pending in _nearDepartures.Where(d=>d.Role==2).Reverse().ToArray())
            {if(RoleCount(2)+manual<=desired)break;_nearDepartures.Remove(pending);}
            // Removing excess virtual routes is necessary for the fractional roll to average correctly.
            // A physically loaded bus retains its stock and visit until the player leaves.
            foreach(var journey in _journeys.Where(j=>j.merchant&&!j.townResident&&!j.townBound).Reverse().ToArray())
            {if(RoleCount(2)+manual<=desired)break;if(!_live.ContainsKey(journey.id))Remove(journey);}
            var groups=EditorStore.Data.Merchants.Where(g=>g.Chance>0f).ToList();
            while(RoleCount(2)+manual<desired)
            {
                groups.RemoveAll(g=>MerchantTypeInUse(MerchantCatalog.Selected(g.MerchantTypes)));if(groups.Count==0)break;
                float roll=UnityEngine.Random.value*groups.Sum(g=>g.Chance);var group=groups[groups.Count-1];
                foreach(var candidate in groups){roll-=candidate.Chance;if(roll<=0f){group=candidate;break;}}
                groups.Remove(group);QueueNearby(group,2);
            }
        }
        /// <summary>Returns the configured target count of current merchant journeys.</summary>
        internal static int MerchantPopulation(int maximum,float chance,float roll)
        {float expected=Mathf.Clamp(maximum,0,3)*Mathf.Clamp01(chance/100f);int whole=Mathf.FloorToInt(expected);return whole+(roll<expected-whole?1:0);}
        /// <summary>Chooses the next eligible authored merchant role.</summary>
        private void NextMerchant(){_nextMerchant=Time.time+PatrolsModule.MerchantCheckMinutes.Value*60f;}
        /// <summary>Stops merchants for the current traffic merchants.</summary>
        internal void StopMerchants(Vector3 player,float range)
        {foreach(var journey in _journeys)if(journey.merchant&&_live.ContainsKey(journey.id)&&TrafficLocations.FlatDistance(journey.position,player)<=range&&journey.waitSeconds<=0f)journey.waitSeconds=300f;}
        /// <summary>Updates arrival and departure notifications from retained merchant journey state.</summary>
        private void MerchantSignals()
        {
            var player=Core.PlayerState.Transform;if(player==null)return;var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.qKey.wasPressedThisFrame&&(keyboard.leftAltKey.isPressed||keyboard.rightAltKey.isPressed))StopMerchants(TrafficSpace.World(player.position),30f);
            var car=Core.PlayerState.Car;
            if(car!=_hornCar){_hornCar=car;_hornFsm=car!=null?car.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f=>f.FsmName=="INPUT_Horn"):null;_hornOn=false;}
            bool horn=_hornFsm!=null&&_hornFsm.ActiveStateName=="on";
            if(horn&&!_hornOn)StopMerchants(TrafficSpace.World(player.position),100f);_hornOn=horn;
        }
    }
}
