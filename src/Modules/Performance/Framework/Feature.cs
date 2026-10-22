using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>Per-feature switch. Shadow (only where <see cref="Feature.SupportsShadow"/>): the vanilla path keeps running and the
    /// optimized result is only computed and compared; mismatches are counted on the Performance tab and logged (first few).</summary>
    public enum FeatureMode { Off, On, Shadow }

    /// <summary>Quality/visual preset for features that change what is rendered or simulated (<see cref="Feature.UsesPreset"/>).
    /// Vanilla = such features stay inactive; exact features (no visible change) are not affected by the preset.</summary>
    public enum PerfPreset { Vanilla, Low, Balanced, Max }

    /// <summary>Feature groups = headings on the Performance tab, in this order.</summary>
    public static class Categories
    {
        public const string PlayMaker = "PlayMaker runtime";
        public const string Content = "Game logic (FSMs)";
        public const string Rendering = "Rendering & lighting";
        public const string World = "World, physics & vehicles";
        public static readonly string[] Order = { PlayMaker, Content, Rendering, World };
    }

    /// <summary>
    /// One optimization (plan-Performance.md id in <see cref="PlanId"/>). Lifecycle, driven by <see cref="PerformanceModule"/>:
    /// Bind (config) -> Enable when the effective mode becomes On/Shadow (module running, mode not Off, preset not Vanilla for
    /// preset features) -> scene/world/tile/origin callbacks while active -> Disable when the mode turns Off, the preset turns
    /// Vanilla or the module shuts down. Disable MUST restore every vanilla value the feature changed (game objects, prefab
    /// assets, settings, FSM actions). Harmony patches are installed once (EnsurePatched) and stay; their bodies check the
    /// feature's static flag, so Off costs one bool test.
    /// Discovered by reflection: every non-abstract subclass in this assembly with a public parameterless constructor.
    /// </summary>
    public abstract class Feature
    {
        public abstract string PlanId { get; }          // "PM2", "EN1", ...
        public abstract string Key { get; }             // config key, stable: "Targeted events"
        public abstract string Title { get; }           // shown on the tab
        public abstract string Category { get; }        // Categories.*
        public virtual string Help { get { return ""; } }
        public virtual int Order { get { return 0; } }  // inside the category
        public virtual bool SupportsShadow { get { return false; } }
        public virtual bool UsesPreset { get { return false; } }
        public virtual FeatureMode DefaultMode { get { return FeatureMode.On; } }

        public PerformanceModule Module { get; internal set; }
        public ConfigEntry<FeatureMode> ModeEntry { get; internal set; }
        /// <summary>Current running state (what Enable was called with), Off when not enabled.</summary>
        public FeatureMode Running { get; private set; }
        public bool IsOn { get { return Running == FeatureMode.On; } }
        public bool IsShadow { get { return Running == FeatureMode.Shadow; } }
        public string LastError;
        public long Mismatches;
        int _logged;

        /// <summary>Extra config (knobs) for this feature. Use <see cref="Knob"/> for preset-driven values. Called once.</summary>
        public virtual void Bind(ModuleConfig cfg) { }
        /// <summary>Start: install patches (EnsurePatched), set static flags, register jobs/handlers, apply changes to live objects.</summary>
        protected abstract void OnEnable(FeatureMode mode);
        /// <summary>Stop: clear static flags, remove jobs/handlers, restore every vanilla value.</summary>
        protected abstract void OnDisable();
        /// <summary>Called once in module Init (before Core applies shared dispatchers): subscribe handlers to Core.Hooks targets here
        /// (Hooks.X.Prefix(Module, handler)); handlers must check the feature's static flag first. Core removes them on Shutdown.</summary>
        public virtual void SubscribeHooks() { }
        public virtual void OnSceneLoaded() { }
        public virtual void OnWorldReady() { }
        public virtual void OnOriginShift(Vector3 delta) { }
        public virtual void OnSaveBegin() { }
        public virtual void OnSaveEnd() { }
        /// <summary>The preset or a knob changed while the feature runs: re-apply values (default: Disable + Enable).</summary>
        public virtual void OnSettingsChanged() { var m = Running; Stop(); Start(m); }
        /// <summary>Live numbers / state lines on the Performance tab (cheap, no allocations beyond the strings drawn).</summary>
        public virtual void DrawStats(ToolLayout l) { }

        internal void Start(FeatureMode mode)
        {
            if (Running != FeatureMode.Off || mode == FeatureMode.Off) return;
            if (mode == FeatureMode.Shadow && !SupportsShadow) mode = FeatureMode.On;
            try { Running = mode; OnEnable(mode); LastError = null; Log.Info("[Performance] " + PlanId + " " + Key + ": " + mode); }
            catch (Exception e)
            {
                LastError = e.Message; Log.Error("[Performance] " + PlanId + " " + Key + " could not start", e);
                try { OnDisable(); } catch (Exception e2) { Log.Error("[Performance] " + PlanId + " cleanup", e2); }
                Running = FeatureMode.Off;
            }
        }

        internal void Stop()
        {
            if (Running == FeatureMode.Off) return;
            try { OnDisable(); } catch (Exception e) { LastError = e.Message; Log.Error("[Performance] " + PlanId + " " + Key + " stop", e); }
            Running = FeatureMode.Off;
        }

        /// <summary>Shadow-mode bookkeeping: count and log (first 5) a difference between vanilla and the optimized result.</summary>
        protected void Mismatch(string what)
        {
            Mismatches++;
            if (_logged < 5) { _logged++; Log.Warn("[Performance] " + PlanId + " shadow mismatch #" + Mismatches + ": " + what); }
        }

        /// <summary>Installs this feature's Harmony patch class once (module lifetime). Returns false if it failed.</summary>
        protected bool EnsurePatched(Type patchClass)
        {
            return Module.EnsurePatched(patchClass);
        }

        /// <summary>Patches one method once (module lifetime).</summary>
        protected bool EnsurePatched(System.Reflection.MethodBase original, System.Reflection.MethodInfo prefix = null, System.Reflection.MethodInfo postfix = null, System.Reflection.MethodInfo transpiler = null, System.Reflection.MethodInfo finalizer = null)
        {
            return Module.EnsurePatched(original, prefix, postfix, transpiler, finalizer);
        }

        /// <summary>Preset-driven value with an optional manual override (tools.cfg, -1 = follow the preset).</summary>
        protected Knob NewKnob(ModuleConfig cfg, string key, string desc, float low, float balanced, float max, float vanilla)
        {
            var k = new Knob(this, cfg, Key + " / " + key, desc, low, balanced, max, vanilla);
            Knobs.Add(k);
            return k;
        }
        public readonly List<Knob> Knobs = new List<Knob>();
    }

    public sealed class Knob
    {
        readonly float[] _values;   // index = (int)PerfPreset
        public readonly ConfigEntry<float> Override;
        public readonly Feature Owner;
        internal Knob(Feature owner, ModuleConfig cfg, string key, string desc, float low, float balanced, float max, float vanilla)
        {
            Owner = owner;
            _values = new[] { vanilla, low, balanced, max };
            Override = cfg.Tool("Tuning", key, -1f, desc + " -1 = follow the preset (Performance " + low + ", Balanced " + balanced + ", Visuals " + max + ", Vanilla " + vanilla + ").");
            Override.SettingChanged += (s, a) => { if (Owner.Module != null) Owner.Module.SettingsChanged(Owner); };
        }
        public float Value
        {
            get
            {
                float o = Override.Value;
                if (o >= 0) return o;
                return _values[(int)PerformanceModule.Preset];
            }
        }
        public float Vanilla { get { return _values[0]; } }
        public int Int { get { return Mathf.RoundToInt(Value); } }
    }
}
