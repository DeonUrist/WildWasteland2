using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    // (1.7.0) Spider nests. A camp / cave / wreck where spiders are (the game spawns them when you come within 500 m, or they come back with
    // a save) becomes a nest: hanging webs where the place has openings to span (between beams, walls, wrecks, rocks: spots with surfaces
    // around them in a vertical plane), sheet webs on the ground around, and the big spiders sitting in the hanging webs, one per web, head
    // down. Touching any web of the nest alarms every spider of it: they know where you are and run at you 1.5 times faster for a while.
    // A spider that notices something itself drops from its web and fights as usual.
    // Built in slices of at most [Nests] FrameBudgetMs (1 ms) a frame while you are still far away; the same layout every visit (seeded by
    // the place). Webs are never saved (not registered with the game) and are rebuilt when you come back / after a load.
    internal static partial class Nests
    {
        /// <summary>A candidate spot for a hanging web: its hub, facing normal and score.</summary>
        private sealed class Cand { public Vector3 Hub, N; public float Score; }
        /// <summary>A big spider sitting in a hanging web (its Rigidbody held kinematic while it sits).</summary>
        private sealed class Sit { public Senses.Agent A; public Webs.Web W; public Rigidbody Rb; public bool WasKinematic; }

        private sealed class Nest
        {
            public Transform Poi; public string Name; public Vector3 C; public float RefY, Radius;
            public readonly List<Senses.Agent> Spiders = new List<Senses.Agent>();
            public readonly List<Webs.Web> Hanging = new List<Webs.Web>(), Ground = new List<Webs.Web>();
            public readonly List<Sit> Sitters = new List<Sit>();
            public int Phase, Step, Want, GroundWant, Tries; public readonly List<Cand> Cands = new List<Cand>(); public List<Cand> Chosen;
            public int Seed; public UnityEngine.Random.State Rnd; public bool RndOn;
            public float BuildMs, MaxFrameMs, NextAlarm; public int Frames; public bool Logged, Waiting, Hold; public float NextTry;
            public readonly List<Vector3> Floor = new List<Vector3>();      // (1.7.2) a mapped place: floor points of its map (a cave: only under its roof)
            public string Key; public bool FromCache, Relaxed, Cave, SpiderSpots; public List<Vector3> CachedGround; public float WaitSince; public int Forced;   // (1.7.4)
        }

        private static readonly List<Nest> _nests = new List<Nest>();
        private static readonly List<Senses.Agent> _pending = new List<Senses.Agent>();
        private static readonly List<Transform> _pois = new List<Transform>();
        private static string[] _prefabs, _sitters; private static string _prefabsSrc, _sittersSrc;
        private static GameObject _mapMagic; private static float _nextPending, _nextStatus, _nextMap, _nextPois, _nextMaint, _nextTouch, _nextSit;
        private static readonly Stopwatch _sw = new Stopwatch();
        private const int GroundMask = (1 << 0) | (1 << 8) | (1 << 11) | (1 << 14) | (1 << 16);
        private const int Solid = (1 << 0) | (1 << 8) | (1 << 11) | (1 << 14) | (1 << 16);

        // Generated nest positions belong to the selected world, alongside the spiders they describe.
        private static Dictionary<string, string> _layouts;
        private static bool _layoutLoadFailed;
        private static readonly Core.SaveKey LayoutRecord = Core.SaveBus.Key("WildWasteland.NPCAI.Nests.v1");
        /// <summary>Loads remembered web positions lazily after native scene changes.</summary>
        private static void LoadLayouts()
        {
            if (_layouts == null) LoadWorld(Core.SaveBus.Slot);
        }
        /// <summary>Restores only the selected world's generated nest layouts.</summary>
        internal static void LoadWorld(string slot)
        {
            _layouts = null; _layoutLoadFailed = true;
            var layouts = new Dictionary<string, string>();
            string value = string.IsNullOrEmpty(slot) ? null : LayoutRecord.Read(slot);
            if (value != null) foreach (string line in value.Split('\n'))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) layouts.Add(line.Substring(0, tab), line.Substring(tab + 1));
                else if (line.Length != 0) throw new System.IO.InvalidDataException("Malformed embedded nest layout");
            }
            _layouts = layouts; _layoutLoadFailed = false;
        }
        /// <summary>Captures generated layouts once in the native world save transaction.</summary>
        internal static void SaveWorld(string slot)
        {
            if (_layoutLoadFailed) throw new System.IO.InvalidDataException("Nest layouts could not be loaded; refusing to replace the saved record");
            LoadLayouts();
            var lines = new List<string>();
            foreach (var pair in _layouts) lines.Add(pair.Key + "\t" + pair.Value);
            LayoutRecord.Write(NpcaiRuntime.Module, slot, string.Join("\n", lines.ToArray()));
        }
        /// <summary>Starts a fresh world without borrowing another world's cached positions.</summary>
        internal static void NewWorld() { _layouts = new Dictionary<string, string>(); _layoutLoadFailed = false; }
        /// <summary>Invariant "x,y,z" text of a vector (layout file).</summary>
        private static string F(Vector3 v) { return v.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," + v.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," + v.z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
        /// <summary>Parses an "x,y,z" layout vector.</summary>
        private static bool P(string s, out Vector3 v)
        {
            v = Vector3.zero; var p = s.Split(','); if (p.Length != 3) return false;
            var ci = System.Globalization.CultureInfo.InvariantCulture; float x, y, z;
            if (!float.TryParse(p[0], System.Globalization.NumberStyles.Float, ci, out x) || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out y) || !float.TryParse(p[2], System.Globalization.NumberStyles.Float, ci, out z)) return false;
            v = new Vector3(x, y, z); return true;
        }
        /// <summary>Stable key of a nest place: its tile, name and local position (layouts survive reloads).</summary>
        private static string KeyOf(Transform poi)
        {
            string tile = poi.parent != null && poi.parent.parent != null ? poi.parent.parent.name : "-";
            Vector3 l = poi.localPosition;
            return tile + "|" + poi.name + "|" + Mathf.RoundToInt(l.x) + "," + Mathf.RoundToInt(l.y) + "," + Mathf.RoundToInt(l.z);
        }
        /// <summary>"H:hub;normal|hub;normal...  G:centre|centre..." in the place's local space</summary>
        private static void Remember(Nest n)
        {
            if (n.Poi == null || n.Key == null || n.Hanging.Count + n.Ground.Count == 0) return;
            var sb = new System.Text.StringBuilder("H:");
            for (int i = 0; i < n.Hanging.Count; i++) { var w = n.Hanging[i]; if (i > 0) sb.Append('|'); sb.Append(F(n.Poi.InverseTransformPoint(w.Center))).Append(';').Append(F(n.Poi.InverseTransformDirection(w.N))); }
            sb.Append(" G:");
            for (int i = 0; i < n.Ground.Count; i++) { if (i > 0) sb.Append('|'); sb.Append(F(n.Poi.InverseTransformPoint(n.Ground[i].Center))); }
            _layouts[n.Key] = sb.ToString();
        }
        /// <summary>Rebuilds a nest's web spots from its remembered layout (false when there is none).</summary>
        private static bool Recall(Nest n)
        {
            string v; if (n.Key == null || !_layouts.TryGetValue(n.Key, out v)) return false;
            n.Chosen = new List<Cand>(); n.CachedGround = new List<Vector3>();
            try
            {
                int g = v.IndexOf(" G:");
                string hs = v.Substring(2, (g < 0 ? v.Length : g) - 2), gs = g < 0 ? "" : v.Substring(g + 3);
                foreach (var part in hs.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var q = part.Split(';'); Vector3 h, nn;
                    if (q.Length == 2 && P(q[0], out h) && P(q[1], out nn)) n.Chosen.Add(new Cand { Hub = n.Poi.TransformPoint(h), N = n.Poi.TransformDirection(nn) });
                }
                foreach (var part in gs.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)) { Vector3 c; if (P(part, out c)) n.CachedGround.Add(n.Poi.TransformPoint(c)); }
            }
            catch (Exception) { n.Chosen.Clear(); n.CachedGround.Clear(); }
            return n.Chosen.Count + n.CachedGround.Count > 0;
        }

        /// <summary>Scene load: every nest freed (webs destroyed), pending spiders and places forgotten.</summary>
        internal static void OnSceneLoaded() { foreach (var n in _nests) Free(n, false); _nests.Clear(); _pending.Clear(); _pois.Clear(); _mapMagic = null; _nextMap = 0f; _nextPois = 0f; _layouts = null; _layoutLoadFailed = false; }

        /// <summary>(WW) module shutdown: sitting spiders drop from their webs (not kinematic, brain hold lifted), every nest web is destroyed</summary>
        internal static void Shutdown()
        {
            foreach (var n in _nests) { try { Free(n, true); } catch (Exception e) { NpcaiRuntime.Warn("Nests shutdown: " + e.Message); } }
            _nests.Clear(); _pending.Clear(); _pois.Clear();
        }

        // (WW) floating origin: nest centres, floor points, spot candidates and the cached ground spots are scene space (the webs: Webs.Shift)
        private static readonly HashSet<Cand> _shifted = new HashSet<Cand>();
        /// <summary>Floating origin: nest centres, web spots and webs move with the world.</summary>
        internal static void Shift(Vector3 d)
        {
            _shifted.Clear();
            foreach (var n in _nests)
            {
                n.C += d; n.RefY += d.y;
                for (int i = 0; i < n.Floor.Count; i++) n.Floor[i] += d;
                if (n.CachedGround != null) for (int i = 0; i < n.CachedGround.Count; i++) n.CachedGround[i] += d;
                foreach (var c in n.Cands) if (c != null && _shifted.Add(c)) c.Hub += d;
                if (n.Chosen != null) foreach (var c in n.Chosen) if (c != null && _shifted.Add(c)) c.Hub += d;
            }
            _shifted.Clear();
        }
        /// <summary>Number of nests.</summary>
        internal static int Count { get { return _nests.Count; } }
        internal static int Ready { get { int k = 0; foreach (var n in _nests) if (n.Phase >= 5) k++; return k; } }
        /// <summary>Spiders not yet assigned to a nest place.</summary>
        internal static int Pending { get { return _pending.Count; } }

        /// <summary>True when the prefab name contains a word of a config list (split once per value).</summary>
        private static bool In(string prefab, string cfg, ref string[] cache, ref string src)
        {
            cfg = cfg ?? "";
            if (cache == null || src != cfg) { src = cfg; cache = cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries); for (int i = 0; i < cache.Length; i++) cache[i] = cache[i].Trim(); }
            foreach (var p in cache) if (p.Length > 0 && string.Equals(prefab, p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Senses.Make: every registered NPC passes here once</summary>
        internal static void Register(Senses.Agent a)
        {
            if (a == null || a.Owner == null || !In(Senses.PrefabOf(a.Owner), NpcaiSettings.NestPrefabs.Value, ref _prefabs, ref _prefabsSrc)) return;
            // a spider saved while it sat in a web: never left held in the air after a load (spiders are never kinematic in the game)
            var rb = a.Owner.GetComponent<Rigidbody>(); if (rb != null && rb.isKinematic) rb.isKinematic = false;
            _pending.Add(a);
        }

        /// <summary>True when the spider type sits in hanging webs ([Nests] Sitters).</summary>
        private static bool CanSit(Senses.Agent a) { return a != null && a.Owner != null && In(Senses.PrefabOf(a.Owner), NpcaiSettings.NestSitters.Value, ref _sitters, ref _sittersSrc); }

        /// <summary>Job "NPCAI.Nests" (every frame): nests near the player are kept / freed, pending spiders placed, nests built in slices within the frame budget, sitters and web touches checked.</summary>
        internal static void Tick()
        {
            float now = Time.time;
            bool on = NpcaiSettings.NestsEnabled.Value && Senses.On;
            var player = Senses.PlayerObj;
            if (!on || player == null)
            {
                if (_nests.Count > 0 && !on) { foreach (var n in _nests) Free(n, true); _nests.Clear(); }
                return;
            }
            Vector3 pp = player.transform.position;
            var cam = Core.GameRefs.MainCamera;
            Vector3 vp = cam != null ? cam.transform.position : pp;          // (1.7.3) where you look from: also while flying in god mode
            if (now >= _nextMaint) { _nextMaint = now + 1f; Maintain(vp, now); }
            if (_nests.Count == 0) return;
            // one nest is built at a time, in slices
            for (int i = 0; i < _nests.Count; i++)
            {
                var n = _nests[i];
                if (n.Phase >= 5 || now < n.NextTry) continue;
                if (Flat(n.C - vp) > NpcaiSettings.NestBuildRange.Value) continue;
                Build(n, now);
                break;
            }
            if (now >= _nextTouch) { _nextTouch = now + 0.1f; Touch(player, pp, now); }
            if (now >= _nextSit) { _nextSit = now + 0.2f; Sitters(player, now); }
        }

        /// <summary>Horizontal length of a vector.</summary>
        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    }
}
