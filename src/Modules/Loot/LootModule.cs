using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Loot module entry (README 6.9): natural loot substitution (CreateObject hook + substitution rules), loot tables and their
    /// assignment (<see cref="ILootTables"/>), spawner progress and substitution registry (<see cref="ILootSpawning"/>).
    /// [Loot] Enabled is bound by Core; Loot binds no further entries (D12: no migration). Data: config/WildWasteland/Loot/.
    /// Town/POI spawner control, cargo loading, the loot editor's item catalog, merchant goods and the quest item query live in
    /// this folder too but are still called by their old owners (see docs/refactor/reports/B15.md).
    /// </summary>
    public sealed class LootModule : ModuleBase
    {
        /// <summary>The running instance (owner for logs, hooks and events); null before construction.</summary>
        internal static LootModule Instance;

        /// <summary>Registers the instance used by the module's static services.</summary>
        public LootModule() { Instance = this; }

        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Loot"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Loot"; } }

        /// <summary>Loot tables and assignments (LootTables.json + runtime tables).</summary>
        internal readonly LootTableStore Tables = new LootTableStore();

        /// <summary>No entries beyond [Loot] Enabled (bound by Core).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Installs the natural loot substitution prefix on the shared CreateObject.OnEnter dispatcher and loads the loot
        /// tables (copy-missing import of the PatrolsAndLoot.json loot part).</summary>
        public override void Init()
        {
            Hooks.CreateObjectEnter.Prefix(this, LootSubstitutions.BeforeCreate);
            try { Tables.Load(Assets.ModuleConfigDir(Id)); }
            catch (System.Exception e) { Log.Warn(this, "loot tables not loaded: " + e.Message); }
        }

        /// <summary>Provides <see cref="ILootTables"/> and <see cref="ILootSpawning"/>.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<INativeSpawners>(this, new NativeSpawnerApi());
            ModApi.Provide<ILootTables>(this, new LootTablesApi(Tables));
            ModApi.Provide<ILootSpawning>(this, new LootSpawningApi());
        }

        /// <summary>Re-imports Patrols loot profiles when PatrolsAndLoot.json changed (one file stamp read per world ready).</summary>
        public override void OnWorldReady()
        {
            Tables.Refresh();
        }

        /// <summary>Drops the substitution rules other modules registered (Core removes the hook handler).</summary>
        public override void Shutdown()
        {
            LootSubstitutions.Clear();
            Tables.ClearRuntime();
        }
    }
}
