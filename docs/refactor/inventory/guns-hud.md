# Inventory: Guns + HUD + CleanHUD (target modules 3 Gunplay, 4 HUD, 11 Compatibility, 0 Core)

Scope: `src/Modules/Guns/**` (24 files, 4,682 lines), `src/Modules/HUD/**` (8 files, 1,087 lines), and `src/Modules/CleanHUD/**` (41 files, 8,593 lines). That is 73 `.cs` files in total. Paths are relative to `src/`. Line numbers are those of the snapshot (±2).

## 1. File map

### 1a. Guns -> Gunplay (and the parts that leave it)

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/Guns/GunsModule.cs | 371 | Module entry: 50+ config binds, hook/patch/job registration, CreateObject postfix, `PlayerRig` lookup | Gunplay (+Core, NPCAI) | REWRITE as `GunplayModule`. `PlayerRig` (l.350-370) -> MERGE-INTO Core `GameRefs` (PlayerCamera/WeaponsArm/Parent); HUD `CrosshairController.NeedsCocking` and PlayerBody need it too. The boss part of `AfterCreateObject` (l.326-337) moves with Bosses to NPCAI. `SyncHud` (l.310) is deleted once hits go over the event bus (§7). |
| Modules/Guns/GunsApi.cs | 72 | `GunsApi : IGunsApi` provider; `Bridge` forwards to Services.Npc and Services.Hud | Gunplay + Core | SPLIT: `GunsApi` -> Gunplay ModAPI provider, extended to `IWeaponStats` (§7, design note). `Bridge.Shot/Hurt/PlayerHit/PartHit` -> replace with Core events WeaponFired/Hurt/ProjectileHit/VehiclePartHit. `Bridge.SpreadFactor` stays a query on `INpcApi`. |
| Modules/Guns/GunsPages.cs | 115 | F7 pages (Tracers, NPC aim, Ammo, Armor, Bosses) | Gunplay (Bosses page -> NPCAI) | SPLIT: `LiveBosses` and the Bosses page go to NPCAI; the rest MOVE |
| Modules/Guns/LegacyConfig.cs | 75 | First-run migration from gunplay/apocaraider/apocaunloader cfgs | Gunplay | MOVE. Keep it (first-run only). Add the `[Guns*]` -> `[Gunplay*]` section migration here if the Id changes (§9). l.110 logs a migration with `Log.Info`; it should use `Log.Always` (AGENTS 5.5). |
| Modules/Guns/Ammo/AmmoMechanics.cs | 558 | `AmmoRunner` (hold-to-unload), `BackpackOverflow`, `Unloader`, `AmmoBox` drop, 4 patch classes | Gunplay (+Core) | SPLIT. `AmmoRunner`+`Unloader`+`GetButtonDown_Patch`+`Fsm_Event_Patch` -> Gunplay/Ammo. `BackpackOverflow`+`AmmoCapacity_Patch` -> Gunplay/Ammo. `BackpackSlot_Patch` (l.514, direct `FsmState.OnEnter`) -> MERGE-INTO the `Hooks.EnterState` postfix. `AmmoBox` (l.373-502) -> MERGE-INTO a Core `IItemSpawner` (save-registered spawn); `InventorySupplies.Drop` and Loot need the same recipe. |
| Modules/Guns/Ammo/AmmoSettings.cs | 40 | Ammo config fields, Reload input read with key fallback | Gunplay | MOVE. `ReloadHeld` key fallback could use `Core.Hotkeys.Held`. |
| Modules/Guns/Ammo/ArmorOverflow.cs | 70 | FloatClamp prefix: drops armor plates when armor capacity shrinks | Gunplay (ambiguous, §8) | MOVE. Uses `AmmoBox.Drop("armorplate")`. |
| Modules/Guns/Ammo/ArmorOverflowTransfer.cs | 43 | Float version of the overflow state machine | Gunplay | MERGE-INTO `OverflowTransfer` as a generic/numeric single class (the logic is a line-for-line duplicate) |
| Modules/Guns/Ammo/OverflowTransfer.cs | 44 | Int version of the overflow state machine | Gunplay | MERGE (see above) |
| Modules/Guns/Ammo/MagazineLedger.cs | 85 | Per-item magazine ledger plus `Apocaunloader.Magazines|1` text codec | Gunplay | MOVE. Save format is frozen (§9). LINQ `OrderBy` at l.49-50 runs at save time only. |
| Modules/Guns/Ammo/PerGunAmmo.cs | 321 | Per-gun magazine bind on draw/holster; ES3 inject on SaveAll/StoreCachedFile; read on LoadAll | Gunplay + Saves | SPLIT. The ledger logic (`Draw/Holster/Capture/AdoptDrawn/Tick`) -> Gunplay. The save plumbing (`BeforeState` SaveLoadGame branch l.133-142, `SaveGlobal/CacheSnapshot/BeforeStore/LoadGlobal`, the three ES3 patch classes l.301-320) -> the Saves ES3-key/sidecar API, which today duplicates SaveBus. |
| Modules/Guns/Bosses/Bosses.cs | 148 | Caps human boss HP (Duke Ironjaw, Buzzgut) at a percentage | NPCAI (ambiguous, §8) | MOVE + REWRITE `Scan()` (l.83-96, `FindObjectsOfType` every 5/60 s) |
| Modules/Guns/Camera/Ironsights.cs | 131 | Toggle-ADS input override plus wheel zoom by player-camera projection hack | Gunplay + Core | SPLIT. `BeforeGetButton` (toggle), the `Tick` zoom target, and `Aiming` -> Gunplay. `PreCull/PostRender/Restore` (l.112-129, projection write) -> a Core camera-zoom arbiter shared with CleanHUD `Binoculars` and PlayerBody `ThirdPerson`. |
| Modules/Guns/Camera/WeaponTransition.cs | 122 | Delays weapon-switch events and lowers/raises the first-person weapon | Gunplay + Presentation | SPLIT. `BeforeEvent/AfterActivate/Tick/Replay` (switch timing) -> Gunplay. `Pose()` (l.112-121, `Parent` offset) -> Presentation as `IWeaponPose.SetLowerWeight(w)`. |
| Modules/Guns/EmptyGunSound.cs | 35 | Plays first-person empty-click and reload sounds at the camera | Gunplay | MOVE. Add a length gate to the filter (§6). |
| Modules/Guns/WeaponDamageText.cs | 33 | Swaps the crossbow tooltip "Damage - 20" for "- 50" while projectiles are on | HUD | MOVE + REWRITE. Read the value from `IWeaponStats` instead of the literal. The `UiTextSetText.OnEnter` target is shared with `MessageHook` -> a Hooks dispatcher. |
| Modules/Guns/ApocaspawnerDamage.cs | 37 | Postfix on `Apocaspawner.Parameters.WeaponDamage`: crossbow x2.5 | Compatibility | MOVE + REWRITE: compiled field delegates; get the multiplier from `IWeaponStats` |
| Modules/Guns/BloodFxCompat.cs | 55 | Keeps BloodFX running after its plugin object is destroyed; suppresses its F8 test hit | Compatibility | MOVE (nothing gun-specific); compiled getter for `relayOwner` |
| Modules/Guns/WeaponsPackSound.cs | 49 | Weapons Pack chainsaw contacts use WW's stab clip | Compatibility | MERGE-INTO one WeaponsPack bridge (with NPC `WeaponsPackCompat.cs`, PlayerBody `WeaponsPackShadows.cs`, CleanHUD `WeaponsPackIcons.cs`). It needs the stab clip from Gunplay through ModAPI. |
| Modules/Guns/Tracers/Tracers.cs | 1788 | Projectile sim for NPC and player shots, hit replay, penetration, vehicle damage, melee extras, tracer mesh | Gunplay (+Core) | SPLIT by class/method: **NPC shots**: `BeforeRayHit`, `Info`, `MuzzleOf`, `VelocityOf`, `TrackPlayer`, `GunKindOf` -> Gunplay/Projectiles. **Player shots**: `BeforeRaycast`, `MakeGun`, `FirstHit`, `SpreadOffset` -> Gunplay/Projectiles; the stats parts of `MakeGun` go behind `IWeaponStats`. **Ballistics**: `Step`, `RenderOf`, `Falloff`, `RangeOf`, `HitTarget`, `PlayerHit(ref Shot…)`, `Replay`, `HeadFactor`, `Penetrates`, `WentThrough`, `MatOf`, `HitWorld`, `PopOff`, `IsWheel`, `OnCar`, the virtual player hitbox (`PlayerHit(Vector3…)`, `RaySphere`, `SegCapsule`, `PlayerCapsule`, `BodyRootOf`, `HeadOf`) -> Gunplay/Ballistics. **Melee**: `AfterGetLayer`, `AfterSetAudioClip`, `BeforeMeleeEffect`, `IsBlade`, `StabClip`, `IsPlayerMelee`, `AfterSetFsmFloat`, `SwingAtFittedWheels`, `FittedWheelAt`, `SwingDamage`, `PartCondition`, `CheckMeleeWheels` -> Gunplay/Melee. **Effects**: `FindEffects`, `WorldImpact`, `MetalSparks`, `IsCarMetal` -> Gunplay/Effects. **Drawing**: `Draw` -> Gunplay/Tracers. **Actor/part classification** (`Creature`, `RootOf`, `OwningPart`, `IsVehiclePart`, `HasBodypart`, `IsCorpse`, `HealthOf`, `IsPlayerObj`, `col_isHead`) -> MERGE-INTO a Core `ActorQuery` service. HUD `VanillaHits` (l.313-343) and NPC duplicate this logic. |
| Modules/Guns/Tracers/Bolts.cs | 99 | Crossbow bolt model: cache native arrow mesh, launch, land/ricochet, expire 15 s | Gunplay | MOVE |
| Modules/Guns/Tracers/BoltAttachment.cs | 117 | Embeds bolt at skinned-mesh surface or bone; moves bolts to the corpse | Gunplay | MOVE + REWRITE `BoltSurface` (§6) |
| Modules/Guns/Tracers/Orbs.cs | 213 | Monster projectiles (TranslatePosition) stop at walls and penetrate car parts | Gunplay | MOVE |
| Modules/Guns/Tracers/Wav.cs | 61 | WAV PCM/float decoder | Core | MERGE-INTO a Core audio decoder. Duplicates: `Modules/NPC/Women/Wav.cs`, `Core/VoiceAudio.cs`, and the RIFF code in `Modules/Patrols/Crews/Rider.cs`. |

### 1b. HUD (old Combat HUD) -> HUD

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/HUD/HudModule.cs | 214 | Module entry and config; `HitFeedbackService : IHitFeedback`; asset path/read helpers; F7 page | HUD | MERGE-INTO the single HUD module entry (with `CleanHudModule`). `HitFeedbackService` stays as a shim until Gunplay publishes the Hit events, then DELETE. `AssetPath/ReadAsset` -> Core `Assets`/`DiskResources`. |
| Modules/HUD/LegacyConfig.cs | 40 | First-run import from gunplayhud/apocaraider cfg and the hidden sizes | HUD | MOVE; merge with CleanHUD's migrations into one HUD LegacyConfig |
| Modules/HUD/Crosshair/CrosshairController.cs | 281 | Crosshair style (vanilla/lines/dot), selection dot, red "needs cocking" tint, F3 previews | HUD | MOVE. `NeedsCocking` (l.26-47) reads weapon FSMs -> Gunplay query `IWeaponState.NeedsCocking`. Remove the `GameObject.Find` at l.141. Expose `PreparePreview/DrawPreview` through a HUD ModAPI; Core `CorePages` calls the type directly today. |
| Modules/HUD/Crosshair/CrosshairMigration.cs | 31 | Retired `TurnOffCrosshair` -> Crosshair 0 / SelectionDot off | HUD | MOVE (first-run config migration; keep) |
| Modules/HUD/HitFeedback/Hud.cs | 212 | IMGUI floating damage numbers, damage list, hit marker | HUD | REWRITE and rename to `HitFeedbackOverlay` (two `Hud` classes today). Draw it on the CleanHUD uGUI canvas with GlyphText so the IMGUI Gui job is not needed; keep the merge/lifetime logic. |
| Modules/HUD/HitFeedback/VanillaHits.cs | 150 | `SetFsmFloat.OnEnter` observer that derives vanilla player hits when projectiles are off | Gunplay | SPLIT. Hit detection (`BeforeSetFsmFloat`, `CreatureHealth`, `Condition`, `Health`, `Flush`) -> Gunplay's single vanilla-hit observer, merged with NPC `VanillaEvents.AfterDamage`; it publishes the Hurt/PlayerHit event. HUD only subscribes. |
| Modules/HUD/HitFeedback/HeadshotAudio.cs | 139 | Loads headshot.mp3 (UnityWebRequest coroutine), 2 voices | HUD | MOVE |
| Modules/HUD/HitFeedback/HeadshotSoundGate.cs | 20 | Voice/interval gate | HUD | MOVE |

