using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Publisher of the Gunplay events (README 6.3 single hit pipeline, core-modapi 9): WeaponFired, ProjectileSpawned/Ended/Hit, Hurt,
    /// VehiclePartHit, WheelPoppedOff, BoltLanded, ammo/unload/ADS/switch events. Every method first checks
    /// <see cref="ModEvents.HasListeners{T}"/> so nothing (entity ids, world positions) is computed while nobody listens. Positions are
    /// world space (<see cref="Origin.World"/>), objects are <see cref="EntityId"/>s. The old service calls (INpcApi.Shot/Hurt,
    /// IHitFeedback, Services.VehiclePartHit) stay where they were until their consumers switch (see requests/B09.md).
    /// </summary>
    internal static class CombatEvents
    {
        static long _nextProjectile = 1;

        /// <summary>A new session-unique projectile id (0 is never used).</summary>
        internal static long NextProjectileId() { return _nextProjectile++; }

        /// <summary>WeaponFired: one trigger pull (all pellets) of the player or an NPC.</summary>
        internal static void Fired(GameObject shooter, int weaponKey, WeaponKind kind, Vector3 origin, Vector3 direction, int pellets, bool player)
        {
            if (!ModEvents.HasListeners<WeaponFired>()) return;
            var e = new WeaponFired
            {
                Shooter = player ? Entities.Player : Entities.Of(shooter), WeaponKey = weaponKey, Kind = kind,
                OriginWorld = Origin.World(origin), Direction = direction, Pellets = pellets, Player = player, Seed = Random.Range(int.MinValue, int.MaxValue),
            };
            ModEvents.Publish(ref e);
        }

        /// <summary>ProjectileSpawned: one bullet/pellet/bolt entered flight.</summary>
        internal static void Spawned(long id, GameObject shooter, ProjectileKind kind, Vector3 origin, Vector3 velocity, float range)
        {
            if (!ModEvents.HasListeners<ProjectileSpawned>()) return;
            var e = new ProjectileSpawned { ProjectileId = id, Shooter = Entities.Of(shooter), Kind = kind, OriginWorld = Origin.World(origin), Velocity = velocity, Range = range };
            ModEvents.Publish(ref e);
        }

        /// <summary>ProjectileEnded: a projectile stopped (hit, out of range, dropped).</summary>
        internal static void Ended(long id, Vector3 end, bool hit)
        {
            if (id == 0 || !ModEvents.HasListeners<ProjectileEnded>()) return;
            var e = new ProjectileEnded { ProjectileId = id, EndWorld = Origin.World(end), Hit = hit };
            ModEvents.Publish(ref e);
        }

        /// <summary>ProjectileHit: a projectile hit a target object or world geometry (target null).</summary>
        internal static void ProjectileHit(long id, GameObject shooter, GameObject target, string part, Vector3 point, Vector3 normal, float damage, bool head, bool penetrated)
        {
            if (!ModEvents.HasListeners<ProjectileHit>()) return;
            var e = new ProjectileHit
            {
                ProjectileId = id, Shooter = Entities.Of(shooter), Target = target != null ? Entities.Of(target) : EntityId.None, Part = part,
                PointWorld = Origin.World(point), Normal = normal, Damage = damage, Head = head, Penetrated = penetrated,
            };
            ModEvents.Publish(ref e);
        }

        /// <summary>Hurt: a creature took damage (projectile, melee or a vanilla FSM hit).</summary>
        internal static void Hurt(GameObject victim, GameObject attacker, float amount, HurtKind kind, string bodyPart, Vector3 point, int weaponKey, bool head, bool fatal)
        {
            if (victim == null || !ModEvents.HasListeners<Hurt>()) return;
            var e = new Hurt
            {
                Victim = Entities.Of(victim), Attacker = attacker != null ? Entities.Of(attacker) : EntityId.None, Amount = amount, Kind = kind,
                BodyPart = bodyPart, PointWorld = Origin.World(point), WeaponKey = weaponKey, Head = head, Fatal = fatal,
            };
            ModEvents.Publish(ref e);
        }

        /// <summary>VehiclePartHit: a vehicle part (under its car root) was hit by a projectile or melee.</summary>
        internal static void PartHit(Transform part, GameObject attacker, bool melee, float before, float after, bool wheel)
        {
            if (part == null || !ModEvents.HasListeners<VehiclePartHit>()) return;
            var root = part.root;
            var e = new VehiclePartHit
            {
                Vehicle = Entities.Of(root.gameObject), PartPath = PathUnder(part, root), Attacker = attacker != null ? Entities.Of(attacker) : EntityId.None,
                Melee = melee, ConditionBefore = before >= 0f ? before / 100f : -1f, ConditionAfter = after >= 0f ? after / 100f : -1f, Wheel = wheel,
            };
            ModEvents.Publish(ref e);
        }

        /// <summary>WheelPoppedOff: a wheel left its car with an impulse.</summary>
        internal static void WheelOff(Transform wheel, Transform vehicleRoot, Vector3 impulse)
        {
            if (wheel == null || !ModEvents.HasListeners<WheelPoppedOff>()) return;
            var e = new WheelPoppedOff { Vehicle = vehicleRoot != null ? Entities.Of(vehicleRoot.gameObject) : EntityId.None, WheelPath = PathUnder(wheel, vehicleRoot), Impulse = impulse };
            ModEvents.Publish(ref e);
        }

        /// <summary>BoltLanded: a crossbow bolt stuck into something (or ricocheted when <paramref name="target"/> is null).</summary>
        internal static void BoltLanded(long id, Transform target, string bonePath, Transform bolt, float ttl)
        {
            if (bolt == null || !ModEvents.HasListeners<BoltLanded>()) return;
            var e = new BoltLanded
            {
                ProjectileId = id, Target = target != null ? Entities.Of(target.root.gameObject) : EntityId.None, BonePath = bonePath,
                PositionWorld = Origin.World(bolt.position), Rotation = bolt.rotation, Ttl = ttl,
            };
            ModEvents.Publish(ref e);
        }

        /// <summary>AmmoDropped / ArmorPlateDropped: overflow or unload leftovers became world items.</summary>
        internal static void Dropped(string caliber, int quantity, GameObject firstItem, Vector3 position)
        {
            if (caliber == "armorplate")
            {
                if (!ModEvents.HasListeners<ArmorPlateDropped>()) return;
                var a = new ArmorPlateDropped { Quantity = quantity, Item = Entities.Of(firstItem), WorldPos = Origin.World(position) };
                ModEvents.Publish(ref a);
                return;
            }
            if (!ModEvents.HasListeners<AmmoDropped>()) return;
            var e = new AmmoDropped { Caliber = caliber, Quantity = quantity, Item = Entities.Of(firstItem), WorldPos = Origin.World(position) };
            ModEvents.Publish(ref e);
        }

        /// <summary>UnloadStarted: the hold-to-unload sequence began.</summary>
        internal static void UnloadStarted(string weaponModel, string itemId, int rounds)
        {
            if (!ModEvents.HasListeners<UnloadStarted>()) return;
            var e = new UnloadStarted { WeaponKey = WeaponCatalog.Instance.Handle(weaponModel), ItemId = itemId, Rounds = rounds };
            ModEvents.Publish(ref e);
        }

        /// <summary>UnloadFinished: the sequence ended (completed or aborted).</summary>
        internal static void UnloadFinished(string weaponModel, string itemId, int rounds, bool completed)
        {
            if (!ModEvents.HasListeners<UnloadFinished>()) return;
            var e = new UnloadFinished { WeaponKey = WeaponCatalog.Instance.Handle(weaponModel), ItemId = itemId, Rounds = rounds, Completed = completed };
            ModEvents.Publish(ref e);
        }

        /// <summary>MagazineChanged: a physical gun was drawn and got its own magazine from the ledger.</summary>
        internal static void Magazine(string weaponModel, string itemId, int rounds)
        {
            if (!ModEvents.HasListeners<MagazineChanged>()) return;
            int h = WeaponCatalog.Instance.Handle(weaponModel);
            var e = new MagazineChanged { WeaponKey = h, ItemId = itemId, Rounds = rounds, Capacity = (int)WeaponStats.Instance.Get(h, itemId, WeaponStat.MagazineCapacity) };
            ModEvents.Publish(ref e);
        }

        /// <summary>AdsChanged: aiming down sights started/stopped or the wheel zoom target changed.</summary>
        internal static void Ads(bool aiming, float zoom, bool scoped)
        {
            if (!ModEvents.HasListeners<AdsChanged>()) return;
            var e = new AdsChanged { Aiming = aiming, Zoom = zoom, Scoped = scoped };
            ModEvents.Publish(ref e);
        }

        /// <summary>WeaponSwitchPhase: the smooth weapon switch progressed.</summary>
        internal static void SwitchPhase(WeaponSwitchStage stage, float weight)
        {
            if (!ModEvents.HasListeners<WeaponSwitchPhase>()) return;
            var e = new WeaponSwitchPhase { Stage = stage, Weight = weight, FromSlot = -1, ToSlot = -1 };
            ModEvents.Publish(ref e);
        }

        /// <summary>"a/b/c" path of <paramref name="t"/> below <paramref name="root"/> (its own name when it is the root or not under it).</summary>
        static string PathUnder(Transform t, Transform root)
        {
            if (t == null) return null;
            if (root == null || t == root || !t.IsChildOf(root)) return t.name;
            string path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
