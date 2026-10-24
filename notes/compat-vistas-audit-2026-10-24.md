# Vista / World Amp compatibility audit — 2026-10-24

Status: initial investigation plus the approved follow-up for findings 3 and 5 (see implementation section below). Denis requested a thorough read-only compatibility review, then explicitly requested repeated programmatic real-player generation/map checks instead of monitor automation. That authorizes the isolated runtime checks below, overriding the normal build-only rule for this task.

Initial audit outcome: all six real-player starts reached gameplay and rendered the native map without fog of war. **A universal seam-free result fails:** seed 666 has a 4.8068 m raw height discontinuity near a starting-tile corner, reproduced with Vista while WW is absent. Separately, WW's Vista patch teardown failed on every WW run. The findings below describe the original build; findings 3 and 5 are superseded by the approved implementation section.

## Exact inputs and isolation

- WW source: `279fdea3809b9d1ce7fe661405047b829f71c2de` (2.0.3). Working tree was clean at audit start.
- Current built and installed WW DLLs match: MD5 `54ACDAE3BC491D2D61A348DB52AE51E2`.
- Staged dependency: `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/OtherMods/VistasWorldGen.dll`, World Amp 1.0.7, 578,048 bytes, MD5 `2E39891C9486E54806645AEADAF6D33A`. The copy in the live `plugins/` folder matches.
- Fresh inspection-only decompile: `V:/Repos/Apocalypter/_tools/decompiled/VistasWorldGen-audit-20261024/`. Its obfuscated strings were decoded without loading or modifying the original assembly. Game behavior was checked against the existing `Sources/` export, not redecompiled.
- Temporary player sandbox/driver: `V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/`. Executable/native libraries, BepInEx core, plugins, assets and configs were copied; game data and Mono runtime are read-through junctions. No test plugin was installed in the live game.
- Real Unity player uses hidden `-batchmode -force-d3d11`, never `-nographics`. Native NewGame receives each requested seed; the driver waits for native `SaveLoadGame/isPlay`, enables the native map FSM, and reads its actual 1024×1024 render texture. No monitor capture or fabricated terrain simulation.
- Only sandbox settings change: WW `FogOfWar=false`, debug level 2, Vista trace on. ES3 file paths and native graphics-settings I/O are redirected into each run directory. No native save operation is requested.
- Driver built with the existing Windows `dotnet`: zero warnings/errors. WW production source/DLL/config/assets were not edited, rebuilt or deployed.

## Findings, ordered for follow-up

### 1. Confirmed runtime defect: Vista bridge teardown cannot reliably remove its patches

The first sandbox run reached gameplay normally, but exiting produced:

```text
Failed to patch static bool A.E::A(A.y , bool ): System.ArgumentNullException: Invalid argument for ldfld NULL
[Compatibility.VistasWorldGen] unpatch failed: HarmonyLib.HarmonyException: IL Compile Error
```

Cause is directly visible in WW: `OptionalBridge.Stop` invokes the bridge's stop callback before `Scope.Shutdown` unpatches it (`src/Modules/Compatibility/OptionalBridge.cs:59`). `VistasWorldGen.Shutdown` clears `_placeName`, `_placeKind`, `_landmarkName` and `_assembly` first (`src/Modules/Compatibility/VistasWorldGen/VistasWorldGen.cs:110`). Harmony removes patches individually and can regenerate the wrapper while its remaining `Landmark`/`Entered` transpilers still need those fields (`VistasWorldGen.cs:205`, `:221`). That produces the observed null field operand. Clearing `_assembly` also disables the special overloaded-field resolver while teardown still needs to copy Vista methods.

`Core.Patches.Unpatch` catches the exception and clears its diagnostic records anyway (`src/Core/Harmony/Patches.cs:119`), so a clean-looking WW patch list would not establish successful removal. The runtime Compatibility switch uses this same stop path; damage during a live toggle is a code-backed risk, not something exercised by these generation runs. Do not interpret this as an initial-install failure: the compatibility-ready line and both facts/banner providers succeeded.

Suggested scope for a separate fix: preserve reflection metadata and the resolver through removal of all dependent patches, then clear state. Verify quit and a bridge off/on cycle. No fix applied here.

### 2. Confirmed policy mismatch: Vista engine heat bypasses WW's raider heat control

WW's `RaidersOverheat=false` path calls `IVehicleEngineHeat.Hold`; its implementation only zeroes native `heatValue` FSM floats (`src/Modules/Patrols/Crews/Crew.Overheat.cs:10`, `src/Modules/Vehicles/Crews/Crew.Overheat.cs:24`). Vista independently adds `EngineHeat` to every discovered `VehicleController`, including WW cars (`decompile/A/U.cs:714`). Its separate `_temperature` drives a power modifier at 105°C and native `EngineComponent.StopEngine()` at 140°C (`decompile/ApocalpyterWorldAmp/EngineHeat.cs:82`, `:135`, `:185`). WW's control never touches that component.

