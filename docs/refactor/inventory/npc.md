# Inventory: NPC module (src/Modules/NPC/**) -> target NPCAI

Snapshot 2026-10-22. 51 .cs files, 18,237 lines (1.17 MB; Senses.cs, Brain.cs and Nav.cs are about 135 KB each, with long lines). Module Id `NPC`, namespace `WildWasteland.Modules.NPC`, replaces the standalone NPCAI 1.9.15 (`com.denis.apocalypter.npcai`) and Women of Wasteland 1.0.0. `src/Modules/NPC/Embedded/` is empty in this snapshot: the shipped NavMaps, PoiNav and WomenOfWasteland resources are not in git. `NavMaps/README.md` documents the `.wwnav` format.

Legend for targets: **NPCAI** (behaviour), **Pres** (Presentation), **Tools**, **Debug**, **Compat** (Compatibility), **Gunplay**, **Survival** (Weather), **Core**.

---

## 1. File map

| path (src/Modules/NPC/...) | lines | what it does | target | action |
|---|---|---|---|---|
| NpcModule.cs | 281 | Module entry: Init/Shutdown, 26 jobs, 22 hook subscriptions, 6 patches, origin shift, NPCAI data migration, LogAdapter | NPCAI, Debug, Tools | SPLIT. `NpcModule` lifecycle, `InstallHooks`, `InstallPatches`, `OnOriginShift` and `ResetAll` stay in NPCAI. `LogAdapter` and `Verbose()`/`Warn()` (l.35-44) go to Debug (one `Log.L1/L2`). PoiNav editor/view/setup jobs and the editor hotkey (l.108-115) go to Tools. The commented ChainsawLimbs lines 175-176 are deleted. `MigrateData` stays for now (see section 9). |
| NpcModule.Config.cs | 340 | Binds about 200 entries: 6 F6 toggles, 8 `B()` feature toggles, 4 Detection, about 180 tools.cfg entries, legacy migrations | NPCAI, Debug, Pres | SPLIT. Behaviour entries stay in NPCAI. The Debug entries (`VerboseLog`, `NavDump`, `ShowNavigation`, `HighlightUnbakedMapPois`) and the four `<X> / Log` entries go to Debug. The alias line 334-336 is deleted. Presentation entries go to Pres: `Brain/ApocaplayerClips`, `AimPose`, `NpcAim/ReloadVolume`, `ReloadSoundRange`, `DogAnimations`, `DogsLieDown` (pose timers), `DogLieFrontLift`. |
| NpcApi.cs | 32 | `INpcApi` implementation that forwards to Senses, Brain, Idle, Nav, SimpleBandits and Aim | NPCAI | MOVE. It becomes the NPCAI ModAPI provider. `SpreadFactor` moves to Gunplay (see 8). |
| ChainsawLimbs.cs | 158 | Dismemberment meshes, entirely inside `#if false` | none | DELETE. Disabled at Denis's request (l.1). The only references are comments: WeaponsPackCompat.cs:40, 182, 185 and NpcModule.cs:175-176. |
| WeaponsPackCompat.cs | 188 | Reflection bridge to `apocalypter.weaponspack`: burning causes fear, chainsaw kills send witnesses fleeing, `ChainsawDrawn` triples footstep hearing | Compat | MOVE to Compat. Replace the `Brain.FireRetreat`, `Brain.ChainsawWitnesses` and `ChainsawDrawn` calls with ModAPI calls: `Npc.Frighten(id, pos, secs)`, an event `KillWitnessed`, and a flag `PlayerNoiseFactor`. |
| Animals/Burrow.cs | 526 | Scorpions burrow into sand and big scorpions hunt underground; includes the sand test, a puff pool and the visual sink in LateTick | NPCAI, Pres | SPLIT. `Tick`, the hunt (`Dive`/`Travel`/`Rise`), the sand test and `Register` stay in NPCAI. The visual parts go to Pres: `LateTick` (l.371, sinking the rig) and the puff FX (l.386-468, the Burrower_Effect pool). |
| Animals/DogAnimations.cs | 695 | Retargeted quadruped clips from `npcai_dogs.bundle`, lie-down/get-up, jump bite, paw/front IK, companion alignment | Pres, NPCAI | SPLIT. Pres gets `LoadBundles`, `Switch`, `Prepare`/`Restore`/`Release`, `MeshCulling`, `SampleDog`, `FrontLegs`, `LevelFront`, `PawsOnGround`, `AlignCompanion`, `LateTick`/`LateCompanion` and the `Dog` pose state. NPCAI gets the decisions: `StartAttack`, `TryAttack`, `DriveDash`, `InReach`/`ReachPoint`, `DamageReady`, the rest/idle timers (`IdleSpan`), and the hooks `BeforeDamageEvent`, `BeforeDamageWait` and `BeforeSetVelocity`. |
| Animals/DogBite.cs | 141 | Dogs stop and face their target in reach; the procedural head-lunge bite runs in LateTick | NPCAI, Pres | SPLIT. `Tick`, `Register` and `AfterSetFsmFloat` (bite detection) stay in NPCAI. `LateTick` (l.117, bone lunge) goes to Pres. |
| Animals/DogCompanionCompat.cs | 384 | Reflection and 8 patches on FlyingKlick's Dog Companion; drives the companion through DogAnimations | Compat | MOVE to Compat. Today it reaches DogAnimations internals (`RegisterCompanion`, `TickCompanion`, `LateCompanion`, `CompanionDashing`, `RemoveCompanion`). It needs a Pres ModAPI such as `IQuadrupedAnimation` and an NPCAI ModAPI for threats (`Brain.cs:571 CloseThreat` is the reverse dependency). |
| Animals/DogCompanionSeating.cs | 11 | Two constants (seat offsets) | Compat | MERGE-INTO DogCompanionCompat.cs. Its only use is DogCompanionCompat.cs:372. |
| Animals/Pounce.cs | 427 | Small spiders zigzag, leap, bite and flee; the carcass of a spider killed mid-air flies on; rear-up pose | NPCAI, Pres | SPLIT. `Tick`, `Register`, `Room` and the bite stay in NPCAI. `LateTick` (l.379, rear pose) and the airborne carcass tumble in `AfterCreateObject` (l.397) go to Pres. |
| Brain/Brain.cs | 1983 | Per-NPC steering: feelers, holding, cover, stuck/hop, magazines, think scheduler, 9 FSM-action hooks; drives the ApBody character | NPCAI, Pres, Debug | SPLIT. NPCAI keeps `Tick`, `Think`, `MoveToward`, `Steer`, `NavSteer`, `Cover*`, `OnStuck`, `TryHop`, the hooks and `Make`/`Of`. Pres gets `Drive` (l.1208), `Aim`/`Unfreeze` (animator freeze), `StrafeClip`, `Crouch` (capsule plus clip), `HeldWeapon`/`GunPoses`, `TryRig`/`BuildRig`/`HopAnim`. NPCAI publishes a per-NPC "situation" (crouched, aiming, firing, airborne, pitch, reload) that Pres consumes. Debug gets `TraceDump` (l.778), `LogAhead` (l.1456, uses RaycastAll) and the `Status` strings. Magazines (`MagazineOf`/`NeedsReload`/`Shot`, l.1288-1370) go to Gunplay. Delete `LateTick` (l.1453, empty and never registered). |
| Brain/Brain.Weapons.cs | 98 | Fear and retreat (burning, chainsaw witnesses), `FrightThink`, `Retreating` | NPCAI | MOVE. Make it generic: `Frighten(npc, threat, seconds)` without Weapons Pack wording. |
| Brain/Aim.cs | 227 | Burst pacing (hold fire beyond range, longer aim at distance, facing tolerance), `SpreadFactor` | NPCAI, Gunplay | SPLIT. Decision prefixes `BeforeSendEvent`/`BeforeRandomWait` stay in NPCAI. `SpreadFactor`/`Steps` go to Gunplay, which already owns spread and lead ("NPC aim" tools section in Guns). |
| Brain/Ambush.cs | 56 | Ambush predators (scorpions/spiders) wait until 3-5 m; 0.5 s list pass | NPCAI | MOVE |
| Brain/ApBody.cs | 120 | Adapter to `Services.Animation`: attaches an `IAnimatedCharacter` to an NPC Animator; weapon key and pistol helpers | Pres | MOVE to Pres. It becomes the Pres-side NPC character driver. `WeaponKey` (l.44) and `IsPistol` (l.55) duplicate `IAnimationApi.WeaponKey`/`WeaponKind`: DELETE them and call the API. |
| Brain/Idle.cs | 832 | Camp raiders: return home, camp walks, search walks, ghost walks, `Services.Npc.Travel` orders; own 3-way capsule fan; plays the run clip | NPCAI, Pres, Debug | SPLIT. Behaviour stays in NPCAI. `Drive`'s `Anim.Play("run")`/speed (l.751-755) and `RunInPlace` go to Pres. `DrawDebug` (l.791) goes to Debug. The `Drive` steering duplicates Brain's (see 5). |
| Brain/ReloadSounds.cs | 84 | Reload sounds of an NPC gun, borrowed from the player's weapon FSMs and played in 3D | Pres | MOVE to Pres. Today it creates a `new GameObject` per sound (l.74); pool it instead. |
| Brain/SimpleBandits.cs | 118 | Convoy-crew "simple bandit" mode for Patrols (simple look, hold/chase), partials of Senses and Brain | NPCAI | MOVE. Patrols drives it via `INpcApi.SetSimpleConvoyBandit*`. |
| Debug/DebugOverlay.cs | 62 | IMGUI diamond and label markers at world points for the overlay | Debug | MOVE |
| Debug/NpcPages.cs | 143 | F7 pages: population, appearance, AI overview, Detection, Navigation, Brain, Idle, Animals, Hazards, Debug, POI pathfinding | NPCAI, Debug, Tools, Pres | SPLIT. ConfigPages follow their entries. "AI overview" job times go to Debug (Performance already shows per-job times). The Debug page (toggles, Reset/Export maps) goes to Debug and Tools. "POI pathfinding" goes to Tools. Population stays in NPCAI; Appearance/voices go to Pres. |
| Detection/Senses.cs | 2166 | Sight, hearing, ghosts, search, flanking, factions, engine/horn/throw/blast noise, taunts, player shout, sensor hooks, `.senses.txt` persistence, overlay labels, NWH reflection | NPCAI, Pres, Compat, Debug, Core | SPLIT. NPCAI keeps `Tick`, `Look`, `Visible`, `Noise`, ghosts, `Engage`, `Make`/`Register`, `Persist`, `CrewOrder`, `Remember`, `Hurt`/`Shot`. Pres gets `Taunt`/`BeforeAudioPlay` audio swap (l.1125-1197), `Voice()`/`FlexaClips`/`VoiceSource`. The shout input (`ShoutPressed`, `ModifierHeld`, `BeforeGetButton*`, l.1199-1283) goes to Core Hotkeys, with NPCAI reacting through a `PlayerShout` event. Compat or Core gets `Nwh` (l.1955-1988), replaced by the existing `Services.VehicleIgnition.TryRunning`. Debug gets `DrawDebug` (l.1925) and `Log(Agent,…)` (l.1876). |
| Detection/Senses.Spiders.cs | 144 | Spider light sensing: lit items, flashlight, cave webs, CaveDarkness ambient | NPCAI | MOVE |
| Detection/CaveDarknessBridge.cs | 60 | Reflection plus a compiled expression into `com.denis.apocalypter.cavedarkness` for an ambient light value | Compat | MOVE to Compat. Expose it as a Core query `Light.AmbientAt(pos)`. |
| Detection/CrewOrders.cs | 46 | `INpcApi.CrewOrder`: forces an enemy, or suspends combat while boarding (Patrols CrewReturn) | NPCAI | MOVE. This is behaviour; Patrols only calls it. |
| Detection/VanillaEvents.cs | 192 | Observes vanilla NPC and player shots, lengthens the NPC burst RaySensor to weapon range, counts magazine shots, raises hurt | Gunplay, NPCAI | SPLIT. Gunplay gets `BeforeNpcShot` ray-length extension (l.32-49) and its finalizer `AfterNpcShot`. Gunplay or Core also gets `BeforePlayerShot` and `AfterDamage`, which raise `WeaponFired`/`NpcHurt` events. NPCAI only consumes those events (`Senses.Shot`/`Hurt`, `Brain.Shot`). |
| Detection/WeaponRanges.cs | 153 | Weapon kind enum, name classification, fallback ranges, NPC gun detection, hand-weapon scan | Gunplay, Pres | SPLIT. Gunplay gets `Kind`, `Classify`, `RangeOf`, `GunKindOf` and `ProjectilesEnabled` (they duplicate Guns Tracers.cs:746 `Classify` and Tracers.cs:678 fire_effect lookup). Pres or Core gets `ScanHands`/`WeaponOf`/`UnderHand` (held-model lookup, also used by Brain.Drive). |
| Hazards/Nests.cs | 632 | Spider nests: plans layouts at POIs, sliced web building, sitters, touch alarm, `nest-layouts.txt` | NPCAI | MOVE. `Pois()` (l.242) should use the shared POI index from Core/POI (see 5). |
| Hazards/Webs.cs | 400 | Procedural web meshes (orb and sheet), material, day/night tint, flashlight flash | NPCAI (Pres optional) | MOVE. See 8 for why this stays in NPCAI rather than Pres. |
| Hazards/Storm.cs | 166 | Sandstorm/tornado membership from `ArrayList_Sandstorms` (2500 m flat); ApocaDustStorm bridge | Survival, Compat | REWRITE. Survival (Weather) provides `IWeatherRegions.StormAt(pos)` and `FunnelAt(pos)`. The DustStorm reflection (l.41-75) merges into the single Compat DustStorm bridge (Patrols/WarRig/DustStormBridge.cs). NPCAI keeps only `StormSight`/`StormHearing` factors. |
| Navigation/Nav.cs | 2126 | Grid navigation: scene sweep discovery, sliced physics bake, Dijkstra fields, exit picks, `Next`, patrol/search/cover points, BMP dumps | NPCAI, Debug | SPLIT. Routing and bake stay in NPCAI. `Dump`/`TraceDump`/`Paint`/`TraceInfo`/`CellText` (l.947-1512) and `DrawDebug` (l.2052) go to Debug. `SweepStep` discovery (l.222) moves to a Core/POI world index (see 5). Delete `MasksNow` (l.801, unused). |
| Navigation/NavMaps.cs | 339 | `.wwnav` persistence: signature hash, template clone, sliced read/write, vanilla/custom/legacy paths | NPCAI | MOVE |
| Navigation/NavMapTools.cs | 131 | F7 "Reset custom pathfinding" and "Export vanilla maps" (developer) | Tools | MOVE to Tools via a ModAPI call (`Nav.ResetCustom`, `Nav.ExportVanilla`) |
| Navigation/NavMapHighlight.cs | 80 | "Temporary collection aid": index of baked POI types for the CleanHUD map tint | Tools | MOVE to Tools, or DELETE once the vanilla pack is complete. 41 maps cover all 39 types today, so it is only useful for custom POIs. |
| Navigation/Passthrough.cs | 115 | Same-faction NPCs ignore each other's colliders while busy (0.25 s pass) | NPCAI | MOVE |
| PoiNav/PoiNav.cs | 722 | Runtime POI graph: per-instance build, A*, route, `Walk` kinematic carry, `TryNext` called from `Nav.Next`, player-ladder logic | NPCAI | MOVE. Delete `LearnLadderSide` (l.205-235, dead after `return;`). Take `WalkerTick` back from the view job (see 3). |
| PoiNav/PoiNavTraverse.cs | 407 | Ladder, climb and drop traversals: kinematic moves plus `BeginNpcTraversal` animation, ladder occupancy | NPCAI, Pres | SPLIT lightly. The move logic stays in NPCAI. Animation phase calls (`Phase`, `Anim`) already go through `Services.Animation` (Pres). |
| PoiNav/PoiNavData.cs | 213 | Data model, text reader/writer, `PoiNavStore` (async load, cache, config/shipped dirs) | NPCAI | MOVE. Tools uses it through ModAPI. |
| PoiNav/PoiNavBake.cs | 598 | Offline developer baker: top-down column casts, surfaces, climbs, drops, ladders, node graph | Tools | MOVE to Tools. Its `Mask` constant is used at runtime (PoiNav.Walk), so it moves to NPCAI. |
| PoiNav/PoiNavSetup.cs | 199 | Developer "Setup POI pathfinding": places stripped POI copies, bake queue, test NPCs, sets `Services.PoiNavSetup` | Tools | MOVE to Tools. The flag becomes a Core ModAPI `WorldGenerationSuspended` read by Towns/POI. |
| PoiNav/PoiNavEditor.cs | 481 | Mouse/free-camera link editor: handles, undo, save, test NPCs | Tools | MOVE to Tools |
| PoiNav/PoiNavView.cs | 130 | GL line display of POI nav data (Camera.onPostRender); also calls the runtime `WalkerTick` | Tools, NPCAI | SPLIT. Display goes to Tools. `PoiNav.WalkerTick()` (l.27) moves to an NPCAI job. Delete `LearnLadderSide()` (l.26). |
| Women/NpcModule.Women.cs | 51 | Binds the Women of Wasteland config (migrates from WoW/Apocaraider cfg) plus the debug spawn key | NPCAI, Pres | SPLIT. Population entries (`EnableFemaleNpc`, `FemalePopulation`, `FemaleCrew`) go to NPCAI. Model/texture/hide-parts/voice entries go to Pres. `FemaleSpawnKey` goes to Debug/Developer. |
| Women/WomenFeature.cs | 104 | Feature entry: standalone check, asset install, `Services.NpcAppearance` and `CharacterAssets` providers, jobs | Pres | MOVE to Pres. It provides `INpcAppearance` and `ICharacterAssets`. |
| Women/Gungirl.cs | 615 | Female variants: spawn-chance rename (identity by name), body swap (mesh/skin/bindposes), corpse label, scene scan, debug spawn | NPCAI, Pres, Debug | SPLIT. NPCAI gets the population rule: `AfterCreateObject` chance and `Rename`, the `DressCrew` randomize, `Registered`/`IsOldMarked` identity. Pres gets the body swap: `Apply`, `Initialize`, `Variant` meshes/materials, `Label`, `Shutdown` restore. Debug gets `SpawnInFront`/`Register`. `StartPass`/`Step` scene walk is replaced by the Core spawn index (see 5). |
| Women/Voice.cs | 354 | Female combat voices: per-NPC FSM clip swap, WAV/OGG load, loudness match | Pres | MOVE to Pres |
| Women/Gltf.cs | 417 | glTF/glb skinned model reader | Core | DELETE. Identical to PlayerBody/Body/Gltf.cs except the namespace and one warn call (l.318). Consolidate in Core. |
| Women/Json.cs | 117 | Minimal JSON reader | Core | DELETE. Identical to PlayerBody/Body/Json.cs and Patrols/WarRig/Json.cs (only the namespace differs). Consolidate in Core.Json. |
| Women/Bindposes.cs | 36 | Bodyguard.001 bind-pose table | Core or Pres | DELETE. Identical to PlayerBody/Animation/Bindposes.cs. |
| Women/Wav.cs | 61 | WAV decoder | Core | DELETE. Identical to Guns/Tracers/Wav.cs (Patrols/Crews/Rider.cs:1086 has a third Wav). |
| Women/Level.cs | 76 | Loudness (RMS) matching and soft limiter for voice clips | Pres or Core audio | MOVE. It is the only copy; put it beside Core Wav as a shared audio utility. |

