using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Loot module entry (W0 skeleton, README 3.1): loot tables and assignment, spawner budget/progress, substitution rules, cargo loot (README 6.9).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Loot] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Loot/, data: config/WildWasteland/Loot/ (D13).
    /// </summary>
    public sealed class LootModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Loot"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Loot"; } }

        /// <summary>No entries yet (Core binds [Loot] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