Therefore switching off raider overheating in WW does not switch off Vista overheating. A long hill/stationary fight can still derate or stop traffic. These short generation runs do not reproduce an actual heat seizure.

With optional Engine Sim there is an additional unresolved ownership mismatch: Vista reads NWH `IsRunning/generatedPower` and directly stops the NWH engine; WW deliberately routes Engine Sim ignition through `IVehicleIgnition` and infers running from coupling/key/RPM (`src/Modules/Compatibility/EngineSim/EngineSimBridge.cs:236`). Engine Sim was absent from this sandbox; no triple-mod runtime result is claimed.

### 3. Code-proven conflict: Vista elevation tiers can change WW's exact enemy budgets

WW's shared spawner implementation promises an exact configured count and fixes the native `RandomInt` min/max to it (`src/Modules/Loot/Spawning/NativeSpawners.cs:94`, `:107`). Vista's `SpawnerTierPatch` then post-processes `RandomInt.OnEnter` in `ItemSpawner/trigger` whenever the result is `numberEnd`; it classifies by spawner name and adds the elevation/region bonus, without excluding WW-managed town/gas-station/custom POI spawners (`decompile/ApocalpyterWorldAmp/SpawnerTierPatch.cs:83`).

Native spawn-point caps still apply; this is a population/budget ownership conflict, not evidence of an array overrun. WW zero-count paths generally park the FSM in `spawned` and are not automatically revived by the postfix. Positive configured counts can be increased. No eligible WW town spawner was deliberately triggered by this suite; runtime quantity validation remains outstanding.

### 4. Code-proven shared-property conflict: Performance far-clip cap versus Vista ridge visibility

WW's Performance preset caps supported cameras at 6,000 m and reapplies every second (`src/Core/Optimization/Rendering/FarClip.cs:65`). Vista `a.v.A()` raises the current camera's far clip every LateUpdate (`decompile/A/V.cs:462`), using `clamp(2500 + Terrain.Height * 2.4, 4000, 9000)`. The staged 1,500 m setting requests 6,100 m: Vista raises the value WW just capped. At 2,400 m it requests 8,260 m.

The two settings cannot both own the final value. This is conditional on the WW cap being active; Vanilla leaves it alone. No FPS or visual-popping claim is inferred from these tests. Vista also adjusts draft material/base-map/pixel-error/instancing while WW TerrainDetail writes some of those properties (`decompile/A/J.cs:233`; `src/Core/Optimization/Rendering/TerrainDetail.cs:119`), a lower-priority ownership overlap rather than proof of a persistent loop.

### 5. Terrain/road integration gaps need separate coverage

Current town flattening is intentional: Denis explicitly superseded natural-ground-only mode in `notes/poinav-performance-step3-town-pads.md`. `TownGroundPad` bounds its owned mask within one tile and restores it after the captured Vista road-grading callback. It should not be blamed for the old starting-tile scale mismatch merely because older compatibility notes say terrain writes are off. The diagnostic text `terrainWrites=off` is now stale (`src/Modules/POI/Towns/TownsModule.Diagnostics.cs:32`).

Two remaining risks are visible in the code:

1. Towns plan during MapMagic finalization using whatever roads have been published at that moment (`TownsModule.cs:230`, `TownPaths.cs:31`). Vista roads continue arriving asynchronously afterward; its route builder has no WW town-footprint exclusion. A later road can intersect a town that was correctly clear when selected. Replaying the foundation preserves the buildings but does not reconcile the road's desired grade with the town pad. No such intersection is demonstrated by these runs.
2. WW recaptures traffic heights after `A.S.A(q, MapMagicObject, TerrainTile, Terrain, TerrainData, A.S+A, int, float)`. Vista has another live terrain writer in `A.m` road cleanup (`decompile/A/M.cs:535`) which does not pass through that hook; it is active in Add/Off rather than Replace mode. Town pad replay and already-published traffic/NPC terrain data have no explicit notification from that writer. The tested config is Replace, so this specific path is not runtime-covered. Actual stale routing or buried geometry is not asserted.

Town access roads remain intentionally disabled with Vista. `CanTravel` requires an access path (`src/Modules/POI/Towns/TownsModule.TownsApi.cs:43`), so road-dependent town bus/convoy visits remain unavailable. Generic added POIs still skip graph `Flatten200` insertion under Vista (`src/Modules/POI/PoiModule.cs:157`); War Rig physical placement independently requires a clear, sufficiently flat footprint. Reduced placement opportunities on rough terrain are an expected limitation.

