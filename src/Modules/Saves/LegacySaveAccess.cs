using System;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Deprecated save-state access retained for consumer packages that have not yet switched to ISaves.</summary>
    [Obsolete("refactor: use ISaves state through ModApi")]
    internal static class SaveSystem
    {
        /// <summary>Reports an active journal transaction using the original narrower admission semantics.</summary>
        internal static bool Busy { get { return Modules.Saves.SaveSystem.Busy; } }
    }

    /// <summary>Deprecated utility entry points retained until NPC/Patrols consumers move to their own persistence adapters.</summary>
    [Obsolete("refactor: use canonical Core player queries and feature-owned save identity validation")]
    internal static class SaveRuntime
    {
        /// <summary>Validates the existing WW2 native/named slot file identity without changing accepted formats.</summary>
        internal static bool IsSaveFile(string file) { return Modules.Saves.SaveRuntime.IsSaveFile(file); }
        /// <summary>Computes player distance, returning infinity when the player is absent.</summary>
        internal static float PlayerDistance(Vector3 position, Transform player) { return player != null ? Vector3.Distance(position, player.position) : float.PositiveInfinity; }
    }
}
