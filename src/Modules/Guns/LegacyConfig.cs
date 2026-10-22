using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// First-run migration into the WW entries (ModuleConfig.Migrate only applies when the WW cfg / tools.cfg did not exist yet).
    /// Sources, applied in the old precedence order (the last one that has a value wins):
    ///  1. com.denis.apocalypter.apocaunloader.cfg [General] (was AmmoSettings.Bind: imported when the Gunplay cfg lacked the key),
    ///  2. com.denis.apocalypter.apocaraider.cfg, same section/key (was LegacyConfig.Import: only when the Gunplay cfg did not exist),
    ///  3. com.denis.apocalypter.gunplay.cfg (Denis's current values).
    /// The 28 hidden entries were never saved by Gunplay, so they have nothing to migrate (defaults are the old constants).
    /// </summary>
    internal static class LegacyConfig
    {
        const string Gunplay = "com.denis.apocalypter.gunplay.cfg";
        const string Raider = "com.denis.apocalypter.apocaraider.cfg";
        const string Unloader = "com.denis.apocalypter.apocaunloader.cfg";

        internal static void Migrate(ModuleConfig cfg)
        {
            bool gunplayCfg = Legacy.Exists(Gunplay);

            // 1. standalone Apocaunloader [General] (old key names)
            cfg.Migrate(AmmoSettings.Enabled, Unloader, "General", "Enabled");
            cfg.Migrate(AmmoSettings.HoldSeconds, Unloader, "General", "HoldSeconds");
            cfg.Migrate(AmmoSettings.PhaseTimeout, Unloader, "General", "AnimationTimeout");
            cfg.Migrate(AmmoSettings.FallbackKey, Unloader, "General", "FallbackKey");
            cfg.Migrate(AmmoSettings.DropBackpackOverflow, Unloader, "General", "DropBackpackOverflow");
            // The standalone Enabled switch also disabled overflow protection. Preserve that on migration.
            if (Legacy.FirstRunMain && Legacy.Get(Gunplay, "Ammo", "DropBackpackOverflow") == null)
            {
                string oldEnabled = Legacy.Get(Unloader, "General", "Enabled");
                bool enabled;
                if (oldEnabled != null && bool.TryParse(oldEnabled, out enabled) && !enabled)
                {
                    AmmoSettings.DropBackpackOverflow.Value = false;
                    Core.Log.Info("[Guns] migrated " + Unloader + " [General] Enabled = false -> BackpackOverflow = false");
                }
            }

            // 2 + 3: the Gunplay layout (Apocaraider used the same sections/keys)
            if (!gunplayCfg) All(cfg, Raider);
            All(cfg, Gunplay);
        }

        static void All(ModuleConfig cfg, string file)
        {
            M(cfg, GunsModule.TracersEnabled, file, "General", "EnableGunplay");
            M(cfg, GunsModule.BossHpPercent, file, "Gunplay", "AdjustHumanBossHP");
            M(cfg, GunsModule.BoltSpeed, file, "Gunplay", "BoltSpeed");
            M(cfg, GunsModule.BulletSpeed, file, "Gunplay", "BulletSpeed");
            M(cfg, GunsModule.HeadshotMultiplier, file, "Gunplay", "HeadshotMultiplier");
            M(cfg, GunsModule.OrbsHitWalls, file, "Gunplay", "MonsterProjectilesHitWalls");
            M(cfg, GunsModule.PlayerGunTracers, file, "Gunplay", "PlayerGunTracers");
            M(cfg, GunsModule.TracersDraw, file, "Gunplay", "Tracers");
            M(cfg, GunsModule.VehicleDamage, file, "Gunplay", "VehicleDamage");
            M(cfg, GunsModule.WheelPopOff, file, "Gunplay", "WheelPopOff");
            M(cfg, GunsModule.PistolRange, file, "Tracers", "PistolRange");
            M(cfg, GunsModule.SmgRange, file, "Tracers", "SmgRange");
            M(cfg, GunsModule.RifleRange, file, "Tracers", "RifleRange");
            M(cfg, GunsModule.SniperRange, file, "Tracers", "SniperRange");
            M(cfg, GunsModule.ShotgunRange, file, "Tracers", "ShotgunRange");
            M(cfg, GunsModule.CrossbowRange, file, "Tracers", "CrossbowRange");
            M(cfg, AmmoSettings.Enabled, file, "Ammo", "EnableUnloading");
            M(cfg, AmmoSettings.HoldSeconds, file, "Ammo", "HoldSeconds");
            M(cfg, AmmoSettings.PhaseTimeout, file, "Ammo", "AnimationTimeout");
            M(cfg, AmmoSettings.FallbackKey, file, "Ammo", "FallbackKey");
            M(cfg, AmmoSettings.DropBackpackOverflow, file, "Ammo", "DropBackpackOverflow");
            M(cfg, AmmoSettings.DropArmorOverflow, file, "Ammo", "DropArmorOverflow");
        }

        static void M<T>(ModuleConfig cfg, ConfigEntry<T> e, string file, string section, string key) { cfg.Migrate(e, file, section, key); }
    }
}