### 6. Code-proven height-range limit in hired-follower/giver recovery

`Core.WorldGeometry.GroundAt` always casts downward from absolute scene Y=1500 (`src/Core/Game/WorldGeometry.cs:13`). Vista supports terrain height up to 2,400 m. At a real terrain surface above Y=1500, that ray starts beneath the surface, so it cannot find the upper ground as intended. The callers are missing-giver recovery (`src/Modules/Quests/Takeover.cs:165`) and hired-follower catch-up (`src/Modules/Quests/Hire.Follower.cs:474`): recovery can keep treating loaded high ground as missing or fail to move the follower.

This is conditional on actual world-space surface height, not simply the terrain height-scale setting. The generation suite retains the staged 1,500 m setting and does not reproduce this high-altitude case. Most traffic/Vantage probes already cast relative to the candidate/player from much higher above; do not generalize this limitation to all ground checks. A repair to the shared helper would require Core scope approval under the repository rules.

### 7. Smaller presentation/fallback differences

- Vista's original heat overlay begins at 92°C and shows the numeric temperature. WW suppresses it whenever its notifications provider is enabled, but sends no replacement before 105°C (`VistasEngineHud.cs:46`; `decompile/A/U.cs:765`). Thus the early 92–104°C heads-up disappears. The replacement uses the nearest selected heat component, as the original does; it is not additional proof that the player occupies that car.
- Vista summit scouting and its near-landmark path are suppressed unconditionally by the bridge (`VistasWorldGen.cs:174`). The replacement belongs to the HUD module (`src/Modules/HUD/HudModule.Clean.cs:95`). Disabling the whole HUD module does not restore Vista scouting while Compatibility stays enabled. This is separate from the Clean HUD style switch, which leaves the module's discovery job running.
- Vista's numpad-minus overlay toggle overlaps the default WW pose-delete binding. Overlay defaults off under WW, reducing the practical effect; deliberately enabling it restores the overlap.
- Vista place journals are seed-wide; WW fog/location discoveries are per slot. They intentionally do not form a shared discovery/save system. Procedural Vista landmarks do not automatically become native `mapIcon` entries.

## Existing protections that still match this DLL

- Initial bridge activation and its obfuscated signatures resolve on 1.0.7; overloaded-field resolution is needed and works on install.
- Replacement road polylines are captured, and route-cache identity includes the Vista assembly MVID and relevant generation settings. Vehicle route certification uses final sampled terrain, not an unchecked fallback.
- WW weather gates Vista dust/haboob eligibility and transitions only while `IWeatherRegions` exists; ordinary Vista weather, atmospheric tint and special effects remain. Native fog is scoped/restored around WW camera rendering. No forced storm or long weather-cycle test was performed.
- Native POI object/floor/spline placement still runs through Vista's floor patches; WW does not replace those patch targets. No direct terrain-layer/tag rewrite was found that would invalidate WW's terrain physics mask.
- Native POI navigation checks real terrain and rebakes an incompatible instance instead of assuming the saved template matches (`src/Modules/NPCAI/Navigation/Grid/Nav.Terrain.cs:21`). That does not establish automatic rebaking after every later terrain edit.
- No Vista save-format replacement, WW class/prefab-ID rename, new serialized game type, external DLL modification or redistribution was introduced by this review.

## Programmatic world/map checks

Six independent processes completed: two WW+Vista starts with seed 12345, two with 666, one with 8675309, and a Vista-without-WW control with 666. Native `isPlay`, requested graph seed, 1,500 m height scales, settled map rendering and fog-disabled checks passed in all six.

Each run measures boundaries at the denser side's heightmap resolution, records maximum/mean height disagreement and boundary hole counts, verifies all four starting borders and generator height scales, and samples again after 40 seconds of live Vista grading. The audit threshold is 0.25 m. Native map textures are preserved as PNGs; nonblank rendering and absence of an active fog overlay are checked separately.

| Seed / setup | Largest settled full-detail border difference | Largest difference over all 40 active borders | Opened map |
|---|---:|---:|---|
| 12345 / WW+Vista, two starts | 0.000244 m | 19.181460 m | Rendered; visible full-detail joins pass |
| 666 / WW+Vista, first measured start | 4.806900 m | 25.155640 m | Rendered; border criterion fails |
| 8675309 / WW+Vista | 0.000885 m | 10.506290 m | Rendered; visible full-detail joins pass |
| 666 / WW+Vista, raw-buffer repeat | 0.002075 m by world-position query; **0 m in aligned raw buffers** | 25.155640 m | Rendered; visible joins pass on this repeat |
| 666 / Vista, **WW DLL removed** | 4.806900 m by world-position query; **4.806808 m in raw buffers** | 25.155640 m | Rendered; border criterion fails |

