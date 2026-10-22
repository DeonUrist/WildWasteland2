# W0-B Core utilities - report

Branch `refactor/W0-B`. Only new files were created (no existing file modified): 12 in `src/Core/Util/**`, 12 in `src/Core/Game/`.
Namespace for everything: `WildWasteland.Core` (no sub-namespaces). Build: `sh tools/refactor/compile.sh` succeeds (39 warnings, same as base).
`check_comments.py --package W0-B`: 0 members without a summary. `check_ownership.py W0-B refactor/main`: 0 files outside ownership.
Nothing calls the new code yet: Wave A switches callers and deletes the copies.

## Name resolution (verified by compiling)

Module copies with the same type names (`Json`, `Glb`, `GlbModel`, `Gltf`, `SkinModel`, `Bindposes`, `KeyMap`, `GameBindings`, Quests `Factions`,
Performance `FsmPause`, ...) live in module namespaces, which C# searches before `using WildWasteland.Core`, so no existing file
became ambiguous. When Wave A deletes a copy, its callers bind to the Core type automatically; same member names were kept where
possible (`Json.Parse/Text/Num/Bool/Str/Floats`, `Glb.Load(path, colliderGeometry)`, `Gltf.Load(path)`, `Gltf.Weights`,
`Bindposes.Human`, `KeyMap.Code/FromKeyCodeName`, `GameBindings.Find/Down/Label/KeyName/IgnitionAction`, `FsmPause.Set/Clear/Held/ClearAll/Prune`).
Exceptions that need care: Quests `Factions` (different API: keep it as Quests' file reader, feed `Core.Factions`), Guns/NPC `Wav.Read`
(-> `Audio.DecodeWav`), Rider `Wav.Load` (-> `Audio.TryLoadWav`), `VoiceAudio.LoadWav` (-> `Audio.LoadWav`, same signature),
StoreJson (-> `Json.Object/Write`), NativeAccess/OptionalAccess/FastReflect/BatchCompiler/CompileQueue (-> `Access.*`).

## Per utility: copies replaced, differences found, choice

### Json (`Util/Json.cs`)
Replaces: Towns/GasStation/Json.cs, Survival/StewPot/Json.cs, NPC/Women/Json.cs, PlayerBody/Body/Json.cs, Patrols/WarRig/Json.cs
(identical apart from namespace), CleanHUD/Json.cs, Patrols/Vehicles/CarTemplateFile.cs `Json`, Patrols/Editor/EditorData.cs `StoreJson`.
(developer/Diagnosis.cs `Json(object)` is in the separate developer DLL; Apocasetter MiniJson is reached by reflection and stays.)
- Differences: the 5 glTF copies are strict (no comments, no trailing commas, throw on a truncated `\u`, messages "JSON: ... at <index>");
  CleanHUD and CarTemplateFile are lenient (// comments, trailing commas, messages "... at line N") and add getters (CleanHUD null-safe
  `Text/Num/Bool/Obj/Arr`; CarTemplateFile `Text/Num/Bool` not null-safe, `Floats` = exactly 3 numbers, writer `Str`).
- Choice: the lenient parser. Every document the strict copies accept parses to identical values (Dictionary insertion order, double,
  string, bool, null); only error-message text differs. Getters are null-safe (superset). `Floats` keeps the exactly-3 rule; `FloatList`
  returns any length. Added `TryParse`, `ParseObject`, `Double`, `Int`, `Quote`, `Float` ("0.####" invariant, CarTemplateFile number format).
- Writer: `Json.Object(params)` + `Json.Write(object)` are byte-identical to StoreJson (2-space indent per depth, inline arrays with ", ",
  Convert.ToString invariant, final newline). String parse uses a [ThreadStatic] StringBuilder (no builder per string).

### Glb (`Util/Glb.cs`)
Replaces: Towns/GasStation/Glb.cs (superset), Survival/StewPot/Glb.cs, Patrols/WarRig/Glb.cs.
- Differences: gas-station copy adds `colliderGeometry` (COL* nodes rendered instead of hulls), `DoubleSided`, `NodeNames`.
- Choice: gas-station code verbatim (same vertices, triangle order, mesh name = file name without extension, hull names = node names,
  so the gas-station mesh-name SHA used by NPC nav-bake signatures is unchanged). `colliderGeometry=false` = StewPot/WarRig behaviour.
  Added `Load(byte[] file, string name, string dir, bool colliderGeometry)` for embedded resources (path overload calls it).
  Only change of substance: JSON parsing now goes through Core `Json` (lenient superset, same values).

### Gltf + SkinModel (`Util/Gltf.cs`)
Replaces: NPC/Women/Gltf.cs, PlayerBody/Body/Gltf.cs. Difference: only the module used for the sparse-accessor warning.
Choice: verbatim; `Load(path, owner)` logs "[owner] glTF: sparse accessor ..." (`Load(path)` uses "Core"). `Weights(...)` unchanged.

### Bindposes (`Util/Bindposes.cs`)
Replaces: NPC/Women/Bindposes.cs, PlayerBody/Animation/Bindposes.cs (identical). Added `TryGet(bone, out Matrix4x4)` (rows 0..2 + 0 0 0 1,
the conversion both callers inline).

### Audio (`Util/Audio.cs`)
Replaces: Core/VoiceAudio.LoadWav, Guns/Tracers/Wav.cs = NPC/Women/Wav.cs, Patrols/Crews/Rider.cs `Wav`, Quests/Voice.cs `ReadWav`;
NPC/Women/Level.cs (ActiveRms/Process); the 5 UnityWebRequestMultimedia coroutines (StewPot Stew.LoadSound MP3, Women Voice OGG,
WeatherThunder MP3, WeatherAudio WAV, HUD HeadshotAudio MP3).
- Decoder differences: Guns/NPC = PCM 8/16/24/32 + float 32/64 + EXTENSIBLE, tolerant chunk sizes, trims to whole frames, last `data`
  chunk wins; VoiceAudio = 16-bit PCM only, throws on a truncated chunk; Rider = 8/16-bit PCM, returns null on failure, first `data` chunk,
  no frame trim; Quests = PCM 8/16/24/32 + float 32 (ignores the format tag for ints), first `data` chunk.
- Choice: `DecodeWav` = Guns/NPC verbatim (broadest; identical samples for every file the others accept). Level helpers keep each
  caller's behaviour: `LoadWav(path, name, targetRms)` = VoiceAudio (RMS gain <= 8, 0.7 tanh soft clip, verbose gain log, kept-loaded
  clip; now also reads non-16-bit files that used to throw), `TryLoadWav(path, name, gain)` = Rider (null on failure, clamped gain),
  `NormalizePeak(data, 0.9f, boost)` = Quests, `Gain` = Rider/HUD clamp, `Level`/`Process` = NPC Women Level, `Rms`, `Peak`, `Clip(...)`.
