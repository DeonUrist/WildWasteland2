# Quest catalog — current native implementation

Snapshot: 2026-10-18. **16 authored quests: 13 repeatable jobs and 3 once-per-save chain steps.** Generated quests are unlimited instances of seven types, with exploration/check-up variants listed separately below.

The installed `BepInEx/plugins/WildWasteland/Quests/quests.txt`, disabled legacy file and embedded default are byte-identical (SHA256 `b286957e0bec40ad07f127464251f0b866d8af5ef1e4e41cc37138adc80c81b1`). This catalog describes current code and installed tuning, not just the older header comments. No quest behavior was changed while preparing it.

## Authored quests

All distances below are horizontal. Rewards are base nuts before reputation adjustment; reputation is gained with the giver's faction if it has one. A giver list matches NPC name parts, not individual named characters. “Any” still means an eligible friendly person under the normal offer rules.

| ID | Title | Givers | Type | Objective and availability | Base nuts | Rep | Repeat |
|---|---|---|---|---|---:|---:|---|
| `field_medic` | Field Medic | Any eligible NPC | fetch | Return 3 bandage uses/units. At least 3 matching units must exist within 4 km when offered. | 55 | +1 | Repeatable |
| `dry_throats` | Dry Throats | Any eligible NPC | fetch | Return 1 plastic water can, at least half full. At least 1 qualifying can must exist within 4 km. | 100 | +2 | Repeatable |
| `rations_run` | Rations Run | merchant, scraper, lugger | fetch | Return 3 dogfood cans, exact dogfood prefab. At least 3 must exist within 4 km. | 170 | +3 | Repeatable |
| `fuel_shortage` | Fuel Shortage | lugger, merchant, pistoleer | fetch | Return 1 gasoline can, at least half full. At least 1 qualifying can must exist within 4 km. | 250 | +2 | Repeatable |
| `hound_trouble` | Hound Trouble | Any eligible NPC | kill | Kill 3 Grimhounds within 1 km of the giver, then report back. At least 3 eligible live matches must exist there when offered. | 150 | +2 | Repeatable |
| `night_terror` | Night Terror | bodyguard, chieftain, scout | kill | Kill 2 Nightwalkers within 1 km of the giver, then report back. At least 2 eligible live matches must exist there when offered. | 200 | +2 | Repeatable |
| `clear_the_camp` | Patrol Camp | chieftain, elder, bodyguard | clear | Kill 5 eligible threats within 1 km of the giver, then report back. Requires at least 5 eligible live threats in that area. | 250 | +2 | Repeatable |
| `scout_the_ridge` | Scout the Area | scout, pistoleer | goto | Visit an eligible undiscovered camp 400–1,500 m from the giver; reach within 50 m, then report back. | 70 | +1 | Repeatable |
| `carry_word` | Carry Word | Any eligible NPC | talk | Talk to a friendly eligible NPC after travelling at least a randomly selected 600–1,400 m from the acceptance position. Paid there; no return. | 65 | +1 | Repeatable |
| `cave_check` | Cave Check | scout, elder | goto | Visit an eligible undiscovered cave 200–1,200 m from the giver; reach within 25 m, then report back. No creature-identification objective. | 60 | +1 | Repeatable |
| `Repairs` | Repairs | Any eligible NPC | fetch | Return 1 Toolbox. At least 1 matching toolbox must exist within 4 km. | 75 | +1 | Repeatable |
| `take_their_camp` | Take the Camp | chieftain, elder, bodyguard | takeover | Take a qualifying opposing-faction camp 50–1,000 m away. Defender kills count within 120 m; headcount is determined from residents. Ownership changes, then report back. | 400 | +4 | Repeatable |
| `supply_run` | Supply Run | merchant, elder, chieftain | deliver | Receive 3 exact dogfood cans from the giver; deliver them to a friendly camp 400–4,000 m away. Talk to a non-hostile recipient within 120 m of its centre. | 200 | +2 | Repeatable |
| `The Bad Batch 1` | The Bad Batch (1/3): Doggy Don't | merchant, elder | fetch | Return 3 exact dogfood cans; at least 3 must exist within 4 km. First chain step, no prerequisite. | 150 | +5 | Once per save |
| `The Bad Batch 2` | The Bad Batch (2/3): Payback | merchant, elder | takeover | Requires completed The Bad Batch 1. Take a qualifying opposing-faction camp 300–4,000 m away; 120 m defender radius, then report back. | 400 | +5 | Once per save |
| `The Bad Batch 3` | The Bad Batch (3/3): Food Run | merchant, elder | deliver | Requires completed The Bad Batch 2. Receive/deliver 3 exact dogfood cans to a friendly camp 400–5,000 m away; recipient within 120 m. Paid there. | 300 | +10 | Once per save |

