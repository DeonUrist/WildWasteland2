using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Compatibility module entry (W0 skeleton, README 3.1): one bridge per third-party mod, implementing Core contracts (README 6.12).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Compatibility] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Compatibility/, data: config/WildWasteland/Compatibility/ (D13).
    /// </summary>
    public sealed class CompatibilityModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Compatibility"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Compatibility"; } }

        /// <summary>No entries yet (Core binds [Compatibility] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
