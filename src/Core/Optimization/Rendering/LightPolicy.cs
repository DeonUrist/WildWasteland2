using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Core.Optimization
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
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "EN1"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Light shadows"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Point-light shadows, budget and distance culling"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.Rendering; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 1; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "Point lights lose their cube shadows except the nearest few; far point lights are switched off; headlights reach less far; fewer per-pixel lights."; } }

        Knob _count, _radius, _res, _cullFactor, _cullMin, _pixel, _headRange, _headShadow;

        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
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
        /// <summary>Retains an unsaved prefab light's exact original shadow mode for restoration.</summary>
        sealed class ARec { public Light L; public LightShadows Orig; }

        internal static bool On;
        static LightPolicy _inst;
        readonly Dictionary<int, LRec> _recs = new Dictionary<int, LRec>();
        readonly List<LRec> _list = new List<LRec>();
        readonly List<ARec> _assets = new List<ARec>();
        readonly Dictionary<string, LightShadows> _assetOrig = new Dictionary<string, LightShadows>();
        readonly HashSet<int> _assetIds = new HashSet<int>();
        Job _job;
        float _nextPrune;
        // budget: indices of the nearest candidates (small fixed arrays, no allocation)
        readonly LRec[] _best = new LRec[16];
        readonly float[] _bestD = new float[16];
        public static int Shadowed, CulledCount, Live;

        /// <summary>Requests light components from the shared rendering scan.</summary>
        public ScanTypes Types { get { return ScanTypes.Lights; } }

        /// <summary>Activates this feature's replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            _inst = this; On = true;
            QualityGuard.Set(QualityGuard.Q.PixelLights, _pixel.Value);
            SaveGuard.Register(this);
            RenderScan.Register(this);
            _job = Scheduler.Interval(Module, "Core.Optimization.LightBudget", 0.25f, Tick);
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
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

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged()
        {
            QualityGuard.Set(QualityGuard.Q.PixelLights, _pixel.Value);
            if (_job != null) _job.RunNow();
        }

        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { GameCameras.Clear(); RenderScan.RequestFull(); }
        /// <summary>Refreshes this feature after native gameplay objects have finished loading.</summary>
        public override void OnWorldReady() { RenderScan.RequestFull(); }

        /// <summary>For CT6 (LightFlicker manager): true while EN1 has switched this light off for distance.</summary>
        internal static bool IsCulled(Light l)
        {
            var p = _inst; LRec r;
            return p != null && l != null && p._recs.TryGetValue(l.GetInstanceID(), out r) && r.Culled;
        }

        /// <summary>Recognizes native tiny-range spotlights that must not become budgeted point lights.</summary>
        static bool SmallSpot(Light l) { return l.type == LightType.Spot && l.range < 1f; }

        /// <summary>Shared scan: records eligible light originals and separates prefab/saved/vehicle/player treatment.</summary>
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

        /// <summary>This scan client does not modify mesh renderers.</summary>
        public void OnRenderer(MeshRenderer r, ref ScanCtx c) { }
        /// <summary>This scan client does not modify particle systems.</summary>
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        /// <summary>This scan client does not modify animators.</summary>
        public void OnAnimator(Animator a, ref ScanCtx c) { }
        /// <summary>This scan client does not consume root geometry.</summary>
        public void OnRoot(Transform root, ref ScanCtx c) { }
        /// <summary>Prunes destroyed light records after shared discovery finishes.</summary>
        public void OnPassDone() { Prune(); }

        /// <summary>Compacts dead light records and removes their instance ids from the lookup.</summary>
        void Prune()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                if (_list[i].L != null) continue;
                _list.RemoveAt(i);
            }
            if (_recs.Count != _list.Count) { _recs.Clear(); for (int i = 0; i < _list.Count; i++) _recs[_list[i].L.GetInstanceID()] = _list[i]; }
        }

        /// <summary>Restores the captured shadow, resolution and range fields of one still-live light.</summary>
        void Restore(LRec r)
        {
            var l = r.L;
            if (l == null) return;
            if (l.shadows != r.OrigShadows) l.shadows = r.OrigShadows;
            if (l.shadowCustomResolution != r.OrigRes) l.shadowCustomResolution = r.OrigRes;
            if (r.Head && !Mathf.Approximately(l.range, r.OrigRange)) l.range = r.OrigRange;
            if (r.Culled) { l.enabled = true; r.Culled = false; }
        }

        /// <summary>Restores the original fields of saved objects before serialization.</summary>
        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Saved) Restore(_list[i]); }
        /// <summary>Reapplies active optimization settings after serialization releases saved objects.</summary>
        public void ReapplySaved() { if (_job != null) _job.RunNow(); }

        /// <summary>0.25 s: distance culling, shadow budget, headlight range/shadow. Writes a Light property only when it changes.</summary>
        void Tick()
        {
            if (!On || SaveGuard.Reverted) return;
            var cam = GameRefs.MainCamera;
            if (cam == null) return;
            if (Time.unscaledTime >= _nextPrune) { _nextPrune = Time.unscaledTime + 10f; Prune(); }
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

        /// <summary>Writes a light's shadow mode/resolution only when the chosen budget result differs.</summary>
        static void SetShadow(LRec r, bool on, int res)
        {
            var l = r.L;
            var want = on ? r.OrigShadows : LightShadows.None;
            if (l.shadows != want) { l.shadows = want; r.Touched = true; }
            int wr = on && res > 0 ? res : r.OrigRes;
            if (l.shadowCustomResolution != wr) l.shadowCustomResolution = wr;
        }

        /// <summary>Draws this feature's current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            l.Value("Lights", Live + " tracked, " + Shadowed + " shadowed, " + CulledCount + " switched off, " + _assets.Count + " prefab lights edited");
            l.Value("Pixel lights", QualityGuard.Describe(QualityGuard.Q.PixelLights));
            RenderScan.Stats(l);
        }
    }
}