## Conditions shared by normal work offers

- Friendly living NPC; the giver's faction must not be configured hostile. Scrapyard is hostile and at war with Coyotes by default. Low reputation alone does not bar work under the current default HostileAtRep=0.
- Current/former vehicle crew never offer new quests. Seated crew also cannot be hired; former crew on foot may be hired. Existing turn-ins remain available.
- Maximum **5 active quests**. A giver will not issue another job while you carry its non-talk job. Accepted or declined offers put that NPC on a **24 in-game hour cooldown**; completion/failure can reopen it through the source rules.
- The same authored ID cannot be active twice. Completed once-only/chain IDs are not offered again in that save; failures do not count as completion.
- Nearby NPCs suppress recently offered duplicate subjects/IDs within **400 m for 12 in-game hours**. Chain offers additionally reserve the step to their offering NPC for **24 in-game hours**, including distant NPCs; that reservation is session-local.
- Valid chain steps take priority over ordinary jobs. Otherwise, with generation enabled, there is a **60% attempt** to select an authored job when eligible definitions exist. Failed authored candidates can fall back to generation; this is not a guaranteed 60/40 final split.
- Goto/clear/kill/fetch objectives must be feasible under the type-specific checks. Talk jobs do not require a known loaded recipient when offered. Takeover/delivery require a faction and an eligible real camp. Active targets and recently offered target locations are excluded where applicable.

### Fetch

Offer check: enough qualifying units exist within **4,000 m of the giver** in the loaded world. Merchant stock does not count. Hand-in: matching held/quick-slot items plus ground items within **10 m of the player**, with carried-item counting currently enabled. Stack uses count individually. Liquid containers must be **at least 50% full**. Items are consumed on turn-in. A leading `=` in a target is an exact prefab match; unprefixed matching also considers display names/categories. Defaults are preserved as written: Field Medic, Dry Throats, Fuel Shortage and Repairs do not use `=`.

### Kills and area patrols

Kill jobs use **1,000 m from the giver** unless a quest has an explicit range. A counted death must also be within **300 m of the player**. Clear/patrol jobs use their own area radius. Allies/the giver's own faction and neutral friendly people are protected; hostile/at-war people and eligible animals/mutants can count. Generated hunts exclude lizards. Current code observes qualifying Health/spawn deaths; it does not test that the player personally dealt the killing blow.

Kill jobs can gain **10% extra pay per matching corpse brought within 35 m of the giver**, capped at the requested kill count; this applies to Kill, not Clear/Takeover jobs.

### Exploration and messages

Exploration requires an eligible real POI in the distance band. Ordinary scouting excludes discovered places and the giver's own camps; foreign non-allied ownership also excludes the POI. Discovery is tested against native visited icons or saved map exploration. Reaching the quest radius completes its objective; returning to the original giver earns the reward.

Carry Word chooses a minimum travel distance when offered. Opening conversation beyond that distance from the **acceptance position** completes it with a matching NPC of the giver's faction, an ally or no faction. It does not spawn a named recipient, require a particular camp, or require a return visit.

### Takeovers

The camp must have a known opposing owner; if the giver has a war list, the owner must be at war with it. Already-targeted/recently offered camps are excluded and the nearest qualifying camp is selected. Kill the target faction's defenders within **120 m**. The two authored takeover quests omit count, so use discovered headcount; unknown headcounts become visible within the **250 m scout range**. A near, empty camp is accepted as cleared after the existing **15-second empty-camp check**. Ownership changes and a winning-faction group is sent; reward still requires reporting back.

