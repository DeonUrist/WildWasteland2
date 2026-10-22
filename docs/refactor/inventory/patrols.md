# Inventory - Patrols (src/Modules/Patrols/**)

Snapshot 2026-10-22. 95 .cs files, 21 720 lines, 1.38 MB (long lines). Namespaces: `WildWasteland.Modules.Patrols` (main), `.Patrols.WarRig` (13 namespace blocks in WarRig/), `.Patrols.Motorcycles` (2 files). `Embedded/` is empty in the snapshot (defaults are read through `Core.DiskResources`: `Patrols.DefaultTemplates.*`, `Patrols.EditorDefaults.json`, `Patrols.MerchantLayouts.defaults.json`).

Target legend: **Pat** Patrols, **Veh** Vehicles, **POI**, **Loot**, **Tools**, **Dev** Developer, **Pres** Presentation, **NPC** NPCAI, **Gun** Gunplay, **HUD**, **Surv** Survival(+Weather), **Compat** Compatibility, **Core**, **Dbg** Debug.

## 1. File map

| path (src/) | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/Patrols/PatrolsModule.cs | 291 | Module entry: Init/Shutdown, runner GameObject with 9 components, 19 scheduler jobs, hotkeys, provides 3 services | Pat (+Core) | SPLIT: lifecycle/jobs stay Pat; `EnsureGameFont`/`Pressed` -> Core (`Hotkeys.Pressed`, Skin); editor components (`_menu,_exporter,_spotter,_busPose,_merchantEditor`, their jobs/hotkeys) -> Tools/Dev; `Services.FollowerVehicles` provider moves with QuestFollowers split |
| Modules/Patrols/PatrolsSettings.cs | 573 | Binds ~140 F6/tools.cfg entries + pose config classes + legacy cfg migration | Pat, Veh, Pres, NPC, Tools, Dev, Core | SPLIT: `BonePoseConfig/PoseFloat/PoseBool/ArmPoseProfile/WeaponPoseConfig`, `[Patrols / Pose]`, `Pose.<Human>`x7, `Turret pose` -> Pres; `War Rig spawns/ground recovery`, `ExitSpeedKmh`, `SuspensionLiftCenterOfMass`, `NPC ram damage multiplier` -> Veh; `AudioVoices` (read by Core/Plugin.cs:114) and `Hide missing-FSM warnings` (drives Core.QuietFsm) -> Core; editor keys/toggles (`MenuKey`,`TemplateDumpKey`,`AllowDebugSpawns`) -> Tools; `TurretSpotEditor`, `Bus passenger pose editor`, `Merchant item editor` (+keys) -> Dev; `AiOverlay`, `DebugMap` -> Dbg; Rider combat tuning (`Rider*` in Combat tuning) -> NPC; rest Pat. Keys must keep section/key (see 9) |
| Modules/Patrols/PatrolsPages.cs | 234 | F7 pages: Patrol editor, recorder, turret spot, bus pose, merchant placement, War Rig, debug, traffic, spawning, merchants | Tools, Dev, Pat, Veh | SPLIT: `PatrolEditorPage`,`RecorderPage` -> Tools; `TurretSpotPage`,`BusPosePage`,`MerchantSpotPage` -> Dev; `WarRigPage` -> Veh/POI; `DebugPage` -> Dbg; `WorldTrafficPage`,`SpawningPage`,`MerchantsPage` stay Pat |
| Modules/Patrols/PatrolsData.cs | 66 | First-run copy of Apocapatrol/WarRig plugin data folders into WW folders | Pat (+Veh for WarRig) | MOVE (keep for >=1 release; WarRig branch goes with Vehicles) |
| Modules/Patrols/Crews/Crew.cs | 697 | Per-car driver owner: drive/hold/death/bail/promote/revive/overheat/fuel, mutes NPC FSMs | Pat | SPLIT: stays Pat except `MuteAi(...)` (disables NPC AI FSMs directly) -> NPC service `INpcApi.SetSeated(npc,bool)`; `ApplyOverheat*` (part heat FSM) -> Veh |
| Modules/Patrols/Crews/CrewCombat.cs | 83 | Crew reaction when attacked: dismount vs pursue, consumes `Services.NpcHurt` | Pat | MOVE (decision is "vehicle crew" level; uses NPC only via events) |
| Modules/Patrols/Crews/CrewReturn.cs | 437 | Dismounted crew walk back via `Services.Npc.Travel`, reseat copies, stranded crew -> foot patrol | Pat | MOVE (already uses Services.Npc; `HealthVar` -> Core FsmUtil helper) |
| Modules/Patrols/Crews/CrewPoi.cs | 184 | `PatrolPoi`: car parks beside POI player is in, reads `Services.Npc.Structures` 2 Hz | Pat | MOVE; replace `TownsApi.Provider.ParkingOutside` (CrewPoi.cs:50) with ModAPI; share Structures snapshot with TrafficLocations |
| Modules/Patrols/Crews/CrewBusDefence.cs | 101 | Coyote bus crew defends bus, repairs wheels, consumes `Services.VehiclePartHit` | Pat | MOVE |
| Modules/Patrols/Crews/CrewFactions.cs | 20 | Faction rosters (Coyotes/Scrapyard travellers), template faction checks | Pat (data) | MOVE; roster lists overlap NPC population/Quests factions (see 5) |
| Modules/Patrols/Crews/CrewAppearance.cs | 51 | Gungirl/Shoota <-> Flexa/Sprokka alias, `Services.NpcAppearance.Dress` | Pres | MERGE-INTO Presentation character-model API (alias table duplicated with NPC/Women) |
| Modules/Patrols/Crews/Pilot.cs | 1006 | Driving AI (`Pilot`) 10-20 Hz decisions, feelers, ram/charge/recover; `PlayerRef` player/car/godmode cache | Pat, Core | SPLIT: `Pilot` stays Pat (driving AI); `PlayerRef` (l.835-1006) -> Core.GameRefs (`PlayerCar`, `PlayerVelocity`, `GodMode`, `Target`) |
| Modules/Patrols/Crews/Passenger.cs | 435 | `PassengerGuard`: seated ranged shooter (vanilla Attack FSM), melee neutralise, aim, quest follower combat | NPC, Pres, Pat | SPLIT: `CombatAllowed/CanAnswer/FindVanillaTarget/IndexTargetSources/Consider/ForceRangedOnly/MuteNonCombatAi/Quest*` -> NPC (seated combat service, also used by Quests); `LateUpdate` aim/`HideMeleeWeapons`/`AimPoint` pose part -> Pres; `AttachDriver/AttachTurret/SetDriverMode/ScheduleBurst` orchestration stays Pat |
| Modules/Patrols/Crews/Pose.cs | 412 | Seated limb pose for every occupant/turret, spine aim, weapon roots, bus preset | Pres | MOVE (type name `Pose` must stay resolvable: Quests Hire.cs:281 and saves) |
| Modules/Patrols/Crews/PlayerAnims.cs | 74 | Human turrets animated via `Services.Animation` (crouch/aim/throw clips) | Pres | MERGE-INTO Presentation animation service (becomes internal call) |
| Modules/Patrols/Crews/RiderAnchorGuard.cs | 38 | LateUpdate keeps turret body/kinematic parent on its anchor after animation | Pres | MOVE |
| Modules/Patrols/Crews/BusPassengerPose.cs | 126 | `BusPassengerPoseStore` (BusPassengerPose.json preset) + numpad `BusPassengerPoseEditor` | Pres, Dev | SPLIT: `BusPassengerPreset`,`BusPassengerPoseStore` -> Pres; `BusPassengerPoseEditor` -> Dev |
| Modules/Patrols/Crews/Rider.cs | 1173 | Human turrets: seating, blast-lance warboy (throw, leap, detonate), gunner, PlayableGraph anims, Wav decoder, RiderApi | Pat, NPC, Pres, Gun, Dev, Core | SPLIT: `Rider.Seat/Slots/AutoSpot/TopAt/IsGunner/IsWarboy/Display/Stored/IsBikeBody/TurretChoices` -> Pat; `RiderGuard` decisions (`Update` witness/leap/throw choice, `CanLeap/Leap/Fly/JumpOff/CombatDismount/SpawnMob/Hop/Land`) -> NPC; `RiderGuard.BuildGraph/Play/Blend`, `JumpClip`, `Rider.Clip/Bone` -> Pres; `Solve/U/FlatLeap/BuildLance/Release`, `Detonator`, `LanceFuse`, `ExplosionPrefab/Boom/Kaboom` -> Gun (projectiles); `Wav` -> Core (dup); `RiderApi` -> Dev (TurretSpotter only), delete `MoveAnchor/SaveLance/LanceOffset/LanceEuler` (retired Apocaspotter shim) |
| Modules/Patrols/Crews/Ram.cs | 264 | Ram damage to player (Ram.Sensor collisions), CarAttack own-crew guard, NPC ram multiplier, push | Pat, Veh | SPLIT: `Sensor`,`Hit`,`HurtPlayer`,`Push`,`BeforeSetFsmFloat`,`BeforeSendEvent` stay Pat; `AfterSetFsmFloat` (all cars' damage x NpcRamDamageMultiplier) -> Veh |
| Modules/Patrols/Crews/MeleeWheels.cs | 251 | Player melee hits fitted wheels (fallback when Guns/Apocaraider do not) | Gun | DELETE (duplicate: Guns Tracers.cs:955/1163 `MeleeOnWheels` default true makes this stand down; Apocaraider detection -> Compat if kept) |
| Modules/Patrols/Crews/SeatedLosGuard.cs | 27 | LOS sensor action without target finishes (seated crews), shared hook | NPC | MOVE (NPC senses domain; NPC already prefixes same dispatcher) |
| Modules/Patrols/Crews/SimpleBanditBridge.cs | 37 | `SimpleBanditTag` + calls `Services.Npc.SetSimpleConvoyBandit*` | Pat | MOVE (internal ModAPI client, not third-party; rename e.g. `CrewNpcMode`) |
| Modules/Patrols/Crews/QuestFollowers.cs | 61 | `IFollowerVehicles` provider for Quests: seat pose, seat combat, IsCrew, ResolveType | Pat, Pres, NPC | SPLIT: `IsSeated/IsCrew/FormerCrew/IsHired` stay Pat; `SeatPose/ReleasePose/Animated` -> Pres; `SeatCombat/CanFire/Combat/ShootDistance` -> NPC; `ResolveType` DELETE (no caller; QuestsCompat retired) |
| Modules/Patrols/Editor/EditorData.cs | 192 | `EditorData` (patrol/convoy/merchant groups, template order), `SpawnGroup`, `LootProfile/LootItem`, `StoreJson` writer | Pat, Loot, Core | SPLIT: `SpawnGroup`,`EditorData` -> Pat data model; `LootProfile`,`LootItem` -> Loot; `StoreJson` -> Core.Json |
| Modules/Patrols/Editor/EditorStore.cs | 187 | Loads/saves PatrolsAndLoot.json, one-time upgrades (markers), loot lookup, AtomicWrite, SafePath | Pat, Loot, Core | SPLIT: store stays Pat (Loot part -> Loot store); `AtomicWrite` -> Core.AtomicFile; `SafePath/UniquePath` -> Core |
| Modules/Patrols/Editor/EditorSelection.cs | 14 | One helper for expanded-group state | Tools | MERGE-INTO TemplateMenu |
| Modules/Patrols/Editor/PickupCatalog.cs | 56 | Item list for loot editor: scans all asset FSMs + reads Apocaspawner cfg [Names] | Loot (+Compat) | SPLIT: catalog -> Loot built on Core prefab catalog (dup of `Prefabs.Scan`); Apocaspawner cfg name read -> Compat |
| Modules/Patrols/Editor/ShellRecycle.cs | 55 | Native IFileOperation recycle-bin delete on STA thread | Tools (or Core) | MOVE to Core utility (generic) |
| Modules/Patrols/Editor/TemplateExporter.cs | 456 | Template recorder (capture live car to TemplateFile JSON); `PrefabName` regex helper used everywhere | Tools, Core | SPLIT: recorder -> Tools (tab in Patrols editor); `PrefabName`/`CloneSuffix` -> Core (used by 15 runtime files); `BedItems` DELETE (dead); unused `using BepInEx.Logging` |
| Modules/Patrols/Editor/TemplateMenu.cs | 87 | Patrol editor MonoBehaviour shell (session, tick, open) | Tools | MOVE |
| Modules/Patrols/Editor/TemplateMenu.Ledger.cs | 477 | Patrol editor UI (groups, templates, loot, merchants, debug spawns) | Tools | MOVE (via ModAPI only); `MoveTemplate` DELETE (dead) |
| Modules/Patrols/Editor/MerchantEditor.cs | 78 | Numpad merchant shop-object placement editor | Dev | MOVE |
| Modules/Patrols/Editor/TurretSpotter.cs | 148 | Numpad turret-spot editor, writes riderPos to template | Dev | MOVE |
| Modules/Patrols/Merchants/MerchantRuntime.cs | 210 | `MerchantHost` per bus: shops, stock, park, probes, demand lists | Pat (+Loot) | MOVE; stock goods lookup could use Loot API |
| Modules/Patrols/Merchants/MerchantGuards.cs | 55 | Merchant turret guard health/restore | Pat | MOVE |
| Modules/Patrols/Merchants/MerchantModel.cs | 98 | `MerchantCatalog` (types, goods), merchant DTOs, `MerchantApi`, `MerchantLayouts` (MerchantLayouts.json) | Pat, Dev, Loot | SPLIT: DTOs/catalog stay Pat; `MerchantCatalog.Goods` -> Loot; `MerchantApi` (public, Dev only) -> Dev-facing ModAPI; layouts store stays Pat |
| Modules/Patrols/Merchants/TrafficMerchants.cs | 95 | WorldTraffic partial: merchant routes/population, horn signals | Pat | MOVE |
| Modules/Patrols/Persistence/Persistence.cs | 663 | `PatrolSaveData/PatrolCarData` schema 14, `PatrolMarker` (per car), `PatrolPersistence` save/restore, ES3 key + sidecar | Pat (+Core) | SPLIT: `PatrolMarker` stays Pat; I/O (ES3 key, sidecar, `AtomicWrite`, slot paths, quit flush) -> Core SaveBus sidecar API; format code stays Pat |
| Modules/Patrols/Persistence/PatrolCrewDelta.cs | 110 | WDR1/WDR2 tagged extension: seated riders + trailer identity | Pat | MOVE |
| Modules/Patrols/Persistence/PatrolLoadPreparation.cs | 36 | Holds SaveLoadGame `Wait for Terrain Gen` until cache+restore ready | Pat (-> Core) | MERGE-INTO Core load barrier (`LoadingProgress`/VehicleLoadPlacement share the same prefix) |
| Modules/Patrols/Spawning/Patrol.cs | 1890 | Runner + build pipeline + crew seating + Prefabs index + InputControl + DayLight + ExitSpeed + Register + Nwh | Pat, Veh, Core, Surv | SPLIT (biggest file, 9 classes): see 1a |
| Modules/Patrols/Spawning/BuildQueue.cs | 101 | Steps build coroutines on `Patrols.Build` slice + `WorldWork` | Veh (-> Core) | MERGE-INTO Core.WorldWork queue (see 5 dup budgets) |
| Modules/Patrols/Spawning/VehicleBuildCache.cs | 252 | Dormant pre-assembled vehicle reserves, load-time burst, LoadingProgress work | Veh | MOVE (Vehicles assembly) |
| Modules/Patrols/Spawning/Cleanup.cs | 110 | Removes abandoned raider cars by distance/age/cap; marks player-entered | Pat (-> Core) | REWRITE as participant of Core.SaveCleanup (same far-timer pattern) |
| Modules/Patrols/Spawning/Convoy.cs | 304 | Distance/boss progression, group roll, editor test spawn, staggered builds, Apocasaver autosave probe | Pat, Compat, Core | SPLIT: progression/roll/BuildAll stay Pat; `AutosaveSoon` -> Core SaveSystem query (Apocasaver is replaced by WW saves); `BossKills` -> Core progression query; `DebugSpawn/DebugRoll` DELETE (dead) |
| Modules/Patrols/Spawning/EventBudget.cs | 42 | `EncounterCar` tag + per-event car caps (MaxCarsInEvent / DoubleEvent) | Pat | MOVE, rename `EncounterCap` (NOT a frame budget) |
| Modules/Patrols/Spawning/PatrolVisibility.cs | 51 | Player-car lights/engine-HP cache for patrol sensing; empty legacy component type | NPC (+Pat shim) | SPLIT: sensing -> NPC senses service; empty `PatrolVisibility` MonoBehaviour must stay as save-compat type (see 9) |
| Modules/Patrols/Traffic/WorldTraffic.cs | 780 | `TrafficAgent` (per-car route goal + player sensing), `TrafficPlatform`, `WorldTraffic` core journey tick | Pat, NPC | SPLIT: journeys stay Pat; `TrafficAgent` sight/hearing (`_nextSense` block, `Sight`) -> NPC senses query |
| Modules/Patrols/Traffic/TownTraffic.cs | 326 | `TownStop`, town visits/parking/guards/merchant seeding via `TownsApi.Provider` | Pat | REWRITE coupling: via ModAPI `ITownTravel` (see 2) |
| Modules/Patrols/Traffic/TrafficBattles.cs | 274 | Faction battles between journeys, `BattleCar` | Pat | MOVE |
| Modules/Patrols/Traffic/TrafficCamps.cs | 159 | Camp status (`ICampStatus` provider), `CampResident`, CreateObject spawn hook | POI/NPC (+Pat) | SPLIT: camp load/clear state -> POI world-locations service (consumed by Quests too); Pat consumes |
| Modules/Patrols/Traffic/TrafficCodec.cs | 63 | Reflection DTO <-> JSON for traffic/merchant records | Core | MERGE-INTO Core.Json |
| Modules/Patrols/Traffic/TrafficCompass.cs | 133 | Compass/map markers per journey via `Services.Compass/Map` | Pat | MOVE (already ModAPI) |
| Modules/Patrols/Traffic/TrafficElevation.cs | 108 | `ElevationGrid` snapshot + `TerrainWaypoints` foot route builder (worker) | Pat / NPC | SPLIT: `ElevationGrid` -> shared routing core; `TerrainWaypoints` foot long-route -> NPC (foot planning) |
| Modules/Patrols/Traffic/TrafficFactions.cs | 129 | `TrafficActor` per NPC (sensor tags, foot target 0.6 s), faction relation, car/foot target choice | NPC, Pat | SPLIT: `TrafficActor` (sensor AllowedTags reflection, FootTarget) + `PlayerHostile` -> NPC/Core faction service; `DriveTarget/ForCar/Target` stay Pat |
| Modules/Patrols/Traffic/TrafficFoot.cs | 564 | Foot patrols: virtual/real walkers via `Services.Npc.Travel`, stranded crews | Pat (+NPC) | MOVE; `FootAlive` DELETE (dead); synchronous `RoadPlanner.Build` (l.319,401) -> worker |
| Modules/Patrols/Traffic/TrafficLocations.cs | 364 | Scene walk for camps/POIs/starting area, NPC structure maps, blocks, Towns counts | POI, Pat | SPLIT: discovery/`Known/Points/Blocks` -> POI world-locations service; `RoadRoute`,`LegSafe` DELETE (dead); `IsTown` (TownMarkers.Owns) via ModAPI |
| Modules/Patrols/Traffic/TrafficMap.cs | 293 | Draws routes/markers on native map M + War Rig notice (Gui job) | HUD, Pat | SPLIT: route/marker data provider stays Pat; drawing (Gui, map camera lookup) -> HUD map layer |
| Modules/Patrols/Traffic/TrafficMapInk.cs | 51 | Route ink texture rasteriser | HUD | MOVE |
| Modules/Patrols/Traffic/TrafficModel.cs | 147 | Saved DTOs TrafficBase/Poi/Vehicle/Walker/Journey/Save + `TrafficRules` | Pat | MOVE (DTO field names are save format) |
| Modules/Patrols/Traffic/TrafficReinforcements.cs | 82 | Camp patrol calls (reinforcements) | Pat | MOVE |
| Modules/Patrols/Traffic/TrafficRoads.cs | 416 | MapMagic road spline capture, final-ground slice, road cache v3 IO thread | Pat (routing core) | MOVE (Towns + Vistas also feed it; terrain tile events -> Core dispatcher) |
| Modules/Patrols/Traffic/TrafficRoaming.cs | 47 | Roaming departures | Pat | MOVE |
| Modules/Patrols/Traffic/TrafficSpace.cs | 23 | Wrapper over Core.Origin + origin-shift fan-out | Core | MERGE-INTO Core.Origin (keep `OriginShift` in PatrolsModule) |
| Modules/Patrols/Traffic/TrafficState.cs | 139 | Snapshot of physical car state (parts, cargo, fluids) into journey record | Pat | MOVE |
| Modules/Patrols/Traffic/VehicleRoute.cs | 160 | Worker A* over road centrelines with grade limits | Pat (routing core) | MOVE |
| Modules/Patrols/Traffic/RoadPath.cs | 88 | Route follower: progress/target/speed with ConditionalWeakTable metrics | Pat | MOVE |
| Modules/Patrols/Traffic/RoadPlanner.cs | 199 | Route job queue + worker + coarse fallback chain | Pat (routing core) | MOVE; Towns' Harmony patch on `Build` replaced by ModAPI hint provider |
| Modules/Patrols/Vehicles/CarTemplateFile.cs | 349 | `TemplateFile/TemplatePart` JSON model + hand-written `Json` parser/writer | Veh (format), Core | SPLIT: `TemplateFile/TemplatePart` -> Veh (vehicle assembly format, also used by Tools/POI); `Json` -> Core.Json (dup) |
| Modules/Patrols/Vehicles/CarTemplates.cs | 177 | Template park loading (Base/Player folders), legacy import, parse | Pat | MOVE (template park = patrol data model) |
| Modules/Patrols/Vehicles/Templates.cs | 177 | `CarTemplate` runtime model, `RamTargets` | Pat (+Veh) | MOVE; vehicle-structure part (`Body/Parts/Bumpers/Trailer`) consumed by Veh assembly |
| Modules/Patrols/Vehicles/Cargo.cs | 209 | Spawns loot items in truck bed PhysicsLock volume | Loot | MOVE (cargo loot); `SeatSurface/FloorAt` RaycastAll -> NonAlloc |
| Modules/Patrols/Vehicles/Explode.cs | 407 | Self-destruct of vacated raider cars: char, pop parts, wreck, blast, cleanup distance | Pat, Veh | SPLIT: `Schedule/Routine/Eligible/Roll/DefaultChance/BattleWreck` stay Pat; `Darken/Deaden/SetCondition/StopTweens/QuietHinges/StopRpm/StopLoops/IsWreck/RestoreDead/BlastAt` -> Veh `IVehicleWreck`; `Preserved` DELETE (dead) |
| Modules/Patrols/Vehicles/ModVehicles.cs | 72 | Registry of runtime vehicle factories + reflection scan of other assemblies' `Factory` | Veh, Compat | SPLIT: `Register/Template` -> Veh factory registry; assembly scan -> Compat |
| Modules/Patrols/Vehicles/MotorcycleIntegration.cs | 77 | Motorcycle ES3 prefab resolver, rear collider trim, MotorcycleMod bridge | Veh, Compat | SPLIT: `ResolvePrefab/TrimRearCollision/MotorcycleRearAccess/IsBody` -> Veh; MotorcycleMod assembly lookup + `Factory.Template` patch -> Compat |
| Modules/Patrols/Vehicles/Motorcycles/Factory.cs | 166 | Builds motorcycle chassis from Crossbreed prefab | Veh | MOVE (ns `.Motorcycles`, prefab id fixed) |
| Modules/Patrols/Vehicles/Motorcycles/MotorcycleBalance.cs | 60 | Lean/roll PD controller (FixedUpdate) | Veh | MOVE (public saved component type) |
| Modules/Patrols/Vehicles/Paint.cs | 418 | Custom frame paint (MaterialPropertyBlock), cargo container textures, frame repair | Veh (+Loot key) | MOVE |
| Modules/Patrols/Vehicles/Suspension.cs | 325 | Template lift-kit width/height; Part Adjustment API or own `SuspensionTune` | Veh, Compat | SPLIT: `SuspensionTune`, `AfterSetPosition` -> Veh; PartAdjustment `SuspensionApi` reflection -> Compat |
| Modules/Patrols/Vehicles/TrailerSupport.cs | 97 | Trailer detection/joint connect | Veh | MOVE |
| Modules/Patrols/Vehicles/VehicleAttachments.cs | 29 | Free attachment part flags (CheckBool Attached) | Veh | MOVE |
| Modules/Patrols/Vehicles/VehicleRules.cs | 19 | Exit distance by body | Pat | MOVE; `CanTemplate` DELETE (dead, always true) |
| Modules/Patrols/WarRig/WarRigFeature.cs | 138 | War Rig install, `SpawnNearPlayer`, `PrefabLookup`, Apocaspawner `SpawnerIntegration` | Veh, Compat | SPLIT: install/prefab lookup/SpawnNearPlayer -> Veh; `SpawnerIntegration` -> Compat |
| Modules/Patrols/WarRig/Factory.cs | 361 | Trailer_Big clone + GLB mesh + deterministic ES3 ids | Veh | MOVE (ns `.WarRig`) |
| Modules/Patrols/WarRig/Glb.cs | 226 | Static GLB reader | Core | MERGE-INTO Core Glb (dup of Towns/GasStation/Glb.cs, Survival/StewPot/Glb.cs) |
| Modules/Patrols/WarRig/Json.cs | 117 | Minimal JSON reader for glTF | Core | MERGE-INTO Core.Json (dup x5) |
| Modules/Patrols/WarRig/Layout.cs | 160 | Applies wheels/colliders/hitch layout to a rig | Veh | MOVE |
| Modules/Patrols/WarRig/Props.cs | 255 | Camp props (ladder etc.) cloned onto the tanker | Veh | MOVE |
| Modules/Patrols/WarRig/Settings.cs | 75 | `S` constants + 2 toggles + parsers | Veh | MOVE (parsers -> Core) |
| Modules/Patrols/WarRig/WarRigUnit.cs | 161 | Per-rig component: visuals, attacher gate, ground recovery, gizmos | Veh | MOVE (public saved component) |
| Modules/Patrols/WarRig/GroundRecovery.cs | 272 | Rescue rig that fell through terrain | Veh | MOVE |
| Modules/Patrols/WarRig/NativeStormProtection.cs | 140 | Shelters player inside rig from sandstorm damage/tornado suction | Surv, Veh | SPLIT: storm patches -> Surv (weather owns storm damage; WeatherNative.cs already patches Explosion/storm FSMs); Veh registers War Rig as a shelter via ModAPI |
| Modules/Patrols/WarRig/DustStormBridge.cs | 61 | ApocaDustStorm shelter/hazard patches | Compat | MOVE (calls the Surv shelter query) |
| Modules/Patrols/WarRig/PoiEncounter.cs | 145 | Abandoned War Rig POI: register with `Services.Poi`, spawn truck+tanker+zombies, cleanup retention patch | POI, Veh, Core | SPLIT: `Register/Spawn/Completion/Prepare/Wheels` -> POI (uses Pat/Veh spawn via ModAPI); `InstallRetention/BeforeCleanupDistance/Protected/DetachTankers` -> Core far-cleanup retention service (Performance PatrolsBridge already asks for it) |
| Modules/Patrols/WarRig/WorldSpawning.cs | 74 | Tanker clearance check, natural-rig list for map notice | Veh (+POI) | SPLIT: `CanSpawnAt/IsTemplate` -> Veh; `NaturalRigs/NaturalNoticeUntil` -> POI (map notice data) |

### 1a. Spawning/Patrol.cs split (1890 lines)

| lines | class / members | destination |
|---|---|---|
| 13-108 | `Patrol.ResetForScene/Tick/LightsTick/PruneIgnoreSignatures/Gui/GuiWanted` | Pat (runner) ; `SaveBusy/WaitForSave` DELETE -> `SaveBus.Busy` |
| 110-325 | `Spawn/SpawnAt/CancelBuild/Build` (crew + cargo + template orchestration) | Pat (calls Veh assembly) |
| 326-707, 1076-1095 | `PackStructure, AttachMissingHeadlights, Headlights, RaiderMusic, DriverSeatPos, OpenDoorAt, StartUp, AttachAll, Attach, AttachTemplatePart, HingeOffset, OnItem, HasPart, PartOn, FindPath, Fuel, FillParts, SetLiquid, Handbrake, SetPartConditions` | Veh (`IVehicleAssembly`); `RaiderMusic` stays Pat |
| 708-1075 | `SeatDriver, SeatLiveDriver, SeatPassenger, SeatOccupant, IgnoreCollisions*, EjectCorpses, Launch, Throw, MoveToDriverSeat, BailOut, GetHealth, SetHealth` + nested `Patrol+CorpseEject`, `Patrol+LateHealth` | Pat (crews); `GetHealth/SetHealth` -> Core FsmUtil; `Launch/Throw` ragdoll push -> Pres |
| 1153-1186 | `PlayerPose, FindChild, FindFsm, Alive` | Core utilities |
| 1188-1373 | `Prefabs` (asset index, ES3 manager hooks) | Core prefab catalog (dup scans in 12 other files, see 5) |
| 1379-1443 | `InputControl` (takes car INPUT FSMs) | Veh (AI/remote driving input) |
| 1444-1501 | `DayLight` (Azure sun / DayTime FSM) | Surv (sky/time query) or Core |
| 1502-1554 | `ExitSpeed` (player exit-speed literal in PlayerCamera FSM) | Veh (player vehicle rule) |
| 1555-1680 | `Register` (game save registry lists, itemNameID counter, RemoveTree) | Core (`WorldRegistry`; also used by WarRig, Survival, Towns, Guns, NPC Women) |
| 1681-1889 | `Nwh` (NWH input/engine/gear, typed + reflection fallback) | Veh (shared with EngineSimCompat ignition) |

## 2. Cross-module coupling

### 2.1 Direct module-to-module type references (RULE VIOLATIONS)

| from | to | what | why |
|---|---|---|---|
| Patrols Crews/Pilot.cs:223,226 | Towns `TownsApi.Provider` (concrete `TownsModule`) | `VehicleWaypoint(pos,target,out gate,out hold)` per pilot decision (10-20 Hz/car) | gate routing through towns |
| Patrols Crews/CrewPoi.cs:50 | Towns | `ParkingOutside(world,out parking)` | POI parking outside towns |
| Patrols Traffic/TrafficLocations.cs:35,43,47,49 | Towns | `PatrolCount`, `PatrolBases(list)`, `PatrolBase(id)`, `ParkingOutside` | towns as traffic bases |
| Patrols Traffic/TrafficLocations.cs:258 | Towns `TownMarkers.Owns(t)` | static internal type | skip town objects in camp discovery |
| Patrols Traffic/RoadPlanner.cs:67 | Towns | `TravelBlocks(blocks)` | avoid town footprints |
| Patrols Traffic/WorldTraffic.cs:430-446 | Towns | `TravelRevision`, `ParkingOutside` | redirect journeys |
| Patrols Traffic/TownTraffic.cs:5 (`using ...Towns`), :43,:57,:130,:200 | Towns | `TravelStops(list)`, `TownGasStation.LaneSpread`, `MerchantSeeded(id)` | town stops/merchant buses |
| Towns TownsModule.cs:566,576 / 605,612 | Patrols `TownStop`, `TrafficBase` | builds Patrols DTOs | feeds TownTraffic / bases |
| Towns TownsModule.cs:681-682 | Patrols `TrafficSpace.World/Scene` | coordinate conversion (= Core.Origin) | |
| Towns TownsModule.cs:741 | Patrols `WorldTraffic.TownsStopped()` | on Towns Shutdown | release town traffic |
| Towns TownParking.cs:29 | Patrols `TownStop` (+`Approach`) | access road geometry | |
| Towns TownRoadBridge.cs:20-28 | Patrols `VehicleRoute.Build/Revalidate`, `TerrainWaypoints.Build`, `RoadPlanner.Build` (Harmony prefixes via Core.Patches) + reflection `PatrolsModule.TrafficRoadRouting` | injects town access roads as route hints; prefixes run on the RoadPlanner worker thread | **a module Harmony-patching another module's code** |
| Performance Content/PatrolsBridge.cs:31-38 | Patrols `WarRig.PoiEncounter.Protected/DetachTankers/CleanupChecks`, `PatrolsModule.Running` (reflection by name) | CT1 culler replaces DeleteFarAwayItems | asks for `Core.Services.Culling` |
| Performance World/CarLod.cs:194 | namespace string `WildWasteland.Modules.Patrols` | skip LOD on cars carrying Patrols components | breaks on namespace move |
| Quests Hire.cs:281 | type-name strings `WildWasteland.Modules.Patrols.PassengerGuard` / `.Pose` | destroy seat components | breaks on namespace move |
| Core/UI/CorePages.cs:7,106-114,243-244,290-300,337-338,350 | `PatrolsModule.ConvoyEnabled/FuryRoad/FootPatrols/MenuKey/TemplateDumpKey/TurretSpotEditor/BusPassengerPoseEditorEnabled/Running`, `WarRig.S.GroundRecoveryEnabled` | Game page (Enemy Vehicles), Debug page | Core referencing module types |
| Core/Plugin.cs:114,133 | `PatrolsModule.AudioVoices`, `new PatrolsModule()` | audio voices reset before modules start | setting should be Core's |

### 2.2 Core.Services consumed

| file:line | member | why |
|---|---|---|
| Crews/CrewReturn.cs:78,110,129,166,186,383,419,425 | `Services.Npc`: `CrewOrder`, `Remember`, `Travel`, `TravelState`, `IsIdle` | dismount awareness handover, walk back to car |
| Crews/CrewBusDefence.cs:38,46 | `Services.Npc.Remember` | bus defenders know attacker |
| Crews/CrewPoi.cs:22,115 | `Services.Npc.Structures` (0.5 s) | POI geometry for parking |
| Traffic/TrafficLocations.cs:128 | `Services.Npc.Structures` (5 s) | camp maps (same data, second consumer) |
| Crews/SimpleBanditBridge.cs:24-35 | `Services.Npc.SetSimpleConvoyBandit/SetSimpleConvoyBanditTarget` | dismounted crew AI mode |
| Traffic/TrafficFoot.cs:54,101,110,182,265,282,351,383,405,449 | `Services.Npc.Travel/TravelState/IsIdle` | walking foot patrols |
| Traffic/TownTraffic.cs:290,293 | `Services.Npc.IsIdle/Travel` | town guards |
| Crews/CrewAppearance.cs:37 | `Services.NpcAppearance.Dress` | female crew bodies |
| Crews/PlayerAnims.cs:22-69, Crews/Rider.cs:190 | `Services.Animation`: `Ready, ApiVersion, Find, Attach, GetClip, WeaponKey, HasWeaponPoses` (+ `IAnimatedCharacter`) | turret animation |
| Crews/MeleeWheels.cs:35 | `Services.Guns.HandlesFittedWheelMelee` | stand down |
| Spawning/Patrol.cs:456,1761,1768,1784 | `Services.VehicleIgnition.TryStart/TryRunning/TryStop` | Engine Sim coupling |
| Traffic/TrafficCompass.cs:34,36,77 | `Services.Compass` (`Remove`, set markers), `Services.Map.Observe` | journey markers |
| Traffic/TrafficMap.cs:39,75,94,272-276 | `Services.Map`: `GetIcon, Hidden, Markers, Open, Zoom, Routes, TrafficMarkers` | map ink |
| WarRig/WarRigFeature.cs:29-38 | `Services.Poi`, `Services.PoiChanged`, `Poi.Remove("WarRig")` | War Rig POI registration |
| PatrolsModule.cs:61-62 | events `Services.NpcHurt` (-> `Crew.Attacked`, CrewCombat.cs:20), `Services.VehiclePartHit` (-> `Crew.PartHit`, CrewBusDefence.cs:21) | crew reactions |
| Core (non-Services) | `GameRefs.*`, `FsmUtil`, `Origin`, `SaveBus` (`StateChanged`, `AfterSave`, `Busy`, `ReportFailure`, `CleanupSettings`, `SaveCancelled`), `SaveSystem.Busy`, `SaveRuntime.PlayerDistance/IsSaveFile`, `WorldWork`, `LoadingProgress`, `VistasWorldGen.RoadLines/RoadRevision/RoadIdentity/TerrainEdited/Present`, `DiskResources`, `Runner.Host`, `Scheduler.StartCoroutine/FrameMs/FrameBudgetMs`, `QuietFsm.On` | |

### 2.3 Core.Services PROVIDED

| member | signature | file:line | consumers |
|---|---|---|---|
| `Services.FollowerVehicles` | `IFollowerVehicles { bool IsSeated(GameObject); float ShootDistance; Component SeatPose(GameObject,Transform,string,string); Component SeatCombat(GameObject,GameObject,Transform); void ReleasePose(Component); bool CanFire(GameObject); Type ResolveType(string); bool IsCrew(GameObject); void Animated(GameObject,bool); void Combat(GameObject,GameObject,float) }` | PatrolsModule.cs:58, Crews/QuestFollowers.cs | Quests (Hire, QuestFollower, CrewHistory, NativeConversation, Plugin) |
| `Services.Camps` | `ICampStatus { CampState At(Vector3 world, float radius) }` | PatrolsModule.cs:59, Traffic/TrafficCamps.cs:22 | Quests Plugin.cs:701 |
| `Services.VehicleCombatActive` | `Func<bool>` | PatrolsModule.cs:60,75 | Core SaveCombat.cs:76 (autosave combat deferral) |
| `Services.RaiseCrewBailedOut(car, pax, npc)` | event raise | Spawning/Patrol.cs:1033,1053; Crews/Rider.cs:918 | NPC Senses |
| `Services.RaiseVehicleExploded(car)` | event raise | Vehicles/Explode.cs:304 | NPC Senses |
| `Services.RaiseVehiclePartHit(part, attacker)` | event raise | Crews/MeleeWheels.cs:124 | Patrols itself (CrewBusDefence) |
| `PoiDefinition{Id="WarRig",Radius=65,Enabled,Spawn,Mark}` | `IPoiApi.Register` | WarRig/PoiEncounter.cs:49 | POI module |
| `Hooks.GetPrefab` resolvers | `ES3Prefab Resolve(long id)` ids 735710202610050001 / 735710202610090001 | MotorcycleIntegration.cs:17, WarRigFeature.cs:22 | ES3 load |
| `LoadingProgress.Register(this, VehicleBuildCache.LoadWork)` | `Work? LoadWork()` | PatrolsModule.cs:65 | Core loading screen |
| `SaveBus.CleanupSettings += DrawSaveCleanupSettings` | `Action<ToolLayout>` | PatrolsModule.cs:63 | Saves page |

### 2.4 Direct NPC-behaviour use (brief focus)

| area | via Services? | what it does to NPCs directly |
|---|---|---|
| CrewCombat (Crew partial) | consumes `NpcHurt` event only | decides dismount/pursuit; reads attacker Health FSM via `CrewReturn.HealthVar` (CrewCombat.cs:29) |
| CrewReturn | yes: `Npc.Travel/CrewOrder/Remember/IsIdle/TravelState` | also destroys walkers and seats prefab copies (`Seat` l.334), reads/writes Health FSM, `Register.RemoveTree` |
| CrewPoi | `Npc.Structures` only | no NPC behaviour (vehicle parking) |
| CrewBusDefence | `Npc.Remember` | wheel repair (vehicle) |
| QuestFollowers | provider to Quests | attaches `Pose` and `PassengerGuard` to hired NPCs (seat pose + seated combat) |
| Crew.MuteAi (Crew.cs:344-364) | **no** | disables NPC AI/movement FSMs on seated crew every 0.5 s |
| PassengerGuard (Passenger.cs) | **no** | toggles vanilla `Attack/RangedAttackWait/Detection/Damage Ranged` FSMs, reads target vars by reflection (IndexTargetSources l.355), forces ranged-only |
| TrafficActor (TrafficFactions.cs:7-38) | **no** | sets SensorToolkit `AllowedTags` by reflection, retargets every 0.6 s through SimpleBanditBridge |
| RiderGuard (Rider.cs) | **no** | jump-off leap physics, mob spawn, detonation, scream audio |
| SeatedLosGuard | n/a (Hook) | skips vanilla LOS action for seated NPCs |
| Patrol.BailOut/SeatOccupant | **no** | instantiate NPC prefabs, set health, ignore collisions, `LateHealth` |
| TrafficAgent sensing (WorldTraffic.cs:36-60) | **no** | own sight/hearing of the player for patrols (dup of NPC Senses) |

### 2.5 What Towns calls in Patrols (summary)
`TownStop` (construct + `Approach`), `TrafficBase` (construct), `TrafficSpace.World/Scene`, `WorldTraffic.TownsStopped()`, and Harmony prefixes on `VehicleRoute.Build/Revalidate`, `TerrainWaypoints.Build`, `RoadPlanner.Build` + reflected `PatrolsModule.TrafficRoadRouting`. Patrols in return calls `TownsApi.Provider` (`TravelStops, TravelRevision, ParkingOutside, VehicleWaypoint, PatrolCount, PatrolBases, PatrolBase, TravelBlocks, MerchantSeeded`) and `TownMarkers.Owns`, `TownGasStation.LaneSpread`. Proposed `ITownTravel` ModAPI (Towns provides): `Revision; Stops(List<TownStopDto>); bool ParkingOutside(Vector3 world,out Vector3); bool VehicleWaypoint(...); void TravelBlocks(List<Bounds>); int PatrolCount(Vector3,float); void Bases(List<BaseDto>); bool IsBase(string); bool Owns(Transform); void MerchantSeeded(string); Vector3[][] AccessRoadHints` and Patrols provides `IPatrolTraffic.TownsStopped()`; DTOs (`TownStop`, `TrafficBase`) move to Core.

## 3. Harmony patches & scheduler jobs

### 3.1 Patches / hooks

| file:line | target | kind | via | cost / filter / shared |
|---|---|---|---|---|
| PatrolsModule.cs:113 | `SetFsmFloat.OnEnter` | postfix `Ram.AfterSetFsmFloat` | Hooks.SetFsmFloatEnter | every SetFsmFloat; `!Running`, `Fsm.Name.Length==9 && =="CarAttack"`. Shared: NPC (VanillaEvents, DogBite), Guns (Tracers) |
| PatrolsModule.cs:114 | `SetFsmFloat.DoSetFsmFloat` | prefix (skip) `Ram.BeforeSetFsmFloat` | Hooks.SetFsmFloatDo | same filter; shared with NativeStormProtection.DamageValue |
| PatrolsModule.cs:115 | `SendEvent.OnEnter` | prefix (skip+Finish) `Ram.BeforeSendEvent` | Hooks.SendEventEnter | every SendEvent in game; shared NPC Brain/Aim, storm |
| Crews/SeatedLosGuard.cs:11-12 | `SensorGetLineOfSightResult.OnEnter3D/OnUpdate3D` | prefix (skip) | Hooks.LosEnter3D/LosUpdate3D | `targetObject.Value != null` first; shared NPC `Senses.BeforeLosResult` |
| Vehicles/Suspension.cs:119 | `SetPosition.DoSetPosition` | postfix | Hooks.SetPositionDo | `SuspensionTune.Live<=0`, then `Fsm.Name != "Suspension"` string compare per call while any tuned car exists |
| Vehicles/MotorcycleIntegration.cs:17 | `ES3ReferenceMgrBase.GetPrefab(long,bool)` | resolver | Hooks.GetPrefab | id compare; shared WarRig, Survival |
| Vehicles/MotorcycleIntegration.cs:22 | `MotorcycleMod.Factory.Template()` (3rd party) | postfix | Patches | rare |
| WarRig/WarRigFeature.cs:22 | `GetPrefab` | resolver | Hooks.GetPrefab | id compare |
| WarRig/WarRigFeature.cs:103 | `Apocaspawner.Catalog.Entries` | postfix | Hooks.SpawnerCatalogEntries | reflection `entry.GetField("Key").GetValue` per entry per call (l.112); shared Survival |
| WarRig/WarRigFeature.cs:104 | `Apocaspawner.Spawner.Spawn` | prefix (skip) | Patches | reflection per call |
| WarRig/DustStormBridge.cs:44-45 | `ApocaDustStorm.StormShelter.ContainsActor`, `PlayerStormHazards.Tick` | prefix | Patches | `broken` flag |
| WarRig/NativeStormProtection.cs:41 | `PlayMakerFSM.OnEnable` | postfix | Hooks.FsmEnable | `FsmName != "SandPlayer"`; shared Performance, Weather, Towns |
| WarRig/NativeStormProtection.cs:42 | `Explosion.ShouldApplyForce` | postfix | Patches | `__result && rigs`; Weather calls the same method by delegate (WeatherNative.cs:61) |
| WarRig/NativeStormProtection.cs:43 | `Tornado.OnTriggerStay` | prefix (skip) | Patches | per tornado contact per physics step |
| WarRig/NativeStormProtection.cs:44-46 | `SetFsmFloat.DoSetFsmFloat`, `SendEvent.OnEnter`, `SendEvent.OnUpdate` | prefix (skip) | Hooks | `WarRigUnit.All.Count == 0` first |
| WarRig/PoiEncounter.cs:16 | `GetDistance.OnEnter` | prefix (skip+FINISHED) | Patches | `HashSet<GetDistance>` lookup of DeleteFarAwayItems actions; Performance CT1 replaces this FSM and calls Protected/DetachTankers by reflection; Towns relies on `WorldTraffic.RetainsTown` inside it |
| Traffic/TrafficCamps.cs:51 | `CreateObject.OnEnter` | postfix | Hooks.CreateObjectEnter | `storeObject==null`, owner `EnemySpawn_`; shared PlayerBody/NPC/Guns/Weather/Towns |
| Persistence/PatrolLoadPreparation.cs:14 | `Wait.OnUpdate` | prefix (hold) | Hooks.WaitUpdate | exact SaveLoadGame FSM + state; shared Core VehicleLoadPlacement, Towns |
| Spawning/Patrol.cs:1203 | `ES3ReferenceMgrBase.AddPrefab(ES3Prefab)` | postfix | Patches | invalidate only |
| Spawning/Patrol.cs:1205 | `ES3ReferenceMgrBase.Awake` | postfix | Patches | invalidate only |
| (Towns) TownRoadBridge.cs:24-27 | Patrols `VehicleRoute.Build/Revalidate`, `TerrainWaypoints.Build`, `RoadPlanner.Build` | prefix | Patches (owner Towns) | worker-thread; cross-module self-patch |
| non-Harmony | `TerrainTile.OnBeforeTilePrepare/OnTileFinalized/OnTileApplied`, `VistasWorldGen.TerrainEdited` (TrafficRoads.cs:97-100); `Application.quitting` (Persistence.cs:231); `SaveBus.StateChanged/CleanupSettings/SaveCancelled`; `Services.NpcHurt/VehiclePartHit/PoiChanged`; `SettingChanged` lambdas (PatrolsSettings.cs:118-121) | events | - | TerrainTile events also subscribed by POI and Towns -> Core terrain-event dispatcher |

### 3.2 Scheduler jobs (all registered in PatrolsModule.cs)

| name | kind | interval/budget | line | notes |
|---|---|---|---|---|
| `Traffic final terrain` | Sliced | 500 us | 107 | `TrafficRoads.GroundStep`; name lacks `Patrols.` prefix, registered in InitInner not RegisterJobs |
| `Patrols.Prefab cache` | Interval | 0.5 s (+RunNow) | 209 | `Prefabs.Refresh` |
| `Patrols.Build` | Sliced | 1000 us + WorldWork | 210 | `BuildQueue.Step` |
| `Patrols.Vehicle build cache` | Sliced | 500 us + WorldWork | 211 | `VehicleBuildCache.Step` |
| `Patrols.Vehicle load preparation` | EveryFrame | own 12 ms loop while loading | 212 | `VehicleBuildCache.LoadBurst` (third budget mechanism) |
| `Compass markers` | Interval | 0.5 s | 214 | `TrafficCompass.Tick`; no prefix |
| `Patrol.Tick` | EveryFrame (lambda) | - | 215 | hides `PatrolPersistence.Tick`, `Ram.Tick`, `ExitSpeed.Tick`(5 s), `MeleeWheels.Tick`, lights 5 s, prune 30 s |
| `TemplateMenu.Tick`, `TemplateExporter.Tick`, `TurretSpotter.Tick`, `BusPassengerPoseEditor.Tick`, `MerchantEditor.Tick` | EveryFrame (lambda) | - | 216-219, 223 | editors tick every frame even when closed |
| `Convoy.Tick` | EveryFrame (lambda) | - | 220 | heat scan 5 s |
| `WorldTraffic.Tick` | EveryFrame (lambda) | inner 0.25 s | 221 | hides per-frame `TrafficLocations.Tick` (0.3 ms Stopwatch scene walk), `TrafficCamps.Tick`, `TrafficRoads.Tick`, `MerchantSignals` - none visible on F7 Performance |
| `Cleanup.Tick` | EveryFrame (lambda) | inner 1 s / 5 s | 222 | |
| `TemplateMenu.LateTick`, `TemplateExporter.LateTick` | EveryFrame LateUpdate | - | 225-226 | |
| `Patrol.Gui`, `TemplateMenu.Gui`, `TemplateExporter.Gui`, `TurretSpotter.Gui`, `BusPassengerPoseEditor.Gui`, `WorldTraffic.Gui`, `MerchantEditor.Gui` | Gui (with predicates) | - | 229-235 | OK (predicates are field reads) |

Rule violations: every tick is a lambda, not a named method (AGENTS 5.4); 3 names without `Patrols.` prefix. Coroutines outside the scheduler on the module's `Patrol` MonoBehaviour: `PatrolPersistence.RestoreTracked` (Persistence.cs:253), `Convoy.BuildAll` (Convoy.cs:192), `WorldTraffic.Materialize` (WorldTraffic.cs:318, TownTraffic.cs:229), `MaterializeBattle` (TrafficBattles.cs:113); per-car `Crew` coroutines (Crew.cs:176,214,604), `Explode.Routine` on crew (Explode.cs:34). Worker threads: `RoadPlanner.Run` (Task.Run, RoadPlanner.cs:80), `TrafficRoads.RunIO` (TrafficRoads.cs:350), `ShellRecycle` STA thread.

Per-object Unity loops (allowed exception, gate on `Running`): Crew.Update/FixedUpdate(->Pilot.Step), PassengerGuard.Update/LateUpdate, Pose.LateUpdate, RiderGuard.Update, RiderAnchorGuard.LateUpdate, JumpClip.FixedUpdate/Update, Detonator.Update, LanceFuse.Update, Patrol+CorpseEject.FixedUpdate, Patrol+LateHealth.Update(3 frames), SuspensionTune.FixedUpdate(1 Hz), MotorcycleBalance.FixedUpdate, WarRigUnit.Update/LateUpdate/FixedUpdate, TrafficActor.Update(0.6 s), MerchantHost.Update, Ram.Sensor/Pilot OnCollision*.

## 4. Logging

No per-feature log switches and no own `ManualLogSource` in Patrols (TemplateExporter.cs:9 has an unused `using BepInEx.Logging`). Helpers and counts:

| helper | definition | sites | behaviour |
|---|---|---|---|
| `PatrolsModule.Verbose(string)` | PatrolsModule.cs:269 | 224 in 31 files (top: Patrol.cs 44, Crew.cs 22, Rider.cs 21, TrafficFoot.cs 18, Pilot/MeleeWheels/CrewReturn 11) | `Log.Info` when `[Debug] VerboseLog`; **156 sites build the string before the check** (only 35 are wrapped in `if (Core.Log.Verbose)`) |
| `Core.Log.Info` direct | - | 25 | verbose-gated in Core |
| `Core.Log.Always` | - | 14 (e.g. PoiEncounter.cs:129 "WAR RIG SPAWNED", VehicleBuildCache.cs:121/128, Cleanup.cs:34, PatrolsModule.cs:134) | always |
| `Core.Log.Warn` / `Error` | - | 123 / 11 | always, rate-limited |
| `WarRigFeature.Say` | WarRigFeature.cs:47 | 2 (+7 `Say` in editors) | Log.Always |
| `TurretSpotter.Say` | TurretSpotter.cs:120 | status line + Log.Info | |
| `CarTemplates.Warn` | CarTemplates.cs:107 | Log.Warn wrapper | |
| `WorldSpawning.Reject` | WorldSpawning.cs:64 | verbose-gated | |
| `MeleeWheels.V` | MeleeWheels.cs:52 | alias of Log.Verbose; drives per-frame diagnostics | |

Debug overlays (not logs, go to Debug module): `[Patrols / Debug] AiOverlay` (Patrol.Gui, Pilot.OverlayLine, Convoy.OverlayLine), `[Patrols / Traffic tuning] DebugMap`, `Real patrol names`, `hide route type` (TrafficMap), War Rig gizmos (WarRigUnit).

Proposed mapping:
- **Level 1 (main actions):** module ready + template summary (PatrolsModule.cs:134); config/data migrations (PatrolsSettings.cs:460, PatrolsData.cs); spawn/build finished/failed per car (Patrol.Build); convoy/patrol group rolled + launched (Convoy); journey created/materialised/unloaded/removed, battles started/ended, reinforcement calls (WorldTraffic*, TrafficBattles, TrafficReinforcements); merchant bus setup/visit; foot patrol created/settled; crew bailed out/dismounted/reboarded/stranded (Crew, CrewReturn); car exploded/wrecked; cleanup removal (Cleanup); persistence saved N cars / restored N cars (Persistence); War Rig spawned/POI, vehicle-cache load start/finish; Warnings/errors (unchanged).
- **Level 2 (deep traces):** Pilot state transitions and 5 s state lines (Pilot.cs:37,113,196,203,267,343,512,556,790,796), feelers/Nwh `Diag`; Pose/Passenger setup details (Pose.cs:73-337, Passenger.cs:136-405); Ram per hit (Ram.cs:92-240); MeleeWheels diagnostics; prefab resolution (Patrol.cs:1246,1284,1307,1342); Suspension geometry; route search/road cache; TrafficLocations discovery progress; foot walker steps; CrewReturn member steps; VehicleBuildCache per-variant; lights switch (Patrol.cs:52); `SaveTypes`/`DayLight` changes.

## 5. Redundancy & dead code

### 5.1 Dead code (evidence: single reference = the declaration, repo-wide)
| item | file:line |
|---|---|
| `Convoy.DebugSpawn`, `Convoy.DebugRoll` | Spawning/Convoy.cs:153,159 |
| `TrafficLocations.RoadRoute`, `LegSafe` | Traffic/TrafficLocations.cs:311,317 |
| `WorldTraffic.FootAlive` | Traffic/TrafficFoot.cs:73 |
| `Explode.Preserved` | Vehicles/Explode.cs:116 |
| `CarTemplates.IsItemSpec` | Vehicles/CarTemplates.cs:175 |
| `VehicleRules.CanTemplate` (body computed, returns true) | Vehicles/VehicleRules.cs:13 |
| `TemplateExporter.BedItems` | Editor/TemplateExporter.cs:324 |
| `TemplateMenu.MoveTemplate` | Editor/TemplateMenu.Ledger.cs:43 |
| `RiderGuard.StopGunner` | Crews/Rider.cs:926 |
| `RiderApi.MoveAnchor`, `SaveLance`, `LanceOffset/LanceEuler` setters (comment: "kept so the retired Apocaspotter plugin keeps working" - impossible, type moved namespace) | Crews/Rider.cs:1136-1169 |
| `PatrolsModule.Exporter` property | PatrolsModule.cs:46 |
| `IFollowerVehicles.ResolveType` impl (no caller in Quests; QuestsCompat retired) | Crews/QuestFollowers.cs:29 |
| unused local `var preset=` in `Pose.ConfigureBusPassenger` | Crews/Pose.cs:15 |
| `TrafficSpace.Reset()` empty | Traffic/TrafficSpace.cs:13 |
| `PatrolSaveData.convoyCooldown` always -1 (format-only) | Persistence/Persistence.cs:20 - keep (binary format) |
| unused `using System.Reflection/Collections` in WarRig/Settings.cs, `BepInEx.Logging` in TemplateExporter.cs | |

### 5.2 Duplicated utilities
| duplicate | here | elsewhere | -> |
|---|---|---|---|
| JSON | Vehicles/CarTemplateFile.cs:201 `Json`, WarRig/Json.cs, Editor/EditorData.cs:166 `StoreJson`, Traffic/TrafficCodec.cs (reflection DTO), BusPassengerPose.cs uses `JsonUtility` | NPC/Women/Json.cs, CleanHUD/Json.cs, PlayerBody/Body/Json.cs, Survival/StewPot/Json.cs, Towns/GasStation/Json.cs | Core.Json |
| GLB reader | WarRig/Glb.cs | Towns/GasStation/Glb.cs, Survival/StewPot/Glb.cs | Core.Glb |
| WAV decoder | Crews/Rider.cs:1086 `Wav` | Guns/Tracers/Wav.cs, NPC/Women/Wav.cs | Core.Wav |
| Atomic write | EditorStore.cs:48, Persistence.cs:549, TrafficRoads.cs:378, BusPassengerPose.cs:45 (inline) | Core/AtomicFile.cs | Core.AtomicFile |
| Key polling | `PatrolsModule.Pressed` (PatrolsModule.cs:282) | `Core.Hotkeys.Pressed` | Core |
| Origin wrapper | `TrafficSpace.World/Scene` | Core.Origin | Core.Origin |
| Prefab-name cleanup | `TemplateExporter.PrefabName` (regex, used by 15 runtime files) | NPC/Towns have own name stripping | Core |
| FSM lookup | `Patrol.FindFsm`, `WarRigFeature.FindFsm`, `MotorcycleIntegration.FindFsm` (LINQ) | Core.FsmUtil.Find | Core.FsmUtil |
| Vector parse | `Rider.ParseV3/FormatV3`, `WarRig.S.V3/Floats` | PlayerBody/NPC parsers | Core |
| Player / player car | `PlayerRef` (Pilot.cs:835) | NPC Senses.cs:1850 `PlayerCar()`, Core.GameRefs.Player | Core.GameRefs |
| Coyote relation | `TrafficFactions.PlayerHostile` (FactionRelation_Coyotes) | NPC Senses.cs, Performance Content/Coyotes.cs | Core faction query |
| Female alias table | CrewAppearance.BasePrefab | NPC/Women | Pres |
| Save-busy check | `Patrol.SaveBusy/WaitForSave` (Patrol.cs:57-78) | `SaveBus.Busy`, `SaveSystem.Busy` | Core |

### 5.3 Duplicated scans / lookups
- Asset prefab index: `Prefabs.ScanExact/Scan` (Patrol.cs:1227,1319, Resources.FindObjectsOfTypeAll GameObject+PlayMakerFSM+ES3ReferenceMgrBase) vs `PickupCatalog.All` (PickupCatalog.cs:34, same FSM scan) inside Patrols, and outside: Towns/PoiSpawnCatalog.cs:29, Performance/Content/FsmEdits.cs:72, Guns Tracers.cs:1485 + AmmoMechanics.cs:398, NPC Senses.cs:1318 + Burrow.cs:393 + Gungirl.cs:589, CleanHUD/Assets.cs:260, PlayerBody Props.cs:113,129 + Body.cs:328, Core/SaveCleanup.cs:129, plus `Motorcycles.Factory.Source` and `WarRig.Factory.Source` (FindObjectsOfTypeAll<ES3Prefab>). -> one Core prefab catalog (Patrols' `Prefabs` is the most complete: generation counter + ES3 manager hooks).
- Scene walks: `TrafficLocations.Tick` deep scene walk every 60 s (+roots 15 s) vs NPC Nav discovery sweep and Women discovery (perf-audit D8/D9) and Towns InteriorPopulation. -> Core world-object registry fed by CreateObject/FsmEnable hooks.
- `Services.Npc.Structures` copied twice inside Patrols (CrewPoi.cs:22 every 0.5 s, TrafficLocations.cs:128 every 5 s).
- Vehicle lists: `WarRigUnit.GateAttacher` FindObjectsOfType<VehicleController> (WarRigUnit.cs:94), `TrailerSupport.Attached` FindObjectsOfType<ConfigurableJoint> (TrailerSupport.cs:46), `GroundRecovery` FindObjectsOfType<Joint> (GroundRecovery.cs:175,221), Performance CarLod -> Core vehicle/joint registry.
- Game save registry (`ArrayList_Cars`, items, `itemNameID`): `Register` (Patrol.cs:1555), WarRigFeature.SpawnNearPlayer:56-61, VehicleBuildCache, Persistence, and in Core/NPC/Guns/Quests/Performance. -> Core `WorldRegistry`.
- MapMagic `TerrainTile` events subscribed by TrafficRoads, POI and Towns separately.
- Assembly probes: `AppDomain.GetAssemblies().FirstOrDefault(...)` in MotorcycleIntegration (each Template() call), Suspension.cs:29, MeleeWheels.cs:43, Convoy.cs:127, ModVehicles.Scan (whenever assembly count changes) -> Compat registry resolved once.

### 5.4 Two systems doing the same job
| job | systems | recommendation |
|---|---|---|
| Route planning | `RoadPlanner` (worker queue + coarse fallback chain `Build`), `VehicleRoute` (vehicle A* over road lines), `TerrainWaypoints` (foot DP over `ElevationGrid`), `TrafficLocations.RoadRoute` (dead, main-thread), synchronous `RoadPlanner.Build` on main thread in TrafficFoot.cs:319/401; `RoadPath` is a follower, not a planner; `TrafficRoads` is data capture. Outside: NPC Nav (foot), Towns TownWalkGraph/TownNavigation, Core.VistasWorldGen road lines | Keep one worker `RoadPlanner` with two solvers (vehicle = `VehicleRoute`, foot = `TerrainWaypoints` or NPC long-range service); move fallback chain into it; delete RoadRoute/LegSafe; hints from Towns/Vistas via ModAPI providers instead of Harmony |
| Frame budgeting | `Scheduler.Sliced` budgets, `Core.WorldWork.Try` arbitration (BuildQueue.cs:45, VehicleBuildCache.cs:135), `BuildQueue` (coroutine stepping on a slice), `VehicleBuildCache.LoadBurst` (own 12 ms Stopwatch loop), `TrafficLocations` own 0.3 ms Stopwatch (TrafficLocations.cs:70-72), coroutines on module MonoBehaviour (Convoy.BuildAll stagger, Materialize, RestoreTracked) | Core WorldWork becomes a queue with owners + budgets (absorb BuildQueue and LoadBurst), all slices as named Sliced jobs. `EventBudget` is NOT a frame budget (encounter car cap) - keep in Patrols, rename |
| Persistence | Own: ES3 key `Apocapatrol.Patrol` (cache+file), binary sidecar + .bak + legacy dir, `_flushAt` timer **and** `SaveBus.AfterSave(FlushPending)` (both scheduled, Persistence.cs:248), `Application.quitting` flush, `SaveBus.StateChanged` string states instead of `OnSaveBegin/OnLoadBegin` hooks, own restore coroutine; traffic JSON nested inside; road cache; editor JSON stores | Use Core sidecar API (slot path, atomic write, flush, quit, cancel) and module lifecycle hooks; keep formats |
| Far-object cleanup | `Patrols.Cleanup` (5 s), `PoiEncounter.BeforeCleanupDistance` (native DeleteFarAwayItems retention), Core `SaveCleanup`, Performance CT1 culler (replaces DeleteFarAwayItems, reflects into Patrols) | One Core cleanup/retention service with participants (`Protected(go)`, `BeforeFarDelete(go)`, classifier for abandoned patrol cars) |
| Wheel melee | `MeleeWheels` vs Guns Tracers `MeleeOnWheels` | delete Patrols copy |
| Player perception by patrols | `TrafficAgent` sight/hearing + `PatrolVisibility` vs NPC Senses | NPC senses service query |
| Seated-NPC AI muting | `Crew.MuteAi`, `PassengerGuard.MuteNonCombatAi` vs NPC brain ownership | NPC `SetSeated` service |
| Autosave timing | `Convoy.AutosaveSoon` reflects retired Apocasaver vs Core SaveSystem autosave | Core query `SaveBus.AutosaveDueIn` |

### 5.5 Legacy migrations / shims
| item | file:line | keep? |
|---|---|---|
| First-run data copy from `plugins\Apocapatrol`, `plugins\WarRig`, theme | PatrolsData.cs | keep >=1 release, then retire |
| Legacy cfg migration (Apocapatrol / WarRig / MerchantSpotter cfgs, moved keys, `MigrateChangedKey`) | PatrolsSettings.cs:155-470 | must stay (first-run only) |
| `CarTemplates` folder import (`legacy-templates-imported.txt`) | CarTemplates.cs:31-45 | can retire later (marker-gated) |
| Editor upgrade markers `.coyote-traffic-v1`, `.merchant-traffic-v1`, Scrapyard preset upgrade | EditorStore.cs:28-42 | keep (cheap, one-time) |
| Sidecar read fallback `config\Apocapatrol\Saves` | PatrolsModule.cs:101, Persistence.cs:522 | keep >=1 release (AGENTS 5.10) |
| Road cache fallback `config\Apocapatrol.roads` | PatrolsModule.cs:103, TrafficRoads.cs:325 | regenerative cache: can retire |
| Save schemas 1-14, WDR1/WDR2 extensions, TrafficSave v1 (scene coords) | Persistence.cs:194,586-660, PatrolCrewDelta.cs:19, TrafficModel.cs:129 | must stay (save compat) |
| MerchantLayouts v1->v2 upgrade; 3-shop legacy buses | MerchantModel.cs:66, MerchantRuntime.cs:69 | keep |
| Empty `PatrolVisibility` component for old saves | PatrolVisibility.cs:12 | keep (save type) |
| `RiderApi` public for Apocaspotter, `QuestFollowers.ResolveType("Apocapatrol.*")`, `MerchantApi` public | Rider.cs:1115, QuestFollowers.cs:29, MerchantModel.cs:45 | delete / internalize |
| Apocaraider wheel detection | MeleeWheels.cs:43 | goes with MeleeWheels deletion (or Compat) |
| Apocasaver autosave reflection | Convoy.cs:120-151 | replace (Apocasaver now stood down by WW saves) |

## 6. Performance hot spots

| file:line | issue | fix |
|---|---|---|
| PatrolsModule.cs:215-226 | 12 EveryFrame lambda jobs; 5 editor ticks run every frame while closed; WorldTraffic.Tick hides 4 sub-systems | named tick methods; editor ticks registered on open (or predicate gate); split `TrafficLocations` (Sliced), `TrafficCamps`/`TrafficRoads`/traffic 0.25 s (Interval) into own named jobs |
| Traffic/WorldTraffic.cs:21-24 `TrafficAgent.Goal`, :171 `Find` | per Pilot decision (10-20 Hz per car): `GetComponent<BattleCar>()`, linear `WorldTraffic.Find(id)` over up to 120 journeys, `PlayerHostile` | cache journey ref + BattleCar on the agent; `Dictionary<string,TrafficJourney>` |
| Crews/Pilot.cs:223-226 | `TownsApi.Provider.VehicleWaypoint` per decision (cross-module, cost unknown) | cache gate per 0.5 s / only near towns (Towns provides bounds) |
| Crews/Pilot.cs:134, Patrol.cs:1815,1857 | `Nwh.GearIndex/MaxSteerAngle` reflection `FieldInfo.GetValue` + boxing (5 Hz/car, 0.1 Hz) | typed `TransmissionComponent.Gear`, `Steering.maximumSteerAngle` like the typed Input/Engine path |
| Traffic/TrafficFactions.cs:32-38 | `TrafficActor.Update` per NPC 0.6 s: O(N) `NearestOpposed` -> O(N^2); `Relations` allocates List+array and reflection `SetValue` on sensors | one Interval job over `Actors` with spatial bucketing (perf-audit D14); cache tag arrays per faction/hostility |
| Traffic/TrafficLocations.cs:84-118 | per-frame 0.3 ms scene walk restarted every 60 s; `t.name` alloc per node | Core world registry; Sliced job; skip rigidbody/NPC subtrees |
| Vehicles/TrailerSupport.cs:46 | `FindObjectsOfType<ConfigurableJoint>` once per frame when called (cleanup, camp clearance, PoiEncounter, Explode) | joint registry (CreateObject/attach events) |
| WarRig/WarRigUnit.cs:94-95 | `FindObjectsOfType<VehicleController>` 1 Hz per rig + `ToArray`/new arrays; `Allowed` `Split` per scan (l.112) | Core vehicle registry; parse `AllowedTrucks` once |
| Vehicles/MotorcycleIntegration.cs:24-30 | `AppDomain.GetAssemblies()` + LINQ + `GetMethod` + `Invoke` on every `Template()` (every `Prefabs.Exact/Find/FindAny` hit for "Motorcycle", every ES3 resolve) | resolve delegate once at Install |
| Merchants/MerchantRuntime.cs:115 | `Resources.FindObjectsOfTypeAll<GameObject>()` per merchant bus setup (when stock wanted/discarded) - whole asset+scene scan on spawn | Register/item list or Core catalog |
| Merchants/MerchantModel.cs:22, MerchantRuntime.cs:82,101,111,162 | `GameObject.Find("ItemBuySellList")`, `GetComponentsInChildren` + LINQ per setup | cache per scene |
| Traffic/TrafficMap.cs:25,102 | `Resources.FindObjectsOfTypeAll<Camera>`, `GameObject.Find("MapCheat_Camera")` from Gui while null | get camera via `Services.Map` (CleanHUD owns it) |
| Spawning/Convoy.cs:80 | `GameObject.Find("Starter_Area")` every 5 s when distance FSM missing | cache; or TrafficLocations Points |
| Spawning/Convoy.cs:120-151 (called WorldTraffic.cs:314, TownTraffic.cs:228, TrafficBattles.cs:112, TrafficFoot.cs:101) | reflection `GetValue` + boxing per journey per 0.25 s, against retired Apocasaver | Core SaveSystem query |
| Crews/MeleeWheels.cs:61-100 | with VerboseLog on, runs per frame even when Guns handles it, `states +=` concat per frame | delete file (dup) |
| 156 `PatrolsModule.Verbose("..."+x)` sites (e.g. Crew.cs:424,431,441; Ram.cs:126-181; Passenger.cs:325,332) | string built before verbose check (AGENTS 5.5 Log) | `if (Log.Verbose)` guard or Debug level API with deferred formatting |
| Spawning/Patrol.cs:290,356,379,396,437,723,733,833,859-940,1079,1177 | many `GetComponentsInChildren` per spawn/bail (per-spawn ban) | one snapshot per car passed through (pattern already exists: `snapshot` param) |
| Spawning/VehicleBuildCache.cs:73-89 | `Key()` MemoryStream+Base64 per `Require` (per Warm/spawn) | cache key on CarTemplate |
| Spawning/Patrol.cs:1272-1281 | LINQ `FirstOrDefault` chains + `Where/Select/Take` warning string per `Prefabs.Find` | dictionaries by (kind,name),(name),(display) |
| Traffic/RoadPlanner.cs:54-71 | main-thread LINQ/ToArray over all road tiles + `TrafficLocations.Blocks()` LINQ per route request | maintain arrays on revision change only |
| Traffic/TrafficFoot.cs:319,401 | synchronous `RoadPlanner.Build` on main thread | enqueue as job |
| Vehicles/Cargo.cs:21,165; Patrol.cs:1020,1132 | `Physics.RaycastAll` (per spawn/bail, CorpseEject 10 Hz for <=3 s) | `RaycastNonAlloc` with shared buffer |
| Spawning/PatrolVisibility.cs:31-33 | every 5 s: `GetComponentsInChildren<Light>` + LINQ + `GetComponentsInParent` per light | refresh on car change only |
| Crews/Crew.cs:395 | SetupOnly cars: `MuteAi(Marker.Passenger)` -> `GetComponents<PlayMakerFSM>` array 2 Hz | cache like driver |
| Traffic/TrafficRoads.cs:293,300 | `FindObjectOfType<MapMagicObject>` every 5 s while null; `tile.coord.x+"_"+z` string per tile every 5 s | cache map from TerrainTile events; int2 key |
| Vehicles/Suspension.cs:126,317 | shared SetPosition postfix: `Fsm.Name != "Suspension"` per call; `_mounts.FirstOrDefault` lambda | length gate/reference cache; loop |
| Crews/Rider.cs:150,1072; Rider.cs:415; Suspension.cs:166-213; VehicleAttachments.cs:10 | LINQ in per-spawn/per-attach paths | plain loops |
| Persistence/Persistence.cs:296-307 | sidecar serialize + atomic write synchronously on SaveGame state | acceptable (save frame); move IO to Core sidecar worker |
| PatrolsModule.cs:250-258 | `FindObjectsOfType` x8 types on Shutdown | acceptable (shutdown); use registries |

AGENTS 5.10 note (pre-existing design, keep in mind): Patrols adds components to natively saved objects (cars: PatrolMarker, Crew, Pilot, Ram+Sensor, TrafficAgent, TrafficPlatform, EncounterCar, BattleCar, MerchantHost, SuspensionTune; NPCs: TrafficActor, SimpleBanditTag, CampResident, Pose, PassengerGuard, RiderGuard, RiderAnchorGuard, MerchantGuardHealth; MotorcycleMod prefab: MotorcycleRearAccess).

## 7. ModAPI / multiplayer events

Needs a Core stable id (`NetId`) for vehicles/NPCs; existing stable ids: journey `id`, camp `TrafficBase.id`, battle `id`, vehicle slot index in journey, native unique save names from `Register.Name`. Positions in MapMagic-local world space (`Core.Origin.World`).

| event / query | data |
|---|---|
| `PatrolGroupRolled` | eventId, groupId, faction, kind (patrol/convoy/merchant/battle/foot/poi/test), templateNames[], km, bossKills |
| `JourneyCreated` / `PatrolDeparted` | journeyId, sourceCampId, destCampId/townId, faction, flags (convoy, merchant, foot, roaming, townBound), routeHash + route points (or seed), speed |
| `JourneyMaterialized` / `JourneyVirtualized` / `JourneyEnded` | journeyId, vehicleNetIds[], reason (arrived, expired, captured, destroyed, unloaded) |
| `VehicleSpawned` | vehicleNetId, body/prefab name, templateName, worldPos, rotation, trailerNetId, cargoKey, paint key, journeyId/eventId |
| `VehicleDespawned` | vehicleNetId, reason (cleanup, traffic unload, scene) |
| `VehicleExploded` (exists, GameObject-typed) | vehicleNetId, worldPos, wreck (truck) vs chassis |
| `VehicleDrivingState` (periodic, 5-10 Hz) | vehicleNetId, throttle, steer, brake, gear, handbrake, engineRunning (from Pilot/Nwh) |
| `CrewSeated` | vehicleNetId, seat (driver/passenger/turret 0-6), npcNetId, prefab/identity, health |
| `CrewBailedOut` (exists) / `CrewDismounted` | vehicleNetId, npcNetId, seat, reason (combat, stuck, vacated, death, jump), worldPos, velocity |
| `CrewReboarded` / `DriverPromoted` | vehicleNetId, npcNetId, seat |
| `RamHit` | vehicleNetId, target (player/npcNetId/vehicleNetId), damage, relSpeed, push vector |
| `LanceThrown` / `RiderLeap` / `RiderDetonated` | riderNetId, vehicleNetId, origin, velocity / launch / pos |
| `MerchantVisit` / `MerchantStockChanged` | journeyId, vehicleNetId, stopId, merchantTypes, slot, purchased |
| `FactionBattleStarted/Ended` | battleId, journeyA, journeyB, worldPos, winnerSide |
| `CampStateChanged` / `CampCleared` | campId, CampState, worldPos |
| `ReinforcementsCalled` | campId, journeyId |
| `TownVisit` | journeyId, townKey, parked/resident |
| `TrailerAttached/Detached` | truckNetId, trailerNetId |
| `WarRigEncounterSpawned` (POI) | siteId, truckNetId, trailerNetId, worldPos |
| Queries | `IsCrew(npc)`, `IsSeated(npc)`, `VehicleCombatActive()`, `CampState At(pos,r)`, `JourneyOf(vehicle)`, `IsProtectedFromCleanup(go)`, `PatrolCount(pos,r)`, `AutosaveDueIn` (consumed), `PlayerCar`, `IsPlayerHostile(faction)` |
| Consumed | `NpcHurt`, `VehiclePartHit`, `PoiChanged`, Town travel provider, terrain tile events, road hint providers (Towns, Vistas) |

## 8. Unmapped or ambiguous code

| code | candidates | recommendation |
|---|---|---|
| Template-driven vehicle assembly (Patrol.Build pipeline 1a, `VehicleBuildCache`, `BuildQueue`, `TemplateFile/CarTemplate` structure, `InputControl`, `Nwh`) | Patrols vs Vehicles | Vehicles owns "assemble vehicle from TemplateFile" + cache + input/NWH access (used by Patrols, POI War Rig, Tools recorder, future Towns parked cars); Patrols keeps crew/cargo/loot rolls, groups, template park |
| `Rider` human turrets | Pat / NPC / Pres / Gun | split as in file map; seat slots and spawn stay Pat, behaviour NPC, anims Pres, lance projectile Gun |
| `PassengerGuard` (seated shooter) | NPC / Pat / Quests | NPC "seated combat" service used by Patrols and Quests |
| `Explode` | Pat / Veh | trigger + rules Pat, wreck/darken/deaden Veh |
| `Ram` | Pat / Veh | raider-vs-player Pat; global NPC ram multiplier Veh |
| `TrafficLocations` / `TrafficCamps` (camp/POI discovery, camp status) | Pat / POI / NPC | POI world-locations registry (Towns, Quests, NPC also need camps); Patrols consumes |
| `NativeStormProtection` / `DustStormBridge` | Veh / Surv / Compat | Surv owns storm damage + shelter registry; Veh registers rig; Compat bridges DustStorm |
| `DayLight` | Surv / Core | Surv (time of day/sky) query, Core fallback |
| `ExitSpeed` | Veh / Core Fixes | Veh (player vehicle rule) |
| `PatrolVisibility` + `TrafficAgent` sensing | NPC / Pat | NPC senses query `CanPerceivePlayer(from, fwd, profile)` |
| `TrafficMap` + `TrafficMapInk` | HUD / Pat | HUD draws, Pat supplies route/marker data via `IMapMarkers` |
| `Cargo` | Loot / Veh | Loot (cargo loot) using a Veh "cargo volume" helper |
| `Paint` (incl. cargo container texture by loot key) | Veh / Loot / Pres | Veh; loot-key -> texture mapping supplied by Loot |
| `PickupCatalog` | Loot / Tools | Loot (catalog), editor UI in Tools |
| `CrewFactions`, `TrafficFactions.PlayerHostile`, `Opposed` | Pat / NPC / Quests | Core faction service (relations), rosters in Pat data |
| `CrewAppearance` | Pres / NPC | Pres character-model API |
| `Prefabs`, `Register`, `PlayerRef`, `TrafficSpace`, `PrefabName`, `AudioVoices`, `HideFsmWarnings` | Core | Core |
| `Convoy` progression (Distance Travelled FSM, `Boss_*` globals) | Pat / Core | Core progression query (Quests may need it) |
| `QuestFollowers` | Pat / Quests | split per 1; interface stays in Core |
| `MeleeWheels` | Gun | delete (dup) |
| `PatrolLoadPreparation` | Pat / Core Saves | Core load barrier with participants |
| `TrafficRoads`/`RoadPlanner`/`VehicleRoute`/`ElevationGrid` (world routing core) | Pat / Core | Patrols-owned "routing" service exposed via ModAPI (Towns, future NPC long-range foot travel); not Core unless another module needs vehicle routes |
| War Rig toggles in `[Patrols]` F6 section | Veh / POI | Veh; POI keeps spawn chance |
| `WorldSpawning` | Veh / POI | clearance -> Veh; notice list -> POI |

## 9. Save/config compatibility constraints

**Game save (ES3):**
- ES3 key `Apocapatrol.Patrol` (string, base64 of the binary sidecar; written to cache + file) - Persistence.cs:207,478-504.
- Binary format: magic `0x434F5041` "APOC", schema 14 (readers for 1-14), max 1024 cars, then tagged extension WDR1 `0x31524457` / WDR2 `0x32524457` (seated riders, trailer identity) - Persistence.cs:194-196, PatrolCrewDelta.cs:19. Field set of `PatrolSaveData/PatrolCarData/PatrolRiderData` incl. `trafficJson` (TrafficSave JSON v2, MapMagic-local; v1 scene coords readable) and merchant `MerchantSave` records - field names are format.
- ES3 prefab ids `735710202610050001` (motorcycle, Motorcycles/Factory.cs:17), `735710202610090001` (War Rig, WarRig/Factory.cs:20) and War Rig deterministic ES3 local reference ids (Factory.cs); prefab names `Motorcycle`, `WarRig` (template `body`), holder names `Apocapatrol.MotorcycleTemplates`, `WarRig.InactiveTemplates`; native storm prefab id `5697600806714206537` (read only).
- **Saved component type names** (ES3 stores `Namespace.Type, Assembly`; `Core.SaveTypes` maps `Apocapatrol, Apocapatrol.*` -> `WildWasteland.Modules.Patrols.*` and `WarRig, WarRig.*` -> `...Patrols.WarRig.*` by pure prefix): `PatrolMarker`, `Crew`, `Pilot`, `Ram+Sensor`, `Patrol+CorpseEject`, `Patrol+LateHealth`, `PassengerGuard`, `Pose`, `RiderGuard`, `RiderAnchorGuard`, `JumpClip`, `Detonator`, `LanceFuse`, `SimpleBanditTag`, `TrafficAgent`, `TrafficPlatform`, `TrafficActor`, `CampResident`, `EncounterCar`, `BattleCar`, `MerchantHost`, `MerchantGuardHealth`, `SuspensionTune`, `MotorcycleRearAccess`, `PatrolVisibility` (empty legacy), `Motorcycles.MotorcycleBalance`, `WarRig.WarRigUnit`. Splitting these across modules breaks the prefix rule: SaveTypes needs (a) a per-type table for old `Apocapatrol`/`WarRig` names and (b) new aliases for WW's own saves (`WildWasteland, WildWasteland.Modules.Patrols.X` -> new namespace). Nested names (`Patrol+CorpseEject`, `Patrol+LateHealth`, `Ram+Sensor`) need the outer class kept or explicit aliases. String type-name users must be updated: Quests Hire.cs:281, Performance CarLod.cs:194 / PatrolsBridge.cs:31-32, Towns TownRoadBridge.cs:20-23.
- Native objects created by Patrols must keep their names (`Register.Name`) so native ES3 save/restore and `TrafficState` part paths match.

**Sidecars and files:**
- `config\WildWasteland\Patrols\Saves\<slot>.bin` (+`.bak`, `.tmp`), read fallback `config\Apocapatrol\Saves\` - PatrolsModule.cs:100-101.
- Road cache `config\WildWasteland\Patrols\roads\<world>\<x>_<z>.roads` (magic `0x41505244`, v3, includes generation identity), fallback `config\Apocapatrol.roads` - regenerable.
- Data in `plugins\WildWasteland\Patrols\`: `BaseTemplates\`, `PlayerTemplates\`, `OldTemplatesFolder\`, `CarTemplates\` (legacy), `Textures\`, `Sounds\` (`witness-me.wav` etc.), `PatrolsAndLoot.json` (schema 3; holds patrol/convoy/merchant groups **and** loot profiles - splitting Loot out must keep this file readable or migrate), `MerchantLayouts.json` (v2), `BusPassengerPose.json(.bak)`, markers `legacy-templates-imported.txt`, `.coyote-traffic-v1`, `.merchant-traffic-v1`, `PatrolsAndLoot.invalid-*.json`; `WarRig\WarRig.glb`, `WarRig_colliders.glb`, `Defaults\`. Moving modules changes `Assets.ModuleDir(Id)` -> needs data-folder migration (copy-missing like PatrolsData).
- Embedded/disk resource names `Patrols.DefaultTemplates.*` (incl. `Truck_War_Rig.json` used by POI), `Patrols.EditorDefaults.json`, `Patrols.MerchantLayouts.defaults.json` (resource name = `<Id>.<path>`; module rename changes them).

**Config (settings.cfg main + tools.cfg):**
- F6: `[Patrols] Enabled` + 21 toggles (`Automatic spawns`, `Fury Road`, `Foot patrols`, `Faction battles`, `Merchants`, `Return to car`, `Ranged combat`, `Human turrets`, `Ram damage`, `Ram damage in car`, `Raiders overheat`, `Raider music`, `Custom paintjobs`, `Always explode cars`, `Cleanup`, `Tactical driving`, `Simple convoy bandits AI`, `War Rig spawns`, `War Rig ground recovery`, ...), `[Patrols / Spawning]`, `[Patrols / World traffic]`, `[Patrols / Merchants]`, `[Patrols / Combat]`, `[Patrols / Balance]`; `[Keys] Patrol editor / Template recorder / Bus passenger pose editor / Merchant item editor`. `Core/UI/CorePages.cs` reads `ConvoyEnabled/FuryRoad/FootPatrols` for the Game page "Enemy Vehicles".
- tools.cfg: `Patrols / Debug`, `/ Traffic tuning`, `/ Combat tuning`, `/ Turret pose`, `/ Driving`, `/ AI`, `/ Loot`, `/ Spawner`, `/ Cleanup`, `/ Audio`, `/ Pose`, `/ Pose.<Boltjaw|Flexa|Lugnut|Scrud|Sprokka|Scraffa|Spanna>`.
- Moving an entry to another module's section renames `[Section] key`: `ModuleConfig.Migrate` only reads old **standalone** cfgs on the **first WW run** (AGENTS 5.7), so a new WW->WW migration path (old WW section -> new section, every run until set) is required before splitting PatrolsSettings. Old standalone migrations (`com.denis.apocalypter.apocapatrol.cfg`, `.warrig.cfg`, `.merchantspotter.cfg`) must remain pointed at the new entries.
- Harmony id `<GUID>.Patrols` and module Id `Patrols` = config section prefix, asset folder, embedded resource prefix: keep `Patrols` as the Id of the Patrols target module.
- Replaced-standalone GUIDs in `ConflictsWith` (`apocapatrol`, `warrig`, `merchantspotter`) must follow the code they replace (War Rig GUID -> Vehicles).
