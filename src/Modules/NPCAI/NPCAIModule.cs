using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// NPC AI module entry (W0 skeleton, README 3.1): senses, brain, navigation, animal behaviours, population rules, followers, seated combat (README 6.2).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [NPCAI] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/NPCAI/, data: config/WildWasteland/NPCAI/ (D13).
    /// </summary>
    public sealed class NPCAIModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "NPCAI"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "NPC AI"; } }

        /// <summary>No entries yet (Core binds [NPCAI] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
