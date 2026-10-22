using UnityEngine;

// Weapon contracts (core-modapi.md 8, README P7 and guns-hud.md 7 design note): one weapon catalog, stats with an ordered modifier
// pipeline (future attachments addon), live weapon state. Provider: Gunplay. Consumers: HUD, NPCAI, Presentation, Compatibility,
// Quests, Loot. Interfaces only. FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs

    /// <summary>Mirror of Gunplay.Api v1. Superseded by <see cref="IWeaponCatalog"/>/<see cref="IWeaponStats"/> for new code.</summary>
    public interface IGunsApi
    {
        /// <summary>Contract version.</summary>
        int ContractVersion { get; }
        /// <summary>Gunplay projectiles/tracers replace the vanilla hitscan (NPC vanilla observers stand down).</summary>
        bool ProjectilesEnabled { get; }
        /// <summary>Player shots use Gunplay projectiles.</summary>
        bool PlayerProjectilesEnabled { get; }
        /// <summary>True when the collider stops projectiles.</summary>
        bool BlocksProjectile(Collider collider);
        /// <summary>Effective range of a weapon kind (0..5), metres.</summary>
        float EffectiveRange(int kind);
        /// <summary>Weapon kind (0..5) held by an NPC/turret.</summary>
        bool TryGetWeaponKind(GameObject owner, out int kind);
        /// <summary>Guns handles melee hits on fitted wheels (Patrols' own melee-wheel code stands down; ex Apocapatrol.MeleeWheels.RaiderHandles).</summary>
        bool HandlesFittedWheelMelee { get; }
    }

    // ------------------------------------------------------------------ new (Wave B Gunplay)

    /// <summary>Weapon kinds. The first six values match the old int kinds (0..5) of IGunsApi/INpcApi.Shot.</summary>
    public enum WeaponKind { Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow, Melee, Thrown, Launcher, Other }

    /// <summary>Stats readable through <see cref="IWeaponStats"/>; modifiers address them.</summary>
    public enum WeaponStat
    {
        BaseDamage, DamageMultiplier, HeadshotMultiplier, Range, FullDamageFraction, ProjectileSpeed, Pellets, PelletSpread,
        Accuracy, Penetration, MagazineCapacity, FireInterval, ReloadTime, ZoomMax, NoiseRadius, VehicleDamage, WheelDamage
    }

    /// <summary>Catalog entry of one weapon key (value type, no Unity references).</summary>
    public struct WeaponInfo
    {
        /// <summary>Normalized weapon key ("akms", "redmark_m11_scoped", "ww_weaponspack_chainsaw").</summary>
        public string Key;
        /// <summary>Display name.</summary>
        public string DisplayName;
        /// <summary>Weapon kind.</summary>
        public WeaponKind Kind;
        /// <summary>Caliber / ammo item key ("7.62x39"); null for melee.</summary>
        public string Caliber;
        /// <summary>Base damage per hit/pellet before modifiers.</summary>
        public float BaseDamage;
        /// <summary>Base range, metres.</summary>
        public float Range;
        /// <summary>Pellets per shot (1 for single projectiles).</summary>
        public int Pellets;
        /// <summary>Magazine capacity (0 = none/unknown).</summary>
        public int Magazine;
        /// <summary>Muzzle offset local to the weapon model.</summary>
        public Vector3 MuzzleOffset;
        /// <summary>Has a native scope.</summary>
        public bool Scoped;
    }

    /// <summary>One weapon catalog replacing the six name classifiers of Guns/NPC/CleanHUD/PlayerBody (README 6.3).</summary>
    public interface IWeaponCatalog
    {
        /// <summary>Integer handle of a normalized key (stable for the session); -1 when unknown.</summary>
        int Resolve(string weaponKey);
        /// <summary>Handle of the weapon held/represented by an item, model or NPC object.</summary>
        bool TryResolve(GameObject itemModelOrOwner, out int weaponKey);
        /// <summary>Normalized key of a raw object/item name ("AKMS(Clone)" -> "akms").</summary>
        string NormalizeKey(string objectName);
        /// <summary>Catalog entry of a handle.</summary>
        bool TryGet(int weaponKey, out WeaponInfo info);
        /// <summary>Key string of a handle (null when unknown).</summary>
        string KeyOf(int weaponKey);
        /// <summary>Kind of a handle (<see cref="WeaponKind.Other"/> when unknown).</summary>
        WeaponKind KindOf(int weaponKey);
        /// <summary>Registers or replaces a weapon (Compatibility: Weapons Pack). Returns its handle.</summary>
        int Register(IModule owner, WeaponInfo info);
        /// <summary>Number of known weapons (handles are 0..Count-1).</summary>
        int Count { get; }
    }

    /// <summary>Stat modifier registered by an attachments addon or a compatibility bridge.</summary>
    public interface IWeaponStatModifier
    {
        /// <summary>Returns the modified value of <paramref name="stat"/>; <paramref name="itemId"/> is the save-stable item name (null for NPC/base queries).</summary>
        float Modify(int weaponKey, string itemId, WeaponStat stat, float value);
    }

    /// <summary>Effective weapon stats = catalog base + config + ordered modifiers (README 6.3, guns-hud.md 7).</summary>
    public interface IWeaponStats
    {
        /// <summary>Effective stat of a weapon (no item-specific modifiers).</summary>
        float Get(int weaponKey, WeaponStat stat);
        /// <summary>Effective stat of one item instance (item-specific modifiers apply).</summary>
        float Get(int weaponKey, string itemId, WeaponStat stat);
        /// <summary>Stat before any modifier.</summary>
        float Base(int weaponKey, WeaponStat stat);
        /// <summary>Adds a modifier (lower order runs first); returns an id. Removed automatically when <paramref name="owner"/> shuts down.</summary>
        int RegisterModifier(IModule owner, IWeaponStatModifier modifier, int order);
        /// <summary>Removes a modifier.</summary>
        void RemoveModifier(int id);
        /// <summary>Call when a modifier's output changed (attachment added/removed) so caches refresh.</summary>
        void Invalidate(string itemId);
        /// <summary>Increments on every modifier/config change; consumers cache per revision.</summary>
        int Revision { get; }
    }

    /// <summary>Live state of the player's drawn weapon and NPC weapons.</summary>
    public interface IWeaponState
    {
        /// <summary>The player holds a weapon.</summary>
        bool HasWeapon { get; }
        /// <summary>Handle of the player's drawn weapon (-1 = none).</summary>
        int WeaponKey { get; }
        /// <summary>Save-stable item id (native item name) of the drawn weapon.</summary>
        string ItemId { get; }
        /// <summary>Rounds in the gun.</summary>
        int AmmoInGun { get; }
        /// <summary>Matching rounds carried.</summary>
        int Reserve { get; }
        /// <summary>The weapon must be cocked before the next shot.</summary>
        bool NeedsCocking { get; }
        /// <summary>A reload is running.</summary>
        bool Reloading { get; }
        /// <summary>Aiming down sights / ironsights.</summary>
        bool Aiming { get; }
        /// <summary>Native scope view active.</summary>
        bool Scoped { get; }
        /// <summary>Current zoom factor (1 = none).</summary>
        float ZoomFactor { get; }
        /// <summary>Weapon switch in progress (lowering/raising).</summary>
        bool Switching { get; }
        /// <summary>Weapon handle held by an NPC/turret.</summary>
        bool TryGetNpcWeapon(GameObject npc, out int weaponKey);
    }
}
