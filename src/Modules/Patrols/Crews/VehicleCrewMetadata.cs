using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Reads current native crew and marker identity for explicit vehicle visual operations.</summary>
    internal sealed class VehicleCrewMetadata : IVehicleCrewMetadata, IVehicleCrewWreck
    {
        /// <summary>Stops traffic control before Vehicles disables native wreck components.</summary>
        public void Disable(GameObject car)
        {
            if(car==null)return;
            car.GetComponent<Pilot>()?.Detach();var crew=car.GetComponent<Crew>();if(crew!=null)crew.enabled=false;
        }
        /// <summary>Projects the actual traffic marker faction for vehicle presentation.</summary>
        public string Faction(GameObject car)=>car!=null?car.GetComponent<PatrolMarker>()?.Faction:null;
        /// <summary>Reports a real traffic crew controller on the supplied vehicle.</summary>
        public bool HasCrewController(GameObject actor)=>actor!=null&&actor.GetComponent<Crew>()!=null;
    }
}
