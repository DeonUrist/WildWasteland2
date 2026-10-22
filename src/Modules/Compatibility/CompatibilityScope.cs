using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Provides a distinct resource owner for an optional bridge without registering another gameplay module.</summary>
    internal sealed class CompatibilityScope : ModuleBase
    {
        private readonly string _id;
        /// <summary>Creates a stable child id so failed setup or per-mod disabling cannot remove sibling resources.</summary>
        internal CompatibilityScope(string name) { _id = "Compatibility." + name; }
        /// <summary>Child resource identifier used by scheduler, patches and diagnostic records.</summary>
        public override string Id { get { return _id; } }
        /// <summary>Human-readable scope name.</summary>
        public override string DisplayName { get { return _id; } }
        /// <summary>The containing Compatibility module binds player settings.</summary>
        public override void BindConfig(ModuleConfig cfg) { }
        /// <summary>Concrete bridge code installs its resources under this scope.</summary>
        public override void Init() { }
        /// <summary>Removes only registrations belonging to this optional bridge.</summary>
        public override void Shutdown() { Patches.Unpatch(this); Scheduler.RemoveAll(this); Hooks.RemoveAll(this); ModApi.RemoveAll(this); ModEvents.RemoveAll(this); WorldIndex.RemoveAll(this); }
    }
}
