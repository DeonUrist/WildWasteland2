using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class EncounterCar : MonoBehaviour
    {
        internal string Event;
        internal bool Convoy;
        /// <summary>Releases this component runtime registrations and owned native state.</summary>
        private void OnDestroy(){EventBudget.Cars.Remove(this);}
        internal bool Building;
        /// <summary>Creates or reuses the runtime component and binds the supplied native objects.</summary>
        internal static void Attach(GameObject car,string id,bool convoy,bool building=false)
        {var tag=car.GetComponent<EncounterCar>()??car.AddComponent<EncounterCar>();tag.Event=id;tag.Convoy=convoy;tag.Building=building;if(!EventBudget.Cars.Contains(tag))EventBudget.Cars.Add(tag);}
    }
    internal static class EventBudget
    {
        internal static readonly List<EncounterCar> Cars=new List<EncounterCar>();
        private static readonly Dictionary<string,int> Pending=new Dictionary<string,int>();
        /// <summary>Clears retained runtime state at the owning lifecycle boundary.</summary>
        internal static void Reset(){Pending.Clear();}
        /// <summary>Checks whether the current value represents convoy for the current event budget.</summary>
        internal static bool IsConvoy(GameObject car)
        {if(car==null)return false;var a=WorldTraffic.AgentOf(car);var journey=a!=null?WorldTraffic.Find(a.Journey):null;return journey!=null?journey.convoy||journey.merchant:car.GetComponent<EncounterCar>()?.Convoy==true;}
        /// <summary>Checks current encounter budget before admitting additional materialization.</summary>
        internal static int Available(string id)
        {
            if(!PatrolsModule.CarSpawnLimit.Value)return int.MaxValue;
            int total=0;var player=Core.PlayerState.Transform;
            foreach(var tag in Cars)
            {
                if(tag==null||tag.Building)continue;
                var marker=tag.GetComponent<PatrolMarker>();if(marker!=null&&(marker.PlayerEntered||marker.Exploded))continue;
                if(player!=null&&(tag.transform.position-player.position).sqrMagnitude>PatrolsModule.TrafficUnloadRange.Value*PatrolsModule.TrafficUnloadRange.Value)continue;
                total++;
            }
            foreach(var pair in Pending)total+=pair.Value;
            return Math.Max(0,PatrolsModule.EventVehicleLimit.Value-total);
        }
        /// <summary>Checks the current reserve condition for event budget.</summary>
        internal static bool Reserve(string id)
        {if(Available(id)<=0)return false;Pending.TryGetValue(id,out int n);Pending[id]=n+1;return true;}
        /// <summary>Releases the explicit hold so the current owner can proceed.</summary>
        internal static void Release(string id)
        {if(!Pending.TryGetValue(id,out int n))return;if(n<=1)Pending.Remove(id);else Pending[id]=n-1;}
    }
}