The 666 failure is at main tile `(-1,0)` east edge versus main `(0,0)` west edge, sample 1 of 1025: 4.8828125 m from their southern corner. The control reports 57.908510 m versus 53.101690 m with aligned interpolation; raw edge buffers confirm the same approximately 4.8068 m difference. It persists at the settled sample. It is intermittent across starts, but it is **not merely a world-coordinate interpolation artifact** and WW is not required for it to occur. Exact ownership among Vista's border/road operations and native welding has not been proven; no speculative seam fix was attempted.

The broader 10–25 m maxima are on distant draft/full-detail or draft/draft joins; the raw control also confirms mismatches at common draft vertices (maximum 12.680820 m). Those larger outer mismatches reproduce without WW as well. The strict all-active-terrain test fails even on seeds whose opened map view contains only well-matched full-detail tiles. This is why successful map rendering alone is not recorded as a seamless world.

The initial pre-open camera projection is not an authoritative visible-map test because the native map camera has not followed the player yet. The settled camera covers 12 full-detail joins in these runs; its measurements and rendered texture are used for the map-view result. In the Vista-only control, the first `IsCreated` check ran before explicit `Camera.Render` allocated its target and failed; actual first readback and the settled allocation/render/readback all succeeded. That initial allocation check is a driver-order limitation, not a claimed compatibility defect.

Every surveyed boundary strip reported zero missing hole cells. Starting terrain briefly retains the baked 600 m draft during loading, then the ready active tiles all match Vista's 1,500 m generator scale; the older persistent 600/1,500 m scale mismatch was not reproduced. Seed 8675309 also constructed town `v1/8675309/1/0`, radius 151.9 m, while its settled full-detail joins remained within 0.000885 m. This covers one actual town foundation, not every town/road arrangement.

All five WW runs logged the same null-field teardown error; the Vista-without-WW control had no BepInEx error headers. All player processes exited by the driver. Ten original live ES3 files retained their SHA256 hashes. Live WW/Vista MD5s remained exactly those recorded above. The temporary sandbox driver DLL is disabled after the audit; the sandbox WW DLL was restored after the control. There was no live deployment, commit, push, release, issue closure or board status change.

Raw evidence:

- [Summary JSON](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/summary.json)
- [Original-save and live-DLL integrity](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/final-integrity.json)
- [Seed 666 with WW, report](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/ww-vista-final-seed666/report.txt)
- [Seed 666 without WW, report](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/vista-only-seed666-raw/report.txt), [edge buffers](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/vista-only-seed666-raw/edges.tsv), [individual outliers](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/vista-only-seed666-raw/edge-outliers.tsv)
- [Rendered map, seed 12345](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/ww-vista-final-seed12345/map-settled.png), [seed 666](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/ww-vista-final-seed666/map-settled.png), [seed 8675309](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/ww-vista-final-seed8675309/map-settled.png)
- Each run folder contains Player/BepInEx logs, Vista trace, parameters/hashes and both handoff/settled map textures. The temporary driver and runner scripts remain outside the repository in the audit directory.

For a later manual reproduction: use Vista 1.0.7 with Terrain.HeightMeters=1500, Roads.Network=Replace, Seams.CrossDetail=true and the staged settings; start a new seed 666 world, turn map fog off and allow grading to settle. Inspect all four starting borders, especially the first height sample near the south-west corner. A fresh start may not show the defect, so a single clean start does not refute the captured control. For the independent WW defect, exiting with the bridge loaded produces `Invalid argument for ldfld NULL` and `[Compatibility.VistasWorldGen] unpatch failed`. Runtime bridge toggling should be tested only as part of a separately scoped teardown repair.

No exhaustive streaming, saved-world regeneration, town construction, Engine Sim, Add-road mode, weather-cycle or handling-performance acceptance is implied by these bounded new-world tests.

## Approved implementation of findings 3 and 5

Denis authorized suppressing Vista's spawn bonuses and placing towns only after Vista finishes its roads, with native Vista connectors from towns to that completed network. He explicitly approved the additional optional Core contract and changes in Compatibility and POI beyond the normal one-module/50-line limit.

