using System;
using System.Collections;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility.WarRig
{
    /// <summary>
    /// War Rig part of the Apocaspawner bridge: lists the War Rig (tanker) under Trailers and spawns it through the War Rig feature.
    /// The War Rig feature registers itself with <see cref="Install"/>; the entry is offered only while that host module runs and War
    /// Rig spawning is enabled. Hooks are owned by <see cref="ApocaspawnerBridge"/>.
    /// </summary>
    internal static class SpawnerIntegration
    {


        /// <summary>Registration by the War Rig feature (WarRigFeature.Install): remembers the host module; no patching here any more (B17).</summary>

        /// <summary>True while the registering War Rig host runs.</summary>
        private static bool HostRunning { get { return ModApi.Get<IWarRigWorld>()?.Running==true; } }

        /// <summary>Adds the War Rig entry to a freshly built catalog (once; skipped when spawning is disabled or the key exists).</summary>
        internal static void AddTo(IList list)
        {
            if (!HostRunning || ModApi.Get<IWarRigWorld>()?.SpawnEnabled!=true) return; // refs-ok: REFACTOR-REQUEST B17-5
            foreach (var e in list) if (ApocaspawnerBridge.KeyOf(e) == ModApi.Get<IWarRigWorld>()?.TemplateName) return; // refs-ok: REFACTOR-REQUEST B17-5
            try
            {
                var item = ApocaspawnerBridge.NewEntry();
                ApocaspawnerBridge.Set(item, "Key", ModApi.Get<IWarRigWorld>()?.TemplateName); ApocaspawnerBridge.Set(item, "Display", "War Rig (tanker)"); // refs-ok: REFACTOR-REQUEST B17-5
                ApocaspawnerBridge.Set(item, "Category", "trailer"); ApocaspawnerBridge.Set(item, "Group", "Trailers");
                ApocaspawnerBridge.Set(item, "IsVehicle", true); ApocaspawnerBridge.Set(item, "IsAsset", true); ApocaspawnerBridge.Set(item, "Prefab", ModApi.Get<IWarRigWorld>()?.Template()); // refs-ok: REFACTOR-REQUEST B17-5
                list.Add(item);
            }
            catch (Exception e) { Log.Main(ApocaspawnerBridge.Scope, "War Rig catalog: " + e.Message); }
        }

        /// <summary>Spawn request of a catalog entry: the War Rig entry spawns <paramref name="count"/> rigs near the player (false = original skipped).</summary>
        internal static bool Spawn(object e, int count)
        {
            if (!HostRunning || ApocaspawnerBridge.KeyOf(e) != ModApi.Get<IWarRigWorld>()?.TemplateName) return true; // refs-ok: REFACTOR-REQUEST B17-5
            try { for (int i = 0; i < count; i++) ModApi.Get<IWarRigWorld>()?.SpawnNearPlayer(); } // refs-ok: REFACTOR-REQUEST B17-5
            catch (Exception err) { Log.Warn(ApocaspawnerBridge.Scope,"War Rig: " + err.Message); } // refs-ok: REFACTOR-REQUEST B17-5
            return false;
        }
    }
}
