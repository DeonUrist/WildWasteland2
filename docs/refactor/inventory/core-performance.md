# Inventory: Core + Performance + developer/ profiler

Area: `src/Core/**` (incl. `UI/`), `src/Modules/Performance/**`, `/home/claude/ww/developer/*.cs`. Snapshot 2026-10-22 (WW 1.0.9). Paths in tables are relative to `src/` (developer files relative to repo root). Line numbers = current snapshot.

---

## 0. Current Core public API (basis for the new Core design)

### 0.1 Entry, runner, lifecycle
| Type | Members (signature) | Notes |
|---|---|---|
| `WildWasteland.Plugin : BaseUnityPlugin` | `const string GUID="com.denis.apocalypter.wildwasteland", NAME, VERSION="1.0.9"`; `static Plugin Instance`; `static ConfigFile Main` (BepInEx main cfg), `Settings` (config/WildWasteland/settings.cfg), `Tools` (tools.cfg); `static ConfigEntry<Key> ToolWindowKey`; `static ConfigEntry<bool> EnableDebug, DeveloperMode, VerboseLog`; `static ConfigEntry<float> FrameBudgetMs` | Awake order (Plugin.cs:56-118): Assets.Init, Skin.Init, Legacy first-run flags, settings.cfg seeding, `[General] Apocasetter`, `[Keys] In-Game Settings Button`, `[General] Enable Debug`, RegisterModules, `ModuleHost.BindAll(Settings, Tools)`, `[Debug] VerboseLog`, `[Debug] Developer mode`, `[Core] Frame budget (ms)`, Runner.Ensure, Core.Frame job, SaveTypes.Install, ReplacementGuard.Prepare, HealthStacks/ColorGrading/ITweenGuard/QuietFsm, VistasWorldGen.Init, SaveRuntime/SaveSystem.Install, SaveSystem.RecoverFiles, ApocasaverExclusion.Install, ToolWindow.Init, sceneLoaded, audio voices (reads **Patrols** setting), ModuleHost.StartAll. 27 `[BepInDependency(..., SoftDependency)]`. |
| `Runner : MonoBehaviour` | `static Runner Instance`, `static GameObject Host`, `static void Ensure()`; internal `EarlyRunner` (order -1000: Update->EarlyUpdate, FixedUpdate->EarlyFixedUpdate), `LateRunner` (order 20000: LateUpdateLast + toggles GuiRunner), `GuiRunner` (OnGUI: Phase.Gui jobs then `ToolWindow.Draw`) | `OnApplicationQuit -> ModuleHost.Quit` |
| `enum Phase` | `EarlyUpdate, Update, LateUpdate, LateUpdateLast, EarlyFixedUpdate, FixedUpdate, Gui` | |
| `Job` (sealed) | `readonly IModule Owner; readonly string Name; readonly Phase Phase; bool Enabled; void Remove(); void RunNow(); long Calls; double TotalMs, LastMs, MaxMs; int Exceptions` (+ internal Tick/Step/Interval/NextAt/BudgetMicros/Removed/Scaled/Wants) | `ToString() = "<OwnerId|Core>.<Name>"` |
| `static Scheduler` | `Job EveryFrame(IModule owner, string name, Action tick, Phase phase = Update)`; `Job Interval(IModule owner, string name, float seconds, Action tick, Phase phase = Update, bool scaledTime = false)`; `Job Sliced(IModule owner, string name, int micros, Func<bool> step, Phase phase = Update)`; `Job Gui(IModule owner, string name, Action draw)` (no predicate, keeps IMGUI on); `Job Gui(IModule owner, string name, Action draw, Func<bool> wants)`; `Coroutine StartCoroutine(IEnumerator)`; `void StopCoroutine(Coroutine)`; `void RemoveAll(IModule)`; `IEnumerable<Job> Jobs(Phase)`, `AllJobs()`; `void ResetStats()`; stats `double FrameMs, FrameBudgetMs(=2), MaxFrameMs, AvgFrameMs; long Frames`; internal `GuiWanted()`, `Run(Phase)` | Job isolation: 20 errors / 10 s -> job disabled (Scheduler.cs:221). Interval = `now + Interval` after a random first phase. |
| `static WorldWork` (internal) | `const int Vehicles=0, Towns=1, Population=2`; `static bool Try(int owner)` | One heavy main-thread world-prep slice per frame, round-robin among requesters active in the last 4 frames; refuses during `SaveBus.Saving`/`SaveSystem.Busy`, timeScale 0, frame budget spent, 50 ms recovery after a >50 ms frame (loading screens exempt). Users: Patrols BuildQueue/VehicleBuildCache, Towns (TownsModule, InteriorPopulation). |
| `interface IModule` | `string Id, DisplayName; string[] ConflictsWith; bool DefaultEnabled; bool Active; void BindConfig(ModuleConfig); void Init(); void OnSceneLoaded(Scene, LoadSceneMode); void OnWorldReady(); void OnSaveBegin(string slot); void OnSaveEnd(string slot); void OnLoadBegin(string slot); void OnOriginShift(Vector3 delta); void Shutdown(); IEnumerable<ToolPage> ToolPages()` | |
| `abstract ModuleBase : IModule` | abstract `Id, DisplayName, BindConfig, Init`; virtual everything else (no-op); `bool Active {get; internal set}`; `ModuleConfig Config {get; internal set}`; `string Dir => Assets.ModuleDir(Id)` | |
| `enum ModuleState` | `NotStarted, Running, OffByConfig, OffConflict, InitFailed, DisabledAfterErrors` | |
| `static ModuleHost` | `class Entry { IModule Module; ModuleConfig Config; ModuleState State; string Conflict; int Errors; string LastError; }`; `enum Event { WorldReady, SaveBegin, SaveEnd, LoadBegin }`; `void Add(IModule)`; `IList<Entry> Entries`; `Entry Of(IModule)`; `bool IsRunning(IModule)`; `void Restart(IModule)` (unused); `string Summary()`; internal `BindAll(main, tools)`, `StartAll()`, `DeferredInit(IModule, Action)` (Quests only), `SceneLoaded`, `Broadcast(Event, slot)`, `OriginShift(delta)`, `Quit()`, `ReportJobException`, `ReportHookException` | Module disabled after 40 hook/lifecycle errors / 10 s. Cleanup = SetActive(false) + Scheduler/Hooks/Hotkeys.RemoveAll + Patches.Unpatch. |
| `static SaveBus` | `delegate void StateChange(string previous, string current)`; `event StateChange StateChanged`; `void ReportFailure(string)`; `string State`, `bool Saving, Loading, Busy`; `int Generation`; `string Slot` (SaveGameN.es3 / WW_*.es3 from ES3 default path); `void AfterSave(Action a, float delay = 1f)`; internal `event Action<ToolLayout> CleanupSettings`, `DrawCleanupSettings`, `event Action SaveCancelled`, `CancelSave()`, `PendingAfterSave`, `OnSceneLoaded()`, `Tick()` | The only SaveLoadGame watcher (1 read/frame). Broadcasts module hooks via ModuleHost. |
| `static LoadingProgress` (internal) | `struct Work { float Fraction; string Detail; }`; `void Register(IModule, Func<Work?>)`, `Remove(IModule)`; `bool ScreenVisible`; `bool MenusBlocked`; `void Begin()`; `void Read(bool saving, out float fraction, out string detail)` | Sources: Patrols, Towns. Reader: CleanHUD LoadingScreen. MenusBlocked reads `SinglePlayerSaving.Serializing` (Saves). |
| `static Origin` | `Vector3 Anchor` (MapMagic root, 1 s retry), `Transform AnchorTransform`, `Vector3 World(Vector3 scene)`, `Vector3 Scene(Vector3 world)`, `int Jumps`, `Vector3 LastDelta`; internal `Reset`, `BeforeJump`, `AfterJump` -> `ModuleHost.OriginShift` | |
| `static GameRefs` | `GameObject Player, PlayerCameraHolder, GameManager, NewGOArrayList, ItemNameId`; `PlayMakerFSM Menu, SaveLoadGame`; `Camera MainCamera` (per-frame cache); `bool InGame, InPlay` (per-frame); `string SaveLoadState`; internal `OnSceneLoaded` | Refresh does up to 7 `GameObject.Find` once a second while ANY ref is missing (GameRefs.cs:65-78). |
| `static FsmUtil` | `PlayMakerFSM Find(GameObject go, string fsmName)` | 81 call sites repo-wide. |
| `static Hotkeys` | `class Binding { IModule Owner; ConfigEntry<Key> Entry; Action Pressed; Func<bool> When; bool AllowWhilePaused; }`; `Binding Register(IModule, ConfigEntry<Key>, Action pressed, Func<bool> when = null, bool allowWhilePaused = false)`; `void RemoveAll(IModule)`; `bool Pressed(Key)`, `Held(Key)`; `void CheckConflicts()`; internal `Tick()` | |
| `ModuleConfig` (sealed) | `readonly string Prefix; readonly ConfigFile Main (=settings.cfg), Tools (=tools.cfg); ConfigEntry<bool> Enabled`; `string Section(string sub)`; `ConfigEntry<T> Player<T>(sub, key, def, desc[, AcceptableValueBase])`; `ConfigEntry<T> Tool<T>(sub, key, def, desc[, AcceptableValueBase])`; `ConfigEntry<Key> Hotkey(key, Key def, desc)` (-> settings.cfg `[Keys]`); `void Migrate<T>(ConfigEntry<T>, string legacyCfgFile, string section, string key)` | All binds go through `TunedSettings.Bind` (embedded defaults). |
| `static Legacy` | `bool FirstRunMain, FirstRunTools`; `string Get(cfgFile, section, key)`; `bool Exists(cfgFile)`; `float ParseFloat(string, float)` (unused) | |
| `static Log` | `ManualLogSource Source; bool Verbose`; `Info(msg)` (Verbose only), `Always(msg)`, `Warn(msg)`, `Error(msg)`, `Error(msg, Exception)` (rate-limited 5/msg), `ResetRateLimits()` | `Error` also calls `SaveSystem.Fail(msg)` (Log.cs:23-24): any error during a save transaction aborts the save. |
| `static Assets` | `string Root, DllPath; Init(dllPath); ModuleDir(id); ConfigDir; ModuleConfigDir(id); LegacyPluginDir(folder)` | |
| `static Patches` | `class Record {Owner,Target,Kind,Patch,Ok,Error}`; `int Conflicts`; `IEnumerable<Record> Records`; `Harmony For(IModule)`; `int ApplyClass(IModule, Type)`; `bool Patch(IModule, MethodBase original, MethodInfo prefix=null, postfix=null, finalizer=null, transpiler=null)`; `MethodInfo Method(Type, name, Type[] args=null)`, `Method(string typeName, name, args)`; `void Unpatch(IModule)`; `string Describe(MethodBase)`; `int Count(owner)`, `Failed(owner)`; internal `RemoveForeignOwner(string harmonyId)` | Harmony id `<GUID>.<ModuleId>`, Core = `<GUID>`. Conflict = same target+kind patched directly by two owners. |
| `static Hooks` | delegates `PrefixHandler<T>(T) -> bool`, `PostfixHandler<T>(T)`, `FinalizerHandler<T>(T, Exception) -> Exception`, `FsmEventPrefix(Fsm, FsmEvent, FsmEventData) -> bool`, `FsmStatePrefix/FsmStatePostfix(Fsm, FsmState)`, `PrefabResolver(long) -> ES3Prefab`; `class Hook<T> { string Target; bool Applied; long Calls; int PrefixCount, PostfixCount, FinalizerCount; IEnumerable<IModule> Subscribers; void Prefix(IModule, PrefixHandler<T>); void Postfix(IModule, PostfixHandler<T>); void Finalizer(IModule, FinalizerHandler<T>); bool RunPrefix(T); void RunPostfix(T); Exception RunFinalizer(T, Exception); }`; `FsmEventHook.Prefix(owner, h)`, `FsmStateHook.Prefix/Postfix`, `PrefabHook.Resolver(owner, h)`; `void RemoveAll(IModule)`; `void ApplyUsed()`; `string Summary()` | Dispatcher list in 0.2. |
| `static Services` | see 0.3 | |
| `static SaveTypes` | `void Alias(oldAssembly, oldNamespace, newNamespace)`; `int Resolved` (unused); internal `Install()` | prefix on `ES3Reflection.GetType(string)` |
| `static ApocasetterBridge` | `const GUID`; `bool Present`, `bool InputBlocked`, `bool SetInputBlock(bool)`, `Font GameFont`; internal `QueueDisable(PluginInfo, out string error)` | Also prefixes `Apocasetter.SettingsUI.Toggle` (menu guard while loading/saving). |
| UI: `EditorSession` | `bool Open; static bool Busy; static EditorSession Owner; static int Generation; static void CancelAll(); bool Acquire(); bool Acquire(bool pause); internal bool Acquire(bool pause, bool allowPaused); void Maintain(); bool Paused; void SetPause(bool); void Close(bool immediate=false); static void Tick()` | Developer plugin calls internal Acquire through reflection (developer/Plugin.cs:77). |
| UI: `ToolWindow` | `bool IsOpen; void Add(ToolPage); Remove(ToolPage); Toggle(); Open(ToolPage); Close(); CloseThen(Action)`; internal `NeedsGui, Init, Tick, Draw, OpenFromMenu, OpenSaves(bool)` | Settings window (F3, "F7" in docs). |
| UI: `ToolPage`, `ActionPage`, `ConfigPage`, `ToolLayout`, `ToolGroups` | `ToolPage { string Group, Title; IModule Owner; bool ShowWhenModuleOff, DebugOnly, IsTool; virtual OnShow(); virtual Update(); abstract Draw(ToolLayout) }`; `ActionPage(IModule owner, string group, string title, Action<ToolLayout> draw)`; `ConfigPage(owner, group, title, ConfigFile, params string[] sections)` / `(..., Func<ConfigDefinition,bool> filter)`; `ToolLayout`: `Next, Space, Heading, Text, Value, Button, Buttons, Toggle(label,bool), Toggle(ConfigEntry<bool>, label), Choices, Slider, KeyPair, Entry(ConfigEntryBase), Category, SearchSection, EntryFilter, DeduplicateEntries, Prefix, PageLabel` | |
| UI: `Ledger`, `LedgerDropdown`, `Skin` | editor widget kit (BeginSettings/EndSettings, Window, Panel, Label, Button, Choice, Slider, TextField, Number, Scroll, Dropdown, Reorder, Icon...), Apocapatrol PatrolSkin | Used by Patrols editors, PoiNavEditor, PoiDresser, PlayerBodyPages, PerGunAmmo, PerformancePage. |