- `LoadAsync(path, AudioType, callback(clip, error), decompress)` returns a cancellable `Audio.Load`; errors "missing" / request error /
  "could not be decoded"; `decompress` sets compressed=false/streamAudio=false (Thunder, Headshot, Women). HideFlags, naming, gain and
  logging stay with the callback (each loader set its own). Runs on the Core runner (`Scheduler.StartCoroutine`).

### Textures (`Util/Textures.cs`)
Replaces the 18 `LoadImage` sites (Core UI Ledger/SaveLibraryPage/Skin; CleanHUD Assets/LoadingScreen/MapIcons/Menu; HUD Crosshair;
NPC Gungirl; Patrols Paint/WarRig Factory; PlayerBody CharacterPortraits/Model/VehicleHud; Quests TalkHook; StewPot Factory;
Towns TownGasStation/TownWalls).
- Differences: each site picks its own format (RGBA32/RGB24/default), mipmaps, markNonReadable, wrap, filter, aniso and hideFlags.
- Choice: `TextureSettings` struct whose zero values are Unity defaults, so every site is expressible exactly (presets `Ui`, `Model`).
  `FromBytes/FromFile` (uncached, caller-owned), `Load(owner, path, settings, reloadIfChanged)` and `LoadResource(owner, resource, settings)`
  (cached per source+settings, negative cache, reload by write time for LoadingScreen/CharacterPortraits), `Forget`, `Release(owner)`,
  `ReleaseAll`. Cached lookups do not allocate (Ledger draws in OnGUI). Release on shutdown needs ModuleHost wiring (request W0B-1).

