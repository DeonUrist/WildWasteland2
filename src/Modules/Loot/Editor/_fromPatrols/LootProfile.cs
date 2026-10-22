using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    // Loot profiles of PatrolsAndLoot.json (LootItem, LootProfile); Loot data model (Wave A split of Patrols Editor/EditorData.cs).
    public sealed class LootItem
    {
        public string Id = "";
        public float Chance;
    }

    public sealed class LootProfile
    {
        public string Id = "", Name = "", TextureKey = "";
        public bool Default;
        public float SpawnChance = 10;
        public List<LootItem> Items = new List<LootItem>();

        public static int RollCount(double percent, Func<double> random)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent) || percent <= 0) return 0;
            double count = Math.Min(100000d, percent) / 100d;
            int whole = (int)Math.Floor(count);
            return whole + (count > whole && random() < count - whole ? 1 : 0);
        }

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
