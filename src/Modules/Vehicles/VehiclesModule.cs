using WildWasteland.Core;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Vehicles module entry (W0 skeleton, README 3.1): vehicle factory and build cache, motorcycle, War Rig, trailers, ignition and cockpit controls (README 6.6).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Vehicles] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Vehicles/, data: config/WildWasteland/Vehicles/ (D13).
    /// </summary>
    public sealed class VehiclesModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Vehicles"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Vehicles"; } }

        /// <summary>No entries yet (Core binds [Vehicles] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
