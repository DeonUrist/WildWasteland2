using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Guns
{
    /// <summary>Existing Gunplay rig queries forwarding to the shared native player state.</summary>
    [Obsolete("refactor: use Core.PlayerState")]
    internal static class PlayerRig
    {
        /// <summary>Core owns scene invalidation; no local rig references remain.</summary>
        internal static void Reset() { }
        /// <summary>Native first-person camera.</summary>
        internal static Transform Camera { get { return PlayerState.PlayerCamera; } }
        /// <summary>Native first-person weapons arm.</summary>
        internal static Transform WeaponsArm { get { return PlayerState.WeaponsArm; } }
        /// <summary>Native first-person weapon models parent.</summary>
        internal static Transform Parent { get { return PlayerState.WeaponsParent; } }
    }
}
