using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Nests part: which nests exist - freeing far nests, assigning spiders to caves / wrecks / camps.</summary>
    internal static partial class Nests
    {
        // ---------- which nests exist ----------
        /// <summary>Frees nests whose place is gone or far, then assigns pending spiders to the nearest cave / wreck / camp (a new nest when needed).</summary>
        private static void Maintain(Vector3 pp, float now)
        {
            for (int i = _nests.Count - 1; i >= 0; i--)
            {
                var n = _nests[i];
                n.Spiders.RemoveAll(a => a == null || a.Owner == null);
                if (n.Poi == null || !n.Poi.gameObject.activeInHierarchy || Flat(n.C - pp) > NpcaiSettings.NestFreeRange.Value)
                {
                    Free(n, true); _nests.RemoveAt(i); PublishFreed(n);
                    foreach (var a in n.Spiders) if (a != null && a.Owner != null && !_pending.Contains(a)) _pending.Add(a);     // (1.7.1) the nest comes back when you do
                    if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + n.Name + " nest freed (place gone or far)");
                }
            }
            if (_pending.Count == 0 || now < _nextPending) return;
            _nextPending = now + 2f;
            if (now >= _nextPois) { _nextPois = now + 5f; Pois(); }
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var a = _pending[i];
                if (a == null || a.Owner == null) { _pending.RemoveAt(i); continue; }
                if (a.T.parent != null) continue;
                var poi = PoiAt(a.T.position);
                if (Flat(a.T.position - pp) > NpcaiSettings.NestFreeRange.Value * 0.9f) continue;    // (1.7.3) far away: planned when you come closer (was planned and freed every 2 s)
                if (poi == null) continue;          // (1.7.1) no place found (yet): asked again later - was dropped for good when > 600 m away
                _pending.RemoveAt(i);
                Nest nest = null;
                foreach (var x in _nests) if (x.Poi == poi) { nest = x; break; }
                if (nest == null) { nest = Make(poi); if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: spiders at " + poi.name + " (" + Flat(poi.position - pp).ToString("0") + " m away) - a nest will be built there"); }
                if (!nest.Spiders.Contains(a)) nest.Spiders.Add(a);
            }
            if (_pending.Count > 0 && WildWasteland.Core.Log.L2 && now >= _nextStatus)
            {
                _nextStatus = now + 30f;
                var a0 = _pending[0];
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + _pending.Count + " spider(s) not at any place (first " + (a0.Owner != null ? a0.Owner.name : "?") + "), " + _pois.Count + " places known" + (_mapMagic == null ? ", MapMagic not found" : ""));
            }
        }

        /// <summary>Creates a nest at a place (its layout key; remembered layouts load lazily).</summary>
        private static Nest Make(Transform poi)
        {
            LoadLayouts();
            var n = new Nest { Poi = poi, Name = poi.name, C = poi.position, RefY = poi.position.y, Key = KeyOf(poi), Cave = poi.name.StartsWith("Cave_") };      // (1.7.1) was (0,0,0) until the build began
            int px = Mathf.RoundToInt(poi.position.x), pz = Mathf.RoundToInt(poi.position.z);
            n.Seed = px * 73856093 ^ pz * 19349663;
            _nests.Add(n);
            return n;
        }

        /// <summary>the places: children of MapMagic/Tile */Objects named Camp_ / Cave_ / Wreck_ / Building_</summary>
        private static void Pois()
        {
            if (_mapMagic == null && Time.time >= _nextMap) { _nextMap = Time.time + 10f; var mm0 = Core.Origin.AnchorTransform; _mapMagic = mm0 != null ? mm0.gameObject : null; }      // (WW) Core's MapMagic lookup
            _pois.Clear();
            if (_mapMagic == null) return;
            var mm = _mapMagic.transform;
            for (int i = 0; i < mm.childCount; i++)
            {
                var objs = mm.GetChild(i).Find("Objects");
                if (objs == null) continue;
                for (int k = 0; k < objs.childCount; k++)
                {
                    var t = objs.GetChild(k);
                    string nm = t.name;
                    if (t.gameObject.activeInHierarchy && IsPlace(nm)) _pois.Add(t);
                }
            }
        }

        /// <summary>Cave_3(Clone), Wreck_6, Camp_12 ...: the prefix and then a digit (not a prop called Camp_fire)</summary>
        private static bool IsPlace(string nm)
        {
            int k = nm.StartsWith("Cave_") ? 5 : nm.StartsWith("Wreck_") ? 6 : nm.StartsWith("Camp_") ? 5 : nm.StartsWith("Building_") ? 9 : -1;
            return k > 0 && nm.Length > k && char.IsDigit(nm[k]);
        }

        /// <summary>The nearest known nest place within PlaceRadius of a point.</summary>
        private static Transform PoiAt(Vector3 p)
        {
            Transform best = null; float bd = NpcaiSettings.NestPoiRadius.Value;
            foreach (var t in _pois) { if (t == null || !t.gameObject.activeInHierarchy) continue; float d = Flat(t.position - p); if (d < bd) { bd = d; best = t; } }
            return best ?? PoiByColliders(p);
        }

        // (1.7.1) fallback: the place whose own geometry is around the spider (a collider within 15 m whose ancestors include a
        // Camp_ / Cave_ / Wreck_ / Building_ object) - works whatever the scene hierarchy looks like
        private static readonly Collider[] _near = new Collider[48];
        /// <summary>A nest place found from the solid colliders around a point (when the place list missed it).</summary>
        private static Transform PoiByColliders(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p, 15f, _near, Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = _near[i] != null ? _near[i].transform : null;
                for (int k = 0; k < 12 && t != null; k++, t = t.parent)
                {
                    string nm = t.name;
                    if (IsPlace(nm)) return t;
                }
            }
            return null;
        }

        /// <summary>Frees a nest: sitters released (optionally dropped), webs destroyed.</summary>
        private static void Free(Nest n, bool drop)
        {
            foreach (var s in n.Sitters) if (s.A != null) s.A.CaveWeb = false;
            if (drop) foreach (var s in n.Sitters) Drop(s, null);
            n.Sitters.Clear();
            foreach (var w in n.Hanging) Webs.Kill(w);
            foreach (var w in n.Ground) Webs.Kill(w);
            n.Hanging.Clear(); n.Ground.Clear();
        }

    }
}
