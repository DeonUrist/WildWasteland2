# W0-A Core kernel - report

Branch `refactor/W0-A`. Gate: `sh tools/refactor/compile.sh` -> COMPILE OK (39 warnings, same as base). `check_ownership.py W0-A refactor/main`
-> 0 files outside ownership; `check_comments.py --package W0-A` -> 0; `check_module_refs.py --package W0-A` -> 0.

## Files moved (namespace unchanged: `WildWasteland.Core`)
| from | to |
|---|---|
| Core/Plugin.cs, Runner.cs | Core/Boot/ |
| Core/ModuleBase.cs, ModuleHost.cs, ModuleConfig.cs | Core/Modules/ |
| Core/Scheduler.cs, WorldWork.cs, LoadingProgress.cs | Core/Scheduling/ |
| Core/Hooks.cs, Patches.cs | Core/Harmony/ |
| Core/Services.cs | Core/ModApi/Services.cs (rewritten as [Obsolete] facade) |
| Core/QuestServices.cs | merged into Core/ModApi/Contracts/Quests.cs (+ IQuestPresentation in Hud.cs) |
| Core/Hotkeys.cs | Core/Input/ |
| Core/SaveBus.cs | Core/Persistence/ |
| Core/Log.cs | Core/Log/ |

Not moved: `GameRefs.cs`, `Origin.cs` (ownership.json gives W0-A only `Game/EntityRegistry*.cs` inside `Core/Game`; B01 moves them),
`Assets.cs` (B01 -> Util), `TunedSettings.cs` (A1/B01). `tools/refactor/_common.py` has a `RELOCATED` map so file-map paths still resolve.

## Files created
- `Core/ModApi/ModApi.cs` (Provide/Withdraw/Get/TryGet/OwnerOf/Changed/RemoveAll/Describe), `ModEvents.cs` (IModEvent, ModEventHandler,
  Authority, BroadcastAttribute, IEventTransport, Subscribe/Unsubscribe/Publish/ApplyRemote/HasListeners/AuthorityOf/Stats; per-handler
  try/catch + timing when Trace.On), `EntityId.cs`; `Core/Game/EntityRegistry.cs` (`Entities`: Of/TryPeek/Find/Bind/Forget/Player/SeedRuntimeCounter).
- `Core/Log/Trace.cs`; `Core/Persistence/Sidecars.cs`, `SaveKeys.cs` (`SaveKey` via `SaveBus.Key`); `Core/UI/SettingsShell.cs`;
  `Core/Boot/ModuleRegistry.cs`.
- Contracts `Core/ModApi/Contracts/*.cs` and events `Core/ModApi/Events/*Events.cs` (lists below).
- Skeleton modules: `src/Modules/{Debug,Compatibility,Saves,Presentation,NPCAI,Gunplay,Vehicles,Loot,Tools,Developer}/<Id>Module.cs`
  (namespace `WildWasteland.Modules.<Id>`, DefaultEnabled, empty Init), registered after Performance.

## Files deleted
- `Core/SaveTypes.cs` (D12: it only held the old-assembly ES3 aliases WarRig/Apocapatrol/Apocaplayer/NPCAI/Gunplay/GunplayHUD; nothing
  else used the `ES3Reflection.GetType` prefix). Startup summary: one Core patch less.
- Dead `Origin.Jumps/LastDelta`, `Legacy.ParseFloat` (README 7, 0 readers).

## Contracts (all in namespace `WildWasteland.Core`)
- Presentation.cs: moved IAnimationApi, INpcTraversalAnimation, IAnimatedCharacter, ICharacterAssets, INpcAppearance; new CharacterKind,
  CharacterActionKind, CharacterInputs, ICharacterPresentation, ICharacterAppearance, TraversalKind, ITraversalLease, ITraversal, SeatKind,
  ISeatPoses, IWeaponPose, IVehicleSkins, CameraViewMode, ICameraRig, IPoseAuthoring, ISeatPoseAuthoring.
- Npc.cs: moved NavStructure, INpcApi; new NpcMode, PerceptionProfile, INpcControl, NavTemplateInfo, INavOverlay, IWaypointProvider, INavApi,
  IFollowers, ISeatedCombat, IPopulation.
- Weapons.cs: moved IGunsApi; new WeaponKind, WeaponStat, WeaponInfo, IWeaponCatalog, IWeaponStatModifier, IWeaponStats, IWeaponState.
- Hud.cs: moved MapDetail, IMapMarkers, ICompassMarkers, IQuestPresentation, IVehicleStatusDisplay, IHitFeedback; new IMapMarkerStyle,
  ILocationMarkers, NotificationKind, INotifications, IUiTheme.
- Vehicles.cs: moved IVehicleIgnition; new VehicleBuildOptions, IVehicleBuilder, IVehicleFactory, CockpitState, IVehicleControls,
  IVehicleOwnerQuery, IVehicleOwnership, IDrivingTelemetry.
- Patrols.cs: moved CampState, ICampStatus, IFollowerVehicles; new IFollowerRelease, IPatrols, IRouteHintProvider, IVehicleRouting,
  ICullingProtection, IMerchantLayouts.