- Compatibility skips Vista's own `SpawnerTierPatch.Postfix`; it leaves the native `RandomInt.OnEnter` and WW population budgeting paths intact.
- The optional `IWorldGenRoads` contract leaves the existing frozen `IWorldGenFacts` interface unchanged. Compatibility resolves Vista's actual metadata names and typed fields; decompiler-generated `m_A` aliases are not runtime field names.
- POI captures immutable tile bounds/avoidance points at finalization, then waits after terrain application. Before siting, Compatibility requires MapMagic generation to finish, Vista's spawn track to finish (when enabled), requested road batches to complete and road workers to become idle, and each currently active main tile's painting/grading flags to match its generation and current intersecting-road revisions.
- POI snapshots the finished terrain in small row batches and plans towns on a worker. Sites must have a safe connection to the completed Vista network. The connector leaves the exterior parking apron, travels outside the town footprint, avoids native POIs and rejects excessive grades or terrain-border crossings.
- Compatibility registers the connector through Vista's native polyline/spatial-index implementation, then invokes its native road-bounds/revision invalidation. POI waits for native painting/grading again before clearing only generated scenery, preparing the foundation and constructing buildings. Town loading holds include these pending decisions.
- Saved towns retain their XZ positions. Their connector can be recomputed against the regenerated network; if no safe route exists they remain in place with a warning rather than being relocated. Access paths use the existing save representation and traffic API.
- No production config entries, dependency DLLs, save keys or format versions changed.

Remaining incompatibility comment: completion covers the currently loaded terrain, not all future streamed terrain. Later road settings changes or newly streamed roads can still affect saved town surroundings. Vista's separate Add/Off cleanup writer remains outside the existing terrain-edit notification, and the independently reproduced Vista-only seams remain. The code comment beside `VistasRoads.Ready` records this boundary. The bridge teardown, engine-heat ownership, far-clip and high-altitude recovery findings are separate outstanding work.

Follow-up verification uses the same isolated real-player runner, native map rendering and fog-off checks. Failed setup/stress iterations are retained separately (`fix1`, `fix2`); they are not acceptance results. The completed `fix3` series uses normal 20% town chance:

| Seed | Native gameplay / fog-free rendered map | Built towns / native connectors | Connector vertices | Settled full-detail / visible-map maximum |
|---|---|---:|---:|---:|
| 8675309 | Pass | 1 / 1 | 80 | 0.000885 m |
| 12345 | Pass | 0 / 0 | — | 0.000244 m |
| 666 | Pass | 1 / 1 | 77 | **4.806900 m; raw buffer 4.806808 m** |

All three directly instrumented Vista's spawn-bonus method and verified that its original body was skipped. All deferred town decisions completed; both accepted towns retained access paths after construction. Logs order `Vista roads ready; town siting begins` before `Vista road connector registered` before `foundation prepared before buildings`, then native loading reaches `isPlay`. Terrain scales all matched 1500 m and boundary strips had zero hole cells. The all-terrain maxima remain 10.506290 / 19.181460 / 25.155640 m respectively, on the previously identified native/Vista joins. The suite deliberately reports these seam failures (3 / 3 / 6 failure lines); it is not an all-tests-pass claim. The prior teardown error also remains in all three exits.

Runtime-verified WW MD5: `8E025AAFB7B34A61B18B4E72D2EABA1C`. Final build/deploy MD5: `28F0E22DA3F88C85A2B47DCAED063846`, zero warnings/errors. The final build differs only by closing the timing-log work item for a no-town decision and indentation in the edited planner block; the road/town behavior is the runtime-tested implementation. Ten original ES3 SHA256 hashes remain unchanged, Vista's MD5 remains `2E39891C9486E54806645AEADAF6D33A`, sandbox config was restored and the verification plugin is disabled. No production configuration was changed and no commit/push was made.

Evidence: [follow-up summary](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/fix-summary.json), [integrity](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/fix-integrity.json), [8675309 report](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/fix3-seed8675309/report.txt), [12345 report](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/fix3-seed12345/report.txt), [666 report](V:/Repos/Apocalypter/_staging/ww2-vistas-audit-20261024/Game/VerificationRuns/fix3-seed666/report.txt). Each run directory also contains the real map PNGs, aligned edge buffers, BepInEx/Player logs and startup hashes.

Optional manual retest: start a new world with seed 8675309 and the same Vista 1.0.7 settings, turn map fog off and open the native map after loading. Expect town `v1/8675309/1/0`, a native connector, and the three ordered log messages above; its 80-point connector was confirmed in the programmatic run. Seed 666 remains a seam reproduction, not an acceptance target for seam removal. Streaming, Add/Off cleanup, saved-world reload and actual vehicle driving along connectors still require separate acceptance; none is implied by these new-world checks.
