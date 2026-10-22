using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary><see cref="ILootSpawning"/> provider (ModApi, owner <see cref="LootModule"/>): spawner progress and budgets
    /// (<see cref="SpawnerProgress"/>), substitution rules (<see cref="LootSubstitutions"/>) and the marker catalog scan progress.</summary>
    internal sealed class LootSpawningApi : ILootSpawning
    {
        /// <summary>Remaining spawns of an owner key (unfinished spawners' numberEnd - number); false when the owner is unknown.</summary>
        public bool TryBudget(string ownerKey, out int remaining) { return SpawnerProgress.TryBudget(ownerKey, out remaining); }

        /// <summary>Records spawner progress under an owner key (owner "PoiDressMarkers" = POI marker groups, saved with the slot).</summary>
        public void ReportProgress(string ownerKey, string spawnerPath, bool on, int number, int numberEnd) { SpawnerProgress.Report(ownerKey, spawnerPath, on, number, numberEnd); }

        /// <summary>Saved progress of a spawner.</summary>
        public bool TryProgress(string ownerKey, string spawnerPath, out bool on, out int number, out int numberEnd) { return SpawnerProgress.TryGet(ownerKey, spawnerPath, out on, out number, out numberEnd); }

        /// <summary>Registers a natural loot substitution rule (asked before the built-in natural table); returns its id.</summary>
        public int RegisterSubstitution(IModule owner, ILootSubstitution rule) { return LootSubstitutions.Register(owner, rule); }

        /// <summary>Removes a substitution rule.</summary>
        public void RemoveSubstitution(int id) { LootSubstitutions.Remove(id); }

        /// <summary>Fraction of the running POI marker spawn-list scan (1 when idle).</summary>
        public float Progress { get { return SpawnerProgress.ScanProgress; } }
    }
}
