# Wild Wasteland 2 — 2.0.9

Boss encounters now retain their native arena-entry triggers. Interior population preparation previously included boss spawners, so approaching another interior group at the same POI could spawn the boss and activate its nearby health display and music before the player reached the arena.

Actual boss spawners now bypass automatic arrival pre-spawning, spawn-event deferral and population suspension. Ordinary interior enemies and boss bodyguards keep the existing paced pre-spawn and hearing rules. Native boss spawning, arena doors and progression logic retain ownership of the encounter. No configuration entries or save formats change.

Bosses already spawned in an existing save remain present. Use a save from before the boss spawned, or an unvisited boss POI, to check the corrected entry behavior.

Install the complete `WildWasteland2` folder from the ZIP into `BepInEx/plugins/`, retaining all included assets and `asset-index.tsv`. BepInEx 5 is required. Existing WW2 saves and settings remain supported. For manual updates from WW2 2.0.5 or earlier, follow the folder-transition instructions in the README and [2.0.6 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.6). Use only one Wild Wasteland variant and disable absorbed standalone mods as described in the README.

In-game acceptance remains **Testing**. Approach an undefeated boss POI without entering its arena: ordinary interior enemies and bodyguards should populate, while boss music and the health display remain absent. Enter the native arena trigger and expect the boss to spawn and its encounter to activate normally. At Debug logging level 1, `boss spawn left to native arena trigger: EnemySpawn_...Boss` confirms the spawner exclusion; ordinary groups retain `location population queued on arrival:`.

Validation: Windows Release build/deploy passed with zero warnings and errors. Complete-package CRC and byte-for-byte verification passed: 597 entries, including 593 runtime assets, the DLL, icon and README.

Installed/package DLL MD5: `6FF6D2BB293843D783A16A16BE7385FC`

Archive: `WildWasteland2-2.0.9.zip` (230,155,354 bytes)

Archive SHA256: `6873fb01c5a5c805b5870892fa5df615c95c8820cd9977d9ad82ed3ecf8466dc`
