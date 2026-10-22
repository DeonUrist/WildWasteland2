# File map - every source file, its owner and destination

Generated from the 7 inventories (`inventory/*.md`, evidence with file:line) plus Denis's decisions (rows marked **D**). Columns: source path (relative to `src/`; `developer/` = repo root), lines, primary target module (+ secondary targets for SPLIT files), action, Wave B owner, notes. Paths of `developer/*.cs` stay in place during Wave A; B03 ports them.

Actions: MOVE = whole file to the target; SPLIT = parts to several targets (notes name the classes/methods); MERGE-INTO = folded into another file/service; REWRITE = moved and rewritten (same behaviour); DELETE = removed (evidence in notes/inventory).

## Summary by primary target

| target | files | lines |
|---|---|---|
| Core | 110 | 19442 |
| NPCAI | 36 | 14687 |
| Presentation | 52 | 14459 |
| Patrols | 45 | 13228 |
| HUD | 40 | 8345 |
| Quests | 14 | 7470 |
| POI | 34 | 6401 |
| Gunplay | 22 | 4829 |
| Tools | 16 | 3811 |
| Vehicles | 20 | 3556 |
| Survival | 21 | 3548 |
| Compatibility | 25 | 3017 |
| Saves | 11 | 1696 |
| DELETE | 17 | 1671 |
| Debug | 11 | 1556 |
| Developer | 3 | 468 |
| Loot | 5 | 463 |
| **total** | 482 | 108647 |

