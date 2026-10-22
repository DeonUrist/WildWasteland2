using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Ported callers' naming facade; Core owns native save registration and composite transactions.</summary>
    [Obsolete("refactor: use Core.ItemSpawner registration and transaction methods")]
    internal static class Register
    {
        /// <summary>Forwards scene reset to the shared registration service.</summary>
        internal static void Invalidate() { ItemSpawner.ResetRegistrations(); }
        /// <summary>Defers publication until the composite vehicle build is complete.</summary>
        internal static void Defer(GameObject root) { ItemSpawner.Defer(root); }
        /// <summary>Checks whether a vehicle build is still absent from native saved lists.</summary>
        internal static bool Pending(GameObject root) { return ItemSpawner.Pending(root); }
        /// <summary>Discards registrations belonging to a failed build.</summary>
        internal static void Discard(GameObject root) { ItemSpawner.Discard(root); }
        /// <summary>Publishes all surviving deferred objects in admission order.</summary>
        internal static int Commit(GameObject root) { return ItemSpawner.Commit(root); }
        /// <summary>Uses the shared increment-before-naming counter rule.</summary>
        internal static void Name(GameObject value, string prefab) { ItemSpawner.Name(value, prefab); }
        /// <summary>Removes a destroyed vehicle and its children from native saved lists.</summary>
        internal static void RemoveTree(GameObject root) { ItemSpawner.RemoveTree(root); }
        /// <summary>Registers an existing named item or defers it under its composite owner.</summary>
        internal static void Add(GameObject value, bool vehicle, GameObject owner = null) { ItemSpawner.AddExisting(value, vehicle, owner); }
    }
}
