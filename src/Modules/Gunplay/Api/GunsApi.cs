using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Core.Services.Guns adapter (ex Gunplay.Api v1). Kind codes: pistol=0, SMG=1, rifle=2, sniper=3, shotgun=4, crossbow=5.
    /// Consumers: NPC (WeaponRanges, VanillaEvents) and Patrols (HandlesFittedWheelMelee).
    /// </summary>
    internal sealed class GunsApi : IGunsApi
    {
        /// <summary>Contract version 1.</summary>
        public int ContractVersion { get { return 1; } }
        /// <summary>Projectiles replace the vanilla hitscan.</summary>
        public bool ProjectilesEnabled { get { return GunplaySettings.ProjectilesEnabled; } }
        /// <summary>The player's shots are projectiles too.</summary>
        public bool PlayerProjectilesEnabled { get { return GunplaySettings.ProjectilesEnabled && GunplaySettings.PlayerTracers.Value; } }
        /// <summary>The collider stops bullets (solid, or a trigger with a Bodypart FSM).</summary>
        public bool BlocksProjectile(Collider collider) { return collider != null && Tracers.Counts(collider); }
        /// <summary>Falloff range of a kind 0..5.</summary>
        public float EffectiveRange(int kind) { return Tracers.RangeOf((Tracers.Kind)kind); }
        /// <summary>Kind 0..5 of the gun an NPC/turret holds (rifle when unknown).</summary>
        public bool TryGetWeaponKind(GameObject owner, out int kind)
        {
            Tracers.Kind found = Tracers.Kind.Rifle;
            bool result = owner != null && Tracers.GunKindOf(owner, out found);
            kind = result ? (int)found : 2;
            return result;
        }
        /// <summary>Gunplay handles melee on fitted wheels (Patrols fallback stands down).</summary>
        public bool HandlesFittedWheelMelee { get { return GunplaySettings.FittedWheelMeleeActive; } }
    }
}
