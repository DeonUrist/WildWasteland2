using System;
using System.Collections;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // Apocaspawner (third party, optional): shared catalog hook and module-only SpawnNearPlayer patch.
    internal static class SpawnerIntegration
    {
        private static Type entry;
        internal static void Install(Core.IModule owner)
        {
            entry = null;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.denis.apocalypter.apocaspawner", out info) || info.Instance == null) return;
            var asm = info.Instance.GetType().Assembly;
            entry = asm.GetType("Apocaspawner.Entry");
            var catalog = AccessTools.Method(asm.GetType("Apocaspawner.Catalog"), "Entries");
            var spawn = AccessTools.Method(asm.GetType("Apocaspawner.Spawner"), "Spawn");
            if (entry == null || catalog == null || spawn == null) { Core.Log.Warn("[Patrols] " + "Apocaspawner API changed; natural spawning remains available."); entry = null; return; }
            Core.Hooks.SpawnerCatalogEntries.Postfix(owner, CatalogPostfix);
            Core.Patches.Patch(owner, spawn, prefix: AccessTools.Method(typeof(SpawnerIntegration), nameof(SpawnPrefix)));
        }

        private static void CatalogPostfix(object __result)
        {
            var list = __result as IList;
            if (!S.SpawnEnabled.Value) return;
            if (list == null || entry == null) return;
            foreach (var e in list) if ((string)entry.GetField("Key").GetValue(e) == Factory.Name) return;
            try
            {
                var item = Activator.CreateInstance(entry, true);
                Set(item, "Key", Factory.Name); Set(item, "Display", "War Rig (tanker)");
                Set(item, "Category", "trailer"); Set(item, "Group", "Trailers");
                Set(item, "IsVehicle", true); Set(item, "IsAsset", true); Set(item, "Prefab", Factory.Template());
                list.Add(item);
            }
            catch (Exception e) { Core.Log.Info("[Patrols] " + "War Rig catalog: " + e.Message); }
        }

        private static void Set(object item, string field, object value)
        {
            var f = entry.GetField(field);
            if (f != null) f.SetValue(item, value);
        }

        private static bool SpawnPrefix(object e, int count)
        {
            if (entry == null || (string)entry.GetField("Key").GetValue(e) != Factory.Name) return true;
            try { for (int i = 0; i < count; i++) WarRigFeature.SpawnNearPlayer(S.EquipWheels); }
            catch (Exception err) { WarRigFeature.Say("War Rig: " + err.Message); }
            return false;
        }
    }
}