---

## 2. Cross-module coupling

### 2.1 Services this area consumes

| file:line | used | why |
|---|---|---|
| Brain/ApBody.cs:28,64,108-118 | `Services.Animation` (`IAnimationApi.Ready/Attach/HasWeaponPoses/ReloadsOneRoundAtATime/CocksAfterShot/ReloadClipSeconds/RoundSeconds/NativeSpeed/ClipNames`), `IAnimatedCharacter.*` | Gunmen animated with player clips |
| Detection/Senses.cs:1286 | `Services.Animation.PlayerShout` | The player's shout clip |
| PoiNav/PoiNavTraverse.cs:109,133-134,262,303 | `Services.Animation.BeginNpcTraversal`, `LadderCycleRise`, `LadderToeDepth` | Ladder and climb animation on NPCs |
| PoiNav/PoiNav.cs:209,216,553,674; PoiNavTraverse.cs:256,331,352,371,377 | `Services.PlayerLadder`, `PlayerLadderSide`, `LadderSlidePhase`, `LadderSlideSpeed` (fields written by PlayerBody/Climbing/Ladders.cs) | NPC fights on ladders, sliding down |
| Detection/WeaponRanges.cs:19,24,54; Debug/NpcPages.cs:69 | `Services.Guns.ProjectilesEnabled/EffectiveRange/TryGetWeaponKind` | Ranges and kinds (falls back when Guns is off) |
| NpcModule.cs:69-70 (via Senses.AfterBailOut/AfterBlast) | events `Services.CrewBailedOut`, `Services.VehicleExploded` (raised by Patrols) | Bailed crews keep knowledge; explosions are heard |
| Detection/Senses.cs:1345; VanillaEvents.cs:186 | `Services.RaiseNpcHurt` (raises; HUD and Quests may consume) | Hurt notification |
| NpcModule.cs:71 | `SaveBus.StateChanged` (Senses persistence); DogAnimations.cs:72 the same | Save/load watcher |
| many | `GameRefs.Player/MainCamera/NewGOArrayList/ItemNameId/SaveLoadGame/InGame/InPlay`, `Origin.AnchorTransform` (Nests.cs:244), `Hooks.*`, `Patches`, `Scheduler`, `Hotkeys`, `Assets`, `Legacy`, `ReplacementGuard` (WomenFeature.cs:38), `Plugin.EnableDebug/DeveloperMode` (PoiNavEditor.cs:41, PoiNavView.cs:28, PoiNav.cs:212), `EditorSession`, `DiskResources` (WomenFeature.cs:88), `SaveRuntime.IsSaveFile` (Senses.cs:2040), `SaveBus.ReportFailure` | Core infrastructure (allowed) |

