using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace WildWasteland.Core
{
    // Captured 2026-10-19 tuning supplies Bind defaults. BepInEx still owns stored values and legacy migration.
    internal static class TunedSettings
    {
        static readonly Dictionary<string, Dictionary<string, string>> Files = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        internal static T Default<T>(ConfigFile file, string section, string key, T fallback)
        {
            string name = Path.GetFileName(file.ConfigFilePath);
            if (name == "com.denis.apocalypter.wildwasteland.cfg") name = "main.cfg";
            Dictionary<string, string> values;
            if (!Files.TryGetValue(name, out values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); Files[name] = values;
                using (var stream = WildWasteland.Core.DiskResources.Open("CleanHUD.DefaultSettings." + name + ".txt"))
                    if (stream != null) using (var reader = new StreamReader(stream))
                    {
                        string group = "", line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim(); if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                            if (line[0] == '[' && line[line.Length - 1] == ']') { group = line.Substring(1, line.Length - 2).Trim(); continue; }
                            int equal = line.IndexOf('='); if (equal > 0) values[group + "\n" + line.Substring(0, equal).Trim()] = line.Substring(equal + 1).Trim();
                        }
                    }
            }
            string raw; if (!values.TryGetValue(section + "\n" + key, out raw)) return fallback;
            try { return (T)TomlTypeConverter.ConvertToValue(raw, typeof(T)); }
            catch (Exception error) { Log.Warn("[Settings] Retaining built-in default for " + section + "/" + key + ": " + error.Message); return fallback; }
        }
        internal static ConfigEntry<T> Bind<T>(ConfigFile file, string section, string key, T fallback, string description)
        { return file.Bind(section, key, Default(file, section, key, fallback), description); }
        internal static ConfigEntry<T> Bind<T>(ConfigFile file, string section, string key, T fallback, ConfigDescription description)
        { return file.Bind(section, key, Default(file, section, key, fallback), description); }

        // Called after the original sidecar migration, so existing/legacy tuning wins over these missing-only defaults.
        internal static void InstallProfiles(string owner)
        {
            var assembly = typeof(Plugin).Assembly; string prefix = "CleanHUD.DefaultProfiles." + owner + ".";
            foreach (string resource in WildWasteland.Core.DiskResources.Names)
            {
                if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string relative = resource.Substring(prefix.Length);
                if (relative.StartsWith("WheelGrips.", StringComparison.Ordinal)) relative = Path.Combine("WheelGrips", relative.Substring(11));
                string path = Path.Combine(Assets.ModuleConfigDir(owner), relative);
                if (File.Exists(path)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var input = WildWasteland.Core.DiskResources.Open(resource)) using (var output = File.Create(path)) input.CopyTo(output);
            }
        }
    }
}
