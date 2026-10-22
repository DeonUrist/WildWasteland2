using System.Collections.Generic;
using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary><see cref="ILootTables"/> provider (ModApi, owner <see cref="LootModule"/>): a thin view over the module's <see cref="LootTableStore"/>.</summary>
    internal sealed class LootTablesApi : ILootTables
    {
        private readonly LootTableStore _store;

        /// <summary>Wraps the store.</summary>
        internal LootTablesApi(LootTableStore store) { _store = store; }

        /// <summary>Increments when tables or assignments change.</summary>
        public int Revision { get { return _store.Revision; } }

        /// <summary>All table ids (cleared first).</summary>
        public void TableIds(List<string> into) { _store.TableIds(into); }

        /// <summary>Entries of a table (cleared first); false when unknown.</summary>
        public bool TryGetTable(string tableId, List<LootEntry> into) { return _store.TryGetTable(tableId, into); }

        /// <summary>Table assigned to a POI type; null = none.</summary>
        public string TableFor(string poiType) { return _store.TableFor(poiType); }

        /// <summary>Table assigned to a lot of a town template; null = none.</summary>
        public string TableForLot(string townTemplateId, int lot) { return _store.TableForLot(townTemplateId, lot); }

        /// <summary>Rolls a table into item keys (cleared first) with the caller's random source.</summary>
        public bool Roll(string tableId, System.Random random, List<string> itemKeys) { return _store.Roll(tableId, random, itemKeys); }

        /// <summary>Registers or replaces a runtime table of another module (not written to disk).</summary>
        public void Register(IModule owner, string tableId, IList<LootEntry> entries) { _store.Register(owner, tableId, entries); }
    }
}
