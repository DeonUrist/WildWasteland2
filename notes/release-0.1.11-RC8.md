# Wild Wasteland 0.1.11 RC8

Numeric plugin version 0.1.11; tag v0.1.11. Status: Testing. This release includes all pending local source, asset and documentation changes.

## Changes

- Women of Wasteland 1.0.0 is integrated into NPC, including Gungirl/Shoota models, textures and voices. Eligible new Flexa/Sprokka raiders and vehicle crew use a **33% default population**, with female crew enabled. Explicit female template choices remain available. Identity follows bailout, death, saved crew and traffic virtualization; restored male crew are not rerolled. Controls live in WW Settings > Debug > NPC AI & navigation > NPC population, with appearance/voice controls alongside. Existing configured or migrated population choices are preserved. Disable standalone WomenOfWasteland and restart to use this feature; a loaded standalone copy disables only the integrated female framework. See [port details and tests](port-NPC-Women.md).
- Every quest phase uses a flag on the map and compass, including returning undeliverable goods. Each quest's flag color matches its small tracker bar, remains stable across phase changes/save-load, and follows source tracker ordering. The former return diamond was a marker-style difference.
- The quest tracker clears the compass bottom edge by 24 scaled pixels, regardless of Insanity visibility.
- Clean menus styles Apocasaver's save-name prompt while retaining its original 24-character limit, Enter guard, Confirm/Cancel and save lifecycle. Turning Clean menus off restores the original prompt.
- Loading screens are chosen randomly for each load/autosave; repeats are allowed. Editable tips and cached textures remain supported.

RC7's delivery recovery remains: distant destinations qualify, ready empty destinations become return-goods objectives after observation, and known-dead camps or missing return recipients fail the quest. See [delivery behavior and tests](release-0.1.10-RC7.md).

## Installation

Extract the full ZIP into BepInEx/plugins. It includes WildWasteland.dll, the existing theme/module/loading-screen assets and the new female models/textures/voices. Remove or disable replaced standalone mods, including WomenOfWasteland. Keep Apocatalker and Apocalypter Quests installed separately for optional quest compatibility; they are not bundled.

New Women of Wasteland settings live in config/WildWasteland/settings.cfg; the debug spawn binding defaults to None. No new save format or quest-style config switch is introduced. Existing custom female asset files are installed/migrated only where missing.

## Test in game

1. Restart and check `Loading [Wild Wasteland 0.1.11]`, the module summary and absence of startup errors. With standalone WomenOfWasteland disabled, confirm FemalePopulation is 33 for a new configuration; existing configured choices remain preserved.
2. With multiple active quests, compare each tracker bar with its map/compass flag. Change a delivery to return, then save/load: its flag stays a flag with the same color. Complete/remove a quest and check remaining rows still match their flags.
3. Toggle Insanity visibility and compass visibility, then restore them. The tracker must remain below the compass when shown, without overlap. Check long wrapped objectives.
4. With Apocasaver installed, open save naming, type/edit a name, confirm and cancel. Check original keyboard behavior and successful saving. Disable Clean menus to compare the source popup. Load/autosave repeatedly: screens are random and may repeat.
5. Follow the port note's female population, explicit template, driving/firing, bailout/death, save/load and traffic virtualization checks. Test 0% and 100%, then restore the desired 33%. Existing standalone saved cars retain the documented turret-restoration limitation.

Validation: Windows Release build with deployment; compilation results and deployed DLL MD5 are recorded below. Runtime verification and profiling remain for Denis; features remain Testing. Quest transition/failure messages still use the original notification/log paths; no new verbose switch is required.

Release build: **zero errors, seven existing warnings**. Built and deployed DLL MD5: **E506F8CE6ADB1C47D6F35934F6477576**. The local installed FemalePopulation was explicitly set to 33 for Denis; upgrade behavior still preserves existing user settings.