### Deliveries

Pick a different friendly/ally-owned camp in the quest's range. A loaded camp with no friendly eligible residents is rejected; a distant camp with nobody loaded can qualify provisionally. Acceptance spawns goods in front of the giver, starts the clock and sets the actual delivery count to the number successfully spawned. Hand-in consumes the items and pays at the destination, with no return required. Generic delivery completion accepts a non-hostile interlocutor inside the destination radius; the separate recovery scan also tests faction friendliness.

At a ready empty destination, a fresh people scan and **15 seconds of active play near arrival** convert the quest to returning its goods. Unknown/unloaded/spawning camps do not count as empty. Cleared destinations and failed returns use normal failure. Returning goods to the original giver normally cancels without theft treatment; abandoning/expiring a regular delivery treats the goods as stolen. Recovery returns bypass theft treatment and can incur normal failure.

## Deadlines, rewards and failures

Current installed tuning: RewardMultiplier=1, minimum 24 hours, extra 4 hours/km, default maximum 96 hours. All times below are **game time**; sleeping advances them, pause stops them. Deadlines start on acceptance, continue through the return/reporting stage and persist in saves.

| Jobs | Time allowed |
|---|---|
| Fetch jobs; Patrol Camp | 24 in-game hours |
| Hound Trouble; Night Terror | 28 hours under current 1 km Kill QuestRange |
| Scout the Area | 24 h + 4 h/km to the chosen camp: 25.6–30 h |
| Cave Check | 24 h + 4 h/km to the chosen cave: 24.8–28.8 h |
| Carry Word | 24 h + 4 h/km of its rolled minimum travel distance: 26.4–29.6 h |
| Take the Camp | 24 h + 4 h/km: 24.2–28 h |
| Supply Run | 24 h + 4 h/km to the destination: 25.6–40 h |
| The Bad Batch 1 and 2 | Explicit 72 hours each |
| The Bad Batch 3 | Delivery clock, 24 h + 4 h/km: 25.6–44 h |

Non-delivery computed limits are capped at 96 h; an explicit `hours` value can exceed that cap but not go below the configured minimum. Deliveries use their own base/per-km clock; an explicit `minutes` overrides it.

Pay is adjusted at offer time by the global multiplier and giver-faction reputation: currently **+/-0.5% per reputation point**, minimum 10% of base pay, no configured upper multiplier. Automatic reputation is round(sqrt(base reward / 60)), +1 for a takeover, clamped 1–6; explicit `rep` overrides it. At RewardMultiplier=1, the authored table shows the resulting reputation values. Corpses can add the Kill bonus at turn-in.

Normal failure or abandonment costs **2 × the quest's completion reputation** (minimum 1), currently. A normal giver-death cancellation has no penalty; recovery-return failure is an exception. A regular stolen/expired delivery costs **3 reputation**, separate from normal failure. Reputation is clamped to -100..100.

## The Bad Batch chain

1. **Doggy Don't** (`The Bad Batch 1`): merchant/elder, 3 dogfood, 150 nuts, +5 reputation, 72 h. No prerequisite.
2. **Payback** (`The Bad Batch 2`): merchant/elder, opposing camp 300–4,000 m, 400 nuts, +5 reputation, 72 h. Requires completion of step 1.
3. **Food Run** (`The Bad Batch 3`): merchant/elder, deliver 3 dogfood to a friendly camp 400–5,000 m, 300 nuts, +10 reputation, delivery clock. Requires completion of step 2.

All three are once per save and take precedence when eligible. They are not bound to one particular merchant, camp or enemy trader. Step 2 picks a qualifying real opposing camp; the story's trader is not a separately tracked character. Step 3 picks a friendly destination, not necessarily the camp from step 2.

`quest_chain_template.txt` is an authoring template, not loaded quest content. `quest_chain.talk.txt.example` is an optional Talk example, not another chain or extra quests. Its prompts gate the three existing IDs by quest flags. Scripted `quest.give` actions use their dialogue conditions and a separate path: they do not reapply every normal offer check such as giver matching, chain prerequisites or cooldown. `quest.complete` directly pays/completes an active ID; script authors must gate it appropriately. No Talk installation is required for the normal chain offers.

