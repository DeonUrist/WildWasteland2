# Inventory: Quests, QuestsCompat, EngineSimCompat (+ section 10: repo-wide global sweep)

Area: `src/Modules/Quests/**` (23 .cs, 9,229 lines; `Embedded/` is empty, data comes from `asset-index.tsv` via `Core.DiskResources`), `src/Modules/QuestsCompat/**` (3 .cs, 134 lines), `src/Modules/EngineSimCompat/**` (2 .cs, 390 lines). Snapshot 2026-10-22.

Headline facts
- Quests is the native port of Apocalypter Quests 1.8.2. It is one large `partial class Plugin` (Plugin.cs, PluginRuntime.cs, NativeConversation.cs), with static helpers `Game`, `Hire`, `Takeover`, `Voice`, `Factions` and `QuestWorld`. It mixes six target modules: Quests, NPCAI, Presentation, HUD, Compatibility and Core.
- QuestsCompat is compiled (csproj `src\**\*.cs`) but never registered (`Core/Plugin.cs:126-140` has no `QuestsCompatModule`; `docs/modules.md:26` lists it as `dropped`). All 3 files are dead. One string dependency remains: `Quests/CrewHistory.cs:49` reads the old `config/WildWasteland/QuestsCompat/Saves/<slot>.crew.txt`.
- EngineSimCompat is a clean, self-contained third-party bridge. It belongs in Compatibility. It provides `Services.VehicleIgnition`, and that contract stays in Core ModAPI.

## 1. File map

| path (relative to src/) | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/Quests/QuestsModule.cs | 86 | ModuleBase entry: binds config, 10 scheduler jobs, deferred engine start, lifecycle, F7 ConfigPage | Quests | MOVE. Fold the jobs that are double gated (see 6). |
| Modules/Quests/PluginRuntime.cs | 156 | `Plugin : IQuestApi`, Start/Stop, Hook and hotkey subscriptions, SaveBus state machine, resume snapshot, Death hook | Quests | MOVE. Replace the string-state machine (`StateChanged` "LoadGame"/"isPlay"/"wait"/"SaveGame") with `OnLoadBegin/OnWorldReady/OnSaveBegin` and a Core SaveBus key API. |
| Modules/Quests/Plugin.cs | 2501 | Quest engine: settings apply, frame, progress, tracker text, aim/directions, rewards, delivery, reputation, kills, talk buttons, rep shop, offers, accept/turn-in, ES3 save, IMGUI HUD | Quests, HUD, Core, Debug | SPLIT. Quests keeps `Frame/Tick/CheckEmptyCamp/ScoutCamp` (140-245, 545-562), rewards/fail/cancel (567-665), delivery (672-770), reputation (770-845), kills (861-935), `Format`, talk menu (`OnTalkOpen..DealRep`, 1022-1265), offers/accept/turn-in (1265-1960), talk actions (1960-2032), `RefreshLines/Objective/ShortStatus/DirectionsLine/Aim/ComputeAim` as view-model text (279-545). HUD gets `EnsureStyles/DrawWindow/Shadowed/Draw`, toasts and `GamePaused` (2410-2499). Core is REWRITTEN as a SaveBus ES3-key/sidecar API replacing `ResolveEs3, StaticMember, InstMember, DefaultEs3Path, CachedSlotFiles, SaveToCache, LoadFromCache, DiskSettings, SaveToDisk, LoadFromDisk, SlotId, BackupPath` (2072-2290). Debug/Tools gets `RecordNpcName` (979-1020). DELETE `FindType` (125), `IsLoadingState` (2033) and the `PluginGuid/PluginName/PluginVersion` consts (19-21). |
| Modules/Quests/NativeConversation.cs | 168 | Native talk session (TalkBridge part), aim-based interaction finder, fallback IMGUI dialogue, Talk Open/Close hooks | Quests, HUD, Compatibility | SPLIT. Quests keeps `TalkBridge.Begin/AddLine/NativeButtons/Choose/Close` and `Plugin.CanInteract/OpenInteraction/InteractionFrame/FindInteraction/Person/FilterCrewButtons`. HUD gets `Plugin.DrawConversation` (the fallback dialogue UI). Compatibility gets `TalkBridge.InstallOptional/Opened/Closed/Uninstall` (the Talk patches). |
| Modules/Quests/TalkHook.cs | 349 | Reflection bridge to Apocatalker: NPC/UI fields, cloning choice buttons, window skin (PNG), TalkApi flags/action event | Compatibility | SPLIT. `TalkBtn` and the `IQuestConversation` members stay in Quests. `TalkBridge.Init/TalkingNow/Bind/Ui/PromptContent/NpcName/Npc/Say/You/WindowSkin/TrySkin*/TalkConfigString/ParseBorder/Signature/NeedsSync/SyncButtons/ClearButtons` and `TalkApiLink` go to Compatibility (Talk bridge), which talks to Quests through a Core dialogue service. |
| Modules/Quests/Quests.cs | 1120 | Model and content: `QType`, `Quest`, `Util`, `QuestBook` (AQ1 serializer), `QuestFile` (quests.txt), `Phrases`, `QuestGen` | Quests | MOVE. Split into one file per class. `Util.Norm/Esc/Unesc/Compass` could become Core text utils. |
| Modules/Quests/Game.cs | 823 | World helpers: POIs, NPC lists, item find/hand-in, money var, game clock, spawn+register items, enemy flag | Core, NPCAI, POI, Loot, Quests | SPLIT. Core gets `Origin/Rel/Abs/Player/HDist/BaseName/FindInt/FindFloat/FindString/GameHours/MoneyVar/GetMoney/AddMoney/RegisterSpawned/SavedItemList/GroundAt` (shared game access). NPCAI ModAPI gets `FindNpcNames/FindEnemies/LiveNames/FindLive/FindPeople/FindBodies/SetPlayerEnemy/IsPlayerEnemy` (population queries). POI ModAPI gets `FindPois/SpawnFactionOf/VisitedIcons`. Loot or a Core item query gets `FindItems/TakeItems/CountStacks/ContainerCapacity/TooEmpty/ForSale/MatchUncached/Eq/AmmoFsm/AddReserveAmmo`. Quests keeps `WatchSpawns/SpawnItemsInFront/GroundNear/FindPrefab` (delivery goods), `ItemHit/Poi/EnemyInfo` DTOs and `ResetWorld`. |
| Modules/Quests/QueryCache.cs | 48 | Memo caches: item token match, ranged-NPC check, nearest-by-name throttle | Loot/Core, NPCAI | SPLIT. `Game.Matches` goes with the item query. `Hire.IsRangedNpc/NearestByName` go with the follower AI (NPCAI). |
| Modules/Quests/Hire.cs | 1792 | Hired follower: contract, follow/catch-up, threat picking, foot/car shooting, reload sounds, recoil, passenger seat | Quests, NPCAI, Patrols/Vehicles, Presentation, Gunplay | SPLIT. Quests keeps the contract: `Init, CostFor, Start, Attach, Dismiss, dismissHint, Clear, Reset, Died, Gone, Shutdown` and the expiry part of `Update` (351-387). NPCAI gets follower behaviour via a new ModAPI `INpcApi.Follow/Escort`: `WakeBrain, Calm, ReleaseNpc, RestoreBrain, Threats, Inform, Hostile, IsCritter, Alive, ExcludeFromIdle, Follow, CatchUp, FindExit, LiftAbove, RescueIfLost, TeleportNear, FootWatch, FootShoot, FootMagazine, EndFootShot, ClearShot, RealShotFired, WatchPlayerHealth, NearestUncached`. Vehicles/Patrols crew-seat service gets `TryMount, Dismount, SeatTaken, PlayerCar, AttachGuard, DetachGuard, SnapCombat, RestoreCombat, ResetFight, IgnoreNewColliders, RestoreCollisions, LateUpdate` (seat lock). Presentation gets `ScanParts, Recoil, PlayShotAnim, TurnToTarget, ArmTemplate, ShootAnimName`. Gunplay gets `FindReloadClips, ReadGunStats, BorrowShotSound, ScheduleReloadSounds, InsertClip, PlayDueSounds, CountShot, FireShots, GunSetup, HoldFire`. DELETE `EarlyTeach` (1553, empty), `LogGuard` (1003, empty), `FindApi` (688, unused), `NpcAiGuid/PatrolGuid` (19-20, unused) and the orphan comments at 1555-1560. |
| Modules/Quests/QuestFollower.cs | 121 | Drives the follower's humanoid body through `Services.Animation` (weapon pose, aim pitch, reload, suspend while melee/seated) | NPCAI (calls Presentation API) | MOVE to the NPCAI follower feature. It already uses only ModAPI (`IAnimationApi`, `INpcApi.SetFollowerDriven`, `IFollowerVehicles.Animated`). |
| Modules/Quests/Takeover.cs | 748 | Camp takeover: walker spawn and raycast steering, garrisons, quest-giver respawn, suppression of other factions' spawners | Quests, NPCAI, POI | SPLIT. Quests keeps `PickCamp, CountDefenders, Fill, Begin, Stamps, DescribeWalkers` and `GiverTick` (quest-giver policy, but spawning through the NPCAI API). NPCAI gets `SpawnGroup, SpawnAround, PlaceNear, RandomSpotNear, Usable, Fighters, FindPrefab/ScanForPrefab, MoveFsm, SolidMask, Blocked, Steer, RoomAt, Update` (walker loop), `ResolvePending/ActiveWalks/RestoreWalks/ClearWalks`. Steering should be replaced by the existing `INpcApi.Travel`. POI (camp ownership) gets `NeutralizeTick, Suppress, TryGarrison`, with `Factions.Overrides` as an ownership store shared with Towns/Patrols. |
| Modules/Quests/SavedWalk.cs | 13 | DTO for saved takeover walkers (name, target, camp key) | Quests/NPCAI | MERGE-INTO Takeover (or the NPCAI walker store). The format lives inside AQ1 `W|` lines. |
| Modules/Quests/Factions.cs | 252 | `Faction` model, factions.txt parser (hot reload by mtime), relations, camp ownership overrides | Quests, Core ModAPI | SPLIT. The file format and `Get/ByKey/Refresh/Load` stay in Quests. `OfObject/Friendly/Hostile/HumanFactions` become a Core faction query (NPCAI, Patrols and Towns also reason about Coyotes/Scrapyard). DELETE `Faction.HasNpc` (31, unused). |
| Modules/Quests/FactionDump.cs | 128 | Hotkey dump of nearby NPCs' components, FSMs and animators to faction_dump.txt | Debug | MOVE to Debug (or DELETE). It writes into the plugin folder (`PluginRuntime.cs:77`, `DataDir` = `plugins/WildWasteland/Quests`). |
| Modules/Quests/MapLink.cs | 63 | Quest/turn-in flags on compass and map via `Services.Compass/Map`, stable per-quest colours | Quests | MOVE (already ModAPI-only). |
| Modules/Quests/CrewHistory.cs | 67 | Remembers former raider crew names (cannot give work); per-slot `.crew.txt` sidecar | Quests | MOVE. Switch to the Core sidecar API. Keep the QuestsCompat fallback read for one release, then retire it. |
| Modules/Quests/Voice.cs | 198 | Authored-quest voice lines: folder lookup, own WAV decoder, own AudioSource. Playback is hard-disabled (`PlaybackEnabled = false`, :16) | Quests (+Core audio) | REWRITE. Use `Core.VoiceAudio.LoadWav` and delete `ReadWav` (125-183). While disabled, `Say` is dead code. |
| Modules/Quests/QuestConfig.cs | 203 | Binds 86 entries: `EngineSettings`, `HireSettings`, `VoiceSettings` | Quests (Hire tuning later NPCAI/Presentation) | MOVE. Stop binding the unused `apocasetter` entry (:72, `[Quests / Legacy] Apocasetter`) but never purge the key. |
| Modules/Quests/QuestConfigMigration.cs | 77 | Per-missing-destination import from `local.apocalypterquests.cfg`, Player/Tool routing, KeyCode->Key | Core config | MERGE-INTO Core `ModuleConfig.MigrateMissing(entry, file, section, key)`. Survival, CleanHUD and NPC Women use the same "each absent entry" pattern. |
| Modules/Quests/QuestContent.cs | 103 | Imports legacy quests/factions/voices (incl. Apocasetter disabled cache), installs defaults from asset index | Quests | MOVE. The legacy-folder discovery (`LegacyRoots`) could become a Core asset-migration helper. |
| Modules/Quests/QuestWorld.cs | 130 | Sliced walker over all scene roots and applied tiles: every GameObject, POIs, map icons, FSM lists (people/items), per-GO FSM cache, RaycastAll wrapper | Core | MERGE-INTO a Core WorldIndex/SceneCache, shared with CleanHUD `SceneCache`, `EngineDiscovery`, Performance `FsmRegistry` and Patrols `Prefabs` (see 10d). |
| Modules/Quests/KeyMap.cs | 74 | Key<->KeyCode tables. Only `FromKeyCodeName` is used (QuestConfigMigration.cs:57) | Core | MERGE-INTO a Core KeyMap. It is a byte-for-byte copy of `PlayerBody/KeyMap.cs` apart from comments. `Code/Build/Convert` are dead here. |
| Modules/Quests/QuestLogger.cs | 9 | `LogInfo` (Verbose-gated), `LogWarning`, `LogError` with the `[Quests]` prefix | Debug | DELETE. Replace with the Core leveled log (see 4). |
| Modules/QuestsCompat/QuestsCompatModule.cs | 75 | Retired module: scheduled Talk Update/LateUpdate, camera, pause session | none | DELETE. It is never registered (Core/Plugin.cs:126-140), the docs mark it dropped (docs/modules.md:26), and native Quests plus CleanHUD replace it. |
| Modules/QuestsCompat/PluginLifetime.cs | 39 | Transpiler replacing `UnityEngine.Object ==/!=` for `BaseUnityPlugin` in TalkApi.LoadFile/Reload | none | DELETE. Its only caller is QuestsCompatModule.cs:39. |
| Modules/QuestsCompat/Binding.cs | 20 | `OptionalAccess` getter/caller helpers for Talk | none | DELETE. Its only caller is QuestsCompatModule.cs. |
| Modules/EngineSimCompat/EngineSimCompatModule.cs | 295 | Engine Sim 1.7 bridge: runs manager Update/FixedUpdate from WW jobs, cached scan/listener/camera, keyboard ignition, AI start requests, `IVehicleIgnition` provider | Compatibility (contract in Core ModAPI) | SPLIT. The bridge moves to Compatibility. `IVehicleIgnition` stays a Core ModAPI service (consumers: Vehicles/PlayerBody `Car.TickIgnition`, Patrols `Patrol.StartUp/EngineRunning/StartEngine/StopEngine`). The `Get/Set/Call/Call<T>` Expression.Compile helpers (269-293) MERGE-INTO `Core.OptionalAccess`. |
| Modules/EngineSimCompat/EngineDiscovery.cs | 95 | Sliced discovery of `RealisticEngineSound` and `AudioListener` (incl. inactive) for Engine Sim's scan | Compatibility (+Core scene index) | MOVE. Use the Core WorldIndex walker instead of its own root walk. |

