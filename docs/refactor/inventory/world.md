# Inventory: World area (Towns, POI, Survival, Weather)

Scope: `src/Modules/Towns/**` (47 files, 8140 lines), `src/Modules/POI/**` (2 / 380), `src/Modules/Survival/**` (9 / 1572), `src/Modules/Weather/**` (15 / 2298). 73 files, 12 390 lines. Snapshot read 2026-10-22, nothing compiled or run. Paths in tables are relative to `src/Modules/`.

Target mapping in short:
- Towns + POI become **POI**.
- Survival + Weather become **Survival**.
- Town/POI navigation code goes to **NPCAI**.
- Spawner and loot code goes to **Loot**.
- The POI dresser goes to **Tools**.
- Shared glTF and Json loaders go to **Core**.
- Bridges to other mods go to **Compatibility**.

## 1. File map

### Towns (becomes the POI module)

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Towns/TownsModule.cs | 782 | Module entry: terrain tile events, town capture/plan/apply, sliced build loop, load handoff hold, save, Patrols queries, F7 pages | POI, NPCAI, Core, Tools, Debug | SPLIT. Lifecycle (`Init`/`Shutdown`/`Prepare`/`Finalized`/`Applied`/`Moved`/`BuildStep`/`LoadBurst`/`Refresh`/`OnSaveBegin`/`OnLoadBegin`/`NewWorld`/`Remove`/`ClearSites`/`NeedsWalls`/`Finish`/`PopulationChanged`) go to POI. The Patrols-facing queries `TravelStops`/`MerchantSeeded`/`TravelRevision`/`PatrolBase`/`PatrolCount`/`PatrolBases`/`ParkingOutside`/`TravelBlocks`/`VehicleWaypoint` become a Core ModAPI `ITownsApi` with Core DTOs, implemented in POI. `Waypoint` becomes an NPCAI waypoint-provider registration. `BeforeHandoff` goes to a Core loading-hold API (`LoadingProgress`). `TraceStartingTerrain` goes to Debug (level 2) or is deleted. `DrawGasStation`/`DrawDresser` go to Tools. |
| Towns/TownData.cs | 164 | Enums (TownCondition/TownFaction/TownRole), BuildingSpec, TownLot, TownPlan, GenerationInput, SpawnerState, TownRecord, TownSite, public static TownsApi | POI, Core | SPLIT. The data model goes to POI. `TownsApi` (L154-163, `internal Provider` typed as `TownsModule`) is REWRITTEN as a Core ModAPI interface. `TownSite.Parking()` and `Building(int)` have no callers. |
| Towns/TownSettings.cs | 95 | All Towns config entries, bound with raw `ConfigFile.Bind`; legacy import; `Snapshot` -> GenerationInput | POI, Tools | SPLIT. Generation settings stay in POI and become per-template data for the Towns editor. `PoiDresserKey`, `TestGasStation`, `TestCamp10` and `GasStationScale` go to Tools. The `Bind(ConfigFile)` standalone path stays only as the shared body (see `BindStandalone`). |
| Towns/TownPlanner.cs | 152 | Worker-side plan: chance, faction, two-row lot packing, siting, walk graph, parking, population | POI | MOVE, then REWRITE the hard-coded lot rules into template data (see 8b). |
| Towns/TownSiting.cs | 82 | Worker-side road-side site search, ranked by relief and grade | POI | MOVE |
| Towns/TownFrontage.cs | 41 | Per-prefab silhouette bands used for lot packing | POI | MOVE |
| Towns/TownOutline.cs | 110 | Contour polygon around the rotated buildings: Crosses/Contains/Offset | POI | MOVE. Exposed as geometry through ModAPI for NPCAI and Patrols. |
| Towns/TownPerimeter.cs | 194 | Frame/Gate/Corner/Normal/WalkNodes geometry, plus `TownVehicleGates` Floyd-Warshall gate graphs for walkers and vehicles | POI, NPCAI, Patrols | SPLIT. Static `TownPerimeter` geometry goes to POI. The walker instance of `TownVehicleGates` (`Walkers`) goes to NPCAI and the vehicle instance (`Vehicles`) goes to Patrols driving AI, both reading the outline through ModAPI. |
| Towns/TownParking.cs | 68 | Puts the parking lot outside the east wall; graded access path; parking BuildingSpec | POI | SPLIT. `Outside` and `Spec` go to POI. `Access` uses `Patrols.TownStop.Approach` (L30-31) and must be rewritten with a local geometry helper. |
| Towns/TownWalkGraph.cs | 89 | Worker-side street visibility graph between lot entrances (StreetNodes/Edges), validated against the .wwnav templates | POI (planner), NPCAI | MOVE to POI as plan data. NPCAI consumes `StreetNodes`/`StreetEdges` (see 5, duplicate pathfinding). |
| Towns/TownPaths.cs | 552 | TownWorldRoads (road segment index), TownGround (numeric template blocking), Heap, dead grid A* | POI, Core, DELETE | SPLIT. `TownWorldRoads.Nearest`/`Sites` and `TownGround.Blocked`/`Protected`/`Wall`/`DoorLine` go to POI. `TownPaths.Heap` (L221-243) merges into a Core binary heap. DELETE `TownPaths.Grid` (L244-400), `TownPaths.Build` plus helpers (L402-552), `TownWorldRoads.Connections` (L80-97), and `TownGround.Height`/`Blocker`/`DoorFailure` (L159-164, 184-215): only the dead `Build` reaches them. |
| Towns/TownLateTerrain.cs | 111 | Worker-side flatten stamp, access grading, height re-export, clears native objects on the pad (Vistas: objects only) | POI | MOVE. The Vistas branch (L15) should read a Compatibility flag from ModAPI. |
| Towns/TownGroundPad.cs | 54 | Vistas only: writes foundation heights into TerrainData and re-applies them after Vistas edits | POI (Compatibility trigger) | MOVE. `VistasWorldGen.TerrainEdited` becomes a ModAPI event. |
| Towns/TownAssets.cs | 356 | Graph catalog: building specs, bakes, spawner paths/budgets, camp wall strip, population pools, hounds, car shells, road material/layer | POI, NPCAI, Loot, Core | SPLIT. `Load`/`Measure`/`Walls`/`HasMerchant`/`BossCamp`/`RoadInput`/`FindRoads` and car shells go to POI. `CatalogPopulation`/`IsBoss`/`Hounds`/`Population` go to an NPCAI population catalog. The spawner `Budgets` capture goes to Loot. `ColliderBox`/`Measure` collider-bounds code merges into a Core geometry helper (duplicated, see 5). |
| Towns/TownBakeData.cs | 156 | Reads NPC `.wwnav` templates (embedded or disk), verifies checksum, finds the outward entrance | NPCAI | MERGE-INTO the NPCAI Nav map reader. Expose a read-only numeric template through ModAPI (`INavApi.TryGetTemplate`). It currently duplicates NPC's reader and the NPC folder paths (L36-42). |
| Towns/TownBakeBridge.cs | 76 | Reflection bridge into `NPC.Nav`/`Brain` (`On`, `Spawned`, `FloorCells`, `StructureRootAt`) and a Harmony prefix/postfix on `Nav.Next` | NPCAI ModAPI | DELETE the bridge and REWRITE it as an NPCAI ModAPI (`Discover(root)`, `ActorSpawned`, `FloorCells`, `StructureRootAt`, `Enabled`, `Tuning`, `RegisterWaypointProvider`). The `Before`/`After` redirect logic moves into NPCAI's `Nav.Next`. |
| Towns/TownNavigation.cs | 224 | Per-town street/entrance graph: capsule-cast validation, per-source Dijkstra, NPC waypoint answers with memo; also hosts the physics `Mask` | NPCAI | MOVE. This is NPC pathfinding: a generic multi-structure route graph fed with nodes and edges from the POI plan. The `Mask` constant goes to Core layer masks. |
| Towns/TownApproach.cs | 79 | Outside-the-wall detour and gate entrance choice for NPCs approaching a town | NPCAI | MOVE as a generic outline detour. |
| Towns/TownOverlap.cs | 358 | Writes neighbour/prop blockers into NPC instance nav grids via reflection; patches Nav Make/BeginBake/CreateMap | NPCAI | MOVE and REWRITE as an NPCAI-internal instance-overlay API (no reflection). |
| Towns/TownOverlayPlan.cs | 243 | TownDressingKit/TownDressingPlan (deterministic tower/tree/wreck placement), TownOverlayPlan (worker nav patches per lot) | POI, NPCAI | SPLIT. `TownDressingKit`/`TownDressingPlan` go to POI. `TownOverlayPlan` (`LotPatch`/`Build`/`Step`/`Inside`) goes to NPCAI, run from a POI plan through ModAPI. |
| Towns/TownWalls.cs | 172 | TownWallKit (Camp_14 fence meshes, gate banners/textures, trees, tower kit) and TownWalls sliced perimeter build | POI | MOVE |
| Towns/TownTowerKit.cs | 101 | Extracts the small tower from a camp; isolated tower nav bake through reflection into `Nav` | POI, NPCAI | SPLIT. `Load`/`Prefab` go to POI. `Step`/`ReleaseBake` (reflection `Invoke` into Nav L64-94) become an NPCAI ModAPI `BakeIsolated(prefab)`. |
| Towns/TownBorderProps.cs | 76 | Sliced placement of planned towers, trees and car shells; feeds DebrisBlockers | POI | MOVE |
| Towns/TownDebris.cs | 25 | Wreck-garden car shells from the dressing plan | POI | MOVE |
| Towns/TownFactory.cs | 149 | Stage/activate lot prefabs, strip camp walls, disable boss/lab doors, spawner resume/capture, KeepGeometry, Destroy | POI, Loot | SPLIT. `NeedsStage`/`Stage`/`Building`/`Streets`/`KeepGeometry`/`Destroy` go to POI. `Resume`/`Capture`/`Get`/`Set` (spawner progress) go to a Loot spawner-progress service. The dead parking-slab branch in `Destroy` (L146) is removed with TownPavement. |
| Towns/TownPopulation.cs | 268 | Town population/loot rolls; native spawner budget capture; rewrites ItemSpawner counts and faction pools; Force for single spawners | POI, Loot, NPCAI | SPLIT. `Assign`/`Classify`/`Legacy`/`Percentage` go to POI. `NativeBudget`/`Capture`/`Allocate`/`Configure`/`Force`/`Read`/`Write` go to Loot (generic "spawner control"), with NPCAI supplying the actor pools. `Created` (faction member count) becomes a POI consumer of an NpcSpawned event. |
| Towns/InteriorPopulation.cs | 265 | Worldwide interior EnemySpawn deferral until the player arrives; paced triggers; interior hearing/deaf control of NPC agents; interior identity names | NPCAI | MOVE as NPCAI population rules. `OwnTown`/`OwnMarker` become ModAPI registrations from POI. `CaptureNames`/`RestoreNames` save data needs an NPCAI sidecar or stays in the POI record through ModAPI. |
| Towns/NativePoiPopulation.cs | 180 | Native non-town POIs: 15% empty roll, same-faction creature variety, safe placement raycasts | NPCAI | MOVE as population rules. `Catalog()` is fed from the shared population catalog. |
| Towns/TownMarkers.cs | 171 | Map/compass town markers, discovery, name labels via CleanHUD internals, hides town map icons, starting-camp tent icon | POI, HUD | SPLIT. `Add`/`Remove`/`Refresh`/discovery/`Owns`/`Building` go to POI (IMapMarkers/ICompassMarkers). The label hack (`Attach`/`Draw`, QuestMap.Tick postfix, `GlyphText`, reflection `_entries`/`Root`/`Image`) and `Observe`/`StartingCamp` (LocationMarkers patches) go to HUD as an `IMapMarkers` label/colour feature. |
| Towns/TownNames.cs | 51 | Editable faction name lists, unique assignment | POI | MOVE |
| Towns/TownStore.cs | 128 | Binary town record serializer, v1..v6 | POI | MOVE (format frozen, see 9) |
| Towns/TownSaveRecord.cs | 73 | ES3 embedded record (base64 + SHA256) with AfterSave verify; legacy sidecar import | POI, Core | MOVE. The ES3-cache + AfterSave re-save/verify pattern merges into a Core SaveBus helper (duplicated 3x, see 5). |
| Towns/TownLoading.cs | 29 | Per-key generation timing logs; `Report` is a no-op | Debug | MERGE-INTO Debug timing (level 1 start/end, level 2 detail). DELETE `Report` and the string-building callers. |
| Towns/TownPoiSettings.cs | 70 | Patches CorePages.DrawWorld/ResetSettings, POI.ToolPages and POI.PoiLocations.Generate; starting-chunk POI policy | POI, Core UI | REWRITE inside POI: the policy becomes a POI-internal check in `PoiLocations.Generate`, and the World section comes from a Core UI extension point instead of patches. |
| Towns/TownGasStation.cs | 617 | Custom gas-station POI: GLB model/material binding, desert roadside planning/placement, loot spawner clones, ES3 progress, markers, dev test pad, Camp_10 test | POI, Loot, Tools | SPLIT. `LoadModel`/`Shell`/`Capture`/`Plan*`/`Applied`/`Remove`/`Moved`/markers `Tick`/`Save`/`Load` go to POI. `Populate`/`Add` (loot spawner cloning) go to Loot. `TestTick`/`PlaceCamp`/`Camp10` test pad go to Tools (Navigation editor test). DELETE `SpawnEnemies` (const false, L51) with `EnemyPoints` and the enemy loop (L52, L333). `LaneCenter`/`LaneSpread` (used by Patrols TownTraffic.cs:130) go to ModAPI data. |
| Towns/GasStation/Glb.cs | 229 | Static GLB reader; superset variant with DoubleSided, NodeNames, collider-geometry flag | Core | MERGE-INTO the Core glTF loader (same as Survival/StewPot/Glb.cs, Patrols/WarRig/Glb.cs, NPC/Women/Gltf.cs, PlayerBody/Body/Gltf.cs) |
| Towns/GasStation/Json.cs | 117 | MiniJson parser | Core | MERGE-INTO Core.Json. It is byte-identical to Survival/StewPot/Json.cs apart from the namespace. |
| Towns/PoiDressing.cs | 224 | Runtime static dressing of custom POIs from layouts (one prop per frame), Nav.Make gate, Exit waypoints, PoiDressNavigation reflection | POI, NPCAI | SPLIT. `Site`/`Attach`/`Step`/`Build`/`Save`/`Sweep`/`Clear` go to POI. `BeforeMap` (Nav.Make prefix) and `Exit()` go to NPCAI ModAPI ("structure not ready" gate, exit-hint provider). DELETE `PoiDressNavigation` (L199-222, reflection `Invoke` into Nav privates) and replace it with `INavApi.ForgetStructure(root)`. |
| Towns/PoiDressLayout.cs | 135 | PoiDressProp/PoiDressLayout data, JSON load/save of layouts | POI | MOVE (data and IO). Tools writes through the POI ModAPI. Uses `GasStation.Json`, which moves to Core.Json. |
| Towns/PoiDressMarkers.cs | 141 | Marker kinds/categories; Hide/Exit markers; Items/CarParts/Monsters spawner clones with per-world progress in ES3 | Tools, Loot, NPCAI | SPLIT. `Kinds`/`Categories`/`Category()` go to Tools (POI editor). `Build` for Items/CarParts and `Save`/`Load`/`Capture` go to Loot (marker spawn groups). Monsters spawn uses NPCAI pools. Hide/Exit semantics go to NPCAI. |
| Towns/PoiSpawnCatalog.cs | 115 | Scans native ItemSpawner templates into named spawn profiles (monsters by faction, loot/supplies/weapons/car parts) | Loot | MOVE as the Loot spawn-profile catalog. Monster profiles are consumed by NPCAI. |
| Towns/PoiPropCatalog.cs | 181 | Vanilla mesh catalog (ES3 refs + all prefab MeshFilters), prop instantiation from layout, marker gizmo meshes | Tools, POI | SPLIT. `Start`/`Step`/`Capture`/`Sources`/`All` and the marker gizmos go to Tools (editor only). `Resolve`/`Create`/`Pose` (runtime instantiation) go to POI. |
| Towns/PoiDresser.cs | 423 | In-world POI layout editor (fly camera, place/move props and markers, undo, save) | Tools | MOVE as the POI editor. Reaches POI only through ModAPI. |
| Towns/TownRoadBridge.cs | 58 | Would append town access roads to Patrols routing via patches | - | DELETE. `Install`/`Add`/`Remove` are never called; only `Clear()` is (TownsModule.cs:728). |
| Towns/TownSurfacePaint.cs | 73 | Old road/path splat painting | - | DELETE (no references anywhere) |
| Towns/TownPavement.cs | 23 | Old parking slab mesh | - | DELETE (no references; "keep only the parking slab" is also gone) |
| Towns/TownDebug.cs | 60 | Old Developer > Towns page and Apocasetter "settings moved" patches | - | DELETE (`Install` never called) |

