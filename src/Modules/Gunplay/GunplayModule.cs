using WildWasteland.Core;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Gunplay module entry (W0 skeleton, README 3.1): weapon catalog and stats, ballistics, projectiles, melee, ammo, hit pipeline (README 6.3).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Gunplay] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Gunplay/, data: config/WildWasteland/Gunplay/ (D13).
    /// </summary>
    public sealed class GunplayModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Gunplay"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Gunplay"; } }

        /// <summary>No entries yet (Core binds [Gunplay] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Nothing to start yet.</summary>
        public override void Init() { }
    }
}
