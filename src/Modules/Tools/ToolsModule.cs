using WildWasteland.Core;

namespace WildWasteland.Modules.Tools
{
    /// <summary>
    /// Tools module entry (W0 skeleton, README 3.1): navigation, POI, patrols and towns editors, LOD export (README 6.10).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Tools] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Tools/, data: config/WildWasteland/Tools/ (D13).
    /// </summary>
    public sealed class ToolsModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Tools"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Tools"; } }

        /// <summary>No entries yet (Core binds [Tools] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
