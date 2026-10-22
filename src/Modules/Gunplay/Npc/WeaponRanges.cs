using System;
using System.Collections.Generic;
using WildWasteland.Core;
using UnityEngine;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.NPC
{
    // Small deterministic fallback kept consistent with Gunplay's version-one contract (Core.Services.Guns when the Guns module runs).
    internal static partial class WeaponRanges
    {
        /// <summary>NPC weapon kinds (values match WeaponKind 0..5).</summary>
        internal enum Kind { Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow }
        /// <summary>Fallback cache per NPC: held weapon, kind and retry state.</summary>
        private sealed class Info { public GameObject Owner; public Transform Weapon; public Kind Kind; public bool IsGun; public int Tries; public float Retry; }
        private static readonly Dictionary<int, Info> _shooters = new Dictionary<int, Info>();
        private static readonly List<int> _deadShooters = new List<int>();
        // (WW) Gunplay's version-one contract is Core.Services.Guns (the Guns module); null = Guns off = the fallback below, as when
        // Gunplay was not installed.
        internal static bool ProjectilesEnabled
        {
            get { var g = Services.Guns; try { return g != null && g.ProjectilesEnabled; } catch (Exception) { return false; } }
        }

        /// <summary>Effective range of a kind: Gunplay's when it runs, else the built-in table.</summary>
        internal static float RangeOf(Kind k)
        {
            var g = Services.Guns;
            if (g != null)
                try { float range = g.EffectiveRange((int)k); if (!float.IsNaN(range) && !float.IsInfinity(range)) return Mathf.Max(1f, range); }
                catch (Exception e) { GunplayLog.Warn("Gunplay range query failed: " + e.Message); }
            switch (k)
            {
                case Kind.Pistol: return 60f;
                case Kind.Smg: return 70f;
                case Kind.Sniper: return 250f;
                case Kind.Shotgun: return 35f;
                case Kind.Crossbow: return 90f;
                default: return 120f;
            }
        }

        /// <summary>Gun kind of a weapon name (Gunplay weapon catalog rule; the enum values match WeaponKind 0..5).</summary>
        internal static Kind Classify(string weapon) { return (Kind)(int)WildWasteland.Modules.Gunplay.WeaponCatalog.GunKindOfName(weapon); }

        /// <summary>Kind of the gun an NPC holds: Gunplay's when it runs, else its own cached lookup.</summary>
        internal static bool GunKindOf(GameObject owner, out Kind kind)
        {
            kind = Kind.Rifle;
            if (owner == null) return false;
            var g = Services.Guns;
            if (g != null)
                try { int found; bool gun = g.TryGetWeaponKind(owner, out found); if (found >= 0 && found <= 5) kind = (Kind)found; return gun; }
                catch (Exception e) { GunplayLog.Warn("Gunplay weapon query failed: " + e.Message); }
            int id = owner.GetInstanceID(); Info info;
            if (_shooters.TryGetValue(id, out info) && info.Owner == owner && (info.Weapon != null ? info.Weapon.gameObject.activeInHierarchy : Time.time < info.Retry))
            { kind = info.Kind; return info.IsGun; }
            bool ranged = false;
            foreach (var f in owner.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Damage Ranged") { ranged = true; break; }
            if (!ranged) return false;
            int tries = info != null && info.Weapon == null ? info.Tries + 1 : 0;
            info = new Info { Owner = owner, Tries = tries, Retry = tries < 10 ? Time.time + 1f : float.MaxValue };
            _shooters[id] = info;
            foreach (var t in owner.GetComponentsInChildren<Transform>(true))
                if (t.name == "fire_effect" && t.parent != null && t.parent.name != "fire_effect" && t.parent.gameObject.activeInHierarchy)
                { info.Weapon = t.parent; break; }
            if (info.Weapon == null)
                foreach (var t in owner.GetComponentsInChildren<Transform>(false))
                    if (t.name.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase) && t.GetComponent<Renderer>() != null) { info.Weapon = t; break; }
            if (info.Weapon == null) return false;
            info.IsGun = true; info.Kind = Classify(info.Weapon.name); kind = info.Kind;
            return true;
        }

        /// <summary>Job "Gunplay.WeaponRanges.Sweep" (30 s): drops cache entries of destroyed NPCs.</summary>
        internal static void Sweep()      // (WW) a 30 s Interval job (was gated here)
        {
            _deadShooters.Clear();
            foreach (var kv in _shooters) if (kv.Value.Owner == null) _deadShooters.Add(kv.Key);
            foreach (var id in _deadShooters) _shooters.Remove(id);
        }
        /// <summary>Scene change: the cache is dropped.</summary>
        internal static void OnSceneLoaded() { _shooters.Clear(); _deadShooters.Clear(); }
    }
}
