using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Loot part of the Patrols editor store: loot profile lookup, truck loot roll and cargo texture key (Loot store, B15).</summary>
    internal static partial class EditorStore
    {
        internal static LootProfile Loot(bool truck, string id) { return (truck ? Data.TruckLoot : Data.CarLoot).FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)); }
        internal static LootProfile RollTruckLoot(Func<double> random, IEnumerable<string> allowed = null)
        {
            var filter = allowed != null ? new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase) : null;
            var eligible = Data.TruckLoot.Where(p => p.SpawnChance > 0 && (filter == null || filter.Contains(p.Id))).ToList(); double total = eligible.Sum(p => (double)p.SpawnChance);
            if (total <= 0) return null;
            double roll = Math.Min(.999999999999, Math.Max(0, random())) * total;
            foreach (var p in eligible) { roll -= p.SpawnChance; if (roll < 0) return p; }
            return eligible.LastOrDefault();
        }

        internal static string CargoTexture(Dictionary<string, int> items, string hint)
        {
            return string.IsNullOrWhiteSpace(hint) ? "none" : hint;

        }
    }
}
