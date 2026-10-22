using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ES3Internal;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Desert gas-station loot: native ItemSpawner templates cloned into the shop, seeded counts, saved progress restored.
    /// Wave A (A6): partial split of Towns/TownGasStation.cs (Loot part, B15).
    /// </summary>
    internal sealed partial class TownGasStation
    {
        private static readonly Vector3[] LootPoints = { new Vector3(7f, .05f, 6.5f), new Vector3(7f, .05f, -3f), new Vector3(8.5f, .05f, 11f), new Vector3(6.5f, .05f, -6.5f) };
        // 4 loot spawners in the shop + 2-3 Scrapyard OR animal spawners, cloned from native town templates; saved progress restored
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
            if (loot == null && enemy == null) { Log.Warn("[Towns] desert gas station: no native spawner templates in the catalog"); return; }
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
            catch (Exception e) { Log.Warn("[Towns] gas station retains native loot: " + e.Message); }
            for (int i = 0; !authoredLoot && i < LootPoints.Length && loot != null; i++, n++) Add(station, loot, LootPoints[i], "ItemSpawn_gas_" + i, 1 + random.Next(2), null, saved, n);
            // (WW) desert stations are loot only: the disabled enemy-spawner loop was deleted in the refactor (README 7). The
            // animals/enemies rolls above stay so the seeded loot counts and saved spawner indices are unchanged.
        }
        private void Add(Station station, Transform template, Vector3 local, string name, int count, List<GameObject> pool, SpawnerState[] saved, int index)
        {
            var go = Object.Instantiate(template.gameObject, station.Root.transform, false);
            go.name = name; go.transform.localPosition = local; go.transform.localRotation = Quaternion.identity; go.SetActive(true);
            var f = FsmUtil.Find(go, "ItemSpawner"); if (f == null) { Object.Destroy(go); return; }
            if (saved != null && index < saved.Length && saved[index] != null && saved[index].Path == name)
            {
                TownPopulation.Write(f, "OnOff", saved[index].OnOff); TownPopulation.Write(f, "number", saved[index].Number); TownPopulation.Write(f, "numberEnd", saved[index].NumberEnd);
            }
            TownPopulation.Force(f, count, pool);
            station.Spawners.Add(f);
        }
    }
}
