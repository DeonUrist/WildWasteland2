using WildWasteland.Modules.Presentation;
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    // All Patrols settings (ex Apocapatrol.Plugin config statics + War Rig's S entries + MerchantSpotter's keys).
    //   F6 (main cfg, Apocasetter): [Patrols] Enabled (Core) + one toggle per feature, then the player-facing values
    //       ([Patrols / Spawning], [Patrols / World traffic], [Patrols / Merchants], [Patrols / Combat], [Patrols / Balance], [Patrols / War Rig]).
    //   F7 (tools.cfg): every former hidden / technical / pose / debug setting in "Patrols / <name>" sections.
    //   [Keys]: Patrol editor, Template recorder, Bus passenger pose editor, Merchant item editor (all None by default).
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<float> PatrolSizePercent;
        internal static ConfigEntry<float> MinPartHealth, MaxPartHealth, MinPartsFill, MaxPartsFill;

        // Condition of a spawned part: min..max, weighted toward two thirds of the way up (triangular distribution)
        /// <summary>Condition of a spawned part: min..max, weighted toward two thirds of the way up (triangular distribution)</summary>
        internal static float RollPartHealth() { return Triangular(MinPartHealth.Value, MaxPartHealth.Value); }
        /// <summary>Rolls part fill for the current patrols settings.</summary>
        internal static float RollPartFill() { return Triangular(MinPartsFill.Value, MaxPartsFill.Value); }

        // random between a and b, weighted toward two thirds of the way up
        /// <summary>random between a and b, weighted toward two thirds of the way up</summary>
        internal static float Triangular(float a, float b)
        {
            float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            if (hi - lo < 0.01f) return lo;
            float mode = lo + (hi - lo) * 2f / 3f;
            float u = UnityEngine.Random.value, f = (mode - lo) / (hi - lo);
            return u < f ? lo + Mathf.Sqrt(u * (hi - lo) * (mode - lo)) : hi - Mathf.Sqrt((1f - u) * (hi - lo) * (hi - mode));
        }

        // [Combat]
        internal static ConfigEntry<bool> RangedCombat;
        internal static ConfigEntry<float> FireArc, MaxAimPitch, AimTurnSpeed, FireBurstSeconds, FireIntervalMin, FireIntervalMax, ShootDistance;
        internal static ConfigEntry<bool> RamDamage, RamDamageInCar;
        internal static ConfigEntry<float> RamDamageMultiplier, RamFullSpeedKmh, RamInCarFactor, RamPushStrength;
        internal static ConfigEntry<string> RamDamageByBody;
        // [Driving]
        internal static ConfigEntry<float> StuckPedalChance, BailChance, StuckBailChance;
        // [Self-destruct]
        internal static ConfigEntry<bool> SelfDestruct;
        internal static ConfigEntry<float> ExplodedLootPercent;
        // [AI]
        internal static ConfigEntry<bool> AiInvertSteering, CustomPaintjobs;
        internal static ConfigEntry<bool> RaidersOverheat;
        internal static ConfigEntry<bool> RaiderMusic, RiderLances, FuryRoad;
        internal static ConfigEntry<float> WitnessMeChance;
        internal static ConfigEntry<float> AiDriveByOffset;
        internal static ConfigEntry<float> AiThrottle, AiLeadTime, AiCommitSeconds, AiSteerRate, AiSteerAngle, AiMaxSteerAtSpeed, AiTurnSafeSpeed,
            AiRamDistance, AiPassWidth, AiRunOutMeters, AiRunOutMaxSeconds, AiReverseSeconds, AiReverseThrottle, AiStuckSeconds,
            AiRecoverWindow, AiMaxRecovers, AiWaitSeconds, AiFeelerRange, AiFeelerSpeedFactor, AiFrontOffset, AiMaxSlopeDeg, AiAvoidGain,
            AiIgnoreMassBelow, AiGiveUpDistance;
        // persistent world traffic
        internal static ConfigEntry<bool> TacticalDriving, FactionBattles, Merchants;
        internal static ConfigEntry<float> TrafficLoadRange, TrafficUnloadRange,
            TrafficDestinationGrace, TrafficPatrolSpeed, TrafficConvoySpeed, ConvoyGuardRange,
            PatrolMemorySeconds;
        internal static ConfigEntry<int> TrafficMaxGroups, TrafficMaxRealCars;
        internal static ConfigEntry<bool> TrafficRoadRouting;
        internal static ConfigEntry<float> TrafficRoadConnector;
        internal static ConfigEntry<bool> CarSpawnLimit, SimpleConvoyBanditsAI, FootPatrols, ReturnToCar;
        // foot patrols / return to car / awareness handover tuning (tools.cfg, Traffic tuning)
        internal static ConfigEntry<int> FootMaxActive;
        internal static ConfigEntry<float> FootSpawnChance, FootCheckSeconds, FootSpeed, FootLoadRange, FootUnloadRange, FootMinSourceDistance, FootMinPoiMetres,
            StrandedIdleSeconds, ReturnTimeoutSeconds, ReturnMaxDistance, DismountMemorySeconds, FootTargetRange;
        internal static ConfigEntry<int> MaxCarsInEvent, MaxCarsInDoubleEvent, MaxPatrols, MaxConvoys;
        internal static ConfigEntry<int> MaxMerchants; internal static ConfigEntry<float> MerchantSpawnChance, MerchantCheckMinutes;
        internal static ConfigEntry<float> CampTrafficRange;
        internal static ConfigEntry<bool> RealPatrolNames, HideRouteType;
        // [Cleanup]
        internal static ConfigEntry<bool> CleanupEnabled;
        internal static ConfigEntry<float> CleanupMinutes, CleanupDistance;
        internal static ConfigEntry<int> CleanupMaxCars;
        // [Convoy spawner]
        internal static ConfigEntry<bool> ConvoyEnabled;
        internal static ConfigEntry<float> ConvoySpawnDistance;

        // fixed values that used to be settings
        internal const float SpawnDistance = 8f;          // m in front of the player (template spawner)
        internal const float TakeoverSeconds = 2f;        // a promoted passenger drives off this long after climbing over
        internal const float EjectDistance = 0.3f;        // the carcass must be this far from the seat before the passenger climbs over
        internal const float EjectSpeed = 4f;             // m/s sideways (+ half of it up) given to the thrown carcass
        internal const float BailDistance = 0.5f;         // an occupant getting out appears this far beside its seat

        // F7 section names (tools.cfg)
        internal const string TDebug = "Debug", TTraffic = "Traffic tuning", TCombat = "Combat tuning", TTurret = "Turret pose", TDriving = "Driving",
            TAI = "AI", TLoot = "Loot", TSpawner = "Spawner", TCleanup = "Cleanup", TAudio = "Audio", TPose = "Pose";

        internal static ModuleConfig Cfg;

        /// <summary>Binds current module configuration and its live change handlers.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            Cfg = cfg;
            BindToggles(cfg);
            BindValues(cfg);
            BindKeys(cfg);
            BindTools(cfg);
            RaidersOverheat.SettingChanged += (s, e) => { if (Running) Crew.ApplyOverheatAll(); };
            SimpleConvoyBanditsAI.SettingChanged += (sender, args) => { if (Running) SimpleBanditBridge.RefreshAll(); };
        }

        // ---------------------------------------------------------------- F6: one toggle per feature, in feature order
        /// <summary>Binds toggles for the current patrols settings.</summary>
        void BindToggles(ModuleConfig cfg)
        {
            ConvoyEnabled = cfg.Player(null, "Automatic spawns", true,
                "Raider patrols and convoys spawn on their own while you play. Off = no automatic spawns at all (the editor's test spawns still work)");
            FactionBattles = cfg.Player(null, "Faction battles", true,
                "World traffic: Scrapyard and Coyote groups that meet fight each other (resolved off-screen when you are far away; wrecks, corpses and a skull on the debug map). Off = no new battles");
            Merchants = cfg.Player(null, "Merchants", true,
                "World traffic: Coyote trader buses (General / Mechanic / Organic shops) travel and park; stop one with Alt+Q or the horn. Off = no automatic merchant routes (same as Max merchants 0)");
            RiderLances = cfg.Player(null, "Human turrets", true,
                "A template's human turrets fight: a Warboy crouching on the roof throws blast lances (and may leap at you: \"Witness me!\"), a gunner " +
                "shoots all around with its own gun. Off = they just ride along");
            RangedCombat = cfg.Player(null, "Ranged combat", true,
                "Ranged humans (Boltjaw/Flexa/Lugnut/Scrud/Sprokka) in a car use their vanilla targeting and ranged attack: the passenger whenever a target " +
                "is in the fire arc, the driver in bursts. Off = everybody just rides along");
            RamDamage = cfg.Player(null, "Ram damage", true,
                "An AI car that hits you (on foot or in your car) hurts you. The game's own bumper damage only works against creatures, not the player");
            RamDamageInCar = cfg.Player(null, "Ram damage in car", false,
                "Ram damage also while you sit in your own car (the game's own CrashDamage still applies by your speed). Off = only on foot");
            RaiderMusic = cfg.Player(null, "Raider music", true,
                "A raider car built from a template that has a cassette in its radio plays it at full volume from the moment it spawns, " +
                "until you switch the radio off. Off = the cassette sits in the radio, silent, as in a parked car");
            CustomPaintjobs = cfg.Player(null, "Custom paintjobs", true,
                "Raider cars wear the paint jobs from the Patrols\\Textures folder (body textures, truck container sides and inside). " +
                "Off = they keep the game's own textures (cars spawned from then on; the textures are not loaded at game start)");
            SelfDestruct = cfg.Player(null, "Always explode cars", false,
                "Override template explodeChance with 100%. Requires a dead driver and all living passengers/turrets to leave. An alive driver bailout never explodes the car; captured cars are protected");
            CleanupEnabled = cfg.Player(null, "Cleanup", true,
                "Remove raider cars you left behind. A car you have ever sat in is never removed (the game itself only deletes cars beyond 5 km)");
            RaidersOverheat = cfg.Player(null, "Raiders overheat", false,
                "Raider engines overheat like yours (smoke, steam, the engine wears down). Off = they never overheat while their crew drives; " +
                "a car you take or an engine you loot heats normally again");
            FuryRoad = cfg.Player(null, "Fury Road", true,
                "Every patrol and convoy group can spawn from the start: their minimum distance travelled and minimum boss kills are ignored. " +
                "Off = groups unlock with distance and bosses as set in the editor");
            SimpleConvoyBanditsAI = cfg.Player(null, "Simple convoy bandits AI", true,
                "NPC module: no crew AI while seated; dismounted bandits fight using baked-map navigation; they hunt you only once they have seen or fought you, and keep track of you for a while after a fight");
            FootPatrols = cfg.Player(null, "Foot patrols", true,
                "World traffic: groups of 2-5 raiders walk between camps over open land (simulated while you are far away). Crew left without a usable car also walk to a nearby camp. Off = no new foot patrols");
            ReturnToCar = cfg.Player(null, "Return to car", true,
                "Patrol, convoy and merchant crews reboard when their attacker dies or moves beyond 50 m from the car, then resume pursuit or their route. A surviving passenger or gunner can drive; fully stuck cars are abandoned");
            TacticalDriving = cfg.Player(null, "Tactical driving", true, "World traffic: varied attack runs and vehicle-checked camp approaches");
        }

        // ---------------------------------------------------------------- F6: player-facing values
        /// <summary>Binds values for the current patrols settings.</summary>
        void BindValues(ModuleConfig cfg)
        {
            const string sp = "Spawning";
            PatrolSizePercent = cfg.Player(sp, "PatrolSizePercent", 100f, "Size of every patrol / convoy that spawns, for weaker PCs: below 100 the group loses cars at random down to this share (rounded down, never below one car; a convoy keeps all of its trucks)", new AcceptableValueRange<float>(10f, 100f));
            MaxPatrols = cfg.Player(sp, "Max patrols", 3, "Maximum tracked patrol groups, including arrived and passing Coyote groups; replaces density", new AcceptableValueRange<int>(0, 64));
            MaxConvoys = cfg.Player(sp, "Max convoys", 1, "Maximum tracked convoy groups, including arrived groups; replaces density", new AcceptableValueRange<int>(0, 64));
            CarSpawnLimit = cfg.Player(sp, "Car spawn limit", true, "Limit physical encounter cars; pending builds count toward the same budget");
            MaxCarsInEvent = cfg.Player(sp, "Max cars in event", 5, "Maximum cars in one patrol or convoy; cargo trucks have priority", new AcceptableValueRange<int>(1, 24));
            MaxCarsInDoubleEvent = cfg.Player(sp, "Max cars in double event", 6, "Combined physical car budget when encounters overlap near the player", new AcceptableValueRange<int>(1, 48));

            const string wt = "World traffic";
            TrafficPatrolSpeed = cfg.Player(wt, "PatrolSpeedKmh", 55f, "Simulated/cruising patrol speed", new AcceptableValueRange<float>(10f, 100f));
            TrafficConvoySpeed = cfg.Player(wt, "ConvoySpeedKmh", 38f, "Simulated/cruising convoy speed", new AcceptableValueRange<float>(10f, 80f));
            CampTrafficRange = cfg.Player(wt, "Camp traffic range", 2500f, "Loaded, uncleared raider camps support base patrols and convoys; incoming Coyote patrols do not require camps", new AcceptableValueRange<float>(500f, 10000f));

            const string mc = "Merchants";
            MaxMerchants = cfg.Player(mc, "Max merchants", 2, "Maximum merchants, with at most one of each shop type. Zero disables automatic merchants.", new AcceptableValueRange<int>(0, 3));
            MerchantSpawnChance = cfg.Player(mc, "Merchant spawn chance", 100f, "Average percentage of Max merchants represented by routes. Whole slots are guaranteed; the remaining fractional slot is rolled at each check. 100 fills the cap; zero creates no merchant routes.", new AcceptableValueRange<float>(0f, 100f));
            MerchantCheckMinutes = cfg.Player(mc, "Merchant check interval (minutes)", 5f, "Check and adjust merchant route population this often, including replacing departed merchants. No immediate replacement when a route ends. Loaded merchants finish their visit before excess routes are removed.", new AcceptableValueRange<float>(1f, 60f));

            const string cb = "Combat";
            ShootDistance = cfg.Player(cb, "ShootDistance", 40f, "Occupants only shoot at a target closer than this, m", new AcceptableValueRange<float>(1f, 200f));
            WitnessMeChance = cfg.Player(cb, "WitnessMeChance", 10f,
                "% chance that an unhurt blast-lance rider screams \"Witness me!\" and leaps at your car when you drive close " +
                "(a wounded rider always does). 100 = every time", new AcceptableValueRange<float>(0f, 100f));

            const string bl = "Balance";
            MinPartHealth = cfg.Player(bl, "MinPartHealth", 80f, "Lowest condition (%) of a spawned raider car's parts that have one (engine, radiator, wheels ...)", new AcceptableValueRange<float>(0f, 100f));
            MaxPartHealth = cfg.Player(bl, "MaxPartHealth", 100f,
                "Highest condition (%) of a spawned raider car's parts; the roll is weighted toward two thirds of the way from Min to Max", new AcceptableValueRange<float>(0f, 100f));
            ExplodedLootPercent = cfg.Player(bl, "ExplodedCarPartsPercent", 20f,
                "% chance for each part of an exploded raider car to keep its condition (MinPartHealth..MaxPartHealth) instead of dropping " +
                "at 0 %. 20 = about one part in five, 0 = all junk, 100 = all usable", new AcceptableValueRange<float>(0f, 100f));

        }

        // ---------------------------------------------------------------- [Keys]
        /// <summary>Binds keys for the current patrols settings.</summary>
        void BindKeys(ModuleConfig cfg)
        {
        }

        // ---------------------------------------------------------------- F7 tools.cfg
        /// <summary>Binds tools for the current patrols settings.</summary>
        void BindTools(ModuleConfig cfg)
        {


            RealPatrolNames = cfg.Tool(TTraffic, "Real patrol names", true, "Use each group's actual name on the game debug map; off labels groups Patrol or Convoy");
            HideRouteType = cfg.Tool(TTraffic, "hide route type", false, "Draw every traffic route and marker gray and label it Distant traffic; overrides Real patrol names");
            TrafficRoadRouting = cfg.Tool(TTraffic, "FollowRoads", true, "Influence sparse patrol waypoints with cached road hints; missing hints use a cached snake route");
            TrafficRoadConnector = cfg.Tool(TTraffic, "MaxRoadConnectorMetres", 350f, "Maximum distance a coarse waypoint is attracted toward a cached road hint, m", new AcceptableValueRange<float>(25f, 1000f));
            TrafficMaxGroups = cfg.Tool(TTraffic, "MaxGroups", 24, "Maximum simulated plus real groups", new AcceptableValueRange<int>(1, 128));
            TrafficMaxRealCars = cfg.Tool(TTraffic, "MaxRealCars", 24, "Budget for nearby physical traffic cars; pending builds reserve slots", new AcceptableValueRange<int>(1, 100));
            TrafficLoadRange = cfg.Tool(TTraffic, "LoadRange", 750f, "Load traffic within this distance of the player, m", new AcceptableValueRange<float>(100f, 2000f));
            TrafficUnloadRange = cfg.Tool(TTraffic, "UnloadRange", 1000f, "Unload only once every group member is this far away; always at least LoadRange + 100 m", new AcceptableValueRange<float>(200f, 4000f));
            TrafficDestinationGrace = cfg.Tool(TTraffic, "DestinationGraceSeconds", 180f, "An arrived group waits this long without a nearby player before removal; proximity resets the clock", new AcceptableValueRange<float>(10f, 3600f));
            ConvoyGuardRange = cfg.Tool(TTraffic, "ConvoyGuardRange", 300f, "Escort drivers attack within this range of the convoy; truck drivers keep travelling, m", new AcceptableValueRange<float>(20f, 1000f));
            PatrolMemorySeconds = cfg.Tool(TTraffic, "PatrolMemorySeconds", 20f, "Search last known player position this long after losing sight/sound, then resume the route", new AcceptableValueRange<float>(0f, 120f));
            FootMaxActive = cfg.Tool(TTraffic, "FootPatrolMaxActive", 2, "Most generated foot patrols at once (crew left without a usable car may add a few more)", new AcceptableValueRange<int>(0, 8));
            FootSpawnChance = cfg.Tool(TTraffic, "FootPatrolSpawnChance", 35f, "Chance (%) per check that a foot patrol leaves a nearby camp", new AcceptableValueRange<float>(0f, 100f));
            FootCheckSeconds = cfg.Tool(TTraffic, "FootPatrolCheckSeconds", 60f, "Seconds between foot patrol spawn checks", new AcceptableValueRange<float>(5f, 1800f));
            FootSpeed = cfg.Tool(TTraffic, "FootPatrolSpeed", 1.3f, "Simulated walking speed of a foot patrol, m/s", new AcceptableValueRange<float>(0.5f, 4f));
            FootLoadRange = cfg.Tool(TTraffic, "FootPatrolLoadRange", 280f, "Foot patrols become real NPCs within this distance of the player, m", new AcceptableValueRange<float>(80f, 600f));
            FootUnloadRange = cfg.Tool(TTraffic, "FootPatrolUnloadRange", 400f, "Real foot patrols become simulated again once every walker is this far away; at least load range + 80 m", new AcceptableValueRange<float>(150f, 1200f));
            FootMinPoiMetres = cfg.Tool(TTraffic, "FootPatrolMinPoiMetres", 1000f, "A generated foot patrol walks out to a point of interest at least this far from its camp (anywhere within one map chunk) and back, m", new AcceptableValueRange<float>(300f, 8000f));
            FootMinSourceDistance = cfg.Tool(TTraffic, "FootPatrolMinSourceDistance", 500f, "A generated foot patrol leaves only from a camp at least this far from you (out of sight; it walks simulated until it comes within FootPatrolLoadRange), m", new AcceptableValueRange<float>(300f, 5000f));
            StrandedIdleSeconds = cfg.Tool(TTraffic, "StrandedIdleSeconds", 30f, "Crew left without a usable car wait this long out of combat before walking to the nearest camp of their faction, s", new AcceptableValueRange<float>(5f, 600f));
            ReturnTimeoutSeconds = cfg.Tool(TTraffic, "ReturnToCarTimeoutSeconds", 120f, "Crew walking back to their car: the car leaves with whoever is aboard after this long (the rest become a foot patrol), s", new AcceptableValueRange<float>(20f, 900f));
            ReturnMaxDistance = cfg.Tool(TTraffic, "ReturnToCarMaxDistance", 300f, "Crew farther than this from their car when the fight is over do not walk back, m", new AcceptableValueRange<float>(30f, 1500f));
            DismountMemorySeconds = cfg.Tool(TTraffic, "DismountMemorySeconds", 90f, "Crew that fought or detected the player from the car still know where he is this long after getting out, s", new AcceptableValueRange<float>(0f, 600f));
            FootTargetRange = cfg.Tool(TTraffic, "FootTargetRange", 350f, "On-foot crew and foot patrols are offered the player as a target only within this distance (they still need to see, hear or remember him), m", new AcceptableValueRange<float>(50f, 1000f));


            // combat tuning (was the hidden [Combat] block)
            FireArc = cfg.Tool(TCombat, "FireArcHalfAngle", 100f, "Occupants may fire this many degrees left or right of the car's forward direction", new AcceptableValueRange<float>(0f, 180f));
            MaxAimPitch = cfg.Tool(TCombat, "MaxAimPitch", 35f, "Maximum upper-body aim angle up or down (degrees)", new AcceptableValueRange<float>(0f, 80f));
            AimTurnSpeed = cfg.Tool(TCombat, "AimTurnSpeed", 180f, "How quickly an occupant turns its upper body toward or away from a target (degrees/second)", new AcceptableValueRange<float>(1f, 720f));
            FireBurstSeconds = cfg.Tool(TCombat, "FireBurstSeconds", 3f, "How long one of the driver's bursts lasts (shooting pose, vanilla Attack inside the fire arc), s", new AcceptableValueRange<float>(0.5f, 30f));
            FireIntervalMin = cfg.Tool(TCombat, "FireIntervalMin", 8f, "Shortest pause between two bursts of the driver, s", new AcceptableValueRange<float>(0f, 120f));
            FireIntervalMax = cfg.Tool(TCombat, "FireIntervalMax", 20f, "Longest pause between two bursts of the driver, s", new AcceptableValueRange<float>(0f, 300f));
            RamDamageMultiplier = cfg.Tool(TCombat, "RamDamageMultiplier", 1f, "Ram damage scale: at 1 a full-speed hit takes 30 health with a small car, 50 with a Junker, 70 with a truck (RamDamageByBody)", new AcceptableValueRange<float>(0f, 3f));
            RamDamageByBody = cfg.Tool(TCombat, "RamDamageByBody", "Junker=50, Rust*=70, Scrapwagon=70, PigPen=50",
                "Full-speed damage per car body, 'Body=damage' pairs; * = prefix. Bodies not listed take " + Ram.DefaultDamage + ". Applied before the multiplier");
            RamFullSpeedKmh = cfg.Tool(TCombat, "RamFullSpeedKmh", 30f, "Impact speed (km/h, relative) for the full damage. At half that speed the hit does half damage; below half it does nothing", new AcceptableValueRange<float>(5f, 200f));
            RamPushStrength = cfg.Tool(TCombat, "RamPushStrength", 1f, "A damaging hit also shoves you (on foot) in the car's direction: 1 = about the impact speed plus a hop; 0 = off", new AcceptableValueRange<float>(0f, 4f));
            RamInCarFactor = cfg.Tool(TCombat, "RamInCarFactor", 1f, "With Ram damage in car: damage factor while you sit in your own car (1 = same as on foot)", new AcceptableValueRange<float>(0f, 1f));
            BindRiders(cfg);


            StuckPedalChance = cfg.Tool(TDriving, "StuckPedalChance", 5f, "% chance that a killed driver's gas pedal stays stuck; otherwise the gas is released and the car rolls to a stop", new AcceptableValueRange<float>(0f, 100f));
            BailChance = cfg.Tool(TDriving, "BailChance", 25f,
                "% chance that a passenger who outlives the driver gets out and fights on foot once the car stands still; otherwise it takes the wheel. " +
                "If the player took the car first it always gets out", new AcceptableValueRange<float>(0f, 100f));
            StuckBailChance = cfg.Tool(TDriving, "StuckBailChance", 100f,
                "% chance that, when the driving AI gives up on a stuck car (after AI MaxRecovers), the whole crew gets out and fights on foot " +
                "instead of the car waiting WaitSeconds and trying again", new AcceptableValueRange<float>(0f, 100f));

            AiThrottle = cfg.Tool(TAI, "Throttle", 1f, "Maximum throttle the driving AI uses (0..1); also the throttle of a stuck pedal", new AcceptableValueRange<float>(0.05f, 1f));
            AiDriveByOffset = cfg.Tool(TAI, "DriveByOffset", 5f, "When the target may not be rammed (the template's ramsTargets), the car aims this far beside it and drives past instead, m", new AcceptableValueRange<float>(1f, 20f));
            AiLeadTime = cfg.Tool(TAI, "LeadTime", 1f, "Aim this many seconds ahead of the target's movement (intercept), s", new AcceptableValueRange<float>(0f, 4f));
            AiCommitSeconds = cfg.Tool(TAI, "CommitSeconds", 0.5f, "The aim point is re-taken only this often, so the car commits to a heading instead of twitching after the target, s", new AcceptableValueRange<float>(0.05f, 3f));
            AiSteerRate = cfg.Tool(TAI, "SteerRate", 1.5f, "How fast the wheel turns: full-lock units per second (1.5 = straight to full lock in 0.67 s). Lower = lazier, wider turns", new AcceptableValueRange<float>(0.2f, 10f));
            AiSteerAngle = cfg.Tool(TAI, "SteerAngle", 30f, "Angle to the aim point at which the driver asks for full lock, degrees (smaller = sharper corrections)", new AcceptableValueRange<float>(5f, 90f));
            AiMaxSteerAtSpeed = cfg.Tool(TAI, "MaxSteerAtSpeed", 0.35f, "Steering limit at 72 km/h and above (0..1); full lock is allowed below 18 km/h, blended in between. Keeps the car on its wheels", new AcceptableValueRange<float>(0.05f, 1f));
            AiTurnSafeSpeed = cfg.Tool(TAI, "TurnSafeSpeed", 18f, "Above this speed (m/s) the driver lifts off and brakes lightly when the target is more than 40 degrees off the nose", new AcceptableValueRange<float>(3f, 40f));
            AiRamDistance = cfg.Tool(TAI, "RamDistance", 15f, "Within this distance of the target (m), obstacle avoidance is switched off: go straight for the ram", new AcceptableValueRange<float>(0f, 50f));
            AiPassWidth = cfg.Tool(TAI, "PassWidth", 12f, "The target counts as passed when it is behind the car and within this many metres of the car's track", new AcceptableValueRange<float>(2f, 40f));
            AiRunOutMeters = cfg.Tool(TAI, "RunOutMeters", 30f, "After passing or ramming the target the car keeps going this far before turning around, m", new AcceptableValueRange<float>(0f, 200f));
            AiRunOutMaxSeconds = cfg.Tool(TAI, "RunOutMaxSeconds", 4f, "... or at most this long, s", new AcceptableValueRange<float>(0.5f, 20f));
            AiReverseSeconds = cfg.Tool(TAI, "ReverseSeconds", 2f, "How long the car reverses after hitting an obstacle or getting stuck, s (grows with repeated attempts)", new AcceptableValueRange<float>(0.5f, 10f));
            AiReverseThrottle = cfg.Tool(TAI, "ReverseThrottle", 0.6f, "Throttle used while reversing", new AcceptableValueRange<float>(0.1f, 1f));
            AiStuckSeconds = cfg.Tool(TAI, "StuckSeconds", 2f, "Not moving for this long while trying to drive forward = stuck, reverse out", new AcceptableValueRange<float>(0.5f, 10f));
            AiRecoverWindow = cfg.Tool(TAI, "RecoverWindow", 30f, "Recoveries closer together than this count as repeated attempts, s", new AcceptableValueRange<float>(1f, 60f));
            AiMaxRecovers = cfg.Tool(TAI, "MaxRecovers", 2f, "After this many repeated recoveries the car gives up: the crew bails (Driving StuckBailChance) or the car waits WaitSeconds", new AcceptableValueRange<float>(1f, 20f));
            AiWaitSeconds = cfg.Tool(TAI, "WaitSeconds", 4f, "How long a car that gave up sits still before trying again, s", new AcceptableValueRange<float>(1f, 60f));
            AiFeelerRange = cfg.Tool(TAI, "FeelerRange", 10f, "Base length of the centre obstacle feeler ray, m (side rays are shorter)", new AcceptableValueRange<float>(2f, 40f));
            AiFeelerSpeedFactor = cfg.Tool(TAI, "FeelerSpeedFactor", 0.6f, "Extra feeler length per m/s of speed", new AcceptableValueRange<float>(0f, 2f));
            AiFrontOffset = cfg.Tool(TAI, "FrontOffset", 2f, "Feelers start this far in front of the car's centre of mass, m (should be just outside the bumper)", new AcceptableValueRange<float>(0f, 5f));
            AiMaxSlopeDeg = cfg.Tool(TAI, "MaxSlopeDeg", 35f, "Surfaces flatter than this are driven over, steeper ones are obstacles (rocks, walls), degrees", new AcceptableValueRange<float>(10f, 80f));
            AiAvoidGain = cfg.Tool(TAI, "AvoidGain", 1.2f, "How hard the feelers steer away from obstacles", new AcceptableValueRange<float>(0f, 4f));
            AiIgnoreMassBelow = cfg.Tool(TAI, "IgnoreMassBelow", 40f, "Loose physics objects lighter than this (kg) are not obstacles; the car drives through them", new AcceptableValueRange<float>(0f, 1000f));
            AiGiveUpDistance = cfg.Tool(TAI, "GiveUpDistance", 700f, "Beyond this distance from the player the car stops chasing and coasts until the player comes closer, m", new AcceptableValueRange<float>(20f, 2000f));
            AiInvertSteering = cfg.Tool(TAI, "InvertSteering", false, "Flip the steering sign if the car turns away from the target instead of toward it");

            MinPartsFill = cfg.Tool(TLoot, "MinPartsFill", 15f, "Lowest fill (% of capacity) of a spawned car's tank (gas/diesel), engine oil and radiator water", new AcceptableValueRange<float>(0f, 100f));
            MaxPartsFill = cfg.Tool(TLoot, "MaxPartsFill", 60f, "Highest fill (%) of tank, oil and water; each rolled separately, weighted toward two thirds of the way from Min to Max", new AcceptableValueRange<float>(0f, 100f));

            ConvoySpawnDistance = cfg.Tool(TSpawner, "SpawnDistance", 350f, "How far away a spawn appears (m): ahead of your car, up to 45 degrees left or right; behind you when on foot", new AcceptableValueRange<float>(50f, 1000f));

            CleanupMinutes = cfg.Tool(TCleanup, "RemoveAfterMinutes", 10f, "A raider car farther than MinDistance for this long is removed with its crew and cargo (0 = never by time)", new AcceptableValueRange<float>(0f, 600f));
            CleanupMaxCars = cfg.Tool(TCleanup, "MaxCars", 30, "With more raider cars than this in the world, the farthest ones beyond MinDistance are removed first (0 = no limit)", new AcceptableValueRange<int>(0, 200));
            CleanupDistance = cfg.Tool(TCleanup, "MinDistance", 800f, "Cars closer than this are never removed (m)", new AcceptableValueRange<float>(100f, 5000f));
        }

        // ---------------------------------------------------------------- per-target parts (Wave A split; implemented in the target module folders)
        /// <summary>Binds the Turret pose entries (crouched gunner pose and the Warboy lance prop). Implemented in Presentation (_fromPatrols).</summary>
    }
}
