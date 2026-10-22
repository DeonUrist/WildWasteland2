using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;
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
        public bool IsPart(Transform part) { return VehicleAttachments.IsPart(part); }
        /// <summary>Tests native attachment eligibility through Vehicles mechanical rules.</summary>
        public bool IsFreeAttachment(GameObject part) { return VehicleAttachments.IsFreeAttachment(part); }
        /// <summary>Recognizes a native trailer through Vehicles mechanical rules.</summary>
        public bool IsTrailer(GameObject car) {return TrailerSupport.IsTrailer(car);}
        /// <summary>Reads the actual native suspension values.</summary>
        public bool ApplySuspension(GameObject vehicle,float width,float height,string reason)=>Suspension.Apply(vehicle,width,height,reason);
        /// <summary>Describes the actual fitted suspension for authoring diagnostics.</summary>
        public string SuspensionDescription(GameObject vehicle)=>Suspension.Describe(vehicle);
        /// <summary>Resolves a motorcycle or registered vehicle template through its real owner.</summary>
        public GameObject Template(string name)=>MotorcycleIntegration.IsBody(name)?MotorcycleIntegration.Template():ModVehicles.Template(name);
        /// <summary>Refreshes optional template discovery and shared prefab invalidation.</summary>
        public void RefreshTemplates()=>ModVehicles.Refresh();
        /// <summary>Reads actual fitted suspension width and height.</summary>
        public void ReadSuspension(GameObject car,out float width,out float height) {Suspension.Read(car,out width,out height);}
    }
}
