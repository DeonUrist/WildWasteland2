using System;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // Config of the NPC module (ex NPCAI.Plugin's statics). Every ConfigEntry keeps the old static name so the ported code reads
    // NpcModule.X where it read Plugin.X. F6 (main cfg): the module switch (Core), one toggle per feature and the player-facing settings.
    // F7 (tools.cfg, sections "NPC / <old section>"): every hidden / tuning entry of NPCAI 1.9.15 (its never-saved hidden-settings file).
    public sealed partial class NpcModule
    {
        internal const string LegacyCfg = "com.denis.apocalypter.npcai.cfg";
        internal const string LegacyHidden = "NPCAI/hidden-settings.not-saved";   // relative to BepInEx\config (Legacy.Get)

        internal static ConfigEntry<bool> ApocaplayerClips, VerboseLog, HighlightUnbakedMapPois;
        internal static ConfigEntry<bool> AimEnabled;
        internal static ConfigEntry<float> AimTimeScale, FacingTolerance, EngagePercent, ReloadSeconds, SingleShotReload, ReloadVolume, ReloadSoundRange, RoundSeconds;
        internal static ConfigEntry<bool> Magazines; internal static ConfigEntry<string> MagazineTable;
        internal static ConfigEntry<bool> BrainEnabled, DropCheck, BrainLog, AimPose, ScaleWithActors;
        internal static ConfigEntry<float> TurnRate, CrouchChance, ReactionTime, FeelerLength, MeleeFeelerLength, FeelerAngle, AdvanceChance, AdvanceMin, AdvanceMax, StuckBackupSeconds, StuckMemorySeconds, MaxDistance;
        internal static ConfigEntry<int> FeelerCount, StuckGiveUpCount, Guesses;
        internal static ConfigEntry<bool> SensesEnabled, SensesLog, ShowGhosts, BailOutAware;
        internal static ConfigEntry<float> SightCone, SightRange, DarkSightRange, FootstepRadius, NoticeSeconds, LoseSeconds, SearchSeconds, GhostTimeout, LookInterval, ArriveDistance, AllClearRange, StormSight, StormHearing, StormRadius, ShotRangePistol, ShotRangeSmg, ShotRangeRifle, ShotRangeSniper, ShotRangeShotgun, ShotRangeCrossbow, TauntRange, EngineMinRange, EngineMaxRange, EngineMinHp, EngineMaxHp, EngineIdleFactor, ThrowRange, BailOutAwareRange, ExplosionRange, BlastRange, PlayerShoutRange, ShoutCooldown, ShoutVolume;
        internal static ConfigEntry<Key> ShoutKey, ShoutModifier;
        internal static ConfigEntry<string> ShoutBlocksButtons;
        internal static ConfigEntry<string> NpcShotRanges, HumanFactions, BlastPrefabs, Trackers, NightHours;
        internal static ConfigEntry<float> StrafeAngle, NoticeFar, FlankDistance, FlankWidth, GuessRadius, TauntMin, TauntMax, GunshotNearRange, CoverBelow, CoverRange, CoverMin, CoverMax;
        internal static ConfigEntry<int> GunshotResponders;
        internal static ConfigEntry<bool> NavEnabled, NavLog, ShowNav, NavDump, IdleEnabled, IdleCoyotes, FriendsPassThrough;
        internal static ConfigEntry<float> MoveFullSpeedAngle, MoveSlowestAngle, MoveSlowestSpeed, BlockedRatio, BlockedSeconds, BlockedMemory;
        internal static ConfigEntry<float> IdleReturnDelay, IdleRetrySeconds, IdleWalkRadius;
        internal static ConfigEntry<int> IdleReturnTries;
        internal static ConfigEntry<float> FoughtMemorySeconds, TravelRunBeyond;      // (crew AI) the fought-player memory; Travel orders run beyond this distance
                internal static ConfigEntry<float> NavBakeRange, NavCellSize, NavMargin, NavMaxStep, NavBakeBudgetMs, NavFieldSeconds;
        // (1.3.0) vision arcs by species group, small-scorpion burrowing
        internal static ConfigEntry<float> SightConeInsects, SightConeBeasts, SightConeBrutes, SightConeBosses, SightConeBurrowers;
        internal static ConfigEntry<string> ConeInsects, ConeBeasts, ConeBrutes, ConeBurrowers, BurrowPrefabs, BurrowSandKeywords;
        internal static ConfigEntry<bool> BurrowEnabled, BurrowTerrainIsSand, BurrowUseLayers, BurrowLog;
        internal static ConfigEntry<string> HuntPrefabs, PouncePrefabs;
        internal static ConfigEntry<int> NestMinWebs, NestMaxWebs, NestGroundMin, NestGroundMax, NestSpots;
        internal static ConfigEntry<bool> NestsEnabled, NestLog, AmbushEnabled;
        internal static ConfigEntry<string> AmbushPrefabs, DogPrefabs;
        internal static ConfigEntry<bool> DogBiteEnabled, DogLog;
        internal static ConfigEntry<float> AmbushMin, AmbushMax;
        internal static ConfigEntry<string> NestPrefabs, NestSitters;
        internal static ConfigEntry<float> NestBuildRange, NestFreeRange, NestPoiRadius, NestMaxRadius, NestFrameBudgetMs, NestAlarmSpeed, NestAlarmSeconds, NestAlarmCooldown, NestSitterReach;
        internal static ConfigEntry<bool> PounceEnabled, PounceLog;
        internal static ConfigEntry<float> PounceZigMin, PounceZigMax, PounceLeapMin, PounceLeapMax, PounceZigAngle, PounceZigSeconds, PounceZigSpeed, PounceLeapSpeed, PouncePitch, PounceFleeMin, PounceFleeMax, PounceCooldownMin, PounceCooldownMax;
        internal static ConfigEntry<float> HuntRange, HuntMaxDistance, HuntBehind, HuntSpeed, HuntCooldown, HuntDiveSeconds, HuntRiseSeconds, HuntFallbackMin, HuntFallbackMax, HuntTravelFactor, HuntBurrowFactor, HuntFxScale, HuntFxVolume, HuntFxPitch;
        internal static ConfigEntry<float> BurrowMin, BurrowMax, BurrowAmbushMin, BurrowAmbushMax, BurrowSeenFrom, BurrowSeconds, RiseSeconds, BurrowDepth, BurrowFxScale, BurrowFxVolume, BurrowFxPitch, BurrowFxRange;
        // new in WW (F6 toggles for features NPCAI ran unconditionally)
        internal static ConfigEntry<bool> DogCompanionCompatEnabled, StormsEnabled;
        internal static ConfigEntry<bool> DogAnimationsEnabled, DogsLieDown, DogJumpAttack;
        internal static ConfigEntry<float> DogIdlePoseMin, DogIdlePoseMax, DogLieFrontLift;

        private static ModuleConfig _cfg;
        private static ConfigEntry<T> H<T>(string section, string key, T value, ConfigDescription description)
        {
            var e = _cfg.Tools.Bind(_cfg.Section(section), key, value, description);
            _cfg.Migrate(e, LegacyHidden, section, key);
            return e;
        }
        private static ConfigEntry<T> H<T>(string section, string key, T value, string description) { return H(section, key, value, new ConfigDescription(description)); }
        private static ConfigEntry<T> V<T>(string sub, string key, T value, ConfigDescription description, string oldSection, string oldKey)
        {
            var e = _cfg.Main.Bind(_cfg.Section(sub), key, value, description);
            _cfg.Migrate(e, LegacyCfg, oldSection, oldKey);
            return e;
        }
        private static ConfigEntry<T> V<T>(string sub, string key, T value, string description, string oldSection, string oldKey) { return V(sub, key, value, new ConfigDescription(description), oldSection, oldKey); }
        // an F6 toggle that was a hidden [Behaviour] switch in NPCAI (migrated from its hidden-settings file when that existed)
        private static ConfigEntry<bool> B(string key, bool value, string description)
        {
            var e = _cfg.Main.Bind(_cfg.Section(null), key, value, description);
            _cfg.Migrate(e, LegacyHidden, "Behaviour", key);
            return e;
        }

        private static void Renamed(ConfigEntryBase e, string oldRaw)
        {
            if (e == null || oldRaw == null) return;
            try { e.SetSerializedValue(oldRaw); Core.Log.Info("[NPC] " + e.Definition.Key + " = " + oldRaw + " (from the old key name)"); }
            catch (Exception ex) { Core.Log.Warn("[NPC] could not carry over " + e.Definition.Key + ": " + ex.Message); }
        }

        public override void BindConfig(ModuleConfig cfg)
        {
            _cfg = cfg;
            BindWomen(cfg);
            // ---------------- F6: one toggle per feature, in feature order (section "NPC", after Core's Enabled) ----------------
            SensesEnabled = V(null, "EnableNpcDetection", true,
                "NPCs see (field of view, light, flashlight), hear gunshots, shouts, engines and explosions, remember where you were and search. Off = the game's own sensors.", "General", "EnableNpcDetection");
            BrainEnabled = V(null, "EnableNpcPathfinding", true,
                "NPCs steer around obstacles, use the maps of camps, caves and buildings, take shooting positions, kneel and aim. Off = the game's own movement.", "General", "EnableNpcPathfinding");
            AimEnabled = V(null, "NpcAim", true, "NPC gunmen pace their bursts: they hold fire beyond their gun's reach, take longer to aim at a distance and lead moving targets.", "Gunplay", "NpcAim");
            IdleEnabled = V(null, "EnableIdleBehavior", true, "Camp raiders who lose you go back to their spawn spot, and walk a short round in their camp while nothing happens.", "Pathfinding", "EnableIdleBehavior");
            IdleCoyotes = V(null, "CoyotesIdle", true, "The idle behaviour also for the peaceful Coyote towns.", "Pathfinding", "CoyotesIdle");
            FriendsPassThrough = V(null, "FriendsPassThrough", true, "NPCs of the same faction walk through each other while they fight, search or walk home - no bumping, no blocking a passage. Solid again once they are idle.", "Pathfinding", "FriendsPassThrough");
            StormsEnabled = _cfg.Main.Bind(_cfg.Section(null), "Storms", true, "Sandstorms (and ApocaDustStorm's dust storms) dim NPC sight and hearing; tornado funnels make NPCs forget what they were doing. Off = storms do not affect the senses.");
            BurrowEnabled = B("Burrow", true, "Scorpions burrow into sand when nothing alerts them (and come up every 10-30 s), eyes above the surface. Big scorpions also hunt underground: they travel under the sand and come up behind their enemy.");
            AmbushEnabled = B("Ambush", true, "Scorpions wait until a target comes within 3-5 m. Spiders wait in darkness, but react to shouts, gunfire, light and a nest's web being touched.");
            PounceEnabled = B("Pounce", true, "Small spiders zigzag in when they get close to you over open ground, leap at you from 5-6 m, bite and run off before trying again.");
            NestsEnabled = B("SpiderNests", true, "Caves, wrecks and camps with spiders become spider nests: webs across their openings and on the ground, big spiders sitting in the webs. Touch a web and the whole nest comes at you.");
            DogBiteEnabled = B("DogBite", true, "Quadrupeds commit to dodgeable running bites and use faster bites at close range. Without the animation bundle, the original procedural bite is used.");
            DogAnimationsEnabled = B("DogAnimations", true, "Retargeted animations on hounds, Grimhound and nightwalkers. Requires NPC/Models/npcai_dogs.bundle; otherwise the existing procedural bite remains active.");
            DogsLieDown = B("DogsLieDown", true, "Non-boss quadrupeds alternate standing and lying idle, spending a random 10-25 seconds in each pose. Threats interrupt rest through get-up.");
            DogJumpAttack = B("DogJumpAttack", true, "Optional short, collision-checked jumping bite. Native damage is applied only if the target remains within reach at contact.");
            DogCompanionCompatEnabled = _cfg.Main.Bind(_cfg.Section(null), "DogCompanionCompat", true, "With FlyingKlick's Dog Companion installed: the companion dog uses the same dog animations (locomotion, bites, sitting in vehicles, sneaking). Off = Dog Companion's own behaviour.");
            // ---------------- F6: player-facing settings ----------------
            SightCone = V("Detection", "SightCone", 100f, new ConfigDescription("Width of an NPC's field of view, degrees.", new AcceptableValueRange<float>(10f, 360f)), "Detection", "SightCone");
            SightRange = V("Detection", "SightRange", 100f, new ConfigDescription("How far an NPC sees in daylight, m (and at any light if your flashlight is on).", new AcceptableValueRange<float>(5f, 300f)), "Detection", "SightRange");
            DarkSightRange = V("Detection", "DarkSightRange", 5f, new ConfigDescription("How far an NPC sees in full darkness, m.", new AcceptableValueRange<float>(0f, 100f)), "Detection", "DarkSightRange");
            FootstepRadius = V("Detection", "FootstepRadius", 5f, new ConfigDescription("An NPC hears you walking this close, m: with a clear line between you it notices you whatever way it faces and however dark it is (standing still makes no footsteps). 0 = off.", new AcceptableValueRange<float>(0f, 30f)), "Detection", "FootstepRadius");
            ScaleWithActors = V("Pathfinding", "ScaleWithActors", false, "With many NPCs around, each one thinks less often (saves CPU in big fights).", "Pathfinding", "ScaleWithActors");
            // ---------------- [Keys] ----------------
            // renamed from "NPC shout" / "NPC shout modifier": read the old values before the new keys are bound (and saved)
            const string sf = "WildWasteland/settings.cfg";
            string oldShout = Legacy.Get(sf, "Keys", "NPC shout"), oldModifier = Legacy.Get(sf, "Keys", "NPC shout modifier");
            bool freshShout = Legacy.Get(sf, "Keys", "Shout") == null, freshModifier = Legacy.Get(sf, "Keys", "Shout modifier") == null;
            ShoutKey = cfg.Hotkey("Shout", Key.Q,
                "Your shout (with 'Shout modifier' held): you yell like a raider and every NPC hostile to you within 25 m comes to check. None = off. (Alt+Q is shared on purpose with the Patrols merchant stop signal.)");
            cfg.Migrate(ShoutKey, LegacyCfg, "Detection", "PlayerShoutKey");
            if (freshShout) Renamed(ShoutKey, oldShout);
            ShoutModifier = cfg.Hotkey("Shout modifier", Key.LeftAlt,
                "Hold this and press 'Shout' to shout (either Alt works when this is an Alt key); the game's own Q actions are ignored while it is held. None = the key alone.");
            cfg.Migrate(ShoutModifier, LegacyCfg, "Detection", "PlayerShoutKeyModifier");
            if (freshModifier) Renamed(ShoutModifier, oldModifier);
            // ---------------- F7 (tools.cfg): debug ----------------
            VerboseLog = H("Debug", "VerboseLog", false, "Detailed NPC logs (hits, detection, movement, maps) - also written when the global [Debug] VerboseLog is off.");
            _cfg.Migrate(VerboseLog, LegacyCfg, "Debug", "VerboseLog");
            NavDump = H("Debug", "NavDump", false, "Map pictures (BMP) of every camp map made, and of NPCs that find no route, in config/WildWasteland/NPC/NavDump (0.5-4 MB each; for troubleshooting). With VerboseLog also a per-second Trace line for every moving NPC and a trace picture of its trail when a chase ends or it rests (cyan = the map's route, white = walked on the map, orange = walked without the map, red = backing up / resting).");
            _cfg.Migrate(NavDump, LegacyCfg, "Debug", "NavDump");
            ShowNav = H("Debug", "ShowNavigation", false, "Draw the detection ghosts, NPC states and the structure maps' waypoints in the world.");
            _cfg.Migrate(ShowNav, LegacyCfg, "Debug", "ShowNavigation");
            HighlightUnbakedMapPois = H("Debug", "HighlightUnbakedMapPois", false,
                "Bake-collection aid: map POIs without a completed saved bake are light purple regardless of allegiance. Turn off to restore normal map colors immediately. Does not reveal fog or change compass colors.");
            // ---------------- F7 (tools.cfg): the dog animation tuning (ex DogAnimations.Initialize) ----------------
            DogIdlePoseMin = H("Behaviour", "DogIdlePoseMinSeconds", 10f, new ConfigDescription("Idle quadrupeds stand or lie at least this long before switching, s.", new AcceptableValueRange<float>(2f, 300f)));
            DogIdlePoseMax = H("Behaviour", "DogIdlePoseMaxSeconds", 25f, new ConfigDescription("... and at the most, s.", new AcceptableValueRange<float>(2f, 300f)));
            DogLieFrontLift = H("Behaviour", "DogLieFrontLift", 0f, new ConfigDescription("Lying quadrupeds: the front half (chest, front legs, head) is tilted up at the waist until the shoulders stand as high over the hips as they do standing. This adds to (or, negative, takes from) that height, m.", new AcceptableValueRange<float>(-0.15f, 0.3f)));
            // ---------------- F7 (tools.cfg): every other hidden NPCAI setting, same section / key / default ----------------
            SightConeInsects = H("Detection", "SightConeInsects", 210f, new ConfigDescription("Field of view of spiders, scorpions, wasps, arachnids, degrees.", new AcceptableValueRange<float>(10f, 360f)));
            SightConeBeasts = H("Detection", "SightConeBeasts", 190f, new ConfigDescription("Field of view of rats, hounds, skinwals, nightwalkers, degrees.", new AcceptableValueRange<float>(10f, 360f)));
            SightConeBrutes = H("Detection", "SightConeBrutes", 210f, new ConfigDescription("Field of view of professors, teachers, juggernauts, degrees.", new AcceptableValueRange<float>(10f, 360f)));
            SightConeBosses = H("Detection", "SightConeBosses", 300f, new ConfigDescription("Field of view of bosses (creatures with the BossUI FSM), degrees.", new AcceptableValueRange<float>(10f, 360f)));
            SightConeBurrowers = H("Detection", "SightConeBurrowers", 360f, new ConfigDescription("Field of view of the sand worms, degrees.", new AcceptableValueRange<float>(10f, 360f)));
            ConeInsects = H("Detection", "ConeInsects", "Spider,Scorpion,Wasp,Arachnid", "Prefab name parts of the insect group.");
            ConeBeasts = H("Detection", "ConeBeasts", "Rat,Hound,Skinwal,Nightwalker", "Prefab name parts of the beast group.");
            ConeBrutes = H("Detection", "ConeBrutes", "Professor,Teacher,Juggernaut", "Prefab name parts of the brute group.");
            ConeBurrowers = H("Detection", "ConeBurrowers", "Burrower", "Prefab name parts of the burrower group.");
            BurrowPrefabs = H("Burrow", "Prefabs", "Scorpion_Small", "Creatures that burrow (exact prefab names).");
            BurrowMin = H("Burrow", "MinSeconds", 10f, new ConfigDescription("Shortest time between burrowing and coming up, s.", new AcceptableValueRange<float>(1f, 600f)));
            BurrowMax = H("Burrow", "MaxSeconds", 30f, new ConfigDescription("Longest time between burrowing and coming up, s.", new AcceptableValueRange<float>(1f, 600f)));
            BurrowAmbushMin = H("Burrow", "AmbushMin", 3f, new ConfigDescription("A burrowed creature lets you come this close (m) at the least before it comes up and attacks; chosen at random per burrowing.", new AcceptableValueRange<float>(0.5f, 30f)));
            BurrowAmbushMax = H("Burrow", "AmbushMax", 5f, new ConfigDescription("... and at the most, m.", new AcceptableValueRange<float>(0.5f, 30f)));
            BurrowSeenFrom = H("Burrow", "SeenFrom", 5f, new ConfigDescription("Other NPCs notice a burrowed creature only from this close, m (the player sees its tail regardless).", new AcceptableValueRange<float>(0.5f, 100f)));
            BurrowSeconds = H("Burrow", "BurrowSeconds", 0.9f, new ConfigDescription("How long sinking takes, s.", new AcceptableValueRange<float>(0.1f, 5f)));
            RiseSeconds = H("Burrow", "RiseSeconds", 0.7f, new ConfigDescription("How long coming up takes, s (half of it when alerted).", new AcceptableValueRange<float>(0.1f, 5f)));
            BurrowDepth = H("Burrow", "Depth", 0.75f, new ConfigDescription("How much of the body goes under, 0.2-1.", new AcceptableValueRange<float>(0.2f, 1f)));
            BurrowSandKeywords = H("Burrow", "SandKeywords", "sand", "A terrain texture layer whose name (or texture name) contains one of these counts as sand.");
            BurrowUseLayers = H("Burrow", "UseTextureLayers", false, "Also require the terrain texture under the creature to be a sand-named layer (see SandKeywords). Off: any open ground (terrain, not an object) is sand.");
            BurrowTerrainIsSand = H("Burrow", "TerrainIsSand", true, "When a terrain has no layer named like the keywords (or no readable layers) its ground counts as sand; meshes (rock, floors, roads) never do.");
            BurrowFxScale = H("Burrow", "FxScale", 0.3f, new ConfigDescription("Size of the sand puff compared with the worm's.", new AcceptableValueRange<float>(0.05f, 1f)));
            BurrowFxVolume = H("Burrow", "FxVolume", 0.35f, new ConfigDescription("Volume of the burrow sound.", new AcceptableValueRange<float>(0f, 1f)));
            BurrowFxPitch = H("Burrow", "FxPitch", 1.5f, new ConfigDescription("Pitch of the burrow sound (higher = smaller creature).", new AcceptableValueRange<float>(0.5f, 3f)));
            BurrowFxRange = H("Burrow", "FxRange", 45f, new ConfigDescription("No puff or sound when the camera is farther than this, m.", new AcceptableValueRange<float>(5f, 300f)));
            BurrowLog = H("Burrow", "Log", false, "Log burrowing and the terrain layers found.");
            DogPrefabs = H("Dogs", "Prefabs", "Wild Hound,Yard Hound,Grimhound,Nightwalker,Alpha_Nightwalker", "Creatures handled as dogs (exact prefab names).");
            DogLog = H("Dogs", "Log", false, "Log each dog's reach and the head / neck bones found.");
            AmbushPrefabs = H("Ambush", "Prefabs", "Scorpion_Small,Scorpion_Big,Spider_Small,Spider_Big", "Creatures that lie in wait (exact prefab names).");
            AmbushMin = H("Ambush", "MinDistance", 3f, new ConfigDescription("They let a target come this close at the least before attacking, m (chosen at random each time they settle).", new AcceptableValueRange<float>(0.5f, 50f)));
            AmbushMax = H("Ambush", "MaxDistance", 5f, new ConfigDescription("... and at the most, m.", new AcceptableValueRange<float>(0.5f, 50f)));
            NestPrefabs = H("Nests", "Prefabs", "Spider_Small,Spider_Big", "Creatures that make a place a nest and answer its alarm (exact prefab names).");
            NestSitters = H("Nests", "Sitters", "Spider_Big", "Creatures that sit in the hanging webs, one per web.");
            NestBuildRange = H("Nests", "BuildRange", 520f, new ConfigDescription("A nest is built when you are this close, m (the game spawns its spiders at 500 m).", new AcceptableValueRange<float>(50f, 2000f)));
            NestFreeRange = H("Nests", "FreeRange", 750f, new ConfigDescription("A nest's webs are removed when you are farther than this, m (rebuilt the same when you come back).", new AcceptableValueRange<float>(100f, 5000f)));
            NestPoiRadius = H("Nests", "PlaceRadius", 80f, new ConfigDescription("A spider belongs to the nearest cave / wreck / camp within this distance, m.", new AcceptableValueRange<float>(10f, 300f)));
            NestMaxRadius = H("Nests", "MaxRadius", 35f, new ConfigDescription("Webs are spread at most this far from the nest's middle, m.", new AcceptableValueRange<float>(8f, 100f)));
            NestMinWebs = H("Nests", "MinHangingWebs", 3, new ConfigDescription("Hanging webs per nest at the least (where the place has room for them).", new AcceptableValueRange<int>(0, 30)));
            NestMaxWebs = H("Nests", "MaxHangingWebs", 7, new ConfigDescription("... and at the most (big spiders + 1-3).", new AcceptableValueRange<int>(0, 30)));
            NestGroundMin = H("Nests", "MinGroundWebs", 4, new ConfigDescription("Ground webs per nest at the least.", new AcceptableValueRange<int>(0, 30)));
            NestGroundMax = H("Nests", "MaxGroundWebs", 7, new ConfigDescription("... and at the most.", new AcceptableValueRange<int>(0, 30)));
            NestSpots = H("Nests", "SpotsTried", 70, new ConfigDescription("Spots looked at for hanging webs per nest (each: 1 + 16 short rays).", new AcceptableValueRange<int>(5, 500)));
            NestFrameBudgetMs = H("Nests", "FrameBudgetMs", 1f, new ConfigDescription("Building a nest takes at most about this much time per frame, ms.", new AcceptableValueRange<float>(0.2f, 20f)));
            NestAlarmSpeed = H("Nests", "AlarmSpeed", 1.5f, new ConfigDescription("Alarmed by a touched web, the nest's spiders run this many times faster.", new AcceptableValueRange<float>(1f, 4f)));
            NestAlarmSeconds = H("Nests", "AlarmSeconds", 20f, new ConfigDescription("... for this long, s.", new AcceptableValueRange<float>(1f, 300f)));
            NestAlarmCooldown = H("Nests", "AlarmCooldown", 5f, new ConfigDescription("A nest is alarmed again by its webs at most this often, s.", new AcceptableValueRange<float>(1f, 120f)));
            NestSitterReach = H("Nests", "SitterReach", 1f, new ConfigDescription("A spider sitting in its web attacks only what comes this close, m (shot or alarmed: at once).", new AcceptableValueRange<float>(0.3f, 20f)));
            NestLog = H("Nests", "Log", false, "Log seating, drops and nests freed (the nest summary and alarms are always logged).");
            PouncePrefabs = H("Pounce", "Prefabs", "Spider_Small", "Creatures that pounce (exact prefab names).");
            PounceZigMin = H("Pounce", "ZigzagFromMin", 8f, new ConfigDescription("It starts zigzagging this close at the least, m (chosen at random per try).", new AcceptableValueRange<float>(1f, 30f)));
            PounceZigMax = H("Pounce", "ZigzagFromMax", 11f, new ConfigDescription("... and at the most, m.", new AcceptableValueRange<float>(1f, 20f)));
            PounceLeapMin = H("Pounce", "LeapFromMin", 5f, new ConfigDescription("It leaps from this distance at the least, m (chosen at random per try).", new AcceptableValueRange<float>(0.5f, 12f)));
            PounceLeapMax = H("Pounce", "LeapFromMax", 6f, new ConfigDescription("... and at the most, m.", new AcceptableValueRange<float>(0.5f, 12f)));
            PounceZigAngle = H("Pounce", "ZigzagAngle", 35f, new ConfigDescription("How far off the straight line each zigzag leg turns, degrees.", new AcceptableValueRange<float>(0f, 80f)));
            PounceZigSeconds = H("Pounce", "ZigzagSeconds", 0.35f, new ConfigDescription("Length of one zigzag leg, s (+-30 %).", new AcceptableValueRange<float>(0.1f, 2f)));
            PounceZigSpeed = H("Pounce", "ZigzagSpeed", 1.3f, new ConfigDescription("Zigzag and run-off speed as a multiple of its run speed.", new AcceptableValueRange<float>(0.5f, 4f)));
            PounceLeapSpeed = H("Pounce", "LeapSpeed", 10f, new ConfigDescription("Speed of the leap, m/s (sets the flight time).", new AcceptableValueRange<float>(1f, 30f)));
            PouncePitch = H("Pounce", "Pitch", 70f, new ConfigDescription("How far the body rears back in the air, degrees (belly towards the player).", new AcceptableValueRange<float>(0f, 110f)));
            PounceFleeMin = H("Pounce", "RunOffMin", 1.5f, new ConfigDescription("After a bite it runs away at least this long, s.", new AcceptableValueRange<float>(0.2f, 20f)));
            PounceFleeMax = H("Pounce", "RunOffMax", 2.5f, new ConfigDescription("... and at the most, s.", new AcceptableValueRange<float>(0.2f, 20f)));
            PounceCooldownMin = H("Pounce", "CooldownMin", 4f, new ConfigDescription("Then it chases as usual at least this long before the next pounce, s.", new AcceptableValueRange<float>(0f, 120f)));
            PounceCooldownMax = H("Pounce", "CooldownMax", 7f, new ConfigDescription("... and at the most, s.", new AcceptableValueRange<float>(0f, 120f)));
            PounceLog = H("Pounce", "Log", false, "Log every zigzag, leap, bite and miss.");
            HuntPrefabs = H("Hunt", "Prefabs", "Scorpion_Big", "Creatures that burrow AND hunt underground: they travel under the sand and come up behind the enemy (exact prefab names).");
            HuntRange = H("Hunt", "Range", 15f, new ConfigDescription("Burrowed, a hunter engages anything within this distance; fighting, it dives when the target is farther than this, m.", new AcceptableValueRange<float>(2f, 100f)));
            HuntMaxDistance = H("Hunt", "MaxDistance", 60f, new ConfigDescription("No dive after a target farther than this, m (it chases or loses it as usual).", new AcceptableValueRange<float>(5f, 300f)));
            HuntBehind = H("Hunt", "Behind", 2.5f, new ConfigDescription("It comes up this far behind the enemy, m.", new AcceptableValueRange<float>(0.5f, 10f)));
            HuntSpeed = H("Hunt", "Speed", 5f, new ConfigDescription("Travel speed underground when the creature's own run speed is not known yet, m/s.", new AcceptableValueRange<float>(0.5f, 30f)));
            HuntCooldown = H("Hunt", "Cooldown", 6f, new ConfigDescription("After coming up it fights above ground at least this long before it dives again, s.", new AcceptableValueRange<float>(0f, 120f)));
            HuntDiveSeconds = H("Hunt", "DiveSeconds", 1.6f, new ConfigDescription("How long going fully under takes, s.", new AcceptableValueRange<float>(0.1f, 5f)));
            HuntRiseSeconds = H("Hunt", "RiseSeconds", 1.0f, new ConfigDescription("How long bursting out takes, s.", new AcceptableValueRange<float>(0.1f, 5f)));
            HuntFallbackMin = H("Hunt", "FallbackMin", 3f, new ConfigDescription("No room behind the enemy (2 spots tried): it comes back up where it went down after at least this long, s.", new AcceptableValueRange<float>(0f, 60f)));
            HuntFallbackMax = H("Hunt", "FallbackMax", 5f, new ConfigDescription("... and at the most, s.", new AcceptableValueRange<float>(0f, 60f)));
            HuntTravelFactor = H("Hunt", "TravelSpeedFactor", 2f, new ConfigDescription("Underground it travels this many times faster than it runs.", new AcceptableValueRange<float>(0.25f, 10f)));
            HuntBurrowFactor = H("Hunt", "BurrowSlowdown", 2f, new ConfigDescription("A hunter's ordinary burrowing and coming up (lying in wait) take this many times longer than a small scorpion's.", new AcceptableValueRange<float>(0.25f, 10f)));
            HuntFxScale = H("Hunt", "FxScale", 0.9f, new ConfigDescription("Size of a hunter's sand puff compared with the worm's.", new AcceptableValueRange<float>(0.05f, 1f)));
            HuntFxVolume = H("Hunt", "FxVolume", 1f, new ConfigDescription("Volume of a hunter's burrow sound.", new AcceptableValueRange<float>(0f, 1f)));
            HuntFxPitch = H("Hunt", "FxPitch", 1f, new ConfigDescription("Pitch of a hunter's burrow sound.", new AcceptableValueRange<float>(0.5f, 3f)));
            AimTimeScale = H("NpcAim", "AimTimeScale", 50f, new ConfigDescription(
                "How long NPCs take to aim between bursts, as % of the game's own pause (3-5 s, plus the distance delay): 50 = half the time, 100 = as the game, 300 = three times slower.",
                new AcceptableValueRange<float>(1f, 300f)));
            FacingTolerance = H("NpcAim", "FacingTolerance", 8f, new ConfigDescription(
                "An NPC fires only once its body faces you within this many degrees (it turns at [Brain] TurnRate); until then it keeps turning and checks again every 0.1-0.2 s.",
                new AcceptableValueRange<float>(0f, 90f)));
            EngagePercent = H("NpcAim", "EngagePercent", 85f, new ConfigDescription(
                "NPCs open fire once the target is within this % of the gun's reach; farther away they keep closing in. Never beyond the reach itself.",
                new AcceptableValueRange<float>(1f, 100f)));
            Magazines = H("NpcAim", "Magazines", true, "Gunmen reload: after the magazine of their gun (MagazineTable: the player's own weapons' capacities) they stop firing for the reload (Apocaplayer's RifleReload / PistolReload clip when its animations are in use, else ReloadSeconds). They reload standing, kneeling or in cover, not while running.");
            MagazineTable = H("NpcAim", "MagazineTable", "22_pipe_pistol=1, 22_pipe_revolver=5, 22_pipe_smg=30, akm_drum=70, akm_trash=30, akms=30, borz_smg=25, crossbow=1, folk_17=17, m16a1=30, redmark_m11=4, redmark_m11_scoped=4, rochester_m24=2, rochester_m24_chopped=2, slamberg_500=5, slamberg_500_chopped=5, slamfire_shotgun=1",
                "Shots per magazine by weapon model (the game's own Reload FSM capacities of the player's weapons, 2026-10-06; the 22_pipe_smg's 165 is cut to 30 here). A gun not listed never reloads.");
            SingleShotReload = H("NpcAim", "SingleShotReload", 1f, new ConfigDescription("Reload time of a gun with a magazine of 1-2 (pipe pistol, slam-fire shotgun, crossbow, the Rochesters): the reload clip is sped up to it, s.", new AcceptableValueRange<float>(0.3f, 5f)));
            ReloadVolume = H("NpcAim", "ReloadVolume", 1f, new ConfigDescription("Volume of a gunman's reload sounds (the clips the player's copy of the gun plays while reloading). 0 = silent.", new AcceptableValueRange<float>(0f, 1f)));
            ReloadSoundRange = H("NpcAim", "ReloadSoundRange", 35f, new ConfigDescription("How far a reload is heard, m (linear falloff from 2 m).", new AcceptableValueRange<float>(5f, 100f)));
            RoundSeconds = H("NpcAim", "RoundSeconds", 0.6f, new ConfigDescription("Guns loaded one round at a time (revolver, shotguns, bolt rifle, double barrel - as the player's copy of the gun): seconds per round; the reload clip's round part plays once per round.", new AcceptableValueRange<float>(0.2f, 3f)));
            ReloadSeconds = H("NpcAim", "ReloadSeconds", 2.5f, new ConfigDescription("Reload time without a reload clip, s.", new AcceptableValueRange<float>(0.5f, 10f)));
            ReactionTime = H("Brain", "ReactionTime", 100f, new ConfigDescription(
                "How quickly NPCs think and react, as % of the default: every wait of the brain (looks, backing out of a stuck, resting, keeping a way around an obstacle, rechecks while holding) is scaled by this. 50 = twice as quick, 500 = five times slower.",
                new AcceptableValueRange<float>(1f, 500f)));
            TurnRate = H("Brain", "TurnRate", 210f, new ConfigDescription(
                "How fast an NPC turns its body, degrees per second - also to face you for a shot (the game snapped instantly).", new AcceptableValueRange<float>(30f, 720f)));
            AimPose = H("Brain", "AimPose", true,
                "A gunman holding a shooting position keeps the gun up and aimed at you between bursts (the game lowered it to the idle pose and raised it only for the shot).");
            CrouchChance = H("Brain", "CrouchChance", 50f, new ConfigDescription(
                "% chance that a gunman kneels when he takes a shooting position (humans only; he stands up when he moves again). He is harder to hit kneeling: his hitbox shrinks with him.",
                new AcceptableValueRange<float>(0f, 100f)));
            FeelerLength = H("Brain", "FeelerLength", 3.5f, new ConfigDescription("How far ahead a moving NPC looks for obstacles, m.", new AcceptableValueRange<float>(1f, 10f)));
            MeleeFeelerLength = H("Brain", "MeleeFeelerLength", 2.5f, new ConfigDescription("How far ahead a melee NPC looks, m (they turn quicker than a gunman needs).", new AcceptableValueRange<float>(1f, 10f)));
            FeelerAngle = H("Brain", "FeelerAngle", 60f, new ConfigDescription("Half-angle of the feeler fan around the direction to the target, degrees.", new AcceptableValueRange<float>(15f, 120f)));
            FeelerCount = H("Brain", "FeelerCount", 7, new ConfigDescription("Feeler rays per look (odd; fewer = cheaper, coarser).", new AcceptableValueRange<int>(3, 15)));
            DropCheck = H("Brain", "DropCheck", true, "A moving NPC also checks for ground 1.5 m along its chosen direction and picks another when there is a drop (one extra ray).");
            AdvanceChance = H("Brain", "AdvanceChance", 10f, new ConfigDescription(
                "A gunman holding a shooting position rolls this % at every hold recheck (1-4 s) to run toward you for AdvanceMin..AdvanceMax s instead.", new AcceptableValueRange<float>(0f, 100f)));
            AdvanceMin = H("Brain", "AdvanceMin", 2f, new ConfigDescription("Shortest advance, s.", new AcceptableValueRange<float>(0.5f, 20f)));
            AdvanceMax = H("Brain", "AdvanceMax", 4f, new ConfigDescription("Longest advance, s.", new AcceptableValueRange<float>(0.5f, 20f)));
            StuckBackupSeconds = H("Brain", "StuckBackupSeconds", 0.8f, new ConfigDescription("A stuck NPC backs up this long, s, before trying another way.", new AcceptableValueRange<float>(0.1f, 5f)));
            StuckMemorySeconds = H("Brain", "StuckMemorySeconds", 5f, new ConfigDescription("How long the heading it got stuck on is avoided, s.", new AcceptableValueRange<float>(0f, 60f)));
            StuckGiveUpCount = H("Brain", "StuckGiveUpCount", 3, new ConfigDescription("Stucks within 10 s after which the NPC stands still for a second (facing you) before trying again.", new AcceptableValueRange<int>(1, 20)));
            ApocaplayerClips = H("Brain", "ApocaplayerClips", true, "With Apocaplayer 2.2.0+ installed: gunmen are animated by its ModAPI - the player's own clips and logic (8-way walk / run / sprint, crouch for rifles and pistols, aim and fire, reloads incl. one round at a time, pump / bolt after a burst, turns in place, hops) with the gun in the right hand at the player's weapon poses. false = the game's own clips (also a quick A/B for performance).");
            StrafeAngle = H("Brain", "StrafeAngle", 50f, new ConfigDescription("(with Apocaplayer's clips) A chasing gunman whose way round something is within this many degrees of the line to his target keeps facing the target and strafes along it instead of turning his body. 0 = always turn.", new AcceptableValueRange<float>(0f, 90f)));
            CoverBelow = H("Brain", "CoverBelow", 50f, new ConfigDescription("A fighting human whose health drops below this % runs to cover (a cover object of the camp, or a spot its map shows breaks your line of sight, within CoverRange) and fights from there for CoverMin..CoverMax s. 0 = off.", new AcceptableValueRange<float>(0f, 100f)));
            CoverRange = H("Brain", "CoverRange", 25f, new ConfigDescription("How far a cover spot may be, m.", new AcceptableValueRange<float>(3f, 100f)));
            CoverMin = H("Brain", "CoverMin", 20f, new ConfigDescription("Shortest stay in cover, s (it leaves early when you come within 8 m).", new AcceptableValueRange<float>(1f, 300f)));
            CoverMax = H("Brain", "CoverMax", 40f, new ConfigDescription("Longest stay in cover, s.", new AcceptableValueRange<float>(1f, 300f)));
            MaxDistance = H("Brain", "MaxDistance", 150f, new ConfigDescription("NPCs farther than this from their target move the game's way (no cost).", new AcceptableValueRange<float>(20f, 1000f)));
            NightHours = H("Senses", "NightHours", "20=100, 21=75, 22=35, 23=0, 4=35, 5=75, 6=100",
                "How bright it is to NPC eyes by the game clock, hour=percent of daylight (100 = SightRange, 0 = DarkSightRange), straight lines in between, wrapping at midnight.");
            FlankDistance = H("Senses", "FlankDistance", 15f, new ConfigDescription("A human going to check a spot at least this far away does not walk straight at it: it comes in from one side (FlankWidth m off the line, 60 % of the way), the side alternating between NPCs. 0 = off. Trackers go straight.", new AcceptableValueRange<float>(0f, 200f)));
            FlankWidth = H("Senses", "FlankWidth", 10f, new ConfigDescription("See FlankDistance, m.", new AcceptableValueRange<float>(0f, 50f)));
            NoticeSeconds = H("Senses", "NoticeSeconds", 0.2f, new ConfigDescription("How long a target has to be in view before the NPC reacts, s (up close, within 15 m).", new AcceptableValueRange<float>(0f, 5f)));
            NoticeFar = H("Senses", "NoticeFar", 2f, new ConfigDescription("... and at the edge of its sight, s (in between it scales with the distance; up to twice as long in the dark).", new AcceptableValueRange<float>(0f, 10f)));
            LoseSeconds = H("Senses", "LoseSeconds", 5f, new ConfigDescription("How long a target can be out of view before the NPC counts it as lost and goes to where it last saw it, s (at least 5).", new AcceptableValueRange<float>(5f, 10f)));
            FoughtMemorySeconds = H("Senses", "FoughtMemorySeconds", 90f, new ConfigDescription("A convoy crew member that fought the player (engaged him, was hit by him, heard his shots, or its car's crew fought him) knows where he is for this long after the last contact, s: losing sight does not drop him meanwhile (it follows his real position, out of caves too). Camp NPCs lose and search for him as usual. Convoy crews on foot target the player only after noticing him (sight, his shots, his hits) or while they remember him.", new AcceptableValueRange<float>(10f, 600f)));
            Guesses = H("Senses", "Guesses", 3, new ConfigDescription("How many times an NPC that lost you from sight goes to a new spot (a guess for humans, your real position for trackers) before it gives up and searches where it stands.", new AcceptableValueRange<int>(0, 10)));
            GuessRadius = H("Senses", "GuessRadius", 8f, new ConfigDescription("A human that reaches the spot it last saw you and finds nothing guesses where you went: this far off (m) the first time, twice that the second, three times the third (PursuitMin..Max guesses). Trackers follow your real position instead.", new AcceptableValueRange<float>(0f, 50f)));
            Trackers = H("Senses", "Trackers", "Hound,Grimhound,Pup,Nightwalker,Spider,Arachnid,Scorpion", "NPC types (name parts) that track you by smell: they go to where you really are when they lose sight, and know exactly where a hit came from.");
            TauntMin = H("Senses", "TauntMin", 12f, new ConfigDescription("An NPC shouts only while it sees you: at once when it spots you, then after a pause of TauntMin..TauntMax seconds (the game shouted every 0.1-4 s while it had any target).", new AcceptableValueRange<float>(1f, 120f)));
            TauntMax = H("Senses", "TauntMax", 25f, new ConfigDescription("See TauntMin.", new AcceptableValueRange<float>(1f, 120f)));
            GunshotResponders = H("Senses", "GunshotResponders", 4, new ConfigDescription("How many NPCs go to check a gunshot or an explosion: the nearest this many, plus everyone within GunshotNearRange. 0 = everyone in earshot.", new AcceptableValueRange<int>(0, 50)));
            GunshotNearRange = H("Senses", "GunshotNearRange", 25f, new ConfigDescription("See GunshotResponders, m.", new AcceptableValueRange<float>(0f, 300f)));
            SearchSeconds = H("Senses", "SearchSeconds", 30f, new ConfigDescription("How long an NPC looks around at the place it went to check before it loses interest, s.", new AcceptableValueRange<float>(0f, 120f)));
            GhostTimeout = H("Senses", "GhostTimeout", 60f, new ConfigDescription("An NPC walking to a spot it goes to check (a ghost) keeps going until it gets there; only if this many seconds pass with no news about that spot (the ghost not renewed or moved) does it give up the walk and search from where it is, s.", new AcceptableValueRange<float>(5f, 600f)));
            ArriveDistance = H("Senses", "ArriveDistance", 1.5f, new ConfigDescription("How close to the remembered spot counts as being there, m.", new AcceptableValueRange<float>(0.5f, 10f)));
            LookInterval = H("Senses", "LookInterval", 0.15f, new ConfigDescription("How often each NPC looks, s (a couple of rays per look).", new AcceptableValueRange<float>(0.05f, 2f)));
            ShotRangePistol = H("Senses", "ShotRangePistol", 80f, new ConfigDescription("A pistol or revolver shot is heard this far, m.", new AcceptableValueRange<float>(0f, 1000f)));
            ShotRangeSmg = H("Senses", "ShotRangeSmg", 120f, new ConfigDescription("An SMG shot is heard this far, m.", new AcceptableValueRange<float>(0f, 1000f)));
            ShotRangeRifle = H("Senses", "ShotRangeRifle", 150f, new ConfigDescription("A rifle or machine-gun shot is heard this far, m.", new AcceptableValueRange<float>(0f, 1000f)));
            ShotRangeSniper = H("Senses", "ShotRangeSniper", 150f, new ConfigDescription("A sniper rifle shot is heard this far, m.", new AcceptableValueRange<float>(0f, 1000f)));
            ShotRangeShotgun = H("Senses", "ShotRangeShotgun", 150f, new ConfigDescription("A shotgun blast is heard this far, m.", new AcceptableValueRange<float>(0f, 1000f)));
            ShotRangeCrossbow = H("Senses", "ShotRangeCrossbow", 15f, new ConfigDescription("A crossbow shot is heard this far, m.", new AcceptableValueRange<float>(0f, 1000f)));
            NpcShotRanges = H("Senses", "NpcShotRanges", "Flexa=150, Gungirl=150, Lugnut=150, Scrud=150, Boltjaw=120, Sprokka=80, Shoota=80, Pistoleer=80, Gunnar=150, Lugger=150",
                "How far each NPC type's gunfire is heard, m, as Type=metres pairs; a type not listed uses the range of its weapon class above.");
            AllClearRange = H("Senses", "AllClearRange", 30f, new ConfigDescription("A human that searched a spot and found nothing tells same-faction humans within this range who are going to (or searching) the same spot: they give up and go back too.", new AcceptableValueRange<float>(0f, 200f)));
            TauntRange = H("Senses", "TauntRange", 15f, new ConfigDescription("A human's shout passes on what it knows to same-faction humans within this range - what it sees (as sight) or the spot it is going to check (at that spot's own rank) - and tells its enemies where it stands, m. 0 = off.", new AcceptableValueRange<float>(0f, 200f)));
            ShoutBlocksButtons = H("Senses", "ShoutBlocksButtons", "Kick,ShiftDown",
                "The game's input buttons (Input Manager names) ignored while ShoutModifier is held.");
            PlayerShoutRange = H("Senses", "PlayerShoutRange", 25f, new ConfigDescription("How far your shout carries, m.", new AcceptableValueRange<float>(0f, 300f)));
            ShoutCooldown = H("Senses", "ShoutCooldown", 1.5f, new ConfigDescription("Shortest time between two of your shouts, s.", new AcceptableValueRange<float>(0f, 30f)));
            StormSight = H("Senses", "StormSight", 80f, new ConfigDescription("Sight range in a sandstorm, % of normal: when the NPC looking or what it looks at is inside one (applied once, not twice).", new AcceptableValueRange<float>(0f, 100f)));
            StormHearing = H("Senses", "StormHearing", 50f, new ConfigDescription("Hearing range in a sandstorm, % of normal: gunfire, explosions, shouts and engines, when the sound or the listener is inside one (applied once, not twice).", new AcceptableValueRange<float>(0f, 100f)));
            StormRadius = H("Senses", "StormRadius", 2500f, new ConfigDescription("A sandstorm covers this far around its centre, m (the game's own distance for the sand around the player).", new AcceptableValueRange<float>(100f, 10000f)));
            ShoutVolume = H("Senses", "ShoutVolume", 1f, new ConfigDescription("Volume of your shout.", new AcceptableValueRange<float>(0f, 1f)));
            HumanFactions = H("Senses", "HumanFactions", "Scrapyard,Coyotes", "Which factions (object tags) count as humans for taunts.");
            EngineMinRange = H("Senses", "EngineMinRange", 50f, new ConfigDescription("Your running engine is heard this far with the weakest engine (EngineMinHp), m.", new AcceptableValueRange<float>(0f, 1000f)));
            EngineMaxRange = H("Senses", "EngineMaxRange", 150f, new ConfigDescription("... and this far with the strongest (EngineMaxHp), m.", new AcceptableValueRange<float>(0f, 1000f)));
            EngineMinHp = H("Senses", "EngineMinHp", 40f, new ConfigDescription("Horsepower that counts as the weakest engine.", new AcceptableValueRange<float>(1f, 2000f)));
            EngineMaxHp = H("Senses", "EngineMaxHp", 300f, new ConfigDescription("Horsepower that counts as the strongest engine.", new AcceptableValueRange<float>(1f, 2000f)));
            EngineIdleFactor = H("Senses", "EngineIdleFactor", 50f, new ConfigDescription("Engine range while idling (no throttle, standing), % of the driving range. A switched-off engine is silent.", new AcceptableValueRange<float>(0f, 100f)));
            BailOutAware = H("Senses", "BailOutAware", true,
                "With Apocapatrol: a raider bailing out of a car keeps what its crew knew - it fights at once if it sees you, otherwise it heads for where you were and searches.");
            BailOutAwareRange = H("Senses", "BailOutAwareRange", 150f, new ConfigDescription("With Apocapatrol: a crew bailing out knows where you are if you are within this range of its car, m.", new AcceptableValueRange<float>(0f, 1000f)));
            ExplosionRange = H("Senses", "ExplosionRange", 150f, new ConfigDescription("With Apocapatrol: an exploding raider car is heard this far (like a gunshot), m. 0 = silent.", new AcceptableValueRange<float>(0f, 1000f)));
            BlastRange = H("Senses", "BlastRange", 150f, new ConfigDescription("Grenades, blast lances, Blast Rats and Blast Zombies exploding are heard this far (like a gunshot), m. 0 = silent.", new AcceptableValueRange<float>(0f, 1000f)));
            BlastPrefabs = H("Senses", "BlastPrefabs", "Explosion_Grenade,Explosion_Can,Explosion_BlastRat,Explosion_BlastZombie", "Explosion prefabs heard within BlastRange when the game spawns them (Explosion_Grenade = grenade + blast lance, Explosion_Can = stronger blast lance, Explosion_BlastRat / Explosion_BlastZombie = the exploding creatures).");
            ThrowRange = H("Senses", "ThrowRange", 10f, new ConfigDescription("An item you throw draws NPCs within this range of where it lands, m. 0 = off.", new AcceptableValueRange<float>(0f, 200f)));
            NavBakeRange = H("Nav", "BakeRange", 200f, new ConfigDescription("A structure is mapped when you come within this distance of it, m.", new AcceptableValueRange<float>(30f, 1000f)));
            NavCellSize = H("Nav", "CellSize", 0.5f, new ConfigDescription("Map resolution, m (smaller = narrower gaps found, slower mapping). Very large structures get coarser cells automatically.", new AcceptableValueRange<float>(0.25f, 2f)));
            NavMargin = H("Nav", "Margin", 4f, new ConfigDescription("Open ground mapped around a structure's outline, m.", new AcceptableValueRange<float>(1f, 20f)));
            NavMaxStep = H("Nav", "MaxStep", 0.25f, new ConfigDescription("Largest height step between neighbouring map cells (0.5 m apart) an NPC can walk, m. An NPC's body can't climb much more than a kerb.", new AcceptableValueRange<float>(0.1f, 2f)));
            NavBakeBudgetMs = H("Nav", "BakeBudgetMs", 1f, new ConfigDescription("CPU time per frame spent mapping a structure, ms.", new AcceptableValueRange<float>(0.2f, 10f)));
            NavFieldSeconds = H("Nav", "FieldSeconds", 1f, new ConfigDescription("How long a computed route to one goal is reused by every NPC heading there, s.", new AcceptableValueRange<float>(0.2f, 10f)));
            IdleReturnDelay = H("Idle", "ReturnDelay", 0f, new ConfigDescription("Seconds an NPC stays where it lost track of everything before it heads home.", new AcceptableValueRange<float>(0f, 300f)));
            IdleReturnTries = H("Idle", "ReturnTries", 10, new ConfigDescription("Failed attempts to get home before the NPC forgets its home.", new AcceptableValueRange<int>(1, 100)));
            IdleRetrySeconds = H("Idle", "RetrySeconds", 5f, new ConfigDescription("Pause between two attempts to get home, s.", new AcceptableValueRange<float>(0f, 60f)));
            MoveFullSpeedAngle = H("Movement", "FullSpeedAngle", 30f, new ConfigDescription("A moving NPC runs at full speed while its body faces within this many degrees of where it wants to go.", new AcceptableValueRange<float>(0f, 180f)));
            MoveSlowestAngle = H("Movement", "SlowestAngle", 120f, new ConfigDescription("At this many degrees off (and beyond) it moves at SlowestSpeed - a big turn is made almost on the spot instead of as an arc.", new AcceptableValueRange<float>(1f, 180f)));
            MoveSlowestSpeed = H("Movement", "SlowestSpeed", 0.1f, new ConfigDescription("Speed factor (0-1) at SlowestAngle; between the two angles it goes linearly.", new AcceptableValueRange<float>(0f, 1f)));
            BlockedRatio = H("Movement", "BlockedRatio", 0.33f, new ConfigDescription("A chasing NPC that covers less than this share of the distance it is driven (running in place, grinding along a wall) is blocked: it hops or steps back and picks another way.", new AcceptableValueRange<float>(0.05f, 0.9f)));
            BlockedSeconds = H("Movement", "BlockedSeconds", 0.5f, new ConfigDescription("Over how many seconds that share is measured.", new AcceptableValueRange<float>(0.2f, 3f)));
            BlockedMemory = H("Movement", "BlockedMemory", 1.5f, new ConfigDescription("For how many seconds the direction it was blocked in is not taken again (on a map route).", new AcceptableValueRange<float>(0f, 10f)));
            TravelRunBeyond = H("Idle", "TravelRunBeyond", 25f, new ConfigDescription("NPCs sent somewhere by another module (convoy crews back to their car, foot patrols) run while farther than this from the goal and walk the rest, m. 0 = always run.", new AcceptableValueRange<float>(0f, 5000f)));
            IdleWalkRadius = H("Idle", "WalkRadius", 200f, new ConfigDescription("Camp walks only for NPCs within this distance of the camera, m.", new AcceptableValueRange<float>(20f, 1000f)));
            NavEnabled = BrainEnabled;
            NavLog = SensesLog = BrainLog = VerboseLog;
            ShowGhosts = ShowNav;
            PoiNav.Bind(cfg);   // (WW) POI navigation: pathfinding switch, setup / display developer options, editor key
        }
    }
}
