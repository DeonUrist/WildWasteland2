using WildWasteland.Core;

namespace WildWasteland.Modules.Debug
{
    /// <summary>
    /// Debug & tracing module entry (W0 skeleton, README 3.1): log level 0/1/2, performance tracing, debug overlays and dump tools (README 5).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Debug] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Debug/, data: config/WildWasteland/Debug/ (D13).
    /// </summary>
    public sealed class DebugModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Debug"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Debug & tracing"; } }

        /// <summary>No entries yet (Core binds [Debug] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
