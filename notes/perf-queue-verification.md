# Patrols/NPC queues and PM2 verification — 2026-10-15 — Testing

Denis requested runtime verification and the next Patrols/NPC performance batch, explicitly allowed a temporary headless verification driver, then approved the PM2 lifecycle fix exposed by Shadow mode. The CopyTexture/masterTextureLimit issue is deferred at his request. No Core, config entries/defaults, save keys/formats or registered component types changed in this batch. This batch and the subsequent spawn-cadence changes are recorded as `Fix #133: pace vehicle builds and queue NPC rigs` under his explicit commit/push instruction.

## Cause and implementation

- Patrols previously started independent build coroutines, allowing several vehicles to assemble in one frame and bypassing scheduler accounting. `Spawning/BuildQueue.cs` now owns one assembly at a time, under the `Patrols.Build` 1 ms slice and existing 2 ms whole-frame budget. Part/cargo steps yield individually; native frame and physics waits remain. After registration, ignition may overlap the next assembly. Queued spawn positions are stored in world coordinates; reset cancels pending/active work and releases callbacks.
- The settle frame repeatedly scanned the same car FSM hierarchy and constructed verbose descriptions with logging off. Short-lived snapshots now serve conditions/headlights/music and Crew.Init's Drive/Handbrake/de_Attach discovery. Expensive strings are guarded by Core.Log.Verbose.
- Brain.Make/TryRig attached all newly discovered animation rigs synchronously. `NPC.Rigs` now processes at most one eligible attachment per frame, respecting the scheduler budget and existing retry timing. Scene/shutdown clears queued references. Brain/Senses reuse component snapshots during each registration; prefab layouts are not assumed identical across female/modded NPCs.
- PM2 Shadow exposed 60 missing/reordered receiver comparisons in the baseline. Native PlayMakerFSM.OnEnable appends to FsmList before calling Fsm.OnEnable, whose callbacks can dispatch events and enable other FSMs. The old postfix indexed too late. OnDisable removed the index before native Disable events. FsmRegistry now indexes at Fsm.OnEnable entry, and removes immediately after the native list removal. With PM3 off, disable retains vanilla List.Remove while tracking. The shared enable postfix remains an idempotent observer.
- Denis also approved a PM7 diagnostic fix after eight terrain hits differed by approximately 1e-7 m. Shadow still requires the same collider and triangle, allowing only 0.1 mm of point/distance noise and a 1e-4 normal-vector difference. Actual mouse-pick selection is unchanged.

The queue limits scheduling concurrency, not the duration of an indivisible Instantiate, rig build, or final settle step. No FPS or hard worst-frame improvement is inferred from queue counts.

## Isolation and method

The temporary plugin/scripts live only in ignored `verification/runtime-perf/driver/`. They were never installed in the normal game's plugins or included in production builds. The sandbox copies BepInEx/plugins/config and junctions only game data/runtime. It uses the real player with `-batchmode -force-d3d11`, no monitor/UI automation. Each run copies SaveGame1 and the other original ES3 files plus Settings.xml into a fresh run directory. File-based ES3 paths and native graphic settings are redirected there. Original ES3 SHA256 hashes are compared afterward.

The driver invokes the native load-menu FSM, waits for live play, checks ESC/settings state, submits three vehicle builds and six ranged NPCs, tests crew triggers/kinematic state/own-car damage guard/bailout, saves natively, returns to title and reloads. The new queue additionally checks scene-reset cancellation callbacks. Engine Sim remains installed in the sandbox and reports simulation of built vehicles. Later runs enable the player's native god mode after load so an unattended death cannot send the test back to title; these runs do not measure ordinary player movement/survival cost. Combat is directed at a native opposing NPC, not the unattended player.

FS4 compares one sample per action-type/field against an independently compiled, unmodified decompiled vanilla LoadActionField algorithm, with ordinary reflection for private ActionData access and setters/constructors. It preserves nextParamIndex and initial field state. This checks sampled values against the source algorithm; it is not exhaustive native-binary equivalence. Earlier driver-development runs with Harmony's reverse-patched loader produced false mismatches and are excluded. Reverse copying remained unreliable even after widening branches.

## Evidence