- World.cs: moved PoiDefinition, IPoiApi; new TownInfo, TownGate, ParkingSlotInfo, ITownsApi, WorldLocationKind, WorldLocation,
  IWorldLocations, IWorldGenFacts (Vistas, provider Compatibility).
- Survival.cs: moved WeatherVisualDetail, IWeatherVisualPolicy, WeatherRegion, IWeatherRegions; new IStormQueries, IShelterProvider,
  IShelterRegistry, IDayLight.
- Loot.cs: LootEntry, ILootTables, ILootSubstitution, ILootSpawning. Quests.cs: moved QuestRow, QuestChoice, IQuestConversation, IQuestApi.
- Saves.cs: SaveKind, SavePhase, ISaves. Game.cs: IInventorySupplies, IInventorySlots, ILanguageMenu. Debug.cs: ILogPolicy, ITraceControl.

Design notes for Wave B: (1) an existing interface cannot grow without breaking its current implementer, so the "existing + X" rows of
core-modapi.md 8 are companion interfaces: `INpcControl` (INpcApi+), `IMapMarkerStyle` (IMapMarkers+), `IStormQueries` (IWeatherRegions+),
`IFollowerRelease` (IFollowerVehicles.Release). (2) `ModApi`/`ModEvents`/`EntityId` live in `WildWasteland.Core`, not
`WildWasteland.Core.ModApi` (a namespace named like the class would make `ModApi.Get` unresolvable inside Core). (3) Templates/loot rows in
contracts use strings/JSON so contracts never name module types. (4) `Sidecars.Register(owner, pattern)` has no `legacyReadOnly` (D12).

## Events (namespace `WildWasteland.Core.Events`, 136 structs, generated from one spec; [Broadcast] per core-modapi.md 9)
Core: ModuleStateChanged, OriginShifted, TileApplied/TileFinalized/TileMoved/TerrainEdited, HotkeyPressed, ItemCulled,
VehicleSimulationFrozen/Unfrozen, SupplyDropped, MoneyChanged, PlayerShout. Saves: LoadBegin, WorldReady, SaveBegin, BeforeSerialize,
AfterSerialize, SaveEnd, SavePresentation, CombatActivity. Presentation: CharacterAttached/Detached, CharacterAction, CharacterModelChanged,
TraversalStarted/Ended, SeatPoseChanged, CameraModeChanged, CharacterVoice. Npc: NpcSpawned/Despawned, NpcStateChanged, NpcKilled,
NoiseEmitted, NpcFrightened, NestBuilt/Freed/Alarm, WebTouched, BurrowChanged, PounceLeap, DogBite, FollowerHired/Dismissed/Died/Lost/
Mounted/Dismounted. Gunplay: WeaponFired, ProjectileSpawned/Ended, ProjectileHit, Hurt, VehiclePartHit, WheelPoppedOff, BoltLanded,
AmmoChanged, MagazineChanged, AdsChanged, WeaponSwitchPhase, UnloadStarted/Finished, AmmoDropped, ArmorPlateDropped. Vehicles:
VehicleSpawned/Despawned, VehicleExploded, VehicleIgnition, VehicleControlsState, TrailerAttached/Detached. Patrols: PatrolGroupRolled,
JourneyCreated/Departed/Materialized/Virtualized/Ended, VehicleDrivingState, CrewSeated, CrewBailedOut, CrewDismounted, CrewReboarded,
DriverPromoted, RamHit, LanceThrown, RiderLeap, RiderDetonated, MerchantVisit, MerchantStockChanged, FactionBattleStarted/Ended,
ReinforcementsCalled, TownVisit. POI: CampStateChanged, CampCleared, CampOwnershipChanged, TownPlanned/Built/StreamedOut/Discovered/
ConditionChanged/LotRoleChanged, PoiSiteGenerated, PoiEncounterSpawned, GasStationPlaced, PoiDressingReady. Loot: LootSpawned,
SpawnerProgressChanged, NaturalLootSubstituted. Survival: StormSpawned/Removed/PhaseChanged, StormHazardHit, LightningStrike,
StewPotSpawned/ContentChanged/Drink/Pour. HUD: LocationDiscovered, FogExplored, MapOpened/Closed. Quests: QuestOffered/Accepted/
Declined/Progress/Completed/Failed/Cancelled/Abandoned, ReputationChanged, PlayerHostilityChanged, ConversationOpened/ChoiceSelected/Closed.

Published by Core already: ModuleStateChanged (ModuleHost), OriginShifted (Origin), HotkeyPressed (Hotkeys), LoadBegin/WorldReady/SaveBegin/
SaveEnd (SaveBus; Kind = Manual, Ok = true from Core), all behind `HasListeners`.

