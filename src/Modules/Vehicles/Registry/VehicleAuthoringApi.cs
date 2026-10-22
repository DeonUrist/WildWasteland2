using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Patrols;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Provides actual native mechanical authoring queries without exposing implementation types.</summary>
    internal sealed class VehicleAuthoringApi : IVehicleAuthoring
    {
        /// <summary>Recognizes the actual motorcycle chassis identity.</summary>
        public bool IsMotorcycleBody(string name) {return MotorcycleIntegration.IsBody(name);}
        /// <summary>Finds the actual native attached trailer.</summary>
        public GameObject AttachedTrailer(GameObject car) {return TrailerSupport.Attached(car);}
        /// <summary>Recognizes the native trailer frame.</summary>
        public bool IsTrailer(GameObject car) {return TrailerSupport.IsTrailer(car);}
        /// <summary>Reads the actual native suspension values.</summary>
        public void ReadSuspension(GameObject car,out float width,out float height) {Suspension.Read(car,out width,out height);}
    }
}
