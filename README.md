# Wild Wasteland 2

Wild Wasteland is a modular single-player overhaul for Apocalypter and BepInEx 5. Version 2.0 consolidates body animation, NPC behavior, weapons, vehicle crews, towns, quests, weather and interface features into 15 modules with shared typed integrations.

Install the complete `WildWasteland2` folder from the release ZIP into `BepInEx/plugins/`. Keep all included asset folders and `asset-index.tsv` beside `WildWasteland.dll`.

Apocasetter lists **Wild Wasteland 2** separately from Wild Wasteland and Wild Wasteland MP. Use only one variant at a time. WW2 2.0.6 uses plugin ID `com.denis.apocalypter.wildwasteland2`; the index installer disables the other variants when switching. For a manual update from WW2 2.0.5 or earlier, move the existing `plugins/WildWasteland` folder outside `plugins` first, install the complete new folder and copy any customized assets into matching paths. Keep existing WW2 saves and `config/WildWasteland` settings; the F3/Debug facade now uses `com.denis.apocalypter.wildwasteland2.cfg`.

Disable standalone mods whose features WW2 absorbs, including Apocaplayer/Apocaclimber, NPCAI, Gunplay/Gunplay HUD, CleanHUD, Immersive Map, Apocapocket, Apocapatrol, War Rig and Stew Pot. Optional Vistas World Gen, Engine Sim and ChaseCamera remain separate and are not bundled.

Transitioning from WW1 requires fresh saves and settings. Back up the previous installation, configuration and saves first. Updates within WW2 preserve existing settings; install the complete release asset folder.

See [2.0.7 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.7) for the crawling hand visibility fix. The [2.0.6 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.6) cover the separate catalog identity and save-button explanations. Finished perk artwork is included. The [2.0.4 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.4) cover the Vista town-road integration, matching NPC ammo drops and known compatibility limits.
