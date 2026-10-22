using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace WildWasteland.Core
{
    /// <summary>
    /// Per-module config helper (AGENTS.md 5.7).
    /// Main  = WildWasteland/settings.cfg: gameplay settings, module switches and hotkeys, edited in the F7 menu.
    /// Tools = BepInEx\config\WildWasteland\tools.cfg (invisible to Apocasetter, edited from the F7 tool window): tuning,
    ///         offsets, debug switches, developer settings.
    /// Sections are prefixed with the module id so five modules can share one file: "Patrols", "Patrols / Combat", ...
    /// Bind order = display order in Apocasetter, so bind Enabled first (Core does), then the important settings.
    /// </summary>
    public sealed class ModuleConfig
    {
        public readonly string Prefix;
        public readonly ConfigFile Main;
        public readonly ConfigFile Tools;
        public ConfigEntry<bool> Enabled { get; internal set; }

        internal ModuleConfig(string prefix, ConfigFile main, ConfigFile tools) { Prefix = prefix; Main = main; Tools = tools; }

        public string Section(string sub)
        {
            return string.IsNullOrEmpty(sub) || sub == "General" ? Prefix : Prefix + " / " + sub;
        }

        public ConfigEntry<T> Player<T>(string sub, string key, T def, string desc)
        {
            return TunedSettings.Bind(Main, Section(sub), key, def, desc);
        }
        public ConfigEntry<T> Player<T>(string sub, string key, T def, string desc, AcceptableValueBase range)
        {
            return TunedSettings.Bind(Main, Section(sub), key, def, new ConfigDescription(desc, range));
        }
        public ConfigEntry<T> Tool<T>(string sub, string key, T def, string desc)
        {
            return TunedSettings.Bind(Tools, Section(sub), key, def, desc);
        }
        public ConfigEntry<T> Tool<T>(string sub, string key, T def, string desc, AcceptableValueBase range)
        {
            return TunedSettings.Bind(Tools, Section(sub), key, def, new ConfigDescription(desc, range));
        }
        /// <summary>Rebindable gameplay/editor hotkey, edited in Debug. The menu key lives in the plugin's F6 config.</summary>
        public ConfigEntry<Key> Hotkey(string key, Key def, string desc)
        {
            return TunedSettings.Bind(Main, "Keys", key, def, desc);
        }

        /// <summary>
        /// First-run migration: copy a value from a standalone mod's cfg (BepInEx\config\&lt;legacyCfgFile&gt;, e.g.
        /// "com.denis.apocalypter.apocapatrol.cfg") into a WW entry. Only applied when the WW file the entry lives in did not
        /// exist at startup (so the user's later edits are never overwritten). Logs what was migrated.
        /// </summary>
        public void Migrate<T>(ConfigEntry<T> entry, string legacyCfgFile, string section, string key)
        {
            if (entry == null) return;
            bool firstRun = ReferenceEquals(entry.ConfigFile, Tools) ? Legacy.FirstRunTools : Legacy.FirstRunMain;
            if (!firstRun) return;
            string raw = Legacy.Get(legacyCfgFile, section, key);
            if (raw == null) return;
            try
            {
                entry.SetSerializedValue(raw);
                Log.Info("[" + Prefix + "] migrated " + legacyCfgFile + " [" + section + "] " + key + " = " + raw);
            }
            catch (Exception e) { Log.Warn("[" + Prefix + "] could not migrate [" + section + "] " + key + " from " + legacyCfgFile + ": " + e.Message); }
        }
    }

    /// <summary>Raw reader for standalone mods' cfg files (BepInEx ContainsKey only sees bound entries).</summary>
    public static class Legacy
    {
        public static bool FirstRunMain, FirstRunTools;
        static readonly Dictionary<string, Dictionary<string, string>> _files = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static string Get(string cfgFile, string section, string key)
        {
            Dictionary<string, string> d;
            if (!_files.TryGetValue(cfgFile, out d))
            {
                d = Parse(Path.Combine(Paths.ConfigPath, cfgFile));
                _files[cfgFile] = d;
            }
            string v;
            return d != null && d.TryGetValue(section + "\n" + key, out v) ? v : null;
        }

        public static bool Exists(string cfgFile) { return File.Exists(Path.Combine(Paths.ConfigPath, cfgFile)); }

        static Dictionary<string, string> Parse(string path)
        {
            if (!File.Exists(path)) return null;
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string section = "";
            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    if (line[0] == '[' && line[line.Length - 1] == ']') { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    d[section + "\n" + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e) { Log.Warn("could not read " + path + ": " + e.Message); }
            return d;
        }

        public static float ParseFloat(string s, float def)
        {
            float f; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : def;
        }
    }
}
