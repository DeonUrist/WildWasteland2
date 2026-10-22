using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// All Towns config entries (raw ConfigFile.Bind into settings.cfg, literal [Towns / ...] sections) and the generation snapshot.
    /// Wave A (A6): POI part of Towns/TownSettings.cs; editor/test entries in Tools/_fromTowns/TownSettings.Tools.cs. D12: the first-run
    /// import from com.denis.apocalypter.wildwasteland.towns.cfg is gone.
    /// </summary>
    internal sealed partial class TownSettings
    {
        internal ConfigEntry<int> Min, Max, Chance, Inhabited;
        internal ConfigEntry<float> Relief, Earthwork, Grade, Feather;
        internal ConfigEntry<string> BuildingPrefabs, RoadLayer;
        internal ConfigEntry<float> Gap;
        internal ConfigEntry<float> AccessLength;
        internal ConfigEntry<bool> EnsureTown;
        internal ConfigEntry<bool> PoiNearSpawn;
        internal ConfigEntry<bool> Walls;
        internal ConfigEntry<float> LoadingBudgetMs;
        internal ConfigEntry<int> DesertGasStationChance;
        internal ConfigEntry<bool> GasStationParking;
        internal ConfigFile File;
        /// <summary>Binds the Tools-owned editor/test entries first (keeps the original bind order); implemented in Tools/_fromTowns.</summary>
        partial void BindTools(ConfigFile file);
        // Raw ConfigFile binder (settings.cfg via ModuleConfig.Main); the standalone-DLL entry BindStandalone is gone (README 7).
        /// <summary>Attaches managed population observations without adding components to saved actors.</summary>
        internal void Bind(ConfigFile file)
        {
            File = file;
            BindTools(file);
            DesertGasStationChance = file.Bind("POI / World", "Desert gas station chance (%)", 8, new ConfigDescription("Chance per generated 5 km block of a roadside gas station POI (native loot inside, Scrapyard or animal spawners). Changes affect new/undecided blocks only after a world reload.", new AcceptableValueRange<int>(0, 100)));
            GasStationParking = file.Bind("POI / World", "Gas station parking lots", true, "Place a gas station beside the open ground parking lot outside the town walls, with a two metre gap. Reload after changing.");
            Walls = file.Bind("POI / Debug", "Town wall prototype", true, "Build native metal perimeter walls and four open cardinal gates around owned Coyote/Scrapyard towns. Parking remains just outside the east gate. Restart or reload after changing; buildings retain their saved positions.");
            LoadingBudgetMs = file.Bind("POI / Debug", "Loading build budget (ms)", 12f, new ConfigDescription("Extra town construction time per frame while a world is loading or generating (nobody is playing yet). Normal play keeps the shared scheduler budget and one prefab per frame.", new AcceptableValueRange<float>(0f, 40f)));
            PoiNearSpawn = file.Bind("POI / World", "New POI near spawn", false, "Allow newly generated Towns and War Rig in the first 5 km chunk around the starting camp. Off excludes new locations in chunk 0/0; existing saved towns remain. Changes affect new/undecided locations.");
            Min = Int(file, "Minimum buildings", 5, 3, 32, "Minimum building lots per town. The larger of minimum/maximum is used as the maximum.");
            Max = Int(file, "Maximum buildings", 8, 3, 32, "Maximum lots per town, inclusive: one open 24 x 40 m parking slot, remaining slots use native test buildings.");
            Chance = Int(file, "Town chance (%)", 20, 0, 100, "Independent town chance per generated 5 km block. WORLD > POI. Selected blocks retry layouts/sites; Ensure selected towns allows the flattest available site when terrain targets cannot be met.");
            EnsureTown = file.Bind("POI / Generation", "Ensure selected towns", true, "After a successful chance roll, use the flattest available unoccupied site if the relief/cut-fill/grade targets cannot be met. Keep building counts, global blockers and connection to a real road. Unavailable sites remain pending with an explanation.");
            Inhabited = Int(file, "Inhabited towns (%)", 50, 0, 100, "Fraction of human Scrapyard/Coyotes faction bases rather than animal/mutant ruins. Test Building_N models retain their native appearance; roles do not yet create service merchants.");
            Gap = Float(file, "Gap between buildings", 2, .5f, 6, "Typical longitudinal gap between each building's actual footprint; bake margins may overlap and share global blockers.");
            Relief = Float(file, "Maximum original relief", 6, .5f, 20, "Preferred terrain height range over the pad and feather. A strict limit with Ensure selected towns off; otherwise the flattest available fallback may exceed it.");
            Earthwork = Float(file, "Maximum cut or fill", 3, .25f, 10, "Preferred terrain-to-pad elevation difference. Ensure selected towns permits more for the flattest available fallback site.");
            Grade = Float(file, "Maximum original grade", .15f, .01f, .4f, "Preferred original neighbour rise/run before flattening. .15 means 15 percent. A strict limit only with Ensure selected towns off.");
            Feather = Float(file, "Pad transition width", 35, 10, 100, "Width in metres of the smooth transition from the level town to the unchanged landscape.");
            BuildingPrefabs = file.Bind("POI / Assets", "Test building prefabs", "", "Comma-separated existing Building_N names; blank uses all Building_N prefabs in the current MapMagic graph. Restart after changing. Native prefab geometry, scales and bake identities are preserved.");
            RoadLayer = file.Bind("POI / Assets", "Road terrain layer", "", "Optional exact TerrainLayer name. Blank follows the graph's roadStroke texture input. Missing/ambiguous road artwork rejects town generation instead of inventing a texture. Restart after changing.");
            AccessLength = Float(file, "Maximum access road length", 900, 100, 1800, "Maximum straight-line road search distance in metres. New towns require a nearby native road and a route with acceptable terrain grade.");
        }
        /// <summary>Attaches managed population observations without adding components to saved actors.</summary>
        internal void Bind(ModuleConfig cfg)
        {
            Bind(cfg.Tools);
        }
        /// <summary>Binds a bounded integer generation setting in the module tuning file.</summary>
        private static ConfigEntry<int> Int(ConfigFile f, string k, int d, int min, int max, string desc)
        { return f.Bind("POI / Generation", k, d, new ConfigDescription(desc, new AcceptableValueRange<int>(min, max))); }
        /// <summary>Binds a bounded floating-point generation setting in the module tuning file.</summary>
        private static ConfigEntry<float> Float(ConfigFile f, string k, float d, float min, float max, string desc)
        { return f.Bind("POI / Generation", k, d, new ConfigDescription(desc, new AcceptableValueRange<float>(min, max))); }
        /// <summary>Captures immutable settings and saved intent for a terrain worker.</summary>
        internal GenerationInput Snapshot(string key, int seed, BuildingSpec[] specs, TownPlan saved)
        {
            // Retry empty placement failures. At 100%, old empty chance decisions are also eligible.
            // Actual saved towns always retain their geometry, faction, name and spawner progress.
            if (RejectedDoor(saved) || saved != null && !saved.HasTown && saved.Decision == "chance" && Chance.Value == 100) saved = null;
            return new GenerationInput { Key = key, Seed = seed, Buildings = specs, Saved = saved != null && saved.PathsVersion == 0 ? saved.CopyPaths() : saved,
                Min = System.Math.Min(Min.Value, Max.Value), Max = System.Math.Max(Min.Value, Max.Value),
                ParkingCompanions = GasStationParking.Value, Chance = Chance.Value, EnsureTown = EnsureTown.Value, Inhabited = Inhabited.Value,
                Gap = Gap.Value, MaxRelief = Relief.Value,
                MaxEarthwork = Earthwork.Value, MaxGrade = Grade.Value, Feather = Feather.Value,
                RuinsMin = 3, RuinsMax = 6, HumansMin = 5, HumansMax = 9, LootMin = 12, LootMax = 20,
                 AccessLength = AccessLength.Value };
        }
        /// <summary>Recognizes an unfinished placement decision eligible for another generation attempt.</summary>
        internal static bool RejectedDoor(TownPlan plan)
        { return plan != null && !plan.HasTown && plan.Decision != "chance" && !plan.Decision.StartsWith("skipped:", System.StringComparison.Ordinal); }
    }
}
