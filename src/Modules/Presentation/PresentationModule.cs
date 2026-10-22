using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Presentation module entry (W0 skeleton, README 3.1): one character pipeline (player, NPCs, female NPCs, animals), traversal, seat poses, weapon poses, camera rig, vehicle skins (README 6.1).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Presentation] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Presentation/, data: config/WildWasteland/Presentation/ (D13).
    /// </summary>
    public sealed class PresentationModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Presentation"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Presentation"; } }

        /// <summary>No entries yet (Core binds [Presentation] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
