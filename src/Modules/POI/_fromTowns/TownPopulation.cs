using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Town population/loot plan rolls (Assign), faction classification, faction member counting and the per-town percentage roll.
    /// Wave A (A6): POI part of Towns/TownPopulation.cs; spawner control in Loot/_fromTowns/TownPopulation.Spawners.cs.
    /// Legacy(plan) only serves TownStore v1 records (D12 old-schema reader, left for B13).
    /// </summary>
    internal static partial class TownPopulation
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
        private static int Percentage(TownPlan plan, int min, int max, string kind)
        {
            int lo = Math.Min(min, max), hi = Math.Max(min, max);
            return new System.Random(TownPlanner.Hash(plan.Key + "/town/" + kind + "/" + lo + "/" + hi)).Next(lo, hi + 1);
        }
    }
}