## 2. Cross-module coupling

### 2.1 Core / Services members this area consumes

| file:line | member | why |
|---|---|---|
| QuestsModule.cs:47 | `ModuleHost.DeferredInit` | Starts the engine after chainloading |
| QuestsModule.cs:55-56,75-76 | `Services.Quests` (provide), `Services.CrewBailedOut += CrewHistory.Bailed`, `SaveBus.StateChanged` | Provider; crew history; save/load state machine |
| PluginRuntime.cs:45-50 | `Hooks.CreateObjectEnter.Postfix`, `GetButtonDownUpdate/GetButtonDo/GetButtonUpUpdate/GetMouseButtonDownUpdate/MouseLookDo.Prefix` | Kill detection; input block while the native conversation is open |
| PluginRuntime.cs:52-55 | `Hotkeys.Register` x4 (log, dismiss, dump, interact) | Hotkeys |
| PluginRuntime.cs:95-111, 149-150 | `SaveBus.Slot/Generation/AfterSave/State`; Plugin.cs:2310,2345 `SaveBus.ReportFailure` | Save transaction |
| MapLink.cs:19-20,54-57 | `Services.Map.Explored/Add/Exists/Remove`, `Services.Compass.Add/Exists/Remove` | Quest flags (CleanHUD provider) |
| Plugin.cs:701-704 | `Services.Camps.At(rel, 120)` (Patrols provider) | Delivery destination dead/ready |
| Plugin.cs:849, PluginRuntime.cs:30-32 | `Services.QuestUi.Notify/Tracker/Journal/Conversation` (CleanHUD) | Presentation hand-off |
| Hire.cs:143,689-752, QuestFollower.cs:28,47,111 | `Services.Npc.Inform`, `Services.Npc.SetFollowerDriven` | Follower combat/idle exclusion |
| Hire.cs:143,997,1344,1485,1564-1571,1673-1674,1707; NativeConversation.cs:107,136; Plugin.cs:1152; CrewHistory.cs:18,31; QuestFollower.cs:66,78,112 | `Services.FollowerVehicles.ShootDistance/CanFire/Combat/SeatCombat/SeatPose/ReleasePose/IsSeated/IsCrew/Animated` (Patrols provider) | Seat, poses and crew checks |
| QuestFollower.cs:30-52,89,96 | `Services.Animation.Find/Attach/Ready/WeaponKey/HasWeaponPoses/WeaponKind/ReloadsOneRoundAtATime` + `IAnimatedCharacter` | Follower body |
| Game.cs:49-54,108,134,593 | `Core.Origin.Anchor`, `GameRefs.Player/ItemNameId/NewGOArrayList/GameManager`, `FsmUtil.Find` | World access |
| QuestWorld.cs:61, NativeConversation.cs:93-119, EngineSimCompat:48,137,162 | `GameRefs.InPlay/InGame/MainCamera/Player`, `ApocasetterBridge.InputBlocked`, `EditorSession` | Gates |
| QuestContent.cs:37-57, 74 | `DiskResources.Names/Open`, `Assets.LegacyPluginDir` | Default content |
| CrewHistory.cs:42,49 | `Assets.ModuleConfigDir("Quests")` and `("QuestsCompat")` | Sidecar paths |
| TalkHook.cs:41-45, QuestsCompat/Binding.cs | `Core.OptionalAccess.Read/Call` | Talk reflection delegates |
| QuestConfigMigration.cs:15-16,49-50 | `Legacy.Get` | Config import |
| NativeConversation.cs:38-39, EngineSimCompat:99, QuestsCompat:105 | `Core.Patches.Patch/Unpatch` | Third-party patches |
| EngineDiscovery.cs:32 | `Core.Runner.Host.scene` | Seeds the DontDestroyOnLoad scene |

### 2.2 Services this area provides

| Provider | Member | Signature (src/Core/QuestServices.cs, Services.cs) |
|---|---|---|
| Quests (`Plugin`, PluginRuntime.cs:11) | `Services.Quests : IQuestApi` | `bool Ready, TrackerEnabled, JournalOpen; string JournalText, JournalKey; int Completed; IList<QuestRow> Rows; IQuestConversation Conversation; void ExternalOpened(object host); void ExternalClosed();` |
| Quests (`TalkBridge`, TalkHook.cs:23) | `IQuestConversation` (via `IQuestApi.Conversation`) | `bool Open; string Name, Prompt; int Revision; IList<string> Chat; IList<QuestChoice> Choices; void Choose(string key); void Close();` |
| EngineSimCompat (EngineSimCompatModule.cs:20,83) | `Services.VehicleIgnition : IVehicleIgnition` | `bool TryPlayer(GameObject car, bool down, bool held, out bool keyOn, out bool running, out bool cranking); bool TryRunning(GameObject car, out bool running); bool TryStart(GameObject car); bool TryStop(GameObject car);` |

`IQuestApi.ExternalOpened/ExternalClosed` are called only by Quests' own TalkBridge (NativeConversation.cs:44-45), so they should not be on the public contract. `IFollowerVehicles.ResolveType(string)` (Services.cs:381) has no consumer anywhere ("Legacy Quests binds these types by reflection" is no longer true), so it is dead API on the Patrols side.

### 2.3 Rule violations (direct module-to-module references)

| file:line | what | fix |
|---|---|---|
| Quests/Hire.cs:277-282 | `c.GetType().FullName == "WildWasteland.Modules.Patrols.PassengerGuard" / ".Pose"`, then `Destroy(c)` over `npc.GetComponents<Component>()` | Add `IFollowerVehicles.Release(GameObject npc)` (Patrols removes its own components) |
| Quests/Hire.cs:964 | Hard-coded Patrols child name `"Apocapatrol.PassengerPos"` to detect an occupied seat | `IFollowerVehicles.SeatTaken(car)` |
| Quests/Hire.cs:15-16, 75, 205, 1489, 1555 and QuestConfig.cs:171-181 | Behaviour depends on Patrols' crew internals (ranged-name list, arm poses, PassengerGuard event rewiring of the Attack FSM) | Crew-seat ModAPI in Vehicles/Patrols |
| Quests/CrewHistory.cs:49 | Path string of the retired QuestsCompat module folder | Keep as a migration read, then drop |
| EngineSimCompat | none (only Core + third-party) | n/a |

The area makes no compile-time references to other modules' types. See 10a for the whole repo.

## 3. Harmony patches and scheduler jobs

### 3.1 Patches

| file:line | target method | kind | via | per-call cost / filter |
|---|---|---|---|---|
| Quests/PluginRuntime.cs:45 | `CreateObject.OnEnter` | postfix `Death` | Core.Hooks | Per spawn. `I==null \|\| !ready` -> `Fsm.Name != "Health"` (string compare, no length gate) -> `ActiveStateName != "spawn"` -> `Game.BaseName` allocations only for Health spawns |
| Quests/PluginRuntime.cs:46 | `GetButtonDown.OnUpdate` | prefix `BlockButtonDown` (skip while NativeOpen) | Core.Hooks | Every GetButtonDown every frame; two field reads |
| Quests/PluginRuntime.cs:47 | `GetButton.DoGetButton` | prefix `BlockButton` | Core.Hooks | Same |
| Quests/PluginRuntime.cs:48 | `GetButtonUp.OnUpdate` | prefix `BlockButtonUp` | Core.Hooks | Same |
| Quests/PluginRuntime.cs:49 | `GetMouseButtonDown.OnUpdate` | prefix `BlockMouse` | Core.Hooks | Same. **docs/harmony.md:34 still says this dispatcher has no subscriber and is never patched. That is stale: Core and Quests subscribe.** |
| Quests/PluginRuntime.cs:50 | `MouseLook.DoMouseLook` | prefix `BlockLook` | Core.Hooks (shared with Core SinglePlayerSaving) | Every MouseLook per frame; NativeOpen check, then owner compare |
| Quests/NativeConversation.cs:38 | `ApocalypterTalk.Plugin.Open` (optional) | postfix `TalkBridge.Opened` | Core.Patches | Per conversation |
| Quests/NativeConversation.cs:39 | `ApocalypterTalk.Plugin.Close` (optional) | postfix `TalkBridge.Closed` | Core.Patches | Per conversation |
| Quests/TalkHook.cs:322-327 | `ApocalypterTalk.TalkApi.ActionTriggered` | event subscription (not a patch) | reflection | Per action line; `GetFlag/SetFlag` use `MethodInfo.Invoke` (339,345) |
| QuestsCompat/QuestsCompatModule.cs:94-96 | Talk `Plugin.Update/LateUpdate` prefix, `Open` prefix+postfix, `Close` postfix, `GetCam` prefix, `OnDestroy` prefix | (dead) | Core.Patches | Never installed (module unregistered) |
| QuestsCompat/PluginLifetime.cs:42 | `TalkApi.LoadFile/Reload` | transpiler (dead) | Core.Patches | Never installed |
| EngineSimCompat:72 | `EngineSim.EngineManager.Update`, `.FixedUpdate` | prefix `ScheduledOnly` | Core.Patches | Per Engine Sim tick; reference compare + bool |
| EngineSimCompat:73 | `EngineManager.Scan` | transpiler `CachedScan` | Core.Patches | Replaces `Resources.FindObjectsOfTypeAll<RealisticEngineSound>` with the cached snapshot (0.5 s source scan) |
| EngineSimCompat:73-74 | `EngineManager.FindListener`, `EngineSim.Listener.get_Current` | prefix `FindListener` | Core.Patches | Cached listener |
| EngineSimCompat:75 | `EngineSim.IgnitionSwitch.Update` | prefix `BeforeSwitch`, postfix `AfterSwitch`, transpiler `SwitchInput` | Core.Patches | Per switch per frame; dictionary lookup |
| EngineSimCompat:76 | `IgnitionSwitch.Pointed` | transpiler `CachedCamera` + postfix `KeyboardPointed` | Core.Patches | `Camera.main` -> `GameRefs.MainCamera` |
| EngineSimCompat:77 | `IgnitionSwitch.SetText`, `DriveCoupling.Release` | prefix `SetText`, postfix `Released` | Core.Patches | Cached delegates |
| EngineSimCompat:78-79 | `NWH VehicleController.OnEnable`, `SkrilStudio.RealisticEngineSound.OnEnable` | postfix `VehicleEnabled/SoundEnabled` | Core.Patches | Per enable; enqueue only. Game types: no other WW patcher found |

