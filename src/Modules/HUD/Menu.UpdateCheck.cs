using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Menu presentation forwards release discovery to the Core version service.</summary>
    internal sealed partial class Menu
    {
        /// <summary>Newest release page, when the session check finds a newer version.</summary>
        private static string _updateUrl { get { return ReleaseInfo.UpdateUrl; } }
        /// <summary>Runs the shared session release check through the existing HUD coroutine.</summary>
        internal static System.Collections.IEnumerator CheckUpdates() { return ReleaseInfo.CheckUpdates(); }
    }
}