### 2.2 Services this area provides

| member | signature | set at | consumers |
|---|---|---|---|
| `Services.Npc` | `INpcApi`: `int ContractVersion`; `SetSimpleConvoyBandit(GameObject,bool)`; `SetSimpleConvoyBanditState(GameObject,bool,bool)`; `SetSimpleConvoyBanditTarget(GameObject,GameObject)`; `Shot(GameObject shooter, Vector3 pos, int weaponKind, bool player)`; `Hurt(GameObject victim, GameObject attacker)`; `float SpreadFactor(float distance)`; `Inform(GameObject npc, GameObject target)`; `SetFollowerDriven(GameObject, bool, float reloadUntil=0)`; `Structures(List<NavStructure>)`; `bool HighlightMissingPoiBake(string prefab)`; `Travel(GameObject, Vector3? scenePoint, float arriveRadius)`; `int TravelState(GameObject)`; `bool IsIdle(GameObject)`; `Remember(GameObject npc, GameObject target, float seconds)`; `bool CrewOrder(GameObject npc, GameObject enemy, bool boarding)` | NpcModule.cs:68, cleared at :136 | Guns (GunsApi.cs, GunsPages), Patrols (CrewReturn, CrewPoi, CrewBusDefence, SimpleBanditBridge, TownTraffic, TrafficFoot, TrafficLocations), Quests (Hire, QuestFollower), Towns (PoiDressing, TownBakeBridge, TownGasStation, TownNavigation), CleanHUD (LocationMarkers) |
| `Services.NpcAppearance` | `INpcAppearance.Dress(GameObject root, string identity, bool randomize)` | WomenFeature.cs:45 | Patrols/Crews/CrewAppearance.cs |
| `Services.CharacterAssets` | `ICharacterAssets.TryPaths(string identity, out string model, out string texture)` | WomenFeature.cs:15 (bound in **BindConfig**, so it is set even when the module is off and never cleared) | PlayerBody/Body/CharacterModels.cs (female player bodies) |
| `Services.PoiNavSetup` | `static volatile bool` | PoiNavSetup.cs:37,142,197 | Towns (TownsModule, TownGasStation): suspends town generation |
| `Services.LadderClimbers` | `List<Vector3>` (feet of NPCs on ladders), rebuilt each frame | PoiNavTraverse.cs:195,206 | PlayerBody/Climbing/Ladders.cs (blocks the player) |

`NavStructure` (Core) is filled by `Nav.Structures` (Nav.cs:2075).

### 2.3 Rule violations (direct module-to-module type or member access)

| where | what | fix |
|---|---|---|
| Towns/InteriorPopulation.cs:8,19,195,199,224,238 | `using WildWasteland.Modules.NPC`; types `Senses.Agent`, `Senses.AgentFor`, `Senses.AllAgents`, `Senses.ClearDeafInvestigation`; writes `Agent.Deaf/Interior` (fields at Senses.cs:66) | Add ModAPI `Npc.SetHearingPolicy(id, deaf, interior)` and `Npc.Enumerate(...)` |
| Towns/TownBakeBridge.cs:24-39 | Reflection on `NpcModule` fields (`Tuning`), `Nav.get_On`, `Nav.Spawned`, `Brain.Spawned`, `Nav.FloorCells`, `Nav.StructureRootAt`; **Harmony prefix/postfix on `Nav.Next`** | `INavExtension`: `RegisterRouteProvider`, `DiscoverStructure(go)`, `RegisterActor(go)`, `FloorCells`, `StructureAt` |
| Towns/TownOverlap.cs:75-87 | Reflection on 20 `Nav.Structure` fields and 5 private caches; **patches `Nav.Make` (postfix), `Nav.BeginBake` (prefix), `Nav.CreateMap` (prefix)** | A grid overlay API: `Nav.ApplyOverlay(root, mask)` and `Nav.InvalidateCaches()` |
| Towns/PoiDressing.cs:42-43,201-221 | **Patches `Nav.Make`** (prefix); reflection `CloseMap`, `_baking`, `_needed`, `PickReset`, `MapColliders`, clears private caches | `Nav.Rebake(root)` / `Nav.Forget(root)` |
| Towns/TownTowerKit.cs:25-93 | Reflection drives `Nav.Make/BeginBake/BakeStep/CloseMap`, `_baking`, `_floorCols`, `_maskQueue` | `Nav.BakeNow(root, callback)` |
| Core/UI/CorePages.cs:135,247,266,277 | Core references `NpcModule.ShoutKey`, `ShoutModifier`, `ShowNav` | Core owns the Shout hotkeys; Debug owns the overlay flag |
| Core/Plugin.cs:129 | `new Modules.NPC.NpcModule()` | Normal registration, allowed |
| Core/SaveTypes.cs:34 | `Alias("NPCAI","NPCAI","WildWasteland.Modules.NPC")` | Must follow any namespace rename (see 9) |
| Core/SaveFiles.cs:183-184,190 | Hard-coded `NPC/Saves/<slot>.senses.txt` | Saves sidecar API registration instead |
| Towns/TownBakeData.cs:41-42 | Hard-codes NPC NavMaps dirs | Expose `Nav.MapDirs` via ModAPI |

Inside the module (these become cross-module after the split): Brain, Idle, Senses, PoiNav, Burrow, Pounce, DogBite and Passthrough call each other's statics freely. For example `PoiNav.Guiding` is checked at 8 sites in Brain.cs:463,703,721,765,1157 and Idle.cs:574-777, and `Senses.Agent` is the shared NPC record. The Presentation split (ApBody/DogAnimations/Gungirl) and the Tools split (PoiNavEditor/Setup/Bake/View, NavMapTools) must therefore be cut along ModAPI lines.

---

## 3. Harmony patches and scheduler jobs

### 3.1 Patches