### Access (`Util/Access/Access*.cs`, one `public static partial class Access`)
Replaces: Core/NativeAccess.cs (`Call<T>(Type, name, params)`, `Get<T>(Type, name)`), Core/OptionalAccess.cs (`Read<T>(FieldInfo)`,
`Call<T>(MethodInfo, result, args)`), Performance FastReflect (all builders, `Batch`, budget 400, `Compiled`), BatchCompiler (`Access.FieldBatch`),
CompileQueue (`Access.Queue`). Code verbatim. Differences: Queue's job is named `<owner.Id>.AccessCompile` (Scheduler naming rule; was
"Performance.Compile") and the Telemetry mark moved to `Access.Queue.OnFirstBatch`. Added `TryGet`/`TryCall` (null instead of throw).
The old classes stay untouched (Wave A turns them into wrappers / switches callers; 30 `Expression.Compile` sites are Wave A/B work).

### KeyMap (`Util/KeyMap.cs`)
Replaces PlayerBody/KeyMap.cs = Quests/KeyMap.cs (identical code). Verbatim. CleanHUD `KeyCatalog` is a UI catalog and not merged.

### GameBindings (`Util/GameBindings.cs`)
Replaces PlayerBody/GameBindings.cs. Difference: `Find` scanned InputStorage.Keys on every call (Down() per frame); Core caches
name -> KeyAction per InputStorage instance / Keys list reference / count (rebuilt when any changes, e.g. IgnitionControl adding its
action). Added `Held`, `Clear`. Same case-insensitive match, first action wins.

### WorldTiles (`Game/WorldTiles.cs`)
Replaces the 7 MapMagic subscriptions (Towns, POI, Performance, Quests QuestWorld, CleanHUD LocationMarkers, Patrols TrafficRoads)
and VistasWorldGen.TerrainEdited consumers. `OnPrepare/OnFinalized/OnApplied/OnMoved/OnEdited(owner, handler)`, `Remove`, `RemoveAll(owner)`;
copy-on-write handler arrays (Finalized runs on a MapMagic worker), per-handler try/catch, worker failures logged on the main thread.
Handoff rule (capture immutable data in Finalized, consume in Applied) is documented on the class. Edited source needs W0B-3.

### WorldIndex (`Game/WorldIndex.cs`)
Replaces the 9 scene walkers (CleanHUD SceneCache, Quests QuestWorld walk, EngineDiscovery, StewPot pot discovery, NPC Nav structure
sweep, Gungirl, Crosshair canvas search, Patrols TrafficLocations, Patrols Persistence). Watches: `WatchName`, `WatchPrefix`,
`WatchComponent<T>`, `Watch(filter)`, `PruneBelow` (vehicle hierarchies); `Found` lists, Added once per object per scene, destroyed
objects swept in slices -> Removed(instanceId); roots from scene load, `WorldTiles` Applied, `QueueRoot`, `Rescan`; cleared on scene
load. One Core sliced job "Core.WorldIndex" (300 us). Module-specific post-processing (CleanHUD per-state key catalog, Quests POI
records, Nav structure kinds) stays in the modules as watch callbacks.

### FsmIndex (`Game/FsmIndex.cs`)
Replaces the 9 full FsmList scans and Quests Game.FindInt/FindFloat. `ByOwner(go)` via `IFsmIndexProvider` (PM1, request W0B-4) or
GetComponents filtered to isActiveAndEnabled (FsmList membership; component order instead of enable order until PM1 registers);
`Track(owner, name)` + `ByName(name)` / `All` from one sliced FsmList pass (256 per step, every 0.5 s, published atomically = QuestWorld
cadence); `Int/Float/String/Bool/Go(fsm, var)`; `Find` = FsmUtil.Find.