### 0.2 Hooks dispatchers (28)
PlayMaker actions (`Hook<T>`, instance = action): `CreateObjectEnter` (CreateObject.OnEnter pre+post), `SendEventEnter` (pre+post), `SendEventUpdate` (pre), `SetFsmFloatEnter` (pre+post), `SetFsmFloatDo` (pre), `SetVelocityDo`, `MouseLookDo`, `WaitUpdate`, `GetButtonDownUpdate`, `GetButtonDo`, `GetButtonUpUpdate`, `GetMouseButtonDownUpdate`, `RaycastEnter` (pre+post), `RaycastDo`, `SmoothLookAtDo`, `ActivateGameObjectEnter` (pre+post), `ActivateGameObjectDo`, `SetPositionDo` (pre+post), `SensorRayHitEnter` (pre+finalizer), `LosEnter3D`, `LosUpdate3D`, `SensorDetectionsDo`. Core/engine: `ProcessEvent` (Fsm.ProcessEvent prefix), `EnterState` (Fsm.EnterState pre+post), `GetPrefab` (ES3ReferenceMgrBase.GetPrefab resolver chain), `FsmEnable` (PlayMakerFSM.OnEnable postfix), `OriginJump` (FloatingOrigin.Jump pre+post, always patched), `SpawnerCatalogEntries` (`Apocaspawner.Catalog.Entries` postfix, optional).

### 0.3 Services (Services.cs + QuestServices.cs) - all defined by Core
| Member | Signature | Provider -> consumers |
|---|---|---|
| `Animation` | `IAnimationApi` (+ `IAnimatedCharacter`, `INpcTraversalAnimation`) | PlayerBody -> Guns, NPC, Patrols, Quests |
| `PoiNavSetup` | `static volatile bool` | NPC -> Towns |
| `PlayerLadder`, `PlayerLadderSide`, `LadderSlidePhase`, `LadderSlideSpeed` | `Collider`, `Vector3`, `float`, `float` (raw fields) | PlayerBody -> NPC PoiNav |
| `LadderClimbers` | `readonly List<Vector3>` | NPC -> PlayerBody |
| `Npc` | `INpcApi` (+ `NavStructure`) | NPC -> CleanHUD, Guns, Patrols, Quests, Towns |
| `NpcAppearance` | `INpcAppearance { void Dress(GameObject, string identity, bool randomize) }` | NPC -> Patrols |
| `CharacterAssets` | `ICharacterAssets { bool TryPaths(string identity, out string model, out string texture) }` | NPC -> PlayerBody |
| `VehicleCombatActive` (internal) | `Func<bool>` | Patrols -> Core SaveCombat |
| `ReportCombat()` (internal) | `void` -> `SaveCombat.Activity()` | Guns -> Core |
| `Guns` | `IGunsApi` | Guns -> HUD, NPC, Patrols, PlayerBody |
| `Hud` | `IHitFeedback` | HUD -> Guns |
| `VehicleStatus` | `IVehicleStatusDisplay { bool TryShow(bool ignitionOff, bool handbrakeOn, bool playing, string volume) }` | CleanHUD -> PlayerBody |
| `VehicleIgnition` | `IVehicleIgnition { TryPlayer, TryRunning, TryStart, TryStop }` | EngineSimCompat -> Core (VehicleLoadPlacement), Patrols, PlayerBody |
| `Compass` | `ICompassMarkers` | CleanHUD -> Patrols, Quests, Towns |
| `Map` | `IMapMarkers` (+ `enum MapDetail`) | CleanHUD -> Guns, Patrols, PlayerBody, Quests, Towns |
| `Weather` | `IWeatherRegions` (+ `struct WeatherRegion`) | Weather -> CleanHUD, Core (VistasWorldGen, CorePages) |
| `WeatherVisualPolicy` | `IWeatherVisualPolicy { WeatherVisualDetail WeatherDetail }` | **Performance** -> Weather |
| `FollowerVehicles` | `IFollowerVehicles` | Patrols -> Quests |
| `Quests` / `QuestUi` | `IQuestApi`, `IQuestPresentation` (+ `QuestRow`, `QuestChoice`, `IQuestConversation`) | Quests -> CleanHUD / CleanHUD -> Quests |
| `Camps` | `ICampStatus { CampState At(Vector3 world, float radius) }` | Patrols -> CleanHUD, Quests |
| `Poi` + `event PoiChanged` | `IPoiApi { Register(PoiDefinition), Remove(id) }` | POI -> Patrols, Towns |
| events | `CrewBailedOut(Action<GO car, GO pax, GO npc>)`, `NpcHurt(Action<GO victim, GO attacker>)`, `VehiclePartHit(Action<GO part, GO attacker>)`, `VehicleExploded(Action<GO>)`; raisers `RaiseCrewBailedOut`, `RaiseVehicleExploded` (public), `RaiseNpcHurt`, `RaiseVehiclePartHit` (internal) | Patrols/NPC/Guns raise; NPC, Quests, CleanHUD, Patrols subscribe. `ClearEvents()` (Services.cs:81) is never called. |

---

## 1. File map

### 1.1 src/Core
| path | lines | what it does | target | action |
|---|---|---|---|---|
| Core/ApocasaverExclusion.cs | 34 | Stands down standalone Apocasaver (prefix-blocks entry points, removes its Harmony ids, takes over HandPose API) | Compatibility (Apocasaver) | MOVE; HandPose handlers (`SaveCompatibility.SetPose/GetPose`) reached through Saves ModAPI |
| Core/ApocasetterBridge.cs | 173 | Apocasetter reflection bridge: InputBlocked, SetInputBlock, GameFont, menu-toggle guard, QueueDisable for replaced mods | Core | MOVE; replace `Expression.Compile` (line 41, interpreted on this runtime) with Core access helper |
| Core/Assets.cs | 38 | Install/config path helpers | Core | MOVE |
| Core/AtomicFile.cs | 34 | Same-directory atomic file replace (MoveFileEx) | Core (util) | MOVE (users: Saves, Towns PoiDressLayout, CleanHUD LocationMarkers) |
| Core/ColorGrading.cs | 28 | Forces native "color grading" option/volume off (ActivateGameObjectDo prefix) | Core/Fixes | MOVE |
| Core/DiskResources.cs | 46 | asset-index.tsv runtime resource index (`Names`, `Open`, `FilePath`) | Core | MOVE |
| Core/GameRefs.cs | 99 | Cached game objects/FSMs, InGame/InPlay; `FsmUtil.Find` | Core | MOVE |
| Core/HealthStacks.cs | 145 | Bandage/First-aid slot top-up to 3 (GetButtonDownUpdate prefix) | Core/Fixes | MOVE |
| Core/Hooks.cs | 365 | 28 shared Harmony dispatchers + P_ forwarders | Core | REWRITE: table-driven registry (one list drives RemoveAll/ApplyUsed/Summary - `FsmEnable` is missing from RemoveAll, see 5.1); optional per-handler timing for Debug; add dispatchers for targets Performance patches twice (3.3) |
| Core/Hotkeys.cs | 78 | Key polling, conflict check | Core | MOVE |
| Core/ITweenGuard.cs | 74 | iTween physics-tween + Stop substring guard | Core/Fixes | MOVE |
| Core/InventorySupplies.cs | 119 | Native ammo/supply counters + "drop N" through native item creation (CleanHUD inventory) | Core/Fixes (per brief) | MOVE; replace `SaveSystem.LocalReady/SinglePlayerSaving/SaveRuntime.Health` (line 19) with SaveBus/GameRefs queries; sliced prefab discovery job registered from SaveRuntime (move registration) |
| Core/LoadingProgress.cs | 67 | Loading fraction/detail model, module progress sources | Core (model) + HUD (draws it) | MOVE; `MenusBlocked` must read Saves state via SaveBus, not `SinglePlayerSaving` |
| Core/Log.cs | 39 | Single log facade, rate limit | Core facade; policy -> Debug | SPLIT: `Log` API stays Core (add `Level 0/1/2`); level switch/UI -> Debug; remove `SaveSystem.Fail` call (Log.cs:23-24) -> Saves subscribes to an error event |
| Core/ModuleBase.cs | 69 | IModule + ModuleBase contract | Core | MOVE |
| Core/ModuleConfig.cs | 122 | ModuleConfig binders/migration + `Legacy` cfg reader | Core | MOVE; delete unused `Legacy.ParseFloat` |
| Core/ModuleHost.cs | 273 | Lifecycle, states, conflict check, failure isolation, broadcasts | Core | MOVE (add module-state events to ModAPI; `Restart` unused) |
| Core/NativeAccess.cs | 70 | Cecil/DMD typed accessors for native members | Core/Access | MERGE-INTO new `Core/Access` (with OptionalAccess, Performance FastReflect, BatchCompiler) |
| Core/OptionalAccess.cs | 36 | Cecil accessors for optional plugins | Core/Access | MERGE-INTO `Core/Access` |
| Core/Origin.cs | 51 | Floating-origin tracking, World/Scene conversion | Core | MOVE |
| Core/Patches.cs | 154 | Per-module Harmony instances, records, conflict detection | Core | MOVE |
| Core/Plugin.cs | 183 | Entry point/boot order, module registration, Core.Frame job, audio voices | Core | SPLIT: boot/registration/Core.Frame stay; `ApplyAudioVoices` + `Modules.Patrols.PatrolsModule.AudioVoices` (line 114) -> Core audio setting; Saves install/recover (106-108) -> Saves.Init; VistasWorldGen/ApocasaverExclusion init (105,109) -> Compatibility.Init; fixes list -> Core/Fixes registry |
| Core/QuestServices.cs | 46 | IQuestApi / IQuestPresentation / IQuestConversation contracts | Core ModAPI | MERGE-INTO ModAPI (quests/dialogue contracts file) |
| Core/QuietFsm.cs | 49 | Silences "Could not find FSM" warnings; PlayMakerPrefs perf warnings only with Verbose | Core/Fixes (+ Debug log policy) | MOVE; its switch `On` is bound by **Patrols** (`PatrolsSettings.cs:120`) - give it a Core/Debug key |
| Core/ReplacementGuard.cs | 146 | Detects replaced standalone mods, queues disable, title notice + recommended mods | Core (detection) + HUD (notice UI) | SPLIT: `Prepare/Disable/Status/LateConflict/Tick` stay Core; `Draw/WantsNotice/RebuildNotice` -> HUD (uses CleanHUD GlyphFont/AccentTheme/Assets, lines 35-36, 113-116) via Core notice API |
| Core/Runner.cs | 65 | Runner GameObject + Early/Late/Gui runners | Core | MOVE |
| Core/SaveBus.cs | 126 | SaveLoadGame watcher, lifecycle broadcast, AfterSave | Core | SPLIT: watcher/StateChanged/AfterSave/Slot/ReportFailure stay Core; `CancelSave`, `SaveCancelled`, `CleanupSettings/DrawCleanupSettings` (Patrols UI hook) -> Saves |
| Core/SaveCleanup.cs | 166 | Opt-in conservative removal of abandoned corpses/empty packs on verified save | Saves | MOVE |
| Core/SaveCombat.cs | 83 | Combat detection for autosave deferral (Detection FSMs, events, Attack states, Patrols vehicle combat) | Saves | MOVE; consume NPCAI/Gunplay combat via ModAPI event instead of `Services.VehicleCombatActive`/`ReportCombat` |
| Core/SaveCompatibility.cs | 61 | Apocasaver stable hinge keys on ES3 Save/Load actions; HandPose store | Saves (+Compatibility) | SPLIT: `BeforeSave/AfterSave/BeforeLoad/AfterLoad/Stable/SavePoses` -> Saves; `SetPose/GetPose` (Apocasaver API takeover) -> Compatibility; `VehicleLoadPlacement.LoadedStance` call (42) -> Saves "PlayerVariableLoaded" event |
| Core/SaveFiles.cs | 219 | Durable undo journal, participants, recovery, prune | Saves | MOVE; replace hard-coded module sidecars in `Participants` (183-190) with Core sidecar registry API |
| Core/SaveRuntime.cs | 149 | Native menu adapters, active file identity, launch, saving overlay, `Surface()` placement probe | Saves (+HUD, Core util) | SPLIT: menu event/launch/IsSaveFile/ActiveFile -> Saves; `DrawSaving` (uses CleanHUD GlyphFont, 113) -> HUD; `Surface` -> Core util (VehicleLoadPlacement); `Health/InCar` -> GameRefs queries; `InventorySupplies.Install()` (40) -> Core/Fixes |
| Core/SaveSystem.cs | 433 | Manual/auto saves, publish snapshot, backups, retention, autosave schedule, exit save | Saves | MOVE; `Modules.CleanHUD.MessageHook.Push` (121) -> HUD notify via ModAPI; Towns key check (264-265) -> sidecar registry |
| Core/SaveTerrain.cs | 48 | Holds MapMagic streaming during world save | Saves | MOVE |
| Core/SaveTypes.cs | 74 | ES3 type-name aliasing for old assemblies | Core (needed with Saves off) | MOVE; must gain aliases for every namespace the refactor renames (section 9) |
| Core/SaveWarning.cs | 34 | Autosave countdown text under the compass | Saves (state) + HUD (draw) | SPLIT: `Show/phase` -> Saves; `Draw` (uses `CleanHud.Hud.SaveWarningAnchor`, `GlyphFont`) -> HUD |
| Core/Scheduler.cs | 239 | Job scheduler, frame budget, job isolation, stats | Core | MOVE; fix `RemoveAll` not marking `Removed` (5.1) |
| Core/Services.cs | 402 | Cross-module service fields, contracts, 4 events | Core ModAPI | REWRITE into typed ModAPI + event bus (split contracts per domain; raw ladder fields -> query/event) |
| Core/SinglePlayerSaving.cs | 107 | Offline save coroutine, input freeze, new-world identity, load launcher | Saves | MOVE |
| Core/TunedSettings.cs | 57 | Embedded default values for binds; profile install | Core | MOVE; rename resource prefixes `CleanHUD.DefaultSettings.*` / `CleanHUD.DefaultProfiles.*` (20, 44) to Core-owned names (keep old names readable) |
| Core/VehicleLoadPlacement.cs | 120 | In-vehicle save load: park car, place player beside it, restore on-foot camera | Core/Fixes (per brief) | MOVE; triggered by Saves hooks (`SaveSystem.LoadedPath`, `SaveCompatibility.AfterLoad`) -> needs SaveBus events; uses `SaveRuntime.Surface/Health` |
| Core/VistasWorldGen.cs | 203 | Vista World Gen (World Amp) bridge: quiet log, field resolver, banners, scouting off, sandstorm owner, road capture, terrain-edit event | Compatibility | MOVE; publish `PlaceEntered`, `TerrainEdited`, `RoadLines/RoadRevision/RoadIdentity`, `Present`, `BannerHeight`, `CleanBanners` through Core ModAPI (consumers: Towns x6 files, CleanHUD, Patrols) |
| Core/VoiceAudio.cs | 52 | 16-bit PCM WAV loader with RMS gain | Core (util) | MOVE (consolidation target for other WAV decoders, e.g. Quests `ReadWav`) |
| Core/WorldHistory.cs | 174 | Captures/restores terrain-pooled POI FSM save records across tile streaming | Saves | MOVE |
| Core/WorldWork.cs | 33 | One heavy world-prep slice per frame arbitration | Core | MOVE (read Saves busy state through SaveBus) |