Shared targets: the input and MouseLook dispatchers are shared with Core, PlayerBody, NPC, Guns, Survival and CleanHUD (see 10b). The Talk plugin type is also patched by CleanHUD (`Talk.DialogueUI.ShowDialogue/AppendChatLine/SetChoices` postfix, docs/harmony.md:226). Once Talk moves to Compatibility, one owner should hold all Talk patches.

### 3.2 Scheduler jobs and other callbacks

| name | kind | interval / budget | file:line | note |
|---|---|---|---|---|
| Quests.Frame | EveryFrame (Update) | every frame | QuestsModule.cs:30 | `Game.WatchSpawns`, deferred queue, empty `Hire.EarlyTeach`, delivery clock, `Takeover.Update`, `Hire.Update`, `SyncTalkUi`, `InteractionFrame`, `QuestFollower.Tick` |
| Quests.Poses | EveryFrame (LateUpdateLast) | every frame | :31 | `Hire.LateUpdate` (seat lock), `QuestFollower.Late` |
| Quests.Progress | Interval | 0.5 s (+ internal 1 s `RefreshLines`) | :32 | `Plugin.Tick` |
| Quests.Camps | Interval | 0.5 s (+ internal 2 s gate) | :33 | `Takeover.NeutralizeTick` |
| Quests.Givers | Interval | 0.5 s (+ internal 1 s gate) | :34 | `Takeover.GiverTick` |
| Quests.Map | Interval | 0.5 s (+ internal 1 s `SyncMapMarkers`) | :35 | `MapLink.Reconcile` runs even when not Live |
| Quests.Reputation | Interval | 0.5 s (+ internal 3 s gate) | :36 | `ReputationTick` |
| Quests.Interaction | Interval | 0.2 s | :37 | `FindInteraction` (people scan + raycast) |
| Quests.World cache | Sliced | 500 us | :38 | `QuestWorld.Step` |
| Quests.Windows | Gui (predicate `WantsGui`) | while needed | :39 | Tracker, journal, toasts, fallback dialogue |
| (not a job) | `Application.onBeforeRender += Hire.Recoil` | every rendered frame | Hire.cs:112 / 1788 | Outside the scheduler, so it gets no attribution or budget. It runs while the module is installed even without a follower (early-outs). |
| (event) | `TerrainTile.OnTileApplied += QuestWorld.Applied` | per tile | QuestWorld.cs:42 | Also subscribed by Towns, POI, Performance, CleanHUD and Patrols (10d) |
| (event) | `SaveBus.StateChanged`, `Services.CrewBailedOut`, Talk `ActionTriggered` | events | QuestsModule.cs:56; TalkHook.cs:327 | |
| Talk.Frame / Talk.Cursor | EveryFrame / LateUpdateLast | (dead) | QuestsCompatModule.cs:40-41 | Unregistered module |
| Engine Sim discovery | Interval | 1 s | EngineSimCompatModule.cs:43 | Keeps ticking after `_attempted` (one bool test). Name lacks the `EngineSimCompat.` prefix (AGENTS 5.4). |
| Engine Sim frame | EveryFrame (Update) | every frame | :80 | Calls the source `EngineManager.Update` through a compiled delegate |
| Engine Sim physics | EveryFrame (FixedUpdate) | every physics step | :81 | Source `FixedUpdate` |
| Engine Sim world cache | Sliced | 500 us | :82 | `EngineDiscovery.Step` |

## 4. Logging

### 4.1 Switches / config keys
| key | file:line | what it gates | proposal |
|---|---|---|---|
| tools.cfg `[Quests / Debug] RecordNpcNames` (bool, false) | QuestConfig.cs:75, Plugin.cs:979-1020 | Logs each NPC talked to and appends `npc_names.txt` / `npc_locations.txt` (ReadAllLines + AppendAllText per conversation) in the plugin folder | Not a log switch but a Tools data recorder. Move to Tools/Debug and write under `config/WildWasteland/Quests/`. |
| tools.cfg `[Quests / Debug] FactionDumpKey` (Key, None) | QuestConfig.cs:74, PluginRuntime.cs:54,77 | Hotkey writes `faction_dump.txt` | Debug page button instead of a hotkey |
| none in EngineSimCompat / QuestsCompat | | | |

### 4.2 Helpers and sinks
| helper | file:line | users |
|---|---|---|
| `QuestLogger` (`Plugin.Log`) | QuestLogger.cs:3-7; created PluginRuntime.cs:37 | ~57 call sites: Hire.cs (34 LogInfo), Plugin.cs (14), Game.cs (5), Voice.cs (2), FactionDump, TalkHook. `LogInfo` checks `Core.Log.Verbose` inside, but every caller concatenates the string first (e.g. Hire.cs:609/1352 per shot, Hire.cs:1383 builds a 1.5 KB part list) |
| `Plugin.LogOnce(where, e)` + static `logged` HashSet | Plugin.cs:27-32 | ~45 catch blocks in Plugin/Hire/Voice; error level, de-duplicated per key for the session |
| `Plugin.Warn/Info` (static) | PluginRuntime.cs:59-60 | Content parsing; `Takeover.Log = Info` (PluginRuntime.cs:44) for 18 `Log(...)` sites in Takeover.cs |
| `Game.Watched.Log` (Action) | Game.cs:204,230 | Delivery goods respawn |
| `Voice.logged` HashSet | Voice.cs:26,69 | Once per quest/line/actor |
| Direct `Core.Log.Always` | PluginRuntime.cs:51,57,102; Plugin.cs:849,2346; QuestFollower.cs:56; Voice.cs:31; QuestConfigMigration.cs:61; NativeConversation.cs:42; QuestsModule (none); EngineSimCompat:44,84; QuestsCompat:90,101 | Plugin.cs:849 logs **every toast at Always level** when CleanHUD shows it. QuestFollower.cs:56 logs every hire. Voice.cs:31 logs "voicelines disabled" on every start. |
| Own ManualLogSource | none (Plugin.cs:9 `using BepInEx.Logging` is a leftover) | |

### 4.3 Proposed mapping (one VerboseLog 0/1/2)
| level 1 (main actions) | level 2 (deep traces) |
|---|---|
| Quest accepted/completed/failed/cancelled/abandoned (`AddToast` text, Plugin.cs:847-856, now Always); quests loaded/saved summary (Plugin.cs:2346, 2404; PluginRuntime.cs:102); hire started/ended/died (Hire.cs:161, Dismiss, Died); follower rides/gets out (Hire.cs:1688,1749); camp ownership changed (Takeover.cs:215); garrison spawned (Takeover.cs:595); giver respawned (Takeover.cs:704); config migrated (QuestConfigMigration.cs:61); content imported (PluginRuntime.cs:38); Talk adapter ready (NativeConversation.cs:42); Engine Sim scheduled/fallback (EngineSimCompat:84,116,131) | Hire gun/part scans (Hire.cs:1032-1158, 1383-1423, 1587); per-shot lines (609, 1352); exit spot (836, 842); `LogBody` (893-907); brain wake/restore (223, 253, 299); takeover spawn geometry (Takeover.cs:225-272, 489, 605-621); walker resume (127); PlayMaker global ints list and money variable (Game.cs:467,489); Easy Save resolution (Plugin.cs:2103); hand-in details (Game.cs:811); voice file picks (Voice.cs:44,69); RecordNpcNames output; follower animation attach (QuestFollower.cs:56); mouse-look guard installed (PluginRuntime.cs:51) |

Errors (`LogOnce`, `LogError`) and warnings stay always on. Callers must check `Log.Level >= 2` before building strings (AGENTS 5.5).

## 5. Redundancy and dead code

