namespace WildWasteland.Core
{
    /// <summary>Installs the always-on native fixes in a single explicit startup and shutdown order.</summary>
    internal static class Fixes
    {
        /// <summary>Installs each independent fix once; optimization-owned fixes retain their feature lifecycle.</summary>
        internal static void Install()
        {
            HealthStacks.Install(); ColorGrading.Install(); ITweenGuard.Install(); QuietFsm.Install();
            InventorySupplies.Install(); VehicleLoadPlacement.Install(); PauseCursorFix.Install();
        }
        /// <summary>Releases fix subscriptions, patches, pending placement and native prefab references.</summary>
        internal static void Remove()
        {
            PauseCursorFix.Remove(); VehicleLoadPlacement.Remove(); InventorySupplies.Remove();
            QuietFsm.Remove(); ITweenGuard.Remove(); ColorGrading.Remove(); HealthStacks.Remove();
        }
    }

    /// <summary>Registration ownership for a kernel fix; it is not a user-configurable module.</summary>
    internal sealed class FixOwner : ModuleBase
    {
        readonly string _id;
        /// <summary>Creates a unique Core fix namespace for patches, hooks and scheduler cleanup.</summary>
        internal FixOwner(string name) { _id = "Core.Fixes." + name; }
        /// <summary>Stable ownership key used by the existing registries.</summary>
        public override string Id { get { return _id; } }
        /// <summary>Diagnostic name of the kernel fix.</summary>
        public override string DisplayName { get { return _id; } }
        /// <summary>Kernel fixes expose no independent module settings.</summary>
        public override void BindConfig(ModuleConfig cfg) { }
        /// <summary>The explicit fix registry owns installation.</summary>
        public override void Init() { }
        /// <summary>Removes only this fix's registrations, leaving Core and modules untouched.</summary>
        internal void Remove() { Scheduler.RemoveAll(this); Hooks.RemoveAll(this); Patches.Unpatch(this); }
    }
}
