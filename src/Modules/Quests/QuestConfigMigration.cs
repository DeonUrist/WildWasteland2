using System;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static class QuestConfigMigration
    {
        internal const string LegacyFile = "local.apocalypterquests.cfg";

        internal static void Snapshot(ModuleConfig cfg)
        {
            // Bind saves new entries immediately. Cache both original files first, as Survival does.
            Legacy.Get(cfg.Main.ConfigFilePath, cfg.Section("General"), "MaxActiveQuests");
            Legacy.Get(cfg.Tools.ConfigFilePath, cfg.Section("Items"), "HandInRadius");
        }

        internal static ConfigEntry<T> Bind<T>(ModuleConfig cfg, string section, string key, T value, string description)
        {
            return Bind(cfg, section, key, value, new ConfigDescription(description));
        }

        internal static ConfigEntry<T> Bind<T>(ModuleConfig cfg, string section, string key, T value, ConfigDescription description)
        {
            // The old opt-in belongs to the standalone plugin, not WW's shared opt-in.
            string target = section == "General" && key == "Apocasetter" ? "Legacy" : section;
            var entry = Player(section, key)
                ? cfg.Player(target, key, value, description.Description, description.AcceptableValues)
                : cfg.Tool(target, key, value, description.Description, description.AcceptableValues);
            Migrate(entry, section, key, false);
            entry.SettingChanged += Changed;
            return entry;
        }

        internal static ConfigEntry<Key> BindKey(ModuleConfig cfg, string section, string key, Key value, string description)
        {
            ConfigEntry<Key> entry;
            if (key == "QuestLogKey") entry = cfg.Hotkey("Quests: quest log", value, description);
            else if (key == "DismissKey") entry = cfg.Hotkey("Quests: dismiss follower", value, description);
            else entry = cfg.Tool(section, key, value, description);
            Migrate(entry, section, key, true);
            entry.SettingChanged += Changed;
            return entry;
        }

        static void Migrate(ConfigEntryBase entry, string section, string key, bool keyCode)
        {
            if (Legacy.Get(entry.ConfigFile.ConfigFilePath, entry.Definition.Section, entry.Definition.Key) != null) return;
            string raw = Legacy.Get(LegacyFile, section, key);
            if (raw == null) return;
            try
            {
                if (keyCode)
                {
                    Key parsed;
                    if (!KeyMap.FromKeyCodeName(raw, out parsed)) throw new ArgumentException("unknown key " + raw);
                    raw = parsed.ToString();
                }
                entry.SetSerializedValue(raw);
                Log.Always("[Quests] migrated [" + section + "] " + key);
            }
            catch (Exception e) { Log.Warn("[Quests] could not migrate [" + section + "] " + key + ": " + e.Message); }
        }

        static bool Player(string section, string key)
        {
            if (section == "General") return key != "Apocasetter";
            if (section == "Generated") return true;
            if (section == "Items") return key == "CountCarriedItems";
            if (section == "Voice") return key != "Spatial";
            if (section == "RepVendor") return key == "Enabled";
            return section == "Hire" && (key == "Enabled" || key == "RideInCars" || key == "ShootFromCar");
        }
        static void Changed(object sender, EventArgs args) { if (Plugin.I != null) Plugin.I.ApplyConfig(); }
    }
}
