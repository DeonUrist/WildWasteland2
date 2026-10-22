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
        public int ContractVersion { get { return 1; } }
        public bool ProjectilesEnabled { get { return GunplaySettings.ProjectilesEnabled; } }
        public bool PlayerProjectilesEnabled { get { return GunplaySettings.ProjectilesEnabled && GunplaySettings.PlayerTracers.Value; } }
        public bool BlocksProjectile(Collider collider) { return collider != null && Tracers.Counts(collider); }
        public float EffectiveRange(int kind) { return Tracers.RangeOf((Tracers.Kind)kind); }
        public bool TryGetWeaponKind(GameObject owner, out int kind)
        {
            Tracers.Kind found = Tracers.Kind.Rifle;
            bool result = owner != null && Tracers.GunKindOf(owner, out found);
            kind = result ? (int)found : 2;
            return result;
        }
        public bool HandlesFittedWheelMelee { get { return GunplaySettings.FittedWheelMeleeActive; } }
    }
}