## A1 Core + Performance + developer (107 files, 18239 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Core/ApocasaverExclusion.cs | 34 | Compatibility | MOVE | B17 | MOVE; HandPose handlers (`SaveCompatibility.SetPose/GetPose`) reached through Saves ModAPI |
| Core/ApocasetterBridge.cs | 173 | Core | MOVE | B01 | MOVE; replace `Expression.Compile` (line 41, interpreted on this runtime) with Core access helper |
| Core/Assets.cs | 38 | Core | MOVE | B01 | MOVE |
| Core/AtomicFile.cs | 34 | Core | MOVE | B01 | MOVE (users: Saves, Towns PoiDressLayout, CleanHUD LocationMarkers) |
| Core/ColorGrading.cs | 28 | Core | MOVE | B01 | MOVE |
| Core/DiskResources.cs | 46 | Core | MOVE | B01 | MOVE |
| Core/GameRefs.cs | 99 | Core | MOVE | B01 | MOVE |
| Core/HealthStacks.cs | 145 | Core | MOVE | B01 | MOVE |
| Core/Hooks.cs | 365 | Core | REWRITE | B01 | REWRITE: table-driven registry (one list drives RemoveAll/ApplyUsed/Summary - `FsmEnable` is missing from RemoveAll, see 5.1); optional per-handler timing for Debug; add dispatchers for targets Performance patches twice (3.3) |
| Core/Hotkeys.cs | 78 | Core | MOVE | B01 | MOVE |
| Core/ITweenGuard.cs | 74 | Core | MOVE | B01 | MOVE |
| Core/InventorySupplies.cs | 119 | Core | MOVE | B01 | MOVE; replace `SaveSystem.LocalReady/SinglePlayerSaving/SaveRuntime.Health` (line 19) with SaveBus/GameRefs queries; sliced prefab discovery job registered from SaveRuntime (move registration) |
| Core/LoadingProgress.cs | 67 | Core (+HUD) | MOVE | B01 | MOVE; `MenusBlocked` must read Saves state via SaveBus, not `SinglePlayerSaving` |
| Core/Log.cs | 39 | Core (+Debug) | SPLIT | B01 | SPLIT: `Log` API stays Core (add `Level 0/1/2`); level switch/UI -> Debug; remove `SaveSystem.Fail` call (Log.cs:23-24) -> Saves subscribes to an error event |
| Core/ModuleBase.cs | 69 | Core | MOVE | B01 | MOVE |
| Core/ModuleConfig.cs | 122 | Core | MOVE | B01 | MOVE; delete unused `Legacy.ParseFloat` |
| Core/ModuleHost.cs | 273 | Core | MOVE | B01 | MOVE (add module-state events to ModAPI; `Restart` unused) |
| Core/NativeAccess.cs | 70 | Core | MERGE-INTO | B01 | MERGE-INTO new `Core/Access` (with OptionalAccess, Performance FastReflect, BatchCompiler) |
| Core/OptionalAccess.cs | 36 | Core | MERGE-INTO | B01 | MERGE-INTO `Core/Access` |
| Core/Origin.cs | 51 | Core | MOVE | B01 | MOVE |
| Core/Patches.cs | 154 | Core | MOVE | B01 | MOVE |
| Core/Plugin.cs | 183 | Core | SPLIT | B01 | SPLIT: boot/registration/Core.Frame stay; `ApplyAudioVoices` + `Modules.Patrols.PatrolsModule.AudioVoices` (line 114) -> Core audio setting; Saves install/recover (106-108) -> Saves.Init; VistasWorldGen/ApocasaverExclusion init (105,109) -> Compatibility.Init; fixes list -> Core/Fixes registry |
| Core/QuestServices.cs | 46 | Core | MERGE-INTO | B01 | MERGE-INTO ModAPI (quests/dialogue contracts file) |
| Core/QuietFsm.cs | 49 | Core (+Debug) | MOVE | B01 | MOVE; its switch `On` is bound by **Patrols** (`PatrolsSettings.cs:120`) - give it a Core/Debug key |
| Core/ReplacementGuard.cs | 146 | Core (+HUD) | SPLIT | B01 | SPLIT: `Prepare/Disable/Status/LateConflict/Tick` stay Core; `Draw/WantsNotice/RebuildNotice` -> HUD (uses CleanHUD GlyphFont/AccentTheme/Assets, lines 35-36, 113-116) via Core notice API |
| Core/Runner.cs | 65 | Core | MOVE | B01 | MOVE |
| Core/SaveBus.cs | 126 | Core | SPLIT | B01 | SPLIT: watcher/StateChanged/AfterSave/Slot/ReportFailure stay Core; `CancelSave`, `SaveCancelled`, `CleanupSettings/DrawCleanupSettings` (Patrols UI hook) -> Saves |
| Core/SaveCleanup.cs | 166 | Saves | MOVE | B04 | MOVE |
| Core/SaveCombat.cs | 83 | Saves | MOVE | B04 | MOVE; consume NPCAI/Gunplay combat via ModAPI event instead of `Services.VehicleCombatActive`/`ReportCombat` |
| Core/SaveCompatibility.cs | 61 | Saves (+Compatibility) | SPLIT | B04 | SPLIT: `BeforeSave/AfterSave/BeforeLoad/AfterLoad/Stable/SavePoses` -> Saves; `SetPose/GetPose` (Apocasaver API takeover) -> Compatibility; `VehicleLoadPlacement.LoadedStance` call (42) -> Saves "PlayerVariableLoaded" event |
| Core/SaveFiles.cs | 219 | Saves | MOVE | B04 | MOVE; replace hard-coded module sidecars in `Participants` (183-190) with Core sidecar registry API |
| Core/SaveRuntime.cs | 149 | Saves (+HUD, Core) | SPLIT | B04 | SPLIT: menu event/launch/IsSaveFile/ActiveFile -> Saves; `DrawSaving` (uses CleanHUD GlyphFont, 113) -> HUD; `Surface` -> Core util (VehicleLoadPlacement); `Health/InCar` -> GameRefs queries; `InventorySupplies.Install()` (40) -> Core/Fixes |
| Core/SaveSystem.cs | 433 | Saves | MOVE | B04 | MOVE; `Modules.CleanHUD.MessageHook.Push` (121) -> HUD notify via ModAPI; Towns key check (264-265) -> sidecar registry |
| Core/SaveTerrain.cs | 48 | Saves | MOVE | B04 | MOVE |
| Core/SaveTypes.cs | 74 | Core (+Saves) | MOVE | B01 | MOVE; must gain aliases for every namespace the refactor renames (section 9) |
| Core/SaveWarning.cs | 34 | Saves (+HUD) | SPLIT | B04 | SPLIT: `Show/phase` -> Saves; `Draw` (uses `CleanHud.Hud.SaveWarningAnchor`, `GlyphFont`) -> HUD |
| Core/Scheduler.cs | 239 | Core | MOVE | B01 | MOVE; fix `RemoveAll` not marking `Removed` (5.1) |
| Core/Services.cs | 402 | Core | REWRITE | B01 | REWRITE into typed ModAPI + event bus (split contracts per domain; raw ladder fields -> query/event) |
| Core/SinglePlayerSaving.cs | 107 | Saves | MOVE | B04 | MOVE |
| Core/TunedSettings.cs | 57 | Core | MOVE | B01 | MOVE; rename resource prefixes `CleanHUD.DefaultSettings.*` / `CleanHUD.DefaultProfiles.*` (20, 44) to Core-owned names (keep old names readable) |
| Core/UI/CorePages.cs | 513 | Core (+Debug) | SPLIT | B01 | SPLIT: `Register/Modules/ResetSettings` (generic part)/`DrawDebugPage`/Category plumbing -> Core shell; `DrawPlayer/BodySelection` -> Presentation contribution; `DrawWorld/NavigationSettings/CompassDetail` -> Patrols/HUD/POI/Survival contributions; `DrawHud/CrosshairSelection/FeedbackSwitch` -> HUD/Gunplay contributions; `Deb... |
| Core/UI/EditorSession.cs | 63 | Core | MOVE | B01 | MOVE (make `Acquire(bool,bool)` public so Debug needs no reflection) |
| Core/UI/Ledger.cs | 440 | Core | MOVE | B01 | MOVE (default `Header(width)` "APOCAPATROL" is Patrols-specific) |
| Core/UI/SaveLibraryPage.cs | 222 | Saves | MOVE | B04 | MOVE (drawn in Core shell) |
| Core/UI/Skin.cs | 324 | Core | MOVE | B01 | MOVE; DELETE unused `PlankButton`, `Frame`, `PitBox`, `SlotBox`, `HazardStrip` (0 call sites) |
| Core/UI/ToolPage.cs | 412 | Core | MOVE | B01 | MOVE |
| Core/UI/ToolWindow.cs | 263 | Core | MOVE | B01 | MOVE; replace CleanHUD refs (lines 4, 24-26, 147-155, 175-183) with a Core "theme/glyph font" service provided by HUD |
| Core/VehicleLoadPlacement.cs | 120 | Core | MOVE | B01 | MOVE; triggered by Saves hooks (`SaveSystem.LoadedPath`, `SaveCompatibility.AfterLoad`) -> needs SaveBus events; uses `SaveRuntime.Surface/Health` |
| Core/VistasWorldGen.cs | 203 | Compatibility | MOVE | B17 | MOVE; publish `PlaceEntered`, `TerrainEdited`, `RoadLines/RoadRevision/RoadIdentity`, `Present`, `BannerHeight`, `CleanBanners` through Core ModAPI (consumers: Towns x6 files, CleanHUD, Patrols) |
| Core/VoiceAudio.cs | 52 | Core | MOVE | B01 | MOVE (consolidation target for other WAV decoders, e.g. Quests `ReadWav`) |
| Core/WorldHistory.cs | 174 | Saves | MOVE | B04 | MOVE |
| Core/WorldWork.cs | 33 | Core | MOVE | B01 | MOVE (read Saves busy state through SaveBus) |
| Modules/Performance/Content/Coyotes.cs | 147 | Core (+NPCAI) | MOVE | B02 | MOVE |
| Modules/Performance/Content/Culler.cs | 329 | Core | MOVE | B02 | MOVE; replace `PatrolsBridge` with ModAPI culling-protection query |
| Modules/Performance/Content/FsmEdits.cs | 324 | Core | MOVE | B02 | MOVE (FsmPause -> Core FSM service shared with FS1) |
| Modules/Performance/Content/HudFsms.cs | 141 | Core (+HUD) | MOVE | B02 | MOVE |
| Modules/Performance/Content/ItemFsmFixes.cs | 109 | Core | SPLIT | B02 | SPLIT: DuplicateRigidbodyFeature -> Core/Fixes; DataFsmRestartFeature -> Core/Optimization/Content |
| Modules/Performance/Content/LightFlicker.cs | 121 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Content/PatrolsBridge.cs | 64 | DELETE | DELETE | - | DELETE after adding `Services.Culling { bool Protected(GameObject); void BeforeFarDelete(GameObject) }` (requested in its own header) provided by Patrols |
| Modules/Performance/Content/PlayerCapsule.cs | 84 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Content/PollFeatures.cs | 62 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Content/PollSleep.cs | 289 | Core | MOVE | B02 | MOVE (replace `IsAlive` AllJobs scan, line 51/58) |
| Modules/Performance/Content/ProxyAction.cs | 64 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Content/VehicleReflection.cs | 146 | Core (+Vehicles) | MOVE | B02 | MOVE |
| Modules/Performance/Framework/Feature.cs | 151 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Framework/PerformancePage.cs | 81 | Core (+Debug) | SPLIT | B02 | SPLIT: preset UI -> Core settings page; `Telemetry.Draw`/`CorePages.Performance` calls + `DrawDeveloper` stats -> Debug |
| Modules/Performance/Framework/Telemetry.cs | 188 | Debug | MOVE | B03 | MOVE (merge with developer Measurements, see 1.4) |
| Modules/Performance/PerformanceModule.cs | 209 | Core | REWRITE | B02 | REWRITE as Core optimization registry (no module switch hop; explicit feature list instead of `Assembly.GetTypes()` discovery, line 62) |
| Modules/Performance/PlayMaker/ActionDataPort.cs | 383 | Core | MOVE | B02 | MOVE (pin to PlayMaker version; Debug.LogError at 358 = vanilla message) |
| Modules/Performance/PlayMaker/BatchCompiler.cs | 99 | Core | MERGE-INTO | B02 | MERGE-INTO `Core/Access` |
| Modules/Performance/PlayMaker/CompileQueue.cs | 84 | Core | MERGE-INTO | B02 | MERGE-INTO `Core/Access` (fix `_job.Removed` reliance, line 36) |
| Modules/Performance/PlayMaker/EventDispatch.cs | 296 | Core | MOVE | B02 | MOVE |
| Modules/Performance/PlayMaker/FastActionLoadFeature.cs | 237 | Core | MOVE | B02 | MOVE |
| Modules/Performance/PlayMaker/FastCallMethodFeature.cs | 137 | Core | MOVE | B02 | MOVE |
| Modules/Performance/PlayMaker/FastPropertyFeature.cs | 479 | Core | MOVE | B02 | MOVE |
| Modules/Performance/PlayMaker/FastReflect.cs | 401 | Core | MERGE-INTO | B02 | MERGE-INTO `Core/Access` (duplicates NativeAccess/OptionalAccess) |
| Modules/Performance/PlayMaker/FsmEventSkip.cs | 62 | Core | MOVE | B02 | MOVE |
| Modules/Performance/PlayMaker/FsmRegistry.cs | 185 | Core | MOVE | B02 | MOVE to Core as public `FsmIndex` (other modules scan `PlayMakerFSM.FsmList`: Towns x2, Weather, Quests x3, Patrols TownTraffic) |
| Modules/Performance/PlayMaker/FsmSleepFeature.cs | 309 | Core | MOVE | B02 | MOVE; use `FsmPause` ownership bits (today a second ManualUpdate owner, see 5.4) |
| Modules/Performance/PlayMaker/GuiLoopFeature.cs | 426 | Core | MOVE | B02 | MOVE (fix `_guard.Removed`, line 83) |
| Modules/Performance/PlayMaker/MousePickFeature.cs | 170 | Core | MOVE | B02 | MOVE (shares `ActionHelpers.DoMousePick` with PlayerBody PickAssist postfix) |
| Modules/Performance/PlayMaker/SigmaFsmStates.cs | 33 | Compatibility | MOVE | B17 | MOVE; cache `Chainloader.PluginInfos.ContainsKey` once (line 22 runs per getter call) and only patch when SigmaMod is loaded |
| Modules/Performance/PlayMaker/StringConvertFeature.cs | 125 | Core | MOVE | B02 | MOVE |
| Modules/Performance/PlayMaker/TriggerProxyFeature.cs | 141 | Core | MOVE | B02 | MOVE (note: skipped events no longer reach `Hooks.ProcessEvent` subscribers) |
| Modules/Performance/Rendering/AnimatorCulling.cs | 110 | Core (+Presentation) | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/CameraCulling.cs | 182 | Core | MOVE | B02 | MOVE (no overlap with PlayerBody "CameraCulling" = occlusion cutaway; rename to avoid confusion) |
| Modules/Performance/Rendering/CullLods.cs | 187 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/GcSlice.cs | 51 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/GeneratedLodExport.cs | 124 | Tools | MOVE **D** | B16 | RC9 LOD authoring export -> Tools (developer authoring). |
| Modules/Performance/Rendering/GeneratedLodPack.cs | 217 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/GeneratedLods.cs | 368 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/LightPolicy.cs | 267 | Core | MOVE | B02 | MOVE (remove 10 s `FindObjectsOfType<Light>`, line 190) |
| Modules/Performance/Rendering/ParticleTuning.cs | 130 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/PostFx.cs | 299 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/RenderShared.cs | 526 | Core | MOVE | B02 | MOVE (SaveGuard should use the Saves "before serialize" event) |
| Modules/Performance/Rendering/SmallCasters.cs | 110 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/SunShadows.cs | 113 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/TerrainDetail.cs | 119 | Core | MOVE | B02 | MOVE |
| Modules/Performance/Rendering/TextureQuality.cs | 48 | Core | MOVE | B02 | MOVE |
| Modules/Performance/World/CarLod.cs | 322 | Core (+Vehicles) | MOVE | B02 | MOVE; replace namespace sniffing (193-194) with ModAPI "vehicle controlled by a module" query |
| Modules/Performance/World/WorldSettings.cs | 404 | Core | MOVE | B02 | MOVE (6 features; consider one file per feature) |
| developer/Dashboard.cs | 218 | Debug | REWRITE | B03 | REWRITE as Debug ToolPages in Core shell |
| developer/Diagnosis.cs | 146 | Debug | MOVE | B03 | MOVE; JSON writer -> Core Json util |
| developer/Instrumentation.cs | 218 | Debug | MOVE | B03 | MOVE; install through `Core.Patches` with owner Debug instead of `new Harmony` (line 64) |
| developer/Measurements.cs | 116 | Debug | MERGE-INTO | B03 | MERGE-INTO Debug FrameStats (with Telemetry) |
| developer/Plugin.cs | 185 | Debug | REWRITE | B03 | REWRITE inside Debug (Scheduler job + Core UI shell; no own MonoBehaviour, no reflection into `EditorSession.Acquire`, line 77) |
| developer/Reports.cs | 83 | Debug | MOVE | B03 | MOVE |

## A2 PlayerBody (55 files, 13946 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/PlayerBody/Animation/AimLift.cs | 249 | Presentation (+Developer) | SPLIT | B05 | SPLIT: Default/Builtin/Load/Save/SaveIfDirty/Get/Resolve/Key/Own/IsAimClip/scalar `Elbow` (l.196) → Pres. Set/ArmKeys/EndArmKeys/NudgeArm/ResetArm/Edit* → Developer. `Elbow(double[]...)` (l.227) → DELETE (only for tools/locotest, no in-game caller). |
| Modules/PlayerBody/Animation/AimTransition.cs | 83 | Presentation | MERGE-INTO | B05 | MERGE-INTO a Pres FP-weapon-motion unit with FirstPersonSway.cs and Guns/Camera/WeaponTransition.cs. |
| Modules/PlayerBody/Animation/AnimationService.cs | 118 | Presentation | REWRITE | B05 | REWRITE as the Pres ModAPI service: id-based character handles and queries instead of Animator references (§7). |
| Modules/PlayerBody/Animation/Anims.cs | 69 | Presentation | MOVE | B05 | MOVE; load at Init (Sliced/async), not on first `Get` mid-play. |
| Modules/PlayerBody/Animation/Bindposes.cs | 36 | Core (+Presentation) | MERGE-INTO | B01 | MERGE-INTO one copy. It is byte-identical to NPC/Women/Bindposes.cs apart from the namespace. |
| Modules/PlayerBody/Animation/BuiltinPoses.cs | 811 | Presentation | MOVE **D** | B05 | Built-in clip/pose table for the shared character driver. |
| Modules/PlayerBody/Animation/CharPlan.cs | 495 | Presentation | MOVE | B05 | MOVE. Becomes the **single** decision model, also used for the player. |
| Modules/PlayerBody/Animation/CrawlBody.cs | 65 | Presentation | MOVE | B05 | MOVE (generalise per character). Empty `partial void CaptureCrawlMotion()` (l.15, called l.48) → DELETE. |
| Modules/PlayerBody/Animation/Crawling.cs | 92 | Presentation | SPLIT | B05 | SPLIT: Clip/bundle load → Pres. Velocity/Button/HeldButton/WeaponEvent/Stance/Holster/Tick → player-movement owner (§8). `DownedProbe` (l.11, never assigned) and `InstallMultiplayer` (l.38, no body) → DELETE (MP stubs). |
| Modules/PlayerBody/Animation/GunPose.cs | 514 | Presentation (+Developer) | SPLIT | B05 | SPLIT: Bind/Parse/Fmt/LoadUser/Own/Dirty/SetPoseList/Put/HasAny/HasPose/Effective(weapon,clip)/EffectiveFor/BlendFor/Blend/Abs/SetLive/Flush and the user-file part of Save → Pres. LoadLegacy/HasLegacy/ConvertLegacy/LegacyWeapons (0.5/0.6 grips) → retire after one more release. Editor: Step/StepClip/Pressed/Keys/Axis/Note/Stat... |
| Modules/PlayerBody/Animation/Loco.cs | 823 | Presentation | REWRITE | B05 | REWRITE: duplicate of ModCharacter+CharPlan (see the duplication table above). Keep graph/rig building as the shared CharacterRig. `AimKeys` (l.309) → Developer. |
| Modules/PlayerBody/Animation/LocoPlan.cs | 341 | Presentation | MOVE | B05 | MOVE. |
| Modules/PlayerBody/Animation/ModAPI.cs | 203 | Presentation | MERGE-INTO | B05 | MERGE-INTO the Pres ModAPI service. `AimLiftOf` (l.81) → DELETE (not in IAnimationApi, no caller). `Find` (l.177) is an O(n) scan → dictionary by Animator instance id. |
| Modules/PlayerBody/Animation/ModCharacter.cs | 564 | Presentation | MOVE | B05 | MOVE as the shared character driver. Fix §6 items: allocation at l.351, rig clone at l.208-260, no LOD. |
| Modules/PlayerBody/Body/Arms.cs | 75 | Presentation | MOVE | B05 | MOVE. Dead left-arm branch: `Model.ArmsLeft()` is always null, so `_matLeft` and `IsLeftArm` (l.57) are dead → DELETE. Share the renderer scan with Game.GameArmsShown. |
| Modules/PlayerBody/Body/Body.cs | 1423 | Presentation | SPLIT | B05 | SPLIT: Create/FlexaPrefab/Clip/BuildGraph(Mixamo)/meshes/SetVisible/SetMesh/CapMaterial/ShiftFirstPerson/FitFirstPerson/LateDead/Ragdoll/props (UpdateProp/ShowProp/PoseProp/AlignProp/TryWeaponMuzzle)/pose fade → shared CharacterBody. WatchAttack/StartMelee/UpdateMelee/WatchThrow/ThrowAim/PunchAim/BeginStrike and the Input rea... |
| Modules/PlayerBody/Body/CharacterModels.cs | 122 | Presentation | MOVE | B05 | MOVE. MERGE the glb-skin path with NPC/Women/Gungirl.cs model building. Today the player's Gungirl/Shoota come from NPC via `Services.CharacterAssets` (l.45); Pres should own those assets. |
| Modules/PlayerBody/Body/CharacterPortraits.cs | 48 | Presentation (+HUD) | SPLIT | B05 | SPLIT: Names/Order/Canonical → Pres character catalog (ModAPI query). Prepare/Picture (UI textures) → HUD. |
| Modules/PlayerBody/Body/Equipment.cs | 336 | Presentation | MOVE | B05 | MOVE. `Classify` (l.50) → MERGE-INTO the shared weapon catalog (§5). Feed it from an inventory snapshot so NPC followers can use it too. |
| Modules/PlayerBody/Body/EquipmentInventory.cs | 214 | Compatibility (+Core) | SPLIT | B17 | SPLIT: Resolve/Bridge (Apocapocket/Apocainventory) → Compatibility, published as a Core ModAPI `PlayerInventorySlots` query. Vanilla Read/FirstItem → Core player-inventory query (CleanHUD reads the same slots). |
| Modules/PlayerBody/Body/FemalePain.cs | 105 | Presentation | MOVE | B05 | MOVE (voice follows the character model); `PlayerShout` → character voice query. See §8. |
| Modules/PlayerBody/Body/Gltf.cs | 417 | Core | MERGE-INTO | B01 | MERGE-INTO Core.Assets.Gltf. It duplicates NPC/Women/Gltf.cs; the only difference is the log call at l.317. |
| Modules/PlayerBody/Body/InventoryModel.cs | 72 | HUD | MOVE | B10 | MOVE. |
| Modules/PlayerBody/Body/Json.cs | 117 | Core | MERGE-INTO | B01 | MERGE-INTO Core Json. Identical copies: NPC/Women, Towns/GasStation, Patrols/WarRig; Survival/StewPot differs by one blank line. CleanHUD/Json.cs is a different 122-line variant. |
| Modules/PlayerBody/Body/Model.cs | 364 | Presentation | MOVE | B05 | MOVE. `ArmsLeft` (l.340, "Max only", always null) → DELETE. |
| Modules/PlayerBody/Body/PickAssist.cs | 176 | Presentation | MOVE | B06 | MOVE. Route through a Core Hooks dispatcher: Performance/PlayMaker/MousePickFeature.cs:142 patches the same target. |
| Modules/PlayerBody/Body/Projectiles.cs | 35 | Gunplay | MOVE | B09 | MOVE. |
| Modules/PlayerBody/Body/Props.cs | 304 | Presentation | SPLIT | B05 | SPLIT: KindOf/KindOfName/Norm → shared weapon catalog (Core ModAPI, data owned by Gunplay). Build/Find/ItemModel/SceneItem/Instantiate/CopyForMount/TryMuzzle/Barrel/MirrorToRight/BindWorld/OnSceneLoaded → Pres. |
| Modules/PlayerBody/Body/Ragdoll.cs | 90 | Presentation | MOVE | B05 | MOVE; pass the ignore-collider set in instead of reading `Game.Player` (l.137). |
| Modules/PlayerBody/BodyRunner.cs | 229 | Presentation | SPLIT | B05 | SPLIT: Tick/LateTick/WheelTick/ModApiTick → Pres player driver. `Car.Tick` call (l.110) → Vehicles. Gui (l.212-213) → Developer + HUD. `EndOfFrameLoop` (l.216) → CamSvc. `Perf.*` calls → DELETE. `TryPlayerJumpHead`/`TryOcclusionPoints`/`DeathFocus` → Pres ModAPI queries. |
| Modules/PlayerBody/Camera/Cave.cs | 41 | Presentation (+Core) | MOVE | B06 | MOVE (or Core `PlayerState.InCave`; Weather/Survival can reuse it). |
| Modules/PlayerBody/Camera/ChaseCameraCompat.cs | 74 | Compatibility | MOVE | B17 | MOVE. Keep the type map (§9). Stop the endless 1 s discovery job after the first post-chainload attempt. |
| Modules/PlayerBody/Camera/FirstPersonJump.cs | 15 | Presentation | MERGE-INTO | B06 | MERGE-INTO CharacterBody (it is misplaced in Camera/). |
| Modules/PlayerBody/Camera/FirstPersonSway.cs | 111 | Presentation | MERGE-INTO | B06 | MERGE-INTO FP weapon motion (with AimTransition and Guns WeaponTransition). |
| Modules/PlayerBody/Camera/OcclusionCutaway.cs | 384 | Presentation | MOVE | B06 | MOVE. |
| Modules/PlayerBody/Camera/Perf.cs | 134 | DELETE | DELETE **D** | - | Superseded by Scheduler timing + Debug FrameStats; fold its census lines into Debug. |
| Modules/PlayerBody/Camera/ThirdPerson.cs | 563 | Presentation (+HUD) | SPLIT | B06 | SPLIT: Tick/Zoom/ComputeView/PreCull/EndOfFrame/HideViewModel/ShowViewModel/Off/CarShiftEye → CamSvc owner. BtnJump branch of `BeforeGetButtonDown` (l.118-121) → player traversal. MoveCrosshair/FindCursor/RestoreCrosshair (l.450-504) → HUD crosshair service (duplicates HUD/Crosshair/CrosshairController.cs:71-142 lookups). Lat... |
| Modules/PlayerBody/Climbing/ClimbGeometry.cs | 191 | Presentation | MOVE | B06 | MOVE; candidate to share with NPC/PoiNav/PoiNavBake climb-link detection. |
| Modules/PlayerBody/Climbing/Climbing.cs | 235 | Presentation | MOVE | B06 | MOVE. |
| Modules/PlayerBody/Climbing/Ladders.cs | 396 | Presentation | MOVE | B06 | MOVE. Replace the raw Services statics with ModAPI query/events (§7). |
| Modules/PlayerBody/Climbing/NpcTraversal.cs | 110 | Presentation | MERGE-INTO | B06 | MERGE-INTO one traversal lease with PlayerTraversal. |
| Modules/PlayerBody/Climbing/PlayerTraversal.cs | 227 | Presentation | MERGE-INTO | B06 | MERGE-INTO the unified traversal lease (foot IK then works for NPCs as well). |
| Modules/PlayerBody/Climbing/PoseClip.cs | 168 | Presentation | MOVE | B06 | MOVE; load at Init (Sliced). Today it builds an Avatar synchronously on first ladder use. |
| Modules/PlayerBody/Game.cs | 498 | Core (+Presentation) | SPLIT | B01 | SPLIT: Ready/Find/Dead/InCar/Velocity/MoveState/StandingHeight/CarRoot/JumpState → MERGE-INTO Core.GameRefs as a typed `PlayerState` (Guns, NPC, HUD and Quests read the same FSMs). DrawnWeapon, IsReloading, ArmsReloading, ArmsReloadPhase, ReloadsPerRound, Attack, KickState, GrenadeState, GameArmsShown, AimDownSights, Binocula... |
| Modules/PlayerBody/GameBindings.cs | 55 | Core | MOVE | B01 | MOVE → Core input service; cache the `KeyAction` per InputStorage instance (§6). |
| Modules/PlayerBody/KeyMap.cs | 76 | Core | MOVE | B01 | MOVE → Core.ModuleConfig/Hotkeys (AGENTS 5.7 already asks for this). |
| Modules/PlayerBody/PlayerBodyModule.cs | 417 | Presentation (+Vehicles, HUD, Developer) | SPLIT | B05 | SPLIT: Character, Body/Arms/FemaleArms/ReplaceDriver/EquipmentOnBody/PainSounds, sway/ADS, Climbing*/Ladder*, Anim/Leg/Model entries → Pres. ThirdPerson*/Orbit/EnableMMB/DynamicCrosshair/PickAssist*/Occlusion*/ObserveKey → CamSvc owner. VehicleStatusHint/VehicleHotkeyHint → HUD. IgnitionControlOn, Cassette/Volume keys, `Legac... |
| Modules/PlayerBody/PlayerBodyPages.cs | 186 | Presentation (+Developer) | SPLIT | B05 | SPLIT: Framework/BodyPage/ClimbingPage → Pres. CameraPage → CamSvc owner. PoseEditor/ArmButtons and the `WheelArms.Draw` page → Developer. |
| Modules/PlayerBody/Vehicles/Car.cs | 379 | Vehicles (+HUD) | SPLIT | B12 | SPLIT: Tick (cockpit reset, controls, cassette/volume), TickIgnition, BeforeMousePick, Patch, FindControls/Part, FindEngine/Member/EngineRunning, SetVolume/RadioVolume → Vehicles. UpdateHints/AddHint/AddActionHint/BindingSig/FindGameText/KeyName/OnGUI/GuiWanted → HUD, reading a `VehicleControlsState` query. |
| Modules/PlayerBody/Vehicles/CarSeat.cs | 121 | Presentation | MOVE | B06 | MOVE (SeatPose; unify with Patrols/Crews/Pose.cs). `FindSource` (l.98) has no caller → DELETE. |
| Modules/PlayerBody/Vehicles/IgnitionControl.cs | 153 | Vehicles | MOVE | B12 | MOVE. Row injection goes through the Core input service. |
| Modules/PlayerBody/Vehicles/VehicleHud.cs | 79 | HUD | MOVE | B10 | MOVE. |
| Modules/PlayerBody/Vehicles/WheelArms.cs | 453 | Presentation (+Vehicles, Developer) | SPLIT | B06 | SPLIT: Bind/Solve/SolveHandlebars/RestPassengerArms/Reach/Load/LoadPath/FittedWheel → Pres SeatPose. `Patch`/`BeforeSteer` and `_steering.returnToCenter = true` (l.102), which are gameplay changes, plus `MissingWheel` → Vehicles. `Draw` page and `Save` → Developer. |
| Modules/PlayerBody/Vehicles/WheelGripEditor.cs | 242 | Developer | MOVE **D** | B16 | DECISION: steering-wheel grip editor -> Developer module. |
| Modules/PlayerBody/WeaponsPackShadows.cs | 48 | Compatibility | MOVE | B17 | MOVE. Replace `FieldInfo Body._prop` reflection (l.24, l.40) with a Pres ModAPI query `TryGetHeldProp(characterId)`. |

