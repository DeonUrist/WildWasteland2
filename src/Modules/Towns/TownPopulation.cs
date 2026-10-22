using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal static class TownPopulation
    {
        internal static void Assign(TownPlan plan, GenerationInput input, System.Random random)
        {
            int min = plan.Condition == TownCondition.Ruined ? input.RuinsMin : input.HumansMin;
            int max = plan.Condition == TownCondition.Ruined ? input.RuinsMax : input.HumansMax;
            int actors = random.Next(Math.Min(min, max), Math.Max(min, max) + 1);
            int loot = random.Next(Math.Min(input.LootMin, input.LootMax), Math.Max(input.LootMin, input.LootMax) + 1);
            int houndLot = -1;
            if (plan.Faction == TownFaction.Scrapyard && actors >= 2 && plan.LotCount >= 2 && random.Next(3) == 0)
            {
                houndLot = random.Next(plan.LotCount); plan.Lots[houndLot].Hounds = true;
                plan.Lots[houndLot].Actors = 1; actors--;
            }
            for (int i = 0; i < actors; i++)
            {
                int lot = random.Next(plan.LotCount - (houndLot >= 0 ? 1 : 0));
                if (houndLot >= 0 && lot >= houndLot) lot++;
                plan.Lots[lot].Actors++;
            }
            for (int i = 0; i < loot; i++) plan.Lots[random.Next(plan.LotCount)].Loot++;
            if (plan.LotCount > 2) plan.Lots[2].Vehicles = 1;
            plan.PopulationMeasured = true;
            Classify(plan);
        }
        internal static void Created(TownSite site, GameObject actor)
        {
            if (!site.CountedMembers.Add(actor.GetInstanceID())) return;
            var plan = site.Plan;
            string tag = plan.Faction == TownFaction.Coyotes ? "Coyotes" : plan.Faction == TownFaction.Scrapyard ? "Scrapyard"
                : plan.Faction == TownFaction.Mutants ? "Mutant" : null;
            if (tag != null && !actor.CompareTag(tag)) return;
            if (tag == null && !actor.CompareTag("Carnivore") && !actor.CompareTag("Herbivore")) return;
            if (plan.Faction == TownFaction.Coyotes || plan.Faction == TownFaction.Scrapyard)
            {
                bool human = false;
                foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) if (animator.avatar != null && animator.avatar.isHuman) { human = true; break; }
                if (!human) return; // Yard Hounds accompany a base but cannot claim it.
            }
            plan.FactionMembers++;
        }
        internal static void Classify(TownPlan plan)
        {
            plan.Condition = plan.Owned && (plan.Faction == TownFaction.Coyotes || plan.Faction == TownFaction.Scrapyard)
                ? TownCondition.Inhabited : TownCondition.Ruined;
        }
        internal static void Legacy(TownPlan plan)
        {
            if (!plan.HasTown || plan.LotCount == 0) return;
            var random = new System.Random(TownPlanner.Hash(plan.Key + "/population"));
            plan.Faction = plan.Condition == TownCondition.Ruined ? (random.Next(2) == 0 ? TownFaction.Animals : TownFaction.Mutants)
                : (random.Next(2) == 0 ? TownFaction.Scrapyard : TownFaction.Coyotes);
            Assign(plan, new GenerationInput { RuinsMin = 3, RuinsMax = 6, HumansMin = 5, HumansMax = 9, LootMin = 12, LootMax = 20, PopulationPercent = 100, LootPercent = 100 },
                random);
            plan.PopulationMeasured = false;
        }
        internal sealed class NativeBudget
        {
            internal int Min, Max, Kind;
        }
        private static int NativeValue(PlayMakerFSM f, FsmInt value)
        {
            int result = value.Value;
            if (string.IsNullOrEmpty(value.Name)) return result;
            var authored = f.FsmVariables.FindFsmInt(value.Name); if (authored != null) result = authored.Value;
            // ItemNum's SetFsmInt may run after another component's activation. Read its authored
            // override as well, so 100% means the prefab's real loot amount, not the 4..8 fallback.
            foreach (var sibling in f.GetComponents<PlayMakerFSM>()) if (sibling.FsmName == "ItemNum")
                foreach (var state in sibling.FsmStates) foreach (var action in state.Actions)
                {
                    var set = action as SetFsmInt;
                    if (set != null && set.fsmName.Value == "ItemSpawner" && set.variableName.Value == value.Name) result = set.setValue.Value;
                }
            return result;
        }
        internal static NativeBudget Capture(PlayMakerFSM f)
        {
            if (f == null) return new NativeBudget();
            string name = f.gameObject.name;
            int kind = name.StartsWith("EnemySpawn", StringComparison.Ordinal) ? 1 : name.StartsWith("ItemSpawn", StringComparison.Ordinal) ? 2
                : name.StartsWith("CarSpawn", StringComparison.Ordinal) || name.StartsWith("TrailerSpawn", StringComparison.Ordinal) ? 3
                : name.StartsWith("CritterSpawn", StringComparison.Ordinal) || name.StartsWith("CrateSpawn", StringComparison.Ordinal) ? 0 : 4;
            var budget = new NativeBudget { Kind = kind };
            if (kind == 1 && name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("bodyguard", StringComparison.OrdinalIgnoreCase) < 0) { budget.Kind = 6; return budget; }
            if (kind != 1 && kind != 2) return budget;
            // Native unique-item FSMs select one item (or iterate authored child points), with no
            // numberEnd roll. Preserve that authored flow; zero loot can still suspend it.
            if (kind == 2 && name.StartsWith("ItemSpawn_Unique", StringComparison.Ordinal))
            { budget.Kind = 5; return budget; }
            foreach (var state in f.FsmStates) foreach (var action in state.Actions)
            {
                var roll = action as RandomInt;
                if (roll == null || roll.storeResult == null || roll.storeResult.Name != "numberEnd") continue;
                budget.Min = Mathf.Max(0, NativeValue(f, roll.min));
                budget.Max = Mathf.Max(budget.Min, NativeValue(f, roll.max) - (roll.inclusiveMax ? 0 : 1));
                if (budget.Max > 100000) throw new InvalidOperationException("Invalid native town spawner count: " + name);
                return budget;
            }
            throw new InvalidOperationException("Native count roll missing for " + name + "; refusing to invent a baseline.");
        }
        private static int Percentage(TownPlan plan, int min, int max, string kind)
        {
            int lo = Math.Min(min, max), hi = Math.Max(min, max);
            return new System.Random(TownPlanner.Hash(plan.Key + "/town/" + kind + "/" + lo + "/" + hi)).Next(lo, hi + 1);
        }
        internal static void Budget(TownSite site, TownAssets assets, TownSettings settings)
        {
            if (!site.Plan.PopulationMeasured)
            {
                // Migrate old saves from native completed spawn counters, never from requested budgets.
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
            int population = Percentage(site.Plan, settings.PopulationMinPercent.Value, settings.PopulationMaxPercent.Value, "population");
            int loot = Percentage(site.Plan, settings.LootMinPercent.Value, settings.LootMaxPercent.Value, "loot");
            site.PopulationCounts = Allocate(site, assets, population, 1);
            site.LootCounts = Allocate(site, assets, loot, 2);
            Log.Always("[Towns] town rolls " + site.Plan.Key + ": faction=" + site.Plan.Faction + ", " + site.Plan.Condition + ", population=" + population + "%, loot=" + loot + "%; planned before construction; shared native totals balanced across buildings and spawn groups; completed groups retained.");
        }
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
                    if (site.Spawners[lot] != null && site.Spawners[lot][n] != null) { done = Get(site.Spawners[lot][n], "number"); on = Get(site.Spawners[lot][n], "OnOff"); }
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
        internal static void Configure(TownSite site, int lot, TownAssets assets, TownSettings settings)
        {
            var item = site.Plan.Lots[lot]; var fsms = site.Spawners[lot]; var asset = assets.Buildings[item.Prefab];
            if (site.PopulationCounts == null) Budget(site, assets, settings);
            int population = Percentage(site.Plan, settings.PopulationMinPercent.Value, settings.PopulationMaxPercent.Value, "population");
            int loot = Percentage(site.Plan, settings.LootMinPercent.Value, settings.LootMaxPercent.Value, "loot");
            bool usedVehicle = false, houndUsed = false; int humanReserved = 0;
            for (int n = 0; n < fsms.Length; n++)
            {
                var f = fsms[n]; if (f == null) continue;
                int kind = asset.Budgets[n].Kind;
                if (kind == 4)
                {
                    if (item.IsMerchant && Get(f, "OnOff") == 0) f.enabled = site.Plan.Faction == TownFaction.Coyotes;   // (WW) a Coyotes town always keeps its merchant
                    continue; // Authored shopkeepers and paid sale stock retain native placement/counts.
                }
                if (kind == 6) { Set(f, "OnOff", 1); f.enabled = false; continue; }
                if (kind == 5) { if (Get(f, "OnOff") == 0) f.enabled = TownPlanner.Hash(site.Plan.Key + "/unique/" + lot + "/" + n) % 100 < loot; continue; }
                bool actor = kind == 1, common = kind == 2, vehicle = kind == 3;
                bool selected = actor || common || vehicle && !usedVehicle && item.Vehicles > 0;
                int count = actor ? site.PopulationCounts[lot][n] : common ? site.LootCounts[lot][n]
                    : vehicle && !usedVehicle && loot > 0 ? item.Vehicles : 0;
                usedVehicle |= vehicle;
                // A hound may use a one-actor native group only after a positive human group.
                // This preserves spawn-point distribution and cannot generate a dogs-only base.
                bool companion = actor && site.Plan.Faction == TownFaction.Scrapyard && item.Hounds && !houndUsed && humanReserved > 0 && count == 1 && assets.Hounds.Count > 0;
                if (actor && count > 0) { if (companion) houndUsed = true; else humanReserved += count; }
                if (selected && count == 0 && Get(f, "OnOff") == 0 && Get(f, "number") == 0)
                { f.enabled = false; continue; }
                if (count == 0 || Get(f, "OnOff") > 0 || Get(f, "number") >= count)
                { Set(f, "OnOff", 1); f.Fsm.SetState("spawned"); continue; }
                List<GameObject> pool = null;
                if (actor && (!assets.Population.TryGetValue(site.Plan.Faction, out pool) || pool.Count == 0))
                    throw new InvalidOperationException("No native " + site.Plan.Faction + " population prefabs in this world graph.");
                if (companion) pool = assets.Hounds;
                if (actor)
                    foreach (var proxy in f.GetComponents<PlayMakerArrayListProxy>())
                        if (proxy.referenceName != null && proxy.referenceName.StartsWith("EnemySpawn", StringComparison.Ordinal))
                        {
                            // Inactive native spawn nodes have not run Awake yet. Supply their
                            // authored prefill too, so later activation uses this town's faction.
                            proxy.preFillGameObjectList = new List<GameObject>(pool); proxy.preFillCount = pool.Count;
                            if (proxy.arrayList != null) { proxy.arrayList.Clear(); foreach (var prefab in pool) proxy.arrayList.Add(prefab); }
                        }
                foreach (var state in f.FsmStates) foreach (var action in state.Actions)
                {
                    var roll = action as RandomInt;
                    if (roll != null && roll.storeResult != null && roll.storeResult.Name == "numberEnd")
                    { roll.min = new FsmInt(count); roll.max = new FsmInt(count); roll.inclusiveMax = true; }
                    var select = action as SelectRandomInt;
                    if (actor && select != null && select.storeInt != null && select.storeInt.Name == "index")
                    {
                        select.ints = new FsmInt[pool.Count]; select.weights = new FsmFloat[pool.Count];
                        for (int i = 0; i < pool.Count; i++) { select.ints[i] = new FsmInt(i); select.weights[i] = new FsmFloat(1); }
                    }
                }
                Set(f, "numberEnd", count);
                if (!f.enabled) f.enabled = true;
            }

        }
        // (WW) one stand-alone native spawner (the gas station's): exactly `count` spawns, actors drawn from `pool` (null = items)
        internal static void Force(PlayMakerFSM f, int count, List<GameObject> pool)
        {
            if (f == null) return;
            if (count <= 0 || Get(f, "OnOff") > 0 || Get(f, "numberEnd") > 0 && Get(f, "number") >= Get(f, "numberEnd")) { Set(f, "OnOff", 1); f.Fsm.SetState("spawned"); return; }
            if (pool != null)
                foreach (var proxy in f.GetComponents<PlayMakerArrayListProxy>())
                    if (proxy.referenceName != null && proxy.referenceName.StartsWith("EnemySpawn", StringComparison.Ordinal))
                    {
                        proxy.preFillGameObjectList = new List<GameObject>(pool); proxy.preFillCount = pool.Count;
                        if (proxy.arrayList != null) { proxy.arrayList.Clear(); foreach (var prefab in pool) proxy.arrayList.Add(prefab); }
                    }
            foreach (var state in f.FsmStates) foreach (var action in state.Actions)
            {
                var roll = action as RandomInt;
                if (roll != null && roll.storeResult != null && roll.storeResult.Name == "numberEnd")
                { roll.min = new FsmInt(count); roll.max = new FsmInt(count); roll.inclusiveMax = true; }
                var select = action as SelectRandomInt;
                if (pool != null && select != null && select.storeInt != null && select.storeInt.Name == "index")
                {
                    select.ints = new FsmInt[pool.Count]; select.weights = new FsmFloat[pool.Count];
                    for (int i = 0; i < pool.Count; i++) { select.ints[i] = new FsmInt(i); select.weights[i] = new FsmFloat(1); }
                }
            }
            Set(f, "numberEnd", count);
            if (!f.enabled) f.enabled = true;
        }
        internal static int Read(PlayMakerFSM f, string name) { return f == null ? 0 : Get(f, name); }
        internal static void Write(PlayMakerFSM f, string name, int value) { if (f != null) Set(f, name, value); }
        private static int Get(PlayMakerFSM f, string name) { var v = f.FsmVariables.FindFsmInt(name); return v == null ? 0 : v.Value; }
        private static void Set(PlayMakerFSM f, string name, int value) { var v = f.FsmVariables.FindFsmInt(name); if (v != null) v.Value = value; }
    }
}
