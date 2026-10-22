using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Guns;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// The one weapon catalog (README P7, 6.3; provider of <see cref="IWeaponCatalog"/>). Replaces the scattered name classifiers:
    /// Guns Tracers.Classify and NPC WeaponRanges.Classify (gun kind), the CleanHUD caliber/kind table (Assets.Weapons), the PlayerBody hold
    /// classifier (Props.Kinds), the folk17 alias of PerGunAmmo and the per-name multipliers that were hard-coded in Tracers.MakeGun
    /// (crossbow x2.5, redmark 100/90, chopped shotguns 9 pellets x1.5, full-length pump shotguns x2 range). Every rule keeps its exact
    /// original text predicate, so the classification of any name (also modded ones) is unchanged. Handles are stable for the session;
    /// unknown names are added on first internal use (<see cref="Handle"/>), the public <see cref="Resolve"/> only finds known keys.
    /// </summary>
    internal sealed class WeaponCatalog : IWeaponCatalog
    {
        /// <summary>The single instance (provided through ModApi while Gunplay runs; usable internally at any time).</summary>
        internal static readonly WeaponCatalog Instance = new WeaponCatalog();

        /// <summary>Animation hold style of a weapon name (the PlayerBody Props.Kind taxonomy: SMGs are held like pistols).</summary>
        internal enum Hold { None, Rifle, Pistol, Melee, Throw }

        /// <summary>Per-weapon data beyond the contract's <see cref="WeaponInfo"/>: the multipliers MakeGun applied by name.</summary>
        internal sealed class Entry
        {
            /// <summary>Contract view of this weapon.</summary>
            public WeaponInfo Info;
            /// <summary>Damage multiplier that applies whatever the gun turns out to be (crossbow 2.5, redmark 100/90).</summary>
            public float DamageMultiplier = 1f;
            /// <summary>Only when the gun's FSM really loops pellets: damage multiplier (chopped pump shotguns 1.5).</summary>
            public float ShotgunDamageMultiplier = 1f;
            /// <summary>Only when the gun's FSM really loops pellets: range multiplier (full-length pump shotguns 2).</summary>
            public float ShotgunRangeMultiplier = 1f;
            /// <summary>Only when the gun's FSM really loops pellets: fixed pellet count (chopped pump shotguns 9; 0 = the FSM's own).</summary>
            public int ShotgunPellets;
            /// <summary>Only when the gun's FSM really loops pellets: fixed pellet spread (chopped 0.03; negative = the FSM's own).</summary>
            public float ShotgunSpread = -1f;
            /// <summary>Owner of a registered entry (null for built-in / discovered ones).</summary>
            public IModule Owner;
            /// <summary>True for entries of the built-in table (the CleanHUD caliber table rows).</summary>
            public bool BuiltIn;
            /// <summary>Animation hold style.</summary>
            public Hold HoldStyle;
        }

        readonly List<Entry> _entries = new List<Entry>(64);
        readonly Dictionary<string, int> _byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.Ordinal) { { "folk17", "folk_17" } };
        readonly List<string> _builtInOrder = new List<string>(32);

        /// <summary>Builds the built-in table (CleanHUD calibers, same order and strings).</summary>
        WeaponCatalog()
        {
            foreach(var row in WeaponNames.BuiltIns())Add(row.Key,row.Caliber,row.Kind==WeaponKind.Other?(WeaponKind?)null:row.Kind);
        }

        /// <summary>Adds one built-in row; guns take their kind from the gun classifier.</summary>
        void Add(string key, string caliber, WeaponKind? kind = null)
        {
            var e = Describe(key);
            e.Info.Caliber = caliber;
            if (kind.HasValue) e.Info.Kind = kind.Value;
            e.BuiltIn = true;
            Store(e);
            _builtInOrder.Add(key);
        }

        /// <summary>Built-in keys in table order (HUD caliber table source).</summary>
        internal List<string> BuiltInKeys { get { return _builtInOrder; } }

        // ------------------------------------------------------------------ classification rules (exact original predicates)

        /// <summary>Gun kind of a weapon name - the former Tracers.Classify / WeaponRanges.Classify text, unchanged.</summary>
        internal static WeaponKind GunKindOfName(string weapon) { return WeaponNames.GunKindOfName(weapon); }
        /// <summary>Canonical native animation hold style.</summary>
        internal static Hold HoldOfName(string weapon) { return (Hold)(int)WeaponNames.HoldOfName(weapon); }

        /// <summary>A catalog entry for a weapon name with the former MakeGun per-name multipliers (same prefix/contains tests).</summary>
        static Entry Describe(string name)
        {
            var e = new Entry();
            e.Info.Key = name; e.Info.DisplayName = name;
            e.Info.Kind = GunKindOfName(name);
            e.Info.Pellets = 1;
            e.Info.Scoped = name.IndexOf("scoped", StringComparison.OrdinalIgnoreCase) >= 0;
            e.HoldStyle = HoldOfName(name);
            if (e.HoldStyle == Hold.Melee) e.Info.Kind = WeaponKind.Melee;
            else if (e.HoldStyle == Hold.Throw) e.Info.Kind = WeaponKind.Thrown;
            if (name.StartsWith("rochester_m24", StringComparison.OrdinalIgnoreCase) || name.StartsWith("slamberg_500", StringComparison.OrdinalIgnoreCase))
            {
                if (name.IndexOf("_chopped", StringComparison.OrdinalIgnoreCase) >= 0)
                { e.ShotgunPellets = 9; e.ShotgunSpread = 0.03f; e.ShotgunDamageMultiplier = 1.5f; }
                else e.ShotgunRangeMultiplier = 2f;
            }
            if (GunKindOfName(name) == WeaponKind.Crossbow) e.DamageMultiplier = 2.5f;
            else if (name.StartsWith("redmark_m11", StringComparison.OrdinalIgnoreCase)) e.DamageMultiplier = 100f / 90f;
            return e;
        }

        /// <summary>Stores an entry under its key (replacing an existing one in place) and returns its handle.</summary>
        int Store(Entry e)
        {
            int h;
            if (_byKey.TryGetValue(e.Info.Key, out h)) { _entries[h] = e; return h; }
            h = _entries.Count;
            _entries.Add(e);
            _byKey[e.Info.Key] = h;
            return h;
        }

        // ------------------------------------------------------------------ internal API (Gunplay parts)

        /// <summary>Handle of a raw object/item name, adding a classified entry the first time (never -1 for a non-empty name).</summary>
        internal int Handle(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return -1;
            string key = NormalizeKey(objectName);
            int h;
            if (_byKey.TryGetValue(key, out h)) return h;
            return Store(Describe(key));
        }

        /// <summary>Entry of a handle (null when out of range).</summary>
        internal Entry EntryOf(int handle) { return handle >= 0 && handle < _entries.Count ? _entries[handle] : null; }

        /// <summary>The native weaponType string of an item mapped to its arm-model name ("folk17" -> "folk_17"); other names unchanged.</summary>
        internal string ModelNameOf(string weaponType)
        {
            string m;
            return weaponType != null && _aliases.TryGetValue(weaponType, out m) ? m : weaponType;
        }

        /// <summary>Drops entries registered by other modules (Gunplay shutdown); built-in and discovered entries stay.</summary>
        internal void ClearRegistered()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Owner == null) continue;
                var fresh = Describe(e.Info.Key);
                fresh.Info.Caliber = e.Info.Caliber;
                _entries[i] = fresh;
            }
        }

        // ------------------------------------------------------------------ IWeaponCatalog

        /// <summary>Handle of a normalized (or raw) key; -1 when the catalog has never seen it.</summary>
        public int Resolve(string weaponKey)
        {
            if (string.IsNullOrEmpty(weaponKey)) return -1;
            int h;
            return _byKey.TryGetValue(NormalizeKey(weaponKey), out h) ? h : -1;
        }

        /// <summary>Handle of the weapon an item/model object (by name) or an NPC/turret (its held gun) represents.</summary>
        public bool TryResolve(GameObject itemModelOrOwner, out int weaponKey)
        {
            weaponKey = -1;
            if (itemModelOrOwner == null) return false;
            Tracers.Kind kind;
            string held;
            if (Tracers.HeldWeaponName(itemModelOrOwner, out kind, out held)) { weaponKey = Handle(held); return weaponKey >= 0; }
            weaponKey = Resolve(itemModelOrOwner.name);
            return weaponKey >= 0;
        }

        /// <summary>"AKMS(Clone)3" -> "akms": strips the clone suffix and an "_model" suffix, lower-cases.</summary>
        public string NormalizeKey(string objectName)
        {
            if (objectName == null) return null;
            string n = objectName.Trim();
            int c = n.IndexOf("(Clone)", StringComparison.Ordinal);
            if (c >= 0) n = n.Substring(0, c);
            if (n.EndsWith("_model", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 6);
            n = n.ToLowerInvariant();
            string alias;
            return _aliases.TryGetValue(n, out alias) ? alias : n;
        }

        /// <summary>Contract entry of a handle.</summary>
        public bool TryGet(int weaponKey, out WeaponInfo info)
        {
            var e = EntryOf(weaponKey);
            info = e != null ? e.Info : default(WeaponInfo);
            if (e == null) return false;
            if (info.Range <= 0f) info.Range = WeaponStats.KindRange(info.Kind);
            return true;
        }

        /// <summary>Key string of a handle.</summary>
        public string KeyOf(int weaponKey) { var e = EntryOf(weaponKey); return e != null ? e.Info.Key : null; }

        /// <summary>Kind of a handle (<see cref="WeaponKind.Other"/> when unknown).</summary>
        public WeaponKind KindOf(int weaponKey) { var e = EntryOf(weaponKey); return e != null ? e.Info.Kind : WeaponKind.Other; }

        /// <summary>Registers or replaces a weapon (Compatibility bridges); its owner's entries reset when Gunplay stops.</summary>
        public int Register(IModule owner, WeaponInfo info)
        {
            if (string.IsNullOrEmpty(info.Key)) return -1;
            info.Key = NormalizeKey(info.Key);
            var e = Describe(info.Key);
            e.Info = info;
            if (e.Info.Pellets <= 0) e.Info.Pellets = 1;
            e.Owner = owner;
            return Store(e);
        }

        /// <summary>Number of known weapons (handles 0..Count-1).</summary>
        public int Count { get { return _entries.Count; } }
    }
}
