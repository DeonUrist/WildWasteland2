# Research C — Engine-side performance audit (rendering, lighting, terrain, physics, vehicles, GC)

Static audit 2026-10-15 for `notes/plan-Performance.md`. Unity 2020.3.49 Mono, built-in RP, Forward + HDR, PPv2, Azure[Sky], MapMagic 2, NWH VP2. Savings are estimates (GTX 1060 / RX 580-class GPU, 1080p, 6-core CPU), not measured.
Shorthand: `S/` = V:\Repos\Apocalypter\Sources\, `H/` = S/data/hierarchy/, `C/` = S/code/.

## 0. Corrections and new findings
| Claimed earlier | Verified | Source |
|---|---|---|
| Azure renders a reflection probe every 0.1 s | No: `state=1` = `AzureReflectionProbeState.Off` (On=0, Off=1) and the probe GO is inactive; only 5 `RenderSettings.ambient*` writes/frame | H/scene1_game/Azure_Sky_ Dynamic Skybox.txt:L5,L11; C/Assembly-CSharp/UnityEngine.AzureSky/AzureReflectionProbeState.cs |
| Beautify is used | Not instantiated; live post-FX = PPv2, PostProcessingUltimate (PPU), AzureFogScattering | S/data/script_usage.tsv |
| Terrain casts shadows | `shadowCastingMode=0` already | H/scene1_game/MapMagic.txt:L2 |
| Tree distance matters | Graph has no TreesOutput (5 GrassOutput, 11 ObjectsOutput) | S/data/mapmagic_graph_MyDesert.md |
| Physics manager | autoSyncTransforms=false, sleepThreshold=0.005, AutomaticBoxPruning, reuseCollisionCallbacks=true, queriesHitBackfaces=true | globalgamemanagers |