| # | evidence | finding | action |
|---|---|---|---|
| 1 | QuestsCompat/*.cs; Core/Plugin.cs:126-140; docs/modules.md:26 | Whole module compiled but unregistered | DELETE the 3 files. Keep the `QuestsCompat/Saves` crew fallback read (CrewHistory.cs:49) for one release. |
| 2 | Voice.cs:16 `PlaybackEnabled = false` | All voice playback, `ReadWav`, AudioSource code and `Say` calls are inert. Settings `[Quests / Voice]` are still bound and shown. | Keep the settings. Either re-enable or remove `Say/Play/Load/ReadWav` until the recordings exist. |
| 3 | Voice.cs:125-183 vs Core/VoiceAudio.cs:9, Guns/Tracers/Wav.cs, NPC/Women/Wav.cs, Patrols/Crews/Rider.cs:1085 | Fifth WAV decoder | Use `Core.VoiceAudio.LoadWav` |
| 4 | Quests/KeyMap.cs vs PlayerBody/KeyMap.cs | Identical tables (diff = 2 comment lines); Quests `Code/Build/Convert` unused | Core KeyMap |
| 5 | Plugin.cs:2072-2290 vs Towns/TownSaveRecord.cs:27-48, Guns/Ammo/PerGunAmmo.cs:246-261 (typed `ES3.Save<string>/Load`) | Quests reaches ES3 entirely by reflection (`MethodInfo.Invoke`, `GetMethods` scan per save, `FindType` over all assemblies) although WW links ES3 directly | One Core `SaveBus.Key(string key)` API: disk + cache write, AfterSave rewrite, read with fallback |
| 6 | Plugin.cs:125-133 `FindType` and 2140/2155/2267 | AppDomain scan each call (several per save) | `typeof(ES3Settings)`, `typeof(ES3File)` |
| 7 | Plugin.cs:2033-2047 `IsLoadingState`; Factions.cs:31 `HasNpc`; Hire.cs:688 `FindApi`; Hire.cs:19-20 `NpcAiGuid/PatrolGuid`; Plugin.cs:19-21 consts | Unreferenced members | DELETE |
| 8 | Hire.cs:1553 `EarlyTeach(){}` (called each frame, Plugin.cs:156); Hire.cs:1003 `LogGuard(){}` (called per frame mounted, :425); comments 1555-1560 | Empty stubs of removed Apocapatrol-teaching code | DELETE |
| 9 | QuestConfig.cs:72 `apocasetter` -> tools.cfg `[Quests / Legacy] Apocasetter` | Bound but never read | Stop binding; never purge (orphan rule) |
| 10 | Plugin.cs:217-222 | Six blank lines where removed code sat in `Tick` | Cosmetic |
| 11 | QuestWorld.cs (own root/tile walk + FsmList slice), CleanHUD/SceneCache.cs, EngineSimCompat/EngineDiscovery.cs, Performance FsmRegistry (PM1 index), Patrols `Prefabs` catalog | Five independent world/FSM indexes | Core WorldIndex (10d) |
| 12 | Game.cs:104-153 (`RegisterSpawned`, reflection on `PlayMakerArrayListProxy`) vs Core/InventorySupplies.cs:65-69 (typed), Patrols/WarRig/WarRigFeature.cs:59, Patrols/Spawning/Patrol.cs:1596, NPC/Women/Gungirl.cs:602, Guns/Ammo/AmmoMechanics.cs:420 (`GameObject.Find("itemNameID")`) | Six "name with itemNameID + add to ArrayList_Items" implementations | `Core.GameRefs.RegisterSaved(GameObject, baseName)` |
| 13 | Game.cs:591-606 `GameHours` (DayTime FSM) vs CleanHUD/MessageLog.cs, Patrols/Traffic/TrafficReinforcements.cs, Patrols/Spawning/Patrol.cs, Performance/Content/HudFsms.cs | Duplicate game-clock readers | `Core.GameRefs.GameHours` |
| 14 | Hire.cs:921-937 `PlayerCar` (player `InCar` FSM, GetComponentsInChildren every 2 s while missing) vs PlayerBody/Game.cs:105 `InCar` | Duplicate player-in-car lookup | Core `GameRefs.PlayerCar` |
| 15 | Game.cs:521-537 `FindInt/FindFloat/FindString`, Hire.cs:323 `FindFsm`, Takeover.cs:343 `MoveFsm`, QuestWorld.cs:111 `Fsms` vs Core `FsmUtil.Find` | Local FSM/variable lookup helpers | Core FsmUtil (+ variable lookups) |
| 16 | Takeover.cs:318-339 `ScanForPrefab`, Game.cs:159-175 `FindPrefab` (walk all `QuestWorld.Objects`) vs Patrols `Prefabs` name index ("Patrols.Prefab cache" job), Towns PoiPropCatalog/PoiSpawnCatalog | Duplicate prefab-by-name catalogs | Core prefab catalog service |
| 17 | Takeover.cs:374-404 `Blocked/Steer/RoomAt` (raycast steering) vs NPC `INpcApi.Travel` (Services.cs) | Second NPC locomotion system | Use `Services.Npc.Travel` (NPCAI) |
| 18 | Hire.cs:701-745 `Threats` + `Game.FindEnemies` vs NPC Senses | Follower threat detection parallel to NPCAI senses | NPCAI follower mode |
| 19 | QuestConfigMigration.cs vs Core `ModuleConfig.Migrate` (first-run only) | Second migration engine (per missing destination) | Core `MigrateMissing` (keep semantics) |
| 20 | Plugin.cs:2283-2290 backup `BepInEx/config/ApocalypterQuests_saves/<slot>.txt` | Written only when ES3 cannot be resolved (never true in WW, since ES3 is linked) | Read-only legacy fallback; stop writing |
| 21 | QuestsModule.cs:32-36 vs Plugin.cs:240-243, 250-253, 813-814; Takeover.cs:660-661, 724-725 | Scheduler intervals plus inner `Time.unscaledTime` gates (double gating) | Set the job intervals to the real cadence (1 s, 2 s, 3 s) and drop the inner timers |
| 22 | TalkHook.cs:173-194 PNG load (`Texture2D.LoadImage`) | One more of 18 PNG loaders (10d) | Core texture loader |
| 23 | EngineSimCompatModule.cs:269-293 `Expression.Compile` helpers vs `Core.OptionalAccess` (used by TalkHook/QuestsCompat and the reason given in docs/harmony.md:228) | Two delegate-compilation helpers | Core.OptionalAccess |
| 24 | docs/modules.md:82 says crew history "remains at QuestsCompat/Saves"; code writes `Quests/Saves` (CrewHistory.cs:42) | Stale doc | Fix the doc |
| 25 | Hire.cs:15-16, QuestConfig.cs:162-181 descriptions mention "needs the NPCAI and Apocapatrol mods" | Stale player-facing text | Update descriptions (keys unchanged) |

Legacy migrations that must stay: the `local.apocalypterquests.cfg` import (QuestConfigMigration), content import from `plugins/ApocalypterQuests` and the Apocasetter disabled cache (QuestContent.cs:69-81), reading the ES3 key `ApocalypterQuests`, and the backup file. The QuestsCompat crew path can go after one release.

## 6. Performance hot spots

| file:line | issue | fix |
|---|---|---|
| QuestWorld.cs:73-89 | The 500 us slice walks **every transform** of every loaded scene and every applied tile. `Add()` keeps every GameObject in `Objects/Known` (tens of thousands). Per node it calls `Game.BaseName(t.name)` (2 string allocations), `t.name` (an allocation) and `Contains("mapIcon")`. `Pois` dedupe is O(n) (`foreach p in Pois`). | Walk roots/tiles only to depth needed; keep only candidates (Camp_/Building_/Cave_/Wreck_, mapIcon); share Core WorldIndex |
| QuestWorld.cs:111-118 | `Fsms(go)`: a `new FsmSet` plus a `GetComponents` array per miss; the cache is cleared wholesale at 128 entries (thrash with many NPCs); `new PlayMakerFSM[0]` for null | Instance-id cache with sweep, static empty array |
| QuestWorld.cs:90-99 | Rebuilds `All/Items/People` from `PlayMakerFSM.FsmList` every 0.5 s, the same list the Performance PM1 index already maintains | Consume the Core FSM index by name |
| Plugin.cs:223 | `book.Active.ToArray()` every 0.5 s (also :883, :893, :1035, :1103) | Index loop with removal-safe iteration |
| Plugin.cs:250-276 | `SyncMapMarkers`: `new Dictionary` and `"Q:"+id` / `"T:"+id` strings each second | Reuse the dictionary; precompute keys per quest |
| Plugin.cs:279-311 | `RefreshLines` each second: string concatenation per quest plus `Game.FindItems` scan of all item FSMs for fetch quests | Rebuild only on change; use an item index |
| Plugin.cs:811-845 | `ReputationTick` every 3 s: `new List<string>(book.Rep.Keys)`, `Game.FindPeople` (new List), `new HashSet<int>` | Reuse buffers |
| NativeConversation.cs:98-125 | `FindInteraction` every 0.2 s: iterates all `People`; per NPC `Game.BaseName` + `Util.Norm` (alloc), `Person()` loops `PeopleWords` with `Contains`; prompt string built each tick | Pre-filter by distance first; cache the normalized name per instance id; build the prompt only on target change |
| Takeover.cs:318-339, 714-720 | `ScanForPrefab` and `InactiveExists` linearly scan **all** `QuestWorld.Objects` with `BaseName` allocations; runs from `GiverTick` (1 s) whenever a giver is missing (30 s miss cache only for prefabs) | Core prefab catalog; name index |
| Takeover.cs:700-703, 222-272, 600-631 | Synchronous `Instantiate` of NPC groups (up to GroupSize 5 / GarrisonSize 4 in one frame) | Spawn through an NPCAI/Patrols sliced spawn queue (Patrols.Build precedent) |
| Hire.cs:351-438 | Per frame while a follower exists: `Threats` -> `Game.FindEnemies` new List every 0.7 s; `ClearShot` RaycastNonAlloc each frame while mounted with a target; `RescueIfLost`, `FootWatch` etc. | Move to the NPCAI follower think (0.1-0.5 s scaled) |
| Hire.cs:424 | `npc.GetComponent<Rigidbody>()` every 0.2 s while mounted | Cache |
| Hire.cs:457, 928 | `GetComponentsInChildren<PlayMakerFSM>(true)` on the player root every 2 s while the Health/InCar FSM is missing | `GameRefs` cached refs |
| Hire.cs:853, 899-900, 1374-1412, 1614, 1661-1662, 1731 | `GetComponentsInChildren<Collider/Transform/Renderer/Rigidbody>` on mount/dismount/scan (per event, not per frame) | Acceptable; reuse list buffers |
| Hire.cs:112, 1431-1447 | `Application.onBeforeRender` callback outside the scheduler | Presentation late pose hook / Scheduler `LateUpdateLast` |
| Hire.cs:609, 1352, 1383-1423 | Log strings built before the Verbose check | Level check first |
| Plugin.cs:2116-2257 | Reflection `MethodInfo.Invoke`, `GetMethods` loops and `Activator`-style constructor scans at each save (`SaveToCache` scans methods per cached file per save) | Typed ES3 calls (see 5.5) |
| Plugin.cs:2461-2499 | IMGUI `Draw`: `"Quests   (" + n + " completed)"` concatenated per OnGUI event while the journal is open | Cache the title string |
| Plugin.cs:979-1020 | RecordNpcNames: `File.ReadAllLines` + `AppendAllText` on each conversation (debug only) | Memory set + flush at save |
| Voice.cs:54-66 | `Directory.GetDirectories` + `File.Exists` + `ReadAllBytes` + decode on the main thread at speak time (disabled today) | Pre-index at start; Core audio loader |
| PluginRuntime.cs:62-68 | `Death` handler: `Fsm.Name != "Health"` string compare for every CreateObject spawn | Length gate first (`Name.Length == 6`) |
| EngineSimCompatModule.cs:217-225 | `Coupling()`: `car.GetComponent<VehicleController>()` + `IDictionary` lookup on every call; `TryPlayer` is called **every frame** while driving (PlayerBody/Vehicles/Car.cs:102-106) | Cache last car -> coupling, invalidate on `Released` |
| EngineSimCompatModule.cs:66, 121-122, 269-293 | Per-frame `_update/_fixed` delegates come from `Expression.Compile`; on this Mono runtime the docs avoid interpreted Expression.Compile in hot paths (docs/harmony.md:228) | Build with `Core.OptionalAccess` (Cecil delegates) |
| EngineDiscovery.cs:29-31 | `Resources.FindObjectsOfTypeAll<RealisticEngineSound>` + `FindObjectsOfType<AudioListener>` at Init and on every scene load (synchronous) | Acceptable at a scene boundary; better seeded from the Core WorldIndex |
| EngineDiscovery.cs:74 | `Listeners.RemoveWhere(DeadListener)` allocates a Predicate every 0.5 s | Cache the delegate |
| EngineSimCompatModule.cs:43 | Discovery interval keeps running after install/attempt | Remove the job after `_attempted` |

## 7. ModAPI / multiplayer events

Ids: quests already have string ids (`Quest.Id`, `gen:` for generated). NPCs are identified by their saved object name (`Pup(Clone)936`, issued by `itemNameID`), and camps by `Factions.KeyOf` = `Name@x,z` (origin-relative, 10 m grid). Positions must be MapMagic-relative (`Game.Rel`, `Core.Origin.World`). Vehicles need a Core stable vehicle id (the saved car name).

| event (publish) | publisher | payload |
|---|---|---|
| QuestOffered / QuestAccepted / QuestDeclined | Quests | questId, type, giverNpcId, giverFactionKey, target token, targetPosRel, count, reward, deadlineGameHours |
| QuestProgress | Quests | questId, progress, count, objectiveDone |
| QuestCompleted / QuestFailed / QuestCancelled / QuestAbandoned | Quests | questId, reason, reward, repDelta |
| ReputationChanged | Quests | factionKey, oldRep, newRep, reasonCode |
| PlayerHostilityChanged | Quests -> NPCAI | npcId, hostile (replaces direct `Game.SetPlayerEnemy` FSM writes) |
| FollowerHired / FollowerDismissed / FollowerDied / FollowerLost | Quests | npcId, factionKey, kind, untilGameHours |
| FollowerMounted / FollowerDismounted | Vehicles/Patrols crew service | npcId, vehicleId, seat |
| CampOwnershipChanged | POI (from Takeover) | campKey, oldFaction, newFaction |
| GarrisonSpawned / GiverRespawned / WalkerArrived | NPCAI | campKey, npcIds[], posRel |
| ConversationOpened / ChoiceSelected / ConversationClosed | Quests dialogue service | npcId, choiceKey (drives HUD and the Talk bridge) |
| DeliveryGoodsSpawned / ItemsHandedIn | Quests / Loot | itemPrefabName, count, posRel, saved names |
| MoneyChanged | Core (from `Game.AddMoney`) | delta, newValue |
| EngineIgnitionChanged | Compatibility (EngineSim) or Vehicles | vehicleId, keyOn, cranking, running, byPlayer |
| EngineStartRequested / EngineStopRequested | Vehicles (Patrols AI, PlayerBody) | vehicleId, requester |

| consume | provider | currently |
|---|---|---|
| NpcKilled(npcId, killerId, corpseName, posRel, tag) | NPCAI | Quests patches `CreateObject` Health `spawn` itself (PluginRuntime.cs:62-68) |
| NpcSpawned / NpcDespawned | NPCAI | `QuestWorld` FSM list rescan |
| CrewBailedOut | Patrols | already an event (`Services.CrewBailedOut`) but carries GameObjects; needs ids |
| CampState query | Patrols/POI | `Services.Camps.At` |
| Faction relation query | Core ModAPI | `Factions.Friendly/OfObject` |
| Population queries (people near, by name) | NPCAI | `Game.FindPeople/LiveNames` |
| Item query (count/take by token) | Loot/Core | `Game.FindItems/TakeItems` |

## 8. Unmapped or ambiguous code

| code | candidates | recommendation |
|---|---|---|
| Takeover (camp takeover, garrisons, faction-spawner suppression, giver respawn) | Quests / NPCAI / POI | Quests owns the quest outcome; POI owns camp ownership (`Factions.Overrides` + suppression); NPCAI owns spawning and walking via ModAPI |
| Factions (relations, human tags, people words) | Quests / NPCAI / Core | A Core faction-relations service fed by Quests' factions.txt; NPCAI, Patrols and Towns read it |
| Hire follower | Quests / NPCAI / Vehicles / Presentation / Gunplay | Split as in 1. The brief says "Followers via NPCAI ModAPI": new `INpcApi.Follow(npc, leader, settings)`, plus a crew-seat API on Vehicles/Patrols |
| QuestFollower (body driver) | NPCAI / Presentation | NPCAI follower feature calling Presentation through `IAnimationApi` |
| IMGUI tracker/journal/toasts/fallback dialogue (Plugin.cs:2410-2499, NativeConversation.cs:143-166) | Quests / HUD | HUD. CleanHUD already provides `Services.QuestUi`; keep a minimal fallback in HUD only |
| TalkBridge / TalkApiLink (Apocatalker) | Quests / Compatibility | Compatibility. Quests exposes a dialogue-choice service in Core ModAPI |
| QuestWorld | Quests / Core | Core WorldIndex |
| Game.RegisterSpawned / MoneyVar / GameHours | Quests / Core | Core (shared game access) |
| FactionDump, RecordNpcNames | Quests / Debug / Tools | Debug (diagnostics) |
| Voice (authored voice lines) | Quests / Presentation | Quests (content). The decoder goes to Core audio |
| EngineSimCompat `IVehicleIgnition` | Compatibility / Vehicles | Contract in Core ModAPI (Vehicles semantics); the implementation in Compatibility |
| EngineDiscovery | Compatibility / Core | Compatibility, fed by the Core WorldIndex |

## 9. Save / config compatibility constraints

| item | value | note |
|---|---|---|
| ES3 key (slot file) | `ApocalypterQuests` (text, header `AQ1`, lines `T|D|C|R|O|H|W|Q...`; Quests.cs:175-330) | Written to disk + ES3 cache on SaveBus `wait`/`SaveGame` and again via `SaveBus.AfterSave` (PluginRuntime.cs:105-112). Must stay readable; a new sidecar may only be added beside it. |
| Saved references inside AQ1 | NPC object names (`GiverId`, `HireName`), camp keys `Name@x,z`, stamp keys `camp:...`, `id:` prefixes, `Place = "WW:return-empty-delivery"`, `gen:` quest ids | Depend on game naming (`itemNameID`) and origin-relative grids; keep them verbatim |
| Legacy backup | `BepInEx/config/ApocalypterQuests_saves/<slot with non-alnum->_>.txt` (Plugin.cs:2283-2290) | Also listed in Core/SaveFiles.cs:185 as a save participant (`LegacyQuestName`, :168, :198) |
| Sidecar | `config/WildWasteland/Quests/Saves/<slot>.crew.txt` (CrewHistory.cs:42); fallback read `config/WildWasteland/QuestsCompat/Saves/<slot>.crew.txt` (:49) | Listed in SaveFiles.cs:189 |
| SaveTypes alias | none: Quests saves no component types | A namespace rename is safe for Quests saves |
| Objects created at runtime | `ApocalypterQuests.HireSeat` (child of the player's car, Hire.cs:1631), `AttackRaycast_Ranged` (child of the follower NPC, Hire.cs:1567), `ApocalypterQuests_Voice` (DontDestroyOnLoad, Voice.cs:94); spawned NPCs and goods registered in `ArrayList_Items` with `itemNameID` names (Game.cs:104-127) | The HireSeat child sits under a saved car while mounted. Verify it is not serialized (AGENTS 5.10). `Takeover.Suppress` (Takeover.cs:36) calls `SetActive(false)` on native camp spawner children, so check they do not need `SaveGame`. |
| settings.cfg | `[Quests] Enabled, MaxActiveQuests, RewardMultiplier, ShowTracker`; `[Quests / Generated] Enabled, Types`; `[Quests / Items] CountCarriedItems`; `[Quests / Voice] Enabled, Volume, Folder`; `[Quests / RepVendor] Enabled`; `[Quests / Hire] Enabled, RideInCars, ShootFromCar`; `[Keys] Quests: talk, Quests: quest log, Quests: dismiss follower` | Routing rule QuestConfigMigration.cs:66-74 |
| tools.cfg | `[Quests / Legacy] Apocasetter`, `[Quests / Advanced] MoneyVariable`, `[Quests / Items] HandInRadius, MinLiquidFraction`, `[Quests / Quests] *` (9), `[Quests / Kills] *` (4), `[Quests / Goto] *` (2), `[Quests / Takeover] *` (12), `[Quests / Deliver] *` (5), `[Quests / Reputation] *` (7), `[Quests / RepVendor] BuyPrice, SellPrice, MaxPerDeal, Npcs`, `[Quests / Factions] HomeRadius, HumanFactions, PeopleNames`, `[Quests / Hire] *` (19 incl. arm/reload overrides), `[Quests / Voice] Spatial`, `[Quests / Debug] FactionDumpKey, RecordNpcNames` | F7 page filter `"Quests / *"` (QuestsModule.cs:82) |
| Config migration source | `BepInEx/config/local.apocalypterquests.cfg` (all 16 old sections), applied **per missing destination** (not first-run only) | Keep |
| Plugin data | `plugins/WildWasteland/Quests/{quests.txt, factions.txt, *.txt.example, Voice Actors/<actor>/<quest>/*.wav}`; outputs `faction_dump.txt`, `npc_names.txt`, `npc_locations.txt` in the same plugin folder | Legacy import roots: the loaded `local.apocalypterquests` folder, `plugins/ApocalypterQuests`, `BepInEx/cache/Apocasetter/disabled/local.apocalypterquests/plugins/ApocalypterQuests` |
| Asset index names | `Quests.Defaults.*`, `Quests.voice-files.txt`, `Quests.<voice path>` (QuestContent.cs:36-57) | Prefixed by module id. A module rename needs `asset-index.tsv` updated. |
| ConflictsWith / GUIDs | `local.apocalypterquests` (QuestsModule.cs:21); optional Talk `com.example.apocalypter.talk` | |
| Module id | `Quests` = config prefix, Harmony id `com.denis.apocalypter.wildwasteland.Quests`, `Assets.ModuleDir/ModuleConfigDir("Quests")` | Keep the id, or alias the config prefix and folder |
| EngineSimCompat | only `[EngineSimCompat] Enabled` (settings.cfg, bound by Core); no saves; Harmony id `...wildwasteland.EngineSimCompat` | Merging into Compatibility moves the Enabled key, so migrate it |
| QuestsCompat | no entries bound today. An older settings.cfg may hold an orphan `[QuestsCompat] Enabled`. The data folder `config/WildWasteland/QuestsCompat/Saves` exists for older users. | Do not purge orphans |

---

## 10. Global sweep (whole repo, src/Core + src/Modules)

Method: comment- and string-aware scan of all 476 .cs files for (1) qualified `Modules.<X>.T` / `<X>.T` references that resolve through the shared `WildWasteland.Modules` namespace, (2) `using WildWasteland.Modules.<X>` imports and the identifiers they bring in, and (3) `"WildWasteland.Modules.<X>..."` reflection strings. False positives were checked by hand (e.g. `InteriorPopulation.Pending` and `TownMarkers.Compass` are local members). Scripts were kept in the session scratchpad.

### 10a. Module -> module type references

Matrix (rows = referencing area, columns = referenced module; counts are reference sites; R = includes reflection strings or Harmony targets):

| from \ to | PlayerBody | NPC | Guns | HUD | CleanHUD | Patrols | POI | Towns | Weather | Quests | EngineSim | Survival | Performance |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Core** | 6 | 8 | 4 | 5 | 9 | 25 | 1 | 1 (+R) | 1 | 1 | 1 | 1 | 4 |
| **Patrols** | | | | | | | | 24 | | | | | |
| **Towns** | | 12 R | | | 6 R | 12 R | 1 R | | | | | | |
| **Performance** | | | | | | 3 R | | | | | | | |
| **Quests** | | | | | | 2 R | | | | | | | |
| PlayerBody, NPC, Guns, HUD, CleanHUD, POI, Weather, Survival, EngineSimCompat, QuestsCompat | none | | | | | | | | | | | | |

Examples (file:line):

| edge | kind | examples |
|---|---|---|
| Core -> all modules | registration (legitimate) | Core/Plugin.cs:128-140 `new Modules.<X>.<X>Module()` |
| Core -> Patrols (22 typed + alias) | static settings and members | Core/UI/CorePages.cs:7 `using Patrols = ...PatrolsModule`; CorePages.cs:106-114,243-244 `Patrols.ConvoyEnabled/FuryRoad/FootPatrols`; :290-300,337 `Patrols.MenuKey/TemplateDumpKey`; :292 `Patrols.TurretSpotEditor/BusPassengerPoseEditorEnabled`; Core/Plugin.cs:114 |
| Core -> CleanHUD | typed | Core/SaveRuntime.cs:113 `CleanHUD.GlyphFont`; Core/SaveSystem.cs:121 `CleanHUD.MessageHook`; CorePages.cs:49,141 `CleanHUD.ChaseCameraHud`; using aliases in SaveWarning.cs:3, ReplacementGuard.cs:10, ToolWindow.cs:4, CorePages.cs:8 |
| Core -> PlayerBody / HUD / NPC / Guns / Performance | typed | CorePages.cs:75 `PlayerBody.InventoryModel`, :181,189 `PlayerBody.CharacterPortraits`; :76,206 `HUD.CrosshairController`; :135,247 `NPC.NpcModule`; :134,246 `Guns.GunsModule`; :30,369 `Performance.PerformancePage`, :56 `Performance.PerformanceModule` |
| Core -> Towns | hard-coded save knowledge | Core/SaveSystem.cs:264-265 (`Towns/Saves` path + ES3 key `WildWasteland.Towns.World.v1`) |
| Core -> modules (SaveTypes) | namespace strings | Core/SaveTypes.cs:31-36 (Patrols.WarRig, Patrols, PlayerBody, NPC, Guns, HUD) |
| Patrols -> Towns | typed static access (24) | Patrols/Crews/Pilot.cs:223,226 `Towns.TownsApi.Provider.VehicleWaypoint`; Crews/CrewPoi.cs:50 `...ParkingOutside`; Traffic/TrafficLocations.cs:35-49 `...PatrolCount/PatrolBases/PatrolBase/ParkingOutside`, :258 `Towns.TownMarkers.Owns`; Traffic/RoadPlanner.cs:67 `...TravelBlocks`; Traffic/WorldTraffic.cs:430; Traffic/TownTraffic.cs:5 `using WildWasteland.Modules.Towns` + :43,57,200 `TownsApi`, :192,292 `TownPlanner.Hash`, :130 `Towns.TownGasStation.LaneSpread` |
| Towns -> Patrols | typed (8) + reflection/patches (4) | Towns/TownsModule.cs:566,576 `Patrols.TownStop`, :605,612 `Patrols.TrafficBase`, :681-682 `Patrols.TrafficSpace.World`, :741 `Patrols.WorldTraffic.TownsStopped()`; TownParking.cs:29; TownRoadBridge.cs:20-29 reflection on `VehicleRoute/TerrainWaypoints/RoadPlanner/PatrolsModule.TrafficRoadRouting` + patches on their `Build/Revalidate` |
| Towns -> NPC | using + reflection + patches (12) | Towns/InteriorPopulation.cs:8 `using ...NPC` + :19,195,199,224,238 `Senses.Agent/AgentFor/AllAgents/ClearDeafInvestigation`; TownBakeBridge.cs:24-37 (`NpcModule`, `Nav.get_On/Spawned/FloorCells/StructureRootAt/Next`, `Brain.Spawned`); TownOverlap.cs:81-87 (`Nav.QueueMasks/Return/BeginBake/BakeStep/PickReset/Make/CreateMap`); PoiDressing.cs:42,201; TownTowerKit.cs:64-65; TownBakeData.cs:41-42 (NPC NavMaps folders) |
| Towns -> CleanHUD | using + reflection + patches (6) | Towns/TownMarkers.cs:9 `using ...CleanHUD`, :25,132 `GlyphText`, :50 `typeof(CleanHudModule)`, :41-55 patches `LocationMarkers.Observe`, `QuestMap.Tick` |
| Towns -> POI / Core UI | reflection + patches | TownPoiSettings.cs:23-34 (reads the POI module's `[POI / World] Spawn chance (%)` entry, patches `PoiModule.ToolPages`, `PoiLocations.Generate`, `CorePages.DrawWorld/ResetSettings`); TownDebug.cs:20-21 (`CorePages.Developer`) |
| Performance -> Patrols | reflection | Performance/Content/PatrolsBridge.cs:31-39 (`Patrols.WarRig.PoiEncounter.Protected/DetachTankers/CleanupChecks`, `PatrolsModule.Running`); World/CarLod.cs:194 (namespace prefix test) |
| Quests -> Patrols | type-name strings | Quests/Hire.cs:281 (`Patrols.PassengerGuard`, `Patrols.Pose`); Hire.cs:964 child name `Apocapatrol.PassengerPos` |

Worst offenders for the "ModAPI only" rule: **Towns** (four modules plus Core UI, through typed and reflected internals and Harmony patches on other modules' private methods), **Patrols <-> Towns** (two-way static coupling) and **Core -> module statics** in CorePages/SaveSystem/SaveRuntime (Core must not know module internals; use registered settings providers and SaveBus participants).

### 10b. Harmony targets with more than one owner (module or file)

Core dispatchers (shared by design; owners from `Hooks.<X>.<Kind>(` call sites, count = subscriptions):

| dispatcher target | kind | owners |
|---|---|---|
| `CreateObject.OnEnter` | postfix (14) | PlayerBody, NPC x5, Guns, Patrols, Quests, Towns x2, Weather, Performance x2 |
| same | prefix (3) | Guns, Survival, Towns |
| `Fsm.ProcessEvent` | prefix (11) | Core x2, PlayerBody, NPC x2, Guns x2, Towns, Weather, Performance x2 (+ Apocasaver's own patch) |
| `GetButtonDown.OnUpdate` | prefix (9) | Core x2 (HealthStacks, saving), PlayerBody x2, NPC, Guns, CleanHUD, Quests, Survival |
| `Fsm.EnterState` | postfix (7) / prefix (2) | Core, PlayerBody, Guns, CleanHUD x2, Performance x2 / Guns, Towns |
| `PlayMakerFSM.OnEnable` | postfix (6) | Core, Patrols, Towns, Weather, Performance x2 |
| `GetButton.DoGetButton` | prefix (5) | Core, PlayerBody, NPC, Guns, Quests |
| `SendEvent.OnEnter` | prefix (4) | NPC x2, Patrols x2 |
| `SetFsmFloat.OnEnter` | postfix (4) / prefix (1) | NPC x2, Guns, Patrols / HUD |
| `SetVelocity.DoSetVelocity` | prefix (4) | PlayerBody, NPC x2, Weather |
| `ActivateGameObject.DoActivateGameObject` | prefix (3) | Core x2, PlayerBody |
| `GetButtonUp.OnUpdate` | prefix (3) | Core, NPC, Quests |
| `Wait.OnUpdate` | prefix (3) | Core (VehicleLoadPlacement), Patrols, Towns |
| `ES3ReferenceMgrBase.GetPrefab` | resolver (3) | Patrols x2, Survival |
| `GetMouseButtonDown.OnUpdate` | prefix (2) | Core, Quests (docs/harmony.md:34 stale) |
| `MouseLook.DoMouseLook` | prefix (2) | Core (SinglePlayerSaving), Quests |
| `Raycast.OnEnter`, `SensorGetDetectionRayHit.OnEnter`, `SensorGetDetections.DoAction`, `ActivateGameObject.OnEnter` (postfix), `SmoothLookAt.DoSmoothLookAt` | prefix/postfix (2 each) | NPC + Guns (SmoothLookAt: NPC + PlayerBody) |
| `SensorGetLineOfSightResult.OnEnter3D / OnUpdate3D` | prefix (2 each) | NPC, Patrols |
| `Apocaspawner.Catalog.Entries` | postfix (2) | Patrols, Survival |
| `SetFsmFloat.DoSetFsmFloat` | prefix (2) | Patrols x2 |

Direct patches on the same target from more than one module or file (should become dispatchers or one owner):

| target | owners (file:line, kind) | note |
|---|---|---|
| `HutongGames.PlayMaker.Actions.SetAudioClip.OnEnter` | Guns GunsModule.cs:214 (postfix `Tracers.AfterSetAudioClip`); PlayerBody Body/FemalePain.cs:60 (prefix + finalizer) | Two modules, direct. Not flagged as CONFLICT because the kinds differ. Should be a dispatcher. |
| `UiTextSetText.OnEnter` | Guns WeaponDamageText.cs:8 (prefix + finalizer, `[HarmonyPatch]`); CleanHUD CleanHudModule.cs:154 (postfix `MessageHook`) | Same |
| `ActionHelpers.DoMousePick` | Performance PlayMaker/MousePickFeature.cs:142 (prefix PM7); PlayerBody Body/PickAssist.cs:29 (postfix) | Same |
| `NPC.Nav.Make` (WW internal) | Towns PoiDressing.cs:43 (prefix); Towns TownOverlap.cs:85 (postfix) | One module, two files, patching another module's private method |
| `Fsm.OnEnable` | Performance FsmRegistry.cs:131; FsmSleepFeature.cs:246 (both prefix) | Same module, two files |
| `PlayMakerFSM.OnDisable`, `PlayMakerFSM.OnDestroy` | Performance FsmRegistry.cs:140,155; GuiLoopFeature.cs:401,404 | Same |
| `PlayMakerFSM.Awake` | Performance Content/FsmEdits.cs:177; FsmSleepFeature.cs:295 (postfix both) | Same |
| `Fsm.OnTriggerEnter/Stay/Exit`, `Fsm.OnCollisionEnter/Stay/Exit` | Performance TriggerProxyFeature.cs:57-127; FsmSleepFeature.cs:260-265 | Same (prefix both) |
| Apocasetter `SettingsUI` | Core ApocasetterBridge.cs:46-47 (`Toggle` prefix); Towns TownDebug.cs:29-30 (`DrawSettings`, `DrawSectionTabs` prefixes) | Third-party type patched by two owners. Move to the Core bridge. |
| Apocalypter Talk | Quests NativeConversation.cs:38-39 (`Plugin.Open/Close`); CleanHUD (`DialogueUI.ShowDialogue/AppendChatLine/SetChoices`, docs/harmony.md:226); QuestsCompat (dead) | One Compatibility owner |
| Weapons Pack (`ChainsawPrototype.*`) | PlayerBody WeaponsPackShadows (`Plugin.UpdateThirdPerson`); NPC WeaponsPackCompat (`ProcessHit`, `FlamethrowerIgniteStatus.Touch/BeginEnding/OnDestroy`, `StartPreview/StopPreview`); CleanHUD WeaponsPackIcons (`PackHudIcon`); Guns WeaponsPackSound (`PlayStandardMeleeHitSound`) | Four modules bind one third-party mod |
| Apocasaver | Core ApocasaverExclusion (plugin/runner callbacks, HandPose); CleanHUD ApocasaverLoading (`AutosavePresentation.Begin/End`, `SaveNaming.OnGUI`) | Two owners |
| ChaseCamera | PlayerBody ChaseCameraCompat (`ApocaplayerBridge.FindType` prefix); CleanHUD ChaseCameraHud (`DrivingReadings.Capture` transpiler) | Two owners |
| Apocaspawner | Patrols WarRigFeature (`Spawner.Spawn` prefix); Guns ApocaspawnerDamage (`Parameters.WeaponDamage` postfix); Patrols + Survival via the Catalog dispatcher | Three modules |
| VistasWorldGen (`a.u.A` etc.) | Core VistasWorldGen (many targets); CleanHUD VistasEngineHud (`a.u.A()` prefix) | Two owners |
| Towns patches on other modules' internals | `NPC.Nav.Next/Make/BeginBake/CreateMap`, `Patrols.VehicleRoute.Build/Revalidate`, `TerrainWaypoints.Build`, `RoadPlanner.Build`, `CleanHUD.LocationMarkers.Observe`, `QuestMap.Tick`, `POI.PoiLocations.Generate`, `PoiModule.ToolPages`, `Core.UI.CorePages.DrawWorld/ResetSettings/Developer` | Rule violation: must become ModAPI hooks or events |

Not shared, despite a similar name: `Explosion.ShouldApplyForce` is patched only by Patrols (NativeStormProtection.cs:42); Weather only builds a delegate to call it (WeatherNative.cs:61). `MouseLook.OnUpdate` (PlayerBody direct) and `MouseLook.DoMouseLook` (dispatcher) are different methods. The same goes for `Wait.OnEnter` (NPC direct) vs `Wait.OnUpdate` (dispatcher) and `FsmState.OnEnter` (Guns direct) vs `Fsm.EnterState` (dispatcher). docs/harmony.md:289 says Towns patches `Senses.Assign/Look/FoughtFor`, but no such patch exists in the source. Towns uses `Senses` members directly (InteriorPopulation.cs), so the doc is stale.

### 10c. Third-party GUIDs and reflection bridges -> Compatibility

`Core/Plugin.cs:16-42` declares 27 `BepInDependency(SoftDependency)`. Bridges found:

| GUID / mod | files (bridge code) | proposed owner |
|---|---|---|
| `com.denis.apocalypter.apocasetter` (Apocasetter) | Core/ApocasetterBridge.cs (Catalog, GameSkin, InputBlocker, MiniJson, SettingsUI, Updates); Towns/TownDebug.cs:23-30 (SettingsUI/ModEntry) | Core (input/menu bridge, per the brief); the Towns part moves into it |
| `com.denis.apocalypter.apocasaver` (+ `.history`) | Core/ApocasaverExclusion.cs; CleanHUD/ApocasaverLoading.cs (AutosavePresentation, SaveCoordinator, SaveNaming); Patrols/Spawning/Convoy.cs:127 (Plugin, Runner) | Compatibility |
| `apocalypter.worldamp` (Vistas World Gen) | Core/VistasWorldGen.cs; CleanHUD/VistasEngineHud.cs | Compatibility |
| `com.burner.apocalypter.enginesim` (Engine Sim) | EngineSimCompat/*; Performance/World/CarLod.cs (`TypeByName("EngineSim.EngineManager")`, namespace test) | Compatibility |
| `local.apocalypter.chasecamera` (ChaseCamera) | PlayerBody/Camera/ChaseCameraCompat.cs; CleanHUD/ChaseCameraHud.cs; Core/ReplacementGuard.cs:27 | Compatibility |
| `com.apoca.bloodfx` (BloodFX) | Guns/BloodFxCompat.cs | Compatibility |
| `apocalypter.weaponspack` (Weapons Pack) | PlayerBody/WeaponsPackShadows.cs; NPC/WeaponsPackCompat.cs; CleanHUD/WeaponsPackIcons.cs; Guns/WeaponsPackSound.cs | Compatibility (one bridge, four ModAPI consumers) |
| `apocalypter.dogcompanion` (Dog Companion) | NPC/Animals/DogCompanionCompat.cs | Compatibility |
| `com.denis.apocalypter.apocapocket` (Apocapocket) | CleanHUD/ApocapocketInventory.cs; PlayerBody/Body/EquipmentInventory.cs:38 (assembly scan); Core/ReplacementGuard.cs:29; Core/SaveFiles.cs:185 (legacy save dir) | Compatibility (inventory bridge) |
| `com.denis.apocalypter.apocalanguage` (ApocaLanguage) | CleanHUD/ApocaLanguageMenu.cs | Compatibility |
| `com.denis.apocalypter.apocaspawner` (Apocaspawner) | Patrols/WarRig/WarRigFeature.cs:97-103; Survival/StewPot/SpawnerBridge.cs:20-30; Guns/ApocaspawnerDamage.cs; Core/Hooks.cs:144 (Catalog dispatcher); Patrols/Editor/PickupCatalog.cs:22 (cfg read) | Compatibility (catalog registration via ModAPI) |
| `local.immersivemap` (Immersive Map) | CleanHUD/MapFog.cs:21 (slot import), CleanHUD/CleanHudModule.cs:77-83 (cfg migration), Core/SaveFiles.cs:185 | Compatibility (import only); config migration stays with HUD |
| `com.denis.apocalypter.motorcyclemod` (MotorcycleMod) | Patrols/Vehicles/MotorcycleIntegration.cs:19-27 (`Factory.Template` patch); Patrols/Vehicles/ModVehicles.cs:21-50 (generic mod-vehicle factory scan of all assemblies) | Compatibility (vehicle factory registry in Vehicles ModAPI) |
| `local.apocalypter.duststorm` (ApocaDustStorm) | NPC/Hazards/Storm.cs:35 (Plugin, StormModel, StormRunner); Patrols/WarRig/DustStormBridge.cs:34 (StormShelter, PlayerStormHazards patches) | Compatibility |
| `com.burner.apocalypter.sigmamod` (SigmaMod) | Performance/PlayMaker/SigmaFsmStates.cs:22 | Compatibility |
| `com.denis.apocalypter.cavedarkness` (CaveDarkness) | NPC/Detection/CaveDarknessBridge.cs:26 | Compatibility |
| `com.example.apocalypter.talk` (Apocatalker) | Quests/NativeConversation.cs:35, TalkHook.cs (TalkApi); CleanHUD/TalkDialogue.cs:45 (DialogueUI); QuestsCompat/* (dead) | Compatibility |
| `com.denis.apocalypter.partadjustment` (Part Adjustment / Ultimate Tools) | Patrols/Vehicles/Suspension.cs:29 (`PartAdjustment.SuspensionApi`) | Compatibility |
| Apocaraider (assembly) | Patrols/Crews/MeleeWheels.cs:42 (`Apocaraider.MeleeWheels`) | Compatibility (or delete if Apocaraider is dropped) |
| `com.fived.apocalypter.fallout_nv_radio` (Radio) | Core/ReplacementGuard.cs:26 (recommended-mods notice) | Core (notice list) |
| Replaced-standalone GUIDs (`apocaplayer, apocaclimber, npcai, womenofwasteland, gunplay, apocaunloader, gunplayhud, cleanhud, apocapatrol, warrig, merchantspotter, healthstacks, itweenguard, stewpot, apocaraider, local.apocalypterquests`) | Core/Plugin.cs:16-42; ConflictsWith in each module; Core/ReplacementGuard.cs; Quests/Hire.cs:19-20 (dead consts) | Core (ReplacementGuard / ConflictsWith); not Compatibility |
| Game/engine internals via reflection (not mods) | Core/WorldHistory.cs (`Den.Tools.ObjectsPool+Pool`); Performance/Rendering/PostFx.cs (PostProcessing, DawidMoza); NPC/Detection/Senses.cs:546 (`AzureTimeController`); NPC/NpcModule.cs (`ActivateGameObjects`); Quests/Plugin.cs:2076-2267 (ES3 by name) | Stay in their modules (or Core); Quests' ES3 reflection should become typed |

### 10d. Duplicated utility code -> one Core implementation

| utility | copies (lines) | evidence | proposal |
|---|---|---|---|
| Minimal JSON reader/writer (`static class Json`) | Towns/GasStation/Json.cs (117), Survival/StewPot/Json.cs (118), NPC/Women/Json.cs (117), PlayerBody/Body/Json.cs (117), Patrols/WarRig/Json.cs (117), CleanHUD/Json.cs (122), Patrols/Vehicles/CarTemplateFile.cs:201 (`public static class Json`), Patrols/Editor/EditorData.cs:166 (`StoreJson` writer); plus Apocasetter's MiniJson by reflection (Core/ApocasetterBridge.cs) | NPC=PlayerBody=WarRig=Towns copies are identical apart from the namespace (diff 0 lines); Survival differs by 1 line | `Core/Json.cs`: Parse (Dictionary/List/double/string/bool/null) + Write; the CarTemplate/StoreJson writer API on top |
| GLB static mesh loader (`Glb`, `GlbModel`) | Towns/GasStation/Glb.cs (229), Survival/StewPot/Glb.cs (227), Patrols/WarRig/Glb.cs (226) | 9-10 differing lines between copies | `Core/Assets/Glb.cs` |
| glTF skinned loader (`Gltf`) | NPC/Women/Gltf.cs (417), PlayerBody/Body/Gltf.cs (417) | 2 differing lines | `Core/Assets/Gltf.cs` (Presentation is the only consumer after the merge) |
| Bindposes | NPC/Women/Bindposes.cs (36), PlayerBody/Animation/Bindposes.cs (36) | Identical | Core (or Presentation once NPC bodies move there) |
| WAV decoders | Core/VoiceAudio.cs:9 `LoadWav` (52); Guns/Tracers/Wav.cs (61); NPC/Women/Wav.cs (61; identical to Guns); Patrols/Crews/Rider.cs:1085 `Wav`; Quests/Voice.cs:125 `ReadWav`; PlayerBody/Body/FemalePain.cs already uses Core | 5 implementations | `Core.VoiceAudio.LoadWav(path, name, gain/targetRms)`; delete the 4 others |
| Async audio via `UnityWebRequestMultimedia` | Survival/StewPot/Stew.cs:274 (MP3), NPC/Women/Voice.cs:87 (OGG), Weather/WeatherThunder.cs:32 (MP3), Weather/WeatherAudio.cs:38 (WAV), HUD/HitFeedback/HeadshotAudio.cs:80 | 5 near-identical coroutines | `Core.Audio.LoadAsync(path, AudioType, callback)` |
| PNG/texture loading (`Texture2D.LoadImage`) | 18 sites: Core/UI/Ledger.cs, SaveLibraryPage.cs, Skin.cs; CleanHUD/Assets.cs, LoadingScreen.cs, MapIcons.cs, Menu.cs; HUD/Crosshair/CrosshairController.cs; NPC/Women/Gungirl.cs; Patrols/Vehicles/Paint.cs, WarRig/Factory.cs; PlayerBody/Body/CharacterPortraits.cs, Model.cs, Vehicles/VehicleHud.cs; Quests/TalkHook.cs; Survival/StewPot/Factory.cs; Towns/TownGasStation.cs, TownWalls.cs | Each sets its own filter/wrap/mipmap/hideFlags | `Core.Assets.LoadTexture(path or resource, linear, mipmaps, readable)` with a cache and Destroy on shutdown |
| Key/KeyCode helpers | PlayerBody/KeyMap.cs (76) = Quests/KeyMap.cs (74); CleanHUD/Keys.cs (`KeyCatalog`, Enum.Parse); PlayerBody/GameBindings.cs, Vehicles/IgnitionControl.cs | Identical KeyMap twice (AGENTS 5.7 already asks to move it) | `Core.KeyMap` in ModuleConfig/Hotkeys |
| Scene / world scanners (sliced root walks) | Quests/QuestWorld.cs; CleanHUD/SceneCache.cs (110); EngineSimCompat/EngineDiscovery.cs; Survival/StewPot/Runtime.cs ("Existing pots" 500 us); NPC/Navigation/Nav.cs; NPC/Women/Gungirl.cs; HUD/Crosshair/CrosshairController.cs; Patrols/Traffic/TrafficLocations.cs; Patrols/Persistence/Persistence.cs (all `GetRootGameObjects`); `TerrainTile.OnTileApplied` subscribers: Towns/TownsModule.cs:107, POI/PoiModule.cs:59, Performance/PerformanceModule.cs:84, Quests/QuestWorld.cs:42, CleanHUD/LocationMarkers.cs:78, Patrols/Traffic/TrafficRoads.cs:99 | 9 walkers, 6 tile hooks | `Core.WorldIndex`: one sliced walker over scenes and applied tiles, with registered component/name predicates (POI roots, mapIcon, RealisticEngineSound, AudioListener, stew pots, crosshair...), events `Added/Removed`, swept on destroy, cleared on scene load |
| PlayMaker FSM indexes over `PlayMakerFSM.FsmList` | Performance/PlayMaker/FsmRegistry.cs (PM1 index), Quests/QuestWorld.cs:91-98, Quests/CrewHistory.cs:37, Quests/FactionDump.cs, Towns/InteriorPopulation.cs, Towns/TownGasStation.cs, Patrols/Traffic/TownTraffic.cs, Weather/WeatherNative.cs, Core/SaveCombat.cs | Each rescans the full list | Promote the PM1 registry to Core: `FsmIndex.ByName("Health")`, `ByOwner(go)` |
| FSM lookup helpers | Core `FsmUtil.Find` (GameRefs.cs:90) vs local: Survival/StewPot/Factory.cs:27, Guns/Ammo/PerGunAmmo.cs:52 (wrapper), CleanHUD/Bindings.cs:175 (wrapper), Patrols/Vehicles/MotorcycleIntegration.cs:65, Patrols/WarRig/WarRigFeature.cs:74, Patrols/Spawning/Patrol.cs:1175, Quests/Hire.cs:323, Quests/Takeover.cs:343, Quests/QuestWorld.cs:111, HUD/HitFeedback/VanillaHits.cs:88-122, Quests/Game.cs:521-537 (`FindInt/FindFloat/FindString`); 190 inline `GetComponents<PlayMakerFSM>` sites (Patrols 88, NPC 29, Guns 19, PlayerBody 19, Quests 16, Towns 10, CleanHUD 6, Core 2, HUD 1) | | `Core.FsmUtil.Find(go, name)` with a per-instance cache, plus `FsmUtil.Float/Int/String/Go(fsm, var)` |
| Save-list registration (`itemNameID` + `ArrayList_Items`) | Core/InventorySupplies.cs:65-69; Quests/Game.cs:104-153 (reflection); Patrols/WarRig/WarRigFeature.cs:59; Patrols/Spawning/Patrol.cs:1596; NPC/Women/Gungirl.cs:602; Guns/Ammo/AmmoMechanics.cs:420 (`GameObject.Find`) | 6 implementations | `Core.GameRefs.RegisterSaved(go, baseName)` |
| Game clock (DayTime FSM) | Quests/Game.cs:591; CleanHUD/MessageLog.cs; Patrols/Traffic/TrafficReinforcements.cs; Patrols/Spawning/Patrol.cs; Performance/Content/HudFsms.cs | | `Core.GameRefs.GameHours/Day` |
| Prefab-by-name catalogs | Patrols `Prefabs` (Spawning/Patrol.cs:1200+, "Patrols.Prefab cache" job); Towns/PoiPropCatalog.cs, PoiSpawnCatalog.cs (`FindObjectsOfTypeAll`); Quests/Game.cs:159 `FindPrefab`, Takeover.cs:318 `ScanForPrefab`; Patrols/Editor/PickupCatalog.cs | | `Core.PrefabCatalog` (ES3 ref manager + resources, warmed at world-ready) |
| `(Clone)` base-name strippers | Quests/Game.cs:617 `BaseName`, CrewHistory.cs:24, Takeover.cs; NPC/Navigation/Nav.cs; Performance/Rendering/RenderShared.cs; Core/SaveCleanup.cs | | `Core.Names.Base(name)` (non-allocating compare variant) |
| ES3 save-key writers (disk + cache + AfterSave) | Quests/Plugin.cs:2072-2405 (reflection); Towns/TownSaveRecord.cs:27-48, TownGasStation.cs:372-383, PoiDressMarkers.cs:111-128; Guns/Ammo/PerGunAmmo.cs:246-261; Patrols/Persistence/Persistence.cs; Core/SaveCleanup.cs, SaveSystem.cs | Same "write to cache, StoreCachedFile, verify on disk" logic repeated | `Core.SaveBus.Key(name).Write(string)/Read()` + `SaveBus.Sidecar(id, ext)` |
| Optional-API delegate builders | Core/OptionalAccess.cs (Cecil); `Expression.Compile` in EngineSimCompat (4), CleanHUD (10), PlayerBody (10), NPC (6) | | `Core.OptionalAccess` only |
| Config "import each missing entry" migrations | Quests/QuestConfigMigration.cs; Survival (per docs/modules.md:64); CleanHUD (docs/modules.md:122); NPC/Women/NpcModule.Women.cs:14-30; Towns/TownSettings.cs (`...wildwasteland.towns.cfg`) vs Core `ModuleConfig.Migrate` (first run only) | | `ModuleConfig.MigrateMissing(entry, file, section, key, convert)` |

### 10e. Config sections per module (for the config migration when modules merge)

Files: `com.denis.apocalypter.wildwasteland.cfg` (F6 "public"), `config/WildWasteland/settings.cfg` (Main), `config/WildWasteland/tools.cfg` (Tools). Sources: docs/defaults-rc1.md (RC1 snapshot) plus a source scan of `cfg.Player/Tool/Hotkey`, helper binds and direct `ConfigFile.Bind` for modules added after RC1. Counts are entries where known.

| module (current) | settings.cfg sections | tools.cfg sections | [Keys] entries (settings.cfg) | target module / migration note |
|---|---|---|---|---|
| Core | `[Debug] VerboseLog, Developer mode`; `[System / Saving]` (8), `[System / Cleanup]` (5); `[<Id>] Enabled` for every module (ModuleHost.cs:56). Public cfg: `[General] Apocasetter, Enable Debug`, `[Keys] In-Game Settings Button` | `[Core] Frame budget (ms)` | | Core. `[System / *]` is the Saves module, keep the names |
| PlayerBody | `[PlayerBody]` (16), `[PlayerBody / Climbing]`, `[PlayerBody / Camera culling]`, `[PlayerBody / Vehicles]` | `[PlayerBody / Animation]` (20), `/ Animation (fallback clips)` (12), `/ Camera` (9), `/ Camera culling`, `/ Car seat` (5), `/ Climbing` (5), `/ Editors` (9), `/ First person` (7), `/ ModAPI`, `/ Model` (4) | Observe (third person), Climb (alternative key), Cassette start/stop, Cassette volume down/up | Presentation; `/ Vehicles` ignition/cassette goes to Vehicles; editors go to Developer |
| NPC | `[NPC]` (17), `[NPC / Detection]`, `[NPC / Pathfinding]`, `[NPC / Women of Wasteland]`, `[NPC / Gungirl]`, `[NPC / Shoota]` | `[NPC / Ambush, Behaviour, Brain, Burrow, Debug, Detection, Dogs, Hunt, Idle, Movement, Nav, Nests, NpcAim, Pounce, Senses (46), POI pathfinding setup]` | Shout, Shout modifier, Spawn female raider | NPCAI (id change: prefix `NPC` -> alias); female model/texture entries go to Presentation |
| Guns | `[Guns]` (13), `[Guns / Projectiles]` (7), `/ Ranges` (6), `/ Ammo`, `/ Bosses` | `[Guns / Tracers]` (11), `/ Ballistics` (12), `/ NPC aim` (5), `/ Melee` (2), `/ Ammo` | Guns: unload fallback key | Gunplay (prefix change `Guns`) |
| HUD | `[HUD]` (8) | `[HUD / Hit feedback]` (7), `/ Crosshair` (2), `/ Headshot sound` (2) | | HUD. **Collision:** CleanHUD's `[CleanHUD / HUD]` will merge into `HUD`, which gives a `[HUD / HUD]` sub-section next to the root `[HUD]`, and two `Enabled` keys (`[HUD] Enabled`, `[CleanHUD] Enabled`) |
| CleanHUD | `[CleanHUD]` (Enabled), `[CleanHUD / HUD]` (13), `[CleanHUD / Map]` (9), `[CleanHUD / Menu]` (4); reads `[Patrols / Traffic tuning]` once for migration (CleanHudModule.cs:72) | | | HUD; keep sub-names, alias the prefix |
| Patrols | `[Patrols]` (19), `/ Balance` (3), `/ Combat` (3), `/ Merchants` (3), `/ Spawning` (6), `/ World traffic` (3) | `[Patrols / AI]` (26), `/ Audio`, `/ Cleanup` (3), `/ Combat tuning` (23), `/ Debug` (7, also holds Core's QuietFsm switch), `/ Driving` (3), `/ Loot` (2), `/ Pose` (14), `/ Pose.<Human>` x7 (Boltjaw, Flexa, Lugnut, Scrud, Sprokka 24 each; Scraffa, Spanna 18), `/ Spawner`, `/ Traffic tuning` (12), `/ Turret pose` (5) | Patrol editor, Template recorder, Bus passenger pose editor, Merchant item editor | Split: Patrols keeps traffic/spawning/combat; `/ Loot` goes to Loot; `/ Pose*` and `/ Turret pose` go to Presentation/Developer; editors and keys go to Tools/Developer; War Rig entries go to Vehicles; `/ Debug` "Hide missing-FSM warnings" goes to Core |
| POI | `[POI]` (Enabled), `[POI / World] Spawn chance (%)` (also read by Towns, TownPoiSettings.cs:23) | | | POI. **Collision** with `[Towns / World]` if merged under one prefix |
| Towns | all in settings.cfg via `file.Bind` (bypasses ModuleConfig.Player/Tool and TunedSettings): `[Towns]` Enabled, `[Towns / Editors]` (1, POI dresser key), `[Towns / Debug]` (8+), `[Towns / World]` (3), `[Towns / Generation]` (6+), `[Towns / Assets]` (2) | none | (POI dresser key lives in `[Towns / Editors]`, not `[Keys]`) | POI (+Tools for the dresser/editors). Legacy import from `com.denis.apocalypter.wildwasteland.towns.cfg`. Debug/tuning entries are in the player file, against AGENTS 5.7 |
| Weather | `[Weather]` (Enabled), `[Weather / Map]` (1), `[Weather / Storms]` (4), `[Weather / Colors]` (4) | | | Survival (Weather merged in): prefix change; keep sub-names `Weather Map/Storms/Colors` to avoid a collision with HUD `Map` |
| Survival | `[Survival]` (Enabled) | `[Survival / Model]`, `[Survival / Stew Pot]` (4), `[Survival / Debug]` (2) | (native Use rebinding) | Survival |
| Quests | `[Quests]`, `[Quests / Generated]`, `[Quests / Items]`, `[Quests / Voice]`, `[Quests / RepVendor]`, `[Quests / Hire]` | `[Quests / Legacy, Advanced, Items, Quests, Kills, Goto, Takeover, Deliver, Reputation, RepVendor, Factions, Hire, Voice, Debug]` | Quests: talk, Quests: quest log, Quests: dismiss follower | Quests; Hire tuning stays here even if the code moves (bind via ModAPI settings) |
| EngineSimCompat | `[EngineSimCompat]` Enabled only | | | Compatibility (`[Compatibility / Engine Sim] Enabled`, migrate the old Enabled) |
| Performance | `[Performance]` Enabled | `[Performance / Preset]`, `/ PlayMaker`, `/ Game logic`, `/ Rendering`, `/ World` (one mode entry per feature, 42), `/ Tuning`, `/ Telemetry` | | Core (absorbs Performance): keep the `Performance / ...` section names under the Core module (do not rename to `Core / ...`, values would be lost) |
| QuestsCompat | none bound (possible orphan `[QuestsCompat] Enabled` in older files) | | | Delete; leave the orphan |

Migration rule proposal: give `ModuleConfig` a `LegacyPrefix` (or a per-sub alias table), so a merged module keeps binding the old section strings verbatim. Then no value migration is needed and the config harness still checks for (section, key) collisions. Only the module `Enabled` switches need a real migration (one per absorbed module -> a feature toggle in the new module).

### 10f. Save data per module: ES3 keys, SaveTypes aliases, sidecars

| module | ES3 keys (in the slot file) | SaveTypes alias / saved component types | prefab ids (GetPrefab) | sidecars / folders |
|---|---|---|---|---|
| Core | `WildWasteland.SaveCommit` (SaveSystem.cs:17), `WildWasteland.CleanupProtected` (SaveCleanup.cs:28) | Installs aliases (SaveTypes.cs:31-36): `WarRig,WarRig -> WildWasteland.Modules.Patrols.WarRig`; `Apocapatrol -> ...Patrols`; `Apocaplayer -> ...PlayerBody`; `NPCAI -> ...NPC`; `Gunplay -> ...Guns`; `GunplayHUD -> ...HUD` | | Save library/world companions (SaveFiles.cs: World/Meta/Image paths, `.bak1..10`, `old-N.bin`/`new-N.bin` transaction files); legacy participant dirs `config/{Apocapatrol/Saves, NPCAI/Saves, Apocaraider/Saves, Apocaunloader/Saves, Apocapocket, ApocalypterQuests_saves, ImmersiveMap/slots}` (SaveFiles.cs:185) |
| Patrols | `Apocapatrol.Patrol` (schema 14, magic `APOC`, WDR2 trailing extension; Persistence.cs:207) | Saved components resolve through the `Apocapatrol`/`WarRig` aliases. **Saves made by WW itself store `WildWasteland.Modules.Patrols[.WarRig].X, WildWasteland` type strings: moving these classes to Vehicles/POI namespaces needs NEW aliases from the current WW namespaces, not just the old ones.** | 735710202610050001 (motorcycle), 735710202610090001 (War Rig) | `config/WildWasteland/Patrols/Saves/<slot>.bin(.bak)`; legacy `config/Apocapatrol/Saves/`; road cache `config/Apocapatrol.roads/<world>/<x>_<z>.roads` (v3) |
| Guns | `Apocaunloader.Magazines` (`|1` format; PerGunAmmo.cs:17) | `Gunplay` alias (unused today) | | none |
| NPC | none | `NPCAI` alias | | `config/WildWasteland/NPC/Saves/<slot>.senses.txt` (v1); legacy `config/NPCAI/Saves`, `config/Apocaraider/Saves`; nav cache `config/WildWasteland/NPC/NavMaps/Custom` (also read by Towns, TownBakeData.cs:41-42); women identity stored inside crew names |
| PlayerBody | none | `Apocaplayer` alias (unused) | | Data, not per slot: `config/WildWasteland/PlayerBody/` (poses, grips, `WheelGrips/<key>.txt`) |
| HUD | none | `GunplayHUD` alias (unused) | | none |
| CleanHUD | none (native SaveSettings.es3/Settings.xml/PlayerPrefs) | | | `config/WildWasteland/CleanHUD/Saves/<slot>.fog.bin`, `<slot>.locations.txt`; imports `config/ImmersiveMap/slots/<stem>_seed*` once |
| POI | none | | | `config/WildWasteland/POI/Saves/<slot>.poi.txt` (atomic, AfterSave) |
| Towns | `WildWasteland.Towns.World.v1` (TownSaveRecord.cs:11), `WildWasteland.Towns.GasStations.v1` (TownGasStation.cs:44), `WildWasteland.Towns.PoiDressMarkers.v1` (PoiDressMarkers.cs:33) | | | `config/WildWasteland/Towns/Saves/<slot>.towns.bin` (TownsModule.cs:707; Core SaveSystem.cs:264 skips it when the ES3 record exists); `PoiDressLayout` JSON per POI type |
| Quests | `ApocalypterQuests` (AQ1) | none | | `config/WildWasteland/Quests/Saves/<slot>.crew.txt` (+ `QuestsCompat/Saves` fallback); legacy `config/ApocalypterQuests_saves/<slot>.txt` |
| Survival | none (native fields) | | 7392481500621300201 (Stew Pot) | none |
| Weather | none (native SandStorm save arrays/IDs retained) | | | `config/WildWasteland/Weather/diagnostic-snapshot.bin` (manual diagnostics, not per slot) |
| Performance, EngineSimCompat, QuestsCompat | none | | | Performance writes census/LOD exports only |

Constraints for the refactor:
1. ES3 keys and sidecar folder names must not change. When a module is renamed (e.g. `Patrols` -> `Vehicles`/`POI`, `NPC` -> `NPCAI`), keep reading `config/WildWasteland/<OldId>/Saves`, because `Assets.ModuleConfigDir(Id)` is derived from the module id. Also update the hard-coded companion list in Core/SaveFiles.cs:189 and the Towns special case in SaveSystem.cs:264.
2. Any namespace move of a class that ends up in a save (Patrols vehicle/War Rig components) needs a `SaveTypes.Alias("WildWasteland", "WildWasteland.Modules.Patrols", "<new namespace>")` entry, ordered longer prefixes first.
3. The prefab ids (motorcycle, War Rig, Stew Pot) are saved in worlds and must stay registered on the `GetPrefab` dispatcher, whichever module owns them.
4. Asset-index logical names (`<Id>.<path>`) and plugin folders (`plugins/WildWasteland/<Id>/`) are also keyed by module id.