### FsmPause (`Game/FsmPause.cs`)
Replaces Performance FsmEdits.FsmPause (verbatim logic) and FS1's direct `ManualUpdate` writes (new fixed bit `FsmSleep = 8`), plus
`Bit(owner, reason)` dynamic bits, `Own`, `ReleaseAll(owner)`, `Paused`, `ClearCopiedFlag` (the two PlayMakerFSM.Awake postfixes).

### PrefabCatalog (`Game/PrefabCatalog.cs`)
Replaces the shared part of Patrols Prefabs (ScanExact: exact name, ES3 manager overrides, case-insensitive fold preferring ES3Prefab),
Quests FindPrefab/ScanForPrefab (asset root with ItemName FSM -> `Item(name)`), Towns PoiPropCatalog/PoiSpawnCatalog and Patrols
Scan inputs (`AssetFsms`), StewPot prefab-id checks (`ById`). Sliced warm "Core.PrefabCatalog" (the two FindObjectsOfTypeAll calls once
per generation), synchronous completion when queried early, invalidated on scene load and by `Refresh` on ES3 manager count change
(Patrols used AddPrefab/Awake postfixes for that; the count check it also had is kept). Domain catalogs (Patrols kinds/display names,
Towns spawn profiles, motorcycle/mod vehicle templates) stay in their modules on top of it. Difference: Quests ScanForPrefab's fallback
to a live scene object (when no asset exists) is not part of the catalog; Quests keeps it.

### ItemSpawner (`Game/ItemSpawner.cs`)
Replaces the 6 save-list registrations (InventorySupplies.Drop, Quests Game.RegisterSpawned, WarRigFeature, Patrols Counter/Registry/Name,
Gungirl.Register, AmmoMechanics.Drop). Differences: numbering - 5 copies increment then use (`Numbering.IncrementFirst`, default), Quests
uses then increments (`UseThenIncrement`); list lookup - exact "ArrayList_Items"/"ArrayList_Cars" vs first name containing "item"/"car"
(Core: exact first, then contains; same proxy in vanilla); Gungirl renamed the object even when the list was missing - `RegisterSaved`
returns false and changes nothing then (use `Name` for rename-only). API: `Counter`, `List(SaveList)`, `Available`, `NextId`, `Name`,
`RegisterSaved`, `Unregister`. Note: Towns "ItemSpawner int get/set" (spawner FSM variables) is a different concern (Loot, Wave B).

### Clock (`Game/Clock.cs`)
Replaces Quests Game.GameHours, Patrols TrafficReinforcements.CallHour and Patrols DayLight. `GameHours` (Quests: -1 when missing, no init
check), `Ready` (TrafficReinforcements' Initialized precondition), `Day`, `HourOfDay`, `Dark` (DayLight verbatim: Azure sun elevation
with 0/0.05 hysteresis, DayTime fallback 6:00-19:30, 2 s re-evaluation, 10 s Azure lookup). Quests' fallback scan of QuestWorld.All for a
DayTime FSM not on __GameManager__ was dropped (the FSM is always there). CleanHUD MessageLog and Performance HudFsms only filter/step
the FSM and are not readers.

### Names (`Game/Names.cs`)
Replaces 6 "(Clone)" strippers, keeping each rule: `Base` (Quests: first "(Clone)", Trim), `StripClone` (SaveCleanup:42, no trim),
`RenderBase` (RenderShared: only when index > 0, TrimEnd), `RemoveClone` (Nav debug: Replace all), `CloneNumber` (CrewHistory/SaveCleanup:145:
LastIndexOf + int.TryParse; CrewHistory additionally requires >= 0). Non-allocating `BaseEquals`/`BaseStartsWith` for walkers.

### Layers (`Game/Layers.cs`)
Named layers (Default 0, IgnoreRaycast 2, UI 5, Player 6, Car 8, Item 9, Actor 10, Door 11, Ground 14, SeeThrough 16, MapIcons 22;
12/13 unnamed) and the repeated masks (BulletBlockers = 19201, SightBlockers, Standable, Obstacles, GunRaycast, Bodies, CameraObstruction),
each bit-identical to the literal it replaces.

