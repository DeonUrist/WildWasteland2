using WildWasteland.Core;
using UnityEngine;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Vehicles module entry (W0 skeleton, README 3.1): vehicle factory and build cache, motorcycle, War Rig, trailers, ignition and cockpit controls (README 6.6).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Vehicles] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Vehicles/, data: config/WildWasteland/Vehicles/ (D13).
    /// </summary>
    public sealed class VehiclesModule : ModuleBase, IVehicleOwnerQuery
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Vehicles"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Vehicles"; } }

        /// <summary>No entries yet (Core binds [Vehicles] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }

        /// <summary>Registers ownership of constructed vehicle bodies through the shared Core registry.</summary>
        public override void ProvideApis()
        {
            var ownership = ModApi.Get<IVehicleOwnership>();
            if (ownership != null) ownership.Register(this, this);
        }

        /// <summary>Recognizes vehicle-owned rig, motorcycle and suspension components on the queried root.</summary>
        public bool Owns(GameObject vehicle)
        {
            if (vehicle == null) return false;
            return vehicle.GetComponent<Patrols.WarRig.WarRigUnit>() != null
                || vehicle.GetComponent<Patrols.Motorcycles.MotorcycleBalance>() != null
                || vehicle.GetComponent<Patrols.MotorcycleRearAccess>() != null
                || vehicle.GetComponent<Patrols.SuspensionTune>() != null;
        }
    }
}