### 1.2 src/Core/UI
| path | lines | what it does | target | action |
|---|---|---|---|---|
| Core/UI/CorePages.cs | 513 | Game (PLAYER/WORLD/HUD/SYSTEM), Modules, Debug, Developer, Harmony pages; reset; scheduler table | Core shell + modules + Debug | SPLIT: `Register/Modules/ResetSettings` (generic part)/`DrawDebugPage`/Category plumbing -> Core shell; `DrawPlayer/BodySelection` -> Presentation contribution; `DrawWorld/NavigationSettings/CompassDetail` -> Patrols/HUD/POI/Survival contributions; `DrawHud/CrosshairSelection/FeedbackSwitch` -> HUD/Gunplay contributions; `DebugSetting/RetainedDebugSetting/DeveloperSetting/VerboseSetting/Logging` -> Debug (single VerboseLog); `Performance(ToolLayout)` -> Debug; `Harmony(l, owner)` -> Debug. Remove all `WildWasteland.Modules.*` refs (lines 5-8, 30, 49, 56, 75-76, 91-151, 181-206, 230-247, 266, 277, 290-300, 337-350, 369) via a "settings row" ModAPI |
| Core/UI/EditorSession.cs | 63 | Single modal owner (pause, cursor, input block) | Core | MOVE (make `Acquire(bool,bool)` public so Debug needs no reflection) |
| Core/UI/Ledger.cs | 440 | Ledger widget kit + LedgerDropdown | Core | MOVE (default `Header(width)` "APOCAPATROL" is Patrols-specific) |
| Core/UI/SaveLibraryPage.cs | 222 | Save library/backup UI, SYSTEM page | Saves | MOVE (drawn in Core shell) |
| Core/UI/Skin.cs | 324 | Old PatrolSkin theme (fonts, textures, plank/rust widgets) | Core | MOVE; DELETE unused `PlankButton`, `Frame`, `PitBox`, `SlotBox`, `HazardStrip` (0 call sites) |
| Core/UI/ToolPage.cs | 412 | ToolPage/ActionPage/ConfigPage/ToolLayout/ToolGroups | Core | MOVE |
| Core/UI/ToolWindow.cs | 263 | Settings window (F3) host, page list, search, accent swatches | Core | MOVE; replace CleanHUD refs (lines 4, 24-26, 147-155, 175-183) with a Core "theme/glyph font" service provided by HUD |

