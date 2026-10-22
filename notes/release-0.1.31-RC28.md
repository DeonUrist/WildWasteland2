# Wild Wasteland 0.1.31 RC28

Experimental prerelease. Natural stew-pot loot remains Testing; Denis verifies it in game.

- Natural item spawners now substitute an empty 20-litre Stew Pot on 25% of gas-stove rolls. This is a share of stove rolls, not a 25% chance at every location. Remaining stove rolls and the total item count retain native behavior.
- Uses the existing shared CreateObject hook in Survival. Native placement, random rotation, item naming, save-list registration and spawner completion continue normally. No loot tables are rewritten and no extra item is added beside a stove.
- Existing spawned items, save loading, merchant stock and manual spawning are excluded. New games and untriggered natural loot spawners in existing games can produce pots. Survival must be enabled; Apocaspawner is not required for natural loot.
- Pot templates remain under their inactive holder but produce active native clones, allowing existing ES3Prefab registration to discover them. If template preparation fails, the original stove is retained.
- No new config entries, saved components, prefab IDs or save-format changes. Includes the complete RC27 asset set, including Weather audio. Install into BepInEx/plugins and preserve custom assets/configs.

## Check after installing

1. Confirm `Loading [Wild Wasteland 0.1.31]` and `[Survival] started` in LogOutput.log. Enable Survival's VerboseLog under F3 Debug for spawn traces.
2. Visit previously untriggered natural loot locations where gas stoves can appear. Some stove rolls should now yield visible, empty pots; the others remain stoves. Several locations may be needed because the original stove roll is uncommon.
3. A selected pot logs `[Survival] Natural Stew Pot selected: ItemSpawn...`, followed by `[Survival] StewPot registered: StewPot(Clone)` during creation. There should be no `Natural Stew Pot unavailable` warning.
4. Pick up a natural pot, fill it, drink, add meat and cook. Save, quit and reload: the pot and contents should persist without duplication. Return to the looted location and confirm it does not spawn again merely because of the reload.
5. Existing stoves, merchant stock and manually spawned stoves should remain stoves. Optional Apocaspawner's Camp & Farming > Stew Pot should still work. With Survival disabled, fresh loot uses normal stove rolls.

Windows Release build/deploy passed with zero errors and ten existing warnings. Built, installed and uniquely staged DLL MD5: `D2FFF9054A099CB2525133902B0B0B29`. Gameplay and performance remain pending.
