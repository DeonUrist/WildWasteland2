using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Apocaspawner part of the loot editor's item catalog: display names from its cfg [Names] section (Compatibility, Wave A split of Patrols Editor/PickupCatalog.cs).</summary>
    internal static partial class PickupCatalog
    {
        /// <summary>Reads Apocaspawner's [Names] section (prefab name = display name) from its cfg; empty when the file is missing or unreadable.</summary>
        private static Dictionary<string, string> ApocaspawnerNames()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(Paths.ConfigPath, "com.denis.apocalypter.apocaspawner.cfg");
            try
            {
                bool section = false;
                if (File.Exists(path)) foreach (string line in File.ReadAllLines(path))
                {
                    string s = line.Trim(); if (s.StartsWith("[")) { section = s == "[Names]"; continue; }
                    int eq = s.IndexOf('='); if (section && eq > 0 && !s.StartsWith("#")) names[s.Substring(0, eq).Trim()] = s.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e) { Core.Log.Warn("[Patrols] " + "Item names: " + e.Message); }
            return names;
        }
    }
}
