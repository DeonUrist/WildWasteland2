# Wild Wasteland 0.1.29 RC26

Experimental prerelease: Weather presentation and gameplay remain Testing. Build verification is Windows compilation; Denis tests gameplay and performance in game.

- Natural SandStorm encounters now use WW's moving dust walls, atmosphere and hurricanes. Original native graphics, sound and centre hazards are suppressed while Weather is enabled; native encounter/save identities and movement remain.
- Critical hurricanes lift vehicles/physics objects with native force values and apply native Bodypart damage when actors enter the dense swirling sand wall. The eye is safe. Prior momentum still carries through the eye. Native War Rig shelter filters remain in the damage/force path.
- Wide and thin uneven rotating sand volumes have wall-smoke spiral tops merging into clouds, thick ground dust, fine local sand clouds and dark orbiting debris. Lightning flashes world lighting and thunder follows after distance /343 seconds using the supplied thunder sound.
- Live settings expose external wall, main tint, hurricane and local sand colors, storm darkening, density, front width and depth; Performance presets choose rendering detail.
- Fresh-install defaults match Denis's current choices: external wall #B89A7D, main #805936, hurricane #A3907F, sand #572F0C; darkening .75, density .8/km², width 20 km, depth 4 km, map areas enabled. Existing saved settings retain their values. Reset storm colors returns to this palette.
- At Denis's explicit request this RC also includes all previously uncommitted work: Debug/Developer settings grouped in expandable module sections with search; title/ESC settings background; native crosshair preview/finer Dot preview; queued manual patrol-template spawning; and the SigmaMod early-FSM-state compatibility guard. Existing Weather map and Performance-detail integration are included.
- Manual critical storms form over 20-40 gameplay seconds, gain wall hazards at 30 s and last ten minutes. Manual storms/snapshot restores are not saved; diagnostic snapshot restores remain visual-only. Natural encounter roots retain the game's save array/ID and native distance cleanup.

Install the full archive into `BepInEx/plugins`; keep your config files and custom assets. This RC includes `WildWasteland/Weather/sound/sandstorm.wav` and `thunder.mp3`, so a DLL-only upgrade from RC25 lacks the new audio. The compiled shader bundle is embedded. No personal settings.cfg/tools.cfg or save files are packaged. Weather is controlled by its existing module Enabled switch; turning it off restores native behavior.

Up to four nearest natural storms are presented at once; manual previews reserve their slots. Current spacing/size limits place up to three hurricanes per region despite a higher requested area-density count. Lifting coverage adapts the original 150 m force falloff to the dense hurricane body (minimum 150 m); exact vehicle response and performance need in-game verification. Fine wisps and the outer storm fog have no continuous health damage, matching vanilla's tornado-contact damage mechanism.

## Check after installing

1. Confirm `Loading [Wild Wasteland 0.1.29]` and `[Weather] Native storm replacement enabled` in LogOutput.log. Keep diagnostics/freeze off.
2. Settings > Developer > Weather visual preview: spawn Critical dust storm preview ahead and wait 40 gameplay seconds. Drive a disposable car into a wide and a thin hurricane's dense sand wall: expect pulsed shoving/lifting and normal collision consequences.
3. Turn F11 god mode off. Cross the sand wall on foot: native -5 Bodypart damage on entry; leave/re-enter for another entry. The central eye applies no new damage/force. NPC contacts use native -5000 Bodypart damage. Check War Rig tank shelter protection.
4. Clear manual previews: no invisible hazards remain. Encounter a naturally spawned storm: WW presentation only, with moving map footprint and no duplicate original storm. Save/reload, save-and-quit/reload, and toggle Weather off/on around a natural encounter.
5. Verify your current colors/dimensions remain and a fresh configuration uses the defaults above. Check thin-column billows, cloud/ground blending, sand/stones, day/night flashes, delayed thunder and frame time.

First wall contact logs `[Weather] Hurricane hazards active`; global WW VerboseLog adds `[Weather] Hazard contacts: lift applications=..., player wall entries=..., NPC wall entries=...`. Counters confirm force calls/event dispatch, not final damage through god mode/shelter or measured vehicle motion. Detailed source/behavior: notes/weather-native-hazards.md.