### 1c. CleanHUD -> HUD / Compatibility / Core / Saves

| path | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/CleanHUD/CleanHudModule.cs | 288 | Module entry: 22 config entries with migrations; 24 jobs; provides VehicleStatus, Compass, Map, QuestUi | HUD | MERGE-INTO the single HUD module entry. `Log()` (l.278, `Log.Always`) is replaced by the Debug levels (§4). |
| Modules/CleanHUD/Hud.cs | 937 | uGUI HUD replacement: vitals, weapon/ammo, slots, compass, driving, vehicle status, events -> message log | HUD | MOVE and rename to `HudView`. `SaveWarningAnchor` (l.16, used by Core `SaveWarning.cs:26`) -> HUD ModAPI query. |
| Modules/CleanHUD/Bindings.cs | 231 | Finds native HUD texts/FSMs and hides the replaced vanilla graphics | HUD | MOVE. Ammo, health and grenades are parsed from UI text (`Number/Digits`). Later, read ammo from Gunplay `IWeaponState`. |
| Modules/CleanHUD/Layout.cs | 146 | layout.json parse/default/poll | HUD | MOVE |
| Modules/CleanHUD/Api.cs | 112 | Public `CleanHUD.Api.Compass` marker store (standalone v1 API) | HUD | MERGE-INTO the internal `CompassMarkers` store behind Core `ICompassMarkers`. DELETE the unused public surface (§5). |
| Modules/CleanHUD/AccentTheme.cs | 92 | Accent color theme, `AccentTint` mesh effect, sliced panel recolor | Core/UI | MOVE. Core `ToolWindow` (l.147-183) and `ReplacementGuard` (l.113-115) use it. |
| Modules/CleanHUD/GlyphText.cs | 212 | `GlyphFont` atlas text (IMGUI draw/measure/wrap) + `GlyphText` uGUI graphic | Core/UI | MOVE. Used by Core `ToolWindow:24-26`, `ReplacementGuard:35-36`, `SaveRuntime:113`, and `SaveWarning:30`. |
| Modules/CleanHUD/Assets.cs | 387 | Embedded texture cache, generated textures, weapon caliber/kind table, runtime weapon-icon renderer | Core/UI + Gunplay + HUD | SPLIT. `Tex/White/VGradient/CompassLine/CompassShadow/Caret/Fade/Preload/Dispose` -> Core/UI theme assets (Core uses `Tex("panel_menu")` and `VGradient`). `WeaponInfo`, `Weapons` table, `Describe` -> Gunplay `WeaponCatalog` (`IWeaponStats`). `KeyOf/WeaponIcon/RenderStep/BeginRender/IconRenderer` -> HUD. `IconRenderer.Prefab` (l.255-264, `FindObjectsOfTypeAll`) -> Core prefab index. |
| Modules/CleanHUD/DialogueWidgets.cs | 45 | uGUI builders (Rect/Text/Button/Scroll), Gold/Gray colors | HUD (colors -> Core/UI) | MOVE. `DialogueWidgets.Gold` is used by Core `ToolWindow`/`ReplacementGuard`, so the palette goes to the Core theme. |
| Modules/CleanHUD/Menu.cs | 1542 | Title/pause menu replacement: widgets, native settings model, slots, seed, settings and keys tabs, update check, CancelHook | HUD + Saves + Core | SPLIT. `MenuButton/BarDrag/MenuScrollRect/ClickZone` -> HUD/UI widgets. `SettingsModel`/`Display` (native SaveSettings.es3/Settings.xml) -> HUD menus. The `Menu` screens -> HUD. Save/load pieces (`BeginLoad` l.938, `SaveFile/SlotUsed`, `LatestSave` l.950, `SlotLabel`, `ContinueLatest` l.973, `OpenSave` l.976, `ShowSlots` l.1049 legacy fallback, `SaveTo` l.1084) -> call the Saves ModAPI (`ISaveMenu`), not Core internals. `CheckUpdates` (l.274, GitHub) -> Core. `CancelHook` (l.1533) -> HUD. |
| Modules/CleanHUD/MenuReference.cs | 215 | Native Tutorial/Codex pages restyled in the menu | HUD | MOVE |
| Modules/CleanHUD/Keys.cs | 691 | Controls catalog: game keys, mod cfg keys, Mono.Cecil scan of every plugin DLL, clash detection | HUD | MOVE. List WW keys through `Core.Hotkeys` instead of scanning WildWasteland.dll (§5). |
| Modules/CleanHUD/LoadingScreen.cs | 313 | Loading/saving/autosave screen: artwork, tips, progress | HUD (Saves-facing) | MOVE. Drive it from Saves ModAPI events (Begin/Progress/End) instead of SaveBus states plus polling `Core.SinglePlayerSaving.Serializing` (l.256) and `SaveSystem.AutomaticSaving`. |
| Modules/CleanHUD/InventoryStyle.cs | 329 | TAB inventory restyle mirroring native sheet; equipment drop | HUD + Core/Fixes | SPLIT. View/Build/Refresh/EquipmentPointer -> HUD. `Drop(Slot)` (l.262-302: native TakeItem/Visibility recipe plus armor/backpack capacity resets) -> a Core inventory service (`IInventory.DropEquipment`). It overlaps Gunplay `BackpackOverflow.Tick` (§5). |
| Modules/CleanHUD/InventoryStyle.Drops.cs | 101 | Supply drop quantity dialog | HUD | MOVE (already uses Core `InventorySupplies`) |
| Modules/CleanHUD/MessageLog.cs | 164 | `MessageHook` (UiTextSetText postfix steals timed native messages) + top-left log | HUD | MOVE. `MessageHook.Push` becomes the HUD `INotifications` service; Core `SaveSystem.cs:121` calls the type directly today. |
| Modules/CleanHUD/DamageDirection.cs | 101 | Red arrows toward attackers on `Services.NpcHurt` | HUD | MOVE; subscribe to Gunplay's Hurt event (§2, §5) |
| Modules/CleanHUD/Binoculars.cs | 147 | Binocular wheel zoom (projection hack), scope detection, Observe/Sees raycast, caption | HUD + Core | SPLIT. `PreCull/PostRender/Restore`, the wheel/lerp in `Tick` -> Core camera-zoom arbiter (same code as Ironsights). `Observe/Sees/Caption/Raised/Scoped` -> HUD. |
| Modules/CleanHUD/QuestMap.cs | 239 | **General** map service `IMapMarkers`: map camera on/off, wheel zoom, markers, player icon, fog/frame sync | HUD | MOVE and rename `MapMarkers`; it is not quest-specific. Its consumers are Quests, Patrols, Towns and PlayerBody. |
| Modules/CleanHUD/MapFog.cs | 337 | Fog-of-war grid (Immersive Map format), sliced paint, overlay, save/load, Immersive Map import | HUD + Saves + Compatibility | SPLIT. Grid/`Paint/PaintStep/Tick/Build/Recenter/Hides/Explored` -> HUD. `SaveEnded/SaveState/Write/Read/WorldReady` file IO -> the Saves sidecar API. Immersive Map import (`WorldReady` l.262 source path; `Init` stand-down l.54-55) -> Compatibility. |
| Modules/CleanHUD/MapFrame.cs | 58 | Restyles the native map frame | HUD | MOVE |
| Modules/CleanHUD/MapIcons.cs | 41 | Loads location icon PNGs from disk | HUD | MOVE |
| Modules/CleanHUD/LocationMarkers.cs | 187 | Native location discovery (mapIcon), compass/map markers, discovered set sidecar | HUD + Saves | MOVE. Sidecar IO (`WorldReady` l.45, `SaveEnded` l.62) -> Saves sidecar API. Add an `ILocationMarkers` ModAPI (exclude root, override icon) so Towns stops Harmony-patching it (§2). |
| Modules/CleanHUD/VantageDiscovery.cs | 60 | Elevation probe that scales discovery range; `Sees` raycast | HUD | MOVE |
| Modules/CleanHUD/WeatherMap.cs | 124 | Draws storm regions on the map from `Services.Weather` | HUD | MOVE |
| Modules/CleanHUD/SceneCache.cs | 110 | Bounded sliced scene walk: root/path cache, named FSMs, mapIcon discovery, key-FSM observe | Core | MERGE-INTO a Core scene index next to `GameRefs`. It duplicates GameRefs lookups (`__GameManager__`, `PlayerCamera`). LocationMarkers and KeyCatalog become subscribers. |
| Modules/CleanHUD/Json.cs | 122 | MiniJson | Core | MERGE-INTO `Core.Json`. There are 7 copies: CleanHUD, NPC/Women, Patrols/WarRig, Patrols/Vehicles/CarTemplateFile, PlayerBody/Body, Survival/StewPot, Towns/GasStation. |
| Modules/CleanHUD/VehicleStatusIcon.cs | 86 | Vector icons (ignition/handbrake/cassette) | HUD | MOVE |
| Modules/CleanHUD/PauseCursorFix.cs | 28 | Turns PlayMakerGUI off on entering Menu "play" (cursor relock bug) | Core/Fixes | MOVE (vanilla fix; affects the F3 EditorSession) |
| Modules/CleanHUD/QuestTracker.cs | 80 | Quest tracker rows on HUD from `Services.Quests.Rows` | HUD | MOVE; drop the dead usings (l.3-11) |
| Modules/CleanHUD/QuestJournal.cs | 89 | Journal window from `Services.Quests.JournalText` | HUD | MOVE; drop the dead usings |
| Modules/CleanHUD/QuestDialogue.cs | 146 | Native Quests conversation window (`IQuestConversation`) + `DialoguePointer` | HUD | MOVE |
| Modules/CleanHUD/QuestNotifications.cs | 17 | `IQuestPresentation` provider (Tracker/Journal/Conversation owned, Notify -> log) | HUD | MOVE |
| Modules/CleanHUD/TalkDialogue.cs | 163 | Mirrors **Apocatalker** (third-party Talk) `DialogueUI` into a CleanHUD window | Compatibility + Core/UI | SPLIT. Apocatalker reflection (`Install/Get/Changed/Tick` source reads, `RestoreSource`) -> Compatibility; it feeds an `IQuestConversation`-like model to HUD's dialogue window (reuse the QuestDialogue view). `Wrap()` (l.132, used by QuestJournal and QuestDialogue) -> Core/UI `GlyphFont.Wrap`. |
| Modules/CleanHUD/ApocaLanguageMenu.cs | 46 | Reads ApocaLanguage language list + SelectLanguage | Compatibility | MOVE (provides `ILanguageMenu` to HUD menu) |
| Modules/CleanHUD/ApocasaverLoading.cs | 125 | Apocasaver autosave screen hooks; restyled save-name popup; `SaveActive` | Compatibility (+Saves) | MOVE. `SaveActive` (l.13) -> a Saves query that ORs Apocasaver's state in. |
| Modules/CleanHUD/ApocapocketInventory.cs | 131 | Apocapocket 6-slot read, icons, native UI shift exclusion | Compatibility | MOVE. It provides `IInventorySlots` to HUD and Presentation; PlayerBody `EquipmentInventory` reads Apocapocket separately. |
| Modules/CleanHUD/ChaseCameraHud.cs | 187 | ChaseCamera telemetry transpiler, compass heading, HUD visibility | Compatibility | MOVE and merge with PlayerBody `ChaseCameraCompat.cs`. It provides `IDrivingTelemetry`/heading to HUD. Core `CorePages:49,141` reads `ChaseCameraHud.HudEnabled` directly. |
| Modules/CleanHUD/VistasBanners.cs | 54 | Place-entered banner from `Core.VistasWorldGen.PlaceEntered` | HUD | MOVE. The bridge already lives in Core/VistasWorldGen, which should become Compatibility; this widget stays HUD. |
| Modules/CleanHUD/VistasEngineHud.cs | 50 | Prefix on obfuscated World Amp `a.u.A()`: engine-heat overlay -> notifications | Compatibility | MOVE (uses HUD `INotifications`) |
| Modules/CleanHUD/WeaponsPackIcons.cs | 60 | Weapons Pack icon override + model lookup for icon key | Compatibility | MERGE-INTO the single WeaponsPack bridge (see WeaponsPackSound) |

