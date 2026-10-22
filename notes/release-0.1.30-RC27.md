# Wild Wasteland 0.1.30 RC27

Experimental prerelease. Crossbow display fixes remain Testing; Denis verifies them in game.

- Apocaspawner's crossbow Damage stat now includes WW's existing 2.5x player-projectile multiplier: 50 instead of the native 20. Integration is optional and does not modify Apocaspawner's DLL.
- Native crossbow pickup and shop descriptions now show Damage - 50 while WW player projectiles are enabled. The original action text is restored after each UI write; disabling player projectiles uses the native 20 again.
- Projectile damage, falloff and headshot rules retain their existing behavior. No config entries or save formats changed.
- Includes the complete RC26 asset set, including Weather audio. Install the archive into BepInEx/plugins and preserve custom assets and config files.

## Check after installing

1. Confirm `Loading [Wild Wasteland 0.1.30]` in LogOutput.log. With Apocaspawner installed, also expect `[Guns] Apocaspawner crossbow damage bridge installed.`
2. With Guns and player projectiles enabled, select Crossbow in Apocaspawner: Damage should read 50.
3. Inspect a crossbow on the ground and in a shop: both descriptions should show Damage - 50.
4. Shoot an unarmored enemy's torso within 20 metres, with no intervening vehicle parts: expect 50 damage. Global WW VerboseLog adds `[Guns] Hit: crossbow ... damage -50.0` for a full-damage hit.
5. Disable player projectiles, then reopen the spawner details and re-hover the item: expect native 20. Re-enable and repeat: expect 50.

Windows Release build/deploy is the automated verification; gameplay and performance remain pending.
