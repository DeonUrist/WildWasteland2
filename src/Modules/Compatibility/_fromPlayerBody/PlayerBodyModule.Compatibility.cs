using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): the third-party bridges that ride on the player body -
    /// ChaseCamera's Apocaplayer type adapter and the Weapons Pack first-person shadow patch. Target: Compatibility (B17).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Init: ChaseCamera adapter (+ its discovery job) and the optional Weapons Pack patch.</summary>
        partial void InitCompatibility()
        {
            ChaseCameraCompat.Init(this);
        }

        /// <summary>Shutdown (first): Weapons Pack shadows removed, ChaseCamera adapter cleared.</summary>
        partial void ShutdownCompatibility()
        {
            ChaseCameraCompat.Clear();
        }
    }
}