## A3 NPC (51 files, 18237 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/NPC/Animals/Burrow.cs | 526 | NPCAI (+Presentation) | SPLIT | B07 | SPLIT. `Tick`, the hunt (`Dive`/`Travel`/`Rise`), the sand test and `Register` stay in NPCAI. The visual parts go to Pres: `LateTick` (l.371, sinking the rig) and the puff FX (l.386-468, the Burrower_Effect pool). |
| Modules/NPC/Animals/DogAnimations.cs | 695 | Presentation (+NPCAI) | SPLIT | B05 | SPLIT. Pres gets `LoadBundles`, `Switch`, `Prepare`/`Restore`/`Release`, `MeshCulling`, `SampleDog`, `FrontLegs`, `LevelFront`, `PawsOnGround`, `AlignCompanion`, `LateTick`/`LateCompanion` and the `Dog` pose state. NPCAI gets the decisions: `StartAttack`, `TryAttack`, `DriveDash`, `InReach`/`ReachPoint`, `DamageReady`, the re... |
| Modules/NPC/Animals/DogBite.cs | 141 | NPCAI (+Presentation) | SPLIT | B07 | SPLIT. `Tick`, `Register` and `AfterSetFsmFloat` (bite detection) stay in NPCAI. `LateTick` (l.117, bone lunge) goes to Pres. |
| Modules/NPC/Animals/DogCompanionCompat.cs | 384 | Compatibility | MOVE | B17 | MOVE to Compat. Today it reaches DogAnimations internals (`RegisterCompanion`, `TickCompanion`, `LateCompanion`, `CompanionDashing`, `RemoveCompanion`). It needs a Pres ModAPI such as `IQuadrupedAnimation` and an NPCAI ModAPI for threats (`Brain.cs:571 CloseThreat` is the reverse dependency). |
| Modules/NPC/Animals/DogCompanionSeating.cs | 11 | Compatibility | MERGE-INTO | B17 | MERGE-INTO DogCompanionCompat.cs. Its only use is DogCompanionCompat.cs:372. |
| Modules/NPC/Animals/Pounce.cs | 427 | NPCAI (+Presentation) | SPLIT | B07 | SPLIT. `Tick`, `Register`, `Room` and the bite stay in NPCAI. `LateTick` (l.379, rear pose) and the airborne carcass tumble in `AfterCreateObject` (l.397) go to Pres. |
| Modules/NPC/Brain/Aim.cs | 227 | NPCAI (+Gunplay) | SPLIT | B07 | SPLIT. Decision prefixes `BeforeSendEvent`/`BeforeRandomWait` stay in NPCAI. `SpreadFactor`/`Steps` go to Gunplay, which already owns spread and lead ("NPC aim" tools section in Guns). |
| Modules/NPC/Brain/Ambush.cs | 56 | NPCAI | MOVE | B07 | MOVE |
| Modules/NPC/Brain/ApBody.cs | 120 | Presentation | MOVE | B05 | MOVE to Pres. It becomes the Pres-side NPC character driver. `WeaponKey` (l.44) and `IsPistol` (l.55) duplicate `IAnimationApi.WeaponKey`/`WeaponKind`: DELETE them and call the API. |
| Modules/NPC/Brain/Brain.Weapons.cs | 98 | NPCAI | MOVE | B07 | MOVE. Make it generic: `Frighten(npc, threat, seconds)` without Weapons Pack wording. |
| Modules/NPC/Brain/Brain.cs | 1983 | NPCAI (+Presentation, Debug) | SPLIT | B07 | SPLIT. NPCAI keeps `Tick`, `Think`, `MoveToward`, `Steer`, `NavSteer`, `Cover*`, `OnStuck`, `TryHop`, the hooks and `Make`/`Of`. Pres gets `Drive` (l.1208), `Aim`/`Unfreeze` (animator freeze), `StrafeClip`, `Crouch` (capsule plus clip), `HeldWeapon`/`GunPoses`, `TryRig`/`BuildRig`/`HopAnim`. NPCAI publishes a per-NPC "situati... |
| Modules/NPC/Brain/Idle.cs | 832 | NPCAI (+Presentation, Debug) | SPLIT | B07 | SPLIT. Behaviour stays in NPCAI. `Drive`'s `Anim.Play("run")`/speed (l.751-755) and `RunInPlace` go to Pres. `DrawDebug` (l.791) goes to Debug. The `Drive` steering duplicates Brain's (see 5). |
| Modules/NPC/Brain/ReloadSounds.cs | 84 | Presentation | MOVE | B05 | MOVE to Pres. Today it creates a `new GameObject` per sound (l.74); pool it instead. |
| Modules/NPC/Brain/SimpleBandits.cs | 118 | NPCAI | MOVE | B07 | MOVE. Patrols drives it via `INpcApi.SetSimpleConvoyBandit*`. |
| Modules/NPC/ChainsawLimbs.cs | 158 | DELETE | DELETE **D** | - | #if false since 2026-10-21 (dismemberment disabled at Denis's request); only comment references remain. |
| Modules/NPC/Debug/DebugOverlay.cs | 62 | Debug | MOVE | B03 | MOVE |
| Modules/NPC/Debug/NpcPages.cs | 143 | NPCAI (+Debug, Tools, Presentation) | SPLIT | B07 | SPLIT. ConfigPages follow their entries. "AI overview" job times go to Debug (Performance already shows per-job times). The Debug page (toggles, Reset/Export maps) goes to Debug and Tools. "POI pathfinding" goes to Tools. Population stays in NPCAI; Appearance/voices go to Pres. |
| Modules/NPC/Detection/CaveDarknessBridge.cs | 60 | Compatibility | MOVE | B17 | MOVE to Compat. Expose it as a Core query `Light.AmbientAt(pos)`. |
| Modules/NPC/Detection/CrewOrders.cs | 46 | NPCAI | MOVE | B07 | MOVE. This is behaviour; Patrols only calls it. |
| Modules/NPC/Detection/Senses.Spiders.cs | 144 | NPCAI | MOVE | B07 | MOVE |
| Modules/NPC/Detection/Senses.cs | 2166 | NPCAI (+Presentation, Compatibility, Debug, Core) | SPLIT | B07 | SPLIT. NPCAI keeps `Tick`, `Look`, `Visible`, `Noise`, ghosts, `Engage`, `Make`/`Register`, `Persist`, `CrewOrder`, `Remember`, `Hurt`/`Shot`. Pres gets `Taunt`/`BeforeAudioPlay` audio swap (l.1125-1197), `Voice()`/`FlexaClips`/`VoiceSource`. The shout input (`ShoutPressed`, `ModifierHeld`, `BeforeGetButton*`, l.1199-1283) go... |
| Modules/NPC/Detection/VanillaEvents.cs | 192 | Gunplay (+NPCAI) | SPLIT | B09 | SPLIT. Gunplay gets `BeforeNpcShot` ray-length extension (l.32-49) and its finalizer `AfterNpcShot`. Gunplay or Core also gets `BeforePlayerShot` and `AfterDamage`, which raise `WeaponFired`/`NpcHurt` events. NPCAI only consumes those events (`Senses.Shot`/`Hurt`, `Brain.Shot`). |
| Modules/NPC/Detection/WeaponRanges.cs | 153 | Gunplay (+Presentation) | SPLIT | B09 | SPLIT. Gunplay gets `Kind`, `Classify`, `RangeOf`, `GunKindOf` and `ProjectilesEnabled` (they duplicate Guns Tracers.cs:746 `Classify` and Tracers.cs:678 fire_effect lookup). Pres or Core gets `ScanHands`/`WeaponOf`/`UnderHand` (held-model lookup, also used by Brain.Drive). |
| Modules/NPC/Hazards/Nests.cs | 632 | NPCAI | MOVE | B07 | MOVE. `Pois()` (l.242) should use the shared POI index from Core/POI (see 5). |
| Modules/NPC/Hazards/Storm.cs | 166 | Survival (+Compatibility) | REWRITE | B14 | REWRITE. Survival (Weather) provides `IWeatherRegions.StormAt(pos)` and `FunnelAt(pos)`. The DustStorm reflection (l.41-75) merges into the single Compat DustStorm bridge (Patrols/WarRig/DustStormBridge.cs). NPCAI keeps only `StormSight`/`StormHearing` factors. |
| Modules/NPC/Hazards/Webs.cs | 400 | NPCAI (+Presentation) | MOVE | B07 | MOVE. See 8 for why this stays in NPCAI rather than Pres. |
| Modules/NPC/Navigation/Nav.cs | 2126 | NPCAI (+Debug) | SPLIT | B08 | SPLIT. Routing and bake stay in NPCAI. `Dump`/`TraceDump`/`Paint`/`TraceInfo`/`CellText` (l.947-1512) and `DrawDebug` (l.2052) go to Debug. `SweepStep` discovery (l.222) moves to a Core/POI world index (see 5). Delete `MasksNow` (l.801, unused). |
| Modules/NPC/Navigation/NavMapHighlight.cs | 80 | Tools | MOVE | B16 | MOVE to Tools, or DELETE once the vanilla pack is complete. 41 maps cover all 39 types today, so it is only useful for custom POIs. |
| Modules/NPC/Navigation/NavMapTools.cs | 131 | Tools | MOVE | B16 | MOVE to Tools via a ModAPI call (`Nav.ResetCustom`, `Nav.ExportVanilla`) |
| Modules/NPC/Navigation/NavMaps.cs | 339 | NPCAI | MOVE | B08 | MOVE |
| Modules/NPC/Navigation/Passthrough.cs | 115 | NPCAI | MOVE | B08 | MOVE |
| Modules/NPC/NpcApi.cs | 32 | NPCAI | MOVE | B07 | MOVE. It becomes the NPCAI ModAPI provider. `SpreadFactor` moves to Gunplay (see 8). |
| Modules/NPC/NpcModule.Config.cs | 340 | NPCAI (+Debug, Presentation) | SPLIT | B07 | SPLIT. Behaviour entries stay in NPCAI. The Debug entries (`VerboseLog`, `NavDump`, `ShowNavigation`, `HighlightUnbakedMapPois`) and the four `<X> / Log` entries go to Debug. The alias line 334-336 is deleted. Presentation entries go to Pres: `Brain/ApocaplayerClips`, `AimPose`, `NpcAim/ReloadVolume`, `ReloadSoundRange`, `Dog... |
| Modules/NPC/NpcModule.cs | 281 | NPCAI (+Debug, Tools) | SPLIT | B07 | SPLIT. `NpcModule` lifecycle, `InstallHooks`, `InstallPatches`, `OnOriginShift` and `ResetAll` stay in NPCAI. `LogAdapter` and `Verbose()`/`Warn()` (l.35-44) go to Debug (one `Log.L1/L2`). PoiNav editor/view/setup jobs and the editor hotkey (l.108-115) go to Tools. The commented ChainsawLimbs lines 175-176 are deleted. `Migra... |
| Modules/NPC/PoiNav/PoiNav.cs | 722 | NPCAI | MOVE | B08 | MOVE. Delete `LearnLadderSide` (l.205-235, dead after `return;`). Take `WalkerTick` back from the view job (see 3). |
| Modules/NPC/PoiNav/PoiNavBake.cs | 598 | Tools | MOVE | B16 | MOVE to Tools. Its `Mask` constant is used at runtime (PoiNav.Walk), so it moves to NPCAI. |
| Modules/NPC/PoiNav/PoiNavData.cs | 213 | NPCAI | MOVE | B08 | MOVE. Tools uses it through ModAPI. |
| Modules/NPC/PoiNav/PoiNavEditor.cs | 481 | Tools | MOVE | B16 | MOVE to Tools |
| Modules/NPC/PoiNav/PoiNavSetup.cs | 199 | Tools | MOVE | B16 | MOVE to Tools. The flag becomes a Core ModAPI `WorldGenerationSuspended` read by Towns/POI. |
| Modules/NPC/PoiNav/PoiNavTraverse.cs | 407 | NPCAI (+Presentation) | SPLIT | B08 | SPLIT lightly. The move logic stays in NPCAI. Animation phase calls (`Phase`, `Anim`) already go through `Services.Animation` (Pres). |
| Modules/NPC/PoiNav/PoiNavView.cs | 130 | Tools (+NPCAI) | SPLIT | B16 | SPLIT. Display goes to Tools. `PoiNav.WalkerTick()` (l.27) moves to an NPCAI job. Delete `LearnLadderSide()` (l.26). |
| Modules/NPC/WeaponsPackCompat.cs | 188 | Compatibility | MOVE | B17 | MOVE to Compat. Replace the `Brain.FireRetreat`, `Brain.ChainsawWitnesses` and `ChainsawDrawn` calls with ModAPI calls: `Npc.Frighten(id, pos, secs)`, an event `KillWitnessed`, and a flag `PlayerNoiseFactor`. |
| Modules/NPC/Women/Bindposes.cs | 36 | DELETE | DELETE **D** | - | Duplicate of the Core/Util copy created in Wave 0 (identical apart from namespace). |
| Modules/NPC/Women/Gltf.cs | 417 | DELETE | DELETE **D** | - | Duplicate of the Core/Util copy created in Wave 0 (identical apart from namespace). |
| Modules/NPC/Women/Gungirl.cs | 615 | Presentation | MOVE **D** | B05 | DECISION (Denis 2026-10-22): Women of Wasteland is Presentation (female skins/voices). Whole file incl. the female spawn roll and identity rename (an appearance rule). Saved name prefixes (Gungirl(Clone)N) must not change. Config entries keep [NPC / Women of Wasteland]/[NPC / Gungirl]/[NPC / Shoota] on disk via legacy prefix. |
| Modules/NPC/Women/Json.cs | 117 | DELETE | DELETE **D** | - | Duplicate of the Core/Util copy created in Wave 0 (identical apart from namespace). |
| Modules/NPC/Women/Level.cs | 76 | Core | MERGE-INTO **D** | B01 | Core/Util/Audio (loudness helper next to the single WAV decoder). |
| Modules/NPC/Women/NpcModule.Women.cs | 51 | Presentation | MOVE **D** | B05 | DECISION (Denis 2026-10-22): Women of Wasteland is Presentation (female skins/voices). Whole file incl. the female spawn roll and identity rename (an appearance rule). Saved name prefixes (Gungirl(Clone)N) must not change. Config entries keep [NPC / Women of Wasteland]/[NPC / Gungirl]/[NPC / Shoota] on disk via legacy prefix. |
| Modules/NPC/Women/Voice.cs | 354 | Presentation | MOVE **D** | B05 | DECISION (Denis 2026-10-22): Women of Wasteland is Presentation (female skins/voices). Whole file incl. the female spawn roll and identity rename (an appearance rule). Saved name prefixes (Gungirl(Clone)N) must not change. Config entries keep [NPC / Women of Wasteland]/[NPC / Gungirl]/[NPC / Shoota] on disk via legacy prefix. |
| Modules/NPC/Women/Wav.cs | 61 | DELETE | DELETE **D** | - | Duplicate of the Core/Util copy created in Wave 0 (identical apart from namespace). |
| Modules/NPC/Women/WomenFeature.cs | 104 | Presentation | MOVE **D** | B05 | DECISION (Denis 2026-10-22): Women of Wasteland is Presentation (female skins/voices). Whole file incl. the female spawn roll and identity rename (an appearance rule). Saved name prefixes (Gungirl(Clone)N) must not change. Config entries keep [NPC / Women of Wasteland]/[NPC / Gungirl]/[NPC / Shoota] on disk via legacy prefix. |