### POI

| path | lines | what it does | target | action |
|---|---|---|---|---|
| POI/PoiModule.cs | 286 | IPoiApi provider: registers PoiLocations/Flatten/Clearance generators into the MapMagic graph, site markers, 500 m proximity spawn, `.poi.txt` sidecar | POI | MOVE. Merge the terrain-tile event plumbing and graph scan with TownsModule (both subscribe the same 4 TerrainTile events and both scan `graph.generators` for native POI outlets). `PoiSite` is a MonoBehaviour. |
| POI/PoiGenerators.cs | 94 | Worker generators: PoiLocations (seeded flat pad) and PoiClearance (removes solid objects on pads) | POI | MOVE. Fold the starting-chunk check from TownPoiSettings.AllowPoi into `Generate`. |

### Survival

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Survival/SurvivalModule.cs | 131 | Stew Pot module entry, config + legacy migration, asset extraction, hooks | Survival, Core, Debug | MOVE. `InstallAssets` merges into a Core "extract embedded file if missing" helper. `Verbose` merges into the Debug VerboseLog level. |
| Survival/StewPot/Factory.cs | 207 | Builds the Stew Pot template from Water_Can_Plastic with custom prefab id, GLB meshes and materials | Survival | MOVE |
| Survival/StewPot/Runtime.cs | 364 | Pot registry/discovery, per-frame drink/pour/pose/label, input hook | Survival | MOVE. Replace the own `live` FSM-state test (L153) with `GameRefs.InGame`. |
| Survival/StewPot/Stew.cs | 289 | Contents model (raw/cooked/insanity/spoil), cooking on fire, interaction aim, boiling sound | Survival | MOVE |
| Survival/StewPot/WaterPour.cs | 136 | Pour stream mesh | Survival (Presentation candidate) | MOVE |
| Survival/StewPot/NaturalLoot.cs | 35 | CreateObject prefix: 25% of natural gasoline_stove spawns become Stew Pots | Loot | MOVE into a Loot substitution table (rule registered by Survival through ModAPI) |
| Survival/StewPot/SpawnerBridge.cs | 65 | Apocaspawner catalog entry injection | Compatibility | MOVE into a Compatibility Apocaspawner bridge, merged with Patrols `SpawnerIntegration`. Survival registers its entry through ModAPI. |
| Survival/StewPot/Glb.cs | 227 | GLB reader (subset of the gas-station copy) | Core | MERGE-INTO the Core glTF loader |
| Survival/StewPot/Json.cs | 118 | MiniJson (identical to Towns/GasStation/Json.cs) | Core | MERGE-INTO Core.Json |