## 2. Cross-module coupling

### 2a. Core.Services and Core members this area PROVIDES

| Provider (file:line) | Member | Signature |
|---|---|---|
| GunsModule.cs:231 / GunsApi.cs:11 | `Services.Guns` | `IGunsApi { int ContractVersion; bool ProjectilesEnabled; bool PlayerProjectilesEnabled; bool BlocksProjectile(Collider); float EffectiveRange(int kind); bool TryGetWeaponKind(GameObject owner, out int kind); bool HandlesFittedWheelMelee; }` (kinds 0..5 = Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow) |
| HudModule.cs:86 / :194 | `Services.Hud` | `IHitFeedback { bool Enabled; void PlayerHit(GameObject target, Vector3 point, float damage, bool head); void PartHit(GameObject target, Vector3 point, float percent); bool ExternalReporter {get;set;} }` |
| CleanHudModule.cs:147 / :204 | `Services.VehicleStatus` | `IVehicleStatusDisplay { bool TryShow(bool ignitionOff, bool handbrakeOn, bool playing, string volume); }` |
| CleanHudModule.cs:148 / :210-216 | `Services.Compass` | `ICompassMarkers { bool VehiclesEnabled; int Add(string owner, Func<Vector3> position, string icon, Color color); bool Exists(int); bool Remove(int); void Identify(int); void RemoveAll(string owner); }` |
| CleanHudModule.cs:149 / QuestMap.cs:10 | `Services.Map` | `IMapMarkers { Texture2D GetIcon(string); MapDetail Markers, Routes, TrafficMarkers; int Add(string owner, Func<Vector3>, string icon, Color, string label); bool Exists(int); void Remove(int); void RemoveAll(string); bool Hidden(Vector3 scene); bool Explored(Vector3 scene); bool Open; float Zoom; bool Observe(Transform, string, Color); }` |
| QuestNotifications.cs:14 | `Services.QuestUi` | `IQuestPresentation { bool Tracker; bool Journal; bool Conversation; bool Notify(string); }` |
| Tracers.cs:1071, 1556 | raises `Services.VehiclePartHit` | `RaiseVehiclePartHit(GameObject part, GameObject attacker)`; the only subscriber is Patrols `Crew.PartHit` (PatrolsModule.cs:62) |

### 2b. What this area CONSUMES

| file:line | Uses | Why |
|---|---|---|
| GunsApi.cs:36-43 | `Services.Npc.SpreadFactor(distance)` | NPC jitter scale; built-in fallback |
| GunsApi.cs:46-58 | `Services.Npc.Shot/Hurt` + `Services.ReportCombat()` (-> `SaveCombat.Activity`) | NPC hearing/knowledge; combat-safe saving |
| GunsApi.cs:60-70 | `Services.Hud.PlayerHit/PartHit` | Hit feedback |
| GunsModule.cs:272-273, 310-316 | `Services.Hud.ExternalReporter` | Silences HUD's vanilla observer (set every frame from `TracersTick`) |
| Ironsights.cs:38, EmptyGunSound.cs:23 | `Services.Animation.PlayerThirdPerson` | First-person-only zoom and sounds |
| Tracers.cs:335 | `Services.Animation.TryPlayerWeaponMuzzle` | 3rd-person muzzle origin |
| Tracers.cs:1387 | `Services.Animation.TryPlayerJumpHead` | Virtual head hitbox during jumps |
| Ironsights.cs:87, Binoculars.cs:59/88/103, VantageDiscovery.cs:43 | `Services.Map.Open` | Wheel ownership. Binoculars and VantageDiscovery are in the same module as the provider, so this is a self-reference through the service. |
| HudModule.cs:88-89, VanillaHits.cs:245,255 | `Services.Guns.ProjectilesEnabled` / null check | Quiet the vanilla observer when Guns reports |
| DamageDirection.cs:33 | event `Services.NpcHurt` | Raised by **NPC** `Senses.Hurt` (Senses.cs:1345) and `VanillaEvents` (l.186). The chain is Guns -> `INpcApi.Hurt` -> NPC -> event -> CleanHUD, so the arrows only work while NPC runs. |
| LocationMarkers.cs:36 | `Services.Npc.HighlightMissingPoiBake(label)` | Debug tint; QuestMap.Tick calls it per map marker per frame |
| LocationMarkers.cs:146 | `Services.Camps.At(world, 120f)` | Cleared-camp styling |
| WeatherMap.cs:16 | `Services.Weather` (`IWeatherRegions`) | Storm areas on the map |
| QuestTracker.cs:37, QuestJournal.cs:46, QuestDialogue.cs:44 | `Services.Quests` (`IQuestApi`, `IQuestConversation`) | Quest UI widgets (§2d) |
| LoadingScreen.cs:178,217,299; Menu.cs:1537 | `Core.LoadingProgress` | Progress text and the Cancel block |
| LoadingScreen.cs:256; ApocasaverLoading.cs:13; Menu.cs:974 | `Core.SinglePlayerSaving.Serializing/Load`, `Core.SaveSystem.AutomaticSaving` | Save presentation (§2e) |
| Menu.cs:940,952-954,977,1051 | `Core.SaveSystem.RecoveryError/Info/Installed`, `Core.SaveRuntime.Latest`, `Core.UI.ToolWindow.OpenSaves/OpenFromMenu` | Continue/Load/Save buttons |
| Menu.cs:453,892,1025,1044; MapFog.cs:55 | `Core.ReplacementGuard.MenuModalOpen/Status` | Notice suppression; Immersive Map stand-down |
| InventoryStyle*.cs | `Core.InventorySupplies.Count/Drop/Ready/Keys`, `Core.UI.EditorSession` | Supplies panel and modal input |
| CleanHudModule.cs:131 | `Core.TunedSettings.InstallProfiles("CleanHUD")` | Default profiles |
| VistasBanners.cs:17,33,47; VistasEngineHud.cs:26-28 | `Core.VistasWorldGen.*`, `Core.OptionalAccess` | Vistas bridges |
| LocationMarkers/MapFog/LoadingScreen | `SaveBus.StateChanged/AfterSave/ReportFailure/Slot/State/Busy`, `Core.AtomicFile` | Sidecars and screen timing |
| Menu.cs:280,289 | `Plugin.VERSION` | Update check |
| many | `GameRefs`, `FsmUtil`, `Origin`, `Hooks`, `Patches`, `Scheduler`, `ApocasetterBridge.InputBlocked/Present/GameFont`, `Assets`, `DiskResources`, `Legacy` | Standard Core |

### 2c. Rule violations (direct type references across modules, Core -> module)

