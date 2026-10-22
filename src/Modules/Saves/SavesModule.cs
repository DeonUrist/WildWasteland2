using WildWasteland.Core;

namespace WildWasteland.Modules.Saves
{
    /// <summary>
    /// Saves module entry (W0 skeleton, README 3.1): named slots, journaled saves, autosave, backups/recovery, save library (README 6.14, D6).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Saves] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Saves/, data: config/WildWasteland/Saves/ (D13).
    /// </summary>
    public sealed partial class SavesModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Saves"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Saves"; } }

        /// <summary>No entries yet (Core binds [Saves] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
