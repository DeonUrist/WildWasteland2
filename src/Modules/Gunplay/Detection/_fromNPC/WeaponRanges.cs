using System;
using System.Collections.Generic;
using WildWasteland.Core;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // Small deterministic fallback kept consistent with Gunplay's version-one contract (Core.Services.Guns when the Guns module runs).
    internal static class WeaponRanges
    {
        internal enum Kind { Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow }
        private sealed class Info { public GameObject Owner; public Transform Weapon; public Kind Kind; public bool IsGun; public int Tries; public float Retry; }
        private static readonly Dictionary<int, Info> _shooters = new Dictionary<int, Info>();
        private static readonly List<int> _deadShooters = new List<int>();
        // (WW) Gunplay's version-one contract is Core.Services.Guns (the Guns module); null = Guns off = the fallback below, as when
        // Gunplay was not installed.
        internal static bool ProjectilesEnabled
        {
            get { var g = Services.Guns; try { return g != null && g.ProjectilesEnabled; } catch (Exception) { return false; } }
        }

        internal static float RangeOf(Kind k)
        {
            var g = Services.Guns;
            if (g != null)
                try { float range = g.EffectiveRange((int)k); if (!float.IsNaN(range) && !float.IsInfinity(range)) return Mathf.Max(1f, range); }
                catch (Exception e) { NpcModule.Warn("Gunplay range query failed: " + e.Message); }
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

        internal static Kind Classify(string weapon)
        {
            string n = weapon.ToLowerInvariant();
            if (n.Contains("crossbow")) return Kind.Crossbow;
            if (n.Contains("shotgun") || n.Contains("slamfire") || n.Contains("slamberg") || n.Contains("rochester")) return Kind.Shotgun;
            if (n.Contains("scoped") || n.Contains("sniper") || n.Contains("redmark")) return Kind.Sniper;
            if (n.Contains("smg") || n.Contains("borz")) return Kind.Smg;
            if (n.Contains("pistol") || n.Contains("revolver") || n.Contains("folk_17")) return Kind.Pistol;
            return Kind.Rifle;
        }

        internal static bool GunKindOf(GameObject owner, out Kind kind)
        {
            kind = Kind.Rifle;
            if (owner == null) return false;
            var g = Services.Guns;
            if (g != null)
                try { int found; bool gun = g.TryGetWeaponKind(owner, out found); if (found >= 0 && found <= 5) kind = (Kind)found; return gun; }
                catch (Exception e) { NpcModule.Warn("Gunplay weapon query failed: " + e.Message); }
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
        // the gun model in the NPC's hand (the parent of its active fire_effect), independent of Gunplay
        // The weapon model the NPC holds now: the active child of a hand bone that has a renderer (the way Apocaplayer's Props catalog
        // finds the NPC weapon models; the WeaponType FSM activates one of the hand's gun models at spawn, the melee swing a machete).
        // Fallback: the parent of an active muzzle flash ("fire_effect"), or an active crossbow renderer.
        // (perf) the bones / candidates WeaponOf looks at, found once per NPC (the skeleton and the gun models under it do not change; the
        // rig only moves a gun from one hand to the other): hand bones in hierarchy order, muzzle flashes, crossbow renderers
        internal sealed class HandCache { public Transform[] Hands, Effects, Bows; }
        private static readonly List<Transform> _hb = new List<Transform>(), _eb = new List<Transform>(), _bb = new List<Transform>();
        internal static HandCache ScanHands(GameObject owner)
        {
            _hb.Clear(); _eb.Clear(); _bb.Clear();
            foreach (var h in owner.GetComponentsInChildren<Transform>(true))
            {
                string hn = h.name; int i = hn.LastIndexOf(':'); string sn = i >= 0 ? hn.Substring(i + 1) : hn;
                if (sn == "LeftHand" || sn == "RightHand") _hb.Add(h);
                if (hn == "fire_effect" && h.parent != null && h.parent.name != "fire_effect") _eb.Add(h);
                if (hn.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase) && h.GetComponent<Renderer>() != null) _bb.Add(h);
            }
            return new HandCache { Hands = _hb.ToArray(), Effects = _eb.ToArray(), Bows = _bb.ToArray() };
        }
        // same result as WeaponOf(owner) from the cached lists; null cache, no hand bone or a destroyed bone: the full scan
        internal static Transform WeaponOf(GameObject owner, HandCache c)
        {
            if (owner == null) return null;
            if (c == null || c.Hands.Length == 0) return WeaponOf(owner);
            foreach (var h in c.Hands)
            {
                if (h == null) return WeaponOf(owner);
                for (int k = 0; k < h.childCount; k++)
                {
                    var w = h.GetChild(k);
                    if (w.gameObject.activeInHierarchy && w.GetComponentInChildren<Renderer>(false) != null) return w;
                }
            }
            foreach (var t in c.Effects)
                if (t != null && t.parent != null && t.parent.gameObject.activeInHierarchy) return t.parent;
            foreach (var t in c.Bows)
                if (t != null && t.gameObject.activeInHierarchy) return t;
            return null;
        }
        internal static bool UnderHand(HandCache c, Transform t)
        {
            if (c == null || c.Hands.Length == 0 || t == null) return true;      // unknown: as before (re-find)
            foreach (var h in c.Hands) if (h == null || t.IsChildOf(h) || h.IsChildOf(t)) return true;
            return false;
        }

        internal static Transform WeaponOf(GameObject owner)
        {
            if (owner == null) return null;
            var all = owner.GetComponentsInChildren<Transform>(true);
            foreach (var h in all)
            {
                string hn = h.name; int i = hn.LastIndexOf(':'); if (i >= 0) hn = hn.Substring(i + 1);
                if (hn != "LeftHand" && hn != "RightHand") continue;
                for (int c = 0; c < h.childCount; c++)
                {
                    var w = h.GetChild(c);
                    if (w.gameObject.activeInHierarchy && w.GetComponentInChildren<Renderer>(false) != null) return w;
                }
            }
            foreach (var t in all)
                if (t.name == "fire_effect" && t.parent != null && t.parent.name != "fire_effect" && t.parent.gameObject.activeInHierarchy) return t.parent;
            foreach (var t in all)
                if (t.gameObject.activeInHierarchy && t.name.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase) && t.GetComponent<Renderer>() != null) return t;
            return null;
        }

        internal static void Sweep()      // (WW) a 30 s Interval job (was gated here)
        {
            _deadShooters.Clear();
            foreach (var kv in _shooters) if (kv.Value.Owner == null) _deadShooters.Add(kv.Key);
            foreach (var id in _deadShooters) _shooters.Remove(id);
        }
        internal static void OnSceneLoaded() { _shooters.Clear(); _deadShooters.Clear(); }
    }
}
