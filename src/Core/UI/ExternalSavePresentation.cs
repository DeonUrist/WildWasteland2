using System;
namespace WildWasteland.Core
{
    /// <summary>HUD-owned presentation for a standalone save operation when takeover is unavailable.</summary>
    public interface IExternalSavePresentation
    {
        /// <summary>Shows the existing save screen.</summary>
        void Begin();
        /// <summary>Ends the external screen without changing native save state.</summary>
        void End();
        /// <summary>Draws naming controls; true retains the external plugin's original UI.</summary>
        bool DrawName(ref string text, float openedAt, ref bool focus, Action confirm, Action cancel);
    }
}
