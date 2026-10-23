# Wild Wasteland 2.0 modules

The registry contains the following 16 modules in initialization order. Each owns its source folder and uses `WildWasteland.Modules.<Id>`; Core owns shared scheduling, lifecycle, ModAPI, events, settings infrastructure and persistence dispatch. Modules exchange typed Core contracts rather than private implementation types.

| Id | Responsibility |
|---|---|
| Debug | Diagnostics and profiling |
| Compatibility | Optional standalone plugin adapters |
| Saves | Native ES3 transactions and save management |
| Perks | Twelve player skills (0–100), saved linear XP/action effects, 36 automatic skill perks at 25/50/75/100, ten permanent insanity perks, typed ModAPI and post-episode three-perk/Persevere choices, rising unlock notices and saved merchant rerolls; Crafting has no effect and Medicine retains its existing healing bonus |
| NPCAI | Perception, tactics, navigation and creatures |
| Presentation | Player and NPC body, animation and traversal visuals |
| Gunplay | Weapons, projectiles, damage and ammunition |
| HUD | Interface, inventory, maps, menus and loading presentation |
| Vehicles | Vehicle mechanics, attachments and War Rig |
| Loot | Item catalogs, cargo and loot |
| Patrols | Journeys, traffic, crews and convoy behavior |
| POI | Locations, towns, authored layouts and population |
| Quests | Quest state, objectives, followers and journal data |
| Survival | Weather, storms, food/butchering, sleep recovery and 30-second Vertigo/No More Death events; insanity freezes, random events grant no perk, reward choices reset to 0 (35 for Persevere) |
| Tools | Player-facing authoring tools |
| Developer | Developer-only authoring tools |

Presentation supplies the shared character driver; native player input and NPC plans feed that driver. Compatibility owns discovery of external plugins and publishes typed data to their consumers. Optional third-party DLLs are not bundled.

Persistent world records, save descriptions and thumbnails are stored inside the selected ES3 transaction (D20). There is no Apocasaver dependency or old-save import. Authored templates/layouts, settings, derived caches and recovery journals remain external. WW2 uses fresh settings and saves (D12).

Runtime payloads live beside the DLL at `<Id>/<relative path>`, with shared artwork under `Core/Theme`. The build generates `asset-index.tsv`; the package tool includes only its indexed files. Default installation copies missing assets; customized files require a deliberate clean setup to reset.

Source integration is complete; Denis owns gameplay acceptance. See [Wave C handover](refactor/reports/C.md), [refactor decisions](refactor/README.md), and [historical source provenance](../notes/modules-1.x-history.md).
