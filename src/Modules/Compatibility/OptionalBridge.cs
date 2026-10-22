using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Lifecycle of one optional third-party bridge inside <see cref="CompatibilityModule"/>: its own resource scope
    /// (Compatibility.&lt;Mod&gt;: patches, jobs, hooks, events), its `[Compatibility / &lt;Mod&gt;] Enabled` switch (D12: new section, no
    /// migration) with a System settings row, and start/stop that react live to the switch. Turning one bridge off removes exactly its
    /// own registrations and never touches a sibling bridge.
    /// </summary>
    internal sealed class OptionalBridge
    {
        private readonly CompatibilityModule _module;
        private readonly Action<IModule> _start;
        private readonly Action _stop;
        private readonly string _label, _description;
        private bool _started;

        /// <summary>The resource owner of this bridge's patches, jobs, hooks and events.</summary>
        internal readonly CompatibilityScope Scope;
        /// <summary>`[Compatibility / &lt;Mod&gt;] Enabled`.</summary>
        internal ConfigEntry<bool> Enabled { get; private set; }

        /// <summary>Declares a bridge: <paramref name="mod"/> = config sub-section and scope name, <paramref name="start"/> registers everything
        /// under the given scope, <paramref name="stop"/> releases the bridge's static state (the scope then removes its registrations).</summary>
        internal OptionalBridge(CompatibilityModule module, string mod, string label, string description, Action<IModule> start, Action stop)
        {
            _module = module; _label = label; _description = description; _start = start; _stop = stop;
            Scope = new CompatibilityScope(mod);
        }

        /// <summary>Binds the per-mod switch in `[Compatibility / &lt;Mod&gt;]`.</summary>
        internal void Bind(ModuleConfig cfg)
        {
            Enabled = cfg.Player(Scope.Mod, "Enabled", true, _description);
            Enabled.SettingChanged += Changed;
        }

        /// <summary>Adds the switch to the System page's Compatibility group.</summary>
        internal void Settings(SettingsShell shell) { shell.Row(_module, SettingsShell.Page.System, "Compatibility", Enabled, _label); }

        /// <summary>Starts the bridge when its switch is on (module Init, or the switch turned on while Compatibility runs).</summary>
        internal void Start()
        {
            if (_started || Enabled == null || !Enabled.Value) return;
            _started = true;
            try { _start(Scope); }
            catch (Exception e) { Log.Error(_module, _label + " bridge could not start", e); Stop(); }
        }

        /// <summary>Releases the bridge state, then every registration of its scope (idempotent).</summary>
        internal void Stop()
        {
            if (!_started) return;
            _started = false;
            try { _stop(); }
            catch (Exception e) { Log.Warn(_module, _label + " bridge shutdown: " + e.Message); }
            finally { Scope.Shutdown(); }
        }

        /// <summary>Live switch change (F6/F7/Apocasetter): restarts only this bridge, then applies any newly used Core dispatcher.</summary>
        private void Changed(object sender, EventArgs args)
        {
            if (!_module.IsRunning) return;
            Stop(); Start();
            Hooks.ApplyUsed();
        }
    }
}