### Factions (`Game/Factions.cs`)
New relations service, empty until Quests/Patrols feed it: `Stance`, `Set/SetPlayer` (owned, symmetric), `Relation/ToPlayer/Hostile`,
`IFactionSource` chain for `Of(go)`, `RemoveAll(owner)`, `Revision`. Quests keeps its factions.txt reader and story defaults and publishes them.

### CameraArbiter (`Game/CameraArbiter.cs`)
For the 3 camera writers (Guns Ironsights zoom, CleanHUD Binoculars zoom, PlayerBody ThirdPerson view/FOV). One onPreCull/onPostRender
pair; channels Projection/View/Fov; writers in priority order, first claim wins; non-persistent changes restored in onPostRender exactly
as Ironsights/Binoculars do (ResetProjectionMatrix, ResetCullingMatrix, jitter flag); `persistent` view option for ThirdPerson; hooks the
Unity callbacks only while writers exist. Not used until Wave B; WeatherSky/Dust/Debris/Smoke/Atmosphere only read camera state in
onPreCull and are not writers.

### PlayerState (`Game/PlayerState.cs`)
Superset of PlayerBody Game.cs player queries (Player, CameraHolder, PlayerCamera, Camera, InCar/Movement FSMs, Ready, InCar, MoveState,
JumpState, StandingHeight, Velocity, Dead, CarRoot, CarFsm, DriveUse), Patrols PlayerRef (Car with the InCar.Car / Drive "inCar" /
VehicleController fallbacks, CarRigidbody, GodMode, Target per physics step, Kind, HasVehicleController) and Quests (Transform, UnderPlayer).
Difference: on-foot Target velocity is measured in Origin world space, so no OnOriginShift wiring is needed (Patrols shifted its cached
position on jumps; same result). Weapon/arms/reload/ADS/binocular queries of PlayerBody Game.cs stay for Gunplay/Presentation.

## Performance notes
- No per-call allocation on hot paths: reused buffers (`List<T>` GetComponents), cached FSM variables, per-frame / per-physics-step
  answers, non-allocating cache keys (Textures), copy-on-write arrays (WorldTiles).
- Sliced work through `Scheduler.Sliced` with owner null (Core jobs, as SaveCombat/InventorySupplies do): "Core.WorldIndex" (300 us),
  "Core.FsmIndex" (300 us), "Core.PrefabCatalog" (500 us); each is registered lazily on first use and idles when it has no work.
- Caches cleared on scene load: WorldIndex, FsmIndex, PrefabCatalog, Clock, PlayerState (self-subscribed to SceneManager.sceneLoaded,
  lazily, never during Plugin.Awake work), GameBindings (instance checks), ItemSpawner (instance checks via GameRefs).

## Contracts
None provided or consumed (Core static services; interfaces `IFsmIndexProvider`, `IFactionSource` are Core-internal extension points).
No `[Obsolete]` members (no existing file touched).

## Behaviour changes
None today (no callers). When callers switch: JSON error texts, VoiceAudio accepting more WAV formats, Gungirl not renaming without a
save list, FsmIndex.ByOwner order without PM1, and the Quests DayTime fallback - all listed above.

## Requests (docs/refactor/requests/W0-B.md)
W0B-1 ModuleHost cleanup calls, W0B-2 plugin teardown, W0B-3 Vistas Edited source, W0B-4 PM1 provider, W0B-5 FsmPause switch,
W0B-6 Access.Queue telemetry, W0B-7 compile.sh stale-dll false "COMPILE OK", W0B-8 optional explicit scene-load order.

## Open questions for Denis
1. D12: `KeyMap.FromKeyCodeName` only serves first-run migrations of old KeyCode cfg values; kept for the superset. Delete in Wave C if
   no caller remains?
2. `Audio.LoadWav` (VoiceAudio replacement) now accepts 8/24/32-bit and float WAVs instead of throwing. OK?

## Test steps for Denis
None in game for W0-B alone (nothing is called). Build with `dotnet build` on Windows; the new files must compile with zero new warnings.
