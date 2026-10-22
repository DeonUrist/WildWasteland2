# Wild Wasteland 0.1.38 RC35

Experimental prerelease. Gameplay verification remains Testing.

## Motorcycle hands and vehicle controls

Motorcycle hands follow moving handlebar grips, with independent left/right hand, wrist, shoulder and elbow poses for straight, left and right steering. Sitting offsets and per-pose torso rotation/bend are included in the shared 96-value bike profile. The final calibrated defaults are embedded; existing custom profiles override them, and RESET restores the embedded fit. Native car wheel defaults and their mirrored hand-over-hand turns remain intact.

Removing a car's removable steering wheel rests the available hands on the knees and blocks only the local driver's steering input, retaining native smoothing/centering. Refitting a wheel restores its normal controls and hand setup.

Start the live editor from F3 > Developer > Player body & animation > Steering-wheel hands with Enable Debug and Developer mode enabled. Numpad decimal only stops the editor. For bikes, Num9 selects straight/left/right, Num0 cycles arms/sitting/torso, Num3 switches preview/live, Num5 switches hand move/rotate, 8/2 up/pitch, 6/4 right/yaw, 7/1 forward-back/roll, Shift fine and Enter saves. See [backport details](mp-bike-defaults.md).

## Settings and graphics

F3 separates PLAYER, WORLD and HUD. World map/compass policy is on WORLD; overall display switches remain on HUD. RESET is available on all three and retains the existing shared reset behavior. Hurricane density, front width and storm depth are under Debug > Weather > Storm tuning, removed from WORLD and generic Developer listings. Existing keys, values, ranges and callbacks are preserved.

Native color grading is forced off independently of CleanHUD and Performance settings. CleanHUD's graphics option is locked OFF. Other post effects are unchanged.

## Installation and testing

The full ZIP retains RC34's assets and defaults. Extract into `BepInEx/plugins` when ready to update, preserving customized assets. No personal configs or saves are bundled. No new config entries or native save-format changes; bike profiles support 96 values and still read legacy 12/60/84-value files.

1. Check PLAYER/WORLD/HUD placement, then enable Debug and verify the three storm tuning controls appear only under Debug > Weather > Storm tuning. Confirm existing values remain.
2. Ride a bike in first and third person; check hands throughout straight/left/right steering and torso/sitting fit. On a backed-up test profile, check RESET restores the final defaults, and independent edits survive save/reload. Test tools/weapons and bike-to-car transitions.
3. With the editor closed, numpad decimal must leave it closed. Start with the Developer button; decimal must stop it.
4. Remove/refit a car steering wheel: hands rest on knees and steering is blocked while absent; controls and normal wheel poses return when refitted.
5. Check color grading stays off in title/ESC graphics, with Clean menus on/off and the Performance Vanilla preset. Other post effects should retain their settings.

Global VerboseLog provides `Handlebar arms:` samples. Startup reports `[Core] Color grading policy ready (native graphics option forced off)`.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with zero warnings and zero errors. Built and release-staged DLL MD5: `1AD4D589287D8B2FC6BA00B987BDDA49`. Per Denis's existing instruction, release staging stays under `V:\Repos\Apocalypter\_releases`; no game-folder deployment or staging while MP testing continues. No runtime test is performed by the agent.

## Updated player models

The male and female GLBs are copied byte for byte from the installed WWMP `PlayerBody/Models` folder at Denis's request. These replace the corresponding RC34 release assets; other assets and textures are retained. Check both body selections on foot and seated in first and third person.

- `Player_male.glb`: 142,116 bytes; MD5 `6213641473857213E7FA1CF0353A7BF2`.
- `Player_female.glb`: 1,735,544 bytes; MD5 `B6DC6BF1186200ED2AD7902826D23048`.
