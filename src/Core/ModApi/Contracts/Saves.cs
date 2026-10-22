// Saves contracts (core-modapi.md 8, README 6.14): save state for HUD/Patrols/POI/Compatibility. Events in Events/SavesEvents.cs.
// Provider: Saves. FROZEN after W0.
namespace WildWasteland.Core
{
    /// <summary>Kind of a save operation.</summary>
    public enum SaveKind { Manual, Auto, Exit, Quick }

    /// <summary>Phase of the save framework shown by HUD presentation.</summary>
    public enum SavePhase { Idle, Saving, Loading, Recovering }   // refs-ok: enum values / field names, not module types

    /// <summary>Save framework state (named slots, journaled saves, autosave + combat deferral).</summary>
    public interface ISaves
    {
        /// <summary>A save or load transaction is running.</summary>
        bool Busy { get; }
        /// <summary>Current phase.</summary>
        SavePhase Phase { get; }
        /// <summary>Kind of the running save (meaningful while Phase = Saving).</summary>
        SaveKind Kind { get; }
        /// <summary>Progress 0..1 of the running phase.</summary>
        float Progress { get; }
        /// <summary>Progress text for loading/saving screens.</summary>
        string ProgressText { get; }
        /// <summary>Seconds until the next autosave (negative = autosave off).</summary>
        float AutosaveDueIn { get; }
        /// <summary>Autosave is being deferred because of combat.</summary>
        bool DeferredByCombat { get; }
        /// <summary>Active save file name ("WW_save-....es3" or "SaveGameN.es3"); null in menus.</summary>
        string ActiveFile { get; }
        /// <summary>Display name of the active save.</summary>
        string ActiveName { get; }
        /// <summary>True when saving is allowed now (in play, not in combat-forbidden state).</summary>
        bool CanSaveNow { get; }
        /// <summary>Asks for an autosave soon (e.g. after a convoy event); ignored when autosave is off.</summary>
        void RequestAutosaveSoon(IModule owner, string reason);
    }
}
