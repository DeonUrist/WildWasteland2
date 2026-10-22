# Patrol vehicle build cache and load preparation — 2026-10-18 — Testing

Denis requested a new Patrols spawning system: cache repeated car/War Rig builds during loading, eliminate assembly delays, and restore placed vehicles and their AI before control returns. He explicitly requested unattended headless spawn/load verification, overriding the normal build-only rule for this task. No commit, push, release, version bump or board change was requested.

## Implementation

`VehicleBuildCache` retains a dormant assembled structure for each exact body/parts/hinge/pose/bumper definition. Template names, faction, loot and crew metadata do not select structure, while edited part definitions select a different cache entry. Trailer definitions and the engine-less abandoned War Rig variant use the same path. The cache never registers its templates or reserves in the game's save lists.

Each entry keeps two unused reserve instances. Native PlayMaker preprocessing decodes their actions and installs event handlers while the hierarchy remains inactive; no state entry, physics or crew AI runs there. A spawn consumes the actual prepared instance, assigns unique native names to its frame/parts, defers save-list registration, then activates it at its requested position. Live vehicles are never returned to the reserve pool. Replenishment uses the named 500 us scheduler slice and the shared frame budget; a request exhausting the reserve waits for that preparation. Native activation itself remains indivisible.

`Patrol.Build` no longer instantiates and fits each structural part. Per-instance fuel/condition, loot, seated crew, suspension, ignition and deferred save commit remain in their existing paths. BuildQueue no longer adds 50 ms per stage or a two-second gap between cars. Native frame waits and the existing settling/ignition waits remain.

Loading observes SaveBus and restores patrol ownership after native object loading, before the native `done` state enables Menu and time scale. The narrowly filtered `Wait.OnUpdate` prefix holds only SaveLoadGame's final `Wait for Terrain Gen` action until cache preparation and saved crews finish. Crews get their Pilot/input ownership and ignition request while loading is paused. Engine Sim still owns its ignition through the existing service; parked merchants, setup vehicles, captured cars, dead crews, dry tanks and stripped engines are not made into healthy driving vehicles.

## Saved deltas

Native ES3 continues to own the existing chassis, parts, missing wheels, condition, liquids, cargo, corpses and transforms. Loading never replaces these with pristine cached vehicles. Traffic snapshots now capture actual remaining cargo separately for the vehicle/trailer rather than reusing the departure loot roll; absent mounted crew remain absent.

The `Apocapatrol.Patrol` key, sidecar location, magic and schema-14 prefix are unchanged. A tagged WDR2 trailer after the complete old payload stores seven rider slots (identity, health, pose/standing; empty for dead/dismounted riders) and the exact currently connected trailer's native name. The reader also accepts the initial WDR1 rider-only extension and old saves without extensions. Existing legacy readers ignore trailing bytes, so they retain their original behavior. No new ES3 prefab ID, component type or config key is added.

Native Rustchief `TrailerLoad` starts with a scaled 0.05 s wait, then a 0.2 m spherecast and another scaled wait (`Sources/fsm/prefabs/Rustchief.md:386`). It cannot reconnect during paused loading; the first real-player check confirmed the missing hitch/rider at handoff. WDR2 reconnects only the named existing saved trailer through the existing native joint recipe, preserving both saved poses. It suppresses the native delayed search for explicit detached records too. A missing trailer is logged and never reconstructed.

