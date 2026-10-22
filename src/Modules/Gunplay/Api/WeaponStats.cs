using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Effective weapon stats (README 6.3 attachments-ready, guns-hud.md 7 design note; provider of <see cref="IWeaponStats"/>):
    /// catalog base + Gunplay config + an ordered list of <see cref="IWeaponStatModifier"/>s. Gunplay's own stat reads go through
    /// <see cref="Modify"/>, which returns the value unchanged while no modifier is registered (the shipped game registers none), so
    /// behaviour is identical until an addon registers one. Modifiers of a module that stopped are skipped and pruned.
    /// </summary>
    internal sealed class WeaponStats : IWeaponStats
    {
        /// <summary>The single instance.</summary>
        internal static readonly WeaponStats Instance = new WeaponStats();

        /// <summary>One registered modifier.</summary>
        sealed class Mod { public int Id, Order, Seq; public IModule Owner; public IWeaponStatModifier Modifier; }

        readonly List<Mod> _mods = new List<Mod>();
        int _nextId = 1, _seq, _revision;

        /// <summary>Private: use <see cref="Instance"/>.</summary>
        WeaponStats() { }

        /// <summary>True while at least one modifier is registered (hot paths skip the modifier chain otherwise).</summary>
        internal bool HasModifiers { get { return _mods.Count > 0; } }

        /// <summary>Falloff range of a weapon kind from [Gunplay / Ranges] (at least 1 m); melee/other kinds use the rifle range as before.</summary>
        internal static float KindRange(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return Mathf.Max(1f, GunplaySettings.PistolRange.Value);
                case WeaponKind.Smg: return Mathf.Max(1f, GunplaySettings.SmgRange.Value);
                case WeaponKind.Sniper: return Mathf.Max(1f, GunplaySettings.SniperRange.Value);
                case WeaponKind.Shotgun: return Mathf.Max(1f, GunplaySettings.ShotgunRange.Value);
                case WeaponKind.Crossbow: return Mathf.Max(1f, GunplaySettings.CrossbowRange.Value);
                default: return Mathf.Max(1f, GunplaySettings.RifleRange.Value);
            }
        }

        /// <summary>
        /// Runs the modifier chain on <paramref name="value"/> (the stat as Gunplay computed it). No modifiers: returns the value as is.
        /// A throwing modifier is logged and skipped.
        /// </summary>
        internal float Modify(int weaponKey, string itemId, WeaponStat stat, float value)
        {
            if (_mods.Count == 0) return value;
            for (int i = 0; i < _mods.Count; i++)
            {
                var m = _mods[i];
                if (m.Owner != null && !m.Owner.Active) continue;
                try { value = m.Modifier.Modify(weaponKey, itemId, stat, value); }
                catch (Exception e) { GunplayLog.Warn("weapon stat modifier of " + (m.Owner != null ? m.Owner.Id : "?") + ": " + e.Message); }
            }
            return value;
        }

        /// <summary>Drops every modifier (Gunplay shutdown) and bumps the revision.</summary>
        internal void Clear() { if (_mods.Count > 0) { _mods.Clear(); _revision++; } }

        // ------------------------------------------------------------------ IWeaponStats

        /// <summary>Effective stat without item-specific modifiers.</summary>
        public float Get(int weaponKey, WeaponStat stat) { return Modify(weaponKey, null, stat, Base(weaponKey, stat)); }

        /// <summary>Effective stat of one item instance.</summary>
        public float Get(int weaponKey, string itemId, WeaponStat stat) { return Modify(weaponKey, itemId, stat, Base(weaponKey, stat)); }

        /// <summary>
        /// Stat before modifiers: catalog multipliers and the current Gunplay config. 0 means "taken from the weapon's own FSM at fire
        /// time" (base damage, pellets of a plain shotgun, magazine, fire interval, reload time, noise).
        /// </summary>
        public float Base(int weaponKey, WeaponStat stat)
        {
            var e = WeaponCatalog.Instance.EntryOf(weaponKey);
            WeaponKind kind = e != null ? e.Info.Kind : WeaponKind.Rifle;
            bool shotgun = kind == WeaponKind.Shotgun;
            switch (stat)
            {
                case WeaponStat.BaseDamage: return e != null ? e.Info.BaseDamage : 0f;
                case WeaponStat.DamageMultiplier: return e == null ? 1f : e.DamageMultiplier * (shotgun ? e.ShotgunDamageMultiplier : 1f);
                case WeaponStat.HeadshotMultiplier: return Mathf.Max(0f, GunplaySettings.HeadshotMultiplier.Value);
                case WeaponStat.Range:
                    {
                        float r = e != null && e.Info.Range > 0f ? e.Info.Range : KindRange(kind);
                        return r * (e != null && shotgun ? e.ShotgunRangeMultiplier : 1f);
                    }
                case WeaponStat.FullDamageFraction: return Mathf.Clamp(GunplaySettings.FullDamageUntil.Value, 0f, 99f) / 100f;
                case WeaponStat.ProjectileSpeed: return kind == WeaponKind.Crossbow ? GunplaySettings.BoltSpeed.Value : GunplaySettings.BulletSpeed.Value;
                case WeaponStat.Pellets: return e != null && shotgun && e.ShotgunPellets > 0 ? e.ShotgunPellets : (e != null && !shotgun ? Mathf.Max(1, e.Info.Pellets) : 0f);
                case WeaponStat.PelletSpread: return e != null && shotgun && e.ShotgunSpread >= 0f ? e.ShotgunSpread : 0f;
                case WeaponStat.Accuracy: return GunplaySettings.Accuracy(true);
                case WeaponStat.MagazineCapacity: return e != null ? e.Info.Magazine : 0f;
                case WeaponStat.ZoomMax: return 3f;
                case WeaponStat.VehicleDamage: return Mathf.Max(1f, GunplaySettings.VehicleDamagePer1.Value);
                case WeaponStat.WheelDamage: return Mathf.Max(0f, GunplaySettings.WheelDamageMultiplier.Value);
                default: return 0f;
            }
        }

        /// <summary>Adds a modifier (lower order first, then registration order); returns its id.</summary>
        public int RegisterModifier(IModule owner, IWeaponStatModifier modifier, int order)
        {
            if (modifier == null) return 0;
            var m = new Mod { Id = _nextId++, Order = order, Seq = _seq++, Owner = owner, Modifier = modifier };
            int i = 0;
            while (i < _mods.Count && (_mods[i].Order < order || (_mods[i].Order == order && _mods[i].Seq < m.Seq))) i++;
            _mods.Insert(i, m);
            _revision++;
            if (Log.L1) GunplayLog.Main("Weapon stat modifier registered by " + (owner != null ? owner.Id : "?") + " (order " + order + ")");
            return m.Id;
        }

        /// <summary>Removes a modifier by id.</summary>
        public void RemoveModifier(int id)
        {
            for (int i = 0; i < _mods.Count; i++) if (_mods[i].Id == id) { _mods.RemoveAt(i); _revision++; return; }
        }

        /// <summary>A modifier's output changed (attachment added/removed): consumers re-read on the next revision.</summary>
        public void Invalidate(string itemId) { _revision++; }

        /// <summary>Changes with every modifier registration/removal/invalidation; also prunes modifiers of stopped modules.</summary>
        public int Revision
        {
            get
            {
                for (int i = _mods.Count - 1; i >= 0; i--)
                    if (_mods[i].Owner != null && !_mods[i].Owner.Active) { _mods.RemoveAt(i); _revision++; }
                return _revision;
            }
        }
    }
}
