# Quest catalog in code - 2026-10-23

Scope: the shipped source defaults contain **16 authored quests**; the engine supports **7 quest types**, including unlimited generated instances. The installed `plugins/WildWasteland/Quests/quests.txt` is editable and is read at startup, so custom definitions can extend or replace this list. All numbers below are source defaults, not a claim about the player's current settings. No game was run.

## Shared offer and completion conditions

- Native interaction requires an active living recognized person, not hostile to the player or seated, within 4 horizontal metres, aimed at and unobstructed. Optional Apocatalker owns its own interaction when installed. [Native interaction](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/NativeConversation.cs:140).
- Current/former vehicle crew cannot give new work; existing turn-ins remain allowed. At most 5 active quests by default. A giver cannot offer another non-talk job while their previous one is active. The default giver cooldown is one game day. [AskForWork](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1713).
- A giver's faction must not be configured hostile (Scrapyard is hostile by default). Authored quests must match the giver name, not be active already, pass `once`/completed checks, prerequisites and nearby-offer reservations. `*` in the giver column below means any otherwise eligible giver, not every creature. Comma-separated giver/name lists use normalized substring matching; a standalone `*` accepts any name. [MakeOffer](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1304), [name matching](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:88).
- Nearby NPCs reserve the same authored ID or objective target within 400 m for 12 game hours by default. Chain steps take priority and are reserved against other givers for 24 game hours. A cached offer is checked again for active/completed/duplicate-position conflicts on acceptance.
- Fetch eligibility requires the requested quantity within `ItemRange` (default 4,000 m); merchant stock does not count. Set ItemRange=0 to bypass offer availability. Turn-in uses matching nearby ground goods within 10 m plus carried/quick-slot items by default; stacks count charges. Liquid containers must be at least 50% full by default. `=prefab` means exactly that prefab; plain tokens use the item matcher (prefab/display-name/category matching). [Authored preparation](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1509).
- Kill/clear targets are live actors remaining after faction protection: own faction, allies and neutral humans are protected; hostile-faction humans or humans at war with the giver may qualify; factionless recognized people qualify only when player-hostile. Animals/mutants are otherwise unprotected. The giver itself is excluded. Kill quests exclude names containing `lizard`; Clear does not apply that exclusion. Only observed deaths within 300 m of the player count, with the quest's additional area and target filters. [Faction protection](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Factions.cs:165), [death accounting](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:906).
- Ordinary POI exploration must be allowed by faction (unowned, own or allied), then excludes own-faction locations and already visited/explored locations. Check-ups instead require the giver's exact faction. Active/reserved destinations are excluded.
- Camp ownership comes from the POI world-location registry. Town ownership comes from the town plan, including town-owned building clones. Saved quest takeovers and explicit `factions.txt` POI overrides retain priority. Child names never infer ownership. Known towns join delivery, exploration, check-up and talk-direction candidates; their constituent building clones are not offered as independent camps. Town takeover is still excluded: the existing takeover implementation manages native camps, not city-plan ownership.
- Deliveries require a known same/allied owner, another destination at least 150 m away and within the quest's distance limits, and no active/reserved delivery there. Unloaded destinations remain eligible. If people are already loaded within 120 m, at least one must pass the existing friendly/non-hostile person check. This is not a requirement to have a loaded recipient. [Delivery picker](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:789).
- Fetch/Kill/Clear/Takeover/Goto normally require reporting back to the original giver. Talk and Deliver pay at the destination. A custom Goto can set `return=no`; Fetch/Kill/Takeover force report-back, while Talk/Deliver force destination completion.
- Non-delivery deadlines default to at least 24 game hours, plus 4 hours/km for relevant travel, capped at 96 hours unless an authored `hours` value supplies its own minimum-clamped deadline. Fetch/Clear use zero travel km. Kill uses its range capped at 4 km; Talk uses required distance. Delivery time defaults to 24+4*distanceKm game hours, or authored `minutes`; sleeping consumes game time. [Deadlines](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1805).
- Rewards below are base nuts. RewardMultiplier (default 1), faction reputation pay scaling (default 0.5% per rep, minimum 10%, no maximum) and any current player-skill payout modifier change payment. Unspecified rep is `clamp(round(sqrt(max(1,scaledBaseReward)/60)) + (takeover?1:0),1,6)`; check-ups give 1. Kill bodies within 35 m of the giver can add 10% of reward each, capped at requested kills. [FinishOffer](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1406).

