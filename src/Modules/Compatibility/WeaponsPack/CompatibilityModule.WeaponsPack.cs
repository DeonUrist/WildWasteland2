using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Owns the optional pack's native observations under one compatibility switch and resource scope.</summary>
    public sealed partial class CompatibilityModule
    {
        private readonly CompatibilityScope _weaponsPackScope = new CompatibilityScope("WeaponsPack");
        private ConfigEntry<bool> _weaponsPackEnabled;
        /// <summary>Binds the optional pack bridge policy independently of presentation and combat features.</summary>
        private void BindWeaponsPack(ModuleConfig cfg)
        {
            _weaponsPackEnabled = cfg.Player("WeaponsPack", "Enabled", true, "Connect optional Weapons Pack behavior and held-model presentation through Wild Wasteland services.");
            _weaponsPackEnabled.SettingChanged += WeaponsPackChanged;
        }
        /// <summary>Contributes the per-mod bridge switch.</summary>
        private void WeaponsPackSettings(SettingsShell shell) { shell.Row(this, SettingsShell.Page.System, "Compatibility", _weaponsPackEnabled, "Weapons Pack"); }
        /// <summary>Restarts only pack observations when the per-mod switch changes.</summary>
        private void WeaponsPackChanged(object sender, EventArgs args) { if (!_running) return; StopWeaponsPack(); StartWeaponsPack(); }
        /// <summary>Discovers the native plugin on a runner frame after chainloading.</summary>
        private void StartWeaponsPack() { if (_weaponsPackEnabled.Value) Scheduler.Interval(_weaponsPackScope, "Compatibility.WeaponsPack.Discovery", .5f, EnsureWeaponsPack); }
        /// <summary>Connects native callbacks to the shared protocol without reaching presentation state.</summary>
        private void EnsureWeaponsPack() { WeaponsPackBridge.Ensure(_weaponsPackScope); }
        /// <summary>Releases optional source observations and their owned Core registrations.</summary>
        private void StopWeaponsPack() { try { WeaponsPackBridge.Shutdown(); } finally { _weaponsPackScope.Shutdown(); } }
    }
}
