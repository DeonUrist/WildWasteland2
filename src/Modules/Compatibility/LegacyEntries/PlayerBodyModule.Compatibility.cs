using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A). Empty since B17: the ChaseCamera type adapter and the
    /// Weapons Pack shadows live in the Compatibility module. Wave C deletes it.
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Empty (B17): the ChaseCamera adapter and the Weapons Pack shadows belong to their Compatibility bridges.</summary>
        partial void InitCompatibility() { }

        /// <summary>Empty (B17): the Compatibility bridges unbind ChaseCamera / restore Weapons Pack shadows when the player body stops.</summary>
        partial void ShutdownCompatibility() { }
    }
}