| file:line | target method | kind | Core.Hooks or Patches | cost and filter notes |
|---|---|---|---|---|
| NpcModule.cs:204 | `SendEvent.OnEnter` | prefix (skip + Finish) | Hooks.SendEventEnter (Brain.BeforeSendEvent) | Hot (every SendEvent). `_npcs.Count==0 \|\| !On`, then string compares on the event name. Shared with Patrols (NativeStormProtection, Ram). |
| :205 | same | prefix | Hooks (Aim.BeforeSendEvent) | FSM "RangedAttackWait" |
| :207 | `SetVelocity.DoSetVelocity` | prefix (skip) | Hooks.SetVelocityDo (Brain.BeforeSetVelocity) | Per frame per ground NPC. `!On`, then `fsm.Name != "Movement"`; registers NPCs (`Of`). Calls `PoiNav.SteerDir/Walk` (one RaycastNonAlloc per walking NPC per frame). |
| DogAnimations.cs:75 | same | prefix (skip) | Hooks.SetVelocityDo | Only with the bundle; `Dogs.Count` filter |
| NpcModule.cs:209-212 | `CreateObject.OnEnter` | postfix | Hooks.CreateObjectEnter (Senses, Idle, Pounce, WeaponsPackCompat.AfterCorpse) | Every spawn. Senses fans out to `Nav.Spawned` and `Brain.Spawned`. Prefix names are cached. Shared with PlayerBody, Guns, Patrols, Towns. |
| WomenFeature.cs:46 | same | postfix | Hooks (Gungirl.AfterCreateObject) | Every spawn; cached prefab name |
| NpcModule.cs:214 | `SetFsmFloat.OnEnter` | postfix | Hooks.SetFsmFloatEnter (VanillaEvents.AfterDamage) | Very hot. First test `!Senses.On`, then `fsmName.Value != "Bodypart"` string compare. GetComponents on player hits (VanillaEvents.cs:182). Shared with HUD, Guns, Patrols. |
| :215 | same | postfix | Hooks (DogBite.AfterSetFsmFloat) | Same frequency |
| :217-219 | `GetButtonDown.OnUpdate`, `GetButton.DoGetButton`, `GetButtonUp.OnUpdate` | prefix (skip) | Hooks (Senses.BeforeGetButton*) | Every input action per frame. The modifier is read once per frame. Shared with Core, PlayerBody, Guns. |
| :221 | `Raycast.DoRaycast` | prefix (skip) | Hooks.RaycastDo (Brain.BeforeRaycast) | `_npcs.Count==0 \|\| !On`, FSM "Attack" |
| :222 | `Raycast.OnEnter` | prefix (observe) | Hooks.RaycastEnter (VanillaEvents.BeforePlayerShot) | `!Senses.On \|\| ProjectilesEnabled`; shared with Guns Tracers |
| :223 | `SmoothLookAt.DoSmoothLookAt` | prefix (skip) | Hooks (Brain.BeforeSmoothLookAt) | Every SmoothLookAt; shared with PlayerBody FirstPersonSway |
| :224 | `Fsm.ProcessEvent(FsmEvent, FsmEventData)` | prefix | Hooks.ProcessEvent (Brain.BeforeDrivingHide) | Every FSM event in the game. `_npcs.Count==0 \|\| !On \|\| ev.Name != "hide"` (string compare). Shared with Guns. |
| DogAnimations.cs:74 | same | prefix (skip) | Hooks.ProcessEvent (BeforeDamageEvent) | Bundle only |
| NpcModule.cs:225 | `ActivateGameObject.OnEnter` | postfix | Hooks (Brain.AfterActivate) | Dictionary lookup |
| :227 | `SensorGetDetections.DoAction` | prefix (skip) | Hooks.SensorDetectionsDo (Senses.BeforeGetDetections) | Per frame per Detection FSM; registers agents. Shared with Guns Bosses. |
| :228-229 | `SensorGetLineOfSightResult.OnEnter3D/OnUpdate3D` | prefix (skip) | Hooks (Senses.BeforeLosResult) | Shared with Patrols SeatedLosGuard |
| :230-231 | `SensorGetDetectionRayHit.OnEnter` | prefix + finalizer | Hooks (VanillaEvents.BeforeNpcShot/AfterNpcShot) | Per burst ray; shared with Guns Tracers.BeforeRayHit |
| :237 | `Rotate.DoRotate` | prefix (skip) | Patches (module-only) | Per frame per rotating NPC; ApocalypterAI also patches it |
| :238 | `LookAt.DoLookAt` | prefix (skip) | Patches | |
| :239 | `AddForce.DoAddForce` | prefix (skip) | Patches | ApocalypterAI also patches it |
| :240 | `AudioPlay.OnEnter` | prefix (skip) | Patches (Senses.BeforeAudioPlay) | Every AudioPlay; `!On`, FSM name |
| :241 | `RandomWait.OnEnter` | prefix | Patches (Aim.BeforeRandomWait) | FSM "RangedAttackWait" |
| :242-243 | `ActivateGameObjects.OnEnter` (optional) | postfix | Patches | Rare |
| DogAnimations.cs:76 | `Wait.OnEnter` | prefix | Patches | Every Wait in the game; `Dogs.Count==0` filter. Patrols patches `Wait.OnUpdate` (a different method). |
| DogCompanionCompat.cs:71-78 | `DogCompanion.Dog.Update` (pre+post), `.ChaseTick`, `.LateUpdate`, `.OnDisable`, `.Steer`, `.RideTick`, `.Step`, `DogCompanion.Distraction.Nearest` | mixed | Patches (third party) | Per companion tick; compiled delegates |
| WeaponsPackCompat.cs:65-70 | `ChainsawPrototype.FlamethrowerIgniteStatus.Touch/BeginEnding/OnDestroy`, `Plugin.ProcessHit` (pre+post), `StartPreview`, `StopPreview` | postfix / prefix | Patches (third party, installed lazily from the 2 s job) | `ProcessHit` prefix walks every state and action of the victim's Health FSM per saw hit (l.112-135) |
| **Towns -> NPC** (TownOverlap.cs:85-87, PoiDressing.cs:43, TownBakeBridge.cs:37) | `NPC.Nav.Make`, `Nav.BeginBake`, `Nav.CreateMap`, `Nav.Next` | prefix/postfix | Patches (Towns module) | **Harmony patching another module's internals.** Must become an NPCAI ModAPI extension point. |

### 3.2 Scheduler jobs and other callbacks

| name | kind | interval/budget | file |
|---|---|---|---|
| Senses | EveryFrame | Look rate capped per frame (see 6.1) | NpcModule.cs:83 -> Senses.Tick |
| Nav | EveryFrame | Sweep 250 nodes/frame; bake `BakeBudgetMs` 1 ms (×0.5 to ×4); pick 1 ms; masks 0.5 ms | :84 -> Nav.Tick |
| Brain | EveryFrame | Think 0.1-0.5 s with a per-frame cap | :85 |
| Weapons Pack compatibility | Interval | 2 s (scaled) | :86 |
| NPC.Rigs | Sliced | 1000 µs, LateUpdate | :87 -> Brain.BuildRig |
| Idle | EveryFrame | Think 0.2 s × scale with a cap | :88 |
| Passthrough | Interval | 0.25 s (scaled) | :89 |
| Burrow | EveryFrame | | :90 |
| Pounce | EveryFrame | | :91 |
| Webs | EveryFrame | Internal 1 s tint / 15 Hz flash; should be Interval | :92 |
| Nests | EveryFrame | Internal 1 s, 0.1 s and 0.2 s timers; a slice of `NestFrameBudgetMs` | :93 |
| Ambush | Interval | 0.5 s | :94 |
| DogBite | EveryFrame | Also ticks DogAnimations | :95 |
| Aim.Sweep | Interval | 10 s (unscaled) | :96 |
| WeaponRanges.Sweep | Interval | 30 s | :97 |
| Burrow.Late / Pounce.Late / DogBite.Late | EveryFrame, LateUpdate | Visual (Pres) | :98-100 |
| Overlay | Gui | Predicate `Running`; job enabled only while ShowNavigation is on | :101 |
| PoiNav.Setup | EveryFrame | Developer only, early-out | :108 |
| PoiNav.Build | Sliced | 500 µs (256 nodes / 64 rays per frame) | :109 |
| PoiNav.Traverse | EveryFrame, EarlyFixedUpdate | | :110 |
| PoiNav.View | EveryFrame | **also runs runtime `PoiNav.WalkerTick`** | :111 |
| PoiNav.Editor / PoiNav.EditorCamera | EveryFrame / LateUpdateLast | Early-out when closed | :112-113 |
| PoiNav.EditorGui | Gui | `WantsGui` = Opened | :114 |
| Female restoration | Sliced | 500 µs | WomenFeature.cs:47 |
| Female discovery | Interval | 5 s (full walk every 60 s) | :48 |
| Female voices | Interval | 0.25 s | :49 |
| Female cache cleanup | Interval | 10 s | :50 |
| (coroutine) Senses.Persist.Restore | Core.Scheduler.StartCoroutine | After load | Senses.cs:2023 |
| (coroutine) Voice.Load | Core.Scheduler.StartCoroutine | Start | Voice.cs:52 |
| (callback) PoiNavView.Draw | `Camera.onPostRender` | Only while display/editor is on | PoiNavView.cs:31 |
| Hotkeys | Shout (Senses), POI navigation editor, Spawn female raider | | NpcModule.cs:105,115; WomenFeature.cs:51 |

Naming: most jobs lack the `NPC.` prefix that AGENTS 5.4 asks for. Only `NPC.Rigs` has it; `Senses`, `Brain`, `Female voices` and the rest do not.

---

## 4. Logging

### 4.1 Switches and config keys

