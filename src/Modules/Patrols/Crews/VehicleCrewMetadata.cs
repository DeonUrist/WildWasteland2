using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Reads current native crew and marker identity for explicit vehicle visual operations.</summary>
    internal sealed class VehicleCrewMetadata : IVehicleCrewMetadata
    {
        public string Faction(GameObject car)=>car!=null?car.GetComponent<PatrolMarker>()?.Faction:null;
        public bool HasCrewController(GameObject actor)=>actor!=null&&actor.GetComponent<Crew>()!=null;
    }
}
