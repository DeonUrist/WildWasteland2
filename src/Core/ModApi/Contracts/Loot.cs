using System.Collections.Generic;
using UnityEngine;

// Loot contracts (core-modapi.md 8, README 6.9): loot tables + assignment by POI type / town lot, spawner budget/progress,
// substitution rules (stew pot natural loot). Provider: Loot. Consumers: POI, Patrols, Survival, Quests, Tools. FROZEN after W0.
namespace WildWasteland.Core
{
    /// <summary>One weighted entry of a loot table.</summary>
    public struct LootEntry
    {
        /// <summary>Item key (native prefab name or ES3 prefab id as string).</summary>
        public string ItemKey;
        /// <summary>Relative weight.</summary>
        public float Weight;
        /// <summary>Minimum count when rolled.</summary>
        public int Min;
        /// <summary>Maximum count when rolled.</summary>
        public int Max;
    }

    /// <summary>Loot tables and their assignment (PatrolsAndLoot.json loot part, later Loot/LootTables.json).</summary>
    public interface ILootTables
    {
        /// <summary>Increments when tables or assignments change.</summary>
        int Revision { get; }
        /// <summary>All table ids (cleared first).</summary>
        void TableIds(List<string> into);
        /// <summary>Entries of a table (cleared first).</summary>
        bool TryGetTable(string tableId, List<LootEntry> into);
        /// <summary>Table assigned to a POI type (native PoiType name or custom POI id); null = none.</summary>
        string TableFor(string poiType);
        /// <summary>Table assigned to a lot of a town template; null = none.</summary>
        string TableForLot(string townTemplateId, int lot);
        /// <summary>Rolls a table into item keys (cleared first) with the caller's random source.</summary>
        bool Roll(string tableId, System.Random random, List<string> itemKeys);
        /// <summary>Registers or replaces a table from another module (cargo, stew pot).</summary>
        void Register(IModule owner, string tableId, IList<LootEntry> entries);
    }

    /// <summary>Substitution rule (e.g. natural loot -> stew pot) registered by a module.</summary>
    public interface ILootSubstitution
    {
        /// <summary>Returns true and the replacement prefab id when <paramref name="prefabId"/> spawning at a world point is replaced.</summary>
        bool TrySubstitute(long prefabId, Vector3 world, out long replacementPrefabId);
    }

    /// <summary>Spawner budgets, per-instance progress (ES3 key WildWasteland.Towns.PoiDressMarkers.v1 stays) and substitutions.</summary>
    public interface ILootSpawning
    {
        /// <summary>Remaining spawn budget of an owner (town/POI/marker key).</summary>
        bool TryBudget(string ownerKey, out int remaining);
        /// <summary>Records spawner progress (onOff, number, numberEnd) of a spawner path under an owner key.</summary>
        void ReportProgress(string ownerKey, string spawnerPath, bool on, int number, int numberEnd);
        /// <summary>Saved progress of a spawner.</summary>
        bool TryProgress(string ownerKey, string spawnerPath, out bool on, out int number, out int numberEnd);
        /// <summary>Registers a substitution rule; returns an id.</summary>
        int RegisterSubstitution(IModule owner, ILootSubstitution rule);
        /// <summary>Removes a substitution rule.</summary>
        void RemoveSubstitution(int id);
        /// <summary>Spawn work fraction done for the loading screen (0..1; 1 when idle).</summary>
        float Progress { get; }
    }
}