| switch | where bound | real meaning | call sites |
|---|---|---|---|
| Core `[Debug] VerboseLog` (global) | Core | `Core.Log.Info` gate. `LogAdapter.LogInfo` falls back to it. | All `NpcModule.Log.LogInfo` and `Verbose()` calls |
| `[NPC / Debug] VerboseLog` (tools.cfg) | Config.cs:130 (+ migrate from npcai.cfg) | Module verbose. When on, `LogInfo` writes with `Core.Log.Always`, even when the global switch is off. | Senses.cs:501 (light), Voice.cs:188, NpcModule.cs:35,41 |
| `BrainLog`, `SensesLog`, `NavLog` | **aliases**: `NavLog = SensesLog = BrainLog = VerboseLog` (Config.cs:335) | Same entry as VerboseLog | BrainLog 40 lines (Brain.cs ×31, Idle.cs:86,391,563,787, Nav.cs:105 `LogOn`, ReloadSounds.cs:52, Brain.Weapons.cs:52); SensesLog 35 lines (Senses ×26, Senses.Spiders, SimpleBandits, PoiNav.cs:231, PoiNavTraverse); NavLog 8 lines (Nav.cs:189,231,264,295,467,472,1405; NavMaps.cs:63) |
| `[NPC / Burrow] Log` | Config.cs:168 | Burrow traces | Burrow.cs:138,229,255,313,405,522 |
| `[NPC / Dogs] Log` | Config.cs:170 | Dog reach and bones | DogAnimations.cs:172,210,613; DogBite.cs:70 |
| `[NPC / Nests] Log` | Config.cs:190 | Nest seating, drops, frees | Nests.cs:203,223,367,440,569,598 |
| `[NPC / Pounce] Log` | Config.cs:205 | Every zig, leap, bite | Pounce.cs:100,425 |
| `[NPC / Debug] NavDump` | Config.cs:132 | BMP map pictures (0.5-4 MB each) + per-second trace lines with VerboseLog | Nav.cs:467,475,1409; Brain.cs:777 (`Tracing`) |
| `[NPC / Debug] ShowNavigation` (+ alias `ShowGhosts`, Config.cs:336) | Config.cs:134 | World overlay (ghosts, NPC states, waypoints) | NpcModule.cs:101-103; Nav.cs; Idle.cs; Senses.cs (2 sites); CorePages.cs:266,277 |
| `[NPC / Debug] HighlightUnbakedMapPois` | Config.cs:136 | Map tint (collection aid), not a log | NavMapHighlight.cs |
| `NavEnabled` alias of `BrainEnabled` | Config.cs:334 | Not logging; dead alias | 3 refs |

### 4.2 Log helpers and sinks

| helper | file:line | behaviour |
|---|---|---|
| `NpcModule.Log` (`LogAdapter`, **not a BepInEx `ManualLogSource`**: it mimics `LogInfo/LogWarning/LogError` so the ported NPCAI code compiles) | NpcModule.cs:27,39-44 | `LogInfo`: Always when module VerboseLog is on, else Info when the global one is on. Warning/Error always. 96 LogInfo, 25 LogWarning and 45 LogError lines in 20 files. |
| `NpcModule.Verbose(s)` | NpcModule.cs:35 | Same as LogInfo but always forwards to `Core.Log.Info`. 12 call sites (Senses, Burrow, Gungirl, Voice, Brain, Aim). |
| `NpcModule.Warn(s)` | NpcModule.cs:36 | `Core.Log.Warn`. 21 sites. |
| `Senses.Log(Agent, s)` | Senses.cs:1876 | Gated on SensesLog. **Callers build the string first** (19 call sites with concatenation, for example Senses.cs:246, 1442). |
| `Idle.Log(Ctl, msg)` | Idle.cs:785 | Gated on BrainLog; callers concatenate first (for example Idle.cs:285) |
| `Pounce.Log(P, s)` | Pounce.cs:425 | Gated on PounceLog; callers concatenate first |
| `Nav.LogOn` | Nav.cs:105 | BrainLog; used as `LogOn ? "text" : "-"` (correctly lazy) |
| `Brain.Tracing` / `TraceDump` / `LogAhead` | Brain.cs:777-800,1456 | NavDump && BrainLog; `LogAhead` uses `Physics.RaycastAll` (debug only) |
| `Nav.Dump` / `TraceDump` / `Paint` | Nav.cs:990,1424-1512 | BMP writer |
| Direct `Core.Log.Info/Always/Warn` | PoiNav*.cs, NavMapTools, NavMapHighlight, WeaponsPackCompat, WomenFeature, NpcPages, Config | Bypass the adapter. PoiNav.cs:231 checks `SensesLog` for a ladder message. |

### 4.3 Proposed mapping to a single Debug level (0 = off, 1 = main actions, 2 = deep)

| level | content (current source) |
|---|---|
| always (Warn/Error) | Every LogWarning/LogError/Warn: integration failures, bake failures, persistence failures, hook exceptions |
| 1 | Module start/stop and integrations found or missing (Weapons Pack, Dog Companion, ApocaDustStorm, CaveDarkness, animation framework ready, dog bundle loaded, Women integrated). Nav: structure found or gone, map baked, loaded, saved or reset, export. PoiNav data loaded or saved. Nests: nest planned, built, freed, alarm. Senses persistence (saved/restored N ghosts and agents). Per-NPC state transitions: engage, lose target, search start/end, flee/fright, cover start (`SetMode` with reason). Travel orders accepted/arrived/failed. Female conversions. Burrow hunter dive/rise. |
| 2 | Everything that is per think or per frame. Brain feeler/nav reasons and `Nav.LastReason`, stuck/hop details. Senses ghost merges, light level, sight-miss reasons, noise fan-out. Idle leg/think details. Pounce zig/leap/bite. Dog bones and reach. Burrow terrain layers. Nest seat/drop details. Reload-sound discovery. Per-second NavDump trace lines. |
| Debug tools, not log levels | NavDump BMP files become a Debug button ("dump the map/trace of the nearest NPC") instead of a switch. ShowNavigation becomes a Debug overlay toggle. HighlightUnbakedMapPois moves to Tools. |

Required with the collapse: every call site must test the level **before** concatenating. This applies to Senses.Log, Idle.Log, Pounce.Log and about 70 `if (...Log.Value) LogInfo("..."+...)` sites, which are fine once guarded. `Log.LogInfo` calls without a guard build strings even at level 0 (for example `Nests.cs:221` and `Storm.cs:62`, which run once, and `Brain.cs:482`). Delete the aliases at Config.cs:334-336 and the four `<X>/Log` entries, but keep the keys readable for migration (section 9).

---

## 5. Redundancy and dead code

