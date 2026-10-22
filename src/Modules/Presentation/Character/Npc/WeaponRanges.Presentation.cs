using System;
using System.Collections.Generic;
using WildWasteland.Core;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>WeaponRanges part owned by Presentation: finding the weapon model held in an NPC's hands (hand bone scan, held weapon lookup).</summary>
    internal static partial class WeaponRanges
    {
        // the gun model in the NPC's hand (the parent of its active fire_effect), independent of Gunplay
        // The weapon model the NPC holds now: the active child of a hand bone that has a renderer (the way Apocaplayer's Props catalog
        // finds the NPC weapon models; the WeaponType FSM activates one of the hand's gun models at spawn, the melee swing a machete).
        // Fallback: the parent of an active muzzle flash ("fire_effect"), or an active crossbow renderer.
        // (perf) the bones / candidates WeaponOf looks at, found once per NPC (the skeleton and the gun models under it do not change; the
        // rig only moves a gun from one hand to the other): hand bones in hierarchy order, muzzle flashes, crossbow renderers
        /// <summary>Hand bones, muzzle flashes and crossbow renderers of an NPC, found once (the held-weapon lookup).</summary>
        internal sealed class HandCache { public Transform[] Hands, Effects, Bows; }
        private static readonly List<Transform> _hb = new List<Transform>(), _eb = new List<Transform>(), _bb = new List<Transform>();
        /// <summary>Scans an NPC's hand bones / muzzle flashes / crossbow renderers into a HandCache.</summary>
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
        /// <summary>The held weapon model from the cached lists (falls back to the full scan).</summary>
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
        /// <summary>True when t sits under one of the cached hand bones.</summary>
        internal static bool UnderHand(HandCache c, Transform t)
        {
            if (c == null || c.Hands.Length == 0 || t == null) return true;      // unknown: as before (re-find)
            foreach (var h in c.Hands) if (h == null || t.IsChildOf(h) || h.IsChildOf(t)) return true;
            return false;
        }

        /// <summary>The weapon model the NPC holds now (full scan).</summary>
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
    }
}
