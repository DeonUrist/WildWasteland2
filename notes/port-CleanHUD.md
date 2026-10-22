# CleanHUD integration

Source: `V:\Repos\Apocalypter\CleanHUD`, version 1.5.1, clean commit `119a2f8`. Target HEAD before work: `a9b5088`. Existing uncommitted Guns, PlayerBody and Patrols changes were preserved.

Status: Testing. Local game execution and config harness runs are prohibited by AGENTS.md; Denis supplies runtime and performance verification. `claude/GAME_KNOWLEDGE.md`, source-mod notes and initial integration reports are not available in this checkout. Existing source code, repo docs, FSM dumps and LogOutput.log were used. The log currently shows standalone CleanHUD 1.5.0 loaded; the port deliberately takes the latest clean source 1.5.1.

Windows Release build/deployment succeeded: `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`, zero errors, seven existing warnings in HUD/PlayerBody/NPC. Build and deployed DLL MD5 match: `62D20E304EE2200EC47ED9D49CAA48A3`. Only WildWasteland.dll was overwritten in the game; standalone CleanHUD remains installed and must be disabled through Apocasetter before testing the integrated module. No commit or push.

## Files and ownership

The ten support files retain their class names in `src/Modules/CleanHUD/`; `Api.cs` retains `.Api` as a documented namespace exception. `CleanHudModule.cs` replaces the standalone plugin/runner. `SceneCache.cs` supplies shared, sliced scene traversal and cached root paths to bindings and menus. All 46 files from Resources are copied into Embedded; logical resource names remain `CleanHUD.<name>`.

Core changes: two Plugin.cs lines register the module and soft dependency; CorePages.cs adds six Game/HUD settings and excludes duplicates from Debug. Both build reference lists gain the existing BepInEx Mono.Cecil DLL. No other module behavior is changed.

## Patches and lifecycle

UiTextSetText.OnEnter postfix: module patch, `MessageHook.Postfix`; Active/text/everyFrame first filter, then cached timed-state classification and HUD-canvas filter. GetButtonDown.OnUpdate prefix: existing dispatcher, `CancelHook.Prefix`; module/menu/frame first filter, skips Cancel only during the menu sub-screen. Original menu game-button FSM events and native settings saves remain unchanged. The menu yields input to EditorSession / Apocasetter. Shutdown restores exact preexisting CanvasGroup state and native HUD alpha, removes render callbacks, disposes unfinished icon jobs and destroys owned graphics/textures/sprites. Scene/load callbacks clear caches; module restart registers fresh jobs.

## Config and data

Eight settings including module Enabled. `CleanHUD / HUD`: HUD, ShowNeeds, ShowHealthNumbers, ShowInsanity, Scale. `CleanHUD / Menu`: Enabled, EnableCheats. F7 Game/HUD exposes all except EnableCheats; Debug exposes the latter. Missing settings migrate by source key, independently of WW's global first-run flag, so existing WW installations retain CleanHUD choices. Existing WW keys take precedence. Layout and game_keys.txt copy only when the new file is absent; legacy files remain untouched. Native SaveSettings.es3, Settings.xml and PlayerPrefs names/formats remain unchanged; no new save data.

## Performance changes and limits

- No standalone Runner/Unity update loop; six named jobs: HUD, Menus, HUD attachment (1 s), Layout reload (2 s), Scene key discovery (500 us sliced), Weapon icons (500 us sliced).
- No blocking Thread.Join. DLL code scanning runs once on a background thread; warnings are delivered on the main thread. The catalog includes WW gameplay keys while excluding this module's conditional menu capture keys. Reflection remains only in menu-opening/rebinding operations.
- Root paths and missing-path retries are cached; the native MODS button is cached, with a 0.5 s retry while absent. Hidden menu paths/modifier arrays are static, CanvasGroups cached.
- HUD references, slot variables and charge Quantity variables are cached. Text parsing/digit extraction occurs on native text changes; number formatting on displayed-value changes. Weapon metadata resolves on item changes; native resources preload at startup. Compass uses GameRefs.MainCamera and Origin.AnchorTransform.
- Modded icons are queued, cached once per prefab key, with mesh building/crop rows spread across slices. Display refreshes when a render finishes. An icon may initially be blank. Camera.Render, ReadPixels, GetPixels32 and final texture upload remain indivisible Unity calls; budget values are cooperative rather than hard caps on those calls.
- Scheduler measures jobs; Canvas render/batching, the render callback and Harmony work also need ModProfiler. No CleanHUD baseline capture exists in the local ModProfiler location. No measured performance claim is made.

## Denis's verification

Disable standalone CleanHUD in Apocasetter and restart. Verify `[CleanHUD] started (1 patches)` and `CleanHUD (Clean HUD & menus): on, patches 1`. Title/pause menus and settings should work; Escape backs out of dialogs/capture without resuming. Load a save; rotate, switch weapons, fire/reload, heal, collect armor/ammo, test Needs/Numbers/Insanity/Scale, F9 hide UI, native prompts/radiation and Combat HUD feedback. Switch the module off/on twice, save/load and return to title; native menus/HUD return when off, no duplicates on restart. Test one modded item icon's first appearance and cached revisit.

For standalone baseline and the integrated build, capture the same save/scenes for 30 seconds: idle, shooting/reloading/healing, driving/origin shift, save load and convoy combat. F7 Modules/Performance: reset, close F7, play 30 s, reopen and record all CleanHUD jobs. Use ModProfiler for full WW/GC/UI costs. Targets: whole WW <=0.5 ms average / <=2 ms worst, no steady-state allocation, no disabled jobs/errors. First modded icon render/readback is a separate measurement. Fill docs/perf-log.md; leave status Testing until Denis verifies.