| # | item | evidence | action |
|---|---|---|---|
| 1 | ChainsawLimbs entirely disabled | ChainsawLimbs.cs:1 `#if false`; commented calls WeaponsPackCompat.cs:40,182,185; NpcModule.cs:175-176 | Delete (158 lines) |
| 2 | `Brain.LateTick` empty and never registered | Brain.cs:1453; grep "Brain.LateTick" = 0 | Delete |
| 3 | `PoiNav.LearnLadderSide` dead after `return;` with `#pragma warning disable CS0162` | PoiNav.cs:205-235, still called from PoiNavView.cs:26 | Delete |
| 4 | `Nav.MasksNow` unused | Nav.cs:801 (only its definition) | Delete |
| 5 | Config aliases | Config.cs:334-336 (`NavEnabled`, `NavLog`, `SensesLog`, `BrainLog`, `ShowGhosts`) | Replace with the Debug level |
| 6 | DogCompanionSeating is a constants-only file | DogCompanionSeating.cs (11 lines), used only at DogCompanionCompat.cs:372 | Merge |
| 7 | Duplicated utilities, byte-identical except the namespace | Women/Json.cs = PlayerBody/Body/Json.cs = Patrols/WarRig/Json.cs (StewPot and Towns have more Json copies; CleanHUD/Json differs); Women/Gltf.cs = PlayerBody/Body/Gltf.cs (one line differs: l.318 warn sink); Women/Bindposes.cs = PlayerBody/Animation/Bindposes.cs; Women/Wav.cs = Guns/Tracers/Wav.cs (+ Patrols/Crews/Rider.cs:1086 Wav) | Core.Json, Core.Gltf, Core.Wav; Bindposes to Pres |
| 8 | Flexa prefab lookup twice, each a full `Resources.FindObjectsOfTypeAll<GameObject>()` | Senses.cs:1315 `FlexaPrefab()` and Gungirl.cs:586 `FlexaPrefab()` | One cached prefab registry in Core (`GameRefs.Prefab("Flexa")`) |
| 9 | Three or more scene-hierarchy walks for discovery | Nav.SweepStep (Nav.cs:222, 250 nodes/frame, every 2-15 s); Gungirl.StartPass/Step (Gungirl.cs:229/248, 0.5 ms slices, every 60 s); Nests.Pois (Nests.cs:242, MapMagic children every 5 s); PoiNavSetup (its own MapMagic prefab list); Towns NativePoiPopulation and PoiDressing | One Core "world index" (POI roots by name/tag, plus a spawn stream from the CreateObject hook) shared by NPCAI, POI, Towns and Loot |
| 10 | Two NPC registries plus about 9 side lists | `Senses._agents` (Make at Senses.cs:1705 runs GetComponents<PlayMakerFSM> and GetComponentsInChildren<Behaviour> and Transform); `Brain._npcs` (Make at Brain.cs:1697 runs GetComponents<PlayMakerFSM> again); `Idle._ctl`; `Burrow/Pounce/DogBite/Ambush/Nests` lists; `WeaponRanges._shooters` (another GetComponents); `VanillaEvents._refs`; `Aim` states; `DogAnimations.Dogs` (GetComponentsInChildren plus LINQ) | One `NpcRecord` per NPC with cached FSM refs, built once; subsystems hang off it |
| 11 | Two ground-movement drivers | `Brain.Steer`/`NavSteer` (Brain.cs:717,800: FeelerCount=7 sweeps, side commitment) and `Idle.Drive` (Idle.cs:747: its own 3-way capsule fan, own TurnRate handling, own `PoiNav.Walk` call) | One locomotion component in NPCAI (`Mover.Drive(npc, goal, speed)`) |
| 12 | CSV prefab-list parsing repeated about 12 times | Split at Nests.cs:146, Senses.cs:433,516,1072,1107,1261,1789, Pounce.cs:67, Burrow.cs:97,514, DogBite.cs:41, Ambush.cs:23 | `Core.Config.CsvSet(entry)` cached helper |
| 13 | Weapon classification duplicated | WeaponRanges.Classify (WeaponRanges.cs:39) = Guns Tracers.cs:746; the WeaponOf fire_effect scan = Tracers.cs:678; ApBody.WeaponKey/IsPistol = `IAnimationApi.WeaponKey/WeaponKind` (PlayerBody Props.Norm/KindOf) | Gunplay owns kind and range; Pres owns the key; NPCAI asks via ModAPI |
| 14 | Weapon range fallback table | WeaponRanges.cs:22-37 (60/70/120/250/35/90 m) vs Guns `EffectiveRange` | Keep only if NPCAI must work without Gunplay. Otherwise delete and require the Gunplay service. |
| 15 | NWH engine reflection | Senses.cs:1955-1988 (`VehicleController.powertrain.engine.IsRunning`, `input.Throttle` via reflection `GetValue` every 0.5 s); the same reflection exists in Patrols (Patrol.cs, Pilot.cs, Explode.cs), PlayerBody/Vehicles/Car.cs, Guns Tracers and HUD VanillaHits | Use `Services.VehicleIgnition.TryRunning` (already in Core) plus a new `Throttle` query |
| 16 | Two ApocaDustStorm bridges | NPC Storm.cs:41-75 and Patrols/WarRig/DustStormBridge.cs | One Compat bridge |
| 17 | Storm membership duplicates Weather | Storm.cs:96-140 re-reads `ArrayList_Sandstorms` and `TornadoPhysics` and uses a fixed 2500 m circle. Weather (WeatherNative.cs, `Services.Weather` `IWeatherRegions`) owns the native storm roots and draws region shapes, so the NPC "in storm" test can disagree with what the player sees. | Survival provides `StormAt(pos)` |
| 18 | Time-of-day via Azure reflection | Senses.cs:489-566 (`AzureTimeController.GetTimeline`, `Resources.FindObjectsOfTypeAll(type)` every 10 s while missing); Performance/Content/HudFsms.cs and Performance/World/WorldSettings.cs also read Azure | Core `World.Hour` / `World.LightLevel` |
| 19 | Two reload-sound / magazine concepts | Brain magazines (`MagazineTable`, Brain.cs:1288-1370) vs Gunplay ammo/magazines; Quests QuestConfig.cs:177 `ReloadSoundFrom` | Magazine sizes to Gunplay; reload audio to Pres |
| 20 | Two navigation systems | See 5.1 | |
| 21 | Debug drawing spread inside behaviour files | Senses.DrawDebug (Senses.cs:1925), Nav.DrawDebug (Nav.cs:2052), Idle.DrawDebug (Idle.cs:791), Nav BMP dumps | Debug module reads NPCAI state through a ModAPI |
| 22 | Legacy migrations or shims that can be retired | NPCAI data copy `MigrateData` (NpcModule.cs:255); Restore fallbacks to `config/NPCAI/Saves` and `config/Apocaraider/Saves` (Senses.cs:2088-2090); Nests LegacyLayoutFile (Nests.cs:48); NavMaps legacy root files and `LegacyMapFile` (NavMaps.cs, README); DogAnimations legacy bundle path `plugins/NPCAI/Models` (DogAnimations.cs:82); WoW asset copy from `plugins/WomenOfWasteland` (WomenFeature.cs:74); Gungirl pre-0.6.0 scale marker `IsOldMarked` (Gungirl.cs:144-152); PoiNav `LadderSpeed` 1 -> 2 rewrite (PoiNav.cs:31, **overrides a user's deliberate 1.0**); renamed hotkeys "NPC shout" -> "Shout" (Config.cs:116-128) | Config/save migrations should stay at least one more release (AGENTS 5.10). The LadderSpeed rewrite and the pre-0.6.0 marker can go now. File-copy migrations can go after release N+1. |
| 23 | Dead commented code | NpcModule.cs:175-176; Brain.cs:1453; Webs.cs header "(TEST)"; `Nav.Probe` is used only by a debug log (Brain.cs:482) | Clean up |
| 24 | `Services.CharacterAssets` set in BindConfig and never cleared | WomenFeature.cs:15 via NpcModule.Women.cs:48 | Provider lifecycle belongs in Init/Shutdown (Pres) |

### 5.1 The two navigation systems

| | Grid maps (`Navigation/Nav.cs` + `NavMaps.cs`) | POI graph (`PoiNav/*`) |
|---|---|---|
| Origin | NPCAI 1.x, ported | WW 2026-10-20 (Testing) |
| Discovery | Nav's own scene sweep (`SweepStep`, names `Camp_/Building_/Cave_/Wreck_/Starter_Area` + `SpawnerStatic` tag) plus `Nav.Spawned` from CreateObject | **Uses Nav's list** (`Nav.Roots`, PoiNav.cs:138) plus PoiNavSetup copies; type = `PoiNavStore.TypeOf(root.name)` |
| Data | Binary `.wwnav` per prefab **signature** (game build + collider hash): 41 embedded vanilla maps (`NPC.NavMaps.Vanilla.*`), plus `plugins/.../NPC/NavMaps/Vanilla`, `config/WildWasteland/NPC/NavMaps/Custom` | Text `<type>.txt` per POI **type**: `config/WildWasteland/NPC/PoiNav` > shipped `plugins/WildWasteland/NPC/PoiNav` |
| Bake | At runtime on approach, sliced physics (1 ms ×0.5..×4), every unmapped structure, including towns and custom POIs | Offline developer tool only (Setup + Bake, 8 ms budget); never at runtime for players |
| Model | One floor per 0.5 m cell, walk/hop edges, components, distance fields (Dijkstra) shared per goal | Multi-surface nodes (~2 m), walks, Ladder/Climb/Drop links, A*, editor links |
| Who uses it | All ground NPCs (humans and creatures) via `Brain.MoveToward` -> `Nav.Next`, Idle legs, `Services.Npc.Travel`, plus Nav.PatrolPoints/SearchPoints/CoverSpot/NearWall (Passthrough), Services.Npc.Structures (Patrols TrafficLocations, Towns), CleanHUD tint, Towns overlays and patches | Human NPCs only (`Senses.Agent.Human && Cone==0`, PoiNav.cs:507), inside a POI that has data. It is the **first step of `Nav.Next`** (Nav.cs:863). |
| Movement | Waypoint handed to the brain's feelers (physics body) | `PoiNav.Walk` makes the body **kinematic**, ignores the player's colliders, slides along the floor (RaycastNonAlloc per frame); `PoiNavTraverse` runs scripted ladders and climbs |

**Overlap and conflicts**
- Both cover the same native POIs. Inside a POI with PoiNav data, humans follow the graph while creatures (and a human the graph hands back with `false`) follow the grid. Grid baking still runs for that POI.
- Two bakers sample the same colliders. `PoiNavBake` reuses `Nav.BakeMask`, but its result never feeds the grid, and the grid never seeds PoiNav.
- PoiNav's kinematic carry bypasses Brain/Idle protections. That is why `PoiNav.Guiding`/`Busy` checks are scattered through Brain.cs:463,703,721,765,1157,1830-1839 and Idle.cs:574,581,736,750,761,776-777.
- Nav.Next has two exit semantics for "goal outside": PoiNav plans to `NearestWorld(..., groundOnly)` and the grid uses `OutNext`.
- Towns patches only the grid (`Nav.Next`, `Make`, `BeginBake`, `CreateMap`), so town routes and PoiNav routes do not know about each other.

**Recommendation**
- Make **one NPCAI navigation service** with the grid as the always-available baseline: it is needed for creatures, unmapped/custom POIs, towns, cover/patrol/search points and other modules' queries. Make the POI graph the canonical layer for multi-level human movement where data exists. Expose both behind one `INavService.Next/Query` API, and replace the Towns Harmony patches with registered route/overlay providers.
- Removable now: `LearnLadderSide`, `MasksNow`, `NavMapHighlight` (once the pack is complete), the legacy map paths after one release, and the duplicate discovery (PoiNavSetup's MapMagic scan should use the shared index).
- Later: generate PoiNav ground surfaces from the baked grid, and let creatures use PoiNav ground surfaces, so a POI with PoiNav data does not also need a runtime grid bake. That needs a format change (`MapVersion` bump), so it is not a quick removal.
- Tools: `PoiNavEditor`, `PoiNavSetup`, `PoiNavBake`, `PoiNavView`, `NavMapTools`. Runtime: `PoiNav`, `PoiNavTraverse`, `PoiNavData`/`PoiNavStore`, `Nav`, `NavMaps`.

---

## 6. Performance hot spots

### 6.1 Think scheduling and per-NPC costs (current)

| system | cadence | per-NPC cost |
|---|---|---|
| Senses.Tick (Senses.cs:129) | Maintenance 1/s (round-robin over 8 frames: storm/funnel test, relation, sensors). Look per agent every `LookInterval` 0.15 s + stagger, ×2/×4 when beyond SightRange+20/+50 m of the player. Cap per frame `ceil(N·dt/0.15)+1`. Engine noise every 0.5 s; thrown-item watch every frame. | `Look`: player test (cone, then up to 2 raycasts), then **every other agent of a hostile faction** within SightRange (`foreach _agents`, Senses.cs:269-280), each with up to 2 raycasts. That is O(N²) in NPC-vs-NPC fights (raids, Coyotes vs Scrapyard). |
| Brain.Tick (Brain.cs:172) | Counting pass 1/s: TickScale ×2 beyond 40 m, ×4 beyond 80 m from the camera. Interval 0.1 s (`ScaleWithActors`: 0.1/0.2/0.3/0.5 s for ≤5/≤10/≤20/>20) × ReactionTime%. Per-frame think cap, random phase on engage, `next += step`. | **Every frame for every registered NPC**: `Engaged()` (FSM `ActiveStateName`), `Drive()` for rigged gunmen (ApBody property sets, a ground ray when \|vy\|>0.5), `ReloadSounds.Tick`, `RotateTowards`. Per think: FeelerCount=7 capsule/ray sweeps + 1 drop check + LOS ray + `Nav.Next` (field lookup or exit pick). SetVelocity hook per frame per NPC (+1 RaycastNonAlloc when on a POI route). |
| Idle.Tick (Idle.cs:221) | Every frame over **all** Senses agents. Think 0.2 s × camera scale with a cap. | Per frame: a dictionary lookup and state tests per agent, `Brain.Retreating`, `SimpleBandits.Has`. Per moving NPC: a fan of capsule casts every 0.08-0.12 s, an Animator state check every 0.25 s. |
| Nav.Tick | Every frame | Sweep 250 transforms, pick 0.5 s, bake slice 0.5-4 ms, exit pick 1 ms, masks |
| Burrow/Pounce/DogBite/DogAnimations | Every frame over their lists | DogAnimations: ClosestPoint/Linecast per dog per frame (cached since D10) |
| Passthrough | 0.25 s | `Nav.NearWall` per agent, pairs within 6 m (O(N²) candidate test) |

### 6.2 Concrete issues

| file:line | issue | fix |
|---|---|---|
| Senses.cs:269-280 | O(N²) NPC-vs-NPC sight candidates, raycasts per pair | Spatial hash of agents per faction (rebuilt in the 1 s maintenance), a max of K candidates per look, and a shared visibility cache per pair (A sees B ⇔ B sees A for the same eye heights) |
| Brain.cs:205-244 | Per-frame per-NPC work for all registered NPCs, even far away or unengaged (`Engaged` + `Drive`) | Skip `Drive` beyond the animation LOD distance. Run `Engaged` from the Detection state change (Senses already knows) instead of polling the FSM string per frame. |
| Idle.cs:229 | Iterates all agents every frame | Keep an "active controllers" list; non-idle or seated agents are not visited per frame |
| Brain.cs:515 | `GameObject.FindGameObjectsWithTag("Hide")` every 10 s from the think path (NearestHide) | Cache Hide points per structure at discovery (Core world index) and update them on CreateObject |
| Senses.cs:560 | `Resources.FindObjectsOfTypeAll(AzureTimeController)` every 10 s while not found (for example the whole main menu) | Core World service, looked up on scene load |
| Senses.cs:1315; Gungirl.cs:586; Burrow.cs:393; ReloadSounds.cs:29 (fallback); Voice.cs:49; Gungirl.cs:108 | `Resources.FindObjectsOfTypeAll` (GameObject, PlayMakerFSM, SkinnedMeshRenderer, AudioClip) on the first use in play | Startup or a sliced Core prefab/asset index |
| Senses.cs:1187-1188,1213 | Lambdas capturing locals (`ttag`, `key`) per taunt and per shout | Static filter with fields (as done for `_beyondEngine`) |
| Senses.cs:1876 + 19 sites; Idle.cs:785; Pounce.cs:425 | Log strings built before the gate (allocations every think/look) | Check the level at the call site (section 4) |
| Senses.cs:1955-1988 | Reflection `GetValue` (field/property lookup by name every call, no compiled delegate) every 0.5 s while driving | `Services.VehicleIgnition` / a compiled delegate |
| Senses.cs:1408 | `GetComponentsInChildren<Transform>` per new car (cached per id, fine), but `_hpOf` and `_engineAbout` dictionaries are never cleared except on scene load | Sweep with the cache rule |
| Senses.cs:1459-1462 | While `GrabItem` is not found, `cam.GetComponents<PlayMakerFSM>()` runs **every frame** | Throttle (1 s) or use Core GameRefs |
| VanillaEvents.cs:168-182 | Per SetFsmFloat: three string compares (`fsmName.Value != "Bodypart"`…); on player hits a `GetComponents<PlayMakerFSM>()` | Reference-compare against interned FsmString values cached per action; cache the "has Reload FSM" result per owner |
| DogAnimations.cs:140-165 | LINQ (`Any`, `SelectMany`, `Where`, `Distinct`, `ToArray`, `Select`) and GetComponentsInChildren per dog registration (per spawn) | Plain loops into reused lists |
| ReloadSounds.cs:72-81 | `new GameObject` + AudioSource + Destroy per reload sound | Pool of 3D AudioSources (Pres) |
| Brain.cs:1461 | `Physics.RaycastAll` in `LogAhead` (debug only, but on stuck) | NonAlloc buffer |
| PoiNav.cs:290 + Brain.cs:1839 | RaycastNonAlloc per walking NPC per **physics frame** in the SetVelocity hook | Acceptable. Consider a node-height interpolation from the graph instead of a ray. |
| PoiNav.cs:275-277 | `GetComponentsInChildren<Collider>` on the NPC and the player on each new walker | Cache the player colliders once (Core) |
| PoiNavView.cs:24-29 | A runtime walker sweep is hidden in a debug view job (EveryFrame) | Its own NPCAI Interval job (0.1 s is enough for a 0.25 s timeout) |
| Webs.cs:38; Nests.cs:162 | EveryFrame jobs that throttle themselves | `Scheduler.Interval` (`Webs` 1/15 s, `Nests` 0.1 s) |
| Nav.cs:222-262 | Scene sweep: 250 `GetInstanceID`/name reads per frame, repeated every 2-15 s; `_nodeKind` grows to 200k entries | Shared Core world index (5 #9) |
| Gungirl.cs:229-262 | A second scene walk (0.5 ms slices) every 60 s, plus a full one per load | Event-driven via the spawn stream plus a one-time load pass in the shared index |
| WeaponsPackCompat.cs:112-135 | Per chainsaw `ProcessHit` (per frame while sawing): iterates every state and action of the victim's Health FSM, `FindFsmFloat` | Cache the death CreateObject actions and the compare per FSM instance |
| NpcModule.cs:101 | Gui job predicate `Running` (always true while running); the job is disabled via `Enabled` | OK. Predicate should be `ShowNav.Value`. |
| Pounce.cs:413 | `AddComponent<Rigidbody>` on a corpse created by the game's Health FSM (a saved object) for 6 s; violates AGENTS 5.10 if a save happens within 6 s | Pres: animate the tumble kinematically, or block the save window |
| NpcModule.cs:84-113 | Job names without `NPC.` prefix | Rename (`NPC.Senses`, …) |

---

## 7. ModAPI and multiplayer events

NPC identity: Unity instance ids are not stable across clients. The saved, unique per-world name (`Flexa(Clone)281`, Gungirl renames preserve the number) is the natural network id, with an NPCAI-assigned `NpcId` int mapped from it. All payloads below use ids and positions only.

| event / query | direction | payload |
|---|---|---|
| `NpcRegistered` / `NpcRemoved` | publish | npcId, prefab, faction tag, human/creature group (Cone), position, home spawn id |
| `NpcStateChanged` | publish | npcId, old/new state (Idle/Combat/Investigate/Search/Hide), targetId (player id or npcId or none), ghost position, reason code |
| `NpcModeChanged` (brain) | publish | npcId, mode (Chase/Hold/Advance/BackUp/Rest/Search/Walk/Cover), heading, crouched |
| `NpcSituation` (for Presentation, per frame, local only) | publish (local) | npcId, crouched, aiming, firing, airborne, aimPitch, reloading(seconds, rounds), strafeDev, speedCap request |
| `NoiseEmitted` | publish | sourceId, kind (Shot/Shout/Engine/Horn/Thrown/Explosion), position, radius, isPlayer |
| `PlayerShout` | consume (from Core hotkey) | playerId, position |
| `WeaponFired` | consume (Gunplay) | shooterId, weaponKind, muzzle position, isPlayer |
| `NpcHurt` | consume and raise (exists: `Services.NpcHurt`) | victimId, attackerId, damage, hit position |
| `ProjectileHit` / `ProjectileNearMiss` | consume | shooterId, position, victimId (for suppression and hearing) |
| `CrewBailedOut`, `VehicleExploded` | consume (exist) | carId, npcId(s), position |
| `NpcTraversalStarted/Ended` | publish | npcId, poiType, poiInstanceId, linkIndex, kind (Ladder/Climb/Drop), from/to (POI-local) |
| `LadderOccupancy` | publish (replaces `Services.LadderClimbers` list) | ladderId, npcId, feet height |
| `PlayerOnLadder` | consume (replaces `Services.PlayerLadder*` fields) | ladderId, side vector, slide phase/speed |
| `NestAlarm` / `NestBuilt` / `NestFreed` / `WebTouched` | publish | nestKey (POI name + rounded position), position, spider ids |
| `BurrowChanged` | publish | npcId, state (Out/Burrowed/Underground/Rising), position, target position |
| `PounceLeap` / `DogBite` | publish | npcId, targetId, from, to |
| `NpcFrightened` | consume (Compat: Weapons Pack burning or chainsaw) and publish | npcId, threat position, seconds, reason |
| `FemaleVariantAssigned` | publish (NPCAI population) -> Pres dresses | npcId, variant (Gungirl/Shoota), corpse flag |
| `NavMapReady` / `PoiNavDataChanged` | publish | prefab type, signature, source (vanilla/custom/baked) |
| Queries (existing `INpcApi`) | keep | Travel/TravelState/IsIdle/Remember/CrewOrder/SetFollowerDriven/Inform/Shot/Hurt/Structures/SimpleConvoyBandit* |
| New queries | add | `Frighten(id,pos,secs)`; `SetHearingPolicy(id,deaf,interior)` (Towns); `INavService.Next/FloorCells/StructureAt/CoverSpot/PatrolPoints`; route and overlay providers (Towns); `NpcState(id)`; `Enumerate(filter)`; `PlayerNoiseFactor` (chainsaw) |

---

## 8. Unmapped or ambiguous code

| code | candidates | recommendation |
|---|---|---|
| Women population rule vs body swap (Gungirl.cs) | NPCAI ("population rules") / Pres (models) | Split as in section 1. The rename-as-identity scheme is save-relevant, so it stays in NPCAI with the population rule. |
| Voice.cs (female combat barks) and Senses taunts (BeforeAudioPlay) | Pres / Quests (voices) | Pres. Quests voices are dialogue; combat barks are body audio. |
| ReloadSounds.cs | Pres / Gunplay | Pres (timed to the reload animation). Gunplay provides the clip set per weapon key. |
| Magazines and reload decision (Brain.cs:1288-1370, `[NpcAim] Magazines/MagazineTable/ReloadSeconds/SingleShotReload/RoundSeconds`) | Gunplay / NPCAI | Gunplay owns capacity, rounds and timings (future attachments change magazines). NPCAI decides *when* (behind cover, not mid-burst). |
| Aim.cs | NPCAI / Gunplay | Fire pacing and "hold fire beyond range" stay in NPCAI. Spread/lead go to Gunplay (Guns "NPC aim" already has lead). |
| VanillaEvents.BeforeNpcShot ray extension | Gunplay / NPCAI | Gunplay (weapon range) |
| WeaponRanges | Gunplay / NPCAI | Gunplay (Detection/WeaponRanges vs Guns ranges overlap); NPCAI keeps no table |
| Hazards/Storm.cs | Survival / NPCAI | Survival provides the storm query; NPCAI only applies the factors |
| Hazards/Webs.cs (mesh generation + tint) | NPCAI / Pres | NPCAI. Webs are gameplay objects (touch alarm, nests) and the target table lists "spider nests/webs/hanging" under NPCAI. Only the shader/tint could move later. |
| Player shout (Senses.ShoutPressed + button blocking + shout audio) | Core / NPCAI / Pres | Hotkey and InputSystem blocking go to Core. The audio clip comes from Pres (`Services.Animation.PlayerShout` today). The hearing reaction stays in NPCAI via the `PlayerShout` event. |
| CrewOrders.cs, SimpleBandits.cs, Idle.Travel | NPCAI / Patrols | NPCAI (behaviour); Patrols drives them through `INpcApi`. The target says "CrewOrders vs Patrols": keep the behaviour in NPCAI, and keep the order lifecycle (boarding timing, car selection) in Patrols. |
| Brain.Weapons FireRetreat/ChainsawWitnesses | NPCAI / Compat | Behaviour stays in NPCAI under generic names; the Weapons Pack trigger goes to Compat |
| DogCompanionCompat | Compat / Pres / NPCAI | Compat, but it needs two ModAPIs: Pres "drive this quadruped's clips" and NPCAI "threats near the companion" (`CloseThreat`, called from Brain.cs:571, which is NPCAI asking Compat; invert it to an event) |
| NavMapHighlight (map tint) | Tools / HUD | Tools: CleanHUD asks `Services.Npc.HighlightMissingPoiBake` |
| PoiNavSetup's `Services.PoiNavSetup` | Tools / Core | Tools owns the mode; Core ModAPI exposes `WorldGenerationSuspended` |
| Brain `ApocaplayerClips`, `AimPose` freeze-frame (Brain.Aim/Unfreeze) | Pres | Pres (animation driving) |
| Burrow/Pounce visual offsets (LateTick) | Pres | Pres, through a "visual root offset/rotation" API on the creature body |
| `DebugOverlay` + `NpcPages` "AI overview" job timings | Debug | Debug (the Performance page already has per-job times) |

---

## 9. Save and config compatibility constraints

**Module Id, namespace and paths.** If `NPC` is renamed to `NPCAI`, every item below changes and must be migrated, or the Id kept:
- Config sections are `Prefix + " / " + sub` (ModuleConfig.cs:28-31). These are main `settings.cfg` sections `[NPC]`, `[NPC / Detection]`, `[NPC / Pathfinding]`, `[NPC / Women of Wasteland]`, `[NPC / Gungirl]`, `[NPC / Shoota]`, `[NPC / POI pathfinding setup]`, `[NPC / Editors]`, and tools.cfg `[NPC / Debug|Brain|Senses|Detection|NpcAim|Nav|Idle|Movement|Burrow|Hunt|Ambush|Pounce|Dogs|Nests|Behaviour|POI pathfinding setup]` (about 183 tools entries). F6 keys under `[NPC]`: `Enabled`, `EnableNpcDetection`, `EnableNpcPathfinding`, `NpcAim`, `EnableIdleBehavior`, `CoyotesIdle`, `FriendsPassThrough`, `Storms`, `Burrow`, `Ambush`, `Pounce`, `SpiderNests`, `DogBite`, `DogAnimations`, `DogsLieDown`, `DogJumpAttack`, `DogCompanionCompat`. `[Keys]`: `Shout`, `Shout modifier`, `Spawn female raider`.
- The asset folder is `BepInEx/plugins/WildWasteland/NPC/` (Models: `npcai_dogs.bundle`, `npcai_wildhound.bundle`, `Flexa_female.glb`, `Sprokka_female.glb`, textures; `Sounds/Gungirl`; `NavMaps/Vanilla`; `PoiNav`). Model/voice paths in config values are relative to it.
- The data folder is `BepInEx/config/WildWasteland/NPC/`. It holds `Saves/<SaveGameN.es3>.senses.txt` (sidecar v1 format), `nest-layouts.txt`, `NavMaps/Custom/<prefab>-<signature>.wwnav`, `NavDump/`, and `PoiNav/<type>.txt` (editor links survive rebakes, so this is user data).
- Embedded resource prefixes are `NPC.NavMaps.Vanilla.*` and `NPC.WomenOfWasteland.*` (build.sh:21 and the csproj remap `NPC/WomenOfWasteland/` to `NPC/`).
- Harmony id suffix `.NPC`. Towns, Core.SaveFiles (SaveFiles.cs:183-190) and TownBakeData.cs:41-42 hard-code the `NPC` folders.

**ES3 / save types**
- `Core.SaveTypes` alias `NPCAI, NPCAI` -> `WildWasteland.Modules.NPC` (SaveTypes.cs:34). If the namespace becomes `WildWasteland.Modules.NPCAI`, update the alias target, and also map `WildWasteland.Modules.NPC.X, WildWasteland` (WW saves) to the new namespace. The NPC module adds no saved components today (no MonoBehaviours), but keep the mapping for old NPCAI saves.
- No ES3 keys are written by NPC. Female identity is carried by **GameObject names** saved by the game (`Gungirl(Clone)N`, `Gungirl_Dead(Clone)N`, `Shoota…`). Those prefixes must never change. The pre-0.6.0 scale marker is read only.
- `.senses.txt` agents are matched by saved NPC names (Senses.cs:2055-2080). The fallback read paths `config/NPCAI/Saves`, `config/Apocaraider/Saves` (Senses.cs:2088-2090) and `Core.SaveFiles.Participants` list the sidecar for slot copy/delete. Move this to the Saves sidecar API without changing the file name.
- `.wwnav` `MapVersion=1`, magic `0x574E4156`, signature covers the game build plus collider geometry (NavMaps.cs:13). A version bump rebakes, and the embedded pack must be re-exported.
- PoiNav text format (PoiNavData.cs:65-120) is shared between the shipped pack and user config.

**Config migrations to preserve (first-run only)**
- `com.denis.apocalypter.npcai.cfg` (V entries and Debug, Config.cs:64-69,131-135).
- `NPCAI/hidden-settings.not-saved` (all `H` entries and `B` toggles from `[Behaviour]`, Config.cs:57-78).
- `com.denis.apocalypter.womenofwasteland.cfg` and `com.denis.apocalypter.apocaraider.cfg` (NpcModule.Women.cs:16-28).
- Renamed hotkeys `NPC shout`/`NPC shout modifier` -> `Shout`/`Shout modifier` (Config.cs:116-128).
- `LadderSpeed` 1 -> 2 rewrite (PoiNav.cs:31): delete it. It runs every start and is not a migration.

**Log-switch collapse**: when `[NPC / Debug] VerboseLog`, `[NPC / Burrow] Log`, `[NPC / Dogs] Log`, `[NPC / Nests] Log`, `[NPC / Pounce] Log` and `NavDump` are removed, migrate "any on" to Debug level 2 once (`cfg.Migrate`). Never purge the old keys (AGENTS 5.7).

**CorePages coupling**: `ShoutKey`, `ShoutModifier` and `ShowNav` are referenced from Core (CorePages.cs:135-277) for the Game page layout and the Developer filter. Moving them changes the F3 page layout, so keep the same keys and defaults.
