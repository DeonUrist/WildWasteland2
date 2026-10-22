using System;
using System.Collections;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Stew Pot part of the Apocaspawner bridge: lists the Stew Pot under Camp &amp; Farming (key "StewPot", category "stewpot") while the
    /// Survival module runs, replacing any duplicate entry. The entry it added is removed again when the bridge stops or Survival is off.
    /// </summary>
    internal static class StewPotSpawnerEntry
    {
        private static IList lastEntries;
        private static object addedEntry;

        /// <summary>Adds or refreshes the Stew Pot entry of a catalog list (skips a list it already filled).</summary>
        internal static void AddTo(IList entries)
        {
            if (!CompatibilityHost.Survival.Running) { Shutdown(); return; }
            if (ReferenceEquals(entries, lastEntries) && addedEntry != null) return;
            try
            {
                var template = Factory.Template(); // refs-ok: REFACTOR-REQUEST B17-4
                object entry = null;
                for (int i = entries.Count - 1; i >= 0; i--)
                    if (string.Equals(ApocaspawnerBridge.KeyOf(entries[i]), "StewPot", StringComparison.OrdinalIgnoreCase))
                    { if (entry == null) entry = entries[i]; else entries.RemoveAt(i); }
                if (entry == null) { entry = ApocaspawnerBridge.NewEntry(); entries.Add(entry); }
                ApocaspawnerBridge.Set(entry, "Key", "StewPot"); ApocaspawnerBridge.Set(entry, "Display", "Stew Pot");
                ApocaspawnerBridge.Set(entry, "Category", "stewpot"); ApocaspawnerBridge.Set(entry, "Group", "Camp & Farming");
                ApocaspawnerBridge.Set(entry, "Prefab", template); ApocaspawnerBridge.Set(entry, "IsAsset", true);
                ApocaspawnerBridge.Set(entry, "Mod", "Wild Wasteland / Survival");
                lastEntries = entries; addedEntry = entry;
            }
            catch (Exception error) { Log.Error(ApocaspawnerBridge.Scope, "Cannot add Stew Pot to Apocaspawner", error); }
        }

        /// <summary>Removes the entry this bridge added from the last catalog list.</summary>
        internal static void Shutdown()
        {
            if (lastEntries != null && addedEntry != null) lastEntries.Remove(addedEntry);
            lastEntries = null; addedEntry = null;
        }
    }
}
