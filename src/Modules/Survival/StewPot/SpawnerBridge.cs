using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    internal static class SpawnerBridge
    {
        static Type entryType;
        static FieldInfo key, display, category, group, prefab, isAsset, mod;
        static IList lastEntries;
        static object addedEntry;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(SurvivalModule owner)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var catalog = assembly.GetType("Apocaspawner.Catalog");
                if (catalog == null) continue;
                entryType = assembly.GetType("Apocaspawner.Entry");
                if (entryType == null) continue;
                key = AccessTools.Field(entryType, "Key"); display = AccessTools.Field(entryType, "Display");
                category = AccessTools.Field(entryType, "Category"); group = AccessTools.Field(entryType, "Group");
                prefab = AccessTools.Field(entryType, "Prefab"); isAsset = AccessTools.Field(entryType, "IsAsset");
                mod = AccessTools.Field(entryType, "Mod");
                Hooks.SpawnerCatalogEntries.Postfix(owner, CatalogBuilt);
                Core.Log.Always("[Survival] Apocaspawner bridge installed: Stew Pot -> Camp & Farming (Camping).");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CatalogBuilt(object __result)
        {
            var entries = __result as IList;
            if (entries == null) return;
            if (ReferenceEquals(entries, lastEntries) && addedEntry != null) return;
            try
            {
                var template = Factory.Template();
                object entry = null;
                for (int i = entries.Count - 1; i >= 0; i--)
                    if (string.Equals(key.GetValue(entries[i]) as string, "StewPot", StringComparison.OrdinalIgnoreCase))
                    { if (entry == null) entry = entries[i]; else entries.RemoveAt(i); }
                if (entry == null) { entry = Activator.CreateInstance(entryType, true); entries.Add(entry); }
                key.SetValue(entry, "StewPot"); display.SetValue(entry, "Stew Pot");
                category.SetValue(entry, "stewpot"); group.SetValue(entry, "Camp & Farming");
                prefab.SetValue(entry, template); isAsset.SetValue(entry, true);
                if (mod != null) mod.SetValue(entry, "Wild Wasteland / Survival");
                lastEntries = entries; addedEntry = entry;
            }
            catch (Exception error) { Core.Log.Error("[Survival] Cannot add Stew Pot to Apocaspawner: " + error); }
        }

        internal static void Shutdown()
        {
            if (lastEntries != null && addedEntry != null) lastEntries.Remove(addedEntry);
            lastEntries = null; addedEntry = null; entryType = null;
            key = display = category = group = prefab = isAsset = mod = null;
        }
    }
}