## A4 Patrols (95 files, 21720 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/Patrols/Crews/BusPassengerPose.cs | 126 | Presentation (+Developer) | SPLIT | B06 | SPLIT: `BusPassengerPreset`,`BusPassengerPoseStore` -> Pres; `BusPassengerPoseEditor` -> Dev |
| Modules/Patrols/Crews/Crew.cs | 697 | Patrols | SPLIT | B11 | SPLIT: stays Pat except `MuteAi(...)` (disables NPC AI FSMs directly) -> NPC service `INpcApi.SetSeated(npc,bool)`; `ApplyOverheat*` (part heat FSM) -> Veh |
| Modules/Patrols/Crews/CrewAppearance.cs | 51 | Presentation | MERGE-INTO **D** | B05 | Presentation character appearance API (faction outfits/skins). Alias table merges with Women identity aliases. |
| Modules/Patrols/Crews/CrewBusDefence.cs | 101 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Crews/CrewCombat.cs | 83 | Patrols | MOVE | B11 | MOVE (decision is "vehicle crew" level; uses NPC only via events) |
| Modules/Patrols/Crews/CrewFactions.cs | 20 | Patrols | MOVE | B11 | MOVE; roster lists overlap NPC population/Quests factions (see 5) |
| Modules/Patrols/Crews/CrewPoi.cs | 184 | Patrols | MOVE | B11 | MOVE; replace `TownsApi.Provider.ParkingOutside` (CrewPoi.cs:50) with ModAPI; share Structures snapshot with TrafficLocations |
| Modules/Patrols/Crews/CrewReturn.cs | 437 | Patrols | MOVE | B11 | MOVE (already uses Services.Npc; `HealthVar` -> Core FsmUtil helper) |
| Modules/Patrols/Crews/MeleeWheels.cs | 251 | DELETE | DELETE **D** | - | Duplicate of Gunplay melee-on-wheels (Tracers MeleeOnWheels, default on); Apocaraider fallback no longer needed. |
| Modules/Patrols/Crews/Passenger.cs | 435 | NPCAI (+Presentation, Patrols) | SPLIT | B07 | SPLIT: `CombatAllowed/CanAnswer/FindVanillaTarget/IndexTargetSources/Consider/ForceRangedOnly/MuteNonCombatAi/Quest*` -> NPC (seated combat service, also used by Quests); `LateUpdate` aim/`HideMeleeWeapons`/`AimPoint` pose part -> Pres; `AttachDriver/AttachTurret/SetDriverMode/ScheduleBurst` orchestration stays Pat |
| Modules/Patrols/Crews/Pilot.cs | 1006 | Patrols (+Core) | SPLIT | B11 | SPLIT: `Pilot` stays Pat (driving AI); `PlayerRef` (l.835-1006) -> Core.GameRefs (`PlayerCar`, `PlayerVelocity`, `GodMode`, `Target`) |
| Modules/Patrols/Crews/PlayerAnims.cs | 74 | Presentation | MERGE-INTO | B05 | MERGE-INTO Presentation animation service (becomes internal call) |
| Modules/Patrols/Crews/Pose.cs | 412 | Presentation | MOVE | B06 | MOVE (type name `Pose` must stay resolvable: Quests Hire.cs:281 and saves) |
| Modules/Patrols/Crews/QuestFollowers.cs | 61 | Patrols (+Presentation, NPCAI) | SPLIT | B11 | SPLIT: `IsSeated/IsCrew/FormerCrew/IsHired` stay Pat; `SeatPose/ReleasePose/Animated` -> Pres; `SeatCombat/CanFire/Combat/ShootDistance` -> NPC; `ResolveType` DELETE (no caller; QuestsCompat retired) |
| Modules/Patrols/Crews/Ram.cs | 264 | Patrols (+Vehicles) | SPLIT | B11 | SPLIT: `Sensor`,`Hit`,`HurtPlayer`,`Push`,`BeforeSetFsmFloat`,`BeforeSendEvent` stay Pat; `AfterSetFsmFloat` (all cars' damage x NpcRamDamageMultiplier) -> Veh |
| Modules/Patrols/Crews/Rider.cs | 1173 | Patrols (+NPCAI, Presentation, Gunplay, Developer, Core) | SPLIT | B11 | SPLIT: `Rider.Seat/Slots/AutoSpot/TopAt/IsGunner/IsWarboy/Display/Stored/IsBikeBody/TurretChoices` -> Pat; `RiderGuard` decisions (`Update` witness/leap/throw choice, `CanLeap/Leap/Fly/JumpOff/CombatDismount/SpawnMob/Hop/Land`) -> NPC; `RiderGuard.BuildGraph/Play/Blend`, `JumpClip`, `Rider.Clip/Bone` -> Pres; `Solve/U/FlatLea... |
| Modules/Patrols/Crews/RiderAnchorGuard.cs | 38 | Presentation | MOVE | B06 | MOVE |
| Modules/Patrols/Crews/SeatedLosGuard.cs | 27 | NPCAI | MOVE | B07 | MOVE (NPC senses domain; NPC already prefixes same dispatcher) |
| Modules/Patrols/Crews/SimpleBanditBridge.cs | 37 | Patrols | MOVE | B11 | MOVE (internal ModAPI client, not third-party; rename e.g. `CrewNpcMode`) |
| Modules/Patrols/Editor/EditorData.cs | 192 | Patrols (+Loot, Core) | SPLIT | B11 | SPLIT: `SpawnGroup`,`EditorData` -> Pat data model; `LootProfile`,`LootItem` -> Loot; `StoreJson` -> Core.Json |
| Modules/Patrols/Editor/EditorSelection.cs | 14 | Tools | MERGE-INTO | B16 | MERGE-INTO TemplateMenu |
| Modules/Patrols/Editor/EditorStore.cs | 187 | Patrols (+Loot, Core) | SPLIT | B11 | SPLIT: store stays Pat (Loot part -> Loot store); `AtomicWrite` -> Core.AtomicFile; `SafePath/UniquePath` -> Core |
| Modules/Patrols/Editor/MerchantEditor.cs | 78 | Developer | MOVE **D** | B16 | DECISION: merchant item placement -> Developer module (deprecation candidate). |
| Modules/Patrols/Editor/PickupCatalog.cs | 56 | Loot (+Compatibility) | SPLIT | B15 | SPLIT: catalog -> Loot built on Core prefab catalog (dup of `Prefabs.Scan`); Apocaspawner cfg name read -> Compat |
| Modules/Patrols/Editor/ShellRecycle.cs | 55 | Tools (+Core) | MOVE | B16 | MOVE to Core utility (generic) |
| Modules/Patrols/Editor/TemplateExporter.cs | 456 | Tools (+Core) | SPLIT **D** | B16 | DECISION: template recorder becomes the "Template" tab of the Patrols editor (Tools), shown when the player sits in a supported vehicle and when editing a template. PrefabName/CloneSuffix -> Core/Game/Names. |
| Modules/Patrols/Editor/TemplateMenu.Ledger.cs | 477 | Tools | MOVE | B16 | MOVE (via ModAPI only); `MoveTemplate` DELETE (dead) |
| Modules/Patrols/Editor/TemplateMenu.cs | 87 | Tools | MOVE | B16 | MOVE |
| Modules/Patrols/Editor/TurretSpotter.cs | 148 | Developer | MOVE **D** | B16 | DECISION: turret spot editor -> Developer module (deprecation candidate). |
| Modules/Patrols/Merchants/MerchantGuards.cs | 55 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Merchants/MerchantModel.cs | 98 | Patrols (+Developer, Loot) | SPLIT | B11 | SPLIT: DTOs/catalog stay Pat; `MerchantCatalog.Goods` -> Loot; `MerchantApi` (public, Dev only) -> Dev-facing ModAPI; layouts store stays Pat |
| Modules/Patrols/Merchants/MerchantRuntime.cs | 210 | Patrols (+Loot) | MOVE | B11 | MOVE; stock goods lookup could use Loot API |
| Modules/Patrols/Merchants/TrafficMerchants.cs | 95 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/PatrolsData.cs | 66 | Patrols (+Vehicles) | MOVE | B11 | MOVE (keep for >=1 release; WarRig branch goes with Vehicles) |
| Modules/Patrols/PatrolsModule.cs | 291 | Patrols (+Core) | SPLIT | B11 | SPLIT: lifecycle/jobs stay Pat; `EnsureGameFont`/`Pressed` -> Core (`Hotkeys.Pressed`, Skin); editor components (`_menu,_exporter,_spotter,_busPose,_merchantEditor`, their jobs/hotkeys) -> Tools/Dev; `Services.FollowerVehicles` provider moves with QuestFollowers split |
| Modules/Patrols/PatrolsPages.cs | 234 | Tools (+Developer, Patrols, Vehicles) | SPLIT | B16 | SPLIT: `PatrolEditorPage`,`RecorderPage` -> Tools; `TurretSpotPage`,`BusPosePage`,`MerchantSpotPage` -> Dev; `WarRigPage` -> Veh/POI; `DebugPage` -> Dbg; `WorldTrafficPage`,`SpawningPage`,`MerchantsPage` stay Pat |
| Modules/Patrols/PatrolsSettings.cs | 573 | Patrols (+Vehicles, Presentation, NPCAI, Tools, Developer, Core) | SPLIT | B11 | SPLIT: `BonePoseConfig/PoseFloat/PoseBool/ArmPoseProfile/WeaponPoseConfig`, `[Patrols / Pose]`, `Pose.<Human>`x7, `Turret pose` -> Pres; `War Rig spawns/ground recovery`, `ExitSpeedKmh`, `SuspensionLiftCenterOfMass`, `NPC ram damage multiplier` -> Veh; `AudioVoices` (read by Core/Plugin.cs:114) and `Hide missing-FSM warnings`... |
| Modules/Patrols/Persistence/PatrolCrewDelta.cs | 110 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Persistence/PatrolLoadPreparation.cs | 36 | Patrols (+Core) | MERGE-INTO | B11 | MERGE-INTO Core load barrier (`LoadingProgress`/VehicleLoadPlacement share the same prefix) |
| Modules/Patrols/Persistence/Persistence.cs | 663 | Patrols (+Core) | SPLIT | B11 | SPLIT: `PatrolMarker` stays Pat; I/O (ES3 key, sidecar, `AtomicWrite`, slot paths, quit flush) -> Core SaveBus sidecar API; format code stays Pat |
| Modules/Patrols/Spawning/BuildQueue.cs | 101 | Vehicles (+Core) | MERGE-INTO | B12 | MERGE-INTO Core.WorldWork queue (see 5 dup budgets) |
| Modules/Patrols/Spawning/Cleanup.cs | 110 | Patrols (+Core) | REWRITE | B11 | REWRITE as participant of Core.SaveCleanup (same far-timer pattern) |
| Modules/Patrols/Spawning/Convoy.cs | 304 | Patrols (+Compatibility, Core) | SPLIT | B11 | SPLIT: progression/roll/BuildAll stay Pat; `AutosaveSoon` -> Core SaveSystem query (Apocasaver is replaced by WW saves); `BossKills` -> Core progression query; `DebugSpawn/DebugRoll` DELETE (dead) |
| Modules/Patrols/Spawning/EventBudget.cs | 42 | Patrols | MOVE | B11 | MOVE, rename `EncounterCap` (NOT a frame budget) |
| Modules/Patrols/Spawning/Patrol.cs | 1890 | Patrols (+Vehicles, Core, Survival) | SPLIT | B11 | SPLIT (biggest file, 9 classes): see 1a |
| Modules/Patrols/Spawning/PatrolVisibility.cs | 51 | NPCAI (+Patrols) | SPLIT | B07 | SPLIT: sensing -> NPC senses service; empty `PatrolVisibility` MonoBehaviour must stay as save-compat type (see 9) |
| Modules/Patrols/Spawning/VehicleBuildCache.cs | 252 | Vehicles | MOVE | B12 | MOVE (Vehicles assembly) |
| Modules/Patrols/Traffic/RoadPath.cs | 88 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Traffic/RoadPlanner.cs | 199 | Patrols | MOVE | B11 | MOVE; Towns' Harmony patch on `Build` replaced by ModAPI hint provider |
| Modules/Patrols/Traffic/TownTraffic.cs | 326 | Patrols | REWRITE | B11 | REWRITE coupling: via ModAPI `ITownTravel` (see 2) |
| Modules/Patrols/Traffic/TrafficBattles.cs | 274 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Traffic/TrafficCamps.cs | 159 | POI (+NPCAI, Patrols) | SPLIT | B13 | SPLIT: camp load/clear state -> POI world-locations service (consumed by Quests too); Pat consumes |
| Modules/Patrols/Traffic/TrafficCodec.cs | 63 | Core | MERGE-INTO | B01 | MERGE-INTO Core.Json |
| Modules/Patrols/Traffic/TrafficCompass.cs | 133 | Patrols | MOVE | B11 | MOVE (already ModAPI) |
| Modules/Patrols/Traffic/TrafficElevation.cs | 108 | Patrols (+NPCAI) | SPLIT | B11 | SPLIT: `ElevationGrid` -> shared routing core; `TerrainWaypoints` foot long-route -> NPC (foot planning) |
| Modules/Patrols/Traffic/TrafficFactions.cs | 129 | NPCAI (+Patrols) | SPLIT | B07 | SPLIT: `TrafficActor` (sensor AllowedTags reflection, FootTarget) + `PlayerHostile` -> NPC/Core faction service; `DriveTarget/ForCar/Target` stay Pat |
| Modules/Patrols/Traffic/TrafficFoot.cs | 564 | Patrols (+NPCAI) | MOVE | B11 | MOVE; `FootAlive` DELETE (dead); synchronous `RoadPlanner.Build` (l.319,401) -> worker |
| Modules/Patrols/Traffic/TrafficLocations.cs | 364 | POI (+Patrols) | SPLIT | B13 | SPLIT: discovery/`Known/Points/Blocks` -> POI world-locations service; `RoadRoute`,`LegSafe` DELETE (dead); `IsTown` (TownMarkers.Owns) via ModAPI |
| Modules/Patrols/Traffic/TrafficMap.cs | 293 | HUD (+Patrols) | SPLIT | B10 | SPLIT: route/marker data provider stays Pat; drawing (Gui, map camera lookup) -> HUD map layer |
| Modules/Patrols/Traffic/TrafficMapInk.cs | 51 | HUD | MOVE | B10 | MOVE |
| Modules/Patrols/Traffic/TrafficModel.cs | 147 | Patrols | MOVE | B11 | MOVE (DTO field names are save format) |
| Modules/Patrols/Traffic/TrafficReinforcements.cs | 82 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Traffic/TrafficRoads.cs | 416 | Patrols | MOVE | B11 | MOVE (Towns + Vistas also feed it; terrain tile events -> Core dispatcher) |
| Modules/Patrols/Traffic/TrafficRoaming.cs | 47 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Traffic/TrafficSpace.cs | 23 | Core | MERGE-INTO | B01 | MERGE-INTO Core.Origin (keep `OriginShift` in PatrolsModule) |
| Modules/Patrols/Traffic/TrafficState.cs | 139 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Traffic/VehicleRoute.cs | 160 | Patrols | MOVE | B11 | MOVE |
| Modules/Patrols/Traffic/WorldTraffic.cs | 780 | Patrols (+NPCAI) | SPLIT | B11 | SPLIT: journeys stay Pat; `TrafficAgent` sight/hearing (`_nextSense` block, `Sight`) -> NPC senses query |
| Modules/Patrols/Vehicles/CarTemplateFile.cs | 349 | Vehicles (+Core) | SPLIT | B12 | SPLIT: `TemplateFile/TemplatePart` -> Veh (vehicle assembly format, also used by Tools/POI); `Json` -> Core.Json (dup) |
| Modules/Patrols/Vehicles/CarTemplates.cs | 177 | Patrols | MOVE | B11 | MOVE (template park = patrol data model) |
| Modules/Patrols/Vehicles/Cargo.cs | 209 | Loot | MOVE | B15 | MOVE (cargo loot); `SeatSurface/FloorAt` RaycastAll -> NonAlloc |
| Modules/Patrols/Vehicles/Explode.cs | 407 | Patrols (+Vehicles) | SPLIT | B11 | SPLIT: `Schedule/Routine/Eligible/Roll/DefaultChance/BattleWreck` stay Pat; `Darken/Deaden/SetCondition/StopTweens/QuietHinges/StopRpm/StopLoops/IsWreck/RestoreDead/BlastAt` -> Veh `IVehicleWreck`; `Preserved` DELETE (dead) |
| Modules/Patrols/Vehicles/ModVehicles.cs | 72 | Vehicles (+Compatibility) | SPLIT | B12 | SPLIT: `Register/Template` -> Veh factory registry; assembly scan -> Compat |
| Modules/Patrols/Vehicles/MotorcycleIntegration.cs | 77 | Vehicles (+Compatibility) | SPLIT | B12 | SPLIT: `ResolvePrefab/TrimRearCollision/MotorcycleRearAccess/IsBody` -> Veh; MotorcycleMod assembly lookup + `Factory.Template` patch -> Compat |
| Modules/Patrols/Vehicles/Motorcycles/Factory.cs | 166 | Vehicles | MOVE | B12 | MOVE (ns `.Motorcycles`, prefab id fixed) |
| Modules/Patrols/Vehicles/Motorcycles/MotorcycleBalance.cs | 60 | Vehicles | MOVE | B12 | MOVE (public saved component type) |
| Modules/Patrols/Vehicles/Paint.cs | 418 | Presentation | MOVE **D** | B06 | DECISION (Denis 2026-10-22): faction car skins belong to Presentation (Presentation/VehicleSkins). Cargo container texture-by-loot-key: Loot supplies the key->texture data through ModAPI. |
| Modules/Patrols/Vehicles/Suspension.cs | 325 | Vehicles (+Compatibility) | SPLIT | B12 | SPLIT: `SuspensionTune`, `AfterSetPosition` -> Veh; PartAdjustment `SuspensionApi` reflection -> Compat |
| Modules/Patrols/Vehicles/Templates.cs | 177 | Patrols (+Vehicles) | MOVE | B11 | MOVE; vehicle-structure part (`Body/Parts/Bumpers/Trailer`) consumed by Veh assembly |
| Modules/Patrols/Vehicles/TrailerSupport.cs | 97 | Vehicles | MOVE | B12 | MOVE |
| Modules/Patrols/Vehicles/VehicleAttachments.cs | 29 | Vehicles | MOVE | B12 | MOVE |
| Modules/Patrols/Vehicles/VehicleRules.cs | 19 | Patrols | MOVE | B11 | MOVE; `CanTemplate` DELETE (dead, always true) |
| Modules/Patrols/WarRig/DustStormBridge.cs | 61 | Compatibility | MOVE | B17 | MOVE (calls the Surv shelter query) |
| Modules/Patrols/WarRig/Factory.cs | 361 | Vehicles | MOVE | B12 | MOVE (ns `.WarRig`) |
| Modules/Patrols/WarRig/Glb.cs | 226 | Core | MERGE-INTO | B01 | MERGE-INTO Core Glb (dup of Towns/GasStation/Glb.cs, Survival/StewPot/Glb.cs) |
| Modules/Patrols/WarRig/GroundRecovery.cs | 272 | Vehicles | MOVE | B12 | MOVE |
| Modules/Patrols/WarRig/Json.cs | 117 | Core | MERGE-INTO | B01 | MERGE-INTO Core.Json (dup x5) |
| Modules/Patrols/WarRig/Layout.cs | 160 | Vehicles | MOVE | B12 | MOVE |
| Modules/Patrols/WarRig/NativeStormProtection.cs | 140 | Survival (+Vehicles) | SPLIT | B14 | SPLIT: storm patches -> Surv (weather owns storm damage; WeatherNative.cs already patches Explosion/storm FSMs); Veh registers War Rig as a shelter via ModAPI |
| Modules/Patrols/WarRig/PoiEncounter.cs | 145 | POI (+Vehicles, Core) | SPLIT | B13 | SPLIT: `Register/Spawn/Completion/Prepare/Wheels` -> POI (uses Pat/Veh spawn via ModAPI); `InstallRetention/BeforeCleanupDistance/Protected/DetachTankers` -> Core far-cleanup retention service (Performance PatrolsBridge already asks for it) |
| Modules/Patrols/WarRig/Props.cs | 255 | Vehicles | MOVE | B12 | MOVE |
| Modules/Patrols/WarRig/Settings.cs | 75 | Vehicles | MOVE | B12 | MOVE (parsers -> Core) |
| Modules/Patrols/WarRig/WarRigFeature.cs | 138 | Vehicles (+Compatibility) | SPLIT | B12 | SPLIT: install/prefab lookup/SpawnNearPlayer -> Veh; `SpawnerIntegration` -> Compat |
| Modules/Patrols/WarRig/WarRigUnit.cs | 161 | Vehicles | MOVE | B12 | MOVE (public saved component) |
| Modules/Patrols/WarRig/WorldSpawning.cs | 74 | Vehicles (+POI) | SPLIT | B12 | SPLIT: `CanSpawnAt/IsTemplate` -> Veh; `NaturalRigs/NaturalNoticeUntil` -> POI (map notice data) |

## A5 Guns + HUD + CleanHUD (73 files, 14362 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/CleanHUD/AccentTheme.cs | 92 | Core | MOVE | B01 | MOVE. Core `ToolWindow` (l.147-183) and `ReplacementGuard` (l.113-115) use it. |
| Modules/CleanHUD/Api.cs | 112 | HUD | MERGE-INTO | B10 | MERGE-INTO the internal `CompassMarkers` store behind Core `ICompassMarkers`. DELETE the unused public surface (§5). |
| Modules/CleanHUD/ApocaLanguageMenu.cs | 46 | Compatibility | MOVE | B17 | MOVE (provides `ILanguageMenu` to HUD menu) |
| Modules/CleanHUD/ApocapocketInventory.cs | 131 | Compatibility | MOVE | B17 | MOVE. It provides `IInventorySlots` to HUD and Presentation; PlayerBody `EquipmentInventory` reads Apocapocket separately. |
| Modules/CleanHUD/ApocasaverLoading.cs | 125 | Compatibility (+Saves) | MOVE | B17 | MOVE. `SaveActive` (l.13) -> a Saves query that ORs Apocasaver's state in. |
| Modules/CleanHUD/Assets.cs | 387 | Core (+Gunplay, HUD) | SPLIT | B01 | SPLIT. `Tex/White/VGradient/CompassLine/CompassShadow/Caret/Fade/Preload/Dispose` -> Core/UI theme assets (Core uses `Tex("panel_menu")` and `VGradient`). `WeaponInfo`, `Weapons` table, `Describe` -> Gunplay `WeaponCatalog` (`IWeaponStats`). `KeyOf/WeaponIcon/RenderStep/BeginRender/IconRenderer` -> HUD. `IconRenderer.Prefab` ... |
| Modules/CleanHUD/Bindings.cs | 231 | HUD | MOVE | B10 | MOVE. Ammo, health and grenades are parsed from UI text (`Number/Digits`). Later, read ammo from Gunplay `IWeaponState`. |
| Modules/CleanHUD/Binoculars.cs | 147 | HUD (+Core) | SPLIT | B10 | SPLIT. `PreCull/PostRender/Restore`, the wheel/lerp in `Tick` -> Core camera-zoom arbiter (same code as Ironsights). `Observe/Sees/Caption/Raised/Scoped` -> HUD. |
| Modules/CleanHUD/ChaseCameraHud.cs | 187 | Compatibility | MOVE | B17 | MOVE and merge with PlayerBody `ChaseCameraCompat.cs`. It provides `IDrivingTelemetry`/heading to HUD. Core `CorePages:49,141` reads `ChaseCameraHud.HudEnabled` directly. |
| Modules/CleanHUD/CleanHudModule.cs | 288 | HUD | MERGE-INTO | B10 | MERGE-INTO the single HUD module entry. `Log()` (l.278, `Log.Always`) is replaced by the Debug levels (§4). |
| Modules/CleanHUD/DamageDirection.cs | 101 | HUD | MOVE | B10 | MOVE; subscribe to Gunplay's Hurt event (§2, §5) |
| Modules/CleanHUD/DialogueWidgets.cs | 45 | HUD (+Core) | MOVE | B10 | MOVE. `DialogueWidgets.Gold` is used by Core `ToolWindow`/`ReplacementGuard`, so the palette goes to the Core theme. |
| Modules/CleanHUD/GlyphText.cs | 212 | Core | MOVE | B01 | MOVE. Used by Core `ToolWindow:24-26`, `ReplacementGuard:35-36`, `SaveRuntime:113`, and `SaveWarning:30`. |
| Modules/CleanHUD/Hud.cs | 937 | HUD | MOVE | B10 | MOVE and rename to `HudView`. `SaveWarningAnchor` (l.16, used by Core `SaveWarning.cs:26`) -> HUD ModAPI query. |
| Modules/CleanHUD/InventoryStyle.Drops.cs | 101 | HUD | MOVE | B10 | MOVE (already uses Core `InventorySupplies`) |
| Modules/CleanHUD/InventoryStyle.cs | 329 | HUD (+Core) | SPLIT | B10 | SPLIT. View/Build/Refresh/EquipmentPointer -> HUD. `Drop(Slot)` (l.262-302: native TakeItem/Visibility recipe plus armor/backpack capacity resets) -> a Core inventory service (`IInventory.DropEquipment`). It overlaps Gunplay `BackpackOverflow.Tick` (§5). |
| Modules/CleanHUD/Json.cs | 122 | Core | MERGE-INTO | B01 | MERGE-INTO `Core.Json`. There are 7 copies: CleanHUD, NPC/Women, Patrols/WarRig, Patrols/Vehicles/CarTemplateFile, PlayerBody/Body, Survival/StewPot, Towns/GasStation. |
| Modules/CleanHUD/Keys.cs | 691 | HUD | MOVE | B10 | MOVE. List WW keys through `Core.Hotkeys` instead of scanning WildWasteland.dll (§5). |
| Modules/CleanHUD/Layout.cs | 146 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/LoadingScreen.cs | 313 | HUD (+Saves) | MOVE | B10 | MOVE. Drive it from Saves ModAPI events (Begin/Progress/End) instead of SaveBus states plus polling `Core.SinglePlayerSaving.Serializing` (l.256) and `SaveSystem.AutomaticSaving`. |
| Modules/CleanHUD/LocationMarkers.cs | 187 | HUD (+Saves) | MOVE | B10 | MOVE. Sidecar IO (`WorldReady` l.45, `SaveEnded` l.62) -> Saves sidecar API. Add an `ILocationMarkers` ModAPI (exclude root, override icon) so Towns stops Harmony-patching it (§2). |
| Modules/CleanHUD/MapFog.cs | 337 | HUD (+Saves, Compatibility) | SPLIT | B10 | SPLIT. Grid/`Paint/PaintStep/Tick/Build/Recenter/Hides/Explored` -> HUD. `SaveEnded/SaveState/Write/Read/WorldReady` file IO -> the Saves sidecar API. Immersive Map import (`WorldReady` l.262 source path; `Init` stand-down l.54-55) -> Compatibility. |
| Modules/CleanHUD/MapFrame.cs | 58 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/MapIcons.cs | 41 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/Menu.cs | 1542 | HUD (+Saves, Core) | SPLIT | B10 | SPLIT. `MenuButton/BarDrag/MenuScrollRect/ClickZone` -> HUD/UI widgets. `SettingsModel`/`Display` (native SaveSettings.es3/Settings.xml) -> HUD menus. The `Menu` screens -> HUD. Save/load pieces (`BeginLoad` l.938, `SaveFile/SlotUsed`, `LatestSave` l.950, `SlotLabel`, `ContinueLatest` l.973, `OpenSave` l.976, `ShowSlots` l.10... |
| Modules/CleanHUD/MenuReference.cs | 215 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/MessageLog.cs | 164 | HUD | MOVE | B10 | MOVE. `MessageHook.Push` becomes the HUD `INotifications` service; Core `SaveSystem.cs:121` calls the type directly today. |
| Modules/CleanHUD/PauseCursorFix.cs | 28 | Core | MOVE | B01 | MOVE (vanilla fix; affects the F3 EditorSession) |
| Modules/CleanHUD/QuestDialogue.cs | 146 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/QuestJournal.cs | 89 | HUD | MOVE | B10 | MOVE; drop the dead usings |
| Modules/CleanHUD/QuestMap.cs | 239 | HUD | MOVE | B10 | MOVE and rename `MapMarkers`; it is not quest-specific. Its consumers are Quests, Patrols, Towns and PlayerBody. |
| Modules/CleanHUD/QuestNotifications.cs | 17 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/QuestTracker.cs | 80 | HUD | MOVE | B10 | MOVE; drop the dead usings (l.3-11) |
| Modules/CleanHUD/SceneCache.cs | 110 | Core | MERGE-INTO | B01 | MERGE-INTO a Core scene index next to `GameRefs`. It duplicates GameRefs lookups (`__GameManager__`, `PlayerCamera`). LocationMarkers and KeyCatalog become subscribers. |
| Modules/CleanHUD/TalkDialogue.cs | 163 | Compatibility (+Core) | SPLIT | B17 | SPLIT. Apocatalker reflection (`Install/Get/Changed/Tick` source reads, `RestoreSource`) -> Compatibility; it feeds an `IQuestConversation`-like model to HUD's dialogue window (reuse the QuestDialogue view). `Wrap()` (l.132, used by QuestJournal and QuestDialogue) -> Core/UI `GlyphFont.Wrap`. |
| Modules/CleanHUD/VantageDiscovery.cs | 60 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/VehicleStatusIcon.cs | 86 | HUD | MOVE | B10 | MOVE |
| Modules/CleanHUD/VistasBanners.cs | 54 | HUD | MOVE | B10 | MOVE. The bridge already lives in Core/VistasWorldGen, which should become Compatibility; this widget stays HUD. |
| Modules/CleanHUD/VistasEngineHud.cs | 50 | Compatibility | MOVE | B17 | MOVE (uses HUD `INotifications`) |
| Modules/CleanHUD/WeaponsPackIcons.cs | 60 | Compatibility | MERGE-INTO | B17 | MERGE-INTO the single WeaponsPack bridge (see WeaponsPackSound) |
| Modules/CleanHUD/WeatherMap.cs | 124 | HUD | MOVE | B10 | MOVE |
| Modules/Guns/Ammo/AmmoMechanics.cs | 558 | Gunplay (+Core) | SPLIT | B09 | SPLIT. `AmmoRunner`+`Unloader`+`GetButtonDown_Patch`+`Fsm_Event_Patch` -> Gunplay/Ammo. `BackpackOverflow`+`AmmoCapacity_Patch` -> Gunplay/Ammo. `BackpackSlot_Patch` (l.514, direct `FsmState.OnEnter`) -> MERGE-INTO the `Hooks.EnterState` postfix. `AmmoBox` (l.373-502) -> MERGE-INTO a Core `IItemSpawner` (save-registered spawn... |
| Modules/Guns/Ammo/AmmoSettings.cs | 40 | Gunplay | MOVE | B09 | MOVE. `ReloadHeld` key fallback could use `Core.Hotkeys.Held`. |
| Modules/Guns/Ammo/ArmorOverflow.cs | 70 | Gunplay | MOVE | B09 | MOVE. Uses `AmmoBox.Drop("armorplate")`. |
| Modules/Guns/Ammo/ArmorOverflowTransfer.cs | 43 | Gunplay | MERGE-INTO | B09 | MERGE-INTO `OverflowTransfer` as a generic/numeric single class (the logic is a line-for-line duplicate) |
| Modules/Guns/Ammo/MagazineLedger.cs | 85 | Gunplay | MOVE **D** | B09 | Magazine ledger text codec (\|1 format of ES3 key Apocaunloader.Magazines) - format frozen. |
| Modules/Guns/Ammo/OverflowTransfer.cs | 44 | Gunplay | MERGE | B09 | MERGE (see above) |
| Modules/Guns/Ammo/PerGunAmmo.cs | 321 | Gunplay (+Saves) | SPLIT | B09 | SPLIT. The ledger logic (`Draw/Holster/Capture/AdoptDrawn/Tick`) -> Gunplay. The save plumbing (`BeforeState` SaveLoadGame branch l.133-142, `SaveGlobal/CacheSnapshot/BeforeStore/LoadGlobal`, the three ES3 patch classes l.301-320) -> the Saves ES3-key/sidecar API, which today duplicates SaveBus. |
| Modules/Guns/ApocaspawnerDamage.cs | 37 | Compatibility | MOVE | B17 | MOVE + REWRITE: compiled field delegates; get the multiplier from `IWeaponStats` |
| Modules/Guns/BloodFxCompat.cs | 55 | Compatibility | MOVE | B17 | MOVE (nothing gun-specific); compiled getter for `relayOwner` |
| Modules/Guns/Bosses/Bosses.cs | 148 | NPCAI | MOVE | B07 | MOVE + REWRITE `Scan()` (l.83-96, `FindObjectsOfType` every 5/60 s) |
| Modules/Guns/Camera/Ironsights.cs | 131 | Gunplay (+Core) | SPLIT | B09 | SPLIT. `BeforeGetButton` (toggle), the `Tick` zoom target, and `Aiming` -> Gunplay. `PreCull/PostRender/Restore` (l.112-129, projection write) -> a Core camera-zoom arbiter shared with CleanHUD `Binoculars` and PlayerBody `ThirdPerson`. |
| Modules/Guns/Camera/WeaponTransition.cs | 122 | Gunplay (+Presentation) | SPLIT | B09 | SPLIT. `BeforeEvent/AfterActivate/Tick/Replay` (switch timing) -> Gunplay. `Pose()` (l.112-121, `Parent` offset) -> Presentation as `IWeaponPose.SetLowerWeight(w)`. |
| Modules/Guns/EmptyGunSound.cs | 35 | Gunplay | MOVE | B09 | MOVE. Add a length gate to the filter (§6). |
| Modules/Guns/GunsApi.cs | 72 | Gunplay (+Core) | SPLIT | B09 | SPLIT: `GunsApi` -> Gunplay ModAPI provider, extended to `IWeaponStats` (§7, design note). `Bridge.Shot/Hurt/PlayerHit/PartHit` -> replace with Core events WeaponFired/Hurt/ProjectileHit/VehiclePartHit. `Bridge.SpreadFactor` stays a query on `INpcApi`. |
| Modules/Guns/GunsModule.cs | 371 | Gunplay (+Core, NPCAI) | REWRITE | B09 | REWRITE as `GunplayModule`. `PlayerRig` (l.350-370) -> MERGE-INTO Core `GameRefs` (PlayerCamera/WeaponsArm/Parent); HUD `CrosshairController.NeedsCocking` and PlayerBody need it too. The boss part of `AfterCreateObject` (l.326-337) moves with Bosses to NPCAI. `SyncHud` (l.310) is deleted once hits go over the event bus (§7). |
| Modules/Guns/GunsPages.cs | 115 | Gunplay (+NPCAI) | SPLIT | B09 | SPLIT: `LiveBosses` and the Bosses page go to NPCAI; the rest MOVE |
| Modules/Guns/LegacyConfig.cs | 75 | Gunplay | MOVE | B09 | MOVE. Keep it (first-run only). Add the `[Guns*]` -> `[Gunplay*]` section migration here if the Id changes (§9). l.110 logs a migration with `Log.Info`; it should use `Log.Always` (AGENTS 5.5). |
| Modules/Guns/Tracers/BoltAttachment.cs | 117 | Gunplay | MOVE | B09 | MOVE + REWRITE `BoltSurface` (§6) |
| Modules/Guns/Tracers/Bolts.cs | 99 | Gunplay | MOVE | B09 | MOVE |
| Modules/Guns/Tracers/Orbs.cs | 213 | Gunplay | MOVE | B09 | MOVE |
| Modules/Guns/Tracers/Tracers.cs | 1788 | Gunplay (+Core) | SPLIT | B09 | SPLIT by class/method: **NPC shots**: `BeforeRayHit`, `Info`, `MuzzleOf`, `VelocityOf`, `TrackPlayer`, `GunKindOf` -> Gunplay/Projectiles. **Player shots**: `BeforeRaycast`, `MakeGun`, `FirstHit`, `SpreadOffset` -> Gunplay/Projectiles; the stats parts of `MakeGun` go behind `IWeaponStats`. **Ballistics**: `Step`, `RenderOf`, ... |
| Modules/Guns/Tracers/Wav.cs | 61 | Core | MERGE-INTO | B01 | MERGE-INTO a Core audio decoder. Duplicates: `Modules/NPC/Women/Wav.cs`, `Core/VoiceAudio.cs`, and the RIFF code in `Modules/Patrols/Crews/Rider.cs`. |
| Modules/Guns/WeaponDamageText.cs | 33 | HUD | MOVE | B10 | MOVE + REWRITE. Read the value from `IWeaponStats` instead of the literal. The `UiTextSetText.OnEnter` target is shared with `MessageHook` -> a Hooks dispatcher. |
| Modules/Guns/WeaponsPackSound.cs | 49 | Compatibility | MERGE-INTO | B17 | MERGE-INTO one WeaponsPack bridge (with NPC `WeaponsPackCompat.cs`, PlayerBody `WeaponsPackShadows.cs`, CleanHUD `WeaponsPackIcons.cs`). It needs the stab clip from Gunplay through ModAPI. |
| Modules/HUD/Crosshair/CrosshairController.cs | 281 | HUD | MOVE | B10 | MOVE. `NeedsCocking` (l.26-47) reads weapon FSMs -> Gunplay query `IWeaponState.NeedsCocking`. Remove the `GameObject.Find` at l.141. Expose `PreparePreview/DrawPreview` through a HUD ModAPI; Core `CorePages` calls the type directly today. |
| Modules/HUD/Crosshair/CrosshairMigration.cs | 31 | HUD | MOVE | B10 | MOVE (first-run config migration; keep) |
| Modules/HUD/HitFeedback/HeadshotAudio.cs | 139 | HUD | MOVE | B10 | MOVE |
| Modules/HUD/HitFeedback/HeadshotSoundGate.cs | 20 | HUD | MOVE | B10 | MOVE |
| Modules/HUD/HitFeedback/Hud.cs | 212 | HUD | REWRITE | B10 | REWRITE and rename to `HitFeedbackOverlay` (two `Hud` classes today). Draw it on the CleanHUD uGUI canvas with GlyphText so the IMGUI Gui job is not needed; keep the merge/lifetime logic. |
| Modules/HUD/HitFeedback/VanillaHits.cs | 150 | Gunplay | SPLIT | B09 | SPLIT. Hit detection (`BeforeSetFsmFloat`, `CreatureHealth`, `Condition`, `Health`, `Flush`) -> Gunplay's single vanilla-hit observer, merged with NPC `VanillaEvents.AfterDamage`; it publishes the Hurt/PlayerHit event. HUD only subscribes. |
| Modules/HUD/HudModule.cs | 214 | HUD | MERGE-INTO | B10 | MERGE-INTO the single HUD module entry (with `CleanHudModule`). `HitFeedbackService` stays as a shim until Gunplay publishes the Hit events, then DELETE. `AssetPath/ReadAsset` -> Core `Assets`/`DiskResources`. |
| Modules/HUD/LegacyConfig.cs | 40 | HUD | MOVE | B10 | MOVE; merge with CleanHUD's migrations into one HUD LegacyConfig |

## A6 Towns + POI (49 files, 8520 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/POI/PoiGenerators.cs | 94 | POI | MOVE | B13 | MOVE. Fold the starting-chunk check from TownPoiSettings.AllowPoi into `Generate`. |
| Modules/POI/PoiModule.cs | 286 | POI | MOVE | B13 | MOVE. Merge the terrain-tile event plumbing and graph scan with TownsModule (both subscribe the same 4 TerrainTile events and both scan `graph.generators` for native POI outlets). `PoiSite` is a MonoBehaviour. |
| Modules/Towns/GasStation/Glb.cs | 229 | Core | MERGE-INTO | B01 | MERGE-INTO the Core glTF loader (same as Survival/StewPot/Glb.cs, Patrols/WarRig/Glb.cs, NPC/Women/Gltf.cs, PlayerBody/Body/Gltf.cs) |
| Modules/Towns/GasStation/Json.cs | 117 | Core | MERGE-INTO | B01 | MERGE-INTO Core.Json. It is byte-identical to Survival/StewPot/Json.cs apart from the namespace. |
| Modules/Towns/InteriorPopulation.cs | 265 | NPCAI | MOVE | B07 | MOVE as NPCAI population rules. `OwnTown`/`OwnMarker` become ModAPI registrations from POI. `CaptureNames`/`RestoreNames` save data needs an NPCAI sidecar or stays in the POI record through ModAPI. |
| Modules/Towns/NativePoiPopulation.cs | 180 | NPCAI | MOVE | B07 | MOVE as population rules. `Catalog()` is fed from the shared population catalog. |
| Modules/Towns/PoiDressLayout.cs | 135 | POI | MOVE | B13 | MOVE (data and IO). Tools writes through the POI ModAPI. Uses `GasStation.Json`, which moves to Core.Json. |
| Modules/Towns/PoiDressMarkers.cs | 141 | Tools (+Loot, NPCAI) | SPLIT | B16 | SPLIT. `Kinds`/`Categories`/`Category()` go to Tools (POI editor). `Build` for Items/CarParts and `Save`/`Load`/`Capture` go to Loot (marker spawn groups). Monsters spawn uses NPCAI pools. Hide/Exit semantics go to NPCAI. |
| Modules/Towns/PoiDresser.cs | 423 | Tools | MOVE | B16 | MOVE as the POI editor. Reaches POI only through ModAPI. |
| Modules/Towns/PoiDressing.cs | 224 | POI (+NPCAI) | SPLIT | B13 | SPLIT. `Site`/`Attach`/`Step`/`Build`/`Save`/`Sweep`/`Clear` go to POI. `BeforeMap` (Nav.Make prefix) and `Exit()` go to NPCAI ModAPI ("structure not ready" gate, exit-hint provider). DELETE `PoiDressNavigation` (L199-222, reflection `Invoke` into Nav privates) and replace it with `INavApi.ForgetStructure(root)`. |
| Modules/Towns/PoiPropCatalog.cs | 181 | Tools (+POI) | SPLIT | B16 | SPLIT. `Start`/`Step`/`Capture`/`Sources`/`All` and the marker gizmos go to Tools (editor only). `Resolve`/`Create`/`Pose` (runtime instantiation) go to POI. |
| Modules/Towns/PoiSpawnCatalog.cs | 115 | Loot | MOVE | B15 | MOVE as the Loot spawn-profile catalog. Monster profiles are consumed by NPCAI. |
| Modules/Towns/TownApproach.cs | 79 | NPCAI | MOVE | B08 | MOVE as a generic outline detour. |
| Modules/Towns/TownAssets.cs | 356 | POI (+NPCAI, Loot, Core) | SPLIT | B13 | SPLIT. `Load`/`Measure`/`Walls`/`HasMerchant`/`BossCamp`/`RoadInput`/`FindRoads` and car shells go to POI. `CatalogPopulation`/`IsBoss`/`Hounds`/`Population` go to an NPCAI population catalog. The spawner `Budgets` capture goes to Loot. `ColliderBox`/`Measure` collider-bounds code merges into a Core geometry helper (duplicate... |
| Modules/Towns/TownBakeBridge.cs | 76 | DELETE | DELETE | - | DELETE the bridge and REWRITE it as an NPCAI ModAPI (`Discover(root)`, `ActorSpawned`, `FloorCells`, `StructureRootAt`, `Enabled`, `Tuning`, `RegisterWaypointProvider`). The `Before`/`After` redirect logic moves into NPCAI's `Nav.Next`. |
| Modules/Towns/TownBakeData.cs | 156 | NPCAI | MERGE-INTO | B08 | MERGE-INTO the NPCAI Nav map reader. Expose a read-only numeric template through ModAPI (`INavApi.TryGetTemplate`). It currently duplicates NPC's reader and the NPC folder paths (L36-42). |
| Modules/Towns/TownBorderProps.cs | 76 | POI | MOVE | B13 | MOVE |
| Modules/Towns/TownData.cs | 164 | POI (+Core) | SPLIT | B13 | SPLIT. The data model goes to POI. `TownsApi` (L154-163, `internal Provider` typed as `TownsModule`) is REWRITTEN as a Core ModAPI interface. `TownSite.Parking()` and `Building(int)` have no callers. |
| Modules/Towns/TownDebris.cs | 25 | POI | MOVE | B13 | MOVE |
| Modules/Towns/TownDebug.cs | 60 | DELETE | DELETE **D** | - | DELETE (`Install` never called) |
| Modules/Towns/TownFactory.cs | 149 | POI (+Loot) | SPLIT | B13 | SPLIT. `NeedsStage`/`Stage`/`Building`/`Streets`/`KeepGeometry`/`Destroy` go to POI. `Resume`/`Capture`/`Get`/`Set` (spawner progress) go to a Loot spawner-progress service. The dead parking-slab branch in `Destroy` (L146) is removed with TownPavement. |
| Modules/Towns/TownFrontage.cs | 41 | POI | MOVE | B13 | MOVE |
| Modules/Towns/TownGasStation.cs | 617 | POI (+Loot, Tools) | SPLIT | B13 | SPLIT. `LoadModel`/`Shell`/`Capture`/`Plan*`/`Applied`/`Remove`/`Moved`/markers `Tick`/`Save`/`Load` go to POI. `Populate`/`Add` (loot spawner cloning) go to Loot. `TestTick`/`PlaceCamp`/`Camp10` test pad go to Tools (Navigation editor test). DELETE `SpawnEnemies` (const false, L51) with `EnemyPoints` and the enemy loop (L52,... |
| Modules/Towns/TownGroundPad.cs | 54 | POI (+Compatibility) | MOVE | B13 | MOVE. `VistasWorldGen.TerrainEdited` becomes a ModAPI event. |
| Modules/Towns/TownLateTerrain.cs | 111 | POI | MOVE | B13 | MOVE. The Vistas branch (L15) should read a Compatibility flag from ModAPI. |
| Modules/Towns/TownLoading.cs | 29 | Debug | MERGE-INTO | B03 | MERGE-INTO Debug timing (level 1 start/end, level 2 detail). DELETE `Report` and the string-building callers. |
| Modules/Towns/TownMarkers.cs | 171 | POI (+HUD) | SPLIT | B13 | SPLIT. `Add`/`Remove`/`Refresh`/discovery/`Owns`/`Building` go to POI (IMapMarkers/ICompassMarkers). The label hack (`Attach`/`Draw`, QuestMap.Tick postfix, `GlyphText`, reflection `_entries`/`Root`/`Image`) and `Observe`/`StartingCamp` (LocationMarkers patches) go to HUD as an `IMapMarkers` label/colour feature. |
| Modules/Towns/TownNames.cs | 51 | POI | MOVE | B13 | MOVE |
| Modules/Towns/TownNavigation.cs | 224 | NPCAI | MOVE | B08 | MOVE. This is NPC pathfinding: a generic multi-structure route graph fed with nodes and edges from the POI plan. The `Mask` constant goes to Core layer masks. |
| Modules/Towns/TownOutline.cs | 110 | POI | MOVE | B13 | MOVE. Exposed as geometry through ModAPI for NPCAI and Patrols. |
| Modules/Towns/TownOverlap.cs | 358 | NPCAI | MOVE | B08 | MOVE and REWRITE as an NPCAI-internal instance-overlay API (no reflection). |
| Modules/Towns/TownOverlayPlan.cs | 243 | POI (+NPCAI) | SPLIT | B13 | SPLIT. `TownDressingKit`/`TownDressingPlan` go to POI. `TownOverlayPlan` (`LotPatch`/`Build`/`Step`/`Inside`) goes to NPCAI, run from a POI plan through ModAPI. |
| Modules/Towns/TownParking.cs | 68 | POI | SPLIT | B13 | SPLIT. `Outside` and `Spec` go to POI. `Access` uses `Patrols.TownStop.Approach` (L30-31) and must be rewritten with a local geometry helper. |
| Modules/Towns/TownPaths.cs | 552 | POI (+Core) | SPLIT | B13 | SPLIT. `TownWorldRoads.Nearest`/`Sites` and `TownGround.Blocked`/`Protected`/`Wall`/`DoorLine` go to POI. `TownPaths.Heap` (L221-243) merges into a Core binary heap. DELETE `TownPaths.Grid` (L244-400), `TownPaths.Build` plus helpers (L402-552), `TownWorldRoads.Connections` (L80-97), and `TownGround.Height`/`Blocker`/`DoorFail... |
| Modules/Towns/TownPavement.cs | 23 | DELETE | DELETE **D** | - | DELETE (no references; "keep only the parking slab" is also gone) |
| Modules/Towns/TownPerimeter.cs | 194 | POI (+NPCAI, Patrols) | SPLIT | B13 | SPLIT. Static `TownPerimeter` geometry goes to POI. The walker instance of `TownVehicleGates` (`Walkers`) goes to NPCAI and the vehicle instance (`Vehicles`) goes to Patrols driving AI, both reading the outline through ModAPI. |
| Modules/Towns/TownPlanner.cs | 152 | POI | MOVE | B13 | MOVE, then REWRITE the hard-coded lot rules into template data (see 8b). |
| Modules/Towns/TownPoiSettings.cs | 70 | POI (+Core) | REWRITE | B13 | REWRITE inside POI: the policy becomes a POI-internal check in `PoiLocations.Generate`, and the World section comes from a Core UI extension point instead of patches. |
| Modules/Towns/TownPopulation.cs | 268 | POI (+Loot, NPCAI) | SPLIT | B13 | SPLIT. `Assign`/`Classify`/`Legacy`/`Percentage` go to POI. `NativeBudget`/`Capture`/`Allocate`/`Configure`/`Force`/`Read`/`Write` go to Loot (generic "spawner control"), with NPCAI supplying the actor pools. `Created` (faction member count) becomes a POI consumer of an NpcSpawned event. |
| Modules/Towns/TownRoadBridge.cs | 58 | DELETE | DELETE **D** | - | DELETE. `Install`/`Add`/`Remove` are never called; only `Clear()` is (TownsModule.cs:728). |
| Modules/Towns/TownSaveRecord.cs | 73 | POI (+Core) | MOVE | B13 | MOVE. The ES3-cache + AfterSave re-save/verify pattern merges into a Core SaveBus helper (duplicated 3x, see 5). |
| Modules/Towns/TownSettings.cs | 95 | POI (+Tools) | SPLIT | B13 | SPLIT. Generation settings stay in POI and become per-template data for the Towns editor. `PoiDresserKey`, `TestGasStation`, `TestCamp10` and `GasStationScale` go to Tools. The `Bind(ConfigFile)` standalone path stays only as the shared body (see `BindStandalone`). |
| Modules/Towns/TownSiting.cs | 82 | POI | MOVE | B13 | MOVE |
| Modules/Towns/TownStore.cs | 128 | POI | MOVE | B13 | MOVE (format frozen, see 9) |
| Modules/Towns/TownSurfacePaint.cs | 73 | DELETE | DELETE **D** | - | DELETE (no references anywhere) |
| Modules/Towns/TownTowerKit.cs | 101 | POI (+NPCAI) | SPLIT | B13 | SPLIT. `Load`/`Prefab` go to POI. `Step`/`ReleaseBake` (reflection `Invoke` into Nav L64-94) become an NPCAI ModAPI `BakeIsolated(prefab)`. |
| Modules/Towns/TownWalkGraph.cs | 89 | POI (+NPCAI) | MOVE | B13 | MOVE to POI as plan data. NPCAI consumes `StreetNodes`/`StreetEdges` (see 5, duplicate pathfinding). |
| Modules/Towns/TownWalls.cs | 172 | POI | MOVE | B13 | MOVE |
| Modules/Towns/TownsModule.cs | 782 | POI (+NPCAI, Core, Tools, Debug) | SPLIT | B13 | SPLIT. Lifecycle (`Init`/`Shutdown`/`Prepare`/`Finalized`/`Applied`/`Moved`/`BuildStep`/`LoadBurst`/`Refresh`/`OnSaveBegin`/`OnLoadBegin`/`NewWorld`/`Remove`/`ClearSites`/`NeedsWalls`/`Finish`/`PopulationChanged`) go to POI. The Patrols-facing queries `TravelStops`/`MerchantSeeded`/`TravelRevision`/`PatrolBase`/`PatrolCount`/... |

## A7 Survival + Weather (24 files, 3870 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/Survival/StewPot/Factory.cs | 207 | Survival | MOVE | B14 | MOVE |
| Modules/Survival/StewPot/Glb.cs | 227 | Core | MERGE-INTO | B01 | MERGE-INTO the Core glTF loader |
| Modules/Survival/StewPot/Json.cs | 118 | Core | MERGE-INTO | B01 | MERGE-INTO Core.Json |
| Modules/Survival/StewPot/NaturalLoot.cs | 35 | Loot | MOVE | B15 | MOVE into a Loot substitution table (rule registered by Survival through ModAPI) |
| Modules/Survival/StewPot/Runtime.cs | 364 | Survival | MOVE | B14 | MOVE. Replace the own `live` FSM-state test (L153) with `GameRefs.InGame`. |
| Modules/Survival/StewPot/SpawnerBridge.cs | 65 | Compatibility | MOVE | B17 | MOVE into a Compatibility Apocaspawner bridge, merged with Patrols `SpawnerIntegration`. Survival registers its entry through ModAPI. |
| Modules/Survival/StewPot/Stew.cs | 289 | Survival | MOVE | B14 | MOVE |
| Modules/Survival/StewPot/WaterPour.cs | 136 | Survival (+Presentation) | MOVE | B14 | MOVE |
| Modules/Survival/SurvivalModule.cs | 131 | Survival (+Core, Debug) | MOVE | B14 | MOVE. `InstallAssets` merges into a Core "extract embedded file if missing" helper. `Verbose` merges into the Debug VerboseLog level. |
| Modules/Weather/WeatherAtmosphere.cs | 155 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherAudio.cs | 107 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherDebris.cs | 112 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherDiagnostics.cs | 183 | Debug | MOVE **D** | B03 | Debug (weather capture/snapshot tooling); delete once Denis accepts the storm renderer (UI says temporary). |
| Modules/Weather/WeatherDust.cs | 152 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherHazards.cs | 102 | Survival | MOVE | B14 | MOVE. War Rig shelter becomes `IShelterProvider` through ModAPI (see 5). |
| Modules/Weather/WeatherModule.cs | 352 | Survival (+Debug, Tools) | SPLIT | B14 | SPLIT. Regions/`Tick`/`TickHazards`/`ReconcileNative`/`Spawn` go to Survival (Weather feature). `CaptureDiagnostic`/`RestoreDiagnostic` go to Debug. `DrawTools` (preview buttons) goes to Tools/Debug page. |
| Modules/Weather/WeatherNative.cs | 319 | Survival | MOVE | B14 | MOVE. The storm prefab id and root lookup become a shared Survival ModAPI query (`IsStormRoot`), used by Patrols and NPCAI. |
| Modules/Weather/WeatherPalette.cs | 45 | Survival | MOVE | B14 | MOVE (config keys, see 9) |
| Modules/Weather/WeatherPerimeter.cs | 42 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherSky.cs | 120 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherSmoke.cs | 144 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherThunder.cs | 98 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherVisuals.cs | 301 | Survival | MOVE | B14 | MOVE |
| Modules/Weather/WeatherVortexSmoke.cs | 66 | Survival | MOVE | B14 | MOVE |

## A8 Quests + QuestsCompat + EngineSimCompat (28 files, 9753 lines)

| file | lines | target (+secondary) | action | B owner | notes |
|---|---|---|---|---|---|
| Modules/EngineSimCompat/EngineDiscovery.cs | 95 | Compatibility (+Core) | MOVE | B17 | MOVE. Use the Core WorldIndex walker instead of its own root walk. |
| Modules/EngineSimCompat/EngineSimCompatModule.cs | 295 | Compatibility (+Core) | SPLIT | B17 | SPLIT. The bridge moves to Compatibility. `IVehicleIgnition` stays a Core ModAPI service (consumers: Vehicles/PlayerBody `Car.TickIgnition`, Patrols `Patrol.StartUp/EngineRunning/StartEngine/StopEngine`). The `Get/Set/Call/Call<T>` Expression.Compile helpers (269-293) MERGE-INTO `Core.OptionalAccess`. |
| Modules/Quests/CrewHistory.cs | 67 | Quests | MOVE | B18 | MOVE. Switch to the Core sidecar API. Keep the QuestsCompat fallback read for one release, then retire it. |
| Modules/Quests/FactionDump.cs | 128 | Debug | MOVE | B03 | MOVE to Debug (or DELETE). It writes into the plugin folder (`PluginRuntime.cs:77`, `DataDir` = `plugins/WildWasteland/Quests`). |
| Modules/Quests/Factions.cs | 252 | Quests (+Core) | SPLIT | B18 | SPLIT. The file format and `Get/ByKey/Refresh/Load` stay in Quests. `OfObject/Friendly/Hostile/HumanFactions` become a Core faction query (NPCAI, Patrols and Towns also reason about Coyotes/Scrapyard). DELETE `Faction.HasNpc` (31, unused). |
| Modules/Quests/Game.cs | 823 | Core (+NPCAI, POI, Loot, Quests) | SPLIT | B01 | SPLIT. Core gets `Origin/Rel/Abs/Player/HDist/BaseName/FindInt/FindFloat/FindString/GameHours/MoneyVar/GetMoney/AddMoney/RegisterSpawned/SavedItemList/GroundAt` (shared game access). NPCAI ModAPI gets `FindNpcNames/FindEnemies/LiveNames/FindLive/FindPeople/FindBodies/SetPlayerEnemy/IsPlayerEnemy` (population queries). POI Mod... |
| Modules/Quests/Hire.cs | 1792 | Quests (+NPCAI, Patrols, Vehicles, Presentation, Gunplay) | SPLIT | B18 | SPLIT. Quests keeps the contract: `Init, CostFor, Start, Attach, Dismiss, dismissHint, Clear, Reset, Died, Gone, Shutdown` and the expiry part of `Update` (351-387). NPCAI gets follower behaviour via a new ModAPI `INpcApi.Follow/Escort`: `WakeBrain, Calm, ReleaseNpc, RestoreBrain, Threats, Inform, Hostile, IsCritter, Alive, E... |
| Modules/Quests/KeyMap.cs | 74 | Core | MERGE-INTO | B01 | MERGE-INTO a Core KeyMap. It is a byte-for-byte copy of `PlayerBody/KeyMap.cs` apart from comments. `Code/Build/Convert` are dead here. |
| Modules/Quests/MapLink.cs | 63 | Quests | MOVE | B18 | MOVE (already ModAPI-only). |
| Modules/Quests/NativeConversation.cs | 168 | Quests (+HUD, Compatibility) | SPLIT | B18 | SPLIT. Quests keeps `TalkBridge.Begin/AddLine/NativeButtons/Choose/Close` and `Plugin.CanInteract/OpenInteraction/InteractionFrame/FindInteraction/Person/FilterCrewButtons`. HUD gets `Plugin.DrawConversation` (the fallback dialogue UI). Compatibility gets `TalkBridge.InstallOptional/Opened/Closed/Uninstall` (the Talk patches). |
| Modules/Quests/Plugin.cs | 2501 | Quests (+HUD, Core, Debug) | SPLIT | B18 | SPLIT. Quests keeps `Frame/Tick/CheckEmptyCamp/ScoutCamp` (140-245, 545-562), rewards/fail/cancel (567-665), delivery (672-770), reputation (770-845), kills (861-935), `Format`, talk menu (`OnTalkOpen..DealRep`, 1022-1265), offers/accept/turn-in (1265-1960), talk actions (1960-2032), `RefreshLines/Objective/ShortStatus/Direct... |
| Modules/Quests/PluginRuntime.cs | 156 | Quests | MOVE | B18 | MOVE. Replace the string-state machine (`StateChanged` "LoadGame"/"isPlay"/"wait"/"SaveGame") with `OnLoadBegin/OnWorldReady/OnSaveBegin` and a Core SaveBus key API. |
| Modules/Quests/QueryCache.cs | 48 | Loot (+Core, NPCAI) | SPLIT | B15 | SPLIT. `Game.Matches` goes with the item query. `Hire.IsRangedNpc/NearestByName` go with the follower AI (NPCAI). |
| Modules/Quests/QuestConfig.cs | 203 | Quests (+NPCAI, Presentation) | MOVE | B18 | MOVE. Stop binding the unused `apocasetter` entry (:72, `[Quests / Legacy] Apocasetter`) but never purge the key. |
| Modules/Quests/QuestConfigMigration.cs | 77 | Core | MERGE-INTO | B01 | MERGE-INTO Core `ModuleConfig.MigrateMissing(entry, file, section, key)`. Survival, CleanHUD and NPC Women use the same "each absent entry" pattern. |
| Modules/Quests/QuestContent.cs | 103 | Quests | MOVE | B18 | MOVE. The legacy-folder discovery (`LegacyRoots`) could become a Core asset-migration helper. |
| Modules/Quests/QuestFollower.cs | 121 | NPCAI (+Presentation) | MOVE | B07 | MOVE to the NPCAI follower feature. It already uses only ModAPI (`IAnimationApi`, `INpcApi.SetFollowerDriven`, `IFollowerVehicles.Animated`). |
| Modules/Quests/QuestLogger.cs | 9 | DELETE | DELETE | - | DELETE. Replace with the Core leveled log (see 4). |
| Modules/Quests/QuestWorld.cs | 130 | Core | MERGE-INTO | B01 | MERGE-INTO a Core WorldIndex/SceneCache, shared with CleanHUD `SceneCache`, `EngineDiscovery`, Performance `FsmRegistry` and Patrols `Prefabs` (see 10d). |
| Modules/Quests/Quests.cs | 1120 | Quests | MOVE | B18 | MOVE. Split into one file per class. `Util.Norm/Esc/Unesc/Compass` could become Core text utils. |
| Modules/Quests/QuestsModule.cs | 86 | Quests | MOVE | B18 | MOVE. Fold the jobs that are double gated (see 6). |
| Modules/Quests/SavedWalk.cs | 13 | Quests (+NPCAI) | MERGE-INTO | B18 | MERGE-INTO Takeover (or the NPCAI walker store). The format lives inside AQ1 `W |
| Modules/Quests/Takeover.cs | 748 | Quests (+NPCAI, POI) | SPLIT | B18 | SPLIT. Quests keeps `PickCamp, CountDefenders, Fill, Begin, Stamps, DescribeWalkers` and `GiverTick` (quest-giver policy, but spawning through the NPCAI API). NPCAI gets `SpawnGroup, SpawnAround, PlaceNear, RandomSpotNear, Usable, Fighters, FindPrefab/ScanForPrefab, MoveFsm, SolidMask, Blocked, Steer, RoomAt, Update` (walker ... |
| Modules/Quests/TalkHook.cs | 349 | Compatibility | SPLIT | B17 | SPLIT. `TalkBtn` and the `IQuestConversation` members stay in Quests. `TalkBridge.Init/TalkingNow/Bind/Ui/PromptContent/NpcName/Npc/Say/You/WindowSkin/TrySkin*/TalkConfigString/ParseBorder/Signature/NeedsSync/SyncButtons/ClearButtons` and `TalkApiLink` go to Compatibility (Talk bridge), which talks to Quests through a Core di... |
| Modules/Quests/Voice.cs | 198 | Quests (+Core) | REWRITE | B18 | REWRITE. Use `Core.VoiceAudio.LoadWav` and delete `ReadWav` (125-183). While disabled, `Say` is dead code. |
| Modules/QuestsCompat/Binding.cs | 20 | DELETE | DELETE **D** | - | QuestsCompat retired 2026-10-18, never registered (Core/Plugin.cs:126-140). Quests/CrewHistory keeps reading the old QuestsCompat/Saves crew file for one release. |
| Modules/QuestsCompat/PluginLifetime.cs | 39 | DELETE | DELETE **D** | - | QuestsCompat retired 2026-10-18, never registered (Core/Plugin.cs:126-140). Quests/CrewHistory keeps reading the old QuestsCompat/Saves crew file for one release. |
| Modules/QuestsCompat/QuestsCompatModule.cs | 75 | DELETE | DELETE **D** | - | QuestsCompat retired 2026-10-18, never registered (Core/Plugin.cs:126-140). Quests/CrewHistory keeps reading the old QuestsCompat/Saves crew file for one release. |
