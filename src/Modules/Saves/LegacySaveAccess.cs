using System;
using UnityEngine;

// Old Core-namespace save framework facades (B04). The framework moved to WildWasteland.Modules.Saves; these forwarders keep
// not-yet-converted consumers (HUD drawing partials, Clean HUD loading screen, Compatibility, NPCAI, Patrols, POI, Vehicles)
// compiling with unchanged behaviour. Every member is [Obsolete]; consumers move to ISaves / SavePresentation events and
// Wave C deletes this file (docs/refactor/requests/B04.md lists the remaining callers).
namespace WildWasteland.Core
{
    /// <summary>Deprecated save-state access for consumer packages that have not yet switched to ISaves.</summary>
    [Obsolete("refactor: use ModApi.Get<ISaves>() (Busy/Phase/Kind/ActiveFile)")]
    internal static class SaveSystem
    {
        /// <summary>Reports an active journal transaction using the original narrower admission semantics.</summary>
        internal static bool Busy { get { return Modules.Saves.SaveSystem.Busy; } }
        /// <summary>True while the Saves module owns the native save adapters.</summary>
        internal static bool Installed { get { return Modules.Saves.SaveSystem.Installed; } }
        /// <summary>Interrupted-write recovery message that blocks loading, or null.</summary>
        internal static string RecoveryError { get { return Modules.Saves.SaveSystem.RecoveryError; } }
        /// <summary>True while the running save is an autosave (timed or exit).</summary>
        internal static bool AutomaticSaving { get { return Modules.Saves.SaveSystem.AutomaticSaving; } }
        /// <summary>True while a local singleplayer world is loaded.</summary>
        internal static bool LocalWorld { get { return Modules.Saves.SaveSystem.LocalWorld; } }
        /// <summary>Display description of a published save (name/kind/date) in the unchanged WW-SAVE-1 format.</summary>
        internal static Modules.Saves.SaveSystem.SaveInfo Info(string file) { return Modules.Saves.SaveSystem.Info(file); }
    }

    /// <summary>Deprecated native-runtime entry points; the HUD part (SaveRuntime.HUD.cs, B10) still declares its overlay here.</summary>
    [Obsolete("refactor: use ISaves / SavePresentation events and Core PlayerState")]
    internal static partial class SaveRuntime
    {
        /// <summary>Validates the existing WW2 native/named slot file identity without changing accepted formats.</summary>
        internal static bool IsSaveFile(string file) { return Modules.Saves.SaveRuntime.IsSaveFile(file); }
        /// <summary>Computes player distance, returning infinity when the player is absent.</summary>
        internal static float PlayerDistance(Vector3 position, Transform player) { return Modules.Saves.SaveRuntime.PlayerDistance(position, player); }
        /// <summary>Newest published save file, or null.</summary>
        internal static string Latest() { return Modules.Saves.SaveRuntime.Latest(); }
    }

    /// <summary>Deprecated serializer state for the HUD loading screen.</summary>
    [Obsolete("refactor: use ISaves.Phase == SavePhase.Saving / SavePresentation events")]
    internal static class SinglePlayerSaving
    {
        /// <summary>True while the offline save freezes the world and serializes.</summary>
        internal static bool Serializing { get { return Modules.Saves.SinglePlayerSaving.Serializing; } }
        /// <summary>Loads a published snapshot through the normal Saves admission path.</summary>
        internal static void Load(string file) { Modules.Saves.SinglePlayerSaving.Load(file); }
    }

}
