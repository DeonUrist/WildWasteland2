using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Owns optional BloodFX lifecycle recovery independently of the gun simulation module.</summary>
    public sealed partial class CompatibilityModule
    {
        private readonly CompatibilityScope _bloodFxScope = new CompatibilityScope("BloodFX");
        private ConfigEntry<bool> _bloodFxEnabled;
        /// <summary>Binds the one optional recovery and debug-key compatibility switch.</summary>
        private void BindBloodFx(ModuleConfig cfg)
        {
            _bloodFxEnabled = cfg.Player("BloodFX", "Enabled", true, "Recover the optional BloodFX updater after scene destruction and suppress its F8 debug hit.");
            _bloodFxEnabled.SettingChanged += BloodFxChanged;
        }
        /// <summary>Exposes the optional bridge switch with the other compatibility settings.</summary>
        private void BloodFxSettings(SettingsShell shell) { shell.Row(this, SettingsShell.Page.System, "Compatibility", _bloodFxEnabled, "BloodFX"); }
        /// <summary>Restarts only BloodFX registrations on an individual switch change.</summary>
        private void BloodFxChanged(object sender, EventArgs args) { if (!_running) return; StopBloodFx(); StartBloodFx(); }
        /// <summary>Starts optional discovery on a scheduler frame after all plugins have initialized.</summary>
        private void StartBloodFx() { if (_bloodFxEnabled.Value) Scheduler.Interval(_bloodFxScope, "Compatibility.BloodFX.Discovery", .5f, EnsureBloodFx); }
        /// <summary>Resolves the reflective optional integration once per enabled lifetime.</summary>
        private void EnsureBloodFx() { BloodFxCompat.Ensure(_bloodFxScope); }
        /// <summary>Releases native delegate references and all patches/jobs belonging to the optional bridge.</summary>
        private void StopBloodFx() { try { BloodFxCompat.Shutdown(); } finally { _bloodFxScope.Shutdown(); } }
    }
}
