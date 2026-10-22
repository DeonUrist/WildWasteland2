using System.IO;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// First-run migration (only when the WW main cfg did not exist yet; see ModuleConfig.Migrate). Order of 1.2.2 kept:
    /// 1. com.denis.apocalypter.gunplayhud.cfg when it exists, else (as 1.2.2's LegacyConfig.Import did for a new HUD cfg)
    ///    the same section/key names from com.denis.apocalypter.apocaraider.cfg;
    /// 2. the old hidden sizes (GunplayHUD\hidden-settings.not-saved, normally absent);
    /// 3. <see cref="CrosshairMigration"/> (retired TurnOffCrosshair -> Crosshair 0 / SelectionDot false).
    /// The legacy files are only read, never changed.
    /// </summary>
    internal static class LegacyConfig
    {
        internal static void Import(ModuleConfig cfg)
        {
            if (!Legacy.FirstRunMain && !Legacy.FirstRunTools) return;
            // 1.2.2 imported from Apocaraider only when its own cfg was new (existing per-mod settings always win).
            string src = Legacy.Exists(HudModule.OldCfg) ? HudModule.OldCfg : Legacy.Exists(HudModule.RaiderCfg) ? HudModule.RaiderCfg : null;
            if (src != null)
            {
                cfg.Migrate(HudModule.HudEnabled, src, "General", "EnableHudEffects");
                cfg.Migrate(HudModule.DamageNumbers, src, "Hud", "FloatingDamage");
                cfg.Migrate(HudModule.HitMarker, src, "Hud", "HitMarker");
                cfg.Migrate(HudModule.Crosshair, src, "Hud", "Crosshair");
                cfg.Migrate(HudModule.SelectionDot, src, "Hud", "SelectionDot");
                cfg.Migrate(HudModule.HeadshotSound, src, "Hud", "HeadshotSound");
            }
            string hidden = Path.Combine("GunplayHUD", "hidden-settings.not-saved");
            if (Legacy.Exists(hidden))
            {
                cfg.Migrate(HudModule.DamageFontSize, hidden, "Hud", "DamageFontSize");
                cfg.Migrate(HudModule.HitMarkerSize, hidden, "Hud", "HitMarkerSize");
            }
            if (Legacy.FirstRunMain) CrosshairMigration.Apply(HudModule.Crosshair, HudModule.SelectionDot);
        }
    }
}
