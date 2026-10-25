# Wild Wasteland 2 — 2.0.8

Recoverable save problems now offer **SAVE (ISSUES PRESENT)** so players can attempt to preserve current gameplay without reloading first. The save library remains accessible when interrupted-save recovery needs attention. A save with issues creates a separate manual snapshot, keeps existing saves and backups, and skips retention pruning for that attempt.

When an optional module cannot supply an updated record, manual saving retains its previous stored data where available. Missing records are restored from retained pre-save records, and completed location-history captures survive another capture failing. Recovery saves can therefore contain older or unavailable data for an affected module; this is a best-effort recovery path, not a guarantee that every subsystem's latest progress can be reconstructed. Native serialization, required world records, transaction stamps and disk readback must still pass before a save is reported as completed. Timed autosaves retain strict admission and verification.

Failed coroutine starts release pending flags. Abort attempts native cancellation, registry restoration and disk rollback independently, while retaining the live cache. Optional cleanup is suspended for the session if it fails. A successful verified save clears the reload-required latch. Loading a selected snapshot recovers only that slot's journals, allowing unrelated damaged journals to remain intact for support.

`BepInEx/LogOutput.log` records the causes, exception/call stacks, version, slot, generation, save phase and native/pending state even with optional debug logging off. Look for `Save issue #`, `Save recovery completed:` and `Loaded save with issues:`. The snapshot also embeds the report available at publication. No configuration entries change; existing native and module records keep their formats, with an additive diagnostic record.

Install the complete `WildWasteland2` folder from the ZIP into `BepInEx/plugins/`, retaining all included assets and `asset-index.tsv`. BepInEx 5 is required. Existing WW2 saves and settings remain supported. For manual updates from WW2 2.0.5 or earlier, follow the folder-transition instructions in the README and [2.0.6 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.6). Use only one Wild Wasteland variant and disable absorbed standalone mods as described in the README.

In-game acceptance remains **Testing**. On an affected world, open SAVE GAME, fill both names and use SAVE (ISSUES PRESENT). Expect a separate snapshot and a return to gameplay. Load the new snapshot, verify player position, inventory and world/module progress, then save again. Check the full log for the affected records and recovery outcome. Also check an ordinary manual save, overwrite and timed autosave on an unaffected world. Do not deliberately damage a player's saves to reproduce a failure.

Validation: Windows Release build/deploy passed with zero warnings and errors. Complete-package CRC and byte-for-byte verification passed: 597 entries, including 593 runtime assets, the DLL, icon and README.

Installed/package DLL MD5: `65430596DDB32ED2FCBDF6888C088E0D`

Archive: `WildWasteland2-2.0.8.zip` (230,155,088 bytes)

Archive SHA256: `4b28e79d8facb0256718c43e1ef155057a1897ad446f4c9fd45d84a2bb8ba060`