## Authored quests

All 13 non-chain entries are repeatable (`once=no`); all three chain steps are once per save. No shipped entry adds `needpoi` or `needenemy` beyond the conditions below. The table's availability predicates also inherit the shared rules and the rules of its type.

| ID / title | Type | Eligible giver name contains | Exact target and additional condition | Base nuts / rep |
|---|---|---|---|---|
| [field_medic - Field Medic](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:93) | Fetch | * | 3 `bandage_1` counted charges, available near giver | 55 / computed |
| [dry_throats - Dry Throats](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:107) | Fetch | * | 1 `Water_Can_Plastic`, at least configured liquid fraction | 100 / 2 |
| [rations_run - Rations Run](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:122) | Fetch | merchant, scraper, lugger | 3 exact `=dogfood_can` | 170 / 3 |
| [fuel_shortage - Fuel Shortage](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:137) | Fetch | lugger, merchant, pistoleer | 1 `Gasoline_Can`, at least configured liquid fraction | 250 / computed |
| [hound_trouble - Hound Trouble](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:151) | Kill | * | 3 names matching `grimhound`; at least 3 eligible alive within default 1,000 m giver range | 150 / computed |
| [night_terror - Night Terror](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:165) | Kill | bodyguard, chieftain, scout | 2 names matching `nightwalker`; at least 2 eligible alive within default 1,000 m giver range | 200 / computed |
| [clear_the_camp - Patrol Camp](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:179) | Clear | chieftain, elder, bodyguard | 5 eligible actors within 1,000 m of giver; kill 5 there, not a remotely selected camp | 250 / computed |
| [scout_the_ridge - Scout the Area](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:192) | Goto | scout, pistoleer | Nearest eligible undiscovered camp/town 400-1,500 m from giver; reach within 50 m, report back | 70 / computed |
| [carry_word - Carry Word](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:206) | Talk | * | Talk to any same/allied/factionless NPC at least a randomly chosen 600-1,400 m from acceptance position | 65 / computed |
| [cave_check - Cave Check](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:219) | Goto | scout, elder | Nearest eligible undiscovered cave 200-1,200 m from giver; reach within 25 m, report back | 60 / computed |
| [Repairs - Repairs](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:233) | Fetch | * | 1 `Toolbox` available near giver | 75 / computed |
| [take_their_camp - Take the Camp](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:252) | Takeover | chieftain, elder, bodyguard | Nearest eligible enemy native camp 50-1,000 m away; owner not friendly, at war if giver has a war list; radius 120 m, defender count learned on approach | 400 / computed |
| [supply_run - Supply Run](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:265) | Deliver | merchant, elder, chieftain | Giver spawns 3 exact `=dogfood_can`; random friendly camp/town 400-4,000 m away; non-hostile recipient within 120 m | 200 / computed |
| [The Bad Batch 1 - Doggy Don't](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:289) | Fetch | merchant, elder | 3 exact `=dogfood_can`; first chain step; 72 game hours | 150 / 5 |
| [The Bad Batch 2 - Payback](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:308) | Takeover | merchant, elder | Requires completion of `The Bad Batch 1`; nearest eligible enemy native camp 300-4,000 m away, radius 120 m; 72 game hours | 400 / 5 |
| [The Bad Batch 3 - Food Run](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Embedded/Defaults/quests.txt:326) | Deliver | merchant, elder | Requires completion of `The Bad Batch 2`; 3 exact `=dogfood_can` to random friendly camp/town 400-5,000 m away; radius 120 m | 300 / 10 |

## Generated quests

Enabled by default, with all seven types allowed. These are templates, not a finite list of named quests. Chain steps take precedence. Otherwise authored quests get a 60% attempt when available; when still empty, allowed Takeover gets a 25% attempt, then allowed Deliver a 25% attempt. Remaining generation uses a pool with Fetch weight 2, Kill 2, discovery Goto 1, check-up Goto 1, Talk 1, Clear 1, omitting unavailable entries. These are sequential conditional chances, not unconditional percentages. [Selection](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1340), [general generator](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:1015).

Here `R5(v)=max(10,round(v/5)*5)` and distances are horizontal metres.

| Type / title | Availability and exact target choice | Count / base nuts |
|---|---|---|
| Fetch - `Fetch N <item>` | Weighted item table below; up to 12 draws to find an unreserved item with at least its minimum available nearby; exact prefab match | Random inclusive min-max capped at available count; R5(value*N*1.15) |
| Kill - `Hunt <plural name>` | Unreserved unprotected live actors within default 1,000 m giver range; pick the species/name of the nearest actor, excluding lizards | If C matching actors are alive, N is random ceil(C/2)..C; R5(KillValue*N*1.25) |
| Goto discovery - `Check out the <kind>` | Nearest unreserved undiscovered eligible camp, town, cave, building or wreck within default 150-1,800 m; excludes giver's own-faction locations; reach within 80 m | One location; R5(20+distance*0.05) |
| Goto check-up - `Check up on the <name>` | Nearest unreserved own-faction camp/town, at least max(150,Goto.MinDistance) and at most Goto.MaxDistance from giver; reach within 80 m | One location; max(5,round((20+distance*0.05)*0.4/5)*5); rep=1 |
| Talk - `Carry word` | No target NPC reserved; any same/allied/factionless NPC after travelling 600+random[0,1)*700 m from acceptance position | One conversation; R5(20+requiredDistance*0.045) |
| Clear - `Clear the area` | At least 3 unprotected live actors within 250 m of giver; area must not be reserved; counts any eligible actor, not a single selected species | Random N=3..min(eligibleCount,8); R5(N*25*1.25) |
| Takeover - `Take the <faction> camp` | Nearest unreserved enemy native camp within default 200-4,000 m; giver known, owner non-friendly and at war when giver has war entries; towns excluded | Defenders in 120 m radius, learned on approach; max(10,round((150+estimatedDefenders*50+distance*0.03)/5)*5), estimate defaults to 4 |
| Deliver - `Deliver N <goods>` | Random unreserved known friendly camp/town within default 400-3,000 m, never within 150 m of giver; no loaded-NPC requirement; goods table below, handed out by giver | Random inclusive goods min-max; R5(25+distance*0.06+value*N*0.5); recipient within default 120 m |

Goto has two variants of one enum type, hence eight rows but seven types. Takeover is a native-camp quest; enabling a town as a delivery/exploration destination does not implement city capture.

Takeover completion now changes the shared camp owner to Coyotes and requests a normal 2-5-member Coyote traveler journey from the nearest known uncleared Coyote camp. Arrival hands the survivors over as saved camp residents. The legacy nearby-spawn, teleport and automatic garrison-refill behavior is removed. See [takeover behavior and tests](quest-takeover-travelers.md).

## Delivery recovery and failure

The recipient does not have to exist when the offer is made. On arrival within max(5,min(30,radius*0.6)) m, a ready destination without a living talkable recipient becomes `Return N <item>` after 15 active-play seconds and a fresh People-cache scan. Unloaded/spawning camps and unbuilt towns wait; they do not count as empty. A built town uses the same observation flow. Known-cleared camps fail immediately. Return markers follow the original giver; returning all goods removes them and the quest without a reward or theft penalty. Missing/dead return recipients and return deadline expiry fail normally; the giver is not respawned for this stage. Keeping goods past the outward deadline normally costs 3 reputation. Normal quest failure costs max(1,round(RepGain*2)) by default; zero multiplier disables this penalty. [Recovery](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:713).

## Custom authored definitions

The parser additionally accepts arbitrary IDs using these seven types, `giver`, `target`, `count`, `reward`, `range`, `radius`, `distance`, `minutes`, `hours`, `rep`, `chain`, `after`, `checkup`, `return`, `once` and text fields. Any type can add `needpoi` (at least one allowed matching POI within NeedRange) and `needenemy` (at least NeedCount matching living enemies within NeedRange). NeedRange defaults to 500 m, NeedCount to 1. Optional Talk actions `quest.give:<id>`, `quest.complete:<id>` and `quest.abandon:<id>` can operate on authored IDs; scripted giving still obeys preparation, crew and acceptance checks. [Parser](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:390), [Talk actions](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Plugin.cs:1993).

## Exact generated Fetch item pool

Values, inclusive count ranges and relative selection weights below are copied from the code. Names are exact prefab tokens; generated jobs prefix them with `=`.

| Prefab / source | Display name | Value | Count min-max | Weight |
|---|---|---:|---|---:|
| [bandage_1](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:718) | Bandage | 16 | 2-5 | 3 |
| [first_aid_1](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:719) | First Aid | 23 | 2-4 | 3 |
| [sanity_pills](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:720) | Sanity Pills | 180 | 1-2 | 1 |
| [cigarette_bactrian](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:721) | Cigarette | 40 | 2-5 | 2 |
| [weed_bag](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:722) | Weed | 14 | 4-8 | 1 |
| [alcohol_canister](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:723) | Alcohol | 50 | 1-3 | 2 |
| [dogfood_can](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:725) | Dogfood | 50 | 2-4 | 3 |
| [meat_mutant_cooked](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:726) | Cooked Mutant meat | 3 | 4-8 | 2 |
| [meat_insect_cooked](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:727) | Cooked Insect meat | 4 | 4-8 | 2 |
| [Rat_cooked](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:728) | Cooked Rat | 5 | 3-6 | 2 |
| [plant_tobacco](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:729) | Tobacco Plant | 180 | 1-2 | 1 |
| [plant_weed](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:730) | Weed Plant | 210 | 1-1 | 1 |
| [Water_Can_Plastic](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:732) | Water Can | 90 | 1-2 | 3 |
| [Diesel_Can](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:733) | Diesel Can | 130 | 1-2 | 2 |
| [Gasoline_Can](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:734) | Gasoline Can | 200 | 1-2 | 2 |
| [MotorOil_Can_Small](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:735) | Motor Oil Can | 30 | 1-2 | 2 |
| [ammo_box_9mm](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:737) | Ammo 9mm | 32 | 1-3 | 3 |
| [ammo_box_22](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:738) | Ammo .22 | 23 | 1-3 | 3 |
| [ammo_box_12gauge](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:739) | Ammo 12 gauge | 56 | 1-2 | 2 |
| [ammo_box_556mm](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:740) | Ammo 5.56mm | 50 | 1-2 | 2 |
| [ammo_box_762mm](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:741) | Ammo 7.62mm | 53 | 1-2 | 2 |
| [ammo_box_3006](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:742) | Ammo .30-06 | 86 | 1-2 | 1 |
| [ammo_arrow](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:743) | Ammo Arrow | 40 | 1-3 | 2 |
| [Toolbox](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:745) | Toolbox | 45 | 1-2 | 3 |
| [Repairbox_Small](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:746) | Repairbox Small | 90 | 1-2 | 2 |
| [armorplate](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:747) | Armor Plate | 30 | 2-4 | 2 |
| [bedroll](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:748) | Bedroll | 35 | 1-1 | 2 |
| [binocular](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:749) | Binocular | 65 | 1-1 | 1 |
| [flashlight1](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:750) | Old Flashlight | 75 | 1-1 | 1 |
| [gasoline_stove](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:751) | Portable Gasoline Stove | 150 | 1-1 | 1 |
| [dew_collector_portable](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:752) | Portable Dew Collector | 120 | 1-1 | 1 |
| [bodyarmor_type_0](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:753) | Bodyarmor Type-0 | 150 | 1-1 | 1 |
| [backpack_small](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:754) | Small Backpack | 230 | 1-1 | 1 |
| [old_knife](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:756) | Old Knife | 20 | 1-2 | 2 |
| [machete](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:757) | Machete | 80 | 1-1 | 1 |
| [22_pipe_pistol](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:758) | Pipe Pistol (.22lr) | 100 | 1-1 | 1 |
| [22_pipe_revolver](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:759) | Pipe Revolver (.22lr) | 180 | 1-1 | 1 |
| [crossbow](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:760) | Crossbow | 140 | 1-1 | 1 |
| [slamfire_shotgun](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:761) | Slamfire Shotgun (20ga) | 260 | 1-1 | 1 |
| [radiator_small](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:763) | Small Radiator | 120 | 1-1 | 1 |
| [radiator_medium](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:764) | Medium Radiator | 180 | 1-1 | 1 |
| [radio_car](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:765) | Cassette Player | 65 | 1-1 | 1 |
| [cassette_80ssynthwave](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:766) | Cassette 80's Synthwave Mix | 110 | 1-1 | 1 |

## Exact generated Deliver goods pool

Values, inclusive count ranges and relative selection weights below are copied from the code. Names are exact prefab tokens; generated jobs prefix them with `=`.

| Prefab / source | Display name | Value | Count min-max | Weight |
|---|---|---:|---|---:|
| [dogfood_can](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:773) | Dogfood | 50 | 2-4 | 3 |
| [ammo_box_9mm](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:774) | Ammo 9mm | 32 | 1-3 | 3 |
| [ammo_box_22](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:775) | Ammo .22 | 23 | 2-4 | 2 |
| [ammo_box_12gauge](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:776) | Ammo 12 gauge | 56 | 1-2 | 2 |
| [Toolbox](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:777) | Toolbox | 45 | 1-2 | 3 |
| [Repairbox_Small](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:778) | Repairbox Small | 90 | 1-1 | 2 |
| [armorplate](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:779) | Armor Plate | 30 | 2-4 | 2 |
| [alcohol_canister](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:780) | Alcohol | 50 | 1-3 | 2 |
| [bedroll](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:781) | Bedroll | 35 | 1-2 | 1 |
| [binocular](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:782) | Binocular | 65 | 1-1 | 1 |
| [radio_car](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:783) | Cassette Player | 65 | 1-1 | 1 |

## Kill reward lookup, in matching order

This is a reward lookup, not a closed list of allowed enemies. Any eligible live name may be targeted, subject to faction/lizard rules. First normalized substring match wins; unmatched names use 30 nuts per kill before the 1.25 multiplier. The Foe min/max fields do not determine generated hunt counts; counts use the live species count.

| Name token / source | Display name | Base nuts per kill |
|---|---|---:|
| [scorpionking](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:912) | Scorpion Kings | 150 |
| [alphanightwalker](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:913) | Alpha Nightwalkers | 150 |
| [terrorofthenight](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:914) | Terrors of the Night | 150 |
| [professor](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:915) | Professors | 150 |
| [dukeironjaw](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:916) | Duke Ironjaws | 150 |
| [juggernaut](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:917) | Juggernauts | 120 |
| [nightwalker](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:918) | Nightwalkers | 60 |
| [skinwal](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:919) | Skinwals | 50 |
| [teacher](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:920) | Teachers | 50 |
| [lanky](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:921) | Lankies | 45 |
| [arachnid](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:922) | Arachnids | 45 |
| [burrower](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:923) | Burrowers | 40 |
| [scorpionsmall](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:924) | Scorpions | 15 |
| [scorpion](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:925) | Scorpions | 40 |
| [bolter](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:926) | Bolters | 35 |
| [grimhound](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:927) | Grimhounds | 30 |
| [hound](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:928) | Hounds | 20 |
| [pup](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:929) | Pups | 12 |
| [zombie](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:930) | Zombies | 20 |
| [spider](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:931) | Spiders | 8 |
| [bat](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:932) | Bats | 8 |
| [rat](/V:/Repos/Apocalypter/WildWasteland2/src/Modules/Quests/Quests.cs:933) | Rats | 6 |
