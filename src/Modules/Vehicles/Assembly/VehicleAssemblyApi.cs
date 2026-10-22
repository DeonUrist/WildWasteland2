using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Exposes the actual Vehicles mechanical preparation and exact-instance consumption pipeline.</summary>
    internal sealed class VehicleAssemblyApi : IVehicleAssembly
    {
        /// <summary>Queues a structural variant in Vehicles' sliced preparation job.</summary>
        public IPreparedVehicle Require(VehicleStructure structure,string bumper,bool trailer) { return VehicleBuildCache.Require(structure,bumper,trailer); }
        /// <summary>Consumes only a ready entry created by this mechanical provider.</summary>
        public GameObject Consume(IPreparedVehicle prepared,Vector3 position,Quaternion rotation) { var entry=prepared as VehicleBuildCache.Entry; return entry!=null&&entry.Ready&&!entry.Failed ? VehicleBuildCache.Spawn(entry,position,rotation) : null; }
        /// <summary>Drops dormant structures at the submitting world's reset boundary.</summary>
        public void Reset() { VehicleBuildCache.Reset(); }
        public bool Warming { get { return VehicleBuildCache.Warming; } }
        public float LoadFraction { get { return VehicleBuildCache.LoadFraction; } }
    }
}
