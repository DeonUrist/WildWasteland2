using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // Sandstorms ([Senses] StormSight / StormHearing / StormRadius, hidden). A storm is the moving "SandStorm" object the game keeps in
    // NewGO_ArrayList's ArrayList_Sandstorms (spawned by Sandstorm_N POIs, restored with saves, removed by the game's own "disable
    // sandstorm" option). The game decides "the player is in the storm" by distance to that object: its SandPlayer FSM switches the sand
    // around the player on within 2500 m (asset read 2026-10-02) - the same test here, flat distance, against the list (0-2 storms).
    // Refresh() runs once a second from Senses' maintenance; the list object is looked up once per scene.
    internal static partial class Storm
    {
        private static readonly List<Transform> _storms = new List<Transform>();
        // The tornado funnels (SandStorm/Sand tornado */TornadoPhysics (n)): each fires an Explosion every 0.5 s (force 50, radius 150,
        // VelocityChange) on layers 6/8/9/10 - player, cars, items and NPCs get shoved (asset read 2026-10-02). Found once per storm.
        private static readonly List<Transform> _funnels = new List<Transform>();
        private static readonly Dictionary<int, Transform[]> _funnelsOf = new Dictionary<int, Transform[]>();
        internal const float BlastRadius = 150f;
        internal static bool AnyFunnel { get { return _funnels.Count > 0; } }
        private static PlayMakerArrayListProxy _list;
        private static float _nextFind;

        internal static bool Any { get { return _storms.Count > 0 || _dust; } }
        internal static int StormCount { get { return _storms.Count; } }
        internal static int FunnelCount { get { return _funnels.Count; } }
        internal static bool Dust { get { return _dust; } }
        internal static bool DustBridge { get { return _dustIntensity != null; } }

        private static readonly Dictionary<int, PlayMakerFSM> _sandPlayer = new Dictionary<int, PlayMakerFSM>();

        private static bool Parked(GameObject go)
        {
            PlayMakerFSM fsm;
            int id = go.GetInstanceID();
            if (!_sandPlayer.TryGetValue(id, out fsm))
            {
                fsm = null;
                foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "SandPlayer") { fsm = f; break; }
                _sandPlayer[id] = fsm;
            }
            return fsm != null && !fsm.enabled;
        }

        internal static void Reset() { _storms.Clear(); _funnels.Clear(); _funnelsOf.Clear(); _sandPlayer.Clear(); _list = null; _nextFind = 0f; _dust = false; }

        internal static void Refresh()
        {
            _storms.Clear(); _funnels.Clear();
            if (!_dustLooked) LookForDust();
            if (_dustIntensity != null)
            {
                try { _dust = _dustOn() && _dustActive(); }
                catch (Exception e) { DustOff(e); }
            }
            if (_list == null)
            {
                if (Time.unscaledTime < _nextFind) return;
                _nextFind = Time.unscaledTime + 10f;
                var reg = Core.GameRefs.NewGOArrayList;      // (WW) Core's cached lookup
                if (reg == null) return;
                foreach (var p in reg.GetComponents<PlayMakerArrayListProxy>())
                    if (p != null && p.referenceName == "ArrayList_Sandstorms") { _list = p; break; }
                if (_list == null) return;
            }
            var al = _list.arrayList;
            if (al == null) return;
            for (int i = 0; i < al.Count; i++)
            {
                var go = al[i] as GameObject;
                if (go != null && go.activeInHierarchy && (_dustIntensity == null || !Parked(go))) _storms.Add(go.transform);
            }
            foreach (var st in _storms)
            {
                Transform[] f;
                int id = st.GetInstanceID();
                if (!_funnelsOf.TryGetValue(id, out f))
                {
                    var l = new List<Transform>();
                    foreach (var t in st.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("TornadoPhysics", System.StringComparison.Ordinal)) l.Add(t);
                    f = l.ToArray();
                    _funnelsOf[id] = f;
                }
                foreach (var t in f) if (t != null && t.gameObject.activeInHierarchy) _funnels.Add(t);
            }
        }

        // within the shove radius of a tornado funnel (flat distance)
        internal static bool InBlast(Vector3 p)
        {
            if (_funnels.Count == 0) return false;
            float r2 = BlastRadius * BlastRadius;
            for (int i = 0; i < _funnels.Count; i++)
            {
                var t = _funnels[i];
                if (t == null) continue;
                Vector3 d = t.position - p; d.y = 0f;
                if (d.sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>Queries only the optional third-party dust provider, without reintroducing the native circle fallback.</summary>
        internal static bool InDust(Vector3 p)
        {
            if (_dust)
            {
                try { if (_dustIntensity(p.x, p.z) >= DustHazard) return true; }
                catch (Exception e) { DustOff(e); }
            }
            return false;
        }

        internal static bool In(Vector3 p)
        {
            if (InDust(p)) return true;
            if (_storms.Count == 0) return false;
            float r = NpcModule.StormRadius.Value, r2 = r * r;
            for (int i = 0; i < _storms.Count; i++)
            {
                var t = _storms[i];
                if (t == null) continue;
                Vector3 d = t.position - p; d.y = 0f;
                if (d.sqrMagnitude <= r2) return true;
            }
            return false;
        }
    }
}
