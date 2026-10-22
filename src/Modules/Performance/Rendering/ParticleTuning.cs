using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// EN5 (research C §1 "Particles", §2 Tier 2.8, §3). 454 prefab systems: 105 prewarm (55 Fire Quad, 16 each Smoke/Ashes/Flames
    /// Small: every POI fire simulates a full cycle on spawn), 125 AlwaysSimulate, 24 world collision High, 7 Lights module.
    /// Prewarm off (prefab assets of unsaved prefabs + instances not yet playing), looping AlwaysSimulate systems without
    /// sub-emitters pause off-screen (PauseAndCatchup or Pause), collision quality High -> Low, Lights module off, optional
    /// maxParticles cap. Saved prefabs (ES3Type_ParticleSystem writes main/collision/lights) are edited per instance only and
    /// reverted while a save runs. The player's own effects (muzzle flashes under PlayerCameraHolder) are left alone.
    /// </summary>
    public sealed class ParticleTuning : Feature, IScanClient, SaveGuard.IClient
    {
        public override string PlanId { get { return "EN5"; } }
        public override string Key { get { return "Particles"; } }
        public override string Title { get { return "Particle prewarm, off-screen culling, collision, lights"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 6; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Fires and smoke no longer pre-simulate when a camp spawns, pause off-screen, collide cheaper and drop their particle lights."; } }

        Knob _prewarm, _cull, _coll, _lights, _max;
        public override void Bind(ModuleConfig cfg)
        {
            _prewarm = NewKnob(cfg, "No prewarm", "1 = switch prewarm off.", 1, 1, 1, 0);
            _cull = NewKnob(cfg, "Off-screen culling", "Looping AlwaysSimulate systems: 0 keep, 1 PauseAndCatchup, 2 Pause.", 2, 1, 0, 0);
            _coll = NewKnob(cfg, "Low collision", "1 = world collision quality High -> Low.", 1, 1, 0, 0);
            _lights = NewKnob(cfg, "No particle lights", "1 = Lights module off.", 1, 1, 0, 0);
            _max = NewKnob(cfg, "Max particles", "Cap of maxParticles per system; 0 = no cap.", 200, 0, 0, 0);
        }

        struct Orig { public bool Prewarm, Lights; public ParticleSystemCullingMode Cull; public ParticleSystemCollisionQuality Coll; public int Max; }
        sealed class PRec { public ParticleSystem P; public bool Saved, Asset; public Orig O; }
        readonly Dictionary<int, PRec> _recs = new Dictionary<int, PRec>();
        readonly List<PRec> _list = new List<PRec>();
        readonly Dictionary<string, Orig> _assetOrig = new Dictionary<string, Orig>();
        public ScanTypes Types { get { return ScanTypes.Particles; } }

        protected override void OnEnable(FeatureMode mode) { SaveGuard.Register(this); RenderScan.Register(this); }

        protected override void OnDisable()
        {
            RenderScan.Unregister(this); SaveGuard.Unregister(this);
            for (int i = 0; i < _list.Count; i++) Restore(_list[i]);
            _list.Clear(); _recs.Clear(); _assetOrig.Clear();
        }

        public override void OnSettingsChanged()
        {
            for (int i = 0; i < _list.Count; i++) { var r = _list[i]; if (r.P == null || (r.Saved && SaveGuard.Reverted)) continue; Restore(r); Apply(r); }
        }

        public override void OnSceneLoaded() { RenderScan.RequestFull(); }
        public override void OnWorldReady() { RenderScan.RequestFull(); }

        static Orig Read(ParticleSystem p)
        {
            var m = p.main;
            return new Orig { Prewarm = m.prewarm, Cull = m.cullingMode, Coll = p.collision.quality, Lights = p.lights.enabled, Max = m.maxParticles };
        }

        public void OnParticles(ParticleSystem p, ref ScanCtx c)
        {
            if (c.Player) return;
            if (c.Asset && c.Saved) return;
            int id = p.GetInstanceID();
            if (_recs.ContainsKey(id)) return;
            var r = new PRec { P = p, Saved = c.Saved, Asset = c.Asset, O = Read(p) };
            if (c.Asset) { string key = RenderScan.Key(p.transform, c.Root); if (key != null) _assetOrig[key] = r.O; }
            else if (_assetOrig.Count > 0 && LooksEdited(r.O))
            {
                Orig o; string key = RenderScan.Key(p.transform, c.Root);
                if (key != null && _assetOrig.TryGetValue(key, out o)) r.O = o;   // inherited from an edited asset
            }
            _recs[id] = r; _list.Add(r);
            if (!(r.Saved && SaveGuard.Reverted)) Apply(r);
        }

        bool LooksEdited(Orig o) { return !o.Prewarm || !o.Lights; }

        void Apply(PRec r)
        {
            var p = r.P; if (p == null) return;
            var m = p.main;
            if (_prewarm.Value > 0.5f && r.O.Prewarm && !r.Saved && (r.Asset || !p.isPlaying) && m.prewarm) m.prewarm = false;
            int cull = _cull.Int;
            if (cull > 0 && r.O.Cull == ParticleSystemCullingMode.AlwaysSimulate && m.loop && p.subEmitters.subEmittersCount == 0)
            {
                var want = cull >= 2 ? ParticleSystemCullingMode.Pause : ParticleSystemCullingMode.PauseAndCatchup;
                if (m.cullingMode != want) m.cullingMode = want;
            }
            var col = p.collision;
            if (_coll.Value > 0.5f && col.enabled && r.O.Coll == ParticleSystemCollisionQuality.High && col.quality != ParticleSystemCollisionQuality.Low) col.quality = ParticleSystemCollisionQuality.Low;
            var li = p.lights;
            if (_lights.Value > 0.5f && r.O.Lights && li.enabled) li.enabled = false;
            int cap = _max.Int;
            if (cap > 0 && r.O.Max > cap && m.maxParticles != cap) m.maxParticles = cap;
        }

        static void Restore(PRec r)
        {
            var p = r.P; if (p == null) return;
            var m = p.main;
            if (m.prewarm != r.O.Prewarm && (r.Asset || !p.isPlaying)) m.prewarm = r.O.Prewarm;
            if (m.cullingMode != r.O.Cull) m.cullingMode = r.O.Cull;
            var col = p.collision; if (col.quality != r.O.Coll) col.quality = r.O.Coll;
            var li = p.lights; if (li.enabled != r.O.Lights) li.enabled = r.O.Lights;
            if (m.maxParticles != r.O.Max) m.maxParticles = r.O.Max;
        }

        public void RevertSaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Saved) Restore(_list[i]); }
        public void ReapplySaved() { for (int i = 0; i < _list.Count; i++) if (_list[i].Saved) Apply(_list[i]); }

        public void OnLight(Light l, ref ScanCtx c) { }
        public void OnRenderer(MeshRenderer r, ref ScanCtx c) { }
        public void OnAnimator(Animator a, ref ScanCtx c) { }
        public void OnRoot(Transform root, ref ScanCtx c) { }
        public void OnPassDone()
        {
            for (int i = _list.Count - 1; i >= 0; i--) if (_list[i].P == null) _list.RemoveAt(i);
            if (_recs.Count != _list.Count) { _recs.Clear(); for (int i = 0; i < _list.Count; i++) _recs[_list[i].P.GetInstanceID()] = _list[i]; }
        }

        public override void DrawStats(ToolLayout l) { l.Value("Particle systems", _list.Count + " tracked, " + _assetOrig.Count + " prefab systems"); }
    }
}
