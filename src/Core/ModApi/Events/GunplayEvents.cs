using UnityEngine;

// Weapon, projectile and damage events (publisher: Gunplay unless noted).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Source of damage.</summary>
    public enum HurtKind { Projectile, Melee, Explosion, Vanilla, Fall, Vehicle, Environment }

    /// <summary>Weapon switch phases.</summary>
    public enum WeaponSwitchStage { Lowering, Switching, Raising, Done }

    /// <summary>Projectile kinds.</summary>
    public enum ProjectileKind { Bullet, Pellet, Bolt, Orb, Lance, Thrown }

    /// <summary>A weapon fired (Gunplay -> NPCAI hearing, HUD, Saves, Presentation).</summary>
    [Broadcast(Authority.Owner)]
    public struct WeaponFired : IModEvent
    {
        /// <summary>Shooter entity.</summary>
        public EntityId Shooter;
        /// <summary>IWeaponCatalog handle (-1 unknown).</summary>
        public int WeaponKey;
        /// <summary>Weapon kind.</summary>
        public WeaponKind Kind;
        /// <summary>Muzzle, world space.</summary>
        public Vector3 OriginWorld;
        /// <summary>Fire direction (unit).</summary>
        public Vector3 Direction;
        /// <summary>Projectiles in the shot.</summary>
        public int Pellets;
        /// <summary>Fired by the player.</summary>
        public bool Player;
        /// <summary>Spread seed (deterministic replay).</summary>
        public int Seed;
    }

    /// <summary>A projectile was created.</summary>
    [Broadcast(Authority.Owner)]
    public struct ProjectileSpawned : IModEvent
    {
        /// <summary>Session-unique projectile id.</summary>
        public long ProjectileId;
        /// <summary>Shooter entity.</summary>
        public EntityId Shooter;
        /// <summary>Projectile kind.</summary>
        public ProjectileKind Kind;
        /// <summary>Origin, world space.</summary>
        public Vector3 OriginWorld;
        /// <summary>Initial velocity, m/s.</summary>
        public Vector3 Velocity;
        /// <summary>Maximum range, metres.</summary>
        public float Range;
    }

    /// <summary>A projectile ended (hit, range, timeout).</summary>
    [Broadcast(Authority.Owner)]
    public struct ProjectileEnded : IModEvent
    {
        /// <summary>Projectile id.</summary>
        public long ProjectileId;
        /// <summary>End position, world space.</summary>
        public Vector3 EndWorld;
        /// <summary>Ended by a hit.</summary>
        public bool Hit;
    }

    /// <summary>A projectile hit something (Gunplay -> HUD, NPCAI, Patrols).</summary>
    [Broadcast(Authority.Host)]
    public struct ProjectileHit : IModEvent
    {
        /// <summary>Projectile id.</summary>
        public long ProjectileId;
        /// <summary>Shooter entity.</summary>
        public EntityId Shooter;
        /// <summary>Hit entity (None for world geometry).</summary>
        public EntityId Target;
        /// <summary>Body part / vehicle part path (may be null).</summary>
        public string Part;
        /// <summary>Hit point, world space.</summary>
        public Vector3 PointWorld;
        /// <summary>Surface normal.</summary>
        public Vector3 Normal;
        /// <summary>Damage applied.</summary>
        public float Damage;
        /// <summary>Headshot.</summary>
        public bool Head;
        /// <summary>Passed through.</summary>
        public bool Penetrated;
    }

    /// <summary>Unified damage event: projectile, melee, explosion or vanilla FSM damage (Gunplay -> HUD, NPCAI, Quests). Replaces INpcApi.Hurt and Services.NpcHurt.</summary>
    [Broadcast(Authority.Host)]
    public struct Hurt : IModEvent
    {
        /// <summary>Victim entity.</summary>
        public EntityId Victim;
        /// <summary>Attacker entity (None when unknown).</summary>
        public EntityId Attacker;
        /// <summary>Damage amount.</summary>
        public float Amount;
        /// <summary>Damage source.</summary>
        public HurtKind Kind;
        /// <summary>Body part (may be null).</summary>
        public string BodyPart;
        /// <summary>Hit point, world space.</summary>
        public Vector3 PointWorld;
        /// <summary>Weapon handle (-1 unknown).</summary>
        public int WeaponKey;
        /// <summary>Headshot.</summary>
        public bool Head;
        /// <summary>The hit killed the victim.</summary>
        public bool Fatal;
    }

    /// <summary>A vehicle part was hit (Gunplay/Patrols -> Patrols, HUD). Id-based successor of Services.VehiclePartHit.</summary>
    [Broadcast(Authority.Host)]
    public struct VehiclePartHit : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Part path under the vehicle root.</summary>
        public string PartPath;
        /// <summary>Attacker entity.</summary>
        public EntityId Attacker;
        /// <summary>Melee (false = projectile).</summary>
        public bool Melee;
        /// <summary>Part condition before, 0..1 (-1 unknown).</summary>
        public float ConditionBefore;
        /// <summary>Part condition after, 0..1 (-1 unknown).</summary>
        public float ConditionAfter;
        /// <summary>The part is a wheel.</summary>
        public bool Wheel;
    }

    /// <summary>A wheel came off a vehicle.</summary>
    [Broadcast(Authority.Host)]
    public struct WheelPoppedOff : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Wheel part path.</summary>
        public string WheelPath;
        /// <summary>Impulse applied.</summary>
        public Vector3 Impulse;
    }

    /// <summary>A crossbow bolt stuck into something.</summary>
    [Broadcast(Authority.Host)]
    public struct BoltLanded : IModEvent
    {
        /// <summary>Bolt projectile id.</summary>
        public long ProjectileId;
        /// <summary>Target entity (None = world).</summary>
        public EntityId Target;
        /// <summary>Bone/part path (may be null).</summary>
        public string BonePath;
        /// <summary>Bolt position, world space.</summary>
        public Vector3 PositionWorld;
        /// <summary>Bolt rotation (world).</summary>
        public Quaternion Rotation;
        /// <summary>Seconds it stays.</summary>
        public float Ttl;
    }

    /// <summary>Ammo counts of a weapon changed (Gunplay -> HUD, Presentation).</summary>
    [Broadcast(Authority.Owner)]
    public struct AmmoChanged : IModEvent
    {
        /// <summary>Weapon handle.</summary>
        public int WeaponKey;
        /// <summary>Save-stable item id.</summary>
        public string ItemId;
        /// <summary>Caliber.</summary>
        public string Caliber;
        /// <summary>Rounds in the gun.</summary>
        public int InGun;
        /// <summary>Matching rounds carried.</summary>
        public int Reserve;
    }

    /// <summary>A magazine was inserted/removed.</summary>
    [Broadcast(Authority.Owner)]
    public struct MagazineChanged : IModEvent
    {
        /// <summary>Weapon handle.</summary>
        public int WeaponKey;
        /// <summary>Save-stable item id.</summary>
        public string ItemId;
        /// <summary>Rounds in the new magazine.</summary>
        public int Rounds;
        /// <summary>Magazine capacity.</summary>
        public int Capacity;
    }

    /// <summary>Aim-down-sights state changed.</summary>
    [Broadcast(Authority.Owner)]
    public struct AdsChanged : IModEvent
    {
        /// <summary>Aiming down sights.</summary>
        public bool Aiming;
        /// <summary>Zoom factor.</summary>
        public float Zoom;
        /// <summary>Native scope view.</summary>
        public bool Scoped;
    }

    /// <summary>Weapon switch progressed (Gunplay -> Presentation lowering pose, HUD).</summary>
    [Broadcast(Authority.Owner)]
    public struct WeaponSwitchPhase : IModEvent
    {
        /// <summary>Stage.</summary>
        public WeaponSwitchStage Stage;
        /// <summary>Lowering weight 0..1.</summary>
        public float Weight;
        /// <summary>Previous slot.</summary>
        public int FromSlot;
        /// <summary>Next slot.</summary>
        public int ToSlot;
    }

    /// <summary>Unloading a weapon started.</summary>
    [Broadcast(Authority.Owner)]
    public struct UnloadStarted : IModEvent
    {
        /// <summary>Weapon handle.</summary>
        public int WeaponKey;
        /// <summary>Item id.</summary>
        public string ItemId;
        /// <summary>Rounds to unload.</summary>
        public int Rounds;
    }

    /// <summary>Unloading a weapon finished.</summary>
    [Broadcast(Authority.Owner)]
    public struct UnloadFinished : IModEvent
    {
        /// <summary>Weapon handle.</summary>
        public int WeaponKey;
        /// <summary>Item id.</summary>
        public string ItemId;
        /// <summary>Rounds unloaded.</summary>
        public int Rounds;
        /// <summary>False when cancelled.</summary>
        public bool Completed;
    }

    /// <summary>Overflow ammo was dropped as an item.</summary>
    [Broadcast(Authority.Owner)]
    public struct AmmoDropped : IModEvent
    {
        /// <summary>Caliber.</summary>
        public string Caliber;
        /// <summary>Rounds.</summary>
        public int Quantity;
        /// <summary>Dropped item entity.</summary>
        public EntityId Item;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>Overflow armor plates were dropped as an item.</summary>
    [Broadcast(Authority.Owner)]
    public struct ArmorPlateDropped : IModEvent
    {
        /// <summary>Plates.</summary>
        public int Quantity;
        /// <summary>Dropped item entity.</summary>
        public EntityId Item;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }
}
