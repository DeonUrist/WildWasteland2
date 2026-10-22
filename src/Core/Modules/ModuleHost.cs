using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>Lifecycle state of a module.</summary>
    public enum ModuleState { NotStarted, Running, OffByConfig, OffConflict, InitFailed, DisabledAfterErrors }

    /// <summary>
    /// Owns every module's lifecycle (AGENTS.md 5.3): config binding, conflict check against the standalone mods it
    /// replaces (5.8), Init, runtime enable/disable from the F6 menu, hook forwarding, and failure isolation
    /// (a module that keeps throwing is shut down instead of throwing every frame).
    /// </summary>
    public static class ModuleHost
    {
        public sealed class Entry
        {
            public IModule Module;
            public ModuleConfig Config;
            public ModuleState State;
            public string Conflict;      // name of the conflicting standalone plugin
            public int Errors;
            public string LastError;
            internal float WindowStart; internal int WindowErrors; internal int Logged;
        }

        /// <summary>Save/world lifecycle broadcasts.</summary>
        public enum Event { WorldReady, SaveBegin, SaveEnd, LoadBegin }

        static readonly List<Entry> _entries = new List<Entry>();
        static readonly Dictionary<IModule, Entry> _byModule = new Dictionary<IModule, Entry>();
        /// <summary>Every module entry in registration order.</summary>
        public static IList<Entry> Entries { get { return _entries; } }
        /// <summary>Entry of a module or null.</summary>
        public static Entry Of(IModule m) { Entry e; return m != null && _byModule.TryGetValue(m, out e) ? e : null; }
        /// <summary>True while the module runs.</summary>
        public static bool IsRunning(IModule m) { var e = Of(m); return e != null && e.State == ModuleState.Running; }

        /// <summary>The module whose Init/ProvideApis/Shutdown (or deferred init) is running right now, else null. Lets the
        /// [Obsolete] Services setters register providers with the right owner.</summary>
        public static IModule Current { get; private set; }

        const int ErrorWindowLimit = 40;      // errors per 10 s that disable a module
        static bool _quitting;

        /// <summary>Registers a module (ModuleRegistry only).</summary>
        public static void Add(IModule m)
        {
            var e = new Entry { Module = m, State = ModuleState.NotStarted };
            _entries.Add(e); _byModule[m] = e;
        }

        /// <summary>Binds every module's config (Enabled first in its section). Order = display order in Apocasetter.</summary>
        internal static void BindAll(ConfigFile main, ConfigFile tools)
        {
            foreach (var e in _entries)
            {
                var m = e.Module;
                var cfg = new ModuleConfig(m.Id, main, tools);
                cfg.Enabled = TunedSettings.Bind(main, m.Id, "Enabled", m.DefaultEnabled,
                    m.DisplayName + ": master switch for this whole feature group. Turning it off at runtime stops it immediately (patches removed); turning it on starts it again.");
                e.Config = cfg;
                var mb = m as ModuleBase; if (mb != null) mb.Config = cfg;
                try { m.BindConfig(cfg); }
                catch (Exception ex) { Log.Error("[" + m.Id + "] BindConfig failed", ex); e.LastError = ex.Message; SetState(e, ModuleState.InitFailed); }
                if (e.State != ModuleState.InitFailed)
                {
                    try { UI.SettingsShell.Shared.RemoveAll(m); m.Settings(UI.SettingsShell.Shared); }
                    catch (Exception ex) { Log.Error(m, "Settings contribution failed", ex); }
                }
                var captured = e;
                cfg.Enabled.SettingChanged += (s, a) => OnEnabledChanged(captured);
            }
        }

        /// <summary>Starts every enabled module without a standalone conflict, then applies used dispatchers.</summary>
        internal static void StartAll()
        {
            foreach (var e in _entries)
            {
                if (e.State == ModuleState.InitFailed) continue;
                string conflict = FindConflict(e.Module);
                if (conflict != null)
                {
                    e.Conflict = conflict; SetState(e, ModuleState.OffConflict);
                    Log.Warn("[" + e.Module.Id + "] waiting for standalone replacement: " + conflict + ".");
                    continue;
                }
                if (!e.Config.Enabled.Value) { SetState(e, ModuleState.OffByConfig); continue; }
                Start(e);
            }
            Hooks.ApplyUsed();
            Hotkeys.CheckConflicts();
        }

        /// <summary>Description of a loaded standalone plugin this module replaces, or null.</summary>
        static string FindConflict(IModule m)
        {
            var c = m.ConflictsWith;
            if (c == null) return null;
            foreach (var guid in c)
            {
                PluginInfo info;
                if (Chainloader.PluginInfos.TryGetValue(guid, out info) && !ReferenceEquals(info.Instance, null))
                    return info.Metadata.Name + " " + info.Metadata.Version + " (" + System.IO.Path.GetFileName(info.Location) + "): " + ReplacementGuard.Status(guid);
            }
            return null;
        }

        /// <summary>Init + ProvideApis inside try/catch; a failed Init is shut down and cleaned up.</summary>
        static void Start(Entry e)
        {
            var m = e.Module;
            try
            {
                Current = m;
                try { m.Init(); m.ProvideApis(); } finally { Current = null; }
                SetActive(m, true);
                SetState(e, ModuleState.Running);
                e.WindowErrors = 0;
                Log.Always("[" + m.Id + "] started (" + Patches.Count(m.Id) + " patches" + (Patches.Failed(m.Id) > 0 ? ", " + Patches.Failed(m.Id) + " FAILED" : "") + ")");
            }
            catch (Exception ex)
            {
                e.LastError = ex.Message; SetState(e, ModuleState.InitFailed);
                Log.Error("[" + m.Id + "] Init failed - module stays off (switch it off and on in F6 / the F7 Modules page to retry)", ex);
                // a half-run Init may have set its service field / Running flag / created objects: Shutdown undoes that (modules keep
                // Shutdown safe to call after a partial Init), then Core removes patches, jobs, hooks and hotkeys
                Current = m;
                try { m.Shutdown(); } catch (Exception sx) { Log.Error("[" + m.Id + "] Shutdown after the failed Init", sx); }
                finally { Current = null; }
                Cleanup(m);
            }
        }

        /// <summary>Shutdown + Core cleanup, then sets the new state.</summary>
        static void Stop(Entry e, ModuleState newState)
        {
            var m = e.Module;
            if (e.State == ModuleState.Running)
            {
                Current = m;
                try { m.Shutdown(); } catch (Exception ex) { Log.Error("[" + m.Id + "] Shutdown", ex); }
                finally { Current = null; }
            }
            Cleanup(m);
            SetState(e, newState);
            Log.Always("[" + m.Id + "] stopped (" + newState + ")");
        }

        /// <summary>Releases every owner-scoped Core registration and resource after module shutdown or failed startup.</summary>
        static void Cleanup(IModule m)
        {
            SetActive(m, false);
            Scheduler.RemoveAll(m);
            Hooks.RemoveAll(m);
            Hotkeys.RemoveAll(m);
            Patches.Unpatch(m);
            ModApi.RemoveAll(m);
            ModEvents.RemoveAll(m);
            Services.RemoveHandlersOf(m);
            Textures.Release(m);
            WorldIndex.RemoveAll(m);
            WorldTiles.RemoveAll(m);
            FsmIndex.RemoveAll(m);
            FsmPause.ReleaseAll(m);
            Factions.RemoveAll(m);
            CameraArbiter.RemoveAll(m);
            VehicleOwnership.RemoveAll(m);
            LoadingProgress.Remove(m);
        }

        /// <summary>Changes a module's state and publishes <see cref="Events.ModuleStateChanged"/> (local event) when it differs.</summary>
        static void SetState(Entry e, ModuleState state)
        {
            var old = e.State;
            e.State = state;
            if (old == state || !ModEvents.HasListeners<Events.ModuleStateChanged>()) return;
            var ev = new Events.ModuleStateChanged { ModuleId = e.Module.Id, Old = (Events.ModuleStateKind)old, New = (Events.ModuleStateKind)state, Error = e.LastError };
            ModEvents.Publish(ref ev);
        }

        /// <summary>Sets ModuleBase.Active.</summary>
        static void SetActive(IModule m, bool on) { var mb = m as ModuleBase; if (mb != null) mb.Active = on; }

        /// <summary>Runtime on/off from the [&lt;Id&gt;] Enabled entry.</summary>
        static void OnEnabledChanged(Entry e)
        {
            if (_quitting) return;
            bool want = e.Config.Enabled.Value;
            if (want && (e.State == ModuleState.OffByConfig || e.State == ModuleState.DisabledAfterErrors || e.State == ModuleState.InitFailed))
            {
                Start(e);
                Hooks.ApplyUsed();
                if (e.State == ModuleState.Running && GameRefs.InGame) Safe(e, "OnWorldReady", () => e.Module.OnWorldReady());
            }
            else if (!want && e.State == ModuleState.Running) Stop(e, ModuleState.OffByConfig);
        }

        /// <summary>Restart a running module (used by tools that change Init-time settings).</summary>
        public static void Restart(IModule m)
        {
            var e = Of(m); if (e == null || e.State != ModuleState.Running) return;
            Stop(e, ModuleState.OffByConfig); Start(e); Hooks.ApplyUsed();
        }
        // Modules without a standalone ordering dependency can finish initialization after chainloading.
        /// <summary>Finishes a module initialization after chainloading (conflict re-check first).</summary>
        internal static bool DeferredInit(IModule module, Action initialize)
        {
            var entry = Of(module); if (entry == null || entry.State != ModuleState.Running) return false;
            string conflict = FindConflict(module);
            if (conflict != null)
            {
                foreach (string guid in module.ConflictsWith) ReplacementGuard.LateConflict(guid);
                conflict = FindConflict(module);
                Stop(entry, ModuleState.OffConflict); entry.Conflict = entry.LastError = conflict; Log.Warn("[" + module.Id + "] " + conflict); return false;
            }
            try
            {
                Current = module;
                try { initialize(); } finally { Current = null; }
                Hooks.ApplyUsed(); Hotkeys.CheckConflicts(); return true;
            }
            catch (Exception error)
            {
                Stop(entry, ModuleState.InitFailed); entry.LastError = error.Message;
                Log.Error("[" + module.Id + "] deferred initialization failed", error); return false;
            }
        }

        // ---------------------------------------------------------------- broadcasts

        /// <summary>Forwards scene loads to running modules.</summary>
        internal static void SceneLoaded(Scene s, LoadSceneMode mode)
        {
            foreach (var e in _entries) if (e.State == ModuleState.Running) { var en = e; Safe(en, "OnSceneLoaded", () => en.Module.OnSceneLoaded(s, mode)); }
            Optimization.OptimizationRuntime.Instance.OnSceneLoaded(s, mode);
        }

        /// <summary>Forwards a save/world lifecycle hook to running modules.</summary>
        internal static void Broadcast(Event ev, string slot)
        {
            var optimization = Optimization.OptimizationRuntime.Instance;
            switch (ev)
            {
                case Event.WorldReady: optimization.OnWorldReady(); break;
                case Event.SaveBegin: optimization.OnSaveBegin(slot); break;
                case Event.SaveEnd: optimization.OnSaveEnd(slot); break;
                case Event.LoadBegin: optimization.OnLoadBegin(slot); break;
            }
            foreach (var e in _entries)
            {
                if (e.State != ModuleState.Running) continue;
                var m = e.Module;
                switch (ev)
                {
                    case Event.WorldReady: Safe(e, "OnWorldReady", () => m.OnWorldReady()); break;
                    case Event.SaveBegin: Safe(e, "OnSaveBegin", () => m.OnSaveBegin(slot)); break;
                    case Event.SaveEnd: Safe(e, "OnSaveEnd", () => m.OnSaveEnd(slot)); break;
                    case Event.LoadBegin: Safe(e, "OnLoadBegin", () => m.OnLoadBegin(slot)); break;
                }
            }
        }

        /// <summary>Forwards a floating-origin jump to running modules.</summary>
        internal static void OriginShift(Vector3 delta)
        {
            Optimization.OptimizationRuntime.Instance.OnOriginShift(delta);
            foreach (var e in _entries) if (e.State == ModuleState.Running) { var en = e; Safe(en, "OnOriginShift", () => en.Module.OnOriginShift(delta)); }
        }

        /// <summary>Shuts every running module down on application quit.</summary>
        internal static void Quit()
        {
            if (_quitting) return;
            _quitting = true;
            foreach (var e in _entries) if (e.State == ModuleState.Running) { var en = e; Safe(en, "Shutdown", () => en.Module.Shutdown()); }
            Optimization.OptimizationRuntime.Instance.Shutdown();
            UI.UiTheme.Shutdown();
            WorldTiles.Uninstall();
            Textures.ReleaseAll();
        }

        /// <summary>Runs a lifecycle hook, counting exceptions toward the module error rule.</summary>
        static void Safe(Entry e, string what, Action a)
        {
            try { a(); }
            catch (Exception ex) { Fail(e, what, ex, true); }
        }

        // ---------------------------------------------------------------- failure isolation

        /// <summary>
        /// A scheduler job that throws is isolated by the Scheduler itself (the job is switched off after repeated errors, the module keeps
        /// running - the source mods' runners also swallowed a subsystem's exception per frame and went on). Job errors are counted and
        /// logged per module but do not disable the module; only hook / lifecycle failures do.
        /// </summary>
        internal static void ReportJobException(Job j, Exception ex) { Fail(Of(j.Owner), "job " + j.Name, ex, false); }
        /// <summary>Counts a hook/event handler exception toward the module error rule.</summary>
        internal static void ReportHookException(IModule m, string target, Exception ex) { Fail(Of(m), target, ex, true); }

        /// <summary>Logs (first 5) and counts an error; 40 counted errors in 10 s disable the module.</summary>
        static void Fail(Entry e, string where, Exception ex, bool countsTowardsDisable)
        {
            if (e == null) { Log.Error("[Core] " + where, ex); return; }
            e.Errors++; e.LastError = where + ": " + ex.Message;
            if (e.Logged < 5) { e.Logged++; Log.Error("[" + e.Module.Id + "] " + where, ex); }
            if (!countsTowardsDisable) return;
            float now = Time.unscaledTime;
            if (now - e.WindowStart > 10f) { e.WindowStart = now; e.WindowErrors = 0; }
            if (++e.WindowErrors >= ErrorWindowLimit && e.State == ModuleState.Running)
            {
                Log.Error("[" + e.Module.Id + "] " + e.WindowErrors + " errors in 10 s - module DISABLED for this session (last: " + e.LastError + "). Re-enable it in the F6 menu or the F7 window to retry.");
                Stop(e, ModuleState.DisabledAfterErrors);
            }
        }

        // ---------------------------------------------------------------- summary

        /// <summary>Startup summary: every module with state and patch counts plus the Core hooks.</summary>
        public static string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("Wild Wasteland ").Append(Plugin.VERSION).Append(" modules:");
            foreach (var e in _entries)
            {
                sb.Append("\n  ").Append(e.Module.Id).Append(" (").Append(e.Module.DisplayName).Append("): ");
                switch (e.State)
                {
                    case ModuleState.Running: sb.Append("on"); break;
                    case ModuleState.OffByConfig: sb.Append("off by config"); break;
                    case ModuleState.OffConflict: sb.Append("off: standalone mod present - ").Append(e.Conflict); break;
                    case ModuleState.InitFailed: sb.Append("off: init failed - ").Append(e.LastError); break;
                    default: sb.Append(e.State); break;
                }
                int p = Patches.Count(e.Module.Id), f = Patches.Failed(e.Module.Id);
                if (p > 0 || f > 0) sb.Append(", patches ").Append(p).Append(f > 0 ? " (" + f + " failed)" : "");
            }
            sb.Append("\n  core ").Append(Hooks.Summary());
            if (Patches.Conflicts > 0) sb.Append("\n  PATCH CONFLICTS: ").Append(Patches.Conflicts);
            return sb.ToString();
        }
    }
}