## Verification

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` passes with zero warnings/errors. Built and installed DLL MD5 both equal `A4D67000E74C30600590C802E8908273` (numeric version remains 0.1.33). Unique staging copy: ignored `verification/runtime-patrol-cache/WildWasteland_0.1.33-cache-e.dll`.

The temporary driver lives under ignored `verification/runtime-patrol-cache/`; it is never installed in the normal game. It runs the actual copied Unity player with copied saves and redirected ES3/graphics-settings IO in the existing sandbox. `-nographics` cannot load native MapMagic terrain (RenderTexture heightmap upload fails), so verification uses hidden `-batchmode -force-d3d11`. No rendering/screenshot/FPS acceptance is claimed.

Final real-player run `packed-e-20261018-005326` completed **36 passed / 0 failed**. It exercised native copied-save loading, repeated prepared spawns, a truck/tanker joint, real native wheel detachment and rider Health death, damage/fuel/crew/cargo mutations, a native save, title return and native reload. Assertions confirmed cache/restore completion and a live Pilot at the native `done` handoff, all tested deltas, restored hitch/trailer rider, and the parked setup exemption. All five checked Patrols scheduler jobs had zero exceptions. No cache/restore/hitch failure, managed exception or patch-conflict signature was found in the captured logs. Original ES3 SHA256 hashes had **zero differences**. The player self-quit and the temporary sandbox driver was disabled afterward. Raw report, Player/BepInEx logs, run MD5 and save hashes are under ignored `verification/runtime-perf/sandbox/Game/VerificationRuns/packed-e-20261018-005326/`.

The initial packed-clone approach still spent 203–212 ms in car activation. Prepared reserves reduced the same instrumented spawn call in the final run to 33.207 / 21.786 ms for TinyTyrants, 15.514 ms for the Rustchief and 7.226 ms for the tanker. This is a substantial reduction of that call, not proof of zero frame impact or total FPS improvement. The reserves trade additional loading work and memory for avoiding repeated runtime deserialization. Crew seating, physics activation and other mods retain their own costs. Visual frame pacing, long sessions, custom bodies, edited templates and the deliberately detached-tanker scenario remain Denis's checks below; the headless suite does not establish those results.

## Denis's visual/gameplay check

1. Enable Debug and global WW VerboseLog, load an existing save, and look for `Vehicle build cache ready`, `Saved patrol preparation complete` and `Load handoff ready`. Live usable patrols should already own their AI when loading ends; parked merchants and the abandoned War Rig stay parked.
2. Spawn repeated car templates and a truck/War Rig through the patrol editor. Each chassis should appear with its fitted structure already present. Check the `Vehicle build cache hit` traces and compare frame pacing with ModProfiler; native activation still has a cost.
3. Damage a part/crew member, remove a wheel, kill/dismount a rider and take cargo. Save, return to title and reload. Those exact changes must survive, without new loot or resurrected riders.
4. Repeat with an attached and a deliberately detached tanker. Only the saved attached pair should reconnect, retaining its loaded position/angle; a detached or missing tanker must not be replaced.
5. Edit/reload a template and spawn again: its changed structure should receive a new cache entry without altering existing vehicles. Check an ordinary vehicle, a motorcycle and any installed custom body.

## Loading presentation / Coyote defaults follow-up (2026-10-18)

Denis's installed PatrolsAndLoot.json already selects Coyote_Bike for Scout and two Coyote_PipeRat plus Coyote_Bike for patrol. The embedded defaults still selected the trader bus for Scout and omitted the patrol bike. Updated only those bundled rosters and the missing-patrol fallback; existing user presets are preserved.

Added Core.LoadingProgress, the shared loading-visible flag used by ReplacementGuard, and CleanHUD's 1632-unit framed accent-colored bar. Small text names the core loading phase and native terrain percentage, then Patrols' current cached template or saved-crew preparation; enabled mod component names appear above it. The display samples actual work at 5 Hz and uses weighted phase milestones, not an elapsed-time estimate. It adds no loading hold, sleep, per-component presentation delay or config entry. Autosave presentation still ends only when the existing Apocasaver bridge finishes. About 170 added/changed source lines across eight source/resource files.

Windows Release/Deploy=true passes with zero warnings/errors. Latest installed DLL MD5: `2EABD1FBDE6AB711AD29262B3D877A6C`. This follow-up was build-verified only; the 36/36 headless results above belong to the preceding binary. Restart the running game, load a save/new world, and check that the framed bar and stage text remain above the image with no recommendations. Change the UI accent and repeat; frame/fill/stage should match. Check the editor's Scout and patrol rosters, and an autosave's normal completion. Visual acceptance remains Denis's test. No new log switch or confirmation trace was added for the presentation.

## Loading text / default font follow-up (2026-10-18)

Removed the enabled-module list while retaining the current loading stage and bar. Moved the large loading label down to close the vacated space. CleanHUD previously hardcoded the forcedSquare atlas; native title-menu buttons use Helveticrap (Sources/data/hierarchy/scene0_menu/Canvas_MM.txt). Added a Helveticrap atlas baked at 96 px from the extracted native font, using the same 100-character set and atlas renderer. Its own native ascent/descent, advances and glyph bearings are stored in game_font_meta.json.

The new settings.cfg [CleanHUD / Menu] CleanFont checkbox defaults false and appears immediately after Clean menus in Debug. Off selects Helveticrap; on restores forcedSquare. Active owned GlyphText meshes/materials refresh on change, inactive text refreshes on enable, and both fonts remain cached. Menu measurement, settings/recommendation IMGUI text, loading text and the save-name window share this selection. Existing native-font HUD text is preserved. Four code files plus two embedded font resources changed, about 60 changed source lines; AGENTS.md updated. No new patches or log switches.

Windows Release/Deploy=true: zero warnings/errors. Latest deployed DLL MD5: `F1AE59172A06736ABAF608CE1BDECE0F`. Build verification only. Restart, confirm the game font and absent loading module list, then open Debug and toggle CleanFont after Clean menus: the current UI text should change immediately in both directions. Leave it off, restart and confirm it stays off. The current loading stage and accent-colored progress bar should remain visible.
