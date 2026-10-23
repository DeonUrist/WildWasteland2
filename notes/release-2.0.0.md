# Wild Wasteland 2.0.0

The first WW2 release consolidates the Apocalypter overhaul into 16 modules with shared character presentation, NPC navigation, weapons, vehicles, towns, quests, survival, skills and interface features. Persistent world records, save descriptions and thumbnails live inside ES3 saves. Apocasaver and legacy save imports are no longer used.

This release includes all pending work through 24 October 2026:

- Town exterior navigation connects open ground and building entrances while retaining authored building maps. Loaded buildings regain town ownership, and preparation completes through the existing scheduler without the former fixed cutoff.
- Gas stations use the normal POI discovery registry: approaching within 100 metres reveals their icon in Immersive/Ambiguous mode, and discoveries are saved.
- Berserk draws a carried weapon, adds periodic native attacks and automatic reloads, and retains manual attacks and weapon dropping. It includes character-specific battle cries, a red tint and temporary Coyote hostility. Coyotes retain their normal appearance, and actual player attacks retain normal faction consequences.
- Delirium adds a shifting sky, two orbiting extra suns and 24 black silhouettes visible through the affected player's camera.
- Doppelganger presents a local visual copy of the player's body and equipment that walks alongside and periodically appears behind them.
- The mental-break debug selector supports all five events. Perk choices show full descriptions with text sized to fit their cards.

Install the complete `WildWasteland` folder from the ZIP into `BepInEx/plugins/`. BepInEx 5 is required. The DLL depends on the included asset folders and `asset-index.tsv`; the archive contains the animation bundles, audio, artwork, navigation data and authored defaults.

WW2 requires fresh saves and settings when transitioning from WW1. Back up the previous plugin folder, configuration and saves before installing. Replace the old Wild Wasteland installation and disable standalone mods whose features WW2 absorbs, including Apocaplayer/Apocaclimber, NPCAI, Gunplay/Gunplay HUD, CleanHUD, Immersive Map, Apocapocket, Apocapatrol, War Rig and Stew Pot. Optional compatible mods remain separate installations; Engine Sim and ChaseCamera are not bundled.

Validation is a Windows Release build and package integrity verification. In-game behavior, visuals and performance remain subject to Denis's acceptance; publishing does not mark the pending changes Verified. No new configuration entries were added by the changes listed above.