### Weather (becomes part of Survival)

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Weather/WeatherModule.cs | 352 | IWeatherRegions provider: up to 4 storm regions, natural/manual storms, hazard tick, F7 preview page, diagnostic snapshot capture/restore | Survival, Debug, Tools | SPLIT. Regions/`Tick`/`TickHazards`/`ReconcileNative`/`Spawn` go to Survival (Weather feature). `CaptureDiagnostic`/`RestoreDiagnostic` go to Debug. `DrawTools` (preview buttons) goes to Tools/Debug page. |
| Weather/WeatherNative.cs | 319 | Native SandStorm replacement: tracks storm roots, suppresses native particles/colliders/FSMs, intercepts Explosion/TriggerEventByLayer, save-time restore | Survival | MOVE. The storm prefab id and root lookup become a shared Survival ModAPI query (`IsStormRoot`), used by Patrols and NPCAI. |
| Weather/WeatherHazards.cs | 102 | Annular hurricane-wall hazards: OverlapBox + native damage events + explosion force | Survival | MOVE. War Rig shelter becomes `IShelterProvider` through ModAPI (see 5). |
| Weather/WeatherVisuals.cs | 301 | Bundle load, per-camera DrawMesh of wall/canopy/vortex/lightning, ground probes | Survival | MOVE |
| Weather/WeatherAtmosphere.cs | 155 | Depth-only atmosphere pass, Camera.onPreCull globals | Survival | MOVE |
| Weather/WeatherSky.cs | 120 | Camera-scoped sky/sun/ambient override; Azure fog prefix/finalizer | Survival | MOVE |
| Weather/WeatherDust.cs | 152 | Native Sand Player particle clone, tinted | Survival | MOVE |
| Weather/WeatherDebris.cs | 112 | Native stone particle pools | Survival | MOVE |
| Weather/WeatherSmoke.cs | 144 | Smoke particle banks | Survival | MOVE |
| Weather/WeatherVortexSmoke.cs | 66 | Hurricane body and cloud materials, column widths | Survival | MOVE |
| Weather/WeatherPerimeter.cs | 42 | Arc-length layout of the region boundary | Survival | MOVE |
| Weather/WeatherPalette.cs | 45 | Colour/darkening config and Revision counter | Survival | MOVE (config keys, see 9) |
| Weather/WeatherAudio.cs | 107 | Storm loop wav (UnityWebRequest) and distance volume | Survival | MOVE |
| Weather/WeatherThunder.cs | 98 | Thunder mp3, delay by distance, 16 voices | Survival | MOVE |
| Weather/WeatherDiagnostics.cs | 183 | "Temporary" render diagnostics presets, freeze, snapshot file, overlay label | Debug | MOVE to Debug, or DELETE once the renderer is accepted (UI label says "temporary", L56) |

## 2. Cross-module coupling

### 2a. This area uses other modules' types or internals (rule violations are marked VIOLATION)

| file:line | uses | why |
|---|---|---|
| Towns/TownsModule.cs:566-581 | `Patrols.TownStop` (constructs it) | VIOLATION: builds Patrols DTOs for town traffic stops |
| Towns/TownsModule.cs:605-614 | `Patrols.TrafficBase` | VIOLATION: patrol bases at town parking |
| Towns/TownsModule.cs:681-682 | `Patrols.TrafficSpace.World/Scene` | VIOLATION: coordinate conversion for vehicle waypoints |
| Towns/TownsModule.cs:741 | `Patrols.WorldTraffic.TownsStopped()` | VIOLATION: tells Patrols on shutdown |
| Towns/TownParking.cs:30-31 | `Patrols.TownStop.Approach` | VIOLATION: reuses the Patrols approach geometry on the MapMagic worker |
| Towns/TownMarkers.cs:9, 24, 140 | `using WildWasteland.Modules.CleanHUD`; `GlyphText` | VIOLATION: CleanHUD UI type used directly |
| Towns/TownMarkers.cs:41-55 | reflection on `CleanHUD.LocationMarkers.Locations`, `QuestMap._entries`, `Entry.Id/Root/Image`; `typeof(CleanHudModule).ShowLocations/ShowCompass` | VIOLATION: private CleanHUD state and config |
| Towns/TownMarkers.cs:56-58 | Harmony on `LocationMarkers.Observe`, `QuestMap.Tick` | VIOLATION: patches another module's methods |
| Towns/InteriorPopulation.cs:8, 19, 195-197, 199-210, 224-238 | `using WildWasteland.Modules.NPC`; `Senses.Agent` (`Interior`/`AmbushDist`/`Deaf`/`Hostile`/`Owner`), `Senses.AgentFor`, `Senses.AllAgents`, `Senses.ClearDeafInvestigation` | VIOLATION: writes NPC agent state directly |
| Towns/TownBakeBridge.cs:24-27 | reflection `NpcModule.<field>` ConfigEntry (`NavMargin`, `NavCellSize`, `NavMaxStep`) | VIOLATION: reads NPC tuning |
| Towns/TownBakeBridge.cs:31-40 | delegates to `NPC.Nav.get_On`/`Spawned`/`FloorCells`/`StructureRootAt`, `NPC.Brain.Spawned`; Harmony on `Nav.Next` | VIOLATION: private NPC API and patch |
| Towns/TownOverlap.cs:74-88, 110-111, 121-128, 184-189, 313-326 | reflection on `Nav.Structure` fields (21 names), `Nav._outPlans/_outMemo/_exits/_relaxedFields/_compRing`, `Invoke` of `QueueMasks`/`Return`/`BeginBake`/`BakeStep`/`PickReset`; Harmony on `Nav.Make`/`BeginBake`/`CreateMap` | VIOLATION: rewrites NPC nav grids from outside |
| Towns/PoiDressing.cs:42-44 | Harmony prefix on `Nav.Make` | VIOLATION |
| Towns/PoiDressing.cs:199-222 | `PoiDressNavigation`: reflection on `Nav._structures/_known/_floorCols/_maskQueue/_baking/_needed/...`, `Invoke CloseMap/PickReset` | VIOLATION: removes NPC structures by hand |
| Towns/TownTowerKit.cs:64-67, 90-94 | reflection `Invoke` of `Nav.Make`/`BeginBake`/`BakeStep`/`CloseMap`, fields `_baking`, `_floorCols`, `_maskQueue` | VIOLATION |
| Towns/TownBakeData.cs:36-42 | NPC embedded resource names `NPC.NavMaps.Vanilla.*` and the NPC folder layout | VIOLATION in spirit: file-format and path knowledge of another module |
| Towns/TownPoiSettings.cs:24-35 | `ModuleHost.Entries` lookup of POI; POI's ConfigEntry `[POI / World] Spawn chance (%)`; Harmony on `PoiModule.ToolPages` and `POI.PoiLocations.Generate` (worker thread); Harmony on Core `CorePages.DrawWorld`/`ResetSettings` | VIOLATION (Towns->POI; also patches Core UI) |
| Towns/TownRoadBridge.cs:18-28 | reflection on Patrols `VehicleRoute`/`TerrainWaypoints`/`RoadPlanner`/`PatrolsModule.TrafficRoadRouting` | VIOLATION, but dead code |
| Towns/TownDebug.cs:20-31 | Core `CorePages.Developer`, Apocasetter `SettingsUI` patches | dead code |
| Towns/TownsModule.cs:182-183, TownGasStation.cs:82 | `Services.Poi`, `Services.PoiNavSetup` | OK (Core). Pauses generation while NPC POI nav setup runs. |
| Towns/TownsModule.cs:101-102, 108, 312, 334 | `Hooks.WaitUpdate`, `LoadingProgress`, `VistasWorldGen.TerrainEdited`, `SaveSystem.Busy`, `WorldWork.Try(WorldWork.Towns)` | OK (Core) |
| Towns/InteriorPopulation.cs:118, 148 | `WorldWork.Try(WorldWork.Population)` | OK (Core arbitration) |
| Towns/TownGasStation.cs:404-405, TownMarkers.cs:97-120 | `Services.Map`, `Services.Compass` | OK |
| Towns/TownGasStation.cs:422, TownNavigation.cs:46, PoiDressing.cs:71-72, TownBakeBridge.cs:45-46 | `Services.Npc` (null check, `Structures()`) | OK |
| Towns/PoiDresser.cs:52, 56, 99 | `Plugin.EnableDebug`, `Plugin.DeveloperMode`, `EditorSession`, `Hotkeys` | OK (Core) |
| POI/PoiModule.cs:130 | `VistasWorldGen.Present` | OK (Core; Compatibility later) |
| Weather/WeatherModule.cs:229, 275, 292, 330 | `Services.WeatherVisualPolicy` (provided by Performance) | OK |
| Weather/WeatherHazards.cs:65 via WeatherNative.cs:61 | delegate to `Explosion.ShouldApplyForce`, which carries the Patrols `NativeStormProtection.RadialForce` postfix | Implicit coupling: War Rig shelter works only because Weather calls the patched original. Undocumented. |
| Survival/SurvivalModule.cs:62-72 | `Runner.Host`, `Hooks.GetPrefab/GetButtonDownUpdate/CreateObjectEnter`, `Scheduler.StartCoroutine` | OK |
| Survival/StewPot/Runtime.cs:154 | `ApocasetterBridge.InputBlocked`, `EditorSession.Busy` | OK |

### 2b. Other modules reaching into this area (also VIOLATIONS)

| file:line | uses |
|---|---|
| Patrols/Crews/Pilot.cs:223-226 | `Towns.TownsApi.Provider.VehicleWaypoint` (internal TownsModule method) |
| Patrols/Crews/CrewPoi.cs:50 | `TownsApi.Provider.ParkingOutside` |
| Patrols/Traffic/TrafficLocations.cs:35, 43, 47, 49 | `PatrolCount`, `PatrolBases`, `PatrolBase`, `ParkingOutside` |
| Patrols/Traffic/TrafficLocations.cs:258 | `Towns.TownMarkers.Owns(t)` |
| Patrols/Traffic/RoadPlanner.cs:67 | `TownsApi.Provider.TravelBlocks` |
| Patrols/Traffic/WorldTraffic.cs:430-446 | `TravelRevision`, `ParkingOutside` |
| Patrols/Traffic/TownTraffic.cs:43, 57, 200 | `TravelStops`, `MerchantSeeded` |
| Patrols/Traffic/TownTraffic.cs:130 | `Towns.TownGasStation.LaneSpread` |
| Core/Plugin.cs:139 | registers `TownsModule` (normal) |

### 2c. Services this area PROVIDES

| member | signature | provider |
|---|---|---|
| `Services.Poi` | `IPoiApi { void Register(PoiDefinition); void Remove(string id); }`. `PoiDefinition { string Id; float Radius; Func<bool> Enabled; Action<Vector3,Quaternion,Action<GameObject>> Spawn; Action<GameObject> Mark; }`. `event Action<IPoiApi> PoiChanged` is raised by the setter. | PoiModule.cs:56 / :269 |
| `Services.Weather` | `IWeatherRegions { int Count; bool MapAreas {get;set;}; float HurricaneDensity {get;set;}; float FrontWidthKm {get;set;}; float DepthKm {get;set;}; bool TryGet(int, out WeatherRegion); }` | WeatherModule.cs:60 / :347. Consumers: CleanHUD/WeatherMap.cs:16, Core/UI/CorePages.cs:116, Core/VistasWorldGen.cs:133-135. |
| `Towns.TownsApi` (NOT in Core) | `public static int Count; TownSite Get(int); bool SetRole(string,int,TownRole); bool SetCondition(string,TownCondition)` plus `internal static TownsModule Provider` (the real API used by Patrols) | TownData.cs:154-163. Public members have no callers. |
| Survival | none | - |

## 3. Harmony patches & scheduler jobs

### 3a. Patches

