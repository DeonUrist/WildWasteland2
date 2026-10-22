# Performance work preparation — 2026-10-15

Original preparation baseline: `a8f189f` on main, pushed to GitHub, with h DLL MD5 `612B2F7B3288311D1AE6C5BF49156152`. Preparation changed documentation only; the subsequently approved implementation is recorded below. Keep runtime/performance claims at Testing; issue #133 was already closed when inspected and was not modified.

Latest authorized follow-ups: `notes/perf-queue-verification.md` and `notes/perf-spawn-cadence.md`. Build l (`E3AB9ABE0E08D6986A608EFC42A61733`) introduced the global Patrols assembly queue, car/NPC snapshot reuse, NPC rig queue, PM2 lifecycle correction and PM7 diagnostic float tolerance. The temporary copied-save Shadow driver completed 43 checks with no failures, 1,264 FS4 source/reflection samples with no differences, and no FS1/PM2/PM7/FS2/PM10 mismatches. Native saves/title/reloads and crew physics/melee passed within that scenario. Build m (`63FAC347149992B654184F48C4986287`) is now deployed with slower assembly, 750 m range and two merchants; its copied-save run passed 42 checks. This supersedes the title-only evidence below without clearing visual, extended-driving, camp or hard worst-frame gates. Denis requested all current WW changes committed and pushed as `Fix #133: pace vehicle builds and queue NPC rigs`.

Update: Denis approved the 100–150-line Patrols prefab-cache implementation after preparation. The local follow-up changes four Patrols files (109 changed lines): name indexing, scheduled warmup, ES3/mod-factory invalidation and generation-aware availability. No Core or config-entry changes. Windows Release build/deploy passed (zero errors, ten warnings); installed MD5 `D9E505A3132D042514522C21C84752A4`. The baseline/evidence below remains historical; current verification evidence is in `docs/perf-log.md`.

## Evidence and remaining gates

| Check | Evidence | Remaining work |
|---|---|---|
| Plugin startup | Existing interactive log and 30-second headless run: 42/42 features started, preset Low | No full regression or measured performance result |
| FS4 compiler | Interactive log: 1,471 constructors / 8,489 setters, 2,584 ms background | Readiness does not prove correct field values; spawn/save-load parity remains pending |
| FS2/FS3 queue | Code uses CompileQueue and background batches after a main-thread warmup | No captured queue statistics or gameplay comparison yet |
| Shadow checks | FS1, PM2, PM7, FS2 and PM10 support Shadow in code | Exercise camps, doors, cars, saves and fights; zero mismatches in an unexercised title menu is not a pass |
| Seated crew / ESC | Code is in the installed h build | Shoot drivers/passengers/turrets, ram own crew, bail out, use Engine Sim; open TAB/F3 immediately after ESC |
| Texture copying | Interactive log reports CopyTexture incompatible with masterTextureLimit(1) | Identify exact caller and compare RC8 on/off before fixing; portrait CopyInto is a suspect, not a proven cause |

The headless run used the installed game with `-batchmode -nographics -logFile`, never monitor automation. It reached CleanHUD title attachment and 42/42 startup without a managed exception or patch conflict found, then the runner stopped its own process after 30 seconds. Shader errors under NullGfxDevice are present. No scene/save-load driver was found in the searched game C# or WW sources; launching with those flags alone does not exercise a world. No game driver, mock or test harness was added. Headless graphics output cannot clear the CopyTexture issue.

Logs and metadata are preserved under ignored `verification/runtime-perf/20261015-224101-baseline/`; `previous-*` preserves the interactive session before BepInEx's live log was overwritten. FS4's ready line is emitted on a subsequent scene callback, so its absence in a title-only run is not a compiler failure.

For existing Shadow controls, `tools.cfg` section `[Performance / PlayMaker]` uses `FSM sleep`, `Targeted events`, `Shared camera pick`, `Compiled properties`, and `Number to text`. Preserve current config; do not switch defaults or claim runtime coverage from code inspection.

Preset Vanilla disables only preset-driven rendering/simulation features. It leaves exact optimizations enabled (`Feature.UsesPreset`, `PerformanceModule.BindConfig`). A complete unoptimized comparison requires Performance off, or the relevant exact switches Off, with identical save/scenario conditions and verbose logging off.

## First implementation candidate: Patrols prefab lookup

Keep the first batch within Patrols; do not start the global builder/NPC changes together with it.

- `Spawning/Patrol.cs` Prefabs.Exact: full GameObject and ES3-manager scans when the exact index is absent or its five-second rescan timer has expired, after per-query hit/miss memo checks. Case-insensitive hits use LINQ and memoize 60 seconds; misses memoize 30 seconds. The five-second timer and 30-second negative cache are distinct.
- Prefabs.FindAny can trigger a full PlayMakerFSM asset scan on a miss every 30 seconds; Find/Scan also decode item display actions. `Vehicles/Templates.cs` revalidates availability every five seconds.
- `Patrol.ResetForScene` already invalidates prefab caches; `PatrolsModule.OnSceneLoaded` calls it. `Vehicles/ModVehicles.cs` owns explicit and reflected factories; Exact/Find/FindAny consult factories before ordinary assets. Preserve this ordering and exact-name safety for exported templates.
- Prepare exact and case-insensitive indexes outside the spawn path, retaining preference for ES3-backed case matches. Keep display-name/partial matching behavior separate. Cache misses only for a valid registry generation; never hide a prefab registered later by a mod or ES3.
- Before editing, identify registration/invalidation events and a point after native/mod prefabs are available. A scene-load snapshot alone is insufficient proof that late registrations are covered. Template availability needs to observe that same generation.
- Compare full-scan counts and worst spawn frame against this baseline. Existing estimated 30–150 ms scan costs are not measured results.

Expected touched files: `Spawning/Patrol.cs`, `Vehicles/Templates.cs`, and possibly `Vehicles/ModVehicles.cs` / `PatrolsModule.cs` for lifecycle invalidation. Resolve the event design and change-size limit before implementation; no Core or other-module dependency is needed for this first candidate unless evidence proves otherwise.

## Subsequent batches

1. A single time-budgeted build queue, after prefab lookup. The handover's proposed 3–4 ms must be reconciled with Core's existing 2 ms whole-frame budget; do not silently raise it. Instantiate and rig creation are indivisible costs even inside a sliced job.
2. Reuse car FSM snapshots during settling; guard expensive verbose strings.
3. Per-occupant/NPC caches and rig queuing, then fight-time work, one measured cause per batch.
4. Shared Perf/Culling services require separate Core scope approval. Do not expose Performance's concrete Batch type to other modules; define a Core-owned contract first.
5. ES3 save caching and FS5 remain separate, gated work. Do not alter save formats, registered components or original plugin DLLs.

For each implementation: Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`, zero errors, game closed, disabled-install assets checked, copied DLL MD5 recorded. Preparation alone needed no rebuild; the approved implementation was built/deployed as recorded above. Denis subsequently requested completion of the fixing cycle; the initial follow-up and preparation docs are recorded in `Fix #133: cache Patrols prefab lookups by registration generation`. The subsequent queue and cadence work is recorded in `Fix #133: pace vehicle builds and queue NPC rigs` under his explicit commit/push instruction.
