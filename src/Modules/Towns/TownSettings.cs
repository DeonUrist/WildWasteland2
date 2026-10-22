using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal sealed class TownSettings
    {
        internal ConfigEntry<int> Min, Max, Chance, Inhabited;
        internal ConfigEntry<float> Width, Setback, Jitter, Relief, Earthwork, Grade, Feather;
        internal ConfigEntry<string> Prefabs, RoadLayer;
        internal ConfigEntry<float> EntrySetback, Gap;
        internal ConfigEntry<int> PopulationMinPercent, PopulationMaxPercent, LootMinPercent, LootMaxPercent;
        internal ConfigEntry<float> PathWidth, AccessWidth, AccessLength;
        internal ConfigEntry<bool> EnsureTown;
        internal ConfigEntry<bool> PoiNearSpawn;
        internal ConfigEntry<bool> Walls;
        internal ConfigEntry<float> LoadingBudgetMs;
        internal ConfigEntry<bool> TestGasStation, GasStationParking, TestCamp10;
        internal ConfigEntry<int> DesertGasStationChance;
        internal ConfigEntry<float> GasStationScale;
        internal ConfigEntry<UnityEngine.InputSystem.Key> PoiDresserKey;
        internal ConfigFile File;
        // This alternate binder makes the standalone DLL independent of internal ModuleConfig construction.
        internal void Bind(ConfigFile file)
        {
            File = file;
            PoiDresserKey = file.Bind("Towns / Editors", "POI dresser", UnityEngine.InputSystem.Key.None, "Open the static prop dresser for the nearest custom POI within 200 metres. Requires Enable Debug and Developer mode. Layouts are saved with Towns assets and reused by every instance of that POI type.");
            TestCamp10 = file.Bind("Towns / Debug", "Test Camp_10 spawn", false, "Place the merchant camp (Camp_10, stripped of its spawners and logic) on a level pad near the starting camp, so its NPC navigation can be baked on flat ground (towns need that bake to use it as their merchant lot). Test only, unsaved. Reload the world after enabling or disabling.");
            TestGasStation = file.Bind("Towns / Debug", "Test gas station", false, "Place the supplied gas station as the closest available POI outside the starting camp, on a clear level pad, with three melee Scrapyard raiders after its ground-floor navigation is baked. Test only; excluded from normal town lots and saves. Reload the world after enabling or disabling.");
            GasStationScale = file.Bind("Towns / Debug", "Gas station scale", 1f, new ConfigDescription("Uniform extra scale for the render model and separate gasstation_collider.glb. Use 1 with the supplied models (their authored 1.5 scale gives a 32.19 x 34.28 metre footprint). Reload after changing: the terrain pad and navigation cache must match the scale.", new AcceptableValueRange<float>(.1f, 5f)));
            DesertGasStationChance = file.Bind("Towns / World", "Desert gas station chance (%)", 8, new ConfigDescription("Chance per generated 5 km block of a roadside gas station POI (native loot inside, Scrapyard or animal spawners). Changes affect new/undecided blocks only after a world reload.", new AcceptableValueRange<int>(0, 100)));
            GasStationParking = file.Bind("Towns / World", "Gas station parking lots", true, "Place a gas station beside the open ground parking lot outside the town walls, with a two metre gap. Reload after changing.");
            Walls = file.Bind("Towns / Debug", "Town wall prototype", true, "Build native metal perimeter walls and four open cardinal gates around owned Coyote/Scrapyard towns. Parking remains just outside the east gate. Restart or reload after changing; buildings retain their saved positions.");
            LoadingBudgetMs = file.Bind("Towns / Debug", "Loading build budget (ms)", 12f, new ConfigDescription("Extra town construction time per frame while a world is loading or generating (nobody is playing yet). Normal play keeps the shared scheduler budget and one prefab per frame.", new AcceptableValueRange<float>(0f, 40f)));
            PoiNearSpawn = file.Bind("Towns / World", "New POI near spawn", false, "Allow newly generated Towns and War Rig in the first 5 km chunk around the starting camp. Off excludes new locations in chunk 0/0; existing saved towns remain. Changes affect new/undecided locations.");
            Min = Int(file, "Minimum buildings", 5, 3, 32, "Minimum building lots per town. The larger of minimum/maximum is used as the maximum.");
            Max = Int(file, "Maximum buildings", 8, 3, 32, "Maximum lots per town, inclusive: one open 24 x 40 m parking slot, remaining slots use native test buildings.");
            Chance = Int(file, "Town chance (%)", 20, 0, 100, "Independent town chance per generated 5 km block. WORLD > POI. Selected blocks retry layouts/sites; Ensure selected towns allows the flattest available site when terrain targets cannot be met.");
            EnsureTown = file.Bind("Towns / Generation", "Ensure selected towns", true, "After a successful chance roll, use the flattest available unoccupied site if the relief/cut-fill/grade targets cannot be met. Keep building counts, global blockers and connection to a real road. Unavailable sites remain pending with an explanation.");
            Inhabited = Int(file, "Inhabited towns (%)", 50, 0, 100, "Fraction of human Scrapyard/Coyotes faction bases rather than animal/mutant ruins. Test Building_N models retain their native appearance; roles do not yet create service merchants.");
            Width = Float(file, "Street width", 10, 4, 24, "Street width in metres, using the current terrain road texture.");
            Setback = Float(file, "Building setback", 4, 2, 20, "Retained legacy envelope setback key. New layout v2 uses Entrance setback instead; saved layouts retain their exact positions.");
            EntrySetback = Float(file, "Entrance setback", 1, .5f, 4, "Clear distance from the road edge to the measured entrance, before small irregular variation. Replaces envelope-based setback for layout v2; old layouts keep their positions.");
            Gap = Float(file, "Gap between buildings", 2, .5f, 6, "Typical longitudinal gap between each building's actual footprint; bake margins may overlap and share global blockers.");
            Jitter = Float(file, "Layout irregularity", 6, 0, 16, "Unequal frontage, setbacks and gentle street bends. Measured entrances continue to face across the street.");
            Relief = Float(file, "Maximum original relief", 6, .5f, 20, "Preferred terrain height range over the pad and feather. A strict limit with Ensure selected towns off; otherwise the flattest available fallback may exceed it.");
            Earthwork = Float(file, "Maximum cut or fill", 3, .25f, 10, "Preferred terrain-to-pad elevation difference. Ensure selected towns permits more for the flattest available fallback site.");
            Grade = Float(file, "Maximum original grade", .15f, .01f, .4f, "Preferred original neighbour rise/run before flattening. .15 means 15 percent. A strict limit only with Ensure selected towns off.");
            Feather = Float(file, "Pad transition width", 35, 10, 100, "Width in metres of the smooth transition from the level town to the unchanged landscape.");
            Prefabs = file.Bind("Towns / Assets", "Test building prefabs", "", "Comma-separated existing Building_N names; blank uses all Building_N prefabs in the current MapMagic graph. Restart after changing. Native prefab geometry, scales and bake identities are preserved.");
            RoadLayer = file.Bind("Towns / Assets", "Road terrain layer", "", "Optional exact TerrainLayer name. Blank follows the graph's roadStroke texture input. Missing/ambiguous road artwork rejects town generation instead of inventing a texture. Restart after changing.");
            PopulationMinPercent = Percent(file, "Minimum population (%)", 10, "Minimum percentage of the whole town's original native enemy total. One town roll is distributed evenly across buildings and groups. 100 restores native amounts, 0 pauses pending spawns.");
            PopulationMaxPercent = Percent(file, "Maximum population (%)", 20, "Maximum percentage of native enemy spawn amounts per town, inclusive. Faction restrictions still apply. 500 is five times the native amounts.");
            LootMinPercent = Percent(file, "Minimum loot (%)", 5, "Minimum percentage of the whole town's native common-loot total. One town roll is distributed evenly across buildings and groups. 100 restores native item amounts, 0 pauses pending loot.");
            LootMaxPercent = Percent(file, "Maximum loot (%)", 10, "Maximum percentage of native common-loot amounts per town, inclusive. 500 is five times the native item amounts. Completed spawners never refill.");
            PathWidth = Float(file, "Entrance path width", 3, 1.5f, 5, "Terrain-blended path width between entrances. Buildings keep the existing placement spacing.");
            AccessWidth = Float(file, "Access road width", 5, 3, 9, "Width of the terrain-blended access road to the actual generated world road.");
            AccessLength = Float(file, "Maximum access road length", 900, 100, 1800, "Maximum straight-line road search distance in metres. New towns require a nearby native road and a route with acceptable terrain grade.");
        }
        private static ConfigEntry<int> Percent(ConfigFile file, string key, int value, string description)
        { return file.Bind("Towns / Debug", key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, 500))); }
        internal void Bind(ModuleConfig cfg)
        {
            Legacy.Get("WildWasteland/settings.cfg", "Towns / Generation", "Town chance (%)");
            Bind(cfg.Main);
            // First merged bind only: preserve the add-on's customized values, including its 100% chance.
            foreach (var key in File.Keys)
            {
                if (!key.Section.StartsWith("Towns /", System.StringComparison.Ordinal)) continue;
                string current = Legacy.Get("WildWasteland/settings.cfg", key.Section, key.Key);
                string old = Legacy.Get("com.denis.apocalypter.wildwasteland.towns.cfg", key.Section, key.Key);
                if (current == null && old != null) File[key].SetSerializedValue(old);
            }
        }
        private static ConfigEntry<int> Int(ConfigFile f, string k, int d, int min, int max, string desc)
        { return f.Bind("Towns / Generation", k, d, new ConfigDescription(desc, new AcceptableValueRange<int>(min, max))); }
        private static ConfigEntry<float> Float(ConfigFile f, string k, float d, float min, float max, string desc)
        { return f.Bind("Towns / Generation", k, d, new ConfigDescription(desc, new AcceptableValueRange<float>(min, max))); }
        internal GenerationInput Snapshot(string key, int seed, BuildingSpec[] specs, TownPlan saved)
        {
            // Retry empty placement failures. At 100%, old empty chance decisions are also eligible.
            // Actual saved towns always retain their geometry, faction, name and spawner progress.
            if (RejectedDoor(saved) || saved != null && !saved.HasTown && saved.Decision == "chance" && Chance.Value == 100) saved = null;
            return new GenerationInput { Key = key, Seed = seed, Buildings = specs, Saved = saved != null && saved.PathsVersion == 0 ? saved.CopyPaths() : saved,
                Min = System.Math.Min(Min.Value, Max.Value), Max = System.Math.Max(Min.Value, Max.Value),
                Chance = Chance.Value, EnsureTown = EnsureTown.Value, Inhabited = Inhabited.Value, StreetWidth = Width.Value,
                Setback = EntrySetback.Value, Gap = Gap.Value, Jitter = Jitter.Value, MaxRelief = Relief.Value,
                MaxEarthwork = Earthwork.Value, MaxGrade = Grade.Value, Feather = Feather.Value,
                RuinsMin = 3, RuinsMax = 6, HumansMin = 5, HumansMax = 9, LootMin = 12, LootMax = 20,
                PathWidth = PathWidth.Value, AccessWidth = AccessWidth.Value, AccessLength = AccessLength.Value };
        }
        internal static bool RejectedDoor(TownPlan plan)
        { return plan != null && !plan.HasTown && plan.Decision != "chance" && !plan.Decision.StartsWith("skipped:", System.StringComparison.Ordinal); }
    }
}
