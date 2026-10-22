using System;
using BepInEx.Configuration;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Old GunsModule statics still read by other modules (Presentation WeaponTransition.Pose, NPCAI bosses page, HUD crossbow tooltip,
    /// Compatibility Apocaspawner damage). They forward to the Gunplay entry; Wave C deletes them once the consumers use ModAPI.
    /// </summary>
    public sealed partial class GunsModule
    {
        /// <summary>Forwarder of [Gunplay] SmoothWeaponSwitch.</summary>
        [Obsolete("refactor: use ModApi IWeaponState / WeaponSwitchPhase event")]
        internal static ConfigEntry<bool> SmoothWeaponSwitch { get { return GunplaySettings.SmoothWeaponSwitch; } }

        /// <summary>Forwarder of [Gunplay] MonsterProjectilesHitWalls.</summary>
        [Obsolete("refactor: Gunplay owns the orb setting (its own F7 page)")]
        internal static ConfigEntry<bool> OrbsHitWalls { get { return GunplaySettings.OrbsHitWalls; } }

        /// <summary>Forwarder of [Gunplay / Ballistics] PlayerGuns.</summary>
        [Obsolete("refactor: use ModApi IGunsApi.PlayerProjectilesEnabled")]
        internal static ConfigEntry<bool> PlayerTracers { get { return GunplaySettings.PlayerTracers; } }

        /// <summary>Forwarder: projectiles on while Gunplay runs.</summary>
        [Obsolete("refactor: use ModApi IGunsApi.ProjectilesEnabled")]
        internal static bool ProjectilesEnabled { get { return GunplaySettings.ProjectilesEnabled; } }
    }
}
