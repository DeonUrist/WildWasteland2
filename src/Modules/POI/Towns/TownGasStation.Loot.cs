using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Desert gas-station loot (Loot, ex Towns/TownGasStation.cs): native ItemSpawner templates cloned into the shop, seeded
    /// counts, saved progress restored (the station record itself is saved by POI under WildWasteland.Towns.GasStations.v1).
    /// Station/asset/plan types are POI's (REFACTOR-REQUEST B15-2). Namespace kept for Wave C.
    /// </summary>
    internal sealed partial class TownGasStation
    {
        /// <summary>Resolves the current Loot-owned native spawner service.</summary>
        private static INativeSpawners Spawners { get { return SpawnerControl.Required; } }

        private static readonly Vector3[] LootPoints = { new Vector3(7f, .05f, 6.5f), new Vector3(7f, .05f, -3f), new Vector3(8.5f, .05f, 11f), new Vector3(6.5f, .05f, -6.5f) };

        /// <summary>
        /// Fills a desert station: the smallest native loot spawner of the town catalog cloned to four shop points with a seeded
        /// 1-2 count each, unless the authored "TownsGasStation" POI layout already has Items/CarParts markers. The animals/enemy
        /// rolls stay (seeded counts and saved spawner indices unchanged) although the enemy loop was deleted (README 7).
        /// </summary>
        private void Populate(Station station, TownAssets assets)
        {
            Transform loot = null, enemy = null; int lootKids = int.MaxValue, enemyKids = int.MaxValue;
            foreach (var asset in assets.Buildings.Values)
                for (int i = 0; i < asset.Spawners.Length; i++)
                {
                    int kind = asset.Budgets[i].Kind; if (kind != 1 && kind != 2) continue;
                    var node = asset.Spawners[i].Length == 0 ? null : asset.Prefab.transform.Find(asset.Spawners[i]); if (node == null) continue;
                    int kids = node.GetComponentsInChildren<Transform>(true).Length;
                    if (kind == 2 && kids < lootKids) { loot = node; lootKids = kids; }
                    if (kind == 1 && kids < enemyKids) { enemy = node; enemyKids = kids; }
                }
            if (loot == null && enemy == null) { Log.Warn(null,"[POI] desert gas station: no native spawner templates in the catalog"); return; }
            var random = new System.Random(TownPlanner.Hash(station.Key + "/gasstation/population"));
            bool animals = random.Next(2) == 0;
            List<GameObject> pool;
            if (!assets.Population.TryGetValue(animals ? TownFaction.Animals : TownFaction.Scrapyard, out pool) || pool.Count == 0)
                assets.Population.TryGetValue(animals ? TownFaction.Scrapyard : TownFaction.Animals, out pool);
            int enemies = 2 + random.Next(2);
            SpawnerState[] saved; _saved.TryGetValue(station.Key, out saved);
            int n = 0;
            bool authoredLoot = false;
            try { foreach (var prop in PoiDressStore.Load("TownsGasStation").Props) if (prop.Kind == "Items" || prop.Kind == "CarParts") { authoredLoot = true; break; } }
            catch (Exception e) { Log.Warn(null,"[POI] gas station retains native loot: " + e.Message); }
            for (int i = 0; !authoredLoot && i < LootPoints.Length && loot != null; i++, n++) Add(station, loot, LootPoints[i], "ItemSpawn_gas_" + i, 1 + random.Next(2), null, saved, n);
        }

        /// <summary>Clones one native spawner template into the station, restores its saved progress when the saved row has the same
        /// name, forces the count and registers it with the station.</summary>
        private void Add(Station station, Transform template, Vector3 local, string name, int count, List<GameObject> pool, SpawnerState[] saved, int index)
        {
            var go = Object.Instantiate(template.gameObject, station.Root.transform, false);
            go.name = name; go.transform.localPosition = local; go.transform.localRotation = Quaternion.identity; go.SetActive(true);
            var f = FsmUtil.Find(go, "ItemSpawner"); if (f == null) { Object.Destroy(go); return; }
            if (saved != null && index < saved.Length && saved[index] != null && saved[index].Path == name)
            {
                Spawners.Write(f, "OnOff", saved[index].OnOff); Spawners.Write(f, "number", saved[index].Number); Spawners.Write(f, "numberEnd", saved[index].NumberEnd);
            }
            Spawners.Force(f, count, pool);
            station.Spawners.Add(f);
        }
    }
}
