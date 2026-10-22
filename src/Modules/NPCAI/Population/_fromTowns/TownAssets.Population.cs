using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Nodes.ObjectsGenerators;
using Den.Tools.Splines;
using Den.Tools.Matrices;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Native population catalog of the graph POIs: faction actor pools, Scrapyard Yard Hounds, boss prefab test.
    /// Wave A (A6): partial split of Towns/TownAssets.cs (NPCAI population catalog part, B07); filled by TownAssets.Load.
    /// </summary>
    internal sealed partial class TownAssets
    {
        internal readonly Dictionary<TownFaction, List<GameObject>> Population = new Dictionary<TownFaction, List<GameObject>>();
        internal readonly List<GameObject> Hounds = new List<GameObject>();
        private readonly Dictionary<GameObject, bool> _bosses = new Dictionary<GameObject, bool>();
        internal bool IsBoss(GameObject prefab)
        {
            bool boss; if (_bosses.TryGetValue(prefab, out boss)) return boss;
            boss = prefab.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var fsm in prefab.GetComponentsInChildren<PlayMakerFSM>(true)) if (fsm.FsmName == "BossUI") { boss = true; break; }
            _bosses[prefab] = boss; return boss;
        }
        private void CatalogPopulation(GameObject root)
        {
            foreach (var proxy in root.GetComponentsInChildren<PlayMakerArrayListProxy>(true))
                if (proxy.preFillGameObjectList != null) foreach (var prefab in proxy.preFillGameObjectList)
                {
                    if (prefab == null || IsBoss(prefab)) continue;
                    TownFaction faction;
                    if (prefab.CompareTag("Mutant")) faction = TownFaction.Mutants;
                    else if (prefab.CompareTag("Carnivore") || prefab.CompareTag("Herbivore")) faction = TownFaction.Animals;
                    else if (prefab.CompareTag("Scrapyard")) faction = TownFaction.Scrapyard;
                    else if (prefab.CompareTag("Coyotes")) faction = TownFaction.Coyotes;
                    else continue;
                    NativePoiPopulation.Catalog(prefab, faction);
                    if (faction == TownFaction.Scrapyard || faction == TownFaction.Coyotes)
                    {
                        // Keep accompanying Yard Hounds separate so a base cannot roll only dogs.
                        var animator = prefab.GetComponentInChildren<Animator>(true);
                        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                        {
                            if (faction == TownFaction.Scrapyard && prefab.name == "Yard Hound" && !Hounds.Contains(prefab)) Hounds.Add(prefab);
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
