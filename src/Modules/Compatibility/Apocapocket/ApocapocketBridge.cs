using WildWasteland.Core;
using WildWasteland.Modules.HUD;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The Apocapocket bridge: resolves the mod once (<see cref="ApocapocketInventory"/>, used by the HUD) and provides
    /// <see cref="IInventorySlots"/> (<see cref="ApocapocketSlots"/>) while the mod is present. The player-body equipment reader keeps
    /// its own runtime custody part (EquipmentInventory.Apocapocket.cs, also Apocainventory) until Presentation consumes the query.
    /// </summary>
    internal static class ApocapocketBridge
    {
        /// <summary>The bridge's resource scope (log owner); null while stopped.</summary>
        internal static IModule Scope { get; private set; }

        /// <summary>Installs the reader under <paramref name="scope"/> and provides the slot query when the mod resolved.</summary>
        internal static void Start(IModule scope)
        {
            Scope = scope;
            LogicalEquipmentInventory.Clear();
            ModApi.Provide<ILogicalEquipmentInventory>(scope,new LogicalEquipmentInventory());
            ApocapocketInventory.Install(scope);
            if (ApocapocketInventory.Ready) { ModApi.Provide<IInventorySlots>(scope, new ApocapocketSlots()); ModApi.Provide<IPocketHud>(scope, new PocketHudAdapter()); }
        }

        /// <summary>Withdraws the query and forgets the reader.</summary>
        internal static void Shutdown()
        {
            if (Scope != null) { ModApi.Withdraw<ILogicalEquipmentInventory>(Scope); ModApi.Withdraw<IInventorySlots>(Scope); ModApi.Withdraw<IPocketHud>(Scope); }
            ApocapocketInventory.Clear(); LogicalEquipmentInventory.Clear(); Scope = null;
        }
    }
}