## Generated quest families

All are repeatable, use generated IDs and obey normal offer gates. The pools use current source data, not every item in the game.

| Family / generated title | Availability and completion | Base reward before modifiers |
|---|---|---|
| Fetch N [item] | Weighted item from the fetch pool below. Loaded qualifying stock in 4 km must reach the item's minimum; requested count rolls within its min/max and is capped by availability. Return items to giver. | nearest 5 nuts of fair value × count × 1.15; minimum 10 |
| Hunt [enemy] | Nearest unblocked eligible live enemy kind within 1 km. Request a random count from ceil(available of that exact name / 2) to all available. Kill within area, then report. | nearest 5 of per-kill value × count × 1.25; minimum 10 |
| Check out the [POI kind] | Nearest eligible undiscovered camp/cave/building/wreck, normally 150–1,800 m. Reach within 80 m and report. | nearest 5 of 20 + 0.05 × distance(m); minimum 10 |
| Check up on the [camp name] | Nearest eligible camp belonging to giver's faction, normally 150–1,800 m. It may already be known. Reach within 80 m and report. | nearest 5 of (20 + 0.05 × distance(m)) × 0.4; minimum 5; +1 rep |
| Carry word | Minimum travel distance rolls 600–less than 1,300 m; talk to a friendly matching recipient beyond it. No known recipient needed to offer, no return. | nearest 5 of 20 + 0.045 × minimum distance(m); minimum 10 |
| Clear the area | At least 3 eligible threats within 250 m. Request 3..min(available,8) kills there, then report. | nearest 5 of count × 25 × 1.25; minimum 10 |
| Take the [faction] camp | Eligible opposing camp normally 200–4,000 m, defender radius 120 m; dynamic headcount and report-back as above. | nearest 5 of 150 + 50 × estimated count + 0.03 × distance(m); estimate 4 if count unknown |
| Deliver N [goods] | Weighted goods from the delivery pool below. Friendly camp normally 400–3,000 m. Goods supplied on acceptance; recipient within 120 m, delivery clock, no return. | nearest 5 of 25 + 0.06 × distance(m) + 0.5 × goods value × count; minimum 10 |

Selection is conditional, not eight equal chances. After authored selection fails/is skipped, takeover gets a 25% attempt, then delivery gets a 25% attempt if still no quest. The remaining pool has Fetch and Kill twice each, eligible Goto/check-up slots, Talk once and Clear once. These passes depend on enabled types, actual world candidates and duplicate restrictions.

## Complete generated fetch pool

Count means counted units, including uses for applicable stacks. Weight is relative selection frequency: 3 common, 2 medium, 1 rare. Every generated fetch target uses exact-prefab matching.

