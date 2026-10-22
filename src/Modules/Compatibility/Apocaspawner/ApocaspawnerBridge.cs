using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using WildWasteland.Core;
using WildWasteland.Modules.Guns;
using WildWasteland.Modules.Patrols.WarRig;
using WildWasteland.Modules.Survival;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The single Apocaspawner (com.denis.apocalypter.apocaspawner) bridge (A7-10): one detection, one catalog postfix through the Core
    /// dispatcher (adds the War Rig, then the Stew Pot, each only while its host runs), the Spawner.Spawn prefix for the War Rig, and the
    /// crossbow damage postfix (<see cref="ApocaspawnerDamage"/>). Registrations belong to the Compatibility.Apocaspawner scope.
    /// </summary>
    internal static class ApocaspawnerBridge
    {
        /// <summary>BepInEx GUID of Apocaspawner.</summary>
        internal const string Guid = "com.denis.apocalypter.apocaspawner";
        private static IModule _scope;
        private static Type _entry;
        private static FieldInfo _key;

        /// <summary>Resolves Apocaspawner (soft dependency: loaded before WW) and installs the catalog, spawn and damage hooks.</summary>
        internal static void Start(IModule scope)
        {
            _scope = scope;
            var asm = FindAssembly();
            if (asm == null) { if (Log.L2) Log.Deep(scope, "Apocaspawner not installed"); return; }
            _entry = asm.GetType("Apocaspawner.Entry");
            _key = _entry != null ? AccessTools.Field(_entry, "Key") : null;
            if (_entry == null || _key == null || asm.GetType("Apocaspawner.Catalog") == null) { _entry = null; Log.Warn(scope, "Apocaspawner API changed; War Rig and Stew Pot are not listed (natural spawning remains available)."); }
            else
            {
                Hooks.SpawnerCatalogEntries.Postfix(scope, CatalogBuilt);
                var spawner = asm.GetType("Apocaspawner.Spawner");
                var spawn = spawner != null ? AccessTools.Method(spawner, "Spawn") : null;
                if (spawn == null || !Core.Patches.Patch(scope, spawn, prefix: AccessTools.Method(typeof(ApocaspawnerBridge), nameof(SpawnPrefix))))
                    Log.Warn(scope, "Apocaspawner Spawner.Spawn unavailable; the War Rig entry cannot spawn.");
                Log.Main(scope, "Apocaspawner bridge installed: War Rig -> Trailers, Stew Pot -> Camp & Farming (Camping).");
            }
            ApocaspawnerDamage.Install(scope, asm);
        }

        /// <summary>The Apocaspawner assembly: by GUID (its plugin instance) or, as the Stew Pot bridge did, by its Catalog type.</summary>
        private static Assembly FindAssembly()
        {
            BepInEx.PluginInfo info;
            if (Chainloader.PluginInfos.TryGetValue(Guid, out info) && !ReferenceEquals(info.Instance, null)) return info.Instance.GetType().Assembly;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetType("Apocaspawner.Catalog") != null) return assembly;
            return null;
        }

        /// <summary>Core Apocaspawner.Catalog.Entries postfix (each catalog build): War Rig first, then Stew Pot, as the two hosts' postfixes ran.</summary>
        private static void CatalogBuilt(object __result)
        {
            var list = __result as IList;
            if (list == null || _entry == null) return;
            SpawnerIntegration.AddTo(list);
            StewPotSpawnerEntry.AddTo(list);
        }

        /// <summary>Prefix of Apocaspawner.Spawner.Spawn(Entry e, int count): the War Rig entry spawns through the War Rig feature.</summary>
        private static bool SpawnPrefix(object e, int count) { return _entry == null || SpawnerIntegration.Spawn(e, count); }

        /// <summary>The bridge's resource scope (log owner of its parts); null while stopped.</summary>
        internal static IModule Scope { get { return _scope; } }

        /// <summary>Key of a catalog entry.</summary>
        internal static string KeyOf(object entry) { return _key.GetValue(entry) as string; }

        /// <summary>A new, empty Apocaspawner.Entry.</summary>
        internal static object NewEntry() { return Activator.CreateInstance(_entry, true); }

        /// <summary>Sets a field of a catalog entry when the installed version has it.</summary>
        internal static void Set(object entry, string field, object value)
        {
            var f = AccessTools.Field(_entry, field);
            if (f != null) f.SetValue(entry, value);
        }

        /// <summary>Removes the Stew Pot entry it added and forgets the Apocaspawner types; the scope removes the hooks.</summary>
        internal static void Shutdown()
        {
            StewPotSpawnerEntry.Shutdown(); ApocaspawnerDamage.Shutdown();
            _entry = null; _key = null; _scope = null;
        }
    }
}
