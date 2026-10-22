using System;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>Saves: resolves the War Rig prefab id through the Core.Hooks.GetPrefab dispatcher (null = not ours, ES3 continues).</summary>
    internal static class PrefabLookup
    {
        /// <summary>GetPrefab resolver: the War Rig template's ES3Prefab for prefab id 735710202610090001, otherwise null.</summary>
        internal static ES3Internal.ES3Prefab Resolve(long id)
        {
            if (id != Factory.PrefabId) return null;
            try { return Factory.Template().GetComponent<ES3Internal.ES3Prefab>(); }
            catch (Exception e) { Log.Error(VehiclesModule.Instance, "War Rig template for a saved rig: " + e.Message); return null; }
        }
    }
}
