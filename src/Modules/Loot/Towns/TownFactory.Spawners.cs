using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Town spawner progress: resume partially completed native spawn loops, capture spawner states for the save record,
    /// and the ItemSpawner int get/set helpers. Wave A (A6): partial split of Towns/TownFactory.cs (Loot spawner-progress part, B15).
    /// </summary>
    internal static partial class TownFactory
    {
        internal static void Resume(TownSite site)
        {
            foreach (var fsms in site.Spawners) if (fsms != null) foreach (var f in fsms)
            {
                if (f == null || !f.enabled || Get(f, "OnOff") > 0 || Get(f, "number") <= 0 || Get(f, "numberEnd") <= Get(f, "number")) continue;
                if (f.ActiveStateName == "wait" || f.ActiveStateName == "trigger") f.Fsm.SetState("getRandom int");
            }
        }
        internal static void Capture(TownSite site, TownRecord record, TownAssets assets)
        {
            for (int i = 0; i < site.Spawners.Length; i++)
            {
                var fsms = site.Spawners[i]; if (fsms == null) continue;
                if (site.Plan.Lots[i].IsParking || site.Plan.Lots[i].Skipped) continue;
                var states = new SpawnerState[fsms.Length];
                var paths = assets.Buildings[site.Plan.Lots[i].Prefab].Spawners;
                for (int j = 0; j < fsms.Length; j++) states[j] = new SpawnerState { Path = paths[j],
                    OnOff = Get(fsms[j], "OnOff"), Number = Get(fsms[j], "number"), NumberEnd = Get(fsms[j], "numberEnd") };
                record.Spawners[i] = states;
            }
        }
        private static int Get(PlayMakerFSM f, string name)
        { var v = f == null ? null : f.FsmVariables.FindFsmInt(name); return v == null ? 0 : v.Value; }
        private static void Set(PlayMakerFSM f, string name, int value)
        { var v = f.FsmVariables.FindFsmInt(name); if (v != null) v.Value = value; }
    }
}
