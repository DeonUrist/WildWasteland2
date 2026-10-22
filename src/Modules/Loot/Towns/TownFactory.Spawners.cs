using HutongGames.PlayMaker;
using WildWasteland.Modules.Loot;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Town spawner progress (Loot, ex Towns/TownFactory.cs): resume partly completed native spawn loops after a load and capture
    /// spawner states into the town save record. Per-FSM work is <see cref="NativeSpawners"/>; the town site/record/asset types
    /// are POI's (REFACTOR-REQUEST B15-2). Namespace kept for Wave C.
    /// </summary>
    internal static partial class TownFactory
    {
        /// <summary>Restarts every partly completed spawn loop of a rebuilt town (called by the POI entry when it goes live in play).</summary>
        internal static void Resume(TownSite site) // refs-ok: REFACTOR-REQUEST B15-2
        {
            foreach (var fsms in site.Spawners) if (fsms != null) foreach (var f in fsms) NativeSpawners.Resume(f);
        }

        /// <summary>Copies OnOff/number/numberEnd of every built lot's spawners into the town save record (parking/skipped lots excluded).</summary>
        internal static void Capture(TownSite site, TownRecord record, TownAssets assets) // refs-ok: REFACTOR-REQUEST B15-2
        {
            for (int i = 0; i < site.Spawners.Length; i++)
            {
                var fsms = site.Spawners[i]; if (fsms == null) continue;
                if (site.Plan.Lots[i].IsParking || site.Plan.Lots[i].Skipped) continue;
                var states = new SpawnerState[fsms.Length]; // refs-ok: REFACTOR-REQUEST B15-2
                var paths = assets.Buildings[site.Plan.Lots[i].Prefab].Spawners;
                for (int j = 0; j < fsms.Length; j++) states[j] = new SpawnerState { Path = paths[j], // refs-ok: REFACTOR-REQUEST B15-2
                    OnOff = NativeSpawners.Read(fsms[j], "OnOff"), Number = NativeSpawners.Read(fsms[j], "number"), NumberEnd = NativeSpawners.Read(fsms[j], "numberEnd") };
                record.Spawners[i] = states;
            }
        }

        /// <summary>Sets a spawner int variable (POI TownFactory call sites); forwards to <see cref="NativeSpawners.Write"/>.</summary>
        private static void Set(PlayMakerFSM f, string name, int value) { NativeSpawners.Write(f, name, value); }
    }
}
