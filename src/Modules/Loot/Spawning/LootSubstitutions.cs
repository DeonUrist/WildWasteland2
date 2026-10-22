using System;
using System.Collections.Generic;
using ES3Internal;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Natural loot substitution (README 6.9; ex Survival NaturalLoot). One CreateObject.OnEnter prefix (Core dispatcher,
    /// subscribed by <see cref="LootModule"/>) watches native natural-loot spawns (ItemSpawner FSM on an "ItemSpawn*" object
    /// creating into its own Item variable) and swaps the prefab before the native action clones, names and saves it.
    /// Sources: first the <see cref="ILootSubstitution"/> rules other modules register through <see cref="ILootSpawning"/>,
    /// then the built-in natural table (one row: 1 in 4 natural gas stoves become a Stew Pot). Replacement prefab ids are resolved
    /// through the shared ES3 GetPrefab resolver chain (<see cref="Hooks.GetPrefab"/>), so the Stew Pot row only applies while
    /// Survival answers its prefab id (7392481500621300201) - exactly when the old Survival-owned hook was installed.
    /// </summary>
    internal static class LootSubstitutions
    {
        /// <summary>One built-in natural table row: native source (name + ES3 prefab id), 1-in-N chance, replacement prefab id.</summary>
        private struct NaturalRow
        {
            /// <summary>Native source prefab name (cheap first filter).</summary>
            internal string SourceName;
            /// <summary>Native source ES3 prefab id.</summary>
            internal long SourceId;
            /// <summary>Replaced when random.Next(OneIn) == 0.</summary>
            internal int OneIn;
            /// <summary>Replacement ES3 prefab id.</summary>
            internal long ReplacementId;
            /// <summary>Name used in log lines.</summary>
            internal string Label;
        }

        /// <summary>A rule registered by another module.</summary>
        private sealed class Rule
        {
            /// <summary>Registration id.</summary>
            internal int Id;
            /// <summary>Registering module (rules are dropped with it).</summary>
            internal IModule Owner;
            /// <summary>The rule.</summary>
            internal ILootSubstitution Impl;
        }

        /// <summary>Natural gas stove (native) -> Stew Pot (Survival), 1 in 4 (unchanged from Survival 0.1.1).</summary>
        private static readonly NaturalRow[] Natural =
        {
            new NaturalRow { SourceName = "gasoline_stove", SourceId = 2571597281285591657L, OneIn = 4, ReplacementId = 7392481500621300201L, Label = "Stew Pot" },
        };

        private static readonly System.Random random = new System.Random();
        private static readonly List<Rule> rules = new List<Rule>();
        private static int nextId;

        /// <summary>Registers a rule of another module; returns its id (positive).</summary>
        internal static int Register(IModule owner, ILootSubstitution rule)
        {
            if (rule == null) throw new ArgumentNullException("rule");
            var r = new Rule { Id = ++nextId, Owner = owner, Impl = rule };
            rules.Add(r);
            if (Log.L1) Log.Main(LootModule.Instance, "substitution rule " + r.Id + " registered by " + (owner != null ? owner.Id : "?"));
            return r.Id;
        }

        /// <summary>Removes a registered rule (no-op for unknown ids).</summary>
        internal static void Remove(int id)
        {
            for (int i = rules.Count - 1; i >= 0; i--) if (rules[i].Id == id) rules.RemoveAt(i);
        }

        /// <summary>Drops every registered rule (Loot shutdown).</summary>
        internal static void Clear() { rules.Clear(); }

        /// <summary>
        /// CreateObject.OnEnter prefix (every PlayMaker CreateObject in the game). Early-outs: FSM name "ItemSpawner", owner name
        /// prefix "ItemSpawn", the action stores into its own source variable, then (no registered rules) the source prefab name.
        /// Never skips the original (returns true); it only swaps the shared Item variable's prefab.
        /// </summary>
        internal static bool BeforeCreate(CreateObject action)
        {
            if (action.Fsm.Name != "ItemSpawner") return true;
            var owner = action.Fsm.GameObject;
            if (owner == null || !owner.name.StartsWith("ItemSpawn", StringComparison.Ordinal)) return true;
            if (!ReferenceEquals(action.gameObject, action.storeObject)) return true;
            var source = action.gameObject.Value;
            if (source == null) return true;
            if (rules.Count > 0 && TryRules(action, owner, source)) return true;
            for (int i = 0; i < Natural.Length; i++)
            {
                if (source.name != Natural[i].SourceName) continue;
                var prefab = source.GetComponent<ES3Prefab>();
                if (prefab == null || prefab.prefabId != Natural[i].SourceId || random.Next(Natural[i].OneIn) != 0) return true;
                Replace(action, owner, Natural[i].SourceId, Natural[i].ReplacementId, Natural[i].Label);
                return true;
            }
            return true;
        }

        /// <summary>Asks the registered rules (in registration order); true when one replaced the prefab.</summary>
        private static bool TryRules(CreateObject action, GameObject owner, GameObject source)
        {
            var prefab = source.GetComponent<ES3Prefab>();
            if (prefab == null) return false;
            var world = Origin.World(owner.transform.position);
            for (int i = 0; i < rules.Count; i++)
            {
                long replacement;
                bool hit;
                try { hit = rules[i].Impl.TrySubstitute(prefab.prefabId, world, out replacement); }
                catch (Exception e) { Log.Warn(LootModule.Instance, "substitution rule of " + (rules[i].Owner != null ? rules[i].Owner.Id : "?") + " failed: " + e.Message); continue; }
                if (hit && Replace(action, owner, prefab.prefabId, replacement, "prefab " + replacement)) return true;
            }
            return false;
        }

        /// <summary>Resolves the replacement prefab and writes it into the action's Item variable (the native action then clones,
        /// names and saves it). False (source kept) when no resolver answers the id.</summary>
        private static bool Replace(CreateObject action, GameObject owner, long sourceId, long replacementId, string label)
        {
            var resolved = Hooks.GetPrefab.Run(replacementId);
            if (resolved == null)
            {
                if (Log.L2) Log.Deep(LootModule.Instance, "natural " + label + " unavailable (prefab id not resolved); source retained: " + owner.name);
                return false;
            }
            action.gameObject.Value = resolved.gameObject;
            if (Log.L2) Log.Deep(LootModule.Instance, "natural " + label + " selected: " + owner.name);
            if (ModEvents.HasListeners<NaturalLootSubstituted>())
            {
                var evt = new NaturalLootSubstituted { SpawnerPath = owner.name, WorldPos = Origin.World(owner.transform.position), OriginalPrefabId = sourceId, NewPrefabId = replacementId };
                ModEvents.Publish(ref evt);
            }
            return true;
        }
    }
}
