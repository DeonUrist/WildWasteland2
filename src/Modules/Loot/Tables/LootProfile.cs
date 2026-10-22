using System;
using System.Collections.Generic;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Car/truck loot profile of PatrolsAndLoot.json (schema 3, "carLoot"/"truckLoot"): items with chances, cargo texture key and
    /// the truck roll weight. Loot data model (Wave A split of Patrols Editor/EditorData.cs); Loot mirrors these profiles as
    /// ILootTables tables (LootTableStore). Namespace kept for Wave C.
    /// </summary>
    public sealed class LootProfile
    {
        /// <summary>Stable id ("truck-food"), display name and cargo container texture key.</summary>
        public string Id = "", Name = "", TextureKey = "";
        /// <summary>Shipped default profile.</summary>
        public bool Default;
        /// <summary>Relative weight when a truck rolls its cargo option.</summary>
        public float SpawnChance = 10;
        /// <summary>Item rows.</summary>
        public List<LootItem> Items = new List<LootItem>();

        /// <summary>Number of copies for a chance in percent: floor(percent/100) plus one more with the fractional remainder as probability.</summary>
        public static int RollCount(double percent, Func<double> random)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent) || percent <= 0) return 0;
            double count = Math.Min(100000d, percent) / 100d;
            int whole = (int)Math.Floor(count);
            return whole + (count > whole && random() < count - whole ? 1 : 0);
        }

        /// <summary>Rolls every item row into prefab name -> count (case-insensitive keys, rows with the same id add up).</summary>
        public Dictionary<string, int> Roll(Func<double> random)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items)
            {
                int count = RollCount(item.Chance, random);
                if (count <= 0 || string.IsNullOrWhiteSpace(item.Id)) continue;
                int previous; result.TryGetValue(item.Id, out previous);
                result[item.Id] = previous + count;
            }
            return result;
        }
    }
}
