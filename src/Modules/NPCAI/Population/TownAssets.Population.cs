using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Nodes.ObjectsGenerators;
using Den.Tools.Splines;
using Den.Tools.Matrices;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Native population catalog of the graph POIs: faction actor pools, Scrapyard Yard Hounds, boss prefab test.
    /// Wave A (A6): partial split of Towns/TownAssets.cs (NPCAI population catalog part, B07); filled by TownAssets.Load.
    /// </summary>
    internal sealed partial class TownAssets
    {
        internal readonly Dictionary<TownFaction, List<GameObject>> Population = new Dictionary<TownFaction, List<GameObject>>();   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
        internal readonly List<GameObject> Hounds = new List<GameObject>();
        private readonly Dictionary<GameObject, bool> _bosses = new Dictionary<GameObject, bool>();
        /// <summary>True for a boss prefab (name or BossUI FSM; cached per prefab).</summary>
        internal bool IsBoss(GameObject prefab)
        {
            bool boss; if (_bosses.TryGetValue(prefab, out boss)) return boss;
            boss = prefab.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var fsm in prefab.GetComponentsInChildren<PlayMakerFSM>(true)) if (fsm.FsmName == "BossUI") { boss = true; break; }
            _bosses[prefab] = boss; return boss;
        }
        /// <summary>Catalogs the town population pools of a graph POI root (faction actors, Scrapyard hounds kept apart) and feeds the native-POI variety pools.</summary>
        private void CatalogPopulation(GameObject root)
        {
            foreach (var proxy in root.GetComponentsInChildren<PlayMakerArrayListProxy>(true))
                if (proxy.preFillGameObjectList != null) foreach (var prefab in proxy.preFillGameObjectList)
                {
                    if (prefab == null || IsBoss(prefab)) continue;
                    TownFaction faction;   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    if (prefab.CompareTag("Mutant")) faction = TownFaction.Mutants;   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    else if (prefab.CompareTag("Carnivore") || prefab.CompareTag("Herbivore")) faction = TownFaction.Animals;   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    else if (prefab.CompareTag("Scrapyard")) faction = TownFaction.Scrapyard;   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    else if (prefab.CompareTag("Coyotes")) faction = TownFaction.Coyotes;   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    else continue;
                    NativePoiPopulation.Catalog(prefab, faction.ToString());
                    if (faction == TownFaction.Scrapyard || faction == TownFaction.Coyotes)   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                    {
                        // Keep accompanying Yard Hounds separate so a base cannot roll only dogs.
                        var animator = prefab.GetComponentInChildren<Animator>(true);
                        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                        {
                            if (faction == TownFaction.Scrapyard && prefab.name == "Yard Hound" && !Hounds.Contains(prefab)) Hounds.Add(prefab);   // refs-ok: REFACTOR-REQUEST B07-9 (POI town types until ITownsApi/IPopulation callers)
                            continue;
                        }
                    }
                    if (prefab.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0 || prefab.name.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0
                        || prefab.name.IndexOf("Mechanic", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    List<GameObject> pool; if (!Population.TryGetValue(faction, out pool)) Population.Add(faction, pool = new List<GameObject>());
                    if (!pool.Contains(prefab)) pool.Add(prefab);
                }
        }
    }
}
