using System.Collections.Generic;
using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// One loot table of Loot/LootTables.json (README 6.9 data model): id, display name, kind ("car"/"truck" for tables imported
    /// from PatrolsAndLoot.json profiles, empty otherwise), cargo texture key, roll weight and weighted entries.
    /// Roll rule (<see cref="LootTableStore.Roll"/>): every entry independently yields floor(Weight/100) copies plus one more with
    /// the fractional remainder as probability (the Patrols profile rule); each copy adds Min..Max item keys.
    /// </summary>
    internal sealed class LootTable
    {
        /// <summary>Stable table id.</summary>
        internal string Id = "";
        /// <summary>Display name.</summary>
        internal string Name = "";
        /// <summary>"car", "truck" or "" (free table).</summary>
        internal string Kind = "";
        /// <summary>Cargo container texture key (may be empty).</summary>
        internal string TextureKey = "";
        /// <summary>Shipped default profile.</summary>
        internal bool Default;
        /// <summary>Relative weight when a table is picked among others (truck cargo option roll).</summary>
        internal float SpawnChance = 10;
        /// <summary>Entries (Weight = chance in percent).</summary>
        internal readonly List<LootEntry> Entries = new List<LootEntry>();
        /// <summary>Module that registered the table at runtime (null = from the file).</summary>
        internal IModule Owner;
    }
}
