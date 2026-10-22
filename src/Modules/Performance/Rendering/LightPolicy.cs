using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// EN1 (plan §5, research C §1 "Lights", §2 Tier 1.1, §3 presets). 169/178 prefab point lights cast 6-face cube shadows (fire_effect
    /// 2 m, Light_Flicker 1.5-15 m, gauge/bulb lights 0.1-0.15 m: Sources data/hierarchy/prefabs/G/gauge_rpm.txt, H/Headlight.txt) and
    /// headlight spots reach 200-300 m with shadows; Forward + Ultra pixelLightCount 20 adds a pass per lit object per light.
    /// Policy: point lights and spots shorter than 1 m lose their shadows (prefab assets of unsaved prefabs + every live instance);
    /// a 0.25 s budget gives the shadow back to the N nearest qualifying point lights (range >= 3 m, originally shadowed) within
    /// the budget radius at a fixed resolution; point lights beyond max(min, factor x range) from the camera are disabled
    /// (Light.enabled only: no FSM touches Light.enabled - Sources fsm/: no SetProperty/ActivateComponent on UnityEngine.Light;
    /// their LightFlicker FSMs keep running and are CT6's business); headlight spots get a shorter range and shadows only within
    /// 8 m of the camera (= the player's own car). pixelLightCount is capped through QualityGuard.
    /// Never touched: lights under Player / PlayerCameraHolder (flashlight, PlayerBody glow), directional/area lights, other spots.
    /// Saved objects (Headlight, gauges, tin_can_light, lamps, weapons: ES3Prefab) are edited per instance only and reverted while
    /// a save runs (ES3Type_Light writes shadows/range/enabled). WW Patrols reads headlight Light.enabled (PatrolVisibility.LightsOn):
    /// headlights are never distance-disabled.
    /// </summary>
    public sealed class LightPolicy : Feature, IScanClient, SaveGuard.IClient
    {
        public override string PlanId { get { return "EN1"; } }
        public override string Key { get { return "Light shadows"; } }
        public override string Title { get { return "Point-light shadows, budget and distance culling"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 1; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Point lights lose their cube shadows except the nearest few; far point lights are switched off; headlights reach less far; fewer per-pixel lights."; } }

        Knob _count, _radius, _res, _cullFactor, _cullMin, _pixel, _headRange, _headShadow;

        public override void Bind(ModuleConfig cfg)
        {
            _count = NewKnob(cfg, "Shadowed point lights", "Point lights that keep their shadow (nearest first).", 0, 2, 4, 99);
            _radius = NewKnob(cfg, "Shadow radius", "Only point lights within this distance (m) keep a shadow.", 0, 25, 35, 10000);
            _res = NewKnob(cfg, "Shadow resolution", "Shadow map size (px) of the kept point-light shadows; 0 = quality default.", 256, 256, 512, 0);
            _cullFactor = NewKnob(cfg, "Cull factor", "Point lights farther than factor x range are switched off; 0 = never.", 12, 20, 0, 0);
            _cullMin = NewKnob(cfg, "Cull minimum", "Minimum distance (m) before a point light is switched off.", 60, 0, 0, 0);
            _pixel = NewKnob(cfg, "Pixel lights", "QualitySettings.pixelLightCount cap.", 4, 6, 8, 20);
            _headRange = NewKnob(cfg, "Headlight range", "Range (m) of headlight spot lights (vanilla 200-300).", 60, 80, 120, 300);
            _headShadow = NewKnob(cfg, "Headlight shadow", "1 = headlights within 8 m of the camera (your car) keep their shadow.", 0, 1, 1, 1);
        }

        sealed class LRec
        {
            public Light L; public Transform T; public bool Saved, Head, Point, Eligible, Culled, Touched;
            public LightShadows OrigShadows; public int OrigRes; public float OrigRange;
        }
        sealed class ARec { public Light L; public LightShadows Orig; }

        internal static bool On;
        static LightPolicy _inst;
        readonly Dictionary<int, LRec> _recs = new Dictionary<int, LRec>();
        readonly List<LRec> _list = new List<LRec>();
        readonly List<ARec> _assets = new List<ARec>();
        readonly Dictionary<string, LightShadows> _assetOrig = new Dictionary<string, LightShadows>();
        readonly HashSet<int> _assetIds = new HashSet<int>();
        Job _job;
        float _nextRescan;
        // budget: indices of the nearest candidates (small fixed arrays, no allocation)
        readonly LRec[] _best = new LRec[16];
        readonly float[] _bestD = new float[16];
        public static int Shadowed, CulledCount, Live;

        public ScanTypes Types { get { return ScanTypes.Lights; } }

        protected override void OnEnable(FeatureMode mode)
        {
            _inst = this; On = true;
            QualityGuard.Set(QualityGuard.Q.PixelLights, _pixel.Value);
            SaveGuard.Register(this);
            RenderScan.Register(this);
            _job = Scheduler.Interval(Module, "Performance.LightBudget", 0.25f, Tick);
        }

        protected override void OnDisable()
        {
            On = false;
            if (_job != null) { _job.Remove(); _job = null; }
            RenderScan.Unregister(this);
            SaveGuard.Unregister(this);
            QualityGuard.Release(QualityGuard.Q.PixelLights);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            for (int i = 0; i < _assets.Count; i++) { var a = _assets[i]; if (a.L != null) a.L.shadows = a.Orig; }
            _list.Clear(); _recs.Clear(); _assets.Clear(); _assetIds.Clear(); _assetOrig.Clear();
            Shadowed = CulledCount = Live = 0;
            if (_inst == this) _inst = null;
        }

        public override void OnSettingsChanged()
        {
            QualityGuard.Set(QualityGuard.Q.PixelLights, _pixel.Value);
            if (_job != null) _job.RunNow();
        }

        // one subscriber for every Rendering feature: the handler is a no-op while no scan client runs
        public override void SubscribeHooks() { Hooks.CreateObjectEnter.Postfix(Module, RenderScan.AfterCreateObject); }

        public override void OnSceneLoaded() { GameCameras.Clear(); RenderScan.RequestFull(); }
        public override void OnWorldReady() { RenderScan.RequestFull(); }

        /// <summary>For CT6 (LightFlicker manager): true while EN1 has switched this light off for distance.</summary>
        internal static bool IsCulled(Light l)
        {
            var p = _inst; LRec r;
            return p != null && l != null && p._recs.TryGetValue(l.GetInstanceID(), out r) && r.Culled;
        }

        static bool SmallSpot(Light l) { return l.type == LightType.Spot && l.range < 1f; }

        public void OnLight(Light l, ref ScanCtx c)
        {
            if (c.Player) return;
            var type = l.type;
            if (type != LightType.Point && type != LightType.Spot) return;
            if (c.Asset)
            {
                // prefab asset of an unsaved prefab (POIs, fire/flicker effects): later spawns inherit "no shadow"
                if (c.Saved || l.shadows == LightShadows.None) return;
                if (type == LightType.Spot && !SmallSpot(l)) return;
                if (!_assetIds.Add(l.GetInstanceID())) return;
                _assets.Add(new ARec { L = l, Orig = l.shadows });
                string key = RenderScan.Key(l.transform, c.Root);
                if (key != null) _assetOrig[key] = l.shadows;
                l.shadows = LightShadows.None;
                return;
            }
            int id = l.GetInstanceID();
            if (_recs.ContainsKey(id)) return;
            bool head = type == LightType.Spot && l.range >= 30f;
            if (type == LightType.Spot && !head && !SmallSpot(l)) return;   // other spots (flashlight-like, scene spots) untouched
            var r = new LRec { L = l, T = l.transform, Saved = c.Saved, Point = type == LightType.Point, Head = head, OrigRes = l.shadowCustomResolution, OrigRange = l.range };
            r.OrigShadows = l.shadows;
            if (r.OrigShadows == LightShadows.None && _assetOrig.Count > 0)
            {
                // inherited "no shadow" from an asset we edited: the vanilla value is the asset's original
                LightShadows o; string key = RenderScan.Key(l.transform, c.Root);
                if (key != null && _assetOrig.TryGetValue(key, out o)) r.OrigShadows = o;
            }
            r.Eligible = r.Point && r.OrigRange >= 3f && r.OrigShadows != LightShadows.None;
            _recs[id] = r; _list.Add(r);
            if (!(r.Saved && SaveGuard.Reverted) && l.shadows != LightShadows.None) { l.shadows = LightShadows.None; r.Touched = true; }
        }

        public void OnRenderer(MeshRenderer r, ref ScanCtx c) { }
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        public void OnAnimator(Animator a, ref ScanCtx c) { }
        public void OnRoot(Transform root, ref ScanCtx c) { }
        public void OnPassDone() { Prune(); }

        void Prune()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                if (_list[i].L != null) continue;
                _list.RemoveAt(i);
            }
            if (_recs.Count != _list.Count) { _recs.Clear(); for (int i = 0; i < _list.Count; i++) _recs[_list[i].L.GetInstanceID()] = _list[i]; }
        }

        void Restore(LRec r)
        {
            var l = r.L;
            if (l == null) return;
            if (l.shadows != r.OrigShadows) l.shadows = r.OrigShadows;
            if (l.shadowCustomResolution != r.OrigRes) l.shadowCustomResolution = r.OrigRes;
            if (r.Head && !Mathf.Approximately(l.range, r.OrigRange)) l.range = r.OrigRange;
            if (r.Culled) { l.enabled = true; r.Culled = false; }
        }

        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Saved) Restore(_list[i]); }
        public void ReapplySaved() { if (_job != null) _job.RunNow(); }

        /// <summary>0.25 s: distance culling, shadow budget, headlight range/shadow. Writes a Light property only when it changes.</summary>
        void Tick()
        {
            if (!On || SaveGuard.Reverted) return;
            var cam = GameRefs.MainCamera;
            if (cam == null) return;
            float now = Time.unscaledTime;
            if (now >= _nextRescan)
            {
                // spawns outside CreateObject (ES3 loads between passes, other mods' Instantiate): cheap native list of active lights
                _nextRescan = now + 10f;
                var all = UnityEngine.Object.FindObjectsOfType<Light>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (_recs.ContainsKey(all[i].GetInstanceID())) continue;
                    ScanCtx c;
                    if (RenderScan.Ctx(all[i].transform, out c)) OnLight(all[i], ref c);
                }
                Prune();
            }
            Vector3 cp = cam.transform.position;
            int n = Mathf.Clamp(_count.Int, 0, _best.Length);
            float radius = _radius.Value, factor = _cullFactor.Value, cullMin = _cullMin.Value, headRange = _headRange.Value;
            bool headShadow = _headShadow.Value > 0.5f;
            int res = _res.Int, found = 0, culled = 0, shadowed = 0;
            for (int i = 0; i < n; i++) { _best[i] = null; _bestD[i] = float.MaxValue; }
            for (int i = 0; i < _list.Count; i++)
            {
                var r = _list[i]; var l = r.L;
                if (l == null) continue;
                float d = (r.T.position - cp).magnitude;
                if (r.Point && factor > 0f)
                {
                    float limit = Mathf.Max(cullMin, factor * r.OrigRange);
                    if (!r.Culled && d > limit && l.enabled) { l.enabled = false; r.Culled = true; }
                    else if (r.Culled && d < limit * 0.9f) { l.enabled = true; r.Culled = false; }
                }
                else if (r.Culled) { l.enabled = true; r.Culled = false; }
                if (r.Culled) { culled++; continue; }
                if (r.Head)
                {
                    float want = Mathf.Min(r.OrigRange, headRange);
                    if (!Mathf.Approximately(l.range, want)) l.range = want;
                    bool sh = headShadow && d < 8f && r.OrigShadows != LightShadows.None;
                    SetShadow(r, sh, 512);
                    if (sh) shadowed++;
                    continue;
                }
                if (r.Eligible && d <= radius && n > 0 && l.isActiveAndEnabled)
                {
                    // insertion into the n nearest
                    if (d < _bestD[n - 1])
                    {
                        int k = n - 1;
                        while (k > 0 && _bestD[k - 1] > d) { _best[k] = _best[k - 1]; _bestD[k] = _bestD[k - 1]; k--; }
                        _best[k] = r; _bestD[k] = d;
                        if (found < n) found++;
                    }
                }
            }
            for (int i = 0; i < _list.Count; i++)
            {
                var r = _list[i];
                if (r.L == null || r.Head || r.Culled) continue;
                bool keep = false;
                for (int k = 0; k < found; k++) if (ReferenceEquals(_best[k], r)) { keep = true; break; }
                SetShadow(r, keep, res);
                if (keep) shadowed++;
            }
            Shadowed = shadowed; CulledCount = culled; Live = _list.Count;
        }

        static void SetShadow(LRec r, bool on, int res)
        {
            var l = r.L;
            var want = on ? r.OrigShadows : LightShadows.None;
            if (l.shadows != want) { l.shadows = want; r.Touched = true; }
            int wr = on && res > 0 ? res : r.OrigRes;
            if (l.shadowCustomResolution != wr) l.shadowCustomResolution = wr;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Lights", Live + " tracked, " + Shadowed + " shadowed, " + CulledCount + " switched off, " + _assets.Count + " prefab lights edited");
            l.Value("Pixel lights", QualityGuard.Describe(QualityGuard.Q.PixelLights));
            RenderScan.Stats(l);
        }
    }
}
