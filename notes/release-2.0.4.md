# Wild Wasteland 2.0.4

Gun-carrying NPCs now drop one matching ammo box, with a 25% chance of a second. Loot reads the selected gun even during a temporary melee switch, removes random ammo entries from that NPC's loot pool and uses native item naming/save registration for the matching boxes. Non-ammo loot remains random. No new configuration entries or save formats are introduced.

With Vistas World Gen 1.0.7 loaded, towns wait until Vista finishes its loaded-area road generation, painting and grading before choosing their sites. Town access roads are registered with Vista's native road network; Vista applies those connectors before WW prepares foundations and builds towns. Existing saved towns retain their positions. Vista's elevation-based population bonuses are suppressed so WW population budgets remain authoritative.

Town chance and starting-chunk exclusion retain their configured values. A particular starting area may contain no towns; the integration does not guarantee a nearby town.

Known compatibility limits: Vista's existing terrain seams remain, including the approximately 4.8068 m seed-666 corner discontinuity reproduced without WW. Future streaming or road-setting changes can still affect saved town surroundings; Vista's separate Add/Off cleanup writer and the existing bridge teardown error remain separate outstanding work. See `notes/compat-vistas-audit-2026-10-24.md` for the full audit.

Install the complete `WildWasteland` folder from the ZIP into `BepInEx/plugins/`. BepInEx 5 is required. Keep all included asset folders and `asset-index.tsv` beside the DLL. Disable standalone mods whose features WW2 absorbs, including Apocaplayer/Apocaclimber, NPCAI, Gunplay/Gunplay HUD, CleanHUD, Immersive Map, Apocapocket, Apocapatrol, War Rig and Stew Pot. Optional Vistas World Gen, Engine Sim and ChaseCamera remain separate and are not bundled.

WW2 requires fresh saves and settings when transitioning from WW1. Back up the previous installation, configuration and saves before transitioning.

Validation: the Vista integration completed three isolated programmatic new-world runs with fog-free native map rendering; two worlds built towns with native Vista connectors. Seed 666 retained the known seam, and the wider draft-border checks also remain failing. NPC ammo drops, saved-world regeneration, later streaming and actual vehicle travel along connectors remain subject to in-game acceptance. All new gameplay retains Testing status.

The Windows 2.0.4 Release build/deployment passed with zero warnings and errors. Complete package CRC and byte-for-byte verification passed: 597 entries, including 593 runtime assets, the DLL, icon and README.

DLL MD5: `7D05C53DF70094E3E4C7B054F2483567`

Archive: `WildWasteland-2.0.4.zip` (228,881,840 bytes)

Archive SHA256: `52d96d7145520e3661711e8f1cb137c10b17203b7ff5334e6a6b07cbeac506e1`
