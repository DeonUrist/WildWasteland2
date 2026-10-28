using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Control of native PlayMaker "ItemSpawner" FSMs (README 6.9 spawner budget/progress; ex Towns TownPopulation/TownFactory
    /// spawner helpers, one copy now): int variables (OnOff, number, numberEnd), native count budget capture, forcing an exact
    /// count with an optional actor pool, and resuming a partly completed spawn loop. Stateless; used by the town, gas station
    /// and POI marker spawners. Called at town build/load time, never per frame.
    /// </summary>
    internal static class NativeSpawners
    {
        /// <summary>Int variable of a spawner FSM (0 when the FSM or the variable is missing).</summary>
        internal static int Read(PlayMakerFSM f, string name)
        {
            var v = f == null ? null : f.FsmVariables.FindFsmInt(name);
            return v == null ? 0 : v.Value;
        }

        /// <summary>Sets an int variable of a spawner FSM (no-op when the FSM or the variable is missing).</summary>
        internal static void Write(PlayMakerFSM f, string name, int value)
        {
            var v = f == null ? null : f.FsmVariables.FindFsmInt(name);
            if (v != null) v.Value = value;
        }

        /// <summary>
        /// Native count budget of a spawner prefab FSM from its object name (EnemySpawn/ItemSpawn/CarSpawn/...) and its numberEnd
        /// RandomInt (authored ItemNum overrides included). Throws when an enemy/loot group has no native count roll or an absurd
        /// count, so callers can disable the group instead of inventing a baseline.
        /// </summary>
        internal static NativeSpawnerBudget Capture(PlayMakerFSM f)
        {
            if (f == null) return new NativeSpawnerBudget();
            string name = f.gameObject.name;
            int kind = name.StartsWith("EnemySpawn", StringComparison.Ordinal) ? 1 : name.StartsWith("ItemSpawn", StringComparison.Ordinal) ? 2
                : name.StartsWith("CarSpawn", StringComparison.Ordinal) || name.StartsWith("TrailerSpawn", StringComparison.Ordinal) ? 3
                : name.StartsWith("CritterSpawn", StringComparison.Ordinal) || name.StartsWith("CrateSpawn", StringComparison.Ordinal) ? 0 : 4;
            var budget = new NativeSpawnerBudget { Kind = kind };
            if (kind == 1 && name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("bodyguard", StringComparison.OrdinalIgnoreCase) < 0) { budget.Kind = 6; return budget; }
            if (kind != 1 && kind != 2) return budget;
            // Native unique-item FSMs select one item (or iterate authored child points), with no
            // numberEnd roll. Preserve that authored flow; zero loot can still suspend it.
            if (kind == 2 && name.StartsWith("ItemSpawn_Unique", StringComparison.Ordinal))
            { budget.Kind = 5; return budget; }
            if (kind == 1 && name == "EnemySpawn_Wild_Attack")
            {
                // Native roll 0 means no attack; 1..5 choose a species and its authored fixed count.
                var trigger = f.Fsm.GetState("trigger"); RandomInt choice = null;
                if (trigger != null) foreach (var action in trigger.Actions)
                { var roll = action as RandomInt; if (roll != null && roll.storeResult != null && roll.storeResult.Name == "random") choice = roll; }
                if (choice == null || NativeValue(f, choice.min) != 0 || NativeValue(f, choice.max) - (choice.inclusiveMax ? 0 : 1) != 5)
                    throw new InvalidOperationException("Unsupported native wild attack selection: " + name);
                budget.Counts = new int[6];
                string[] branches = { "rat", "wildhound", "nightwalker", "scorpion", "zombie" };
                for (int i = 0; i < branches.Length; i++)
                {
                    var state = f.Fsm.GetState(branches[i]); bool found = false;
                    if (state != null) foreach (var action in state.Actions)
                    {
                        var set = action as SetIntValue;
                        if (set == null || set.intVariable == null || set.intVariable.Name != "numberEnd") continue;
                        int count = NativeValue(f, set.intValue);
                        if (count < 0 || count > 100000) throw new InvalidOperationException("Invalid native wild attack count: " + name);
                        budget.Counts[i + 1] = count; budget.Max = Mathf.Max(budget.Max, count); found = true;
                    }
                    if (!found) throw new InvalidOperationException("Native wild attack count missing: " + branches[i]);
                }
                return budget;
            }
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

        /// <summary>Value of a RandomInt bound: its own value, the named FSM variable, or a sibling ItemNum FSM's SetFsmInt override.</summary>
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

        /// <summary>
        /// (WW) A stand-alone native spawner (gas station, POI marker): exactly <paramref name="count"/> spawns, actors drawn from
        /// <paramref name="pool"/> (null = items). A finished or zero group is switched off and parked in "spawned".
        /// </summary>
        internal static void Force(PlayMakerFSM f, int count, List<GameObject> pool)
        {
            if (f == null) return;
            if (count <= 0 || Read(f, "OnOff") > 0 || Read(f, "numberEnd") > 0 && Read(f, "number") >= Read(f, "numberEnd")) { Write(f, "OnOff", 1); f.Fsm.SetState("spawned"); return; }
            Apply(f, count, pool);
        }

        /// <summary>
        /// Rewrites a spawner for exactly <paramref name="count"/> spawns: actor pool into the EnemySpawn array proxies (also the
        /// prefill, for nodes that have not run Awake), numberEnd roll fixed to count, index selector over the pool (actors only),
        /// numberEnd set and the FSM enabled.
        /// </summary>
        internal static void Apply(PlayMakerFSM f, int count, List<GameObject> pool)
        {
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
            if (pool != null && f.gameObject.name == "EnemySpawn_Wild_Attack")
            {
                // Town clones use the allocated faction/count, bypassing the native species branches.
                var item = f.Fsm.GetState("getItem");
                if (!(item.Actions[0] is SelectRandomInt)) item.Actions = new[] { f.Fsm.GetState("rat").Actions[0], item.Actions[0], item.Actions[1] };
                foreach (var state in f.FsmStates) if (state.Name == "trigger" || state.Name == "spawn")
                    foreach (var transition in state.Transitions) if (transition.ToState == "random")
                    { transition.ToState = item.Name; transition.ToFsmState = item; }
            }
            Write(f, "numberEnd", count);
            if (!f.enabled) f.enabled = true;
        }

        /// <summary>Restricts a spawner's index selector to the given native list indices and weights (POI marker item lists).</summary>
        internal static void Select(PlayMakerFSM f, int[] indices, float[] weights)
        {
            foreach (var state in f.FsmStates) foreach (var action in state.Actions)
            {
                var select = action as SelectRandomInt;
                if (select != null && select.storeInt != null && select.storeInt.Name == "index")
                {
                    select.ints = new FsmInt[indices.Length]; select.weights = new FsmFloat[weights.Length];
                    for (int i = 0; i < select.ints.Length; i++) { select.ints[i] = new FsmInt(indices[i]); select.weights[i] = new FsmFloat(weights[i]); }
                }
            }
        }

        /// <summary>Restarts an enabled, partly completed spawn loop that is parked in "wait"/"trigger" (after a town load).</summary>
        internal static void Resume(PlayMakerFSM f)
        {
            if (f == null || !f.enabled || Read(f, "OnOff") > 0 || Read(f, "number") <= 0 || Read(f, "numberEnd") <= Read(f, "number")) return;
            if (f.ActiveStateName == "wait" || f.ActiveStateName == "trigger") f.Fsm.SetState(f.gameObject.name == "EnemySpawn_Wild_Attack" ? "getItem" : "getRandom int");
        }
    }
}