| file:line | target method | kind | via | per-call cost / filter |
|---|---|---|---|---|
| Towns/TownsModule.cs:101 | `Wait.OnUpdate` | prefix `BeforeHandoff` | Hooks.WaitUpdate | Runs for every Wait action every frame in the game. First test is a string compare `action.Fsm.Name != "SaveLoadGame"`. Should be `ReferenceEquals(action.Fsm, GameRefs.SaveLoadGame?.Fsm)`. While loading it loops sites and `InteriorPopulation.TownReady` (FindFsmInt per group). |
| Towns/InteriorPopulation.cs:34 | `Fsm.ProcessEvent` | prefix `SpawnEvent` | Hooks.ProcessEvent | Every FSM event in the game. Filter is `fsm.Name != "ItemSpawner"` (string compare). Use a HashSet<Fsm> of tracked groups. |
| Towns/InteriorPopulation.cs:35 | `PlayMakerFSM.OnEnable` | postfix `Enabled` | Hooks.FsmEnable | `scene.IsValid()` runs before the cheap FsmName test; `Interior()` calls `GetComponents<Collider>()` (allocates) |
| Towns/InteriorPopulation.cs:35 | `CreateObject.OnEnter` | postfix `Created` | Hooks.CreateObjectEnter | `Fsm.Name` string compare, then dictionary; 2x `FsmUtil.Find` per created actor |
| Towns/NativePoiPopulation.cs:36 | `Fsm.EnterState` | prefix `Enter` | Hooks.EnterState | Every state entry of every FSM; `fsm.Name != "ItemSpawner"` string compare |
| Towns/NativePoiPopulation.cs:37 | `CreateObject.OnEnter` | prefix `Creating` + postfix `Created` | Hooks.CreateObjectEnter | Dictionary lookup (cheap). On a hit, up to ~40 physics queries per spawn (L130-163). |
| Towns/TownBakeBridge.cs:37 | `NPC.Nav.Next` | prefix `Before` + postfix `After` | Patches (cross-module target) | Every NPC path query: loops all town sites (`TransformPoint`, segment test) and all POI dressing sites (`PoiDressing.Exit`, 2x InverseTransformPoint per site) |
| Towns/TownOverlap.cs:85 | `NPC.Nav.Make` | postfix `Made` | Patches (cross-module) | Per structure creation; HashSet test |
| Towns/PoiDressing.cs:43 | `NPC.Nav.Make` | prefix `BeforeMap` | Patches (cross-module; same target as above) | `CompareTag` plus name parse per structure |
| Towns/TownOverlap.cs:86 | `NPC.Nav.BeginBake` | prefix `BeforeBake` | Patches (cross-module) | dictionary lookup |
| Towns/TownOverlap.cs:87 | `NPC.Nav.CreateMap` | prefix `Writable` | Patches (cross-module) | reflection `GetValue` per call (rare) |
| Towns/TownMarkers.cs:56 | `CleanHUD.LocationMarkers.Observe` | prefix `Observe` + postfix `StartingCamp` | Patches (cross-module) | parent walk with HashSet per icon |
| Towns/TownMarkers.cs:58 | `CleanHUD.QuestMap.Tick` | postfix `Draw` | Patches (cross-module) | per frame while the map is open; sets the label string per frame |
| Towns/TownPoiSettings.cs:29 | `Core.UI.CorePages.DrawWorld` | postfix `Draw` | Patches (Core UI target) | GUI |
| Towns/TownPoiSettings.cs:30 | `POI.PoiModule.ToolPages` | postfix `Pages` (replaces the result) | Patches (cross-module) | once |
| Towns/TownPoiSettings.cs:31 | `Core.UI.CorePages.ResetSettings` | postfix `ResetTown` | Patches (Core UI target) | rare |
| Towns/TownPoiSettings.cs:34 | `POI.PoiLocations.Generate` | prefix `AllowPoi` | Patches (cross-module, MapMagic worker thread) | ConcurrentDictionary lookup per tile |
| Towns/TownDebug.cs:21, 29-30 | `CorePages.Developer`, `Apocasetter.SettingsUI.DrawSettings/DrawSectionTabs` | postfix / prefix | Patches | DEAD (never installed) |
| Towns/TownRoadBridge.cs:25-28 | Patrols `VehicleRoute.Build/Revalidate`, `TerrainWaypoints.Build`, `RoadPlanner.Build` | prefix | Patches (cross-module) | DEAD (never installed) |
| Weather/WeatherModule.cs:54 | `AzureFogScattering.OnRenderImage` | prefix + finalizer | Patches | per camera image; camera reference test |
| Weather/WeatherNative.cs:62 | `Explosion.DoExplosion` | prefix `ExplosionBefore` | Patches | every Explosion action; `Fsm.Name != "Tornado"` string compare, then cached dictionary. Patrols patches `Explosion.ShouldApplyForce` (different method). |
| Weather/WeatherNative.cs:63 | `TriggerEventByLayer.DoTriggerEnter` | prefix `TriggerBefore` | Patches | every trigger enter of that action; `Fsm.Name != "TornadoDamage"` |
| Weather/WeatherNative.cs:65 | `PlayMakerFSM.OnEnable` | postfix `Enabled` | Hooks.FsmEnable | 2 FsmName compares |
| Weather/WeatherNative.cs:66 | `Fsm.ProcessEvent` | prefix `EventBefore` | Hooks.ProcessEvent | every FSM event; `ev.Name == "SaveGame"` string compare with no FSM filter. Duplicates `OnSaveBegin` (WeatherModule.cs:341) except for timing. |
| Weather/WeatherNative.cs:67 | `SetVelocity.DoSetVelocity` | prefix `VelocityBefore` | Hooks.SetVelocityDo | `Fsm.Name != "Move"`, then dictionary |
| Weather/WeatherNative.cs:68 | `CreateObject.OnEnter` | postfix `Created` | Hooks.CreateObjectEnter | `Fsm.Name`/`Owner.name` string compares |
| Survival/SurvivalModule.cs:64 | `ES3ReferenceMgrBase.GetPrefab` | resolver | Hooks.GetPrefab | long compare |
| Survival/SurvivalModule.cs:65 | `GetButtonDown.OnUpdate` | prefix `Runtime.DrinkInput` | Hooks.GetButtonDownUpdate | per GetButtonDown update |
| Survival/SurvivalModule.cs:66 | `CreateObject.OnEnter` | prefix `NaturalLoot.BeforeCreate` | Hooks.CreateObjectEnter | `Fsm.Name` string compare |
| Survival/SurvivalModule.cs:67 | `ES3Prefab.Awake` | postfix `PrefabAwake` | Patches (direct; only patcher of this target) | long compare |
| Survival/StewPot/SpawnerBridge.cs:30 | `Apocaspawner.Catalog.Entries` | postfix `CatalogBuilt` | Hooks.SpawnerCatalogEntries | reference compare (shared with Patrols) |
| Weather/WeatherSky.cs:34, WeatherAtmosphere.cs:58 | `Camera.onPreCull` / `onPostRender` delegates (not Harmony) | callback | Unity | per camera per frame; camera reference early out |
| POI/PoiModule.cs:57-60, TownsModule.cs:106-107 | `TerrainTile.OnBeforeTilePrepare/OnTileFinalized/OnTileApplied/OnTileMoved` (native events) | subscriptions | MapMagic | Two modules each subscribe all 4 and each re-scan the graph |

Shared targets inside this area: `CreateObject.OnEnter` has 5 subscribers here (InteriorPopulation post, NativePoiPopulation pre+post, Survival pre, Weather post) on top of Guns/Quests. `Fsm.ProcessEvent` has InteriorPopulation and Weather. `PlayMakerFSM.OnEnable` has InteriorPopulation and Weather, plus Patrols NativeStormProtection and Performance. `NPC.Nav.Make` is patched twice by Towns (prefix PoiDressing, postfix TownOverlap). Explosion: Weather (`DoExplosion`) and Patrols (`ShouldApplyForce`) touch the same action for the same storm.

### 3b. Scheduler jobs

| name | kind | interval / budget | file |
|---|---|---|---|
| Build and connect | Sliced | 700 us | Towns/TownsModule.cs:110 |
| Load construction | EveryFrame + Wants predicate | own stopwatch up to `LoadingBudgetMs` (12 ms) while loading | Towns/TownsModule.cs:112 |
| Spawner state | Interval | 0.5 s unscaled | Towns/TownsModule.cs:113 |
| Town discovery | Interval | 0.25 s unscaled | Towns/TownsModule.cs:114 |
| Gas stations | Interval | 0.25 s unscaled | Towns/TownsModule.cs:115 |
| POI static dressing | Sliced | 500 us | Towns/PoiDressing.cs:45 |
| POI dressing cleanup | Interval | 5 s scaled | Towns/PoiDressing.cs:46 |
| POI marker catalog | Sliced | 500 us | Towns/PoiDressing.cs:47 |
| POI dresser input | EveryFrame | always registered, early out | Towns/PoiDresser.cs:47 |
| POI dresser camera | EveryFrame (LateUpdateLast) | always registered, early out | Towns/PoiDresser.cs:48 |
| POI dresser | Gui | predicate `Opened` | Towns/PoiDresser.cs:49 |
| Interior pre-spawn | Sliced | 700 us | Towns/InteriorPopulation.cs:36 |
| Interior hearing | Interval | 0.25 s | Towns/InteriorPopulation.cs:37 |
| Native POI spawn cache | Interval | 30 s | Towns/NativePoiPopulation.cs:38 |
| Site proximity | Interval | 1 s unscaled | POI/PoiModule.cs:61 |
| Survival.StewPot | EveryFrame (LateUpdateLast) | - | Survival/SurvivalModule.cs:70 |
| Survival.References | Interval | 1 s (RunNow) | Survival/SurvivalModule.cs:71 |
| Survival.Existing pots | Sliced | 500 us (whole-scene transform walk) | Survival/SurvivalModule.cs:72 |
| Weather.PrepareNativeAssets | Interval | 1 s, forever (bool test after ready) | Weather/WeatherModule.cs:59 |
| Moving storm visuals | EveryFrame (LateUpdateLast) | enabled only while regions exist | Weather/WeatherModule.cs:61 |
| Weather.HurricaneHazards | EveryFrame (FixedUpdate) | always enabled | Weather/WeatherModule.cs:64 |
| Weather diagnostic label | Gui | predicate `WantsOverlay` | Weather/WeatherModule.cs:65 |
| Weather.NativeDiscovery | Sliced | 500 us | Weather/WeatherNative.cs:69 |
| Weather.NativeEncounters | Interval | 0.5 s | Weather/WeatherNative.cs:70 |

