using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// EN2, exact part (research C §0, §2 Tier 1.2). WW does not reference Unity.Postprocessing.Runtime, so every PPv2 member is
    /// resolved by reflection once and called through generated (MonoMod) delegates (no per-frame reflection).
    /// 1. PPU ScreenSpaceShadows (global volume "__PostProcessing__/PP screen space shadows", on by default, toggled by the
    ///    Graphics_Canvas "screen space shadows" FSM with ActivateGameObject: Sources fsm/scene1_game/Graphics_Canvas.md:L174-190)
    ///    renders 3 full-screen blits + 2 screen-size temporary RTs + Shader.Find every frame although its Intensity is 0
    ///    (ScreenSpaceShadowsRenderer.Render; settings default Intensity 0, asset sharedassets1:103130). A postfix on
    ///    PostProcessEffectSettings.IsEnabledAndSupported reports such an effect as disabled while its blended Intensity <= 0.001
    ///    (the final pass composites source with the shadow term scaled by Intensity). Because it reads the live blended value
    ///    every frame, the menu toggle and any volume change are honoured without re-applying anything.
    /// 2. PPU Sharpen allocates an unused screen-size temporary RT and calls Shader.Find every frame (SharpenRenderer.Render).
    ///    The prefix renders the same single blit (same sheet, same _Intensity, same pass 0, clear=false) with a cached shader.
    /// </summary>
    public sealed class PostFxExact : Feature
    {
        public override string PlanId { get { return "EN2"; } }
        public override string Key { get { return "Dead post effects"; } }
        public override string Title { get { return "Skip zero-intensity screen-space shadows, lean Sharpen"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 2; } }
        public override string Help { get { return "Exact: no visible change. Skips the PPU screen-space-shadow effect while its intensity is 0 and renders Sharpen without its unused render texture."; } }

        internal static bool On;
        static Type _sssType, _sharpenType;
        static Func<object, float> _sssIntensity;
        static FieldInfo _sssRt0, _sssRt1, _sharpRt0;
        static Func<object, object> _sharpenSettings;
        static Action<object, object, Shader> _sharpenRender;
        static Shader _sharpenShader;
        static object _lastSss, _lastSharp;
        public static long Skipped, SharpFast;

        protected override void OnEnable(FeatureMode mode)
        {
            if (!Resolve()) throw new Exception("PostProcessing types not found");
            var isEnabled = AccessTools.Method(AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PostProcessEffectSettings"), "IsEnabledAndSupported");
            if (!EnsurePatched(isEnabled, postfix: AccessTools.Method(typeof(PostFxExact), "IsEnabledPost"))) throw new Exception("IsEnabledAndSupported patch failed");
            if (_sharpenRender != null)
                EnsurePatched(AccessTools.Method(_sharpenType, "Render"), prefix: AccessTools.Method(typeof(PostFxExact), "SharpenPre"));
            _lastSss = _lastSharp = null;
            On = true;
        }

        protected override void OnDisable() { On = false; _lastSss = _lastSharp = null; }

        static bool _resolved, _ok;
        static bool Resolve()
        {
            if (_resolved) return _ok;
            _resolved = true;
            try
            {
                _sssType = AccessTools.TypeByName("DawidMoza.PostProcessingUltimate.ScreenSpaceShadows");
                _sharpenType = AccessTools.TypeByName("DawidMoza.PostProcessingUltimate.SharpenRenderer");
                var sharpSettingsType = AccessTools.TypeByName("DawidMoza.PostProcessingUltimate.Sharpen");
                if (_sssType == null) return _ok = false;
                // (object s) => ((ScreenSpaceShadows)s).Intensity.value, generated with MonoMod (Expression.Compile is an
                // interpreter in this game's stripped runtime: slower than reflection and allocating)
                var fInt = AccessTools.Field(_sssType, "Intensity");
                var fVal = fInt != null ? AccessTools.Field(fInt.FieldType, "value") : null;
                var gInt = fInt != null ? FastReflect.BoxedGetter(fInt) : null;
                var gVal = fVal != null ? FastReflect.TypedGetter<float>(fVal) : null;
                if (gInt == null || gVal == null) return _ok = false;
                _sssIntensity = s => gVal(gInt(s));
                _sssRt0 = AccessTools.Field(_sssType, "tempRT0"); _sssRt1 = AccessTools.Field(_sssType, "tempRT1");
                _ok = true;
                try { BuildSharpen(sharpSettingsType); }
                catch (Exception e) { _sharpenRender = null; Log.Warn("[Performance] EN2 Sharpen fast path unavailable: " + e.Message); }
            }
            catch (Exception e) { Log.Warn("[Performance] EN2 post-processing reflection failed: " + e.Message); _ok = false; }
            return _ok;
        }

        static void BuildSharpen(Type sharpSettingsType)
        {
            if (_sharpenType == null || sharpSettingsType == null) return;
            var ctxType = AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PostProcessRenderContext");
            var sheetType = AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PropertySheet");
            var utils = AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.RuntimeUtilities");
            var rti = typeof(UnityEngine.Rendering.RenderTargetIdentifier);
            MethodInfo blit = null;
            foreach (var m in utils.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "BlitFullscreenTriangle") continue;
                var ps = m.GetParameters();
                if (ps.Length == 8 && ps[0].ParameterType == typeof(UnityEngine.Rendering.CommandBuffer) && ps[1].ParameterType == rti && ps[2].ParameterType == rti
                    && ps[3].ParameterType == sheetType && ps[4].ParameterType == typeof(int) && ps[5].ParameterType == typeof(bool)
                    && ps[6].ParameterType == typeof(Rect?) && ps[7].ParameterType == typeof(bool)) { blit = m; break; }
            }
            if (blit == null) throw new Exception("BlitFullscreenTriangle overload not found");
            var getSheet = AccessTools.Method(AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PropertySheetFactory"), "Get", new[] { typeof(Shader) });
            var setFloat = typeof(MaterialPropertyBlock).GetMethod("SetFloat", new[] { typeof(string), typeof(float) });
            // (object renderer, object ctx, Shader shader) =>
            // { var sheet = ctx.propertySheets.Get(shader); sheet.properties.SetFloat("_Intensity", renderer.settings.Intensity.value);
            //   RuntimeUtilities.BlitFullscreenTriangle(ctx.command, ctx.source, ctx.destination, sheet, 0, false, null, false); }
            // Generated IL (MonoMod DynamicMethodDefinition): Reflection.Emit is stripped and Expression.Compile interprets here.
            var settingsProp = AccessTools.Property(_sharpenType, "settings");
            var fIntensity = AccessTools.Field(sharpSettingsType, "Intensity");
            var fValue = fIntensity != null ? AccessTools.Field(fIntensity.FieldType, "value") : null;
            MethodInfo gSheets = Getter(ctxType, "propertySheets"), gProps = Getter(sheetType, "properties"), gSettings = settingsProp != null ? settingsProp.GetGetMethod(true) : null,
                gCmd = Getter(ctxType, "command"), gSrc = Getter(ctxType, "source"), gDst = Getter(ctxType, "destination");
            if (getSheet == null || setFloat == null || fValue == null || gSheets == null || gProps == null || gSettings == null || gCmd == null || gSrc == null || gDst == null)
                throw new Exception("Sharpen members not found");
            var dm = new FastReflect.Dm("WW_SharpenRender", typeof(void), new[] { typeof(object), typeof(object), typeof(Shader) });
            var il = dm.GetILGenerator();
            var lCtx = il.DeclareLocal(ctxType); var lSheet = il.DeclareLocal(sheetType); var lRect = il.DeclareLocal(typeof(Rect?));
            il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Castclass, ctxType); il.Emit(OpCodes.Stloc, lCtx);
            il.Emit(OpCodes.Ldloc, lCtx); il.Emit(OpCodes.Callvirt, gSheets); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Callvirt, getSheet); il.Emit(OpCodes.Stloc, lSheet);
            il.Emit(OpCodes.Ldloc, lSheet); il.Emit(OpCodes.Callvirt, gProps); il.Emit(OpCodes.Ldstr, "_Intensity");
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, _sharpenType); il.Emit(OpCodes.Callvirt, gSettings);
            il.Emit(OpCodes.Ldfld, fIntensity); il.Emit(OpCodes.Ldfld, fValue);
            il.Emit(OpCodes.Callvirt, setFloat);
            il.Emit(OpCodes.Ldloc, lCtx); il.Emit(OpCodes.Callvirt, gCmd);
            il.Emit(OpCodes.Ldloc, lCtx); il.Emit(OpCodes.Callvirt, gSrc);
            il.Emit(OpCodes.Ldloc, lCtx); il.Emit(OpCodes.Callvirt, gDst);
            il.Emit(OpCodes.Ldloc, lSheet); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldloca, lRect); il.Emit(OpCodes.Initobj, typeof(Rect?)); il.Emit(OpCodes.Ldloc, lRect);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Call, blit);
            il.Emit(OpCodes.Ret);
            _sharpenRender = (Action<object, object, Shader>)dm.CreateDelegate(typeof(Action<object, object, Shader>));
            _sharpenSettings = FastReflect.BoxedGetter(settingsProp);
            if (_sharpenSettings == null) { _sharpenRender = null; throw new Exception("Sharpen settings getter not compilable"); }
            _sharpRt0 = AccessTools.Field(sharpSettingsType, "tempRT0");
        }

        static MethodInfo Getter(Type t, string prop)
        {
            var p = t != null ? AccessTools.Property(t, prop) : null;
            return p != null ? p.GetGetMethod(true) : null;
        }

        static void ReleaseRt(FieldInfo f, object owner)
        {
            if (f == null || owner == null) return;
            var rt = f.GetValue(owner) as RenderTexture;
            if (rt != null) { RenderTexture.ReleaseTemporary(rt); f.SetValue(owner, null); }
        }

        // PostProcessEffectSettings.IsEnabledAndSupported(PostProcessRenderContext): virtual on the base, not overridden by ScreenSpaceShadows.
        static void IsEnabledPost(object __instance, ref bool __result)
        {
            if (!On || !__result || __instance == null || __instance.GetType() != _sssType) return;
            if (_sssIntensity(__instance) > 0.001f) { if (ReferenceEquals(_lastSss, __instance)) _lastSss = null; return; }
            __result = false; Skipped++;
            if (!ReferenceEquals(_lastSss, __instance))
            {
                // first skipped frame for this settings instance: hand back the two RTs the last vanilla Render kept
                _lastSss = __instance;
                try { ReleaseRt(_sssRt0, __instance); ReleaseRt(_sssRt1, __instance); } catch (Exception e) { Log.Warn("[Performance] EN2 release: " + e.Message); }
            }
        }

        static bool SharpenPre(object __instance, object context)
        {
            if (!On || _sharpenRender == null) return true;
            if (_sharpenShader == null) { _sharpenShader = Shader.Find("PPU/Sharpen"); if (_sharpenShader == null) return true; }
            var settings = _sharpenSettings(__instance);
            if (!ReferenceEquals(settings, _lastSharp)) { _lastSharp = settings; try { ReleaseRt(_sharpRt0, settings); } catch { } }
            _sharpenRender(__instance, context, _sharpenShader);
            SharpFast++;
            return false;
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Screen-space shadow frames skipped", Skipped.ToString());
            l.Value("Sharpen fast frames", _sharpenRender == null ? "unavailable" : SharpFast.ToString());
        }
    }

    /// <summary>
    /// EN2, preset part. PlayerCamera and all 14 vehicle 3rd-person cameras run PPv2 TAA (antialiasingMode 3) with
    /// stationaryBlending 0 and motionBlending 0 (Sources data/hierarchy/scene1_game/PlayerCameraHolder.txt:L9,
    /// prefabs/R/Rustallion.txt:L63): jitter + motion vectors + resolve without any history blend = no anti-aliasing. Such a
    /// layer gets SMAA (Low: FXAA fast); stopNaN propagation off in Low/Balanced. PostProcessLayer is not written by ES3
    /// (no ES3Type for it) and no FSM touches it (Sources fsm/: no antialiasing/PostProcessLayer reference).
    /// </summary>
    public sealed class AntiAliasing : Feature
    {
        public override string PlanId { get { return "EN2"; } }
        public override string Key { get { return "Anti-aliasing"; } }
        public override string Title { get { return "Replace the no-op TAA with SMAA / FXAA"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 3; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "The game's TAA has zero blending (costs time, smooths nothing). Performance uses FXAA, Balanced/Visuals SMAA."; } }

        Knob _mode, _quality, _nan;
        public override void Bind(ModuleConfig cfg)
        {
            _mode = NewKnob(cfg, "Mode", "1 = FXAA, 2 = SMAA, 3 = keep TAA.", 1, 2, 2, 3);
            _quality = NewKnob(cfg, "SMAA quality", "0 Low, 1 Medium, 2 High.", 0, 1, 2, 0);
            _nan = NewKnob(cfg, "Stop NaN", "1 = keep PPv2 stopNaN propagation.", 0, 0, 1, 1);
        }

        sealed class Rec { public Component Layer; public int Mode, Quality; public bool Fast, Nan; }
        readonly Dictionary<int, Rec> _recs = new Dictionary<int, Rec>();
        Job _job; int _version = -1;
        static Type _layerType;
        static FieldInfo _fMode, _fNan, _fTaa, _fStat, _fMotion, _fSmaa, _fSmaaQ, _fFxaa, _fFast;

        protected override void OnEnable(FeatureMode mode)
        {
            if (!Resolve()) throw new Exception("PostProcessLayer not found");
            _version = -1;
            _job = Scheduler.Interval(Module, "Performance.AntiAliasing", 1f, Tick);
            Tick();
        }

        protected override void OnDisable()
        {
            if (_job != null) { _job.Remove(); _job = null; }
            foreach (var r in _recs.Values) { if (r.Layer != null) Write(r.Layer, r.Mode, r.Quality, r.Fast, r.Nan); }
            _recs.Clear();
        }

        public override void OnSettingsChanged()
        {
            foreach (var r in _recs.Values) if (r.Layer != null) ApplyTo(r);
        }

        static bool Resolve()
        {
            if (_layerType != null) return true;
            var t = AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
            if (t == null) return false;
            _fMode = AccessTools.Field(t, "antialiasingMode"); _fNan = AccessTools.Field(t, "stopNaNPropagation");
            _fTaa = AccessTools.Field(t, "temporalAntialiasing"); _fSmaa = AccessTools.Field(t, "subpixelMorphologicalAntialiasing"); _fFxaa = AccessTools.Field(t, "fastApproximateAntialiasing");
            _fStat = AccessTools.Field(_fTaa.FieldType, "stationaryBlending"); _fMotion = AccessTools.Field(_fTaa.FieldType, "motionBlending");
            _fSmaaQ = AccessTools.Field(_fSmaa.FieldType, "quality"); _fFast = AccessTools.Field(_fFxaa.FieldType, "fastMode");
            _layerType = t;
            return true;
        }

        void Tick()
        {
            GameCameras.Refresh();
            if (GameCameras.Version == _version) return;
            _version = GameCameras.Version;
            var list = GameCameras.List;
            for (int i = 0; i < list.Count; i++)
            {
                var cam = list[i];
                if (cam == null || _recs.ContainsKey(cam.GetInstanceID())) continue;
                var layer = cam.GetComponent(_layerType);
                if (layer == null) continue;
                var r = new Rec { Layer = layer, Mode = Convert.ToInt32(_fMode.GetValue(layer)), Nan = (bool)_fNan.GetValue(layer) };
                var smaa = _fSmaa.GetValue(layer); var fxaa = _fFxaa.GetValue(layer);
                r.Quality = smaa != null ? Convert.ToInt32(_fSmaaQ.GetValue(smaa)) : 0;
                r.Fast = fxaa != null && (bool)_fFast.GetValue(fxaa);
                _recs[cam.GetInstanceID()] = r;
                ApplyTo(r);
            }
        }

        void ApplyTo(Rec r)
        {
            int mode = r.Mode, q = r.Quality; bool fast = r.Fast;
            if (r.Mode == 3 && DeadTaa(r.Layer))
            {
                int want = _mode.Int;
                if (want == 1) { mode = 1; fast = true; }
                else if (want == 2) { mode = 2; q = Mathf.Clamp(_quality.Int, 0, 2); }
            }
            bool nan = r.Nan && _nan.Value > 0.5f;
            Write(r.Layer, mode, q, fast, nan);
        }

        static bool DeadTaa(Component layer)
        {
            var taa = _fTaa.GetValue(layer);
            if (taa == null) return false;
            return (float)_fStat.GetValue(taa) <= 0.001f && (float)_fMotion.GetValue(taa) <= 0.001f;
        }

        static void Write(Component layer, int mode, int quality, bool fast, bool nan)
        {
            _fMode.SetValue(layer, Enum.ToObject(_fMode.FieldType, mode));
            _fNan.SetValue(layer, nan);
            var smaa = _fSmaa.GetValue(layer); if (smaa != null) _fSmaaQ.SetValue(smaa, Enum.ToObject(_fSmaaQ.FieldType, quality));
            var fxaa = _fFxaa.GetValue(layer); if (fxaa != null) _fFast.SetValue(fxaa, fast);
        }

        public override void DrawStats(ToolLayout l) { l.Value("Cameras", _recs.Count.ToString()); }
    }
}
