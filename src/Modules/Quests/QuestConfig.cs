using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal class EngineSettings
    {
        internal ConfigEntry<bool> apocasetter;
        internal ConfigEntry<Key> logKey;
        internal ConfigEntry<Key> dumpKey;
        internal ConfigEntry<bool> recordNpcs;
        internal ConfigEntry<int> maxActive;
        internal ConfigEntry<float> rewardMult;
        internal ConfigEntry<bool> showTracker;
        internal ConfigEntry<bool> generated;
        internal ConfigEntry<string> genTypes;
        internal ConfigEntry<string> moneyVar;
        internal ConfigEntry<float> handRadius;
        internal ConfigEntry<float> minLiquid;
        internal ConfigEntry<float> giverCooldown;
        internal ConfigEntry<float> itemRange;
        internal ConfigEntry<float> minHours;
        internal ConfigEntry<float> extraHoursPerKm;
        internal ConfigEntry<float> maxHours;
        internal ConfigEntry<float> noRepeatMeters;
        internal ConfigEntry<float> noRepeatHours;
        internal ConfigEntry<bool> countCarried;
        internal ConfigEntry<float> chainOfferHours;
        internal ConfigEntry<float> bodyRadius;
        internal ConfigEntry<float> bodyBonus;
        internal ConfigEntry<float> killRadius;
        internal ConfigEntry<float> questRange;
        internal ConfigEntry<float> gotoMin;
        internal ConfigEntry<float> gotoMax;
        internal ConfigEntry<float> takeoverRadius;
        internal ConfigEntry<float> takeoverMin;
        internal ConfigEntry<float> takeoverMax;
        internal ConfigEntry<int> takeoverKills;
        internal ConfigEntry<float> takeoverSpawnDist;
        internal ConfigEntry<int> takeoverGroup;
        internal ConfigEntry<float> takeoverWalk;
        internal ConfigEntry<int> garrisonSize;
        internal ConfigEntry<float> garrisonRange;
        internal ConfigEntry<float> giverRespawn;
        internal ConfigEntry<float> garrisonCooldown;
        internal ConfigEntry<float> deliverMin;
        internal ConfigEntry<float> deliverMax;
        internal ConfigEntry<float> deliverMinutesBase;
        internal ConfigEntry<float> deliverMinutesPerKm;
        internal ConfigEntry<float> deliverRadius;
        internal ConfigEntry<int> repFail;
        internal ConfigEntry<float> failPenaltyMult;
        internal ConfigEntry<int> repHostile;
        internal ConfigEntry<float> repRecoverHours;
        internal ConfigEntry<float> payPerRep;
        internal ConfigEntry<float> minPay;
        internal ConfigEntry<float> maxPay;
        internal ConfigEntry<bool> repShopOn;
        internal ConfigEntry<int> repBuyPrice;
        internal ConfigEntry<int> repSellPrice;
        internal ConfigEntry<int> repMaxDeal;
        internal ConfigEntry<string> repShopNpcs;
        internal ConfigEntry<float> scoutRange;
        internal ConfigEntry<bool> patrolAtCamp;
        internal ConfigEntry<float> homeRadius;
        internal ConfigEntry<string> humanFactions;
        internal ConfigEntry<string> peopleNames;

        internal void Bind(ModuleConfig cfg)
        {
            apocasetter = QuestConfigMigration.Bind(cfg, "General", "Apocasetter", true, "Show this mod in the Apocasetter Mods menu");
            logKey = QuestConfigMigration.BindKey(cfg, "General", "QuestLogKey", Key.J, "Key that shows or hides the quest log.");
            dumpKey = QuestConfigMigration.BindKey(cfg, "Debug", "FactionDumpKey", Key.None, "Troubleshooting: press near NPCs and enemies to write faction_dump.txt (next to the DLL) with every detail that might show which faction they belong to. None = off.");
            recordNpcs = QuestConfigMigration.Bind(cfg, "Debug", "RecordNpcNames", false, "For quest writers: log every NPC you talk to and keep lists of their names and home camps in npc_names.txt and npc_locations.txt next to the DLL.");
            maxActive = QuestConfigMigration.Bind(cfg, "General", "MaxActiveQuests", 5, "How many quests you can carry at once.");
            rewardMult = QuestConfigMigration.Bind(cfg, "General", "RewardMultiplier", 1f, "Multiplies every reward (hand-written and generated).");
            showTracker = QuestConfigMigration.Bind(cfg, "General", "ShowTracker", true, "Show a short list of your active quests in the top-right corner.");
            generated = QuestConfigMigration.Bind(cfg, "Generated", "Enabled", true, "NPCs can offer randomly generated jobs as well as the ones in quests.txt.");
            genTypes = QuestConfigMigration.Bind(cfg, "Generated", "Types", "fetch,kill,goto,talk,clear,takeover,deliver", "Which kinds of random jobs may be generated (fetch, kill, goto, talk, clear, takeover, deliver). Add takeover or deliver here if your config was made by an older version.");
            moneyVar = QuestConfigMigration.Bind(cfg, "Advanced", "MoneyVariable", "", "Name of the PlayMaker global int that holds your nuts. Leave empty to detect it; the log lists all global ints on first use.");
            handRadius = QuestConfigMigration.Bind(cfg, "Items", "HandInRadius", 10f, "Fetch quests also count matching items lying on the ground within this many metres of you.");
            minLiquid = QuestConfigMigration.Bind(cfg, "Items", "MinLiquidFraction", 0.5f, "Cans and other containers only count for fetch quests when they are at least this full (0.5 = half). 0 = any amount, even empty.");
            giverCooldown = QuestConfigMigration.Bind(cfg, "Quests", "GiverCooldownDays", 1f, "After you take a job from an NPC, or turn one down, that NPC has no new job for you for this many in-game days (sleeping counts). 1 = one day. 0 = no limit.");
            itemRange = QuestConfigMigration.Bind(cfg, "Quests", "ItemRange", 4000f, "A fetch quest is only offered when at least that many of the item exist within this many metres of the giver (items in the loaded part of the world, your own and the ones lying around; merchant stock never counts). 0 = no check.");
            minHours = QuestConfigMigration.Bind(cfg, "Quests", "MinimumHours", 24f, "Every job except a delivery has a time limit of at least this many in-game hours (24 = one day). When it runs out the job fails and costs FailPenalty reputation.");
            extraHoursPerKm = QuestConfigMigration.Bind(cfg, "Quests", "ExtraHoursPerKm", 4f, "Added to the time limit for every km between the giver and the target (go-to, camp and talk jobs).");
            maxHours = QuestConfigMigration.Bind(cfg, "Quests", "MaximumHours", 96f, "The most time any job is given (hand-written jobs with an hours = line may go over it).");
            noRepeatMeters = QuestConfigMigration.Bind(cfg, "Quests", "NoRepeatDistance", 400f, "An NPC will not offer the same quest (same camp, same enemy kind, same item...) that another NPC within this many metres has offered recently. 0 = off.");
            noRepeatHours = QuestConfigMigration.Bind(cfg, "Quests", "NoRepeatGameHours", 12f, "How long (in-game hours) an offered quest counts as taken for the neighbours.");
            countCarried = QuestConfigMigration.Bind(cfg, "Items", "CountCarriedItems", true, "Fetch quests also count items in your hand and quick slots.");
            chainOfferHours = QuestConfigMigration.Bind(cfg, "Quests", "ChainOfferGameHours", 24f, "A step of a quest chain (hand-written quests with chain / after) is always offered by an NPC that may give it, unless a different NPC offered that same step within this many in-game hours.");
            bodyRadius = QuestConfigMigration.Bind(cfg, "Kills", "BodyRadius", 35f, "Kill jobs: bodies of the kills count when they lie within this many metres of the giver as you report in.");
            bodyBonus = QuestConfigMigration.Bind(cfg, "Kills", "BodyBonus", 0.10f, "Kill jobs: bringing a body back to the giver adds this fraction of the reward for each body (0.10 = 10%), up to the number of kills asked for. Without bodies you get the normal reward. 0 = off.");
            killRadius = QuestConfigMigration.Bind(cfg, "Kills", "KillRadius", 300f, "Enemy deaths farther than this from you are ignored.");
            questRange = QuestConfigMigration.Bind(cfg, "Kills", "QuestRange", 1000f, "Kill quests only target enemies within this many metres of the giver, and only kills inside that area count. 0 = anywhere. Hand-written quests can override it with 'range'.");
            gotoMin = QuestConfigMigration.Bind(cfg, "Goto", "MinDistance", 150f, "Generated go-to quests pick points of interest at least this far from the giver.");
            gotoMax = QuestConfigMigration.Bind(cfg, "Goto", "MaxDistance", 1800f, "Generated go-to quests pick points of interest at most this far from the giver.");
            takeoverRadius = QuestConfigMigration.Bind(cfg, "Takeover", "CampRadius", 120f, "Kills of the camp's people within this many metres of the camp count towards a takeover quest.");
            takeoverMin = QuestConfigMigration.Bind(cfg, "Takeover", "MinDistance", 200f, "Generated takeover quests pick camps at least this far from the giver.");
            takeoverMax = QuestConfigMigration.Bind(cfg, "Takeover", "MaxDistance", 4000f, "Generated takeover quests pick camps at most this far from the giver.");
            takeoverKills = QuestConfigMigration.Bind(cfg, "Takeover", "KillsNeeded", 4, "How many of the camp's people you must kill (generated takeover quests).");
            takeoverSpawnDist = QuestConfigMigration.Bind(cfg, "Takeover", "WalkerSpawnDistance", 250f, "After the camp is cleared, the winning faction's people appear about this far from it and walk in.");
            takeoverGroup = QuestConfigMigration.Bind(cfg, "Takeover", "GroupSize", 5, "How many people walk in.");
            takeoverWalk = QuestConfigMigration.Bind(cfg, "Takeover", "WalkSeconds", 300f, "If they have not arrived after this long, they are placed at the camp.");
            garrisonSize = QuestConfigMigration.Bind(cfg, "Takeover", "GarrisonSize", 4, "When you come near a camp you took and none of its new owner's people are there, this many of them are created. 0 = off.");
            garrisonRange = QuestConfigMigration.Bind(cfg, "Takeover", "GarrisonRange", 500f, "How close you must be (metres) for an empty camp you took to get its garrison.");
            giverRespawn = QuestConfigMigration.Bind(cfg, "Quests", "GiverRespawnDistance", 500f, "How close you must be (metres) to where you last saw a quest giver for them to be brought back if they are gone.");
            garrisonCooldown = QuestConfigMigration.Bind(cfg, "Takeover", "GarrisonCooldownGameHours", 12f, "When one of a camp's garrison is killed while you are near, the camp is not refilled for this many in-game hours.");
            deliverMin = QuestConfigMigration.Bind(cfg, "Deliver", "MinDistance", 400f, "Deliveries go to a friendly camp at least this far (m) from the giver.");
            deliverMax = QuestConfigMigration.Bind(cfg, "Deliver", "MaxDistance", 3000f, "Deliveries go to a friendly camp at most this far (m) from the giver.");
            deliverMinutesBase = QuestConfigMigration.Bind(cfg, "Deliver", "GameHoursBase", 24f, "Time limit of a delivery: this many IN-GAME hours (the game clock, not real time)...");
            deliverMinutesPerKm = QuestConfigMigration.Bind(cfg, "Deliver", "GameHoursPerKm", 4f, "...plus this many in-game hours per km to the camp (straight line). Pausing stops it; sleeping uses it up.");
            deliverRadius = QuestConfigMigration.Bind(cfg, "Deliver", "CampRadius", 120f, "Talk to someone friendly within this many metres of the camp to hand the goods over.");
            repFail = QuestConfigMigration.Bind(cfg, "Reputation", "StolenPenalty", 3, "Reputation lost with the giver's faction when a delivery runs out of time (you kept the goods).");
            failPenaltyMult = QuestConfigMigration.Bind(cfg, "Reputation", "FailPenaltyMultiplier", 2f, "When a job fails (its time limit runs out) or you give it up, you lose this many times the reputation it would have given you for finishing it (at least 1). 0 = no penalty. A giver who dies costs nothing.");
            repHostile = QuestConfigMigration.Bind(cfg, "Reputation", "HostileAtRep", 0, "At or below this reputation (a negative number, the scale is -100 to +100) a faction's people turn hostile to you (the game's own PlayerIsEnemy switch). 0 = never (the default): bad reputation only lowers the pay. Factions always give work, whatever your reputation.");
            repRecoverHours = QuestConfigMigration.Bind(cfg, "Reputation", "RecoverGameHours", 24f, "Bad reputation creeps back by 1 every this many in-game hours. 0 = never.");
            payPerRep = QuestConfigMigration.Bind(cfg, "Reputation", "PayPerRep", 0.005f, "Jobs for a faction pay this fraction more for every point of positive reputation with it, and this fraction less for every negative point (0.005 = 0.5%). 0 = reputation does not change the pay.");
            minPay = QuestConfigMigration.Bind(cfg, "Reputation", "MinimumPayFraction", 0.1f, "Bad reputation never cuts the pay below this fraction of the normal reward.");
            maxPay = QuestConfigMigration.Bind(cfg, "Reputation", "MaximumPayMultiplier", 0f, "Good reputation never raises the pay above this many times the normal reward. 0 = no limit.");
            repShopOn = QuestConfigMigration.Bind(cfg, "RepVendor", "Enabled", true, "Merchants (see Npcs) buy and sell reputation with their own faction in the talk window: Buy / Sell 1, 5, 25, 50 or as many as possible.");
            repBuyPrice = QuestConfigMigration.Bind(cfg, "RepVendor", "BuyPrice", 20, new ConfigDescription("Nuts you pay for 1 reputation point.", new AcceptableValueRange<int>(0, 100000)));
            repSellPrice = QuestConfigMigration.Bind(cfg, "RepVendor", "SellPrice", 10, new ConfigDescription("Nuts you get for selling 1 reputation point.", new AcceptableValueRange<int>(0, 100000)));
            repMaxDeal = QuestConfigMigration.Bind(cfg, "RepVendor", "MaxPerDeal", 100, new ConfigDescription("The most reputation one 'all' button buys or sells at a time.", new AcceptableValueRange<int>(1, 200)));
            repShopNpcs = QuestConfigMigration.Bind(cfg, "RepVendor", "Npcs", "merchant", "Names of the NPCs who trade reputation (part of the name is enough, comma separated). They trade the reputation of the faction they belong to; an NPC with no faction does not.");
            scoutRange = QuestConfigMigration.Bind(cfg, "Takeover", "ScoutRange", 250f, "A camp's head count is only known once you are this close (metres); the quest log is updated then.");
            patrolAtCamp = QuestConfigMigration.Bind(cfg, "Takeover", "PatrolAtCamp", true, "People who moved into a camp you took walk around it a little. Off = they stand where they arrive.");
            homeRadius = QuestConfigMigration.Bind(cfg, "Factions", "HomeRadius", 120f, "A person without a faction tag counts as living at the nearest camp / building / cave within this many metres, and takes that place's faction.");
            humanFactions = QuestConfigMigration.Bind(cfg, "Factions", "HumanFactions", "Scrapyard,Coyotes", "Unity tags the game uses for human factions (taken from NPCAI). NPCs with one of these tags belong to that faction; camps with an EnemySpawn_<Faction> child belong to it too.");
            peopleNames = QuestConfigMigration.Bind(cfg, "Factions", "PeopleNames", "bodyguard,pistoleer,pup,merchant,bolter,lugger,scout,scraper,chieftain,elder", "Names that count as people (not animals) when working out who lives where. Enemies with these names are protected from kill quests of their own faction.");
        }
    }

    internal sealed class HireSettings
    {
        internal ConfigEntry<bool> enabled;
        internal ConfigEntry<string> prices;
        internal ConfigEntry<float> durationHours;
        internal ConfigEntry<int> deathPenalty;
        internal ConfigEntry<float> followDist;
        internal ConfigEntry<float> teleportDist;
        internal ConfigEntry<string> ignoreNames;
        internal ConfigEntry<float> leash;
        internal ConfigEntry<bool> rideCars;
        internal ConfigEntry<bool> shootFromCar;
        internal ConfigEntry<bool> realFire;
        internal ConfigEntry<bool> shotAnim;
        internal ConfigEntry<float> recoilDeg;
        internal ConfigEntry<string> magSizes;
        internal ConfigEntry<string> reloadTimes;
        internal ConfigEntry<string> reloadSoundFrom;
        internal ConfigEntry<float> shotDamage;
        internal ConfigEntry<float> shotInterval;
        internal ConfigEntry<string> rangedNames;
        internal ConfigEntry<string> armPoseLike;
        internal ConfigEntry<float> rideRange;
        internal ConfigEntry<Key> dismissKey;

        internal void Bind(ModuleConfig cfg)
        {
            enabled = QuestConfigMigration.Bind(cfg, "Hire", "Enabled", true, "Bodyguards, pups, bolters, pistoleers, gunnars and luggers can be hired for reputation (needs the NPCAI and Apocapatrol mods).");
            prices = QuestConfigMigration.Bind(cfg, "Hire", "Hirable", "Bodyguard=5,Pup=1,Bolter=5,Pistoleer=5,Gunnar=15,Lugger=10", "Who can be hired and for how much reputation (with their own faction): name part = cost, comma separated. 0 or a missing name = not for hire.");
            durationHours = QuestConfigMigration.Bind(cfg, "Hire", "DurationGameHours", 24f, "How long a hire lasts, in in-game hours (24 = one day).");
            deathPenalty = QuestConfigMigration.Bind(cfg, "Hire", "DeathPenalty", 1, "Reputation lost with the NPC's faction when your hired help dies.");
            followDist = QuestConfigMigration.Bind(cfg, "Hire", "FollowDistance", 6f, "Metres the follower keeps behind you.");
            teleportDist = QuestConfigMigration.Bind(cfg, "Hire", "CatchUpDistance", 70f, "A follower this far behind you for a few seconds (walls, a car you drove off in) pops back next to you. 0 = never.");
            ignoreNames = QuestConfigMigration.Bind(cfg, "Hire", "IgnoreCritters", "Lizard", "Small animals the follower will not go hunting on its own (comma separated name parts). It still fights them if they hurt you.");
            leash = QuestConfigMigration.Bind(cfg, "Hire", "LeashDistance", 28f, "The follower gives up a fight and comes back when its target is further than this many metres from you. 0 = no limit.");
            rideCars = QuestConfigMigration.Bind(cfg, "Hire", "RideInCars", true, "The follower rides in your car's passenger seat when it is free, and gets out when you do.");
            shootFromCar = QuestConfigMigration.Bind(cfg, "Hire", "ShootFromCar", true, "A follower with a ranged weapon shoots at your enemies from the passenger seat, the way Apocapatrol's crews do (needs Apocapatrol).");
            realFire = QuestConfigMigration.Bind(cfg, "Hire", "RealShooting", true, "In the car, let the follower's own Detection/Attack/Damage Ranged chain shoot, as it does on foot. The mod's ShotDamage is only a backup for when that chain does not fire.");
            shotAnim = QuestConfigMigration.Bind(cfg, "Hire", "ShotAnimation", false, "Play the follower's whole shooting animation on each shot from the car. Off by default: it lifts his arms over Apocapatrol's aiming pose. The muzzle flash is shown on its own instead.");
            recoilDeg = QuestConfigMigration.Bind(cfg, "Hire", "RecoilDegrees", 10f, "How far a follower's gun arm kicks up on each shot from the car (degrees). 0 = no recoil.");
            magSizes = QuestConfigMigration.Bind(cfg, "Hire", "MagazineOverride", "", "Normally a follower's magazine size comes from the player's version of the gun he holds. Name=shots pairs here override it (e.g. Gunnar=20).");
            reloadTimes = QuestConfigMigration.Bind(cfg, "Hire", "ReloadOverride", "", "Normally a follower's reload time comes from the reload animation of the player's version of his gun. Name=seconds pairs here override it (e.g. Bolter=2).");
            reloadSoundFrom = QuestConfigMigration.Bind(cfg, "Hire", "ReloadSoundFrom", "Bolter=crossbow", "Whose reload sounds a follower uses when his gun is not recognised (Name=player weapon, e.g. akm_trash, slamberg_500, crossbow). Others use the sounds of the player's version of the gun they hold.");
            shotDamage = QuestConfigMigration.Bind(cfg, "Hire", "ShotDamage", 7f, "Damage of each shot a follower fires from the car (the mod delivers the hit itself, because the follower's own sensors cannot see your enemies). 0 = no damage.");
            shotInterval = QuestConfigMigration.Bind(cfg, "Hire", "ShotInterval", 0.9f, "Seconds between a follower's shots from the car.");
            rangedNames = QuestConfigMigration.Bind(cfg, "Hire", "RangedHires", "Bolter", "Hirable people who shoot from the car even though their ranged parts are not recognised (comma separated name parts). Gunnar, Pistoleer and Lugger are found automatically.");
            armPoseLike = QuestConfigMigration.Bind(cfg, "Hire", "ArmPoseLike", "Pistoleer=Scrud,Gunnar=Flexa,Lugger=Lugnut,Bolter=Boltjaw,Lugnut", "Apocapatrol only has gun-arm poses for its own humans (Boltjaw, Flexa, Lugnut, Scrud, Sprokka, Scraffa, Spanna). A ranged follower borrows the pose of this one so its arms aim and shoot. Use Name=Template pairs for different people, e.g. Gunnar=Scrud,Pistoleer=Lugnut; a lone name applies to everyone.");
            rideRange = QuestConfigMigration.Bind(cfg, "Hire", "RideRange", 35f, "When you get in a car, a follower within this many metres gets in too.");
            dismissKey = QuestConfigMigration.BindKey(cfg, "Hire", "DismissKey", Key.Delete, "Press to send your hired help away at any time. None = off (you can still dismiss them by talking to them).");
        }
    }

    internal sealed class VoiceSettings
    {
        internal ConfigEntry<bool> enabled;
        internal ConfigEntry<float> volume;
        internal ConfigEntry<float> spatial;
        internal ConfigEntry<string> folderCfg;

        internal void Bind(ModuleConfig cfg)
        {
            enabled = QuestConfigMigration.Bind(cfg, "Voice", "Enabled", true, "Play recorded voice lines for quests that have them (Voice Actors\\<actor>\\<quest id>\\offer.wav, accept.wav, remind.wav, done.wav).");
            volume = QuestConfigMigration.Bind(cfg, "Voice", "Volume", 5f, new ConfigDescription("Volume of the voice lines, 0 to 10. Every recording is first levelled to the same loudness; 5 is full level, above 5 boosts it further.", new AcceptableValueRange<float>(0f, 10f)));
            spatial = QuestConfigMigration.Bind(cfg, "Voice", "Spatial", 0f, "0 = the voice plays flat in your ears (best for dialogue), 1 = it comes from the NPC and gets quieter with distance.");
            folderCfg = QuestConfigMigration.Bind(cfg, "Voice", "Folder", "", "Where the Voice Actors folder is. Empty = a 'Voice Actors' folder next to the plugin DLL (or one level above it).");
        }
    }

}