Naming: the Towns/POI jobs lack the `<Id>.` prefix that AGENTS 5.4 asks for; Weather is mixed. Coroutines: WeatherAudio.cs:34, WeatherThunder.cs:28, Survival StewLogic.LoadSound (SurvivalModule.cs:69).

## 4. Logging

No own `ManualLogSource`, no `UnityEngine.Debug.Log` in the area. Everything uses `Core.Log`. Call counts: Towns Always 56 / Info 1 / Warn 38 / Error 2; POI 3/0/1/3; Survival 10/0/5/1; Weather 31/4/14/3.

| switch / helper | where | used by | proposed level |
|---|---|---|---|
| `[Survival / Debug] VerboseLog` (tools.cfg, `SurvivalModule.Verbose`) | SurvivalModule.cs:32 | Runtime.cs:138, :361 (`Trace`), Stew.cs:286, NaturalLoot.cs:30. These emit `Log.Always` when the switch is on. | Retire the switch. Pot registered and natural substitution go to level 1; drink/pour/boil traces (`StewLogic.Trace`, `Runtime.Trace`) go to level 2. |
| `Core.Log.Verbose` gate | WeatherAtmosphere.cs:133, WeatherThunder.cs:57/:82, WeatherHazards.cs:75 | atmosphere state, lightning/thunder, 5 s hazard counters | level 2 |
| WeatherDiagnostics `[WeatherDiag]` (`Log.Always`) | WeatherDiagnostics.cs:42/76/113, WeatherDust.cs:119-120, WeatherVisuals.cs:187, WeatherModule.cs:291-302 | diagnostic tool output | Debug tool output: level 1 when the tool is used, otherwise none |
| `TownLoading.Work` timings | TownLoading.cs:16, :21 | `[Towns] generation started/ended <key> ... ms` per tile | Ended goes to level 1 and started to level 2. Delete the no-op `Report` (callers TownsModule.cs:216/:268, TownSiting.cs:66, TownPaths.cs:418/:439 build strings for nothing). |
| `[Towns] DEBUG ranges` | TownsModule.cs:458 | population slider change | level 1 |
| `[Towns] Starting terrain` | TownsModule.cs:470-504 `TraceStartingTerrain` | Vistas seam diagnostics on every applied near-origin tile | level 2, or delete |
| per-lot / per-building logs | TownFactory.cs:44, 46, 64, 98; TownAssets.cs:105, 127, 129, 131, 151; TownGasStation.cs:515, 537 (per material); TownWalls.cs:85 | catalog and construction detail | level 2 |
| per-town milestones | TownsModule.cs:129, 158, 251, 261, 544-545; TownPopulation.cs:137; TownNavigation.cs:105; TownOverlap.cs:215; TownWalls.cs:167; TownBorderProps.cs:45; TownGroundPad.cs:38; TownSaveRecord.cs:38/62/69; TownMarkers.cs:87 (visited); TownsModule.cs:564/640 | main actions | level 1 (TownOverlap/TownNavigation/TownBorderProps summaries can be level 2) |
| population pacing | InteriorPopulation.cs:182 (each queued group), :241 (hearing toggle) | | 182 goes to level 2; 241 to level 2 |
| `[POI] native population ...` | NativePoiPopulation.cs:106 (one per native POI visited; wrong prefix: Towns code logs as POI) | | level 2 |
| POI markers | PoiDressMarkers.cs:103 (each marker group), PoiDressLayout.cs:132 (save), PoiPropCatalog.cs:63 | | 103 to level 2; 132 to level 1; 63 to level 2 |
| Weather milestones | WeatherModule.cs:96, 135, 181, 218, 319, 337; WeatherNative.cs:72, 175, 198; WeatherHazards.cs:46; WeatherVisuals.cs:75-78, 100; WeatherDust.cs:54, 101; WeatherDebris.cs:55, 91; WeatherAudio.cs:50; WeatherThunder.cs:47; WeatherVortexSmoke.cs:26; WeatherAtmosphere.cs:60; WeatherSky.cs:90 | | Storm spawned/replaced/cleared/critical (96, 135, 198, 218, 319) go to level 1. Asset/pool/"live" descriptions (75-78, 100, 54, 101, 55, 91, 26, 60, 90, 181, 175) go to level 2. |
| `Log.Always` used as warning | TownsModule.cs:223, TownOverlayPlan.cs:69 (worker thread, because Warn's rate-limit table is main-thread only) | | Core Log needs a thread-safe Warn; then use Warn |
| POI module | PoiModule.cs:157, 201, 227 | registration, site generated, encounter spawned | 157 and 227 go to level 1; 201 to level 2 |
| Survival | SurvivalModule.cs:53 (migration), :73; Factory.cs:128, 166; Stew.cs:280; SpawnerBridge.cs:31 | | 53 stays Always (migration); others go to level 2 |

No per-feature log switches exist in Towns, POI or Weather. The only per-module switch is Survival's VerboseLog.

## 5. Redundancy & dead code

### Dead or disabled
| evidence | what |
|---|---|
| TownPaths.cs:244-552 | `TownPaths.Grid`, `Build`, `EntranceRoute`: grid A* path builder, no callers (also stated in project doc towns-placement-perf-review). Only `TownPaths.Heap` (L221-243) is live, used by TownNavigation.cs:128. |
| TownPaths.cs:80-97, 159-164, 184-215; TownGround ctor `heights` branch L132-137 | `Connections`, `Height`, `Blocker`, `DoorFailure`; heights are always null in the live callers (TownWalkGraph.cs:12, :31) |
| Towns/TownSurfacePaint.cs (whole) | unreferenced |
| Towns/TownPavement.cs (whole) + TownFactory.cs:146 parking-slab cleanup | unreferenced |
| Towns/TownDebug.cs (whole) | `Install` never called; blank lines TownsModule.cs:97-98 mark the removed installs |
| Towns/TownRoadBridge.cs (all except `Clear`) | `Install`/`Add`/`Remove` never called; the patches are never applied |
| TownsModule.cs:85 `BindStandalone` | standalone Towns DLL path, no callers |
| TownLoading.cs:17 `Report` | empty body; 6 call sites build strings |
| TownGasStation.cs:51-52, :325, :333 | `SpawnEnemies = false`: desert-station enemy spawners are disabled code |
| TownNavigation.cs:30-31 | `Failed` always false; `FailureReason` never set; TownsModule.cs:401 tests it |
| TownData.cs:41, :99; TownPopulation.cs:24-30 | `TownLot.Actors`/`Loot` are rolled and saved but never read at runtime (spawner budgets come from native %). `GenerationInput.PopulationPercent`/`LootPercent` are only set in `Legacy`. Keep the save fields; drop the logic. |
| TownData.cs:157-162, :145-151 | `TownsApi.Count/Get/SetRole/SetCondition`, `TownSite.Building(int)`/`Parking(...)`: no callers (intended future API for shops/quests) |
| TownPopulation.cs:31 | `Lots[2].Vehicles = 1`, but lot 2 is always the parking lot, which `Configure` never visits. The vehicle budget is effectively dead. |
| WeatherDiagnostics.cs (whole) | labelled "(temporary)" (L56) |
| TownPoiSettings.cs:59-60 | Text says "Defaults: ... Towns 50%" and the reset sets 50, but TownSettings.cs:38 default is 20, so the "Reset POI chances" button does not match the Game RESET (`ResetTown` uses DefaultValue). |

### Duplicated utilities
| copies | evidence | merge into |
|---|---|---|
| MiniJson x2 identical in area (5 in repo) | Towns/GasStation/Json.cs, Survival/StewPot/Json.cs (diff: namespace only); also NPC/Women/Json.cs, CleanHUD/Json.cs, PlayerBody/Body/Json.cs, Patrols/WarRig/Json.cs | Core.Json |
| GLB reader x2 in area (5 in repo) | Towns/GasStation/Glb.cs vs Survival/StewPot/Glb.cs (gas-station copy adds `colliderGeometry`, `DoubleSided`, `NodeNames`); also Patrols/WarRig/Glb.cs, NPC/Women/Gltf.cs, PlayerBody/Body/Gltf.cs | Core glTF (use the gas-station superset) |
| `.wwnav` reader | TownBakeData.cs:24-75 duplicates NPC Nav map reading (format, checksum, folder search) | NPCAI read-only template service |
| collider-bounds composition x4 | TownAssets.cs:42-69 `ColliderBox`, :309-345 `Measure`, :263-285 `Box`; TownDressingPlan.Rotated (TownOverlayPlan.cs:27-36); NativePoiPopulation.cs:46-55 | Core geometry helper |
| nav-grid edge walk x3 | TownBakeData.Walk (L85-94), TownOverlap.Grid.Step (L36-46), TownOverlayPlan.Step (L136-144); same bit layout as NPC Nav | NPCAI (one implementation) |
| overlay projection x2 | TownOverlap.Project (runtime fallback, L247-285) vs TownOverlayPlan.Build (worker) | NPCAI. Keep one; Vistas currently always uses the fallback because `Overlay` is null (TownsModule.cs:222, :281). |
| native-POI name predicate x6 | PoiModule.cs:92-97, TownsModule.cs:132-138, TownAssets.cs:89-90, InteriorPopulation.cs:57-59, NativePoiPopulation.cs:68-69, PoiDressing.cs:58-66 (`Building_/Camp_/Cave_/Wreck_`) | POI ModAPI `IsNativePoi(name)` |
| ItemSpawner int get/set x5 | TownFactory.cs:121-124, TownPopulation.cs:263-266, InteriorPopulation.cs:94-95, NativePoiPopulation.cs:93-94, TownGasStation via `TownPopulation.Read/Write` | Loot spawner helper with cached FsmInt refs |
| spawner pool/selector rewrite x3 | TownPopulation.Configure L210-230, `Force` L241-258, PoiDressMarkers.cs:88-96 | Loot `SpawnerControl.SetCount/SetPool/SetProfile` |
| ES3 cache-save + AfterSave re-save x3 | TownSaveRecord.cs:26-41, PoiDressMarkers.cs:110-120, TownGasStation.cs:371-373 (only the first verifies) | Core SaveBus "embedded ES3 record" helper |
| "extract embedded file if missing" x3 | TownNames.cs:18-23, TownWalls.cs:70-77, SurvivalModule.cs:103-128 | Core.Assets |
| map/compass marker owner-change handling x2 | TownMarkers.cs:96-120, TownGasStation.cs:404-415 | POI marker helper |
| StarterAreaDistance FsmList scan x2 | TownGasStation.cs:99-101 and :121-123 | one cached lookup (GameRefs) |
| storm prefab id + parent walk x2 | WeatherNative.cs:14, :75-83 vs Patrols/WarRig/NativeStormProtection.cs:13, :28-36 | Survival ModAPI `IsStormRoot(Transform)` |
| terrain probe per hurricane column x2 | WeatherHazards.cs:29-30 (5000 m raycast every FixedUpdate per column) vs WeatherVisuals.cs:207-214 (bounded 0.15 s probe) | the single bounded probe in visuals |
| in-game state test | Survival/StewPot/Runtime.cs:153 (own menu/save FSM state strings) | `GameRefs.InGame` |
| Apocaspawner bridges | Survival/StewPot/SpawnerBridge.cs vs Patrols SpawnerIntegration (both on Hooks.SpawnerCatalogEntries) | Compatibility |
| scene/prefab scans | PoiSpawnCatalog.cs:29 and :69 `Resources.FindObjectsOfTypeAll`, PoiPropCatalog.cs:49 and :154 (twice in the same class), TownGasStation.cs:500/:506; TownAssets.Load full graph prefab scan; NPC also catalogs the same prefabs | shared Core/Loot prefab catalog built once |

### Two systems doing the same job
- **Town pathfinding vs NPC navigation (duplicate pathfinding).** NPC Nav owns per-structure grids. Towns adds four routing layers outside NPCAI:
  - `TownWalkGraph` (plan-time visibility graph)
  - `TownNavigation` (runtime street graph + Dijkstra + capsule casts)
  - `TownApproach` (outside-wall corner detour)
  - `TownVehicleGates.Walkers` (gate graph)

  These plug into `Nav.Next` through a Harmony prefix/postfix (TownBakeBridge.cs:37). Separately, `PoiDressing.Exit` adds exit hints for custom POIs. NPCAI should own one "multi-structure route" layer with a waypoint-provider ModAPI. POI only supplies geometry (outline, gates, street nodes, exits).
- **Town traffic and parking vs Patrols.** Patrols owns traffic (TownTraffic.cs, WorldTraffic, Crew.TownParking). Towns computes parking positions, patrol bases, travel blocks and vehicle gate routing (`TownVehicleGates.Vehicles`, `VehicleWaypoint`) and builds Patrols DTOs. Patrols in turn calls internal TownsModule methods. Make it one ModAPI: POI publishes `TownInfo` (key, name, faction, condition, centre, radius, frame, outline, gates, parking slots with lane flag, access approach, merchantSeeded). Patrols does all vehicle logic. `TownRoadBridge` (dead) was a third attempt at the same.
- **Vistas compatibility scattered across POI.** `VistasWorldGen.Present` branches sit in TownPlanner (L21, 26-27, 42, 45), TownSiting (L24, 37-40, 50, 73-75), TownPaths (L31-47), TownParking:26, TownLateTerrain:15, TownGroundPad, TownGasStation (L181, 250, 283), TownsModule (L109, 222, 278-290, 472) and PoiModule:130. Compatibility should expose `WorldGen { Present, RoadLines, RoadIdentity, TerrainEdited }` through ModAPI. POI keeps a single "flatten mode" strategy object (`IPadStrategy`: MapMagic stamp vs. TerrainData foundation pad) instead of about 30 inline branches.
- **Weather storms vs NPC/Hazards/Storm.** NPC Storm.cs:73-84 (`Parked`) treats a native storm whose `SandPlayer` FSM is disabled as parked/off. WeatherNative.cs:228-229 disables exactly that FSM on every replaced storm. With Weather on, NPC storm sight/hearing (`StormSight`/`StormHearing`) and funnel-blast awareness (`InBlast`, 150 m centre spheres) therefore switch off or no longer match the WW annular walls. This is a plausible functional conflict from code reading, not verified in game. Fix: NPCAI asks Survival through ModAPI `IWeatherRegions` plus a new `StormExposure(worldPos)` / `InHazardWall(worldPos)`. The ApocaDustStorm detection in NPC Storm.cs moves to Compatibility.
- **Weather vs Patrols War Rig NativeStormProtection / DustStormBridge.** The same prefab id is defined twice. `TrackDust` keys on `SandPlayer` PlayerOn/PlayerOff (disabled by Weather). `Suction` (`Tornado.OnTriggerStay`) is moot when Weather disables the tornado colliders (WeatherNative.cs:239-240). `RadialForce` (ShouldApplyForce postfix) is reached only implicitly through WeatherHazards' delegate. Proposed shape:
  - Vehicles registers an `IShelterProvider.IsSheltered(GameObject actor)` through ModAPI.
  - Survival hazards consult it explicitly.
  - The DustStorm (ApocaDustStorm) shelter patch moves to Compatibility, calling the same provider.
  - NativeStormProtection keeps only the vanilla-storm path, which is used only when Weather is off.

### Legacy migrations / shims
| evidence | what | keep? |
|---|---|---|
| TownSettings.cs:62-74 | import from `com.denis.apocalypter.wildwasteland.towns.cfg` per missing key | keep one release, then retire |
| TownSaveRecord.cs:66-70, InteriorPopulation.cs:255-256 | read-only import of `config/WildWasteland/Towns/Saves/<slot>.towns.bin` + `.interiors.txt` | save compat: keep at least one release (AGENTS 5.10) |
| TownStore.cs:57-117, TownPopulation.cs:56-65 (`Legacy`), TownPlanner.cs:22-23 (PathsVersion < 7 rebuild), TownPopulation.Budget L118-131 (population measured from old saves) | old record versions | save compat: keep |
| SurvivalModule.cs:48-55, :103-118 | cfg migration from `com.denis.apocalypter.stewpot.cfg`; copy assets from the old plugin folder and the Apocasetter disabled cache | config migration keep; asset copy can retire after a release |
| Runtime.cs:166-172 | old-save visual-mesh collider fix | save compat: keep |
| PoiDressLayout.cs:50 | layout JSON version 1 and 2 accepted | keep |
| TownFactory.cs:60-65 | "retain the exit for saves already inside" (lab door) | keep |

## 6. Performance hot spots

| file:line | issue | fix |
|---|---|---|
| TownsModule.cs:539-548 | Wait.OnUpdate prefix runs for every Wait action; string compare first; during loading it iterates sites plus `InteriorPopulation.TownReady` (FindFsmInt by name per group) per call | Reference-compare the SaveLoadGame Fsm. Compute "pending" once per frame (cache by `Time.frameCount`). Better: a Core loading-hold API polled once per frame. |
| InteriorPopulation.cs:110-123 | ProcessEvent prefix on every FSM event; `fsm.Name` string compare | tracked-Fsm HashSet, or ref compare with an early `Groups.Count == 0` exit |
| NativePoiPopulation.cs:108-110 | EnterState prefix on every state entry game-wide; string compare | `Groups.Count == 0` early out; key on known spawner Fsm set |
| WeatherNative.cs:117-121 | ProcessEvent prefix, `ev.Name == "SaveGame"` for every event in the game | cache the `FsmEvent` instance and ref compare, or have Core SaveBus raise a synchronous "native SaveGame event" once |
| InteriorPopulation.cs:48-52, 41-47 | FsmEnable: `scene.IsValid()` before FsmName test; `GetComponents<Collider>()` allocates | reorder; `GetComponents(list)` with a reused list |
| TownBakeBridge.cs:55-62 + TownsModule.cs:642-677 + PoiDressing.cs:174-195 | Nav.Next prefix/postfix: per NPC path query, loops all towns (2x TransformPoint per site) and all POI dressing sites (2x InverseTransformPoint) even when nobody is near | Cache world-space AABBs per site (updated on OriginShift), reject by AABB first. Move into NPCAI as a provider list. |
| TownNavigation.cs:157-202 | Uncached query: 2x `StructureRootAt`, linear `Lot()` search, up to 3 physics capsule queries; memo cleared wholesale at 1024 | Root-to-lot dictionary; keep the memo but evict per age |
| TownApproach.cs:56 | `new Detour` per route change on the NPC path | pool Detour objects |
| TownOverlap.cs:110-111, 184-189, 313-326 | Reflection GetValue/SetValue/Invoke and `new object[]` per grid/cell batch; lambda closure per cell in `PortalCandidates` (L201) | belongs inside NPCAI: direct field access; replace FindIndex lambda with a loop |
| TownTowerKit.cs:73-76 | `MethodInfo.Invoke` + `FieldInfo.GetValue/SetValue` each bake step | NPCAI ModAPI `BakeIsolated` |
| PoiDressing.cs:204-221 | reflection `Invoke` on layout save | NPCAI `ForgetStructure` |
| TownAssets.cs:77-183 | First `OnBeforeTilePrepare` (main thread) scans every graph prefab: GetComponentsInChildren x6 kinds, reads and checksums `.wwnav` files for every candidate building (TownBakeData.Load), material search. This is one big hitch at first world generation. | do it at module start or sliced behind the loading screen; reuse NPCAI's decoded templates |
| TownGasStation.cs:72, 471-539 | `Available` lazily loads GLBs, SHA256 and `Resources.FindObjectsOfTypeAll<Material>/<Texture2D>` fallback inside `Capture` (OnBeforeTilePrepare) | preload sliced at Init/world load |
| TownGasStation.cs:96-102, 120-123 | `PlayMakerFSM.FsmList` scan per tile prepare while the camp is unknown (test settings on) | cache, or use GameRefs |
| PoiSpawnCatalog.cs:29-30, :69 | `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` + sort with lambda; `FindObjectsOfTypeAll<PlayMakerArrayListProxy>()` inside Step per matching loot template. Triggered at runtime by any layout with markers (PoiDressing.cs:93, :126), not only by the editor. | build once at startup in Loot catalog; never at gameplay time |
| PoiPropCatalog.cs:49-54, 151-155, 156-163 | `FindObjectsOfTypeAll<MeshFilter>()` + StringBuilder.Insert path per filter in one frame; `Sources()` repeats the scan when resolving a layout prop at runtime | Tools-only catalog; runtime resolves by stored ES3 id/source path with a lazily built dictionary built sliced |
| WeatherModule.cs:64 + WeatherHazards.cs:16-80 | Each FixedUpdate (50 Hz), per storm and column: 5000 m raycast and `OverlapBoxNonAlloc` of about 1500x1110x1500 m x width with `QueryTriggerInteraction.Collide` over layers 6/8/9/10 (up to 2048 hits), `hit.bounds` per collider. The job is always enabled. | Enable the job only while hurricanes exist. Run damage-entry checks at about 10 Hz and forces only on the native 0.5 s pulse. Reuse the visuals' cached ground offset. Use Ignore for triggers unless needed. |
| InteriorPopulation.cs:220-242 | Every 0.25 s iterates all `Senses.AllAgents` plus all members, calls `ClearDeafInvestigation` for each member every tick | fold into NPCAI senses maintenance (flag on the agent, no second loop) |
| TownFactory.cs:100-107 (via `Refresh` 0.5 s) | `FindFsmInt` by name 4x per spawner per tick for every town | cache FsmInt refs per spawner at build time |
| TownMarkers.cs:83-92, 104-105 | Every 0.25 s per town: distance test per building, `Map.Observe` per building | observe once per building on build; discovery test against the town centre and radius only |
| TownMarkers.cs:129-143 | QuestMap.Tick postfix per frame: label `Set` per frame | set label only on change |
| TownsModule.cs:470-504 | `TraceStartingTerrain` per applied tile near origin (Vistas): `GetHoles` arrays, string concat, Log.Always | Debug level 2 or delete |
| NativePoiPopulation.cs:119-164 | Per enemy spawn up to about 40 physics queries | acceptable (once per actor); keep NonAlloc; consider fewer attempts |
| PoiDresser.cs:47-48; WeatherModule.cs:59, 64 | EveryFrame/Interval jobs registered permanently while idle | toggle `Job.Enabled` on open/close or when regions appear |
| Survival/StewPot/Runtime.cs:64-86 | `Survival.Existing pots`: walks every transform of the active scene after each world load | rely on the `ES3Prefab.Awake` postfix; do the walk only on runtime enable (module turned on mid-game) |
| PoiModule.cs:98-163, TownsModule.cs:163-191 | Both scan the graph per tile prepare (lists allocated; `new List<...>(...).ToArray()` per prepare) | shared registration cache keyed by graph (POI merge) |
| TownsModule.cs:216, 268 + TownLoading.cs:17 | string building for the no-op `Report` | delete |

AGENTS section 6 violations summary:
- Reflection `GetValue`/`Invoke` in recurring paths: TownOverlap, TownTowerKit, PoiDressNavigation.
- `Resources.FindObjectsOfTypeAll` reachable during gameplay: PoiSpawnCatalog, PoiPropCatalog.
- Lambdas with captures: TownOverlap.cs:201 (per cell), PoiSpawnCatalog.cs:59-75 (capture `list`). PoiDressing.cs:92/155 are non-capturing.
- Two modules doing the same scan: native-POI predicates, storm-root lookup, ground probes.

## 7. ModAPI / multiplayer events

Ids must be stable, serializable values, never Unity references:
- town key `v1/<seed>/<x>/<z>`
- lot index
- spawner path (relative transform path)
- POI site key `<seed>/<defId>/<x>/<z>`
- gas-station key (= chunk key)
- marker key `base64(type):x10:z10:<guid>`
- storm: native ES3 reference id (manual previews get a GUID)
- stew pot: ES3 reference id

Positions use `Origin.World` space.

| event / query | publisher -> consumers | payload |
|---|---|---|
| `TownPlanned` | POI -> Tools, Quests, Patrols | key, seed, chunk x/z, faction, condition, templateId, lotCount, worldCenter, yaw, radius, hasWalls |
| `TownBuilt` / `TownStreamedOut` | POI -> NPCAI, Patrols, HUD | key, worldCenter, lot prefabs[] |
| `TownDiscovered` | POI -> HUD, Quests | key, name |
| `TownConditionChanged`, `TownLotRoleChanged` | POI -> Quests, Patrols | key, condition / lot, role |
| `TownMerchantSeeded` | Patrols -> POI (now a direct call, TownTraffic.cs:200) | key |
| `PoiSiteGenerated`, `PoiEncounterSpawned` | POI -> Patrols, HUD | defId, siteKey, worldPos, yaw |
| `GasStationPlaced` | POI -> Patrols, HUD | key, worldPos, yaw, kind (desert/town/test), lane centre, spread |
| `PoiDressingReady` | POI -> NPCAI | poiType, rootWorldPos, exits[] (pos, dir) |
| `SpawnerProgressChanged` | Loot -> POI, saves | ownerKey (town/POI/marker), spawnerPath, onOff, number, numberEnd |
| `InteriorGroupTriggered` | NPCAI -> POI, Debug | locationKey, spawnerPath, count |
| `NativePoiPopulationRolled` | NPCAI -> Debug | rootName, worldPos, empty |
| `NpcSpawned` (consumed) | NPCAI -> POI (faction member count, replaces `TownPopulation.Created`), Survival hazards | npcNetId, prefabId, faction tag, worldPos, owner spawnerPath |
| Queries `ITownsApi` | POI -> Patrols/NPCAI/Quests | `Towns(List<TownInfo>)`, `TownAt(worldPos)`, `IsTownBuilding(rootId)`, `ParkingOutside(world, out parking)`, `TravelBlocks(list)`, `Revision`, outline/gates per key |
| Query `INavApi` (NPCAI) | NPCAI -> POI | `Discover(root)`, `ForgetStructure(root)`, `FloorCells(...)`, `StructureRootAt(pos)`, `TryGetTemplate(prefab)`, `RegisterWaypointProvider`, `RegisterInstanceOverlay(root, patch)`, `BakeIsolated(prefab)`, tuning |
| `StormSpawned` / `StormRemoved` | Survival -> HUD, NPCAI, Patrols | stormId, natural/manual, worldCenter, radii, heading, velocity, hurricane |
| `StormPhaseChanged` | Survival -> HUD, NPCAI | stormId, phase (forming/critical/ending), age |
| `StormHazardHit` | Survival -> HUD, Debug | stormId, targetNetId, kind (player/NPC/vehicle), damage or impulse |
| `LightningStrike` | Survival -> audio/HUD | stormId, worldPos, time (clients play thunder by distance) |
| Query `StormExposure(worldPos)`, `IsStormRoot(id)`; `IShelterProvider.IsSheltered(actor)` | Survival <-> Vehicles, NPCAI | float 0..1, bool |
| `StewPotSpawned` | Survival -> Loot, Debug | potId, worldPos, source (natural/spawner/save) |
| `StewPotContentChanged` | Survival -> HUD | potId, litres, raw, cooked, insanity, spoiled, heating |
| `StewPotDrink` / `StewPotPour` | Survival -> HUD, multiplayer | potId, actorNetId / targetPotId, litres |
| `NaturalLootSubstituted` | Loot -> Debug | spawnerPath, worldPos, originalPrefabId (2571597281285591657), newPrefabId (7392481500621300201) |

## 8. Unmapped or ambiguous code

### 8a. Placement calls

| code | candidates | recommendation |
|---|---|---|
| TownNavigation, TownApproach, TownWalkGraph, TownVehicleGates.Walkers, TownOverlap/TownOverlayPlan.LotPatch | POI vs NPCAI | NPCAI owns routing and overlays. POI keeps plan geometry (street nodes, outline, gates) published by ModAPI. TownWalkGraph stays in the POI planner because it is part of the saved plan (`StreetNodes`/`StreetEdges`, PathsVersion 7). |
| TownVehicleGates.Vehicles, `VehicleWaypoint`, `ParkingOutside`, `TravelBlocks` | POI vs Patrols | Patrols computes vehicle routing from POI geometry |
| InteriorPopulation, NativePoiPopulation | NPCAI vs Loot | NPCAI ("population rules"). Loot only owns item spawners. |
| TownPopulation budgets (kinds 1 actors, 2 loot, 3 vehicles, 5 unique, 6 boss) | Loot vs NPCAI | Loot owns the generic ItemSpawner control (count, profile, progress). NPCAI supplies actor pools per faction. POI holds per-town %. |
| PoiDressMarkers | Tools vs Loot vs NPCAI | Tools: editor palette. Loot: Items/CarParts groups and progress (ES3 key). NPCAI: Monsters pools, Hide, Exit. |
| TownGasStation test pad / Camp_10 test | Tools vs Debug | Tools "Navigation editor" test tab (it exists to bake navigation) |
| TownMarkers labels and starting-camp icon | POI vs HUD | HUD (extend `IMapMarkers` with label/colour/size); POI only registers markers |
| TownGroundPad / TownLateTerrain Vistas paths, TownPaths Vistas roads | POI vs Compatibility | POI keeps the terrain code; Compatibility provides the Vistas facts and events through ModAPI |
| TownPoiSettings World section and reset | POI vs Core UI vs HUD | POI page through a Core "Game/World section" extension point (no patching CorePages) |
| TownLoading + BeforeHandoff | POI vs Core vs Debug | Core LoadingProgress "hold" API; timings go to Debug |
| WeatherDiagnostics, WeatherModule diagnostic snapshot | Survival vs Debug | Debug (or delete) |
| Weather renderer (Visuals/Sky/Atmosphere/Smoke/...) | Survival vs Presentation | Survival per target table; it is world presentation, not character presentation |
| WaterPour mesh | Survival vs Presentation | Survival |
| SpawnerBridge (Apocaspawner) | Survival vs Compatibility | Compatibility |
| TownsApi SetRole/SetCondition | POI vs Quests | POI ModAPI; Quests (shops/merchants) consumes |
| Car shells / wreck garden / border dressing (TownDressingPlan) | POI vs Loot | POI (static scenery, not loot) |
| `TownNavigation.Mask` and NativePoiPopulation `Solid`/`Floor` layer masks | per-file constants | Core `Layers` constants (NPCAI uses the same "static world" mask) |

### 8b. Towns editor: data model required (from TownSettings, TownPlanner, TownPoiSettings)

These rules are hard-coded today and must become template data:

| rule | evidence |
|---|---|
| Faction is a 50/50 roll inside "inhabited" (Scrapyard/Coyotes) or "ruined" (Animals/Mutants) | TownPlanner.cs:31-33; `Inhabited` % TownSettings.cs:40 |
| Condition is derived from faction | TownPopulation.Classify L51-55 |
| Walls only for owned Coyotes/Scrapyard, global switch `[Towns / Debug] Town wall prototype` | TownPerimeter.Allegiance L12-13, TownsModule.NeedsWalls L418-420, TownSettings.cs:33 |
| Gate banner textures exist only for Scrapyard and Coyotes | TownWalls.cs:66 |
| Lot slot rules: lot 0 = merchant (Camp_10) for Coyotes; lot 1 role GasStation; lot 2 = parking; wreck at i==3 and last; at most 2 camps (1 big); one crane | TownPlanner.cs:59-78, 99, 128 |
| Allowed lots: `[Towns / Assets] Test building prefabs` filters only Building_N; camps, wrecks and merchants bypass it; boss camps excluded; every lot needs a matching .wwnav bake and measured entrance | TownAssets.cs:103-112 |
| Merchant set hard-coded as `Camp_10` (+ camps with a merchant) | TownData.cs:31, TownAssets.cs:102 |
| External parking: one 24x40 m slot east of the frame at +26 m; gas station 2 m beside it if `Gas station parking lots` | TownParking.cs:15-19, :52-53, TownFactory.cs:31-39 |
| Population constants RuinsMin..LootMax (3-6 / 5-9 / 12-20) | TownSettings.cs:89 |
| Population and loot % are global | TownSettings.cs:52-55 |
| Hounds only for Scrapyard (1/3 chance) | TownPopulation.cs:19 |
| Custom POIs can appear only as the parking companion (gas station "TownsGasStation" shell); a custom POI as a lot would need a BuildingSpec (bounds, bake, entry) | - |

Proposed model:

`TownTemplate`
- `Id` (stable string), `DisplayName`, `Weight`
- `Faction` (Animals/Mutants/Scrapyard/Coyotes); `Condition` derived; `NameListFile`
- `MinLots`, `MaxLots`
- `Walls` (bool), `WallKit` (default Camp_14), `GateTexture`, `Towers` (count target), `BorderProps` (bool), `WreckGarden` (bool)
- `Lots` (list of `LotRule`)
- `ExternalParking` (list of `ParkingSlot`)
- `PopulationPercent` (min/max), `LootPercent` (min/max), `HoundChance`, `RequireMerchant` (bool)
- `Layout` {street width, entrance setback, gap, jitter, path/access width}
- `Terrain` {relief, earthwork, grade, feather}

`LotRule`
- `Source`: native prefab name **or** custom POI type (from PoiDressLayout types / registered custom POIs)
- `Kind`: Building/Camp/Wreck/Merchant/Custom
- `Role` (TownRole flags), `Required`, `MaxPerTown`, `Big`, `LootTableId` (Loot module), `PopulationProfile` (NPCAI)

`ParkingSlot`
- `Side` (N/E/S/W), `Offset` (default 26 m), `Size` (24x40), `CompanionPoi` (custom POI type, e.g. TownsGasStation), `Gap` (2 m), `Lane` (bool; Patrols lane parking)

Save impact:
- `TownPlan` must store `TemplateId`. That needs TownStore v7, with v1..v6 records mapping to a default template by faction.
- Lot prefab names stay the authoritative layout (saved towns never re-roll).
- Editor files would sit next to the existing layouts (`plugins/WildWasteland/Towns/Templates/*.json`, Core.Json).
- The Chance/Ensure/NearSpawn and War Rig chance knobs from TownPoiSettings stay global POI settings.

## 9. Save/config compatibility constraints

| item | value / location | constraint |
|---|---|---|
| ES3 key (world save) | `WildWasteland.Towns.World.v1` (TownSaveRecord.cs:11): base64 of [magic 0x57575453, ver 1, TownStore bytes, interior names[], SHA256] | keep the literal key even after the module is renamed to POI. TownStore magic 0x5757544E, Version 6, GeneratorVersion 9, LayoutVersion <= 8, PathsVersion <= 7: readers for v1..v6 must stay |
| ES3 key | `WildWasteland.Towns.PoiDressMarkers.v1` (PoiDressMarkers.cs:33), text `WWPM1` | keep. Marker identity `base64(type):round(x*10):round(z*10):<guid>` uses `Origin.World` + TypeName: do not change the formula when moving to Loot/Tools |
| ES3 key | `WildWasteland.Towns.GasStations.v1` (TownGasStation.cs:44), TSV of spawner states | keep |
| Legacy sidecars (read-only import) | `config/WildWasteland/Towns/Saves/<slot>.towns.bin` and `<...>.interiors.txt` (TownsModule.cs:707 uses `Assets.ModuleConfigDir(Id)`) | **Id-dependent path**: if the module Id becomes "POI", hard-code "Towns" here |
| POI sidecar | `config/WildWasteland/POI/Saves/<slot>.poi.txt` (spawned site keys "seed/defId/x/z"; written in OnSaveBegin directly, not through `SaveBus.AfterSave`) | keep path and format; losing it duplicates War Rig encounters (docs/modules.md:167) |
| Town name lists | `config/WildWasteland/Towns/Names/{Animals,Mutants,Scrapyard,Coyotes}.txt` (TownNames.cs:15, literal "Towns") | user-edited: keep the path |
| POI dress layouts | `plugins/WildWasteland/Towns/Layouts/<type>-<FNV hash X8>.json` + `.bak` (PoiDressLayout.cs:33-38) | keep folder and `TownPlanner.Hash` (FNV-1a) unchanged |
| Gas-station assets | `plugins/WildWasteland/Towns/Models/gasstation.glb`, `gasstation_collider.glb`, `<texture>.png`. Mesh name = file + SHA256 of GLB bytes (TownGasStation.cs:559); this feeds the NPC nav-bake signature | keep the naming; the Core glTF merge must keep identical vertex output (x mirror, winding, v flip, concrete_1 slab offset) or every gas-station bake invalidates |
| Perimeter textures | `plugins/WildWasteland/Towns/Perimeter/gate_scrapyard.png`, `gate_coyotes.png` | keep |
| Embedded resource logical names | `Towns.Names.*.txt`, `Towns.Perimeter.*`, `Survival.Models.*`, `Survival.Sounds.*`, `Weather.weather.bundle`, plus `NPC.NavMaps.Vanilla.*` read by TownBakeData | derived from folder `<Id>/Embedded`: moving folders changes names, so keep the old logical names via csproj LogicalName |
| Object/prefab name contracts | `TownsGasStation*` (nav bake identity, dressing layout type, PoiDressing.TypeName), `TownsTower*` (excluded from custom POIs), `<Prefab> (Towns <hash>/<lot>)` (NativePoiPopulation skips " (Towns "), `WW.POI.<id>` site markers | keep |
| Determinism | `TownPlanner.Hash` seeds with strings `key+"/gasstation"`, `"/gasstation/site"`, `"/gasstation/population"`, `"/wreck-garden"`, `"/border-props-v1"`, `"/population"`, `"/town/<kind>/<lo>/<hi>"`, `"/<lot>/native/<path>"`, `"/balance/<kind>"`, `"/unique/<lot>/<n>"`, `root.name+"/native-population"`; town key format `v1/<seed>/<x>/<z>`; POI seed from `definition.Id` (PoiModule.cs:128) | any change re-rolls existing worlds |
| ES3 prefab ids | Stew Pot 7392481500621300201 (custom, `Hooks.GetPrefab` resolver; template from `Water_Can_Plastic`); natural gas stove 2571597281285591657; SandStorm 5697600806714206537; Scraffa 2524626245796737373; gas-station material ref ids (TownGasStation.cs:491) | keep |
| SaveTypes aliases | none needed today: no custom component type of this area is ES3-serialized (`PoiSite` sits on MapMagic objectsPool markers, the Stew Pot uses native components only) | renaming namespaces (Towns->POI, Weather->Survival) is safe for ES3 as long as no new saved component is added |
| Config: Towns (settings.cfg, bound with raw `ConfigFile.Bind`, literal sections) | `[Towns] Enabled`; `[Towns / Editors] POI dresser`; `[Towns / Debug]` Test Camp_10 spawn, Test gas station, Gas station scale, Town wall prototype, Loading build budget (ms), Minimum/Maximum population (%), Minimum/Maximum loot (%); `[Towns / World]` Desert gas station chance (%), Gas station parking lots, New POI near spawn; `[Towns / Generation]` Minimum buildings, Maximum buildings, Town chance (%), Ensure selected towns, Inhabited towns (%), Street width, Building setback (retained legacy), Entrance setback, Gap between buildings, Layout irregularity, Maximum original relief, Maximum cut or fill, Maximum original grade, Pad transition width, Entrance path width, Access road width, Maximum access road length; `[Towns / Assets]` Test building prefabs, Road terrain layer | Sections are literal strings, so they survive an Id change. `[Towns] Enabled` is Id-derived: migrate it to `[POI] Enabled` or keep two masters. Note: tuning/debug keys live in settings.cfg, not tools.cfg (rule 5.7), and bypass `cfg.Player/Tool`. Legacy source: `com.denis.apocalypter.wildwasteland.towns.cfg`. |
| Config: POI | `[POI] Enabled`, `[POI / World] Spawn chance (%)` (default 2; Towns reads it by ConfigDefinition, TownPoiSettings.cs:26) | keep |
| Config: Weather (settings.cfg via `cfg.Player`) | `[Weather] Enabled`; `[Weather / Map] Storm areas`; `[Weather / Storms]` Hurricanes per km², Front width (km), Depth (km), Storm darkening; `[Weather / Colors]` External wall color, Main tint color, Hurricane color, Vanilla sand effect color | **Id-derived sections**: merging into Survival renames them to `[Survival / ...]`, so `cfg.Migrate` from `[Weather / ...]` is needed. `Dir` (sound files at `plugins/WildWasteland/Weather/sound/`) and `config/WildWasteland/Weather/diagnostic-snapshot.bin` are Id-derived too. |
| Config: Survival | `[Survival] Enabled` (settings.cfg); tools.cfg `[Survival / Model] Scale`, `[Survival / Stew Pot]` Nutrition conversion multiplier, Cooking rate, Insanity boilaway rate, Spoiling rate; `[Survival / Debug] VerboseLog`, AllowStewPotSpilling | VerboseLog is retired into Debug level (leave the orphan, never purge). Migration source `com.denis.apocalypter.stewpot.cfg`; ConflictsWith GUID `com.denis.apocalypter.stewpot`. |
| Coexistence | Towns has no `ConflictsWith`; docs/modules.md:23 asks to remove standalone `WildWastelandTowns.dll` manually | add the standalone GUID `com.denis.apocalypter.wildwasteland.towns` (seen in TownDebug.cs:44) to ConflictsWith of the POI module so ReplacementGuard handles it |
| Shared F7 reset | TownPoiSettings.ResetTown, `Reset POI chances (2% / 50%)` button | keep behaviour or document the default (20 vs 50) |
