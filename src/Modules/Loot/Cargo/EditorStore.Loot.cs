using System;
using System.Collections.Generic;
using System.Linq;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Loot part of the Patrols editor store (Loot, ex Patrols Editor/EditorStore.cs): loot profile lookup, truck cargo option roll
    /// and the cargo texture key. Profiles are the Patrols editor data (PatrolsAndLoot.json, mirrored into Loot/LootTables.json
    /// by LootTableStore); REFACTOR-REQUEST B15-3. Namespace kept for Wave C.
    /// </summary>
    internal static partial class EditorStore
    {
        /// <summary>Truck or car loot profile by id (case-insensitive), or null.</summary>
        internal static LootProfile Loot(bool truck, string id) { return (truck ? Data.TruckLoot : Data.CarLoot).FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)); }

        /// <summary>Weighted roll (SpawnChance) of a truck cargo option among the allowed ids (all when null); null when none is eligible.</summary>
        internal static LootProfile RollTruckLoot(Func<double> random, IEnumerable<string> allowed = null)
        {
            var filter = allowed != null ? new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase) : null;
            var eligible = Data.TruckLoot.Where(p => p.SpawnChance > 0 && (filter == null || filter.Contains(p.Id))).ToList(); double total = eligible.Sum(p => (double)p.SpawnChance);
            if (total <= 0) return null;
            double roll = Math.Min(.999999999999, Math.Max(0, random())) * total;
            foreach (var p in eligible) { roll -= p.SpawnChance; if (roll < 0) return p; }
            return eligible.LastOrDefault();
        }

        /// <summary>Cargo container texture key: the profile's hint, "none" when empty (the rolled items do not matter).</summary>
        internal static string CargoTexture(Dictionary<string, int> items, string hint)
        {
            return string.IsNullOrWhiteSpace(hint) ? "none" : hint;
        }
    }
}
