using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Town spawner budgets (Loot, ex Towns/TownPopulation.cs "spawner control" part): native budget capture for the town asset
    /// catalog, per-town population/loot percentage allocation balanced across buildings and spawn groups (completed groups
    /// retained) and per-lot spawner configuration (counts, faction pools, merchants, unique and boss groups). Per-FSM work is
    /// <see cref="NativeSpawners"/>; the town plan/site/asset/settings types, the percentage roll and the faction pools are POI's
    /// and NPCAI's (REFACTOR-REQUEST B15-2). Namespace kept for Wave C.
    /// </summary>
    internal static partial class TownPopulation
    {
        /// <summary>Resolves the current Loot-owned native spawner service.</summary>
        private static INativeSpawners Spawners { get { return SpawnerControl.Required; } }

        /// <summary>Native count budget of one spawn group of a town building asset (POI TownAssets data; see <see cref="NativeSpawnerBudget"/>).</summary>
        internal sealed class NativeBudget
        {
            /// <summary>Native numberEnd range and group kind.</summary>
            internal int Min, Max, Kind;
        }

        /// <summary>Native budget of a spawner prefab FSM (POI TownAssets catalog build); throws for unsupported groups.</summary>
        internal static NativeBudget Capture(PlayMakerFSM f)
        {
            var b = Spawners.Capture(f);
            return new NativeBudget { Min = b.Min, Max = b.Max, Kind = b.Kind };
        }

        /// <summary>
        /// Plans a town's population and loot before construction: measures completed faction members once (from saved spawner
        /// records), classifies the town, rolls the population/loot percentages and allocates both over the lots' spawn groups.
        /// </summary>
        internal static void Budget(TownSite site, TownAssets assets, TownSettings settings)
        {
            if (!site.Plan.PopulationMeasured)
            {
                // Measure completed faction members from native completed spawn counters, never from requested budgets.
                int spawned = 0;
                if (site.Record != null) foreach (var row in site.Record.Spawners)
                {
                    var lot = site.Plan.Lots[row.Key]; BuildingAsset asset;
                    if (!assets.Buildings.TryGetValue(lot.Prefab, out asset)) continue;
                    foreach (var state in row.Value) for (int i = 0; i < asset.Spawners.Length; i++)
                        if (asset.Spawners[i] == state.Path && asset.Budgets[i].Kind == 1) spawned += Mathf.Max(0, state.Number);
                    if (lot.Hounds) spawned = Mathf.Max(0, spawned - 1);
                }
                site.Plan.FactionMembers = spawned; site.Plan.PopulationMeasured = true;
            }
            Classify(site.Plan);
            int population = Percentage(site.Plan, site.Plan.Rules.PopulationMin, site.Plan.Rules.PopulationMax, "population");
            int loot = Percentage(site.Plan, site.Plan.Rules.LootMin, site.Plan.Rules.LootMax, "loot");
            site.PopulationCounts = Allocate(site, assets, population, 1);
            site.LootCounts = Allocate(site, assets, loot, 2);
            if (Log.L1) Log.Main(null, "town rolls " + site.Plan.Key + ": faction=" + site.Plan.Faction + ", " + site.Plan.Condition + ", population=" + population + "%, loot=" + loot + "%; planned before construction; shared native totals balanced across buildings and spawn groups; completed groups retained.");
        }

        /// <summary>
        /// Spreads percent of the town's seeded native total of one kind (1 enemies, 2 loot) over the eligible groups: committed
        /// (already spawned) counts first, then one at a time to the lot with the fewest, the group with the fewest, from a seeded
        /// start lot. Finished groups are not eligible.
        /// </summary>
        private static int[][] Allocate(TownSite site, TownAssets assets, int percent, int kind)
        {
            int lots = site.Plan.LotCount, native = 0, committed = 0;
            var result = new int[lots][]; var eligible = new bool[lots][]; var totals = new int[lots];
            for (int lot = 0; lot < lots; lot++)
            {
                if (site.Plan.Lots[lot].IsParking || site.Plan.Lots[lot].Skipped) { result[lot] = new int[0]; eligible[lot] = new bool[0]; continue; }
                var asset = assets.Buildings[site.Plan.Lots[lot].Prefab]; int count = asset.Budgets.Length;
                result[lot] = new int[count]; eligible[lot] = new bool[count];
                SpawnerState[] saved = null; if (site.Record != null) site.Record.Spawners.TryGetValue(lot, out saved);
                for (int n = 0; n < count; n++) if (asset.Budgets[n].Kind == kind)
                {
                    var b = asset.Budgets[n]; native += new System.Random(TownPlanner.Hash(site.Plan.Key + "/" + lot + "/native/" + asset.Spawners[n])).Next(b.Min, b.Max + 1);
                    int done = 0, on = 0;
                    if (site.Spawners[lot] != null && site.Spawners[lot][n] != null) { done = Spawners.Read(site.Spawners[lot][n], "number"); on = Spawners.Read(site.Spawners[lot][n], "OnOff"); }
                    else if (saved != null) foreach (var state in saved) if (state.Path == asset.Spawners[n]) { done = state.Number; on = state.OnOff; break; }
                    result[lot][n] = Mathf.Max(0, done); totals[lot] += result[lot][n]; committed += result[lot][n];
                    eligible[lot][n] = on == 0;
                }
            }
            int remaining = Mathf.Max(0, Mathf.RoundToInt(native * percent / 100f) - committed);
            int offset = TownPlanner.Hash(site.Plan.Key + "/balance/" + kind) % lots;
            while (remaining-- > 0)
            {
                int bestLot = -1, bestGroup = -1, best = int.MaxValue;
                for (int i = 0; i < lots; i++)
                {
                    int lot = (i + offset) % lots, group = -1, least = int.MaxValue;
                    for (int n = 0; n < result[lot].Length; n++) if (eligible[lot][n] && result[lot][n] < least) { least = result[lot][n]; group = n; }
                    if (group >= 0 && totals[lot] < best) { best = totals[lot]; bestLot = lot; bestGroup = group; }
                }
                if (bestLot < 0) break;
                result[bestLot][bestGroup]++; totals[bestLot]++;
            }
            return result;
        }

        /// <summary>
        /// Configures the spawners of one built lot from the town budget: shopkeepers/sale stock keep native counts (Coyotes towns
        /// keep their merchant), bosses off, unique loot by seeded loot percentage, enemies/loot/one vehicle group forced to their
        /// allocated counts with the town faction pool (a Scrapyard hound may take a one-actor group after a human group).
        /// Zero or finished groups are disabled or parked.
        /// </summary>
        internal static void Configure(TownSite site, int lot, TownAssets assets, TownSettings settings)
        {
            var item = site.Plan.Lots[lot]; var fsms = site.Spawners[lot]; var asset = assets.Buildings[item.Prefab];
            if (site.PopulationCounts == null) Budget(site, assets, settings);
            int population = Percentage(site.Plan, site.Plan.Rules.PopulationMin, site.Plan.Rules.PopulationMax, "population");
            int loot = Percentage(site.Plan, site.Plan.Rules.LootMin, site.Plan.Rules.LootMax, "loot");
            bool usedVehicle = false, houndUsed = false; int humanReserved = 0;
            for (int n = 0; n < fsms.Length; n++)
            {
                var f = fsms[n]; if (f == null) continue;
                int kind = asset.Budgets[n].Kind;
                if (kind == 4)
                {
                    if (item.IsMerchant && Spawners.Read(f, "OnOff") == 0) f.enabled = site.Plan.Faction == TownFaction.Coyotes;   // (WW) a Coyotes town always keeps its merchant
                    continue; // Authored shopkeepers and paid sale stock retain native placement/counts.
                }
                if (kind == 6) { Spawners.Write(f, "OnOff", 1); f.enabled = false; continue; }
                if (kind == 5) { if (Spawners.Read(f, "OnOff") == 0) f.enabled = TownPlanner.Hash(site.Plan.Key + "/unique/" + lot + "/" + n) % 100 < loot; continue; }
                bool actor = kind == 1, common = kind == 2, vehicle = kind == 3;
                bool selected = actor || common || vehicle && !usedVehicle && item.Vehicles > 0;
                int count = actor ? site.PopulationCounts[lot][n] : common ? site.LootCounts[lot][n]
                    : vehicle && !usedVehicle && loot > 0 ? item.Vehicles : 0;
                usedVehicle |= vehicle;
                // A hound may use a one-actor native group only after a positive human group.
                // This preserves spawn-point distribution and cannot generate a dogs-only base.
                bool companion = actor && site.Plan.Faction == TownFaction.Scrapyard && item.Hounds && !houndUsed && humanReserved > 0 && count == 1 && assets.Hounds.Count > 0;
                if (actor && count > 0) { if (companion) houndUsed = true; else humanReserved += count; }
                if (selected && count == 0 && Spawners.Read(f, "OnOff") == 0 && Spawners.Read(f, "number") == 0)
                { f.enabled = false; continue; }
                if (count == 0 || Spawners.Read(f, "OnOff") > 0 || Spawners.Read(f, "number") >= count)
                { Spawners.Write(f, "OnOff", 1); f.Fsm.SetState("spawned"); continue; }
                List<GameObject> pool = null;
                if (actor && (!assets.Population.TryGetValue(site.Plan.Faction, out pool) || pool.Count == 0))
                    throw new InvalidOperationException("No native " + site.Plan.Faction + " population prefabs in this world graph.");
                if (companion) pool = assets.Hounds;
                // Inactive native spawn nodes have not run Awake yet: Apply also supplies the authored prefill, so later
                // activation uses this town's faction. Loot groups (pool null) keep their native item selector.
                Spawners.Apply(f, count, pool);
            }
        }

        /// <summary>Stand-alone spawner, exact count (POI call sites); forwards to <see cref="Spawners.Force"/>.</summary>
        internal static void Force(PlayMakerFSM f, int count, List<GameObject> pool) { Spawners.Force(f, count, pool); }

        /// <summary>Spawner int variable (POI call sites); forwards to <see cref="Spawners.Read"/>.</summary>
        internal static int Read(PlayMakerFSM f, string name) { return Spawners.Read(f, name); }

        /// <summary>Sets a spawner int variable (POI call sites); forwards to <see cref="Spawners.Write"/>.</summary>
        internal static void Write(PlayMakerFSM f, string name, int value) { Spawners.Write(f, name, value); }
    }
}