| Item | Prefab | Count range | Fair value/unit | Weight |
|---|---|---:|---:|---:|
| Bandage | `bandage_1` | 2–5 | 16 | 3 |
| First Aid | `first_aid_1` | 2–4 | 23 | 3 |
| Sanity Pills | `sanity_pills` | 1–2 | 180 | 1 |
| Cigarette | `cigarette_bactrian` | 2–5 | 40 | 2 |
| Weed | `weed_bag` | 4–8 | 14 | 1 |
| Alcohol | `alcohol_canister` | 1–3 | 50 | 2 |
| Dogfood | `dogfood_can` | 2–4 | 50 | 3 |
| Cooked Mutant meat | `meat_mutant_cooked` | 4–8 | 3 | 2 |
| Cooked Insect meat | `meat_insect_cooked` | 4–8 | 4 | 2 |
| Cooked Rat | `Rat_cooked` | 3–6 | 5 | 2 |
| Tobacco Plant | `plant_tobacco` | 1–2 | 180 | 1 |
| Weed Plant | `plant_weed` | 1–1 | 210 | 1 |
| Water Can | `Water_Can_Plastic` | 1–2 | 90 | 3 |
| Diesel Can | `Diesel_Can` | 1–2 | 130 | 2 |
| Gasoline Can | `Gasoline_Can` | 1–2 | 200 | 2 |
| Motor Oil Can | `MotorOil_Can_Small` | 1–2 | 30 | 2 |
| Ammo 9mm | `ammo_box_9mm` | 1–3 | 32 | 3 |
| Ammo .22 | `ammo_box_22` | 1–3 | 23 | 3 |
| Ammo 12 gauge | `ammo_box_12gauge` | 1–2 | 56 | 2 |
| Ammo 5.56mm | `ammo_box_556mm` | 1–2 | 50 | 2 |
| Ammo 7.62mm | `ammo_box_762mm` | 1–2 | 53 | 2 |
| Ammo .30-06 | `ammo_box_3006` | 1–2 | 86 | 1 |
| Ammo Arrow | `ammo_arrow` | 1–3 | 40 | 2 |
| Toolbox | `Toolbox` | 1–2 | 45 | 3 |
| Repairbox Small | `Repairbox_Small` | 1–2 | 90 | 2 |
| Armor Plate | `armorplate` | 2–4 | 30 | 2 |
| Bedroll | `bedroll` | 1–1 | 35 | 2 |
| Binocular | `binocular` | 1–1 | 65 | 1 |
| Old Flashlight | `flashlight1` | 1–1 | 75 | 1 |
| Portable Gasoline Stove | `gasoline_stove` | 1–1 | 150 | 1 |
| Portable Dew Collector | `dew_collector_portable` | 1–1 | 120 | 1 |
| Bodyarmor Type-0 | `bodyarmor_type_0` | 1–1 | 150 | 1 |
| Small Backpack | `backpack_small` | 1–1 | 230 | 1 |
| Old Knife | `old_knife` | 1–2 | 20 | 2 |
| Machete | `machete` | 1–1 | 80 | 1 |
| Pipe Pistol (.22lr) | `22_pipe_pistol` | 1–1 | 100 | 1 |
| Pipe Revolver (.22lr) | `22_pipe_revolver` | 1–1 | 180 | 1 |
| Crossbow | `crossbow` | 1–1 | 140 | 1 |
| Slamfire Shotgun (20ga) | `slamfire_shotgun` | 1–1 | 260 | 1 |
| Small Radiator | `radiator_small` | 1–1 | 120 | 1 |
| Medium Radiator | `radiator_medium` | 1–1 | 180 | 1 |
| Cassette Player | `radio_car` | 1–1 | 65 | 1 |
| Cassette 80's Synthwave Mix | `cassette_80ssynthwave` | 1–1 | 110 | 1 |

## Complete generated delivery pool

| Goods | Prefab | Count range | Fair value/unit | Weight |
|---|---|---:|---:|---:|
| Dogfood | `dogfood_can` | 2–4 | 50 | 3 |
| Ammo 9mm | `ammo_box_9mm` | 1–3 | 32 | 3 |
| Ammo .22 | `ammo_box_22` | 2–4 | 23 | 2 |
| Ammo 12 gauge | `ammo_box_12gauge` | 1–2 | 56 | 2 |
| Toolbox | `Toolbox` | 1–2 | 45 | 3 |
| Repairbox Small | `Repairbox_Small` | 1–1 | 90 | 2 |
| Armor Plate | `armorplate` | 2–4 | 30 | 2 |
| Alcohol | `alcohol_canister` | 1–3 | 50 | 2 |
| Bedroll | `bedroll` | 1–2 | 35 | 1 |
| Binocular | `binocular` | 1–1 | 65 | 1 |
| Cassette Player | `radio_car` | 1–1 | 65 | 1 |

## Generated hunt pricing table

This is a price lookup, not an exclusive target whitelist. The nearest eligible live enemy's full name is the generated target. Other names use 30 nuts per kill before the 1.25 multiplier. Tokens match in the listed order; specific bosses/small scorpions precede broader substrings. The Foe table contains min/max fields, but current generated hunt count uses actual population instead of those fields.