| file:line | Reference | Fix |
|---|---|---|
| **Modules/Towns/TownMarkers.cs:9, 40-56** | `using WildWasteland.Modules.CleanHUD`; reflects private `LocationMarkers.Locations`, `QuestMap._entries/Entry.Id/Root/Image`, `CleanHudModule.ShowLocations/ShowCompass`; **Harmony-patches** `LocationMarkers.Observe` (prefix+postfix) and `QuestMap.Tick` (postfix) | Severe module-to-module break. Replace with HUD ModAPI `ILocationMarkers.Exclude(Transform root)`, `SetIcon(Transform root, string icon)`, and `IMapMarkers` entries that carry a size/style. Expose the compass/location toggles as read-only flags. |
| Core/UI/ToolWindow.cs:4, 24-26, 147-183 | `CleanHud.GlyphFont`, `AccentTheme`, `Assets.Tex/VGradient`, `DialogueWidgets.Gold`, `CleanHudModule.Running/MenuEnabled`, `Menu.MenuBackground()` | Move the theme/font/panel assets to Core/UI (§1c); the HUD module only sets "menu skin enabled" |
| Core/ReplacementGuard.cs:10, 35-36, 113-116 | same theme types | same |
| Core/SaveRuntime.cs:113; Core/SaveWarning.cs:3, 26, 30 | `GlyphFont.DrawGame`, `Hud.SaveWarningAnchor()` | Core/UI GlyphFont; HUD ModAPI anchor query |
| Core/SaveSystem.cs:121 | `CleanHUD.MessageHook.Push` | HUD `INotifications` service (null-checked) |
| Core/UI/CorePages.cs:6-8, 49, 76, 125-176, 203-208, 230-246, 322-326 | `HudModule.*` entries, `CrosshairController.PreparePreview/DrawPreview`, `CleanHudModule.*` entries, `ChaseCameraHud.HudEnabled`, `GunsModule.ToggleIronsights` | Each module contributes its rows to the Game page via a `ToolPage`/settings-contribution API; previews via the HUD ModAPI |
| Core/TunedSettings.cs:20, 44 | Embedded resource prefix `CleanHUD.DefaultSettings.*` / `CleanHUD.DefaultProfiles.*` (global defaults for **all** modules live under CleanHUD's asset tree) | Move to a `Core.*` resource prefix (asset-index rename, §9) |
| Core/SaveTypes.cs:35-36 | Alias strings `WildWasteland.Modules.Guns` / `.HUD` | Update if namespaces change (§9) |

There are no direct type references between Guns, HUD and CleanHUD themselves; HUD <-> Guns goes only through `Services.Hud`/`Services.Guns`.

### 2d. Quest UI pieces and Quests

All of these are typed-service only; none references Quests types.

| Piece | Direction | Contract | Notes |
|---|---|---|---|
| QuestNotifications.cs | HUD provides -> Quests consumes | `IQuestPresentation` (Quests `Plugin.cs:849` Notify; `PluginRuntime.cs:30-32` asks Tracker/Journal/Conversation "styled?" to suppress its own IMGUI) | Keep it; rename to `QuestPresentation` |
| QuestTracker.cs | HUD reads | `IQuestApi.Ready/TrackerEnabled/JournalOpen/Rows` (`QuestRow{Id,Text,Color}`) per frame | Built inside `Hud` ctor (`Hud.cs` builds `_quests`), ticked by `Hud.Tick` |
| QuestJournal.cs | HUD reads | `IQuestApi.JournalOpen/JournalText/Completed/JournalKey` | Own overlay canvas (sorting 32000) |
| QuestDialogue.cs | HUD reads + commands | `IQuestApi.Conversation` -> `IQuestConversation{Open,Name,Prompt,Revision,Chat,Choices,Choose(key),Close()}` | Sets `Menu.BlockCancelFrame` |
| QuestMap.cs | HUD provides | `IMapMarkers` used by Quests `MapLink.cs:19-55` (markers, Explored) | Also used by Patrols/Towns/PlayerBody |
| LocationMarkers / Api.Compass | HUD provides | `ICompassMarkers` used by Quests `MapLink` and Patrols/Towns | — |
| TalkDialogue.cs | **Not** native Quests | Apocatalker DialogueUI reflection; also mirrors the Quests "AQ" buttons injected into Talk's UI | -> Compatibility; `IQuestApi.ExternalOpened/ExternalClosed` are driven by Quests' own NativeConversation, not by CleanHUD |

### 2e. Menu/loading/save presentation the Saves module must interact with

| File | Today | Needed from Saves ModAPI |
|---|---|---|
| LoadingScreen.cs | Watches `SaveBus.StateChanged` (l.228), `LoadingScreen.AfterEnterState` on "Continue/clicked" for savegame/Yes_Save (l.222), polls `SinglePlayerSaving.Serializing` every frame (l.256), uses `SaveBus.AfterSave` | Events SaveStarted{slot, manual/auto}, SaveProgress, SaveFinished{ok}, LoadStarted/LoadFinished; `ILoadingScreen.Begin/End` owned by HUD |
| ApocasaverLoading.cs | Apocasaver AutosavePresentation Begin/End postfix; SaveNaming OnGUI replacement; `SaveActive` | Compatibility forwards to the same Saves events; naming popup = HUD widget |
| Menu.cs | Continue/Load/Save buttons call `SaveRuntime.Latest`, `SaveSystem.Info/RecoveryError/Installed`, `SinglePlayerSaving.Load`, `ToolWindow.OpenSaves`; legacy 10-slot UI clicks native buttons (`ShowSlots`, `SaveTo`) | `ISaveMenu { string LatestName; bool CanContinue; void Continue(); void OpenLibrary(bool save); string Blocker; }` |
| MapFog.cs / LocationMarkers.cs | Own sidecars `CleanHUD/Saves/<slot>.fog.bin` / `.locations.txt`, written at SaveLoadGame "wait" and "SaveGame" (twice) plus `OnSaveEnd`; Core `SaveFiles.cs:189` hard-codes their paths for copy/backup/remap | Saves sidecar API: register(owner, extension, write(stream), read(stream)) |
| Hud.cs `TickEvents` | Turns native "Saving game" text into a log line | SaveStarted event |
| PerGunAmmo.cs (Guns) | ES3 key injection on `SaveAll`/`StoreCachedFile` patches | Saves ES3 extra-key API (key, writer, reader) |

## 3. Harmony patches & scheduler jobs

### 3a. Patches

| file:line | target method | kind | via | per-call cost / filter |
|---|---|---|---|---|
| GunsModule.cs:200 (Tracers.BeforeRayHit) | `SensorGetDetectionRayHit.OnEnter` | prefix (skip + isNotIntersectedEvent) | Hooks.SensorRayHitEnter | Per NPC burst ray. `TracersEnabled`, `fsm.Name != "Damage Ranged"`. Shared with NPC VanillaEvents (prefix + finalizer). |
| GunsModule.cs:201 (Bosses.BeforeGetDetections) | `SensorGetDetections.DoAction` | prefix (observe) | Hooks.SensorDetectionsDo | Per frame per NPC. `BossHealth` then `fsm.Name != "Detection"`. Shared with NPC Senses. |
| GunsModule.cs:202 (Tracers.BeforeRaycast) | `Raycast.OnEnter` | prefix (skip + Finish) | Hooks.RaycastEnter | Every Raycast.OnEnter. `fsm.Name != "Attack"` (string compare, no length gate), repeatInterval, `_notGun` dictionary. Shared with NPC. |
| GunsModule.cs:203 (Tracers.AfterSetFsmFloat) | `SetFsmFloat.OnEnter` | postfix | Hooks.SetFsmFloatEnter | Many per frame. `MeleeOnWheels`, name length 6. Shared with NPC, Patrols and the HUD prefix. |
| GunsModule.cs:204 (Tracers.BeforeMeleeEffect) | `CreateObject.OnEnter` | prefix (skip + Finish) | Hooks.CreateObjectEnter | Per spawn; length 9 "HitEffect"; shared with Survival |
| GunsModule.cs:205 (GunsModule.AfterCreateObject) | `CreateObject.OnEnter` | postfix | Hooks.CreateObjectEnter | Per spawn; per-prefab name cache; shared with PlayerBody/NPC/Patrols |
| GunsModule.cs:206 (GetButtonDown_Patch) | `GetButtonDown.OnUpdate` | prefix (skips every Reload poll) | Hooks.GetButtonDownUpdate | Every GetButtonDown every frame. `Enabled` then `buttonName.Value != "Reload"` string compare; shared with Core/PlayerBody/NPC/Survival/CleanHUD |
| GunsModule.cs:207 (MagazineDrawPatch) | `ActivateGameObject.OnEnter` | prefix (observe) | Hooks.ActivateGameObjectEnter | Name length 12 |
| WeaponTransition.cs:26 (AfterActivate) | `ActivateGameObject.OnEnter` | postfix | Hooks.ActivateGameObjectEnter | Reference compare to cached Fsm |
| GunsModule.cs:208-209 (MagazineStatePatch) | `Fsm.EnterState` | prefix + postfix (observe) | Hooks.EnterState | Every state entry x2; name length 12. Shared with CleanHUD and Performance. |
| GunsModule.cs:210 (Fsm_Event_Patch) | `Fsm.ProcessEvent` | prefix (observe) | Hooks.ProcessEvent | Every FSM event; `Unloader.Busy` first |
| WeaponTransition.cs:25 (BeforeEvent) | `Fsm.ProcessEvent` | prefix (skip while lowering) | Hooks.ProcessEvent | Every FSM event; reference compare first. Shared with NPC Brain/DogAnimations. |
| Ironsights.cs:19 (BeforeGetButton) | `GetButton.DoGetButton` | prefix (skip, storeResult) | Hooks.GetButtonDo | Reference compare first; shared with NPC |
| GunsModule.cs:213 | `GetLayer.OnEnter` | postfix | Patches | `TracersEnabled`, `fsm.Name != "Attack"` |
| GunsModule.cs:214 | `SetAudioClip.OnEnter` | postfix | Patches | Name length 6. **Also patched by PlayerBody `FemalePain.cs:60` (postfix)**, which should become a Hooks dispatcher. |
| GunsModule.cs:215 | `TranslatePosition.OnFixedUpdate` | prefix (skip for orbs) | Patches | `_orbs.Count == 0` |
| AmmoMechanics.cs:508 (AmmoCapacity_Patch) | `IntClamp.DoClamp` | prefix (skip while drop pending) | Patches.ApplyClass | Every IntClamp everyFrame; length 4 + per-action dictionary. Performance PollSleep/HudFsms wrap these actions. |
| ArmorOverflow.cs:65 (ArmorCapacityPatch) | `FloatClamp.DoClamp` | prefix | Patches.ApplyClass | Length 13; Performance LightFlicker/HudFsms/PollSleep touch FloatClamp |
| AmmoMechanics.cs:514 (BackpackSlot_Patch) | `FsmState.OnEnter` | postfix | Patches.ApplyClass | **Every state entry of every FSM**, a second trampoline next to `Hooks.EnterState`. Length 13. Move it to Hooks.EnterState. |
| PerGunAmmo.cs:301 | `ES3PlayMaker.SaveAll.Enter` | postfix | Patches.ApplyClass | Per save; Apocasaver patches it too |
| PerGunAmmo.cs:308 | `ES3PlayMaker.LoadAll.Enter` | prefix | Patches.ApplyClass | Per load |
| PerGunAmmo.cs:315 | `ES3.StoreCachedFile(ES3Settings)` | prefix | Patches.ApplyClass | Per flush; Core SaveSystem/Towns/Quests call StoreCachedFile |
| ApocaspawnerDamage.cs:25 | `Apocaspawner.Parameters.WeaponDamage` | postfix | Patches (optional) | Reflection Get/SetValue + `go.name` per call |
| BloodFxCompat.cs:32 | `BloodFX.BloodRelay.OnParticleCollision` | prefix | Patches (optional, lazy from Bosses job) | `FieldInfo.GetValue` only after native destroyed |
| BloodFxCompat.cs:35 | `BloodFX.Plugin.TestHit(bool)` | prefix | Patches (optional) | Rare |
| WeaponsPackSound.cs:31 | `PlayStandardMeleeHitSound(Vector3,…)` | prefix | Patches (optional, lazy) | `FieldInfo.GetValue` per hit |
| WeaponDamageText.cs:8 | `UiTextSetText.OnEnter` | prefix + finalizer | Patches.ApplyClass | First checks `ProjectilesEnabled`; then FSM name. **Same target as CleanHUD MessageHook postfix (CleanHudModule.cs:154).** After the merge both are HUD, so use one handler or a Hooks dispatcher. |
| EmptyGunSound.cs:12 | `PlaySound.DoPlaySound` | prefix (skip) | Patches | Every PlaySound: 3 string compares + ActiveStateName before any cheap gate |
| HudModule.cs:92 (VanillaHits.BeforeSetFsmFloat) | `SetFsmFloat.OnEnter` | prefix (observe) | Hooks.SetFsmFloatEnter | Static bool first. Duplicates NPC `VanillaEvents.AfterDamage` (postfix on same target). |
| CleanHudModule.cs:154 (MessageHook.Postfix) | `UiTextSetText.OnEnter` | postfix | Patches | `!Active` static first |
| CleanHudModule.cs:155 (CancelHook.Prefix) | `GetButtonDown.OnUpdate` | prefix (skip Cancel) | Hooks.GetButtonDownUpdate | Static bools first |
| CleanHudModule.cs:156 (PauseCursorFix) | `Fsm.EnterState` | postfix | Hooks.EnterState | Name length 4 |
| CleanHudModule.cs:157 (LoadingScreen.AfterEnterState) | `Fsm.EnterState` | postfix | Hooks.EnterState | `state.Name != "clicked"` first |
| ChaseCameraHud.cs:81 | `ApocaChaseCamera.DrivingReadings.Capture` | transpiler | Patches (optional) | Observation only |
| ApocapocketInventory.cs:62 | `Apocapocket.ExtraSlots.BuildUi` | postfix | Patches (optional) | Rare |
| ApocasaverLoading.cs:27-28 | `Apocasaver.AutosavePresentation.Begin/End` | postfix | Patches (optional) | Rare |
| ApocasaverLoading.cs:44 | `Apocasaver.SaveNaming.OnGUI` | prefix (replace draw) | Patches (optional) | Per OnGUI while open |
| TalkDialogue.cs:53 | `ApocalypterTalk.DialogueUI.ShowDialogue/AppendChatLine/SetChoices` | postfix | Patches (optional) | Rare |
| VistasEngineHud.cs:33 | World Amp `a.u.A()` | prefix (skip while CleanHUD on) | Patches (optional) | Per source Repaint |
| WeaponsPackIcons.cs:30 | Weapons Pack `PackHudIcon(GameObject, out Texture2D)` | prefix | Patches (optional, lazy) | Rare |
| *(Towns)* TownMarkers.cs:53-55 | **CleanHUD's own** `LocationMarkers.Observe`, `QuestMap.Tick` | prefix/postfix | Patches | Per frame while the map is open. Violation, §2c. |

Non-Harmony global callbacks: `Camera.onPreCull/onPostRender` in Ironsights.cs:21-22 and Binoculars.cs:30 (plus PlayerBody ThirdPerson and 5 Weather classes). Also `Canvas.willRenderCanvases` in Hud.cs (BeforeRender), `TerrainTile.OnTileApplied` in LocationMarkers.cs:78, and `SaveBus.StateChanged` in LoadingScreen, LocationMarkers and MapFog.

### 3b. Scheduler jobs

| name | kind | interval/budget | file |
|---|---|---|---|
| Guns.Tracers | EveryFrame (Update) | — | GunsModule.cs:226 |
| Guns.Bosses | Interval | 0.5 s (also lazily installs WeaponsPack/BloodFX bridges) | GunsModule.cs:227 |
| Guns.Ammo | EveryFrame | — | GunsModule.cs:228 |
| Guns.Ironsights | EveryFrame (EarlyUpdate) | — | Ironsights.cs:20 |
| Guns.WeaponTransition | EveryFrame (EarlyUpdate) | — | WeaponTransition.cs:27 |
| Guns.WeaponTransitionPose | EveryFrame (LateUpdateLast) | — | WeaponTransition.cs:28 |
| Guns.BloodFX lifecycle | EveryFrame (only with BloodFX) | — | BloodFxCompat.cs:37 |
| HUD.Crosshair | Interval | 0.25 s (RunNow on setting change) | HudModule.cs:95 |
| HUD.Hit feedback flush | EveryFrame (LateUpdate) | — | HudModule.cs:96 |
| HUD.Hit feedback draw | Gui (predicate `GuiWanted`) | — | HudModule.cs:97 |
| HUD headshot load | coroutine (StartCoroutine) | once | HeadshotAudio.cs:37 |
| CleanHUD.Vantage terrain | Interval | 0.2 s | CleanHudModule.cs:139 |
| CleanHUD update check | coroutine | once (network) | CleanHudModule.cs:145 |
| CleanHUD.Scene key discovery | Sliced | 500 µs | :158 |
| CleanHUD.Weapon icons | Sliced | 500 µs | :159 |
| CleanHUD.Accent panels | Sliced | 500 µs | :160 |
| CleanHUD.Layout reload | Interval | 2 s (file stat) | :161 |
| CleanHUD.Inventory window | EveryFrame (LateUpdateLast) | — | :162 |
| CleanHUD.Location markers | Interval | 0.5 s | :163 |
| CleanHUD.Quest map lookup | Interval | 1 s | :164 |
| CleanHUD.Quest map markers | EveryFrame (LateUpdateLast) | — | :165 |
| CleanHUD.Weather map areas | EveryFrame (LateUpdateLast) | — | :166 |
| CleanHUD.Player map marker | Gui (predicate `PlayerShown`) | — | :167 |
| CleanHUD.Map fog exploration | Interval | 0.2 s | :168 |
| CleanHUD.Map fog painting | Sliced | 500 µs | :169 |
| CleanHUD.Dialogue window | EveryFrame (LateUpdateLast) | — | :170 |
| CleanHUD.Quest journal | EveryFrame (LateUpdateLast) | — | :171 |
| CleanHUD.Quest dialogue | EveryFrame (LateUpdateLast) | — | :172 |
| CleanHUD.HUD attachment | Interval (LateUpdateLast) | 1 s | :173 |
| CleanHUD.HUD | EveryFrame (LateUpdateLast) | — | :175 |
| CleanHUD.Menus | EveryFrame (LateUpdateLast) | — | :176 |
| CleanHUD.Loading screen | EveryFrame (LateUpdateLast) | — | :177 |
| CleanHUD.Vistas place banners | EveryFrame (LateUpdateLast) | — | VistasBanners.cs:18 |
| CleanHUD.Binocular zoom | EveryFrame (LateUpdateLast) | — | Binoculars.cs:31 |
| (thread) KeyCatalog Mono.Cecil scan | background `Thread` | once, started from HUD attachment | Keys.cs:391 |

The merged HUD module would own 26 jobs. Six EveryFrame jobs are idle most of the time: Dialogue window, Quest journal, Quest dialogue, Weather map, Vistas banners and Inventory window. Each early-outs, but a single `HUD.Overlays` tick could fan them out.

## 4. Logging

There is no own `ManualLogSource` and no per-feature log switch config key in this area. Gunplay's old `[Debug] HitLog` survives only as a comment at Tracers.cs:780. All logging goes through `Core.Log`.

| Helper / pattern | Defined | Call sites (count) | Proposed level |
|---|---|---|---|
| `GunsModule.Verbose(msg)` = `if (Log.Verbose) Log.Info("[Guns] "+…)` | GunsModule.cs:68 | Tracers (lead l.146, hit lines 543, 1467, 1633, player gun class 428, shooter 731, bolts, orbs), Bosses 135/141 | Per shot/hit -> **2**. Gun classified once and boss HP adjusted -> **1**. |
| `AmmoSettings.V(msg)` -> GunsModule.Verbose | AmmoSettings.cs:19 | AmmoMechanics (unload phases, replay events, reload tap, ammo box template), PerGunAmmo:122 | Unload start/finish, box drop -> **1**; phase/replay/tap -> **2** |
| Direct `Core.Log.Info` behind `Log.Verbose` | — | AmmoMechanics 105, 219, 303, 486; ArmorOverflow 93; WeaponTransition 65, 74, 98; Ironsights 79; EmptyGunSound 31; Tracers 859, 879, 899, 978, 1082 | Capacity drops/returns -> 1; transitions, melee hit lines, empty click -> 2 |
| `Log.Info` **without** a Verbose guard (string built anyway) | — | Ironsights.cs:91 (per wheel notch) | 2, and add the guard |
| `Core.Log.Always` | — | ApocaspawnerDamage 26, BloodFxCompat 35/38/44, WeaponsPackSound 32, WeaponsPackIcons 31, CrosshairMigration 186 | Bridge ready/migration -> startup summary (level 1 / Always) |
| `Core.Log.Warn/Error` | — | Guns ~25 sites (unload failures, ammo box, Bridge service exceptions, Tracers errors), HUD 6 | Unchanged (always) |
| `CleanHudModule.Log(s)` = **`Log.Always`** | CleanHudModule.cs:278 | 19 sites: MapFog 268/284 (every load/save), LoadingScreen 190/263 (**every load and save**), Layout 138, Hud 168 (every HUD attach, i.e. every scene), Menu 297/618/730/1494, MenuReference 62, Assets 373 (**per runtime weapon icon**), LocationMarkers 60, MapIcons 37, QuestMap 124, bridges (ChaseCamera 86, Talk 56, Apocasaver 30/46, Apocapocket 64, VistasEngine 34) | **This is the main logging gap: these messages ignore VerboseLog.** Bridges, layout and icons loaded -> 1 (startup). Load/save screen, fog/locations saved/loaded, HUD attached, menu attached -> 1. Icon rendered, key capture -> 2. |
| `CleanHudModule.Warn` / `Throttled(key, e)` | CleanHudModule.cs:279-286 | ~30 sites; `Throttled` adds its own 10 s per-key limiter on top of Core's 5-repeat limiter | Keep as Warn/Error. Fold `Throttled` into Core Log rate limiting (duplicate limiter). |
| Direct `Core.Log.Info` guarded | — | Binoculars 63/122, MapFog 241, LocationMarkers 70/144/151, InventoryStyle (open/close/drop), VantageDiscovery 36 | Location discovered/cleared, inventory drop -> 1. Zoom target, caption, fog window, vantage -> 2. |
| `ChaseCameraHud.TraceState/TraceHidden` | ChaseCameraHud.cs:147-170 | Hud.cs:324, 552, 570 (every frame, internally gated by Verbose and 1-5 s timers) | **2** |
| HUD `Log.Info` | HudModule.cs:100 (unguarded, start), HeadshotAudio 103, CrosshairController 191 | | Start -> 1; loaded/attached -> 2 |

## 5. Redundancy & dead code

| # | Evidence | Finding | Proposal |
|---|---|---|---|
| R1 | HUD/HitFeedback/Hud.cs:14 vs CleanHUD/Hud.cs:13 | Two classes named `Hud`. One is an IMGUI hit overlay, the other the uGUI HUD; their style differs (GUI.skin label vs GlyphText). | Rename the HUD one `HitFeedbackOverlay` and draw it on the uGUI HUD canvas |
| R2 | HUD VanillaHits.cs:41-81 (SetFsmFloat prefix) vs NPC VanillaEvents `AfterDamage` (SetFsmFloat postfix) vs Tracers.PlayerHit/HitTarget | Three observers of the same "Bodypart.Damage" writes and hit outcomes | One Gunplay hit pipeline publishes Hurt/PlayerHit events (vanilla observer when projectiles are off, projectile hits when on). HUD and NPC subscribe. `ExternalReporter` and `SyncHud` (GunsModule.cs:310) then go away. |
| R3 | DamageDirection.cs:33 (`Services.NpcHurt`, raised by NPC Senses.cs:1345) vs HUD hit feedback (`Services.Hud` from Guns) | Incoming and outgoing hit feedback are fed by two separate channels; DamageDirection silently depends on the NPC module | Both subscribe to the same Core Hurt event (attackerId, victimId, …) |
| R4 | Tracers.cs:1681-1717 (`Creature`/`RootOf`/`OwningPart`), :784 `HealthOf`, :589 `IsVehiclePart`; VanillaHits.cs:313-343 (`Condition`, `CreatureHealth`, `Health`); NPC has the same walk | Duplicate actor/part/health resolution with different caches | Core `ActorQuery` (cached per root, swept) |
| R5 | Tracers.cs:742 `Classify`; NPC WeaponRanges.cs:40 `Classify` (identical text) + :24 range fallback table identical to GunsModule defaults; CleanHUD Assets.cs:33-49 `Weapons` caliber/kind table; AmmoMechanics.cs:375 `DefaultBoxes` caliber table; PerGunAmmo.cs:55 `MatchesModel` folk17 special case; WeaponDamageText literals; Tracers.MakeGun per-name overrides (l.416-425) | Weapon identity and stats are scattered over 5 files and 3 modules | One `WeaponCatalog` behind `IWeaponStats` (design note below) |
| R6 | Ironsights.cs:112-129 vs Binoculars.cs:68-85 (and PlayerBody ThirdPerson PreCull) | Same projection-matrix zoom hack, two `onPreCull` subscribers, same wheel/lerp code. Four wheel consumers coordinate through `Services.Map.Open`. | Core camera-zoom/wheel arbiter |
| R7 | CleanHUD Json.cs + 6 other Json copies; Guns Wav.cs + NPC/Women/Wav.cs + Core/VoiceAudio.cs | Duplicated utilities | Core.Json and a Core audio decoder |
| R8 | CleanHUD SceneCache.cs vs Core GameRefs; `PlayerRig` (GunsModule.cs:350), CrosshairController.cs:33 (`PlayerCamera/WeaponsArm/Parent`), WeaponTransition.cs:79-83, Ironsights.cs:67 | Rig/camera/HUD-root lookups done 5 different ways | Core GameRefs exposes `PlayerCamera`, `WeaponsArm`, `WeaponParent`, `HudCanvas`; SceneCache becomes a Core scene index |
| R9 | AmmoMechanics.cs:398, Tracers.cs:1485, CleanHUD Assets.cs:260 (+ Patrols prefab cache, + Quests) | Three separate `Resources.FindObjectsOfTypeAll<GameObject>()` prefab scans | One Core prefab index (Patrols already has one) |
| R10 | ArmorOverflowTransfer.cs vs OverflowTransfer.cs | Same state machine for float and int | Merge |
| R11 | InventoryStyle.cs:290-295 (Backpack drop: `backpack_capacity=0`, `multiply=1`, `SetState("set")`; Armor drop: `armor_capacity=0`) vs Guns BackpackOverflow.Tick (AmmoMechanics.cs:131-167: on backpack removal restores base ammo capacities, `backpack_capacity=0`, `multiply=1`) | Two systems reset the same capacity variables on equipment removal | One Core inventory service |
| R12 | Native setting "TOGGLE AIM DOWN SIGHTS" (`toggleADS`, Menu.cs SettingsModel l.~114) vs Guns `ToggleIronsights` (Ironsights.cs:97-111) | Possible duplicate of a native feature. WW's toggle overrides `AimDownSIghts_Hold`. | Verify in game whether native toggleADS works; if it does, drop WW's toggle or map it to the native key |
| R13 | Api.cs: `Compass.Update/Move/AddAnchored/RegisterIcon/MapRoot/Count/Version` have **0** callers in src; `CustomIcons` is never filled; `Hud.Resolve` Anchored branch (Hud.cs:~873) is unreachable; the comment tells third parties to check GUID `com.denis.apocalypter.cleanhud`, which never resolves under WW | Dead public API of standalone CleanHUD 1.5.1 | DELETE the public surface and keep the internal entry list behind `ICompassMarkers` (no known third-party consumer; record it in docs/modules.md 1.4) |
| R14 | QuestJournal.cs:3-11, QuestTracker.cs:3-11, TalkDialogue.cs:3,7-9 | Unused `using` (Linq.Expressions, Reflection.Emit, HarmonyLib, BepInEx.Bootstrap, BepInEx.Configuration) left from the retired QuestsCompat reflection bridge | Remove |
| R15 | Keys.cs:399 `info.Metadata.GUID == CleanHudModule.GUID` (standalone GUID) | The self-skip never matches under WW, so the Mono.Cecil scan reads WildWasteland.dll itself (the largest DLL) | Skip `Plugin.GUID`; list WW keys from `Core.Hotkeys` |
| R16 | Menu.cs:1049-1088 `ShowSlots/SaveTo/SlotLabel/SlotUsed` (legacy 10-slot UI clicking native buttons) | Fallback used only when `Core.SaveSystem.Installed` is false | Keep until Saves declares the native path dead; then DELETE |
| R17 | Tracers.cs:1761 `if (!tracers && !mine && !s.Bolt) continue;` right after `if (s.Bolt) continue;` and before the stricter l.1762 check | Dead condition | Remove |
| R18 | PerGunAmmo.Find (l.52), Bindings.Fsm (l.175), SettingsModel.FindPath (Menu.cs:138) | Trivial wrappers around FsmUtil/SceneCache | Inline |
| R19 | Three WeaponsPack bridges (Guns WeaponsPackSound, CleanHUD WeaponsPackIcons, NPC WeaponsPackCompat) + PlayerBody WeaponsPackShadows; two ChaseCamera bridges (CleanHUD ChaseCameraHud, PlayerBody ChaseCameraCompat); Apocaspawner in Guns + Core Hooks.SpawnerCatalogEntries + Patrols; Apocasaver in CleanHUD + Core ApocasaverExclusion + Patrols Convoy | One bridge per third-party mod, spread over modules | One Compatibility file per mod |
| R20 | Bosses.cs:83-107 | Periodic whole-scene scan "as a safety net" although CreateObject and SensorGetDetections already register bosses | Replace with a `Hooks.FsmEnable` "BossUI" filter or an NPC spawn event |
| R21 | Throttled (CleanHudModule.cs:280) vs Core Log's repeat limiter | Two rate limiters | Core only |
| Migrations | Guns LegacyConfig.cs (gunplay/apocaraider/apocaunloader); HUD LegacyConfig.cs + CrosshairMigration.cs (TurnOffCrosshair); CleanHudModule.cs:60-95 (TrafficMarkers bool->enum, Routes from `Patrols / Traffic tuning`, Immersive Map fog keys, `FogOpacity == .463468` one-off fix l.85, CleanHUD cfg import that runs **every** start for unsaved keys, l.102-116); CleanHudModule.cs:126-130 (copy `config/CleanHUD/{layout.json,game_keys.txt}`); Tracers.StabClip/HudModule.AssetPath legacy plugin folder fallbacks | Config/data migrations | Keep config migrations (AGENTS 5.7). The `.463468` fix and the legacy plugin-folder fallbacks (`plugins/Gunplay`, `plugins/GunplayHUD`) are retirement candidates after one more release (AGENTS 1.5: "at least one release"). |
| Compat shims | ArmorOverflow/BackpackOverflow `Idle()` fast paths, `GetButtonDown_Patch.Reset` | Not shims for retired versions | — |

## 6. Performance hot spots

| file:line | Problem | Fix |
|---|---|---|
| Tracers.cs:1510-1525 `MetalSparks` | With the default `MetalSparksScale` 0.1 (≠1), **every** spark instantiation calls `GetComponentsInChildren<ParticleSystem>(true)` (allocates) and rescales. It fires per bullet hitting any car part and per penetration. | Build one scaled template once per scale value (or cache the ParticleSystem array per prefab) and instantiate that |
| Tracers.cs:1133-1143 `PartCondition`, 1564 `HitWorld` | `GetComponentsInChildren<PlayMakerFSM>(true)` per vehicle-part hit (per bullet in automatic fire) and per melee wheel check | Cache the Condition `FsmFloat` per part instance id (sweep with the 30 s Sweep) |
| Tracers.cs:479, 559, 595, 644, 654, 1456, 1692 | `GetComponents<PlayMakerFSM>()` array allocation per hit (`HasBodypart`, `Replay` SetFsmFloat/GameObject, `HitTarget`, `Creature`). The caches are cleared every 30 s (l.1245), so the allocations recur. | Use the `GetComponents(List)` buffer overload (as VanillaHits.cs:293 does) or `FsmUtil.Find` |
| BoltAttachment.cs:117-187 `BoltSurface` | Per bolt hitting a creature: `GetComponentsInChildren<SkinnedMeshRenderer>` walking up parents, `BakeMesh` of each skin, then a full ray-triangle loop over all triangles, all synchronous. That is several ms per hit on detailed NPCs; NPC crossbowmen hitting the player/NPCs trigger it too. | Use bone colliders/capsules as the anchor (nearest bone to the hit point). If the exact skin point is required, bake only the closest skin and use a triangle budget or a Sliced job (re-pose the bolt next frame). |
| Tracers.cs:666-734 `Info` | Per new shooter: two `GetComponentsInChildren<Transform>` over the whole NPC hierarchy, retried every 1 s up to 10 times. **Gunless monsters always fail**, so 10 full walks per monster. NPC `WeaponRanges.GunKindOf` repeats the same walk. | Cache "not a gun" after the first miss for monsters (no "fire_effect"/crossbow child ever appears); share one result between Gunplay and NPCAI via `IWeaponStats.TryGetWeapon(owner)` |
| Tracers.cs:1485 `FindEffects`, AmmoMechanics.cs:398 `FindPrefab` | `Resources.FindObjectsOfTypeAll<GameObject>()` on the **first impact** / first ammo drop happens mid-combat (tens of ms) | Resolve at world-ready (Sliced) through the Core prefab index |
| Tracers.cs:904-913 `IsBlade` | `string.Split` allocation per blade hit / per AfterSetAudioClip | Parse `StabWeapons` once on SettingChanged |
| Bosses.cs:88 | `FindObjectsOfType<PlayMakerFSM>()` 4x in the first ~20 s after load, then every 60 s forever (AGENTS 6 violation) | R20 |
| AmmoMechanics.cs:348, PerGunAmmo.cs:170, 190 | `FindObjectsOfType<PlayMakerFSM>()` fallbacks while the rig/handler are missing; PerGunAmmo.Tick retries every 1 s | Use GameRefs rig lookups only; never fall back to scene scans |
| AmmoMechanics.cs:420, 424 | `GameObject.Find` fallbacks in `AmmoBox.Drop` | Core GameRefs only |
| PerGunAmmo.cs:156-181 `AdoptDrawn` | Every 1 s while no gun is bound (holstered or melee): `WeaponsArm.GetComponentsInChildren` | Trigger from the WeaponInHand state entry (already hooked) instead of polling |
| EmptyGunSound.cs:16-21 | Every `PlaySound.DoPlaySound` in the game: up to 3 `fsm.Name` string compares + `ActiveStateName` before cheap gates | Gate on `Name.Length` (6/9/6) first, or move to a Hooks dispatcher with a cached-FSM set |
| AmmoMechanics.cs:528-545 `GetButtonDown_Patch` | String compare `buttonName.Value != "Reload"` for every GetButtonDown action every frame | Per-action classification cache (like `BackpackOverflow._clamps`) |
| AmmoMechanics.cs:514 | Extra `FsmState.OnEnter` trampoline on every state entry | Move to `Hooks.EnterState` |
| Ironsights.cs:91 | Unguarded `Log.Info` string building per wheel notch | `if (Log.Verbose)` |
| ApocaspawnerDamage.cs:34, BloodFxCompat.cs:49, WeaponsPackSound.cs:41 | Reflection `FieldInfo.GetValue/SetValue` (+boxing) per call; BloodFX runs per particle collision once its native object is gone | Compile delegates once (`OptionalAccess` already exists in Core) |
| HUD CrosshairController.cs:141 | `GameObject.Find("MousePoint")` every `SearchSeconds` (≥1 s) **in all menus and loading screens**, while the HUD canvas does not exist | Use the Core scene index/HUD canvas lookup |
| HUD CrosshairController.cs:34 | `_weaponFsms` captured once by `GetComponentsInChildren(true)` and never refreshed, so weapons added later (Weapons Pack) are missed; `Fsm.GetState("pump"/"bolt action"/"hammer")` linear lookups x3 per weapon every 0.25 s | Gunplay `IWeaponState.NeedsCocking` (it already knows the drawn gun's Attack FSM) |
| HUD Hud.cs:138-200 | IMGUI OnGUI keeps the GuiRunner on while numbers show (Repaint only, but GUI.Label with shadow = 2 labels per number). `Flush` builds strings per hit. | uGUI on the HUD canvas (R1); pooled text |
| CleanHUD Hud.cs:922 → Bindings.cs:115-125 `Hide` | Runs in `willRenderCanvases` **every frame**: iterates every captured native graphic (all children of 17 roots) and calls `canvasRenderer.GetAlpha()` | Graphics are already culled after the first pass. Re-check only on `UiRevision`/HUD rebuild, or every N frames, since the game rarely re-enables alpha. |
| LoadingScreen.cs:175-190 → `Reload` l.49-104 | **Every Begin (each load and each save, including in-play manual saves)**: `Directory.GetFiles`, stat per PNG, PNG decode of new/changed screens, reading and JSON-parsing the embedded and custom `screens.json`, all synchronous on the main thread | Load once at Init (Sliced decode); re-stat only on the title screen |
| MapFog.cs:271-293 + LocationMarkers.cs:42, 62-71 | Gzip and write all fog chunks **twice per save** (SaveLoadGame "wait" and "SaveGame") plus the locations file 3x (both states + OnSaveEnd); synchronous File IO | One write via the Saves sidecar API (after-save or a worker thread with a snapshot copy) |
| LocationMarkers.cs:119-170 (every 0.5 s) | Iterates all discovered native locations. Per location: `Origin.World`, `Binoculars.Observe` (raycast while raised and in cone), `VantageDiscovery.Sees` for every unknown location in view when elevated (**up to N raycasts of up to 4 km per tick**), `Services.Camps.At` per uncleared hostile camp | Round-robin budget (e.g. 8 locations per tick); frustum/range cull before `Sees`; cache camp state per location for 5 s |
| QuestMap.cs:192-210 (per frame while the map is open) | Per entry: `LocationMarkers.MissingBake` -> `Services.Npc.HighlightMissingPoiBake(label)` (a debug feature!), `MapFog.Hides`, delegate position, `WorldToViewportPoint`; Towns' postfix adds more | Resolve MissingBake once per entry per open (or only when the NPC debug overlay is on) |
| QuestMap.cs:89, 122 | `GameObject.Find("MapCheat_Camera")` every 5 s while missing (menus/worlds without map); `FindObjectsOfType<RawImage>(true)` on camera change | Core scene index |
| Keys.cs:391-418 | Mono.Cecil reads **every plugin DLL including WildWasteland.dll** on a background thread 1 s after Init, even if the Controls tab is never opened; `AddModFiles` reads all `*.cfg` synchronously when the tab is built | Start lazily on first Controls open; skip WW (R15) |
| Menu.cs:546-565 | `GetComponentsInChildren<Button>` + `GameObject.Find("Apocasetter.MenuFallback")` every 0.5 s while the Mods button is not found (menus only) | Ask `ApocasetterBridge` for the button |
| Assets.cs:52-60 `KeyOf` | 3x `Regex.Replace` + `item.name` allocation + `WeaponsPackIcons.Model` name checks per call (on slot/weapon change, plus 0.5 s retries for Apocapocket items) | Cache the key per item instance id |
| Assets.cs:255-264 `IconRenderer.Preload` | `Resources.FindObjectsOfTypeAll<GameObject>()` at Init (startup hitch, R9) | Core prefab index, Sliced |
| TalkDialogue.cs:79-131 | Per frame while talking: hashing source children; `Clear()` destroys and recreates all chat/choice widgets on each revision | Pool rows as QuestDialogue does |
| Guns Tracers `Tick` (GunsModule.cs:296) | `SyncHud()` per frame | Removed with R2 |
| CleanHUD 6 idle EveryFrame jobs | Six per-frame calls that early-out | One dispatcher job (fewer timed jobs on the F7 page) |

## 7. ModAPI / multiplayer events

Ids: GameObject references must become Core entity ids. Use a save-stable item id (the native item `name` that PerGunAmmo already uses as ledger key), the NPCAI NPC id, the Patrols/Vehicles vehicle id, and a part path (vehicle id + part name/hinge). Positions should be MapMagic-relative (`Origin.World`).

| Event / query | Publisher -> consumers | Payload (broadcastable) |
|---|---|---|
| `WeaponFired` | Gunplay -> NPCAI (hearing; replaces `INpcApi.Shot`), HUD (crosshair bloom), Saves (combat-safe, replaces `ReportCombat`) | shooterId, weaponKey, weaponKind, isPlayer, origin(world), direction, pellets, shotSeed (for deterministic spread), projectileIds[] |
| `ProjectileSpawned` / `ProjectileEnded` | Gunplay -> MP replication, tracers | projectileId, shooterId, kind(bullet/bolt/orb), origin, velocity, speed, range, launchTime |
| `ProjectileHit` | Gunplay -> HUD (hit marker/numbers), NPCAI (knowledge), Patrols | projectileId, shooterId, targetId or partRef, point, normal, damage, head, penetrated(bool), material |
| `Hurt` (unified) | Gunplay (projectile + vanilla observer + melee) -> HUD (DamageDirection, hit feedback), NPCAI (replaces `INpcApi.Hurt` + `Services.NpcHurt`), Quests (kills) | victimId, attackerId, amount, head, point, source(projectile/melee/vanilla/explosion), weaponKey |
| `VehiclePartHit` (exists, extend) | Gunplay/Patrols -> Patrols crews, HUD (blue %) | vehicleId, partRef, conditionBefore/After, attackerId, wheel(bool) |
| `WheelPoppedOff` | Gunplay -> Vehicles/Patrols, MP | vehicleId, partRef, impulse |
| `BoltLanded` | Gunplay -> MP | boltId, targetId/bonePath or world pose, ttl |
| `AmmoChanged` / `MagazineChanged` | Gunplay -> HUD weapon panel (replaces UI-text parsing in Bindings) | itemId, caliber, inGun, reserve, hammer |
| `UnloadStarted/Finished`, `AmmoDropped`, `ArmorPlateDropped` | Gunplay -> HUD log, Loot/MP | itemId, caliber, count, dropItemId, position |
| `AdsChanged` | Gunplay -> HUD (crosshair hide), Presentation | aiming, zoomFactor, scoped |
| `WeaponSwitchPhase` | Gunplay -> Presentation (lower/raise pose) | phase(lowering/raising), weight, fromSlot, toSlot |
| `BossHealthAdjusted` | NPCAI (ex Bosses) -> debug/MP | npcId, base, cap |
| Query `IWeaponStats` | Gunplay -> HUD, NPCAI, Compatibility (Apocaspawner, WeaponsPack) | see design note |
| Query `IWeaponState` | Gunplay -> HUD (crosshair cocking tint, ammo), Presentation | drawn itemId, needsCocking, inGun, reserve, reloading |
| `Notification` (`INotifications.Push`) | HUD provides; Quests, Saves (SaveSystem.cs:121), Compatibility (Vistas engine heat), Gunplay | text, category, priority (MP: per-player) |
| `LocationDiscovered` | HUD -> Quests/POI/MP | locationKey ("Label:x:z"), world pos, method(proximity/binoculars/vantage) |
| `FogExplored` (chunk deltas) | HUD -> Saves/MP | chunkKey, rect, values (or a revision) |
| `MapOpened/Closed`, `MapZoom` | HUD -> wheel arbiter, PlayerBody | open, zoom |
| `ILocationMarkers` | HUD -> Towns, POI | Exclude(rootRef), SetIcon(rootRef, icon), Known(rootRef) |
| Save presentation events | Saves -> HUD LoadingScreen, Compatibility (Apocasaver) | slot, kind(manual/auto/load/new), progress, ok |
| `IDrivingTelemetry` | Compatibility (ChaseCamera)/Vehicles -> HUD | speedKph, rpm, rpmFraction, gearText, heading, visible |
| `IInventorySlots` | Compatibility (Apocapocket) -> HUD, Presentation | count, selected, itemIds[6], ordinary[6], icon(itemId) |

### Design note: future gun-attachments addon (WeaponStats service in Core ModAPI)

**Where stats are read or applied today** (all inside Gunplay unless noted):

| Stat | Source today | Applied at |
|---|---|---|
| Weapon kind | Name heuristics `Tracers.Classify` (Tracers.cs:742), duplicated in NPC `WeaponRanges.Classify`. Shotguns are detected by an FSM loop (`MakeGun` l.404-410). | Range, speed, pellets, penetration tables, NPC pacing |
| Caliber / item kind | CleanHUD `Assets.Weapons` table + item-name "(cal)" parse (Assets.cs:33-80); `AmmoBox.DefaultBoxes` (AmmoMechanics.cs:375) | HUD caliber label; ammo drop item |
| Base damage | Player: FSM "hit" state `SetFsmFloat Bodypart.setValue` (MakeGun l.390). NPC: "attack" state `SetFsmFloat` or literal -7 (Info l.701-722). Melee: "hit" state value (`SwingDamage` l.1108). | `Step` (l.1292-1297), `HitTarget` (1451), `Replay` (600) |
| Damage multipliers | Hard-coded per name: crossbow x2.5 (l.424, `CrossbowDamageMultiplier` l.29), redmark 100/90 (l.425), chopped shotguns x1.5 / 9 pellets (l.416-420); `NpcShotgunDamage`, `HeadshotMultiplier` (via `HeadFactor` l.552), `Falloff` (`FullDamageUntil`, l.772), penetration `DmgMult` (l.1629) | Shot creation and `Step` |
| Range | `GunsModule.*Range` per kind (`RangeOf` l.798) x `RangeMultiplier` (rochester/slamberg x2, l.420) | Shot range and falloff; NPC engagement via `IGunsApi.EffectiveRange` |
| Projectile speed | Global `BulletSpeed`/`BoltSpeed` | Shot creation; NPC lead |
| Pellets / spread / accuracy | FSM loop count and `RandomFloat` (MakeGun), `ShotgunPellets`/`ShotgunPelletSpread`, `PlayerAccuracy`/`NpcAccuracy` (`Accuracy` GunsModule.cs:66, `SpreadOffset` l.265); NPC jitter `offset` FSM var x `INpcApi.SpreadFactor` | `BeforeRaycast`, `BeforeRayHit` |
| Penetration | `PassGlass/Grid/Sheet/Armour[kind]` (l.1588-1591) | `Penetrates` |
| Vehicle damage | `VehicleDamagePer1`, `WheelDamageMultiplier` | `HitWorld`, melee |
| Muzzle point | `muzzle_flash` child (MakeGun l.393), `fire_effect` (Info), native arrow tip, PlayerBody `TryPlayerWeaponMuzzle` | Shot origin |
| Zoom | Ironsights fixed max 3x; scope = native `AimDownSights` state | `PreCull` |
| Magazine | Vanilla Reload FSM `ammo_in_gun` + capacity (not touched); ledger per item name | PerGunAmmo |
| Fire rate, recoil, reload time, noise | Untouched vanilla FSM values | — |
| Display | `WeaponDamageText` literal "Damage - 50"; Apocaspawner x2.5 | UI |

**What a Core ModAPI `IWeaponStats` service would need:**

1. **One weapon catalog / identity**. Key = normalized weapon key (`akms`, `redmark_m11_scoped`; pack keys like `ww_weaponspack_chainsaw`). Instance = save-stable item id. `bool TryResolve(GameObject itemOrModelOrNpc, out WeaponRef)`, where `WeaponRef = {key, itemId, ownerId}`. This replaces the 5 tables listed in R5.
2. **A stats struct** (value type, no Unity refs): Kind, Caliber, BaseDamage, DamageMultiplier, Range, FullDamageFraction, ProjectileSpeed, Pellets, PelletSpread, Accuracy (0-100), PenetrationClass, MagazineCapacity, FireInterval, ReloadTime, ZoomMax, NoiseRadius, MuzzleOffset (local), HeadshotMultiplier.
3. **Queries:** `WeaponStats Base(string key)` and `WeaponStats Effective(WeaponRef)` (base + config + modifiers). Use a revision counter (`int Revision(itemId)`) so Gunplay's per-FSM `PlayerGun` cache stays valid without per-shot dictionary or string work.
4. **A modifier pipeline:** `int AddModifier(string owner, string itemIdOrKey, StatModifier{stat, add, mul, priority})`, `RemoveModifier(int)`, `RemoveAll(owner)`, and an event `WeaponStatsChanged{itemId}`. Attachments become modifiers keyed by item id.
5. **Attachment slots and persistence:** `IWeaponAttachments{ slots(key) -> [muzzle, optic, magazine, stock, rail]; Attach(itemId, slot, attachmentKey); Detach }`. Save through the Saves sidecar API keyed by item id, which is the same identity rule as `Apocaunloader.Magazines` (survives native save/load).
6. **Presentation hooks** (Presentation module, via ModAPI): `IWeaponVisuals.Attach(itemId, slot, prefabKey, localPose)` for both first-person and the visible third-person prop. Suppressors and optics must move the muzzle point, so `TryPlayerWeaponMuzzle` and NPC muzzles have to read the attachment offset. Optic attachments set ZoomMax/scope overlay through the camera-zoom arbiter.
7. **Consumers:**
   - Gunplay `MakeGun/Info/RangeOf/Falloff/Penetrates`.
   - NPCAI (range/pacing, replacing `EffectiveRange`/`TryGetWeaponKind`).
   - HUD (caliber/kind label, magazine capacity, crosshair, tooltip text replacing `WeaponDamageText`).
   - Compatibility (Apocaspawner damage, WeaponsPack weapons registering their own base stats).
   - Loot (attachment items in loot lists).
8. **Multiplayer:** stats changes must be deterministic from (key + modifier list), so peers sync only the attachment list per item id.

## 8. Unmapped or ambiguous code

| Code | Could go to | Recommendation |
|---|---|---|
| Ironsights (input toggle + zoom + projection) | Gunplay / Presentation / Core | ADS toggle and zoom level are gameplay -> **Gunplay**. The projection write -> **Core camera-zoom arbiter**, shared with Binoculars and ThirdPerson; one `onPreCull` owner fixes ordering bugs between three modules. |
| WeaponTransition (switch delay + weapon lowering pose) | Gunplay / Presentation | Split. The 0.14 s event delay changes when you can fire -> **Gunplay**. The lowering offset of `Parent` is weapon positioning -> **Presentation** (`IWeaponPose.SetLowerWeight`). |
| EmptyGunSound | Gunplay / Presentation | **Gunplay** (weapon audio); it queries Presentation's first/third-person state |
| Bosses (human boss HP cap) | Gunplay (balance) / NPCAI (NPC stats) | **NPCAI**. It is NPC health, not weapons, and it shares the SensorGetDetections hook with NPC Senses. Its config `[Guns / Bosses]` must migrate. |
| ArmorOverflow / BackpackOverflow + InventoryStyle.Drop capacity resets | Gunplay / Survival / Core/Fixes | Ammo overflow -> **Gunplay** (ammo). Armor overflow and equipment-drop capacity resets -> **Core inventory service** (next to `InventorySupplies`). Both keep their toggles. |
| AmmoBox.Drop (save-registered item spawn) | Gunplay / Loot / Core | **Core `IItemSpawner`**; Loot, HUD supplies and Gunplay all spawn save-registered items |
| NPC aim (AimAtBody, Lead, LeadAccuracy, per-NPC LeadSkill) inside Tracers.BeforeRayHit | Gunplay / NPCAI | NPCAI owns "decisions (shoot)", but lead is a projectile-flight correction -> keep in **Gunplay**. Config stays `[Gunplay / NPC aim]`; expose per-NPC skill via `INpcApi` later. |
| Melee on wheels (Tracers) vs Patrols `MeleeWheels` fallback | Gunplay / Patrols | **Gunplay** (target table: melee, cutting tires). DELETE the Patrols fallback once Gunplay melee cannot be switched off independently. |
| VanillaHits | HUD / Gunplay | **Gunplay** (hit detection), HUD subscribes (R2) |
| WeaponDamageText | Gunplay / HUD | **HUD** (UI text), driven by `IWeaponStats` |
| Crosshair (HUD CrosshairController style; PlayerBody ThirdPerson moves/activates `MouseCrosshair`, ThirdPerson.cs:34-42, 446-447; Core CorePages previews) | HUD / Presentation | **HUD** owns the crosshair graphic. Presentation publishes the "crosshair screen offset" and HUD applies it, so only one writer touches `MouseCrosshair`. |
| Binoculars Observe/Sees + VantageDiscovery + LocationMarkers discovery state | HUD / POI | Discovery state is gameplay-like, but it only drives map/compass -> **HUD**, publishing `LocationDiscovered` |
| TalkDialogue | HUD / Quests / Compatibility | **Compatibility** (Apocatalker is third-party). The window view is shared with HUD's QuestDialogue. |
| Menu.CheckUpdates + Discord/GitHub buttons | HUD / Core | Update check -> **Core** (version/release info service); the buttons stay HUD |
| Keys.cs controls catalog | HUD / Core (Hotkeys) | **HUD** (menu screen). It must ask Core Hotkeys for WW bindings. |
| PauseCursorFix | HUD / Core | **Core/Fixes** |
| GlyphFont/GlyphText, AccentTheme, panel textures | HUD / Core | **Core/UI**, because Core's F3 window and notices draw with them |
| SceneCache | HUD / Core | **Core** scene index |
| Api.cs public Compass API | HUD | Internal store + delete the public surface (R13) |
| MapFog Immersive Map import | HUD / Compatibility | **Compatibility** (Immersive Map), passing chunks into HUD's fog via ModAPI |
| VistasBanners widget | HUD / Compatibility | Widget -> **HUD**; the event source Core/VistasWorldGen -> Compatibility |

## 9. Save/config compatibility constraints

| Item | Where | Constraint |
|---|---|---|
| ES3 key `Apocaunloader.Magazines` (value header `Apocaunloader.Magazines|1`, lines `C|type`, `M|item|type|rounds|hammer`) in the slot file | PerGunAmmo.cs:17, MagazineLedger.cs:48-81 | Verbatim. Injected after SaveAll("global") **and** before `StoreCachedFile` (Apocasaver rebuilds the cache). Read in a LoadAll prefix. The Saves module API must preserve this timing. |
| SaveTypes aliases `Gunplay, Gunplay` -> `WildWasteland.Modules.Guns`; `GunplayHUD, GunplayHUD` -> `WildWasteland.Modules.HUD` | Core/SaveTypes.cs:35-36 | No saved component types are known from these modules today ("unused"). If namespaces are renamed (`Modules.Gunplay`, merged `Modules.HUD`), update the alias targets anyway. |
| Sidecar `config/WildWasteland/CleanHUD/Saves/<slot>.fog.bin` | MapFog.cs:56, 261, 281 | gzip, magic 1229799760 ("IMFP"), version 4, seed, chunk list (64x64 bytes, reveal = 255-fog), two trailing ints. The Immersive Map format must stay importable. |
| Sidecar `config/WildWasteland/CleanHUD/Saves/<slot>.locations.txt` | LocationMarkers.cs:44 | Line 0 = seed; following lines `Label:x:z` (MapMagic-world rounded) |
| Core save copy/backup/remap list | Core/SaveFiles.cs:189 (`CleanHUD/Saves/*.fog.bin`, `*.locations.txt`) and :185 (`ImmersiveMap/slots`, `Apocaunloader/Saves`) | **The module Id is the folder name** (`Assets.ModuleConfigDir("CleanHUD")`). If the merged module is called `HUD`, either keep the `CleanHUD` data folder name or migrate the folder and update SaveFiles; keep reading the old folder for at least one release (AGENTS 1.5). |
| Immersive Map import | MapFog.cs:262 `config/ImmersiveMap/slots/<slot>_seed<seed>.bin`; stand-down if GUID `local.immersivemap` is loaded | Keep (Compatibility) |
| Config data | `config/WildWasteland/CleanHUD/layout.json`, `game_keys.txt` (copied once from `config/CleanHUD/`); `config/WildWasteland/CleanHUD/` default profiles via `TunedSettings.InstallProfiles("CleanHUD")` | Same folder-name issue |
| Runtime assets | `plugins/WildWasteland/CleanHUD/Icons/*.png` (MapIcons.cs:79), `plugins/WildWasteland/LoadingScreens/{Screen*.png, screens.json, BG_MAIN.png}` (LoadingScreen.cs:16, Menu.cs:613), `plugins/WildWasteland/HUD/{Textures,Sounds/headshot.mp3}` (+ fallback `plugins/GunplayHUD/`), `plugins/WildWasteland/Guns/Sounds/knifestab.wav` (+ fallback `plugins/Gunplay/`) | `Assets.ModuleDir(Id)`: renaming Guns -> Gunplay moves the folder; keep a fallback |
| Embedded asset logical names (asset-index.tsv, key = `<ModuleFolder>.<path>`, build.sh:17-24) | `CleanHUD.*.png` (panel_menu, g_*, font_atlas, game_font_atlas, weapon icons), `CleanHUD.font_meta.json`, `CleanHUD.game_font_meta.json`, `CleanHUD.LoadingScreens.*` (special-cased to `LoadingScreens/`), **`CleanHUD.DefaultSettings.<cfg>.txt` and `CleanHUD.DefaultProfiles.<owner>.*` used by Core TunedSettings for all modules**, `HUD.Textures.crosshair_*.png` | Moving `src/Modules/CleanHUD/Embedded` changes keys and on-disk targets; update `DiskResources.Open` callers, TunedSettings and build.sh/csproj mapping together |
| settings.cfg sections | `[Guns]` (Enabled, Projectiles, PerGunMagazines, Unloading, BackpackOverflow, ArmorOverflow, BossHealth, MonsterProjectilesHitWalls, MeleeOnWheels, NpcAim, IronsightWheelZoom, ToggleIronsights, SmoothWeaponSwitch), `[Guns / Projectiles]`, `[Guns / Ranges]`, `[Guns / Ammo]` HoldSeconds, `[Guns / Bosses]` AdjustHumanBossHP, `[Keys]` "Guns: unload fallback key"; `[HUD]` (Enabled, Crosshair, SelectionDot, HitFeedback, HitMarker, FloatingDamage, HeadshotSound, HeadshotVolume); `[CleanHUD]` Enabled, `[CleanHUD / HUD]` (Enabled, ShowNeeds, ShowHealthNumbers, ShowInsanity, DamageDirection, ShowConvoys, ShowLocations, "Simple map icons", AmbiguousPatrols, AmbiguousLocations, Compass, DrivingHudOnlyThirdPerson, Scale), `[CleanHUD / Map]` (Enabled, Markers, TrafficMarkersMode, Routes, FogOfWar, FogRevealRadius, FogEdgeFade, FogOpacity, BinocularZoom), `[CleanHUD / Menu]` (Enabled, CleanFont, Accent, EnableCheats) | Section prefix = module Id (`ModuleConfig.Section`, ModuleConfig.cs:28-31). Renaming Guns -> Gunplay or merging CleanHUD into HUD **renames every section**, so a migration per key is needed. Two collisions to handle: merged HUD gives `[HUD / HUD]` and an `[HUD] Enabled` meaning clash between HUD and CleanHUD master switches. CleanHUD's `Bind()` (CleanHudModule.cs:102-110) checks "saved" by section name, so it must be updated or old values will be re-imported. Bosses -> NPCAI also moves `[Guns] BossHealth` and `[Guns / Bosses]`. |
| tools.cfg sections | `[Guns / Tracers]`, `[Guns / Ballistics]`, `[Guns / NPC aim]`, `[Guns / Melee]`, `[Guns / Ammo]` AnimationTimeout; `[HUD / Hit feedback]`, `[HUD / Crosshair]`, `[HUD / Headshot sound]` | Same rename issue; F3 Debug/Developer pages filter by these sections |
| Legacy cfg imports | gunplay.cfg / apocaraider.cfg / apocaunloader.cfg (Guns LegacyConfig); gunplayhud.cfg / apocaraider.cfg / `GunplayHUD/hidden-settings.not-saved` (HUD); `com.denis.apocalypter.cleanhud.cfg` (every start for unsaved keys); `local.immersivemap.cfg [Fog]`; `tools.cfg [Patrols / Traffic tuning] DebugMap / "hide route type"` -> MapRoutes; legacy `[CleanHUD / Map] TrafficMarkers` bool | Keep (first-run / missing-key imports). Note that the CleanHUD imports are not first-run-only. |
| F3/RC1 defaults | docs/defaults-rc1.md; CorePages hard-codes HUD/CleanHUD/Guns entries; "ToggleIronsights explicitly remains off" | Preserve defaults and the Game-page layout when entries move |
| ConflictsWith / replacement guard | Guns: `com.denis.apocalypter.gunplay`, `com.denis.apocalypter.apocaunloader`; HUD: `com.denis.apocalypter.gunplayhud`; CleanHUD: `com.denis.apocalypter.cleanhud`; MapFog stand-down: `local.immersivemap` | Carry them into the merged modules' `ConflictsWith` |
| Native game data touched | Menu SettingsModel writes `SaveSettings.es3` keys (FOV, bloom, …, toggleADS …), `Settings.xml` (resolution/mode/quality), and PlayerPrefs; KeyCatalog writes native `InputStorage` bindings | Behavior must stay identical |