## Kernel changes
- ModuleHost: `Current` (module in Init/ProvideApis/Shutdown/deferred init), `ProvideApis()` after Init, `Settings(SettingsShell.Shared)`
  after BindConfig, Cleanup also runs `ModApi.RemoveAll`, `ModEvents.RemoveAll`, `Services.RemoveHandlersOf` (old events: handlers declared
  in the module's namespace are dropped - README 9 ClearEvents bug), ModuleStateChanged event.
- IModule/ModuleBase: `ProvideApis()`, `Settings(SettingsShell)` (no StorageId/LegacySections, D12).
- Log: `Level` 0/1/2, `L1/L2`, `Main/Deep` (+1-3 arg generic overloads, format only when on), `Always/Warn/Error(owner, ...)` with "[Id] "
  prefix. `Info/Always(string)/Warn(string)/Error(string[,e])/Verbose` are [Obsolete] wrappers (Info = level 1). `Log.Error` no longer calls
  `SaveSystem.Fail`. `[Debug] VerboseLevel` (int 0/1/2) bound in settings.cfg; effective level = max(VerboseLevel, VerboseLog ? 1 : 0)
  because CorePages still toggles `Plugin.VerboseLog`.
- SaveBus: `ReportFailure(owner, msg)` (+[Obsolete] string form) still forwards to `SaveSystem.Fail` and raises `FailureReported`;
  `Key(es3Key)` -> SaveKey (cache write now + after the native flush, StoreCachedFile, verify, failure channel); `SaveSlot`; typed events.
- Scheduler: RemoveAll sets `Job.Removed` (README 9: Performance FS2/FS3 compile pump and PM5 guard come back after off -> on), new
  `Job.IsRemoved`; Interval keeps phase (`NextAt += Interval`, restart from now when more than one interval behind); job names checked
  ("<Id>.<Name>", logged once per name at level 1); `Job.ToString` no longer doubles a prefix ("Core.Frame" not "Core.Core.Frame").
- Hooks: table-driven (`HookBase` registry drives RemoveAll/ApplyUsed/Summary/Stats) - fixes FsmEnable never removed (README 9); `Calls`
  counted for postfix-only targets; per-handler `HandlerStat` (calls/ticks/errors) timed while `Trace.On`; warning when subscribing a kind
  the patch class lacks. New dispatchers (no subscribers yet, so never patched): SetAudioClipEnter, UiTextSetTextEnter, DoMousePick,
  GetGameObjectFsm, FsmAwake, FsmInnerEnable, FsmDisable, FsmDestroy, FsmPhysicsCallback, ExplosionShouldApplyForce (RefHook<args struct>
  for static/argument targets).
- D12: `ModuleConfig.Migrate` and `Legacy.*` are [Obsolete] no-ops (Get -> null, Exists -> false, FirstRun* false); settings.cfg no longer
  seeded from the old main cfg; old `[Keys] Tool window` migration removed; `Assets.LegacyPluginDir` [Obsolete] (callers deleted in Wave A).
- `Plugin.VERSION = "2.0.0"`; module list moved to `Boot/ModuleRegistry.cs` (also the Patrols AudioVoices read).

## [Obsolete] left for Wave C
`Services.*` (all members; setters register with ModApi under `ModuleHost.Current`), `Log.Info/Always(string)/Warn(string)/Error(string)/
Error(string,Exception)/Verbose`, `ModuleConfig.Migrate`, `Legacy`, `Assets.LegacyPluginDir`, `SaveBus.ReportFailure(string)`.

## Behaviour changes (intended)
1. README 9 fixes: Scheduler.Removed, Hooks FsmEnable removal, Log.Error no longer aborts saves, stopped modules lose old Services event
   handlers. 2. Interval jobs keep phase (no drift; no burst after hitches). 3. D12: no config/asset-folder migrations, no settings.cfg
   seeding, no old-assembly SaveTypes aliases. 4. New config entries: `[Debug] VerboseLevel`, `[<Id>] Enabled` for the 10 skeleton modules
   (nothing reads them). 5. Log text: SaveBus state lines now prefixed "[Core] "; level-1 lines when providers register.

## Denis test steps
1. Start the game: startup summary lists the 10 new modules as "on", same patch counts as 1.0.9 except Core without the SaveTypes patch;
   version 2.0.0. 2. F3 Modules page: toggle a module (e.g. Weather) off/on twice - no duplicate behaviour (FsmEnable fix), Performance
   off/on keeps FS2/FS3 compiled paths. 3. Set `[Debug] VerboseLevel` 1/2 or VerboseLog on: info lines appear. 4. Save/load a WW2 game
   (Towns/Patrols sidecars still verified). 5. `dotnet build`: expect CS0618 warnings unless the csproj request below is applied.

## Requests / open questions (also in requests/W0-A.md and state/W0-A.md)
- WildWasteland.csproj: add `<NoWarn>$(NoWarn);CS0618</NoWarn>` until Wave C (hundreds of [Obsolete] call sites).
- B04: take over `SaveBus.FailureReported`, drop the direct `SaveSystem.Fail` call in SaveBus.
- Wave B: namespace `WildWasteland.Modules.Debug` now exists, so unqualified `Debug.Log` inside any `WildWasteland.Modules.*` namespace
  resolves to it - use `UnityEngine.Debug` (none today). Same caution for `Tools`, `Saves`, `Vehicles`, `Loot` identifiers.