| Name token | Display label | Base value/kill |
|---|---|---:|
| `scorpionking` | Scorpion Kings | 150 |
| `alphanightwalker` | Alpha Nightwalkers | 150 |
| `terrorofthenight` | Terrors of the Night | 150 |
| `professor` | Professors | 150 |
| `dukeironjaw` | Duke Ironjaws | 150 |
| `juggernaut` | Juggernauts | 120 |
| `nightwalker` | Nightwalkers | 60 |
| `skinwal` | Skinwals | 50 |
| `teacher` | Teachers | 50 |
| `lanky` | Lankies | 45 |
| `arachnid` | Arachnids | 45 |
| `burrower` | Burrowers | 40 |
| `scorpionsmall` | Scorpions | 15 |
| `scorpion` | Scorpions | 40 |
| `bolter` | Bolters | 35 |
| `grimhound` | Grimhounds | 30 |
| `hound` | Hounds | 20 |
| `pup` | Pups | 12 |
| `zombie` | Zombies | 20 |
| `spider` | Spiders | 8 |
| `bat` | Bats | 8 |
| `rat` | Rats | 6 |

## Authoring conditions available for new quests

| Field | Meaning |
|---|---|
| `giver` | `*` or comma-separated NPC name patterns |
| `target`, `label`, `count` | Type-specific item/enemy/POI/NPC target, player text and quantity |
| `distance` | POI/camp distance band; for Talk, rolled minimum travel distance |
| `range` | Kill radius around giver; 0 means unrestricted giver area |
| `radius` | Arrival, clear, defender or delivery-recipient radius |
| `needpoi` | Require eligible POI kind within `needrange` of giver |
| `needenemy`, `needcount` | Require at least this many matching live enemies within `needrange` |
| `needrange` | Extra precondition radius, default 500 m, minimum 10 m |
| `checkup` | Goto on giver-faction camp instead of undiscovered place |
| `chain`, `after` | Chain label; comma-separated completed quest IDs all required in normal offers |
| `once` | One completion per save (default yes); chain/after forces yes |
| `return` | Whether to report back, subject to parser overrides: Fetch/Kill/Takeover always yes, Talk/Deliver always no |
| `hours`, `minutes` | Non-delivery deadline / delivery deadline override |
| `reward`, `rep` | Base nuts and explicit completion reputation |
| `offer`, `accept`, `remind`, `done` | Quest dialogue; supports source placeholders |

None of the 16 bundled quests sets `needpoi`, `needenemy`, `needcount` or `checkup`; their environmental gates come from their types. There are no general quest-file gates for player level, equipment, named-item ownership, time of day or minimum reputation. Additional story gates can be authored through optional Talk conditions, or require engine work.

## Design points to revisit

- **Cave Check** says something is nesting in a cave and asks what it is; implementation checks arrival only.
- **Scout the Area** cannot normally target an opposing-faction camp because ordinary Goto applies friendly/unowned POI rules, despite the scouting theme.
- **Patrol Camp** is five eligible kills within 1 km, not elimination of every threat or a patrol route.
- **Carry Word** has no fixed named addressee and no physical message item.
- **The Bad Batch** has story continuity in text/prerequisite IDs, while world locations and the trader remain generic selections.
- Some target names use broad substring/category matching. For example `nightwalker` can also match names containing that token; exact-prefab `=` is only used on selected item quests.
- Older header comments say kill range 4 km and simple failure -1 rep. Current installed code/config use 1 km and 2 × completion reputation. The catalog follows code/config.
- The ordinary authored selection branch can be skipped by random choice even when a suitable quest exists. This affects perceived availability.

## Source locations

- Definitions and authoring header: `src/Modules/Quests/Embedded/Defaults/quests.txt` (installed copy is authoritative on a running installation).
- Defaults/config: `src/Modules/Quests/QuestConfig.cs`; installed `BepInEx/config/WildWasteland/{settings.cfg,tools.cfg}`.
- Parser, generated pools, prices and selection: `src/Modules/Quests/Quests.cs`, QuestFile / QuestGen.
- Offer gates, deadlines, turn-in/death checks and recovery: `src/Modules/Quests/Plugin.cs`, PluginRuntime.cs and NativeConversation.cs.
- Camp selection/headcount/ownership: `src/Modules/Quests/Takeover.cs`; faction protection/POI gates: Factions.cs.