### 1.3 src/Modules/Performance
Feature legend: P = preset-gated (`UsesPreset`, off with the default VANILLA preset); X = exact (always on when the module is on); S = Shadow mode supported.

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/Performance/PerformanceModule.cs | 209 | Feature discovery (reflection), preset, per-feature config, lifecycle fan-out, tile event, EnsurePatched cache, WeatherVisualPolicy provider | Core/Optimizations host | REWRITE as Core optimization registry (no module switch hop; explicit feature list instead of `Assembly.GetTypes()` discovery, line 62) |
| Modules/Performance/Framework/Feature.cs | 151 | Feature base (mode Off/On/Shadow, preset, Knob overrides, Mismatch) | Core/Optimizations | MOVE |
| Modules/Performance/Framework/PerformancePage.cs | 81 | Performance tab (switch, preset, measurements) + Developer feature list | Core shell (preset) + Debug (measurements, feature stats) | SPLIT: preset UI -> Core settings page; `Telemetry.Draw`/`CorePages.Performance` calls + `DrawDeveloper` stats -> Debug |
| Modules/Performance/Framework/Telemetry.cs | 188 | Frame ring buffer, spikes+tags, GC, ProfilerRecorders, FSM census, feature counters | Debug | MOVE (merge with developer Measurements, see 1.4) |
| Modules/Performance/PlayMaker/ActionDataPort.cs | 383 | Exact copy of PlayMaker `ActionData.LoadActionField` (FS4) | Core/Optimizations (PlayMaker) | MOVE (pin to PlayMaker version; Debug.LogError at 358 = vanilla message) |
| Modules/Performance/PlayMaker/BatchCompiler.cs | 99 | Cecil batch assembly of ctors/field setters | Core/Access | MERGE-INTO `Core/Access` |
| Modules/Performance/PlayMaker/CompileQueue.cs | 84 | Background batch compile queue for FS2/FS3 | Core/Access | MERGE-INTO `Core/Access` (fix `_job.Removed` reliance, line 36) |
| Modules/Performance/PlayMaker/EventDispatch.cs | 296 | PM2 targeted events (X,S), PM4 broadcast without list copy (X) | Core/Optimizations | MOVE |
| Modules/Performance/PlayMaker/FastActionLoadFeature.cs | 237 | FS4 compiled action loading (X) | Core/Optimizations | MOVE |
| Modules/Performance/PlayMaker/FastCallMethodFeature.cs | 137 | FS3 CallMethod transpiler -> compiled invoker (X) | Core/Optimizations | MOVE |
| Modules/Performance/PlayMaker/FastPropertyFeature.cs | 479 | FS2 compiled FsmProperty Get/Set (X,S) | Core/Optimizations | MOVE |
| Modules/Performance/PlayMaker/FastReflect.cs | 401 | DMD/Cecil accessor builders (getters/setters/ctors/invokers) | Core/Access | MERGE-INTO `Core/Access` (duplicates NativeAccess/OptionalAccess) |
| Modules/Performance/PlayMaker/FsmEventSkip.cs | 62 | Proves an FSM system event is a no-op | Core/Optimizations | MOVE |
| Modules/Performance/PlayMaker/FsmRegistry.cs | 185 | PM1 per-GameObject FSM index + PM3 fast list removal (X) | Core (FSM index service) | MOVE to Core as public `FsmIndex` (other modules scan `PlayMakerFSM.FsmList`: Towns x2, Weather, Quests x3, Patrols TownTraffic) |
| Modules/Performance/PlayMaker/FsmSleepFeature.cs | 309 | FS1 idle FSM sleep via ManualUpdate (X,S); ~30 wake patches | Core/Optimizations | MOVE; use `FsmPause` ownership bits (today a second ManualUpdate owner, see 5.4) |
| Modules/Performance/PlayMaker/GuiLoopFeature.cs | 426 | PM5 PlayMakerGUI candidate loop, OnGUI proxy emulation, visibility event skip (X) | Core/Optimizations | MOVE (fix `_guard.Removed`, line 83) |
| Modules/Performance/PlayMaker/MousePickFeature.cs | 170 | PM7 one shared mouse-pick raycast per frame (X,S) | Core/Optimizations | MOVE (shares `ActionHelpers.DoMousePick` with PlayerBody PickAssist postfix) |
| Modules/Performance/PlayMaker/SigmaFsmStates.cs | 33 | SigmaMod guard: InitStates before early action reads (postfix on PlayMakerFSM.FsmStates getter) | Compatibility (SigmaMod) | MOVE; cache `Chainloader.PluginInfos.ContainsKey` once (line 22 runs per getter call) and only patch when SigmaMod is loaded |
| Modules/Performance/PlayMaker/StringConvertFeature.cs | 125 | PM10 reuse unchanged number strings (X,S) | Core/Optimizations | MOVE |
| Modules/Performance/PlayMaker/TriggerProxyFeature.cs | 141 | PM6 exact copies of 6 Fsm trigger/collision methods skipping unhandled system events (X) | Core/Optimizations | MOVE (note: skipped events no longer reach `Hooks.ProcessEvent` subscribers) |
| Modules/Performance/Content/Coyotes.cs | 147 | CT8 repeat Coyote hostility broadcast only to still-friendly receivers (X) | Core/Optimizations (ambiguous: NPCAI factions) | MOVE |
| Modules/Performance/Content/Culler.cs | 329 | CT1 sliced replacement of 4 vanilla cull/delete sweeps (P) | Core/Optimizations | MOVE; replace `PatrolsBridge` with ModAPI culling-protection query |
| Modules/Performance/Content/FsmEdits.cs | 324 | Watch FSMs by name at Awake, verified action replace, UndoLog, FsmPause (ManualUpdate ownership) | Core/Optimizations (FSM edit framework) | MOVE (FsmPause -> Core FSM service shared with FS1) |
| Modules/Performance/Content/HudFsms.cs | 141 | CT7 player-sheet texts at 2 Hz while hidden; DayTime stepped at 0.2 s (X; 0.2 s day-change latency) | Core/Optimizations (ambiguous: HUD) | MOVE |
| Modules/Performance/Content/ItemFsmFixes.cs | 109 | CT9 no duplicate Rigidbody AddComponent (X); CT9 RestartOnEnable=false on empty data FSMs (X) | SPLIT: `DuplicateRigidbodyFeature` -> Core/Fixes (vanilla error-spam fix); `DataFsmRestartFeature` -> Core/Optimizations |
| Modules/Performance/Content/LightFlicker.cs | 121 | CT6 flicker FSMs stepped at preset Hz within distance (P) | Core/Optimizations | MOVE |
| Modules/Performance/Content/PatrolsBridge.cs | 64 | Reflection into Patrols War Rig cleanup protection for CT1 | ModAPI | DELETE after adding `Services.Culling { bool Protected(GameObject); void BeforeFarDelete(GameObject) }` (requested in its own header) provided by Patrols |
| Modules/Performance/Content/PlayerCapsule.cs | 84 | CT10 player capsule height written only on change (X) | Core/Optimizations | MOVE |
| Modules/Performance/Content/PollFeatures.cs | 62 | CT3 hinge polls, CT5 wait-until polls, CT11 option toggles (X) | Core/Optimizations | MOVE |
| Modules/Performance/Content/PollSleep.cs | 289 | Exact sleep for polling FSM states (EnterState postfix + EarlyUpdate job) | Core/Optimizations | MOVE (replace `IsAlive` AllJobs scan, line 51/58) |
| Modules/Performance/Content/ProxyAction.cs | 64 | Stand-in FsmStateAction forwarding to the original | Core/Optimizations | MOVE |
| Modules/Performance/Content/VehicleReflection.cs | 146 | CT4 Pedal/RpmGear/Handbrake FSM actions use direct NWH access (X) | Core/Optimizations (ambiguous: Vehicles) | MOVE |
| Modules/Performance/Rendering/AnimatorCulling.cs | 110 | RC6 NPC prefab animators -> CullUpdateTransforms (P) | Core/Rendering (coordinate with Presentation, which forces AlwaysAnimate) | MOVE |
| Modules/Performance/Rendering/CameraCulling.cs | 182 | RC3 per-layer cull distances on game cameras (P); RC4 far clip (P) | Core/Rendering | MOVE (no overlap with PlayerBody "CameraCulling" = occlusion cutaway; rename to avoid confusion) |
| Modules/Performance/Rendering/CullLods.cs | 187 | RC2 cull-only LODGroups + LOD bias cap (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/GcSlice.cs | 51 | EN8 incremental GC time slice (P) | Core/Rendering (engine settings) | MOVE |
| Modules/Performance/Rendering/GeneratedLodExport.cs | 124 | RC9 authoring: export loaded meshes for the LOD generator | Tools | MOVE |
| Modules/Performance/Rendering/GeneratedLodPack.cs | 217 | RC9 LOD pack reader (catalog.tsv, .wwlod) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/GeneratedLods.cs | 368 | RC9 swap meshes to generated LODs, zoom restore, save revert (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/LightPolicy.cs | 267 | EN1 point-light shadow budget, distance cull, headlights, pixel lights (P) | Core/Rendering | MOVE (remove 10 s `FindObjectsOfType<Light>`, line 190) |
| Modules/Performance/Rendering/ParticleTuning.cs | 130 | EN5 particle prewarm/culling/collision/lights/max (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/PostFx.cs | 299 | EN2 skip zero-intensity SSS + lean Sharpen (X); EN2 TAA->SMAA/FXAA (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/RenderShared.cs | 526 | RenderScan (sliced prefab/scene scan), SaveGuard, QualityGuard (+ graphics-menu postfix), GameCameras | Core/Rendering | MOVE (SaveGuard should use the Saves "before serialize" event) |
| Modules/Performance/Rendering/SmallCasters.cs | 110 | RC1 tiny renderers stop casting shadows (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/SunShadows.cs | 113 | EN3 sun shadow distance/cascades/res/skin weights caps + layer shadow cull (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/TerrainDetail.cs | 119 | EN4 terrain pixel error/basemap/grass (P) | Core/Rendering | MOVE |
| Modules/Performance/Rendering/TextureQuality.cs | 48 | RC8 texture limit / aniso cap (P) | Core/Rendering | MOVE |
| Modules/Performance/World/CarLod.cs | 322 | CT2 parked-car FSM pause, kinematic threshold, NWH off (P) | Core/Optimizations (ambiguous: Vehicles) | MOVE; replace namespace sniffing (193-194) with ModAPI "vehicle controlled by a module" query |
| Modules/Performance/World/WorldSettings.cs | 404 | RC5 MapMagic budget (P), RC7 CCD sweep (P), EN6 time/sleep (P), EN6 origin-jump listener swap (X), EN7 Azure sun once/frame (X), EN7 Azure weather rate (P) | Core/Optimizations | MOVE (6 features; consider one file per feature) |

### 1.4 developer/ (standalone `WildWastelandDeveloper.dll`)
| path | lines | what it does | target | action |
|---|---|---|---|---|
| developer/Plugin.cs | 185 | Separate BepInEx plugin (F4), own MonoBehaviour runtime, capture scheduling, summaries | Debug | REWRITE inside Debug (Scheduler job + Core UI shell; no own MonoBehaviour, no reflection into `EditorSession.Acquire`, line 77) |
| developer/Instrumentation.cs | 218 | Opt-in Harmony timing probes over WW namespaces (+ optional base-game scopes), self/inclusive/main/worker/alloc | Debug (level 2 tracing) | MOVE; install through `Core.Patches` with owner Debug instead of `new Harmony` (line 64) |
| developer/Measurements.cs | 116 | Frame percentiles (8192), GC, ProfilerRecorders, per-job ms/s deltas from Scheduler, module states | Debug | MERGE-INTO Debug FrameStats (with Telemetry) |
| developer/Diagnosis.cs | 146 | Capture JSON, A/B comparison, settings diff, warnings; own JSON writer | Debug | MOVE; JSON writer -> Core Json util |
| developer/Reports.cs | 83 | Export capture folder (CSV/JSON/MD, MD5 of DLLs) | Debug | MOVE |
| developer/Dashboard.cs | 218 | IMGUI dashboard tabs (Overview, Scheduler, Detailed calls, Systems, Counters, Logs, Dev tools) | Debug | REWRITE as Debug ToolPages in Core shell |

---

## 2. Cross-module coupling

### 2.1 Core -> module types (rule violations: Core must not know modules except registration)
| file:line | used | why |
|---|---|---|
| Core/Plugin.cs:114 | `Modules.Patrols.PatrolsModule.AudioVoices` | audio voice count setting lives in Patrols |
| Core/Plugin.cs:128-140 | 13 module constructors | registration (acceptable, keep in one registry file) |
| Core/UI/CorePages.cs:5-8 + ~40 sites | `PlayerBodyModule.*` (BodyFirstPerson, FullBodyOnlyOnFoot, EmptyHandArms, FirstPersonArmsOnlyOnFoot, ClimbingOn, ThirdPersonOnFoot, OcclusionPrototype, Character, Gender, VehicleHotkeyHint, VehicleStatusHint, WeaponAdjust, Instance), `CharacterPortraits.Order/Picture`, `InventoryModel.PrepareSettingsPictures`; `HudModule.*` (SelectionDot, Crosshair, HudEnabled, DamageNumbers, HitMarker, HeadshotSound, Instance), `CrosshairController.PreparePreview/DrawPreview`; `PatrolsModule.*` (ConvoyEnabled, FuryRoad, FootPatrols, MenuKey, TemplateDumpKey, TurretSpotEditor, BusPassengerPoseEditorEnabled, Running, Instance.OpenEditor), `Patrols.WarRig.S.GroundRecoveryEnabled`; `CleanHudModule.*` (~25 entries), `ChaseCameraHud.HudEnabled`; `GunsModule.ToggleIronsights`; `NpcModule.ShoutKey/ShoutModifier/ShowNav`; `PerformanceModule.PresetEntry`, `PerformancePage.Instance(.DrawDeveloper)` | settings pages hand-built in Core |
| Core/UI/ToolWindow.cs:4,24-26,147-155,175-183 | `CleanHUD.GlyphFont`, `AccentTheme`, `DialogueWidgets.Gold`, `Assets.Tex/VGradient`, `CleanHudModule.Running/MenuEnabled`, `Menu.MenuBackground` | settings window theming |
| Core/ReplacementGuard.cs:10,35-36,113-116 | same CleanHUD theme types | title notice |
| Core/SaveWarning.cs:3,26,30 | `CleanHUD.Hud.SaveWarningAnchor`, `GlyphFont.DrawGame` | autosave countdown |
| Core/SaveRuntime.cs:113 | `CleanHUD.GlyphFont.DrawGame` | "SAVING" overlay |
| Core/SaveSystem.cs:121 | `CleanHUD.MessageHook.Push` | save notices |
| Core/SaveFiles.cs:183-190 | hard-coded module sidecar paths (NPC, Patrols, POI, CleanHUD, Quests, legacy Apocapatrol/NPCAI/Apocaraider/Apocaunloader/Apocapocket/ApocalypterQuests_saves/ImmersiveMap) | save participants |
| Core/SaveSystem.cs:264-265 | Towns ES3 key `WildWasteland.Towns.World.v1` + `config/WildWasteland/Towns/Saves` | snapshot publication |
| Core/TunedSettings.cs:20,44 | resources named `CleanHUD.DefaultSettings.*`, `CleanHUD.DefaultProfiles.<owner>.*` | defaults live in CleanHUD's resource namespace |
| Core/SaveTypes.cs:31-36 | namespace strings of Patrols/PlayerBody/NPC/Guns/HUD | ES3 aliasing (must stay; data, not types) |

### 2.2 Modules -> Core internals (not Services) that need a ModAPI home
| Core member | used by | note |
|---|---|---|
| `QuietFsm.On` (internal static) | Patrols PatrolsSettings.cs:120-121 | module drives a Core fix |
| `SaveBus.CleanupSettings` (internal event, ToolLayout) | Patrols PatrolsModule.cs:63/143 | Patrols draws into Saves' SYSTEM page |
| `LoadingProgress.Register/Remove/Work` | Patrols, Towns; `Begin/ScreenVisible/Read` CleanHUD | |
| `WorldWork.Try` | Patrols (2), Towns (3) | |
| `VistasWorldGen.*` | Towns (6 files), CleanHUD, Patrols | Compatibility -> ModAPI |
| `InventorySupplies.Count/Drop/Keys/Ready` | CleanHUD InventoryStyle(.Drops) | |
| `SaveSystem.Busy/RecoveryError/Info/Installed/AutomaticSaving`, `SaveRuntime.Latest/IsSaveFile/PlayerDistance`, `SinglePlayerSaving.Load/Serializing` | Towns, CleanHUD Menu/LoadingScreen/ApocasaverLoading, Patrols, NPC Senses | Saves ModAPI |
| `ReplacementGuard.MenuModalOpen` | CleanHUD Menu.cs:453,892,1025,1044 | |
| `TunedSettings.InstallProfiles` | CleanHUD, PlayerBody | |
| `DiskResources.Names/Open` | Towns, Survival, NPC (4 files) | |
| `AtomicFile.Replace` | Towns, CleanHUD | |
| `OptionalAccess` | Quests, QuestsCompat, CleanHUD VistasEngineHud | |
| `VoiceAudio.LoadWav` | PlayerBody FemalePain | |
| `Legacy.*` | Towns, Survival, Guns, NPC | |
| `ModuleHost.DeferredInit` | Quests | |
| `Hooks.FsmEnable` | Towns, Weather, Patrols, Performance, Core | see 5.1 bug |

### 2.3 Performance -> other modules
| file:line | used | why | violation? |
|---|---|---|---|
| Content/PatrolsBridge.cs:31-38,53-56 | reflection: `WildWasteland.Modules.Patrols.WarRig.PoiEncounter.Protected/DetachTankers/CleanupChecks`, `PatrolsModule.Running` | CT1 culler must honour War Rig protection | YES (indirect type reference by name) |
| World/CarLod.cs:193-194 | namespace prefix `WildWasteland.Modules.Patrols` on components | skip Patrols-driven cars | YES (implicit) |
| World/CarLod.cs:89, 194, 200 | `EngineSim.EngineManager` type/namespace | skip Engine Sim cars | third-party -> Compatibility query |
| PerformanceModule.cs:22,37,81,108 | provides `Services.WeatherVisualPolicy` | Weather visual detail from preset | OK |
| Rendering/RenderShared.cs:347-353 | `SaveBus.StateChanged` | SaveGuard reverts saved-object edits | OK (should become Saves event) |
| comments only | Guns/CleanHUD/PlayerBody/Patrols behaviours documented (HudFsms, AnimatorCulling, LightPolicy, SunShadows) | coexistence assumptions | note for testing |

Performance is referenced from Core only (CorePages.cs:30,56,369; Plugin.cs:140); no gameplay module references Performance types.

### 2.4 Services members this area provides
Core declares every Services member (0.3). Implementations provided by this area: `Services.WeatherVisualPolicy = PerformanceModule` (`WeatherVisualDetail WeatherDetail { get; }`), `Services.ReportCombat()` (internal, Core->SaveCombat). Consumed by Core: `Services.VehicleCombatActive` (SaveCombat.cs:76), `Services.VehicleIgnition.TryStop(GameObject)` (VehicleLoadPlacement.cs:82), `Services.Weather` (VistasWorldGen.cs:133-135, CorePages.cs:116-119).

### 2.5 developer/ -> WW
`WildWasteland.Plugin.EnableDebug/DeveloperMode/Main/Settings/Tools`, `Core.Scheduler.AllJobs()` + Job fields, `ModuleHost.Entries` (State/Errors/LastError), `GameRefs.Player/SaveLoadState`, `EditorSession` (+ internal `Acquire(bool,bool)` via reflection), `ToolWindow.Open`, `IModule.ToolPages()`. Separate Harmony id `com.denis.apocalypter.wildwastelanddeveloper.timing`.

---

## 3. Harmony patches & scheduler jobs

### 3.1 Core patches
| file:line | target | kind | via | per-call cost / filter |
|---|---|---|---|---|
| Hooks.cs:329-363 | 28 dispatcher targets (0.2) | pre/post/finalizer | Hooks (Core GUID) | dispatcher: `Calls++` + array loop; postfix-only hooks never count `Calls` |
| SaveTypes.cs:38 | `ES3Reflection.GetType(string)` | prefix | Patches(Core) | dictionary cache; per type string |
| ITweenGuard.cs:23 | `iTween.Stop(GameObject,string)` | transpiler | Patches | n/a |
| ITweenGuard.cs:33 | `iTween.TweenUpdate` | prefix | Patches | `if (!___physics) return` then `GetComponent<Rigidbody>` per physics tween update |
| QuietFsm.cs:22 | `PlayMakerPrefs.get_LogPerformanceWarnings` | postfix | Patches | trivial |
| QuietFsm.cs:26 | `ActionHelpers.GetGameObjectFsm(GameObject,string)` | prefix (skip) | Patches | `On` bool, then `GetComponents` into buffer per FSM lookup |
| ApocasetterBridge.cs:50 | `Apocasetter.SettingsUI.Toggle` | prefix | Patches | menu toggle only |
| ApocasaverExclusion.cs:17-26 | Apocasaver `Plugin.Awake/ApplyPatches/EnsureRunner/OnSceneLoaded`, `Runner.Update/LateUpdate/OnGUI/OnDestroy`, `HandPose.Set/Get` | prefix (skip) | Patches | only when Apocasaver loaded |
| VistasWorldGen.cs:51 | `BepInEx.Logging.ManualLogSource.Log(LogLevel, object)` | prefix | Patches | **every log line of every plugin** (ReferenceEquals filter); only when Vistas loaded |
| VistasWorldGen.cs:54 | `MonoMod.Utils.ReflectionHelper._ResolveReflection` | prefix | Patches | every MonoMod member resolution (all Harmony patching) |
| VistasWorldGen.cs:64-74 | 9 obfuscated World Amp methods | transpiler/prefix/postfix | Patches | optional |
| SaveSystem.cs:69 | `ES3PlayMaker.ActionBase.HandleError` | prefix | Patches | on error |
| SaveSystem.cs:70 | `ES3File.Sync(ES3Settings)` | prefix | Patches | per sync |
| SaveSystem.cs:71 | `ES3IO.CommitBackup` | finalizer | Patches | per commit |
| SaveSystem.cs:72 | `ES3PlayMaker.PMDataWrapper.ApplyVariables` | postfix | Patches | per load wrapper |
| SaveCompatibility.cs:15-16 | `ES3PlayMaker.Save.Enter`, `ES3PlayMaker.Load.Enter` | prefix + finalizer | Patches | per save/load action: `fsm.Name != "saveItemVar"` string compare first |
| SaveTerrain.cs:15-16 | `MapMagicObject.Update`, `Den.Tools.Tasks.CoroutineManager.Update` | prefix | Patches | `_held` bool, every frame |
| WorldHistory.cs:38-40 | `TerrainTile.Move/Remove/ResetTerrain`, `ObjectsPool+Pool.ClampCount`, `Pool.MoveItems` | prefix / prefix+postfix | Patches | per tile streaming op; `GetComponentsInChildren<PlayMakerFSM>` + new List per root (see 6) |
| Core hook subscriptions | HealthStacks: GetButtonDownUpdate pre; ColorGrading: ActivateGameObjectDo pre; SaveSystem: ActivateGameObjectDo pre; VehicleLoadPlacement: WaitUpdate pre; SaveCombat: FsmEnable post, ProcessEvent pre, EnterState post; SaveRuntime: ProcessEvent pre; SinglePlayerSaving: MouseLookDo, GetButtonDownUpdate, GetButtonDo, GetButtonUpUpdate, GetMouseButtonDownUpdate pre | Hooks (owner null) | all length/bool gates; SaveCombat/SaveRuntime do 1-3 string compares of `ev.Name` per ProcessEvent while in a world |

### 3.2 Performance patches (Harmony id `<GUID>.Performance`, installed lazily by `EnsurePatched`, never removed until module off)
| file:line | target | kind | via | per-call cost / filter |
|---|---|---|---|---|
| PlayMaker/FsmRegistry.cs:131 | `Fsm.OnEnable` | prefix | Patches | `_users==0` |
| PlayMaker/FsmRegistry.cs:140 | `PlayMakerFSM.OnDisable` | prefix (skip, copy of original) | Patches | `!FastRemove && !tracked` |
| PlayMaker/FsmRegistry.cs:155 | `PlayMakerFSM.OnDestroy` | prefix (skip, copy) | Patches | same |
| PlayMaker/FsmRegistry.cs:49 | PlayMakerFSM.OnEnable | postfix | Hooks.FsmEnable | `_users>0` |
| PlayMaker/EventDispatch.cs:144 | `Fsm.SendEventToFsmOnGameObject(GO,string,FsmEvent)` | prefix (skip), Priority.Last, `__runOriginal` | Patches | `!On && !Shadow` |
| PlayMaker/EventDispatch.cs:188 | `Fsm.BroadcastEventToGameObject(GO,FsmEvent,FsmEventData,bool,bool)` | prefix (skip), Priority.Last | Patches | same; allocates 1 FsmEventData per receiver (vanilla semantics) |
| PlayMaker/EventDispatch.cs:254, 280 | `Fsm.BroadcastEvent(FsmEvent,bool)`, `PlayMakerFSM.BroadcastEvent(FsmEvent)` | prefix (skip) | Patches | `!On` |
| PlayMaker/GuiLoopFeature.cs:390-422 | `PlayMakerGUI.OnGUI` (skip), `PlayMakerFSM.OnDisable/OnDestroy` (pre), `Fsm.CreateSubFsm` (post), `PlayMakerOnGUI.Start` (post), `PlayMakerFSM.OnBecameVisible/OnBecameInvisible` (pre, skip when unhandled) | mixed | Patches + Hooks.FsmEnable | `On` |
| PlayMaker/TriggerProxyFeature.cs:57-127 | `Fsm.OnTriggerEnter/Stay/Exit`, `Fsm.OnCollisionEnter/Stay/Exit` | prefix (skip, exact copies) | Patches | `On`; per physics callback |
| PlayMaker/MousePickFeature.cs:142 | `ActionHelpers.DoMousePick(float,int)` | prefix (skip) | Patches | `On/Shadow`; target shared with PlayerBody PickAssist postfix (PickAssist.cs:29) |
| PlayMaker/StringConvertFeature.cs:74,100 | `ConvertFloatToString.DoConvertFloatToString`, `ConvertIntToString.DoConvertIntToString` | prefix (skip) | Patches | dictionary lookup per call |
| PlayMaker/FastPropertyFeature.cs:436-470 | `FsmProperty.SetValue`, `FsmProperty.GetValue` | prefix + postfix | Patches | `On/Shadow` |
| PlayMaker/FastCallMethodFeature.cs:119 | `CallMethod.DoMethodCall` | transpiler | Patches | replaces 2 `MethodBase.Invoke` |
| PlayMaker/FastActionLoadFeature.cs:229 | `ActionData.LoadActionField` | prefix (always runs ActionDataPort copy once installed) | Patches | per action field on every FSM instantiate (may run off main thread) |
| PlayMaker/FsmSleepFeature.cs:219-295 | `Fsm.Update` (pre+post), `Fsm.Event(FsmEventTarget,FsmEvent)`, `SwitchState`, `Start`, `OnEnable`, `Stop`, `Init`, `Reinitialize`, `Clear`, `OnDestroy`, `DelayedEvent` x2, `FsmStateAction.Finish`, 19 physics/animator/GUI callbacks, `PlayMakerFSM.OnBeforeSerialize` (pre), `PlayMakerFSM.Awake` (post) | prefix/postfix (~32 targets) | Patches + Hooks.ProcessEvent | `Any` bool; `Fsm.Update` postfix runs `CanSleep` per awake FSM per frame while On |
| PlayMaker/SigmaFsmStates.cs:15 | `PlayMakerFSM.get_FsmStates` | postfix | Patches | **always installed in Init**, `Chainloader.PluginInfos.ContainsKey` per getter call |
| Content/FsmEdits.cs:177 | `PlayMakerFSM.Awake` | postfix | Patches | `_count` + name dictionary per FSM awake |
| Content/ItemFsmFixes.cs:40 | `AddComponent.OnEnter` | prefix (skip) | Patches | length gate 21 |
| World/CarLod.cs:305 | `SetIsKinematic.OnEnter` | prefix | Patches | `On` + dictionary |
| World/WorldSettings.cs:99 | `Den.Tools.ThreadManager.TaskThreadAction` | prefix (worker thread) | Patches | per task |
| World/WorldSettings.cs:333, 388 | `AzureTimeController.FixedUpdate`, `AzureWeatherController.FixedUpdate` | prefix (skip) | Patches | per physics step |
| Rendering/PostFx.cs:49,51 | `PostProcessEffectSettings.IsEnabledAndSupported` (post), `SharpenRenderer.Render` (pre, skip) | Patches | per effect per frame |
| Rendering/RenderShared.cs:482 | `GraphicSettingHelperMethods.ChangeQualitySettings` | postfix | Patches | menu only |
| Hook subscriptions | ProcessEvent pre: Coyotes (`ReferenceEquals(fsm,_coyote)` first), FsmSleep (wake); EnterState post: PollSleep, CarLod; CreateObjectEnter post: LightPolicy + GeneratedLods (both `RenderScan.AfterCreateObject` -> subscribed twice); FsmEnable post: FsmRegistry, GuiLoop | Hooks | |

### 3.3 Shared targets / inconsistencies to fix in the new Core
- Inside Performance, several engine targets are patched by 2 features directly (same Harmony id, so `Patches` CONFLICT detection is blind): `PlayMakerFSM.OnDisable` + `OnDestroy` (FsmRegistry skip-prefix + GuiLoop prefix), `Fsm.OnEnable` (FsmRegistry + FsmSleep), `Fsm.OnTrigger*/OnCollision*` (TriggerProxy skip-copy + FsmSleep wake), `PlayMakerFSM.Awake` postfix (FsmEdits + FsmSleep). In Core these become `Hooks` dispatchers (FsmDisable, FsmDestroy, FsmInnerEnable, FsmPhysicsCallback, FsmAwake).
- `ActionHelpers.DoMousePick`: Performance prefix (skip) + PlayerBody postfix (PickAssist.cs:29); `ActionHelpers.GetGameObjectFsm`: Core QuietFsm. Make `ActionHelpers` patches Core-owned.
- `Fsm.ProcessEvent` dispatcher subscribers (NPC Brain, Dog, Guns Unloader/WeaponTransition, Coyotes, FsmSleep, SaveCombat, SaveRuntime) no longer see TRIGGER/COLLISION/BECAME VISIBLE events that PM5/PM6 skip (`FsmEventSkip`) - documented, must stay documented in the ModAPI.
- `LightPolicy` and `GeneratedLods` both subscribe `RenderScan.AfterCreateObject` (LightPolicy.cs:102, GeneratedLods.cs:72): the scan's postfix runs twice per CreateObject when both are on (enqueue is de-duplicated by `_queued`, but the call is doubled).
- Duplicate `PlanId`s: CT9 (2 features), EN2 (2), EN6 (2), EN7 (2).

### 3.4 Scheduler jobs
| name | kind | interval/budget | file |
|---|---|---|---|
| Core.Frame (EditorSession/SaveBus/Hotkeys/ToolWindow tick) | EveryFrame EarlyUpdate | - | Core/Plugin.cs:160 |
| Core.Replacement notice | Interval | 0.5 s | Core/ReplacementGuard.cs:50 |
| Core.Replacement notice | Gui (predicate) | - | Core/ReplacementGuard.cs:51 |
| Autosave combat discovery | Sliced | 500 us | Core/SaveCombat.cs:23 |
| Conservative save cleanup observations | Interval | 5 s | Core/SaveCleanup.cs:37 |
| Save cleanup observation slice | Sliced | 500 us | Core/SaveCleanup.cs:38 |
| Native saving fallback | Gui (predicate) | - | Core/SaveRuntime.cs:41 |
| Singleplayer autosave warning | Interval | 0.25 s | Core/SaveSystem.cs:79 |
| Autosave compass countdown | Gui (predicate) | - | Core/SaveWarning.cs:12 |
| Inventory supply prefabs | Sliced | 500 us | Core/InventorySupplies.cs:20 |
| (coroutines) SaveBeforeExit/Save/ReturnAndLoad | StartCoroutine | - | Core/SaveSystem.cs:415, SinglePlayerSaving.cs:42,96 |
| Performance.Frames | EveryFrame EarlyUpdate (always while module on) | - | Framework/Telemetry.cs:47 |
| Performance.PeriodicCensus | Interval | 60 s | Framework/Telemetry.cs:48 |
| Performance.Compile | EveryFrame LateUpdate | - | PlayMaker/CompileQueue.cs:36 |
| Performance.GuiProxyGuard | Interval | 1 s | PlayMaker/GuiLoopFeature.cs:83 |
| Performance.PollSleep | EveryFrame EarlyUpdate | - | Content/PollSleep.cs:51 |
| Performance.LightFlicker | EveryFrame | - | Content/LightFlicker.cs:50 |
| Performance.Culler | Sliced | 1000 us (64 entries / 8 changes per frame) | Content/Culler.cs:80 |
| Performance.DayTime | EveryFrame | - | Content/HudFsms.cs:46 |
| Performance.CarLod | Interval | 0.1 s | World/CarLod.cs:92 |
| Performance.CcdSweep | Sliced | 300 us | World/WorldSettings.cs:134 |
| Performance.OriginJump | Interval | 1 s | World/WorldSettings.cs:260 |
| Performance.RenderScan | Sliced | 1000 us | Rendering/RenderShared.cs:91 |
| Performance.QualityGuard | Interval | 1 s | Rendering/RenderShared.cs:420 |
| Performance.LodExport | Sliced (manual) | 500 us | Rendering/GeneratedLodExport.cs:37 |
| Performance.LightBudget | Interval | 0.25 s | Rendering/LightPolicy.cs:78 |
| Performance.LayerCull | Interval | 1 s | Rendering/CameraCulling.cs:40 |
| Performance.FarClip | Interval | 1 s | Rendering/CameraCulling.cs:134 |
| Performance.AntiAliasing | Interval | 1 s | Rendering/PostFx.cs:219 |
| Performance.SunShadows | Interval | 5 s | Rendering/SunShadows.cs:46 |
| Performance.GeneratedLods | Sliced LateUpdateLast | 750 us | Rendering/GeneratedLods.cs:88 |
| Performance.GeneratedLodDiscovery | Interval | 15 s | Rendering/GeneratedLods.cs:89 |
Naming: Core jobs are inconsistent ("Core.Frame" vs "Autosave combat discovery" vs "Core.Replacement notice" shown as `Core.Core.Replacement notice`). Background threads outside the scheduler: FS4 compiler thread (FastActionLoadFeature.cs:794), CompileQueue thread-pool batches (CompileQueue.cs:57).

---

## 4. Logging
| switch / source | where | used by | proposed level |
|---|---|---|---|
| `[Debug] VerboseLog` (settings.cfg) -> `Log.Verbose` | Plugin.cs:89-93 | every `Log.Info` (Core 14 sites, Performance 6) + guards | becomes Debug `VerboseLog` 0/1/2 |
| `[Debug] Developer mode`, `[General] Enable Debug` | Plugin.cs:83,90 | visibility only | Debug UI gates (not log) |
| `[Debug] Recommended mods` | ReplacementGuard.cs:40 | title notice | not logging; move to Core/HUD settings |
| `[Patrols / Debug] Hide missing-FSM warnings` -> `QuietFsm.On` | PatrolsSettings.cs:120 | QuietFsm prefix | Debug: hide at level 0/1, show raw Unity warnings only at level 2 |
| `PlayMakerPrefs.LogPerformanceWarnings` gated by `Log.Verbose` | QuietFsm.cs:29 | native PlayMaker | level 2 |
| Vistas own logger silenced unless Verbose (Info/Debug levels) | VistasWorldGen.cs:79-80 | World Amp | level 2 |
| `[Performance / Telemetry] Periodic census` | Telemetry.cs:41 | census file every 60 s | Debug measurement switch (not a log level) |
| developer: `[Logging] Periodic summaries`, `Method spikes`, `Lifecycle events`, `Summary interval seconds`, `Slow call milliseconds`; `[Capture] Thread allocation timing`; own `ManualLogSource` (Plugin.Log) | developer/Plugin.cs:21-36 | PERF/PERF-CALL/PERF-JOB lines, trace events | fold into Debug: summaries = level 1 perf report; spikes/lifecycle traces = level 2 |
| Own ManualLogSource in area | `Log.Source` (= WW plugin Logger) only; developer has its own | - | Debug owns the single source |
| `UnityEngine.Debug.LogError` | ActionDataPort.cs:358 | vanilla-identical message | keep (exact port) |

Call sites -> level mapping:
- Level 1 (main actions): `Log.Always` state changes - module started/stopped (ModuleHost.cs:108,130), startup summary (Plugin.cs:117), fixes ready (HealthStacks.cs:24, ColorGrading.cs:11, ITweenGuard.cs:24,34), ReplacementGuard decisions (78, 98), Vistas ready (75), save completed/failed/recovered/cleanup/notices (SaveSystem.cs:80,82,116,251; SaveFiles.cs:160; SaveCleanup.cs:162; SaveTerrain.cs:18,40; VehicleLoadPlacement.cs:74,115; ApocasaverExclusion.cs:30), settings opened/reset (ToolWindow.cs:54,77; CorePages.cs:66), Performance started/feature mode changes (PerformanceModule.cs:92,102,141), PM2 stand-down (EventDispatch.cs:37,135), FS4 compile result (FastActionLoadFeature.cs:769), RC9 export (GeneratedLodExport.cs:261), SigmaMod guard (SigmaFsmStates.cs:17,30), census written (Telemetry.cs:151).
- Level 2 (deep): SaveBus state transitions (SaveBus.cs:99), SaveTypes mappings (SaveTypes.cs:51), migrations (ModuleConfig.cs:70), iTween repairs (ITweenGuard.cs:68), HealthStacks merges (HealthStacks.cs:79), QuietFsm first hide (QuietFsm.cs:43), VoiceAudio gain (VoiceAudio.cs:43), InventorySupplies drops (115), Feature start (Feature.cs:82), FS1 copied flag (FsmSleepFeature.cs:304), PM5 helpers (GuiLoopFeature.cs:316,372), Skin font/centring (Skin.cs:69-80,229), audio voices unchanged (Plugin.cs:148), "another editor open" (ToolWindow.cs:73).
- Warn/Error stay always-on (rate-limited); shadow mismatches (Feature.Mismatch, first 5) = Warn.
- Note: `Log.Info` calls at ToolWindow.cs:73, Plugin.cs:148, ModuleConfig.cs:70, Feature.cs:82, Telemetry.cs:70 build strings without a `Log.Verbose` guard (not hot paths).

---

## 5. Redundancy & dead code

### 5.1 Bugs found while inventorying (fix during the refactor)
| evidence | issue |
|---|---|
| Core/Hooks.cs:235-243 | `RemoveAll` has no `FsmEnable.Remove(owner)`: FsmEnable postfixes of Towns (InteriorPopulation.cs:35), Weather (WeatherNative.cs:65), Patrols (NativeStormProtection.cs:41), Performance (FsmRegistry.cs:49, GuiLoopFeature.cs:72) survive module Shutdown and are added again on every off->on (duplicate handlers, handlers of stopped modules keep running). |
| Core/Scheduler.cs:152-159 vs CompileQueue.cs:36, GuiLoopFeature.cs:83 | `RemoveAll` removes jobs from the lists without setting `Job.Removed`; CompileQueue/GuiLoop re-register only when `_job.Removed` -> after Performance off->on the `Performance.Compile` pump and `Performance.GuiProxyGuard` are never registered again (FS2/FS3 stay on reflection forever; PM5 proxy guard missing). PollSleep worked around it with an `AllJobs` scan (PollSleep.cs:51,58). |
| Core/Log.cs:23-24 | every `Log.Error` (any module, any message) during a save transaction fails the save via `SaveSystem.Fail`; an unrelated rate-limited error can abort autosaves. Make it an explicit Saves error channel. |
| Core/Services.cs:81 | `ClearEvents()` never called: module-provided event handlers rely only on each module's own `-=`. |

### 5.2 Dead / unused
| evidence | item |
|---|---|
| Core/ModuleConfig.cs:117 | `Legacy.ParseFloat` - 0 call sites |
| Core/ModuleHost.cs:158 | `ModuleHost.Restart` - 0 call sites (documented API) |
| Core/SaveTypes.cs:20 | `SaveTypes.Resolved` - written, never read |
| Core/Origin.cs:17-18 | `Origin.Jumps`, `LastDelta` - never read |
| Core/UI/Skin.cs:159,170,187,234,250 | `PlankButton`, `Frame`, `PitBox`, `SlotBox`, `HazardStrip` - 0 call sites |
| Core/Hooks.cs:140-141 | stale doc comment (OriginJump text above FsmEnable) |
| Core/SaveTypes.cs:33-36 | aliases Apocaplayer/NPCAI/Gunplay/GunplayHUD "unused today" (docs/modules.md:114) - keep (cheap, protects old saves) |
| Modules/Performance/Content/PatrolsBridge.cs | whole file obsolete once a Culling ModAPI exists |
| Modules/Performance/PerformanceModule.cs:207 | `ToolPages()` returns null; page registered by Core |
| No `#if false` blocks in the area; no feature flagged obsolete/removed in code. FS4's old transpiler (0.1.26c) is already gone (FastActionLoadFeature.cs:756-757). |

### 5.3 Duplicated utilities / systems
| evidence | duplication | proposal |
|---|---|---|
| Core/NativeAccess.cs, Core/OptionalAccess.cs, Performance/PlayMaker/FastReflect.cs, BatchCompiler.cs, CompileQueue.cs; ApocasetterBridge.cs:41 (Expression.Compile); FastPropertyFeature/TriggerProxyFeature use `AccessTools.MethodDelegate` | 4 accessor generators (DMD single, Cecil batch, Expression) | one `Core/Access` (typed getter/setter/invoker/ctor, batch + background queue, budget) |
| Telemetry.cs:28-36 (1024 ring, spikes, GC, 6 ProfilerRecorders), developer/Measurements.cs:26-58 (8192 ring, p95/p99, GC0/1/2, 9 ProfilerRecorders), PlayerBody/Camera/Perf.cs (10 s windows, >33/>50 ms, GC, quality summary), Scheduler.cs:66-72 (WW FrameMs avg/max) | 4 frame-stat collectors, 2 identical ProfilerRecorder sets | one Debug FrameStats (see 10) |
| Telemetry `WriteCensus` (FsmList census) vs developer Dashboard "Counters" (static collection sizes) | two census tools | Debug census page |
| developer/Diagnosis.cs:136 `Json(object)` | another hand-written JSON writer | Core Json util |
| FsmSleepFeature `_asleep` set sets `Fsm.ManualUpdate` directly (FsmSleepFeature.cs:237) vs `FsmPause` bit ownership (FsmEdits.cs:267-) used by PollSleep, LightFlicker, HudFsms, CarLod | two ManualUpdate owners; FsmEdits.cs:180-182 and FsmSleepFeature.cs:295-306 both clear copied flags in `PlayMakerFSM.Awake` postfixes | one Core `FsmPause` service with owner bits (FS1 = one bit) |
| FS1 (FsmSleep) vs PollSleep (CT3/CT5/CT11) | both put idle FSMs to sleep (FS1: states with no OnUpdate; PollSleep: polling states evaluated by a job) | one "FSM sleep" service with two strategies |
| FsmRegistry (PM1 index) vs ad-hoc `PlayMakerFSM.FsmList` scans in Towns (TownGasStation, InteriorPopulation), Weather (WeatherNative), Quests (FactionDump, CrewHistory, QuestWorld), Patrols (TownTraffic), Core SaveCombat.cs:38 | repeated O(N) FSM scans | expose FsmRegistry as Core `FsmIndex` (by GameObject, by FSM name) |
| PerformanceModule.TileApplied (PerformanceModule.cs:83,196) + `TerrainTile.OnTileApplied/Finalized/Moved` subscriptions in Towns, POI, Quests, CleanHUD, Patrols TrafficRoads (+ VistasWorldGen.TerrainEdited) | 7 independent MapMagic tile subscriptions | Core `WorldTiles` events (Applied/Finalized/Moved/Edited) with try/catch per handler |
| RenderShared `SaveGuard` (SaveBus.StateChanged "wait"/"SaveGame") vs Saves own begin/end; CarLod.OnSaveBegin; FS1.OnSaveBegin | each reverts on its own trigger | Saves "BeforeSerialize/AfterSerialize" events in Core |
| RenderShared `GameCameras` (camera discovery) vs `GameRefs.MainCamera`, PlayerBody ThirdPerson camera discovery | camera lookups | Core `GameRefs.Cameras` |
| Culler (CT1) reimplements vanilla culling + Patrols War Rig protection via reflection; Patrols separately prefixes the vanilla GetDistance | two implementations of the same protection | Patrols provides `Culling` query; vanilla path and culler both call it |
| `LightPolicy` & `GeneratedLods` both subscribe `RenderScan.AfterCreateObject` | double dispatch | subscribe once in RenderScan.Register |
| Core VoiceAudio WAV decoder vs Quests `ReadWav` (per project plan doc) | WAV decoders | Core VoiceAudio |
| `TunedSettings` (embedded defaults) and `Legacy` (old cfg reader) both parse cfg text (TunedSettings.cs:23-29, ModuleConfig.cs:96-115) | two INI parsers | one Core cfg parser |
| Two Performance features with same idea at Core level: `ColorGrading` (Core) forces a native graphics option; Performance `QualityGuard` owns QualitySettings | graphics policy in two places | Core/Rendering policy owns both |

### 5.4 Legacy migrations / shims
| evidence | item | keep? |
|---|---|---|
| Plugin.cs:64-82 | first-run seeding of settings.cfg from main cfg; `[Keys] Tool window` -> `In-Game Settings Button` | keep (config compat) |
| ModuleConfig.Migrate / Legacy.FirstRun* | old standalone cfg migration | keep at least one release; candidates to retire later |
| SaveTypes aliases | ES3 type rename | must keep |
| SaveFiles.cs:185-186 legacy sidecar dirs (Apocapatrol/Saves, NPCAI/Saves, Apocaraider, Apocaunloader, Apocapocket, ApocalypterQuests_saves, ImmersiveMap/slots) + `LegacyQuestName` | old sidecars included in backups | keep (save compat) |
| SaveCompatibility (Apocasaver hinge keys `apocasaver.object.<id>.hinge-rotation`, `apocasaver.pose.<item>`) | Apocasaver save-format compat | keep |
| ApocasaverExclusion | shim that disables standalone Apocasaver | keep while Apocasaver can be installed |
| Performance preset stored values Low/Balanced/Max/Vanilla shown as PERFORMANCE/BALANCED/VISUALS/VANILLA | config compat | keep enum names |

---

## 6. Performance hot spots
| file:line | issue | fix |
|---|---|---|
| Core/Hooks.cs:77-106 | every dispatch does `Calls++` and loops handlers; postfix-only hooks never count calls; no per-handler timing, so hook time (the biggest WW cost per fps-regression-1.0.5 review) is invisible on the Performance page | per-handler `Stopwatch` behind a static `Debug.TraceHooks` bool; count calls on all kinds |
| Core/SaveCombat.cs:42-46, 48-52 | ProcessEvent prefix: up to 3 `ev.Name` string compares per FSM event while in a world; EnterState postfix: `fsm.Name != "Attack"` per state entry | compare `FsmEvent` references cached once (`FsmEvent.GetFsmEvent("Damage")`), length gate on names |
| Core/SaveRuntime.cs:84-86 | ProcessEvent prefix: `ev.Name != "Clicked"` per event | cache the `FsmEvent` reference |
| Core/WorldHistory.cs:90-101, 65-79 | per terrain tile move/pool move: `GetComponentsInChildren<PlayMakerFSM>(true)` + `new List` + nested state/action loop per root, plus string building for template keys (116-117) | cache per prefab which child paths have Save FSMs; reuse buffers |
| Core/GameRefs.cs:65-78 | while any of 7 refs is missing (main menu, scenes without itemNameID) up to 7 `GameObject.Find` per second | refresh only the refs asked for; per-ref retry timers |
| Core/Origin.cs:25-29 | `GameObject.Find("MapMagic")` each second while missing (menus) | reuse a GameRefs-style lookup only when InPlay |
| Core/ApocasetterBridge.cs:41,147-155 | `InputBlocked` read per frame by Hotkeys and by every input hook; delegate built with `Expression.Compile` (interpreted on this runtime per OptionalAccess.cs:8) | build with Core/Access (Cecil) |
| Core/VistasWorldGen.cs:51,79 | prefix on `ManualLogSource.Log` runs for every log line of every plugin | patch only Vistas' own logger's log call sites, or filter by `__instance` via a cheaper route (keep but document) |
| Core/SaveCleanup.cs:82 | `new List<GameObject>()` every 5 s; 129 `Resources.FindObjectsOfTypeAll<PlayMakerFSM>` per save (documented on-save) | reuse list; acceptable on save |
| Core/UI/ToolWindow.cs:108-118,189,240 | per OnGUI event while open: `Sorted()` allocs + lambda sort + closures | cache sorted list on Add/visibility change; paused game, low priority |
| Core/UI/CorePages.cs:475-494 | `Scheduler.AllJobs()` iterator + string concat per OnGUI event while Performance tab open | Debug page snapshots once per second (like developer Measurements) |
| Core/Scheduler.cs:195 | Interval reschedules `now + Interval` (phase drift, can lock to frame spikes) | `NextAt += Interval` with catch-up clamp (AGENTS 5.4 rule) |
| Performance/PlayMaker/FsmSleepFeature.cs:225-239 | `Fsm.Update` postfix on every FSM every frame: `CanSleep` (HashSet, 3 delegate getters, action loop) for each awake FSM while On; ~32 wake prefixes on hot Fsm methods (Event, SwitchState, Finish, physics callbacks) | measure with Debug hook timing; cache "cannot sleep" per (Fsm, ActiveState) until state change |
| Performance/PlayMaker/SigmaFsmStates.cs:15-22 | postfix on `PlayMakerFSM.FsmStates` getter always installed; dictionary lookup per call even without SigmaMod | install only when SigmaMod is loaded (Compatibility) |
| Performance/Rendering/LightPolicy.cs:186-195 | `FindObjectsOfType<Light>()` every 10 s during play (rule 6: no scene scans in play) | rely on RenderScan incremental (tile/CreateObject) + Saves "loaded" event; or slice the scan |
| Performance/Rendering/SunShadows.cs:81 | `FindObjectsOfType<Light>()` every 5 s while `RenderSettings.sun` is null | find once per scene via GameRefs |
| Performance/Rendering/RenderShared.cs:166-170 | each full pass calls `Resources.FindObjectsOfTypeAll<T>` for 5 types, each in one frame (the slice budget cannot split the native call) on scene load/world ready/feature start | acceptable at load; never trigger a full pass during play (OnSettingsChanged -> RequestFull, SmallCasters.cs:646) |
| Performance/Content/FsmEdits.cs:72 | `Resources.FindObjectsOfTypeAll<PlayMakerFSM>` when a watch is added mid-game | only at module start / scene load |
| Performance/World/WorldSettings.cs:296,304 | `FindObjectsOfType<Rigidbody>` per origin jump (already halves vanilla's two scans) | fine; document |
| Performance/Content/PatrolsBridge.cs:53-56 | `FieldInfo.GetValue` + `PropertyInfo.GetValue` (boxing) per culling pass | ModAPI query |
| Performance/Rendering/PostFx.cs:260-294 | FieldInfo Get/SetValue with boxing per new camera (1 s job) | Core/Access delegates |
| Performance/Framework/Telemetry.cs:83,96 | `Mark()` concatenates tag strings on tile/save/load/compile events; spike log formats a string per spike | tag bitmask; format only when drawn |
| Performance/Framework/Telemetry.cs:47 | `Performance.Frames` EveryFrame job runs whenever the module is on, even when nobody views stats | move to Debug, run only while Debug measurement is enabled |
| Performance/PlayMaker/EventDispatch.cs:207,216; 259-271 | `new FsmEventData` per receiver (vanilla-identical, kept for exactness) | keep (exactness); note |
| Performance/World/CarLod.cs:199 | `GetComponentsInChildren(_mbs)` once a second per car with Engine Sim installed | Compatibility query cached per car |

---

## 7. ModAPI / multiplayer events
| event / query | publisher -> consumers | payload (broadcastable) |
|---|---|---|
| `ModuleStateChanged` | Core ModuleHost | module id, old/new state, error text |
| `SceneLoaded`, `WorldReady`, `LoadBegin`, `SaveBegin`, `SaveEnd`, `SaveStateChanged` | Core SaveBus -> all | scene name/build index, slot file name, save generation, state names |
| `BeforeSerialize` / `AfterSerialize` (revert/re-apply visual edits) | Saves -> Core/Rendering SaveGuard, FS1, CarLod | slot, generation |
| `SaveFailed` / `ReportFailure` | any -> Saves | module id, message |
| `SidecarRegister(owner, filePattern)` (query/registration) | modules -> Saves (replaces SaveFiles.Participants hard-coding) | owner id, relative path template |
| `OriginShifted` | Core Origin -> all | delta (Vector3), total offset |
| `TileApplied` / `TileFinalized` / `TileMoved` / `TerrainEdited` | Core WorldTiles (+ Vistas) -> POI, Towns, Patrols, CleanHUD, Quests, Core/Rendering | tile coord (int x,z), world rect, draft/main flag |
| `LoadingProgress` source registration | modules -> Core -> HUD | owner id, fraction, detail text |
| `WorldWorkTry(owner)` (query) | Patrols/Towns/NPCAI -> Core | owner slot |
| `HotkeyPressed` | Core Hotkeys | key name, owner id (for MP input replay only if needed) |
| `PresetChanged` / `QualityPolicy` (query: WeatherVisualDetail, preset) | Core/Optimizations -> Weather, HUD | preset enum |
| `CullingProtected(GameObject)` / `BeforeFarDelete(GameObject)` (query) | Patrols -> Core culler | object network id |
| `ItemCulled` (deactivated/reactivated) / `ItemDestroyedByCleanup` | Core culler (CT1), SaveCleanup -> Loot, Saves, MP host | item id (ES3 ref id or `name(Clone)N`), world position (Origin.World), reason (far/fall/cleanup) - host-authoritative in MP |
| `VehicleSimulationFrozen/Unfrozen` | Core CarLod (CT2) -> Vehicles, Patrols, MP | vehicle id, world position, kinematic state |
| `VehicleOwnedByModule(GameObject)` (query) | Patrols/Vehicles/Compatibility(EngineSim) -> CarLod | vehicle id |
| `FsmWake(Fsm)` / `FsmPause` ownership API | Core -> any module editing FSM actions/delayed events | FSM owner id + FSM name (local only) |
| `FsmIndex` query (FSMs on GameObject / by name) | Core -> Towns, Weather, Quests, Patrols | local only |
| `CombatActivity` | Gunplay (shots), NPCAI (Detection/Attack), Patrols (vehicle combat) -> Saves autosave deferral | actor id, kind, world position |
| `PlaceEntered(name, kind)`, `RoadsChanged(revision, identity)` | Compatibility(Vistas) -> HUD, Towns, Patrols | strings, revision int; road polyline points in world coords |
| `SupplyDropped(kind, quantity)` | Core/Fixes InventorySupplies -> HUD, MP | item kind key, quantity, world position, new item ids |
| `PlayerPlacedAfterVehicleLoad` | Core/Fixes VehicleLoadPlacement -> Presentation, Vehicles | vehicle id, player world position |
| `ReplacedModsNotice` | Core ReplacementGuard -> HUD | list of mod names + status |
| `SettingsContribution` (rows for PLAYER/WORLD/HUD pages) | modules -> Core UI shell | entry key, label, choices |

Performance features changing simulation (CT1 culler destroy/deactivate, CT2 car freeze, RC7 CCD, EN6 time/sleep, RC6 animator culling) must run host-side only in multiplayer; exact PlayMaker fast paths are local.

---

## 8. Unmapped or ambiguous code
| code | options | recommendation |
|---|---|---|
| CT2 CarLod (World/CarLod.cs) | Core/Optimizations vs Vehicles | Core (vanilla car simulation LOD), with ModAPI query for module/third-party-driven cars |
| CT4 VehicleReflection | Core vs Vehicles | Core (exact FSM fast path) |
| CT7 HudFsms (player sheet texts, DayTime clock) | Core vs HUD vs Survival (day clock) | Core; publish `DayChanged` so HUD/Survival/Weather do not depend on the 0.2 s stepping |
| CT8 Coyotes | Core vs NPCAI (factions) | Core (it only trims a vanilla broadcast); NPCAI must know receivers differ |
| CT1 Culler | Core vs Loot vs Saves | Core/Optimizations; emits ItemCulled events for Loot/MP |
| CT9 DuplicateRigidbody | Core/Fixes vs Core/Optimizations | Core/Fixes (vanilla error fix) |
| RC9 GeneratedLodExport | Core/Rendering vs Tools | Tools (authoring) |
| SigmaFsmStates | Core vs Compatibility | Compatibility (SigmaMod) |
| Telemetry, PerformancePage measurements, CorePages.Performance | Core vs Debug | Debug |
| InventorySupplies | Core/Fixes vs HUD vs Loot | Core/Fixes per brief; it is really a gameplay feature (supply drop) used by HUD - consider HUD-owned UI + Core query |
| VehicleLoadPlacement | Core/Fixes vs Saves vs Vehicles | Core/Fixes per brief; driven by Saves load events |
| ColorGrading | Core/Fixes vs Core/Rendering policy | Core/Rendering policy (it is a forced graphics option, not a crash fix) |
| QuietFsm | Core/Fixes vs Debug | Core/Fixes; its switch belongs to Debug level |
| ReplacementGuard notice / LoadingProgress / SaveWarning / saving overlay | Core vs HUD | model in Core/Saves, drawing in HUD |
| SaveLibraryPage | Saves vs HUD | Saves (page in Core shell) |
| TunedSettings default resources named `CleanHUD.*` | Core vs HUD | Core-owned resource names, alias old |
| Plugin.ApplyAudioVoices + Patrols `AudioVoices` setting | Core vs Patrols | Core audio setting |
| PlayerBody/Camera/Perf.cs (outside this area) | PlayerBody vs Debug | Debug (delete from PlayerBody) |
| CorePages PLAYER/WORLD/HUD content | Core vs modules | each module contributes rows via ModAPI; Core only lays out |
| AnimatorCulling vs Presentation forcing AlwaysAnimate | Core/Rendering vs Presentation | Core/Rendering prefab policy; Presentation owns live animators (documented coexistence) |
| `Services` ladder raw fields (PlayerLadder, LadderClimbers, ...) | Core vs Presentation/NPCAI | typed query/event in ModAPI |
| developer/ profiler | separate DLL vs Debug | Debug module (in WW.dll), opt-in deep tracing |

---

## 9. Save / config compatibility constraints
**Saves (ES3 / files) owned by this area**
- ES3 keys written: `WildWasteland.SaveCommit` (SaveSystem.cs:17), `WildWasteland.CleanupProtected` (string[], SaveCleanup.cs:28), `apocasaver.object.<es3RefId>.hinge-rotation` (Vector3, SaveCompatibility.cs:25), `apocasaver.pose.<itemName>` (string, SaveCompatibility.cs:44-59). Keys read/validated: `global`, `Player`, `newGOArray_Cars`, `newGOArray_Items`, `newGOArray_Sandstorms` (SaveSystem.cs:219), `WildWasteland.Towns.World.v1` (264), `PlayerInCar` (VehicleLoadPlacement.cs:48).
- Save file identities: `SaveGame1..10.es3`, `WW_live-<32hex>.es3`, `WW_save-<32hex>.es3`, `WW_auto-<32hex>.es3` (SaveRuntime.cs:15); metadata `<file>.wwsave` (text "WW-SAVE-1", base64 name, kind manual/auto/legacy, date, lineage, km, base64 world, "sp"), thumbnail `<file>.wwsave.png`; backups `<ES3 root>/WildWasteland-Saves/Backups/<slot>/<UTC stamp>-<guid>/` with `operation.txt`, `undo.bin` (binary "WW-SAVE-1" journal), `files.txt`, `old-N.bin`, `new-N.bin`, markers `committed`/`recovered`; path tokens `world/`, `config/`, `assets/` inside journals; transient `ww-location-history.es3` (cache only). NPC senses companion header `v1 <slot>` rewritten on snapshot copy (SaveSystem.cs:279-284).
- Participant sidecars that must keep their names: `config/WildWasteland/NPC/Saves/<file>.senses.txt` (+ `plugins/WildWasteland/NPC/Saves`), `Patrols/Saves/<file>.bin(.bak)`, `POI/Saves/<file>.poi.txt`, `CleanHUD/Saves/<file>.fog.bin`, `<file>.locations.txt`, `Quests/Saves/<file>.crew.txt`, any `config/WildWasteland/**` file matching the slot, legacy dirs listed in 5.4, `<stem>_seed*`, `<file>.bakN`.
- ES3 type aliases (SaveTypes.cs:31-36) must stay. **Any namespace rename in the refactor needs a new alias from `"WildWasteland"` assembly + old `WildWasteland.Modules.<Old>` prefix** (saves made by WW 1.x store e.g. `WildWasteland.Modules.Patrols.Crew, WildWasteland`); today's Lookup only maps when the old assembly is NOT loaded, so a same-assembly alias needs a different check (old namespace prefix inside WildWasteland). ES3 prefab ids resolved through `Hooks.GetPrefab` (735710202610050001 motorcycle, 735710202610090001 War Rig, 7392481500621300201 stew pot) must stay.
- Performance edits nothing saved: SaveGuard reverts Light/MeshRenderer/ParticleSystem/Camera edits on saved objects before serialization (RenderShared.cs:276-310); CT2 wakes NWH before save. Keep this contract when moving to Core.

**Config**
- Main cfg `BepInEx/config/com.denis.apocalypter.wildwasteland.cfg`: `[General] Apocasetter`, `[General] Enable Debug`, `[Keys] In-Game Settings Button` (migrated from `[Keys] Tool window`).
- `config/WildWasteland/settings.cfg`: `[<ModuleId>] Enabled` (incl. `[Performance] Enabled`), `[Debug] VerboseLog`, `[Debug] Developer mode`, `[Debug] Recommended mods`, `[System / Saving]` Autosave / Exit save / Autosave minutes / Warning seconds / Autosave while driving / Autosaves to keep / Backups to keep / Save screenshots, `[System / Cleanup]` Cleanup on save / Non-resource zombie remains / Exhausted disposable packs / Minimum distance metres / Abandoned minutes, `[Keys] *` (module hotkeys).
- `config/WildWasteland/tools.cfg`: `[Core] Frame budget (ms)`; `[Performance / Preset] Preset` (enum names Vanilla/Low/Balanced/Max); `[Performance / PlayMaker|Game logic|Rendering|World] <feature Key>` (FeatureMode Off/On/Shadow; keys = `Feature.Key` strings, e.g. "Targeted events", "FSM sleep", "Culler", "Light shadows", "Car LOD"...); `[Performance / Tuning] "<feature Key> / <knob>"` (-1 = follow preset; ~45 knobs listed in feature Bind methods); `[Performance / Telemetry] Periodic census`; `[Patrols / Debug] Hide missing-FSM warnings` drives Core QuietFsm.
- Embedded default resources `CleanHUD.DefaultSettings.{main.cfg,settings.cfg,tools.cfg}.txt` and `CleanHUD.DefaultProfiles.<owner>.*` (TunedSettings) - renaming needs a fallback.
- Section prefix = module Id: moving Performance into Core changes nothing on disk only if the `Performance / ...` section names are kept (or migrated with `cfg.Migrate`). Merging modules (e.g. CleanHUD+HUD) must migrate `[<Id>] Enabled` and `<Id> / ...` sections; CorePages' RESET keeps every module master except `Performance`/`POI`, matched by Id string (CorePages.cs:44).
- Performance data files: `config/WildWasteland/Performance/census-*.txt`; `plugins/WildWasteland/Performance/LOD/catalog.tsv` + `*.wwlod` (format "WWLOD\t1"), `plugins/WildWasteland/Performance/LODInput/` (export).
- developer: `BepInEx/config/com.denis.apocalypter.wildwastelanddeveloper.cfg` (`[Menu] Key`, `[Logging]`, `[Capture]`), captures under `BepInEx/config/WildWastelandDeveloper/Captures/<id>/`.

---

## 10. Recommended Debug perf tracing (from developer/ + Telemetry + Scheduler)
| capability | today | Debug module |
|---|---|---|
| WW job cost | Scheduler per-job Stopwatch (Calls/TotalMs/LastMs/MaxMs/Exceptions), F3 table (CorePages.Performance), developer per-second deltas + per-system ms/s | keep counters in Core `Job`; Debug derives ms/s per job/module once per second (developer Measurements.Snapshot logic) |
| Harmony hook cost | invisible (fps-regression doc: PoiNav cost inside hooks not on F7 page); developer can probe only by installing method hooks | Core `Hooks` per-handler timing (owner module + target) behind `Debug.TraceHooks`; `Hook.Calls` for all kinds; module patch cost via opt-in probes |
| Method-level attribution | developer Instrumentation (boundary methods by name heuristic, deep mode, base-game scopes, self/inclusive, main/worker, alloc) | Debug level 2 "deep trace", installed through `Core.Patches(owner=Debug)`, sliced install job, removed on level change |
| Frame pacing | Telemetry 1024 ring + spike tags; developer 8192 ring + p95/p99; PlayerBody Perf 10 s windows | one ring (8192) with p50/p95/p99, spikes tagged by Core lifecycle events (tile, save, load, origin, compile, scene) via a bitmask |
| Engine counters | two ProfilerRecorder sets (Telemetry 6, developer 9) | one set, started only while Debug measurement is on |
| GC / heap | Telemetry gen0 count/min + mono heap; developer gen0/1/2 + total memory; PlayerBody Perf GC | one GC panel |
| Feature counters (SendGo, Broadcasts, Sleeps, Wakes, PickQueries...) + per-feature `DrawStats`, shadow mismatches | Telemetry statics + PerformancePage.DrawDeveloper | Core optimizations expose `IStatsSource`; Debug lists them |
| Census | Telemetry FSM census file; developer static collection counts | Debug census page (FSM census + registered collection counters) |
| Capture/compare | developer 30 s capture A/B, JSON/CSV/MD export, settings diff | Debug "Capture A/B" (export under `config/WildWasteland/Debug/Captures`) |
| Logging | VerboseLog + many module switches; developer own log source/switches | single VerboseLog 0/1/2; perf summaries at level 1, spike/lifecycle traces at level 2 |
