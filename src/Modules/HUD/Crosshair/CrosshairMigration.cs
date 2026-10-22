using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// The pre-1.1.0 key [Hud] TurnOffCrosshair (true = hide the crosshair and the selection dot), same semantics as
    /// Gunplay HUD 1.2.2's CrosshairMigration, but read from the old cfg TEXT through Core.Legacy instead of
    /// ConfigFile.OrphanedEntries (WW's shared cfg never had the key; orphans of the shared file are never touched):
    /// - TurnOffCrosshair comes from com.denis.apocalypter.gunplayhud.cfg; only when that file does not exist, from
    ///   com.denis.apocalypter.apocaraider.cfg (1.2.2 looked there only for a new HUD cfg);
    /// - true -> Crosshair = 0 unless the old HUD cfg already had [Hud] Crosshair, SelectionDot = false unless it had [Hud] SelectionDot.
    /// Runs once, on the first WW run, after the value import (so it wins over an imported Crosshair, as in 1.2.2).
    /// </summary>
    internal static class CrosshairMigration
    {
        internal static void Apply(ConfigEntry<int> crosshair, ConfigEntry<bool> dot)
        {
            bool hudCfg = Legacy.Exists(HudModule.OldCfg);
            string raw = hudCfg ? Legacy.Get(HudModule.OldCfg, "Hud", "TurnOffCrosshair") : Legacy.Get(HudModule.RaiderCfg, "Hud", "TurnOffCrosshair");
            bool off;
            if (raw == null || !bool.TryParse(raw, out off) || !off) return;
            bool hasCrosshair = hudCfg && Legacy.Get(HudModule.OldCfg, "Hud", "Crosshair") != null;
            bool hasDot = hudCfg && Legacy.Get(HudModule.OldCfg, "Hud", "SelectionDot") != null;
            if (!hasCrosshair) crosshair.Value = 0;
            if (!hasDot) dot.Value = false;
            Log.Always("[HUD] migrated retired [Hud] TurnOffCrosshair = true from " + (hudCfg ? HudModule.OldCfg : HudModule.RaiderCfg) +
                       (hasCrosshair ? "" : " -> Crosshair = 0") + (hasDot ? "" : " -> SelectionDot = false"));
        }
    }
}
