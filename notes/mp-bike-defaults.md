# Motorcycle controls and calibrated defaults from MP #8 — Testing

On 2026-10-18 Denis explicitly requested the current MP motorcycle work in standard Wild Wasteland, with all saved bike offsets as defaults and numpad decimal no longer opening the editor. The earlier base repo-only/no-deployment instruction is retained.

Four source files: `PlayerBody/Vehicles/WheelArms.cs`, `Vehicles/WheelGripEditor.cs`, `Body/Body.cs`, `PlayerBodyModule.cs` (210 added/53 removed lines), plus `PlayerBody/Embedded/WheelGrips/handlebars.txt` and the affected Harmony inventory row. Existing pending Core UI backports are preserved. No multiplayer dependency or protocol code is added.

The backport includes fixed moving-handlebar grip targets, independent left/right hand/wrist/shoulder/elbow calibration for straight/left/right turns, one shared bike profile, sitting translation and per-pose torso rotation/bending. Native two-bone reach limits remain. A missing removable car wheel gives knee-rest hands and zero local steering input while native steering/centering still runs; fitted cars retain their existing mirrored wheel fits and regrips.

All 96 saved live values from `config/WildWasteland/PlayerBody/WheelGrips/handlebars.txt` at capture are embedded byte-for-byte: MD5 **C734E4A42CEE9028B84B66BC6C826732**. This includes all three independent arm poses, sitting XYZ and torso Euler; no reset or mirrored overwrite was performed. Local custom profiles still override defaults; fresh installs and RESET use the embedded profile. Legacy 12/60/84-value bike files remain readable. No live file was edited by the agent. The ten existing native-car defaults are unchanged.

The final embedded defaults include Denis's latest minor corrections saved at 20:51:48, replacing the initial F10C snapshot in both repos. Final source backup: `%TEMP%/wwmp-issue8-defaults-20261018_204817/handlebars-latest.txt`.

The hidden menu is still F3 > Developer > Player body & animation > Steering-wheel hands, requiring Enable Debug and Developer mode. Start using its existing button. Numpad decimal now only stops an active editor. Num0 cycles both hands/shoulders/elbows and sitting/torso controls; Num9 straight/left/right pose, Num3 selected preview/live, Num5 hand move/rotate, 8/2 up/pitch, 6/4 right/yaw, 7/1 forward-back/roll, Shift fine, Enter save. Existing release/pause auto-save stays.

Denis also requested grading off in both versions and explicitly approved Core changes. `Core/ColorGrading.cs`, registered by `Core/Plugin.cs`, uses the existing `ActivateGameObject.DoActivateGameObject` dispatcher to force the exact native color-grading volume/FsmBool/toggle off. Original OnEnter still finishes, with no update loop or scene scan. It works with CleanHUD/Performance disabled or Vanilla preset. CleanHUD Menu.cs has false grading default/read/write and a disabled OFF row. No new config or effect changes beyond the native `PP color grading` volume. Sources: `Sources/fsm/scene1_game/Graphics_Canvas.md:98-127`, `Sources/data/hierarchy/scene1_game/__PostProcessing__.txt:6-7`. Startup: `[Core] Color grading policy ready (native graphics option forced off)`.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed **zero warnings/errors**. Built and unique staged MD5 **41C3DEF45CE51A0A153BCDCAA04F7B8C**; stage `bin/Release/WildWasteland_bike-defaults-grading_20261018_205628.dll`. Base was not deployed. No game launch, harness, commit, push or release. Changes remain Testing.

Denis's manual checks:

1. While seated, press numpad decimal with the editor closed: it must stay closed. Enable Debug and Developer mode, open the existing hidden page and start via its button. Decimal should stop it and another decimal press should not reopen it.
2. On a backed-up/disposable bike profile, RESET should restore all accepted independent hand/shoulder/elbow positions, wrist rotations, sitting offset and torso settings for straight/left/right turns. A fresh profile should start with the same embedded fit; custom local profiles must still win on ordinary load.
3. Edit left and right separately in each steering pose; confirm independent values survive save/reload. Check live turn blending and shared setup on Crossbreed/Halfbreed/Motorcycle, first/third person, both player models, and body/arm ownership while using tools/weapons.
4. Remove/refit a car steering wheel: knees/rest and blocked steering while absent; normal controls and saved wheel regrips when fitted. Check bike-to-car editor transitions and save/load/module off/on without accumulating sitting offsets.
5. Color grading is locked OFF in CleanHUD title/ESC graphics; native toggle attempts with CleanHUD off must leave grading off. Repeat after settings/save reload with Performance Vanilla/off; other post effects retain their own settings.

Global VerboseLog retains `Handlebar arms:` per-side pose/blend/reach/torso/sitting samples. Correct visual fit and performance require game testing.
