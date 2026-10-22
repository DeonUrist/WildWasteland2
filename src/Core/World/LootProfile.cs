using System;
using System.Collections.Generic;

namespace WildWasteland.Core
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

    }
}
