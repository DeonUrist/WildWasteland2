using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Owns third-party bridges and their patch lifetime. The SigmaMod early FSM guard is installed here before
    /// Core optimization starts; B17 supplies the remaining bridge integrations (README 6.12).
    /// </summary>
    public sealed class CompatibilityModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Compatibility"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Compatibility"; } }

        /// <summary>No entries yet (Core binds [Compatibility] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Installs the early FSM guard only when SigmaMod is present.</summary>
        public override void Init() { SigmaFsmStates.Install(this); }

        /// <summary>Registers the detected third-party vehicle owner before Core optimization starts.</summary>
        public override void ProvideApis()
        {
            var query = EngineSimVehicleOwnership.Create();
            var ownership = ModApi.Get<IVehicleOwnership>();
            if (query != null && ownership != null) ownership.Register(this, query);
        }

        /// <summary>Releases the guard's runtime state; Core removes its owner-scoped patch immediately afterward.</summary>
        public override void Shutdown() { SigmaFsmStates.Clear(); }
    }
}