- Baseline prefab-cache i, DLL MD5 `D9E505A3132D042514522C21C84752A4`: `verification/runtime-perf/sandbox/Game/VerificationRuns/baseline-shadow-7/`. Completed native save/title/reload; assembly peak 3 vehicles, rig peak 9 attachments/frame over the run, 1,264 sampled FS4 fields / 0 differences. PM2 had 60 Shadow mismatches; this is a failing baseline, not a clean regression pass.
- Intermediate production build k, DLL MD5 `F5349E1B11692CD75F31731027169F54`: Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`, zero errors / ten existing warnings. Staged under `verification/runtime-perf/20261015-queue-rigs/`. Numeric version remains 0.1.26; use the MD5.
- Build l adds only the approved PM7 comparison tolerance: MD5 `E3AB9ABE0E08D6986A608EFC42A61733`, Release/Deploy=true zero errors / ten existing warnings, copied to the normal game. No temporary verification plugin is deployed there.
- Instrumented k3 attributed 101–123 ms to the first body-construction step, approximately 5–21 ms to individual JSON part steps and 20–35 ms to occupant steps. Scheduler job maxima include the temporary profiling hooks and all enabled mods. This shows why a 1 ms slice is not a hard per-frame bound; further native construction/seating profiling remains necessary. No broad performance acceptance target is claimed.
- Final Shadow and enabled-mode results are recorded below. Raw report.json, Player.log, BepInEx.log, jobs.txt, run metadata and original-save hashes remain in each ignored run directory. Both final players self-quit via Application.Quit after reporting no failures; no managed exception, native crash signature, patch conflict, disabled job or Missing LoadActionField was found in their captured logs.

### Completed Shadow run

`queue-shadow-l`: 43 passed / 0 failed, clean Application.Quit. 1,264 FS4 source/reflection samples / 0 differences; all five requested Shadow features have 0 mismatches. Three vehicle callbacks succeeded; assembly peak 1. Seventeen rig attachments across the full run, peak 1 per frame. Native save/title/reload retained all built vehicle names. Crew trigger/kinematic state, native knife and pipe-wrench Actor-only casts, physics ray, own-CarAttack guard, bailout physics and pending scene-cancellation callbacks passed. PM2 also passed after temporarily disabling PM3. Original ES3 hashes: 0 differences.

Compile queue: 225 accessors / 20 batches, final 3 ms / worst 13 ms background, 0 queued. These are worker batch times. Scheduler before save: Patrols.Build max 114.830 ms, NPC.Rigs max 3.468 ms, both 0 exceptions. Shadow and temporary instrumentation affect timing; neither value is a production FPS benchmark.

### Completed enabled-mode run

`queue-on-l`: 38 passed / 0 failed. FS1/PM2/PM7/FS2/PM10 ran On; PM2 temporarily ran Shadow with PM3 Off for the lifecycle check. All three vehicles survived native save/title/reload, crew checks passed, and scene change cancelled one active plus two pending builds with exactly one null callback each. Queue empty afterward. Seventeen rig attachments across the full run, peak 1/frame; assembly peak 1. Original ES3 hashes: 0 differences. No FS4 reference sampling ran in this mode.

Actual feature counters: compiled properties 999 sets / 3,211 gets / 272 vanilla fallbacks; number conversion 40,008 calls / 31,612 reuses. Compile queue 225 accessors / 20 batches, last 3 ms / worst 15 ms background, empty. Scheduler before save: Patrols.Build max 105.880 ms, NPC.Rigs max 2.992 ms, 0 exceptions. Additional method probes found SeatOccupant 15–21 ms; Paint.Apply, Register.Name and Register.Add had no recorded call above the 5 ms probe threshold. The approximately 105 ms first body step therefore remains a construction/activation bottleneck; native instantiation still needs separate profiling before a further fix.

## Limits and Denis's remaining checks

Headless state/physics checks do not establish physical mouse input, TAB/F3 repaint/visibility, graphical correctness, windshield obstruction/headshot behavior, extended driving through tiles/origin shifts, or a prolonged camp fight. Denis should compare the same save with verbose logging off, test those interactions, inspect Patrols.Build/NPC.Rigs in the scheduler, and check for Shadow mismatch or disabled-job lines. Keep status Testing; no issue/board status was changed.

1. Load the usual save, spawn a three-car convoy from the Patrol editor and watch `Patrols.Build` / `NPC.Rigs` in F3. Vehicles should finish in order, retain parts/crew, then start normally; no disabled-job or exception lines. The known first construction spike can still occur.
2. Save, return to title, reload: all assembled cars/crew should remain. Change scenes while builds are pending: callbacks must not create vehicles in the new world. Engine Sim should start/stop the completed cars as before.
3. Open F3/TAB immediately after closing ESC; verify the mouse remains usable and the panels render correctly. Shoot and melee accessible driver/passenger/turret bodies, then test windshield obstruction/headshots and a ram at speed: enemy hits should damage crew while their own car never ram-damages them.
4. Drive across a tile/origin shift and run a longer camp fight with the five features in Shadow. Expect no `[Performance] ... shadow mismatch` or job `DISABLED` lines. This remains outside the bounded runs above.

The sandbox verification DLL is renamed `.dll.disabled` after the final runs. Its source and built DLL remain ignored for reproducibility; the normal game contains only the production WildWasteland DLL. No config entries changed in the production install.