New findings:
- **TAA does no anti-aliasing**: `PostProcessLayer.antialiasingMode=3` with stationaryBlending=0, motionBlending=0 on PlayerCamera and all 14 vehicle 3rd-person cameras → output is only jitter (shimmer) while paying motion vectors + resolve (H/scene1_game/PlayerCameraHolder.txt:L9, H/prefabs/R/Rustallion.txt:L63).
- **PPU ScreenSpaceShadows on by default at intensity 0**: 3 full-screen blits + 2 screen-size temporary RTs per frame + `Shader.Find` every frame (C/Assembly-CSharp/DawidMoza.PostProcessingUltimate/ScreenSpaceShadowsRenderer.cs; asset sharedassets1:103130; toggle FSM S/fsm/scene1_game/Graphics_Canvas.md:L174-190, default true).
- **PPU Sharpen** allocates an unused screen-size temporary RT every frame (SharpenRenderer.cs).
- **Pointless shadowed lights**: headlight spots range 200 m (one 300 m) with shadows (H/prefabs/H/Headlight.txt:L17); gauge lights (0.15 m) and headlight bulbs (0.1 m) are shadowed point lights (H/prefabs/G/gauge_rpm.txt:L19); muzzle-flash point lights shadowed (16 in scene weapon models + prefab).
- **Vehicles fully simulated out to 2000 m** (DistanceKinematic, S/fsm/prefabs/Rustallion.md:L256-271); NWH `WheelController.Step` never checks sleep (C/NWH.WheelController/NWH.WheelController3D/WheelController.cs:L637-745) → parked cars never sleep.
- **MapMagic**: autoMaxThreads = processorCount−1 (C/Den.Tools/Den.Tools.Tasks/ThreadManager.cs:L133-141); objectsNumPerFrame 500 (MapMagic.txt:L2 globals; ObjectsOutput.cs:L231); 5 km tiles at res 1025 (~1057² with margins) → many 4.5 MB `float[]` per main-tile generation (cleared after finalize, TerrainTile.cs:L625-628, but the Mono heap never shrinks) — most likely driver of the ~400 MB heap and periodic full GCs.
- **Azure WeatherController boxes ~2200 values/s**: FixedUpdate (50 Hz) writes 44 override properties via reflection SetValue (AzureWeatherController.cs:L115-118, L290-546).
- **Floating-origin jump hitch** (threshold 1000 m, H/scene1_game/Player.txt): each jump runs `FindObjectsOfType<T>().ToList()` three times (Rigidbody ×2, ParticleSystem), moves every scene root, `Physics.SyncTransforms()` (C/NWH.Common/NWH.Common.FloatingOrigin/FloatingOrigin.cs:L40-80); side effect: after the first jump every Rigidbody gets sleepThreshold 0.14.
- **Textures**: 38 at 4096² (709 MB with mips; 3 4K maps per each of 9 vehicle bodies), 206 at 2048² (673 MB); streaming mipmaps off on all 1354 (can't enable at runtime); Ultra `anisotropicTextures=ForceEnable` (S/data/settings.md:L170).
- **The game overwrites settings**: graphics menu `QualitySettings.SetQualityLevel(i)` resets every quality knob (C/Assembly-CSharp/AkshayDhotre.GraphicSettingsMenu/GraphicSettingHelperMethods.cs:L17-20); `SetClippingPlanes` FSM sets farClipPlane=10000 three seconds after start (S/fsm/scene1_game/PlayerCameraHolder.md:L660-668) → re-apply after both.

## 1. Inventory
**Lights** — 178 prefab + 27 scene, all render mode Auto; shadow resolution 129 quality default, 36 Medium, 13 Low.
| Group | Count | Type/range | Shadows | Where |
|---|---|---|---|---|
| fire_effect | 43 | point 2 m | hard | barrel fires, Camp_* |
| Light_Flicker (+_2) | 53 | point 1.5–15 m | hard | camps, lamps, tin cans; 58 LightFlicker FSMs PerlinNoise→SetLightIntensity every frame |
| "Light" range 10 | 46 | point 10 m | hard (Medium/Low) | Camp_7 19, Camp_14 9, Building_3 7, Camp_13 4 |
| "Light" 0.1–4 m | ~30 | point | mostly hard | gauges, bulbs, interiors |
| muzzle_flesh_effect | 17 | point 2 m | hard | weapon models |
| Spot Light | 4 | spot 200/300 m (prefab), 10 m (scene) | hard | headlights, flashlight |
| Sun | 1 | directional | soft | Azure Directional Light |
Point shadows = 6 cube faces; Forward + pixelLightCount 20 → each light adds a pass per lit object. Camp_7 up to 19×6 = 114 shadow passes + 19 additive passes, day and night (intensity 0.7–1.5).

**Renderers** — 11,321 prefab MeshRenderers, 11,041 cast shadows. Casters by mesh half-extent: <0.1 m 1,162; 0.1–0.25 m 1,667; 0.25–0.5 m 2,778; 0.5–1 m 2,046; 1–3 m 2,390; ≥3 m 854 (~51% under 1 m). 10,464 on Default layer → per-layer culling can't separate props from buildings. LODGroups: 60 prefab + 24 scene, all single-level cull-only (screen heights 0.004–0.018); with lodBias 2 a 10 m building renders to ~3 km; most props limited only by the 10 km far plane. 164 SkinnedMeshRenderers all cast shadows.

**Post stack on PlayerCamera** — 5 global volumes (H/scene1_game/__PostProcessing__.txt): AO (MSVO Medium), Bloom (intensity 7 + dirt), HDR ColorGrading, PPU Sharpen 0.4, PPU ScreenSpaceShadows (0); plus TAA, stopNaN, AzureFogScattering `OnRenderImage` forcing a depth texture (full depth pre-pass in Forward).

**Particles** — 454 prefab, 95 scene; culling 286 Automatic, 125 AlwaysSimulate, 43 PauseAndCatchup; 105 prewarm (55 Fire Quad, 16 each Smoke/Ashes/Flames Small → every POI fire prewarms on spawn); 24 world collision High; 7 Lights module.

**Animators** — prefabs 47 CullUpdateTransforms, 12 AlwaysAnimate; scene 32 AlwaysAnimate (arms/weapons — fine).

**Physics** — 875 prefab Rigidbodies (399 CCD), 819 non-convex MeshColliders; fixed 0.02, maximumDeltaTime 0.333 (up to 16 steps after a slow frame); LockPhysics re-adds a Rigidbody with Continuous CCD on every pickup.

**NWH** — 17 VehicleController prefabs, LOD 20/50/∞ m (scriptable_objects.txt:L49-56); Powertrain, WheelComponent, GroundDetection, WheelSkid, TireNoise lodIndex −1 = always on (S/data/assets_full/StateSettings__DefaultStateSettings.json); per wheel 1 SphereCast/step + up to 9 more (StandardGroundDetection.cs:L22-86); no substeps.

**Audio** — 458 sources (172 playOnAwake, 182 looping, 109 maxDistance >200); 32 real voices; engine sound already uses PauseSlowly.

## 2. Ranked optimizations
**Application method ("prefab pass")**: on scene load walk prefab assets (`Resources.FindObjectsOfTypeAll<T>()` with `!go.scene.IsValid()`) and edit in memory → every later spawn inherits (MapMagic pool, CreateObject, ES3 loads); then scene instances; re-run the scene part on world ready and on each `TerrainTile.OnTileApplied` for that tile's Objects.

### Tier 1 (big, low risk)
1. **Light shadow policy** — est. 2–8 ms GPU, 1–4 ms CPU in POIs. Prefab defaults: shadows off on every point light and on spots with range <1 m; headlights 60–80 m no shadow (optional shadow on player's car only, shadowCustomResolution 512). LightBudget job every 0.25 s: shadowed (256 px) = nearest 2 (Balanced)/4 (Max) point lights within 25 m with range ≥3. Distance cull: beyond max(60, 12×range) (Low) / 20×range (Balanced) disable the light + its LightFlicker FSM; never destroy (Bodypart FSMs reference them). pixelLightCount 4/6/8. Low: vertex lighting for range ≤2.
2. **Dead post-processing** — est. 0.5–1.5 ms GPU, no visual loss. Deactivate `__PostProcessing__/PP screen space shadows` (re-apply after its toggle FSM) or prefix `ScreenSpaceShadowsRenderer.Render` to copy source→dest when intensity ≤0.001; prefix `SharpenRenderer.Render` without the unused RT and with cached Shader.Find; TAA → SMAA (FXAA in Low) on PlayerCamera and every vehicle 3rdCamera/Camera (or if kept, blends 0.95/0.85); stopNaN off in Low/Balanced.
3. **Shadow caster culling** — est. 1–3 ms CPU + 1–3 ms GPU. Sun shadows 200→120 (Balanced)/80 (Low); 4→2 cascades split 0.2; VeryHigh→High. Small casters: shadows off below world half-extent 0.15 m (~25% of casters) or 0.35 m in Low (~45%), excluding car frames. Sun `layerShadowCullDistances`: Item 30, Food 20, Weapon 20, Hinge 40, Actor 60. skinWeights 2 (Low/Balanced).
4. **Size-based render culling** — est. 2–6 ms CPU (3×3 main tiles = 15×15 km of objects). Inject cull-only LODGroups on prefab roots without one (one LOD with all renderers; screen height 0.012 for <1 m, 0.006 for 1–5 m, 0.003 large; no fade; RecalculateBounds; skip vehicles, anything under the player camera, weapons, roots with nested LODGroups). lodBias = view-distance slider 0.8/1.2/2.0. Camera `layerCullDistances` (spherical): Item 150, Food 80, Weapon 150, Hinge 300, Actor 600, Car 2500, Default 0 (left to LODs). Far plane Low 6000 with Azure globalFogDistance 5500, applied after SetClippingPlanes (≥3.5 s after world ready).
5. **Vehicle simulation LOD** — est. 0.1–0.3 ms per parked vehicle per fixed step (1–4 ms with patrol/raider mods), medium risk. DistanceKinematic threshold 2000→250 m (150 m Low) in both FloatCompare states; while kinematic and not driven: `WheelController.AutoSimulate=false` + disable VehicleController (hook `SetIsKinematic.OnEnter` filtered to that FSM; never the player's car). Shared `StateSettings` before vehicles init: WheelSkid/TireNoise lodIndex→1, SurfaceParticle/ExhaustSmoke→0, LOD distances 15/40 m. Keep fixedDeltaTime 0.02.

### Tier 2
6. **MapMagic smoothing** — removes 20–200 ms tile-crossing spikes, lowers peak heap/GC count. `ThreadManager.autoMaxThreads=false`, maxThreads = clamp(cores/4,1,3); prefix on thread entry (ThreadManager.cs:L160) → BelowNormal priority; objectsNumPerFrame 500→40 (25 Low); `CoroutineManager.timePerFrame` 3→2 ms. Do **not** set mainRange 0 (tile distance counted in whole tiles: TileManager.GetRemoteness, TerrainTile.cs:L263 → 5 km neighbours would become objectless drafts). Tile size can't change at runtime.
7. **Terrain** — est. 0.5–2 ms GPU. Set `mapMagic.terrainSettings`, call `mapMagic.ApplyTerrainSettings()` (no regen, MapMagicObject.cs:L195), hook `TerrainTile.OnTileApplied`. pixelError 1→5 (Low 8, Max 3); baseMapDist 1000→600 (350/1000); detailDistance 100→70 (50/100); detailDensity 1→0.8 (0.6/1).
8. **Particles** — removes POI spawn hitch, ~0.2–1 ms. prewarm off (105); looping systems without sub-emitters → Pause (PauseAndCatchup for fire/smoke); collision quality Low; Low: maxParticles ≤200; Lights module off (7).
9. **Animator/IK culling** — ~0.2–1 ms. NPC root Animators → CullUpdateTransforms; FastIK LateUpdate prefix skipping invisible or >40 m.
10. **Physics** — 0.3–2 ms + no slow-frame spiral. `Time.maximumDeltaTime=0.1`; `Physics.sleepThreshold=0.14` at load; 1 s sweep: resting/slow items (<2 m/s) → ContinuousSpeculative, Continuous only >8 m/s; keep queriesHitBackfaces; floating origin BeforeJump/AfterJump with cached lists instead of FindObjectsOfType.
11. **Azure** — ~60–100 KB/s less garbage, 0.05 ms per fixed step: WeatherController.FixedUpdate every 10th call (5 Hz; transitions take 7 s); TimeController.FixedUpdate every 2nd.
12. **Textures** — up to ~1.2 GB VRAM: Low `masterTextureLimit=1`; aniso Enable in Low, ForceEnable with global limits (2,8) in Balanced. Uncompressed/readable textures (65 MB/16 MB) are an asset problem.
13. **Audio** — small: keep 32 real voices (changing needs AudioSettings.Reset which cuts all sounds); looping ambient sources maxDistance >200 → 120, priority 200.

### Tier 3 — GC
Engine-side allocators: MapMagic matrices (§6), Azure boxing (§11), FloatingOrigin FindObjectsOfType().ToList() (§10), NWH LOD coroutine `new WaitForSeconds(0.2)` 5×/s per vehicle (minor); Micosmo sensors use pooled lists (fine). PlayMaker likely the largest allocator overall (research A/B). GC settings: incremental slice 3→1.5 ms (Balanced)/2 ms (Max); watch `GC.CollectionCount(0)` vs GC Used Memory; cut allocations rather than tune the slice. Never Disabled/Manual GC mode (heap peaks during tile generation; Mono never shrinks).

## 3. Presets
| Knob | Low | Balanced (default) | Max |
|---|---|---|---|
| Point-light shadows | none | 2 nearest ≤25 m @256 px | 4 nearest ≤35 m @512 px |
| Light cull distance | max(60, 12×range) | 20×range | none |
| pixelLightCount | 4 | 6 | 8 |
| Headlight | 60 m, no shadow | 80 m, shadow player's car only | 120 m, shadow player's car only |
| Sun shadow distance / cascades / res | 80 / 1 / Medium | 120 / 2 / High | 160 / 4 / VeryHigh |
| Small-caster shadows off below | 0.35 m | 0.15 m | 0.08 m |
| Injected LOD + lodBias | 0.8 | 1.2 | 2.0 |
| layerCullDistances | ×0.6 | §2.4 table | ×1.5 |
| Far clip / fog | 6000 / 5500 | 10000 / 9000 | 10000 / 9000 |
| Anti-aliasing | FXAA | SMAA Medium | SMAA High |
| AO | off | MSVO Low | MSVO Medium |
| Bloom | fastMode | fastMode | vanilla |
| PPU SSS / Sharpen | off / off | off / fixed | off / fixed |
| Terrain pixelError / basemap / detail dist / density | 8 / 350 / 50 / 0.6 | 5 / 600 / 70 / 0.8 | 3 / 1000 / 100 / 1 |
| MapMagic threads / objects per frame / apply ms | 1 / 25 / 2 | cores/4 (max 3) / 40 / 2 | cores/4 (max 3) / 80 / 3 |
| Particles | no prewarm, Pause, max 200 | no prewarm, Pause | no prewarm |
| NWH kinematic distance (wheels off) | 150 m | 250 m | 500 m |
| maximumDeltaTime | 0.1 | 0.1 | 0.15 |
| skinWeights | 2 | 2 | 4 |
| masterTextureLimit / aniso | 1 / Enable | 0 / Force (2,8) | 0 / vanilla |
| GC slice | 1 ms | 1.5 ms | 2 ms |
Re-apply quality settings in a postfix on `GraphicSettingHelperMethods.ChangeQualitySettings`; Shutdown restores every original value incl. prefab-asset edits.

## 4. Measure first
Baselines: A open desert by day; B Camp_7 at night; C 100 km/h across a tile boundary incl. a floating-origin jump; D ≥3 parked cars within 300 m + 2 NPC packs.
1. In-mod telemetry (release player): `Time.unscaledDeltaTime` ring buffer (avg, p95, p99, spikes >33 ms tagged: tile generating, origin jump, save); `FrameTimingManager` (only if the build has Frame Timing Stats — check gpuFrameTime ≠ 0); `ProfilerRecorder.StartNew` for "Draw Calls Count", "SetPass Calls Count", "Batches Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count", "GC Used Memory", "GC Reserved Memory", "System Used Memory", "GC Allocated In Frame" (check `.Valid`; some are development-only; dispose on shutdown; `UnityStats` is editor-only); `GC.CollectionCount(0)`/min, `Profiler.GetMonoUsedSizeLong/GetMonoHeapSizeLong/GetTotalAllocatedMemoryLong`; log at `TerrainTile.OnTileApplied`; Stopwatch around `FloatingOrigin.Jump`; F7 census (shadowed/total lights, awake/asleep Rigidbodies, wheels with AutoSimulate, playing particle systems, MapMagic thread/coroutine queues).
2. A/B toggles: vsync off, targetFrameRate −1, 30 s averages from a fixed pose; halve resolution to tell GPU- from CPU-bound.
3. Ground truth with a development player: copy UnityPlayer.dll and WindowsPlayer.exe (renamed Apocalypter.exe) + PDB from `Unity 2020.3.49f1/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_development_mono/`, add `player-connection-debug=1` to boot.config → Editor Profiler (Shadows.RenderShadowmap, Culling, PostProcessing, Physics.Simulate, GC.Alloc stacks), Frame Debugger, Memory Profiler. Never ship.
4. External: RenderDoc, Nsight, PresentMon for AO, depth pre-pass and point-shadow cube passes.
Questions: shadow-map share of frame at Camp_7 vs desert; GPU cost of PPU SSS + TAA vs SMAA; tile-apply spike vs objects/frame and threads; do full GCs line up with tile generation or PlayMaker; physics time of 3 parked vehicles before/after the kinematic patch.

## 5. Risks
Prefab-asset edits reach every later instance incl. ES3 restores, and other mods see edited values. A renderer can belong to one LODGroup only (skip roots containing one; skip vehicle frames — parts re-parent). CullUpdateTransforms freezes bone-mounted sensors off-screen (verify Bodypart/attack FSMs). NWH hand-off: re-enable VehicleController before isKinematic=false; test on slopes. Graphics menu and SetClippingPlanes overwrite settings → re-apply after them. `ApplyTerrainSettings()` once per preset change, not every frame.
