# Inventory: PlayerBody (`src/Modules/PlayerBody/**`)

Snapshot read 2026-10-22: 55 .cs files, 13,946 lines (about 890 KB). The module is Apocaplayer 2.3.9 plus Apocaclimber, Low Crawl, ladders, NPC traversal, the wheel arms and the Weapons Pack and ChaseCamera bridges. The project perf audit (2026-10-15) measured about 0.55 ms/frame of WW time in PlayerBody.

Abbreviations:
- **Pres** = Presentation.
- **CamSvc** = the camera decision in §8. The camera rig implementation stays in Pres; a thin Core camera-arbitration/state service is added.
- **PIA** = the "player input adapter". This is the player-only code that samples the game's FSMs, mouse and camera and feeds the shared character model.

## Key finding for the Presentation seed

The module already has **two complete animation paths for the same Mixamo set**:
- (a) **Player:** `Body` (Body.cs + Loco.cs + CrawlBody.cs + PlayerTraversal.cs + FirstPersonJump.cs, all `partial class Body`). It reads the game directly, e.g. `Input.GetMouseButton` and `Game.*`, about 60 call sites in Body.cs and Loco.cs.
- (b) **NPCs:** `ModAPI.Character` (ModCharacter.cs), driven by the Unity-free `CharPlan` (CharPlan.cs).

CharPlan's own header (CharPlan.cs:6-9) says it is "the player's own logic (Loco.cs / Body.cs ...) with the game's inputs replaced". These pieces are duplicated method by method:

| Logic | Player version | NPC version |
|---|---|---|
| Hand rig | `RigSet`/`RigApply` (Loco.cs:162,227) | ModCharacter.cs:468 |
| Arm lift | `LiftArm` (Loco.cs:296) | ModCharacter.cs:521 |
| Twist | `YawIn`/`TwistSpine` (Loco.cs:382,388) | ModCharacter.cs:535,541 |
| Aim lift | `ApplyAimLift` (Loco.cs:278) | `AimLiftApply` (ModCharacter.cs:485) |
| Arm pose | `ApplyArmPose` (Loco.cs:352) | `ArmPoseApply` (ModCharacter.cs:499) |
| Stride phase | `PhaseOf`/`MeasurePhase` (Loco.cs:182,193) | ModCharacter.cs:275,285 |
| Turn in place | `TurnInPlace`/`EndTurn` (Loco.cs:507,542) | `DoTurn`/`EndTurn` (CharPlan.cs:451,481) |
| Upper-rig clone | `BuildUpperRig` (Loco.cs:117) | `BuildRig` (ModCharacter.cs:208) |
| Upper mask | `UpperMask` (Body.cs:1141) | `UpperMask` (ModCharacter.cs:192) |

Presentation should keep **one** rig/graph driver (ModCharacter + CharPlan generalised) and make the player a client of it through a PIA. NPCs (ApBody), Patrols riders and Quests followers feed the same driver through Core ModAPI.

Player-only today, must become shared (player + human NPC + female NPC + animals):

| Capability | Player-only code today | Parallel copies elsewhere | Shared target in Pres |
|---|---|---|---|
| Character model build (clone rig, strip, skin glb onto 22-bone skeleton, FP mesh variants) | Body.Create (Body.cs:211-322), Model.cs, CharacterModels.cs, Gltf.cs, Bindposes.cs | NPC/Women/Gungirl.cs:382-420 (+ its own Gltf/Bindposes/Json copies) | `CharacterModel` factory keyed by character identity (Male, Female, Merchant ... Gungirl, Shoota) for player and NPCs |
| Locomotion/hands/actions decision | Loco.cs (Body partial), Body.cs melee/throw/strike timing | CharPlan.cs + LocoPlan.cs (NPC), Patrols/Crews/Rider.cs own graph (Rider.cs:376-460) | CharPlan as the single model, inputs from PIA / NPCAI / Patrols |
| Upper-body rig copy | Loco.BuildUpperRig | ModCharacter.BuildRig | one `UpperRig` builder (template per avatar, see §6) |
| Weapon in hand + poses + aim lift | Props.cs, GunPose.cs, AimLift.cs, Body.PoseProp | ModCharacter.GrabWeapon/PoseWeapon already reuse GunPose/AimLift | MOVE as is (already shared via service) |
| Visible equipment on body | Equipment.cs, EquipmentInventory.cs | none (NPC followers have none) | Equipment mounts driven by an inventory snapshot (ids), any character |
| Climb/ladder animation | PlayerTraversal.cs (+ foot-on-rung IK, eye follow) | NpcTraversal.cs (separate lease, no foot IK) | one `TraversalLease(characterId, kind)` |
| Crawl/prone | CrawlBody.cs + Crawling.Clip | none | crawl clip slot in CharPlan |
| Seated/vehicle body pose | CarSeat.cs, Body.LateCar*/CarBase/LearnCarThrow, WheelArms.cs | Patrols/Crews/Pose.cs (412 lines), BusPassengerPose.cs, Rider.cs seat graph, Quests `Hire.LateUpdate` seat pose | `SeatPose` service: seat anchor + profile (driver/passenger/turret/bike), arm IK onto wheel/bars |
| Ragdoll | Ragdoll.cs (takes `Game.Player` colliders) | none (NPCs use native `_Dead` prefabs) | generic ragdoll(bones, ignore set) |
| Character voice (pain/death/shout) | FemalePain.cs | NPC/Women/Voice.cs | character audio profile |
| FP weapon motion | AimTransition.cs, FirstPersonSway.cs | Guns/Camera/WeaponTransition.cs (raise/lower) | one FP weapon-motion component |
| Animals | none | NPC/Animals/DogAnimations.cs (695 lines, Generic clips, own blending/IK/culling) | Pres "quadruped driver"; NPCAI keeps dog behaviour (jump/bite decisions) |

## 1. File map

| path (src/) | lines | what it does | target | action |
|---|---|---|---|---|
| Modules/PlayerBody/PlayerBodyModule.cs | 417 | Module entry: F6/F7 binds + migration chains, Init/Shutdown/scene/origin, patches, jobs, sidecar copy | Pres (+Vehicles, HUD, Developer, CamSvc) | SPLIT: Character, Body/Arms/FemaleArms/ReplaceDriver/EquipmentOnBody/PainSounds, sway/ADS, Climbing*/Ladder*, Anim/Leg/Model entries → Pres. ThirdPerson*/Orbit/EnableMMB/DynamicCrosshair/PickAssist*/Occlusion*/ObserveKey → CamSvc owner. VehicleStatusHint/VehicleHotkeyHint → HUD. IgnitionControlOn, Cassette/Volume keys, `LegacyIgnitionKey`, `IgnitionKeyCode` → Vehicles. LanceIgnoresOwnCar → Gunplay. WeaponAdjust + `GunPose.BindEditorKeys` → Developer. VerboseModApi → DELETE (Debug level 2). `Info`/`Verbose` (identical, l.84-85) → Debug. `MigrateKey`/`KeyMap` → Core. `Mig*`/`ClimbEntry`/`Slider` migration chains: keep with the owner (first-run migrations). |
| Modules/PlayerBody/PlayerBodyPages.cs | 186 | F7 pages: Animation framework, Body & model, Camera, Climbing, Weapon pose editor, Steering-wheel hands | Pres, CamSvc, Developer | SPLIT: Framework/BodyPage/ClimbingPage → Pres. CameraPage → CamSvc owner. PoseEditor/ArmButtons and the `WheelArms.Draw` page → Developer. |
| Modules/PlayerBody/BodyRunner.cs | 229 | Job methods: build/rebuild player Body every 2 s, view-mode routing (foot/car/FP/TP/dead), ModAPI tick, GUI, end-of-frame camera reset | Pres | SPLIT: Tick/LateTick/WheelTick/ModApiTick → Pres player driver. `Car.Tick` call (l.110) → Vehicles. Gui (l.212-213) → Developer + HUD. `EndOfFrameLoop` (l.216) → CamSvc. `Perf.*` calls → DELETE. `TryPlayerJumpHead`/`TryOcclusionPoints`/`DeathFocus` → Pres ModAPI queries. |
| Modules/PlayerBody/Game.cs | 498 | Cached player refs and FSM state: Ready, Dead, InCar, MoveState, Velocity, DrawnWeapon, reload/ADS/binocular/kick/jump/grenade, CarRoot | Core + Pres PIA | SPLIT: Ready/Find/Dead/InCar/Velocity/MoveState/StandingHeight/CarRoot/JumpState → MERGE-INTO Core.GameRefs as a typed `PlayerState` (Guns, NPC, HUD and Quests read the same FSMs). DrawnWeapon, IsReloading, ArmsReloading, ArmsReloadPhase, ReloadsPerRound, Attack, KickState, GrenadeState, GameArmsShown, AimDownSights, Binoculars, BinocularInUse, ToolInHand → Pres PIA. `CarFsm` (l.440) and `DriveUse` (l.468) have no WW caller: keep only while ChaseCamera reflects `Apocaplayer.Game` (§9), else DELETE. FindDeep/PathOf → Core util. |
| Modules/PlayerBody/GameBindings.cs | 55 | Looks up the game's InsaneSystems key actions by name; KeyDown/Held/Label | Core | MOVE → Core input service; cache the `KeyAction` per InputStorage instance (§6). |
| Modules/PlayerBody/KeyMap.cs | 76 | InputSystem Key ↔ legacy KeyCode table; old cfg KeyCode-name parser for migration | Core | MOVE → Core.ModuleConfig/Hotkeys (AGENTS 5.7 already asks for this). |
| Modules/PlayerBody/WeaponsPackShadows.cs | 48 | Optional Weapons Pack patch: FP shadow-only copies of the pack's held model; suppresses the body's carrier renderers | Compatibility | MOVE. Replace `FieldInfo Body._prop` reflection (l.24, l.40) with a Pres ModAPI query `TryGetHeldProp(characterId)`. |
| Modules/PlayerBody/Animation/AimLift.cs | 249 | Per weapon × aim clip: hand lift, head tilt, forward and arm offsets (aim-lift.txt + built-in); two-bone elbow solver; editor nudges | Pres + Developer | SPLIT: Default/Builtin/Load/Save/SaveIfDirty/Get/Resolve/Key/Own/IsAimClip/scalar `Elbow` (l.196) → Pres. Set/ArmKeys/EndArmKeys/NudgeArm/ResetArm/Edit* → Developer. `Elbow(double[]...)` (l.227) → DELETE (only for tools/locotest, no in-game caller). |
| Modules/PlayerBody/Animation/AimTransition.cs | 83 | iTweenMoveTo.OnEnter prefix: FP gun eases hip ↔ sights over AdsTime | Pres | MERGE-INTO a Pres FP-weapon-motion unit with FirstPersonSway.cs and Guns/Camera/WeaponTransition.cs. |
| Modules/PlayerBody/Animation/AnimationService.cs | 118 | `IAnimationApi` adapter forwarding to static ModAPI; cached `IAnimatedCharacter` per character | Pres (provider) | REWRITE as the Pres ModAPI service: id-based character handles and queries instead of Animator references (§7). |
| Modules/PlayerBody/Animation/Anims.cs | 69 | Loads the Mixamo clip bundle on first `Get`; Strafe/Shoot alias names | Pres | MOVE; load at Init (Sliced/async), not on first `Get` mid-play. |
| Modules/PlayerBody/Animation/Bindposes.cs | 36 | Hard-coded Bodyguard.001 bind poses | Core/Pres | MERGE-INTO one copy. It is byte-identical to NPC/Women/Bindposes.cs apart from the namespace. |
| Modules/PlayerBody/Animation/BuiltinPoses.cs | 811 | Generated weapon\|clip\|pose table | Pres | MOVE. Better: ship it as a data file read at Init, which also removes the C# export round-trip in GunPose.Save. |
| Modules/PlayerBody/Animation/CharPlan.cs | 495 | Unity-free per-frame animation decisions for a ModAPI character | Pres | MOVE. Becomes the **single** decision model, also used for the player. |
| Modules/PlayerBody/Animation/CrawlBody.cs | 65 | Body partial: low-crawl clip mixed into the player graph | Pres | MOVE (generalise per character). Empty `partial void CaptureCrawlMotion()` (l.15, called l.48) → DELETE. |
| Modules/PlayerBody/Animation/Crawling.cs | 92 | Low Crawl: loads the clip; hooks zero strafe, block jump/kick/fire/weapon switch, auto-holster | Pres + player gameplay | SPLIT: Clip/bundle load → Pres. Velocity/Button/HeldButton/WeaponEvent/Stance/Holster/Tick → player-movement owner (§8). `DownedProbe` (l.11, never assigned) and `InstallMultiplayer` (l.38, no body) → DELETE (MP stubs). |
| Modules/PlayerBody/Animation/GunPose.cs | 514 | Weapon-in-hand pose table (user file > built-in > idle > legacy), blending; numpad editor, hint GUI, editor keys, C# export | Pres + Developer | SPLIT: Bind/Parse/Fmt/LoadUser/Own/Dirty/SetPoseList/Put/HasAny/HasPose/Effective(weapon,clip)/EffectiveFor/BlendFor/Blend/Abs/SetLive/Flush and the user-file part of Save → Pres. LoadLegacy/HasLegacy/ConvertLegacy/LegacyWeapons (0.5/0.6 grips) → retire after one more release. Editor: Step/StepClip/Pressed/Keys/Axis/Note/Status/AimLine/ClearHint/OnGUI/BindEditorKeys/EditorKey/EditorAxis/DrawEditorBindings/CopyShown/PasteCurrent/DeleteCurrent/SaveNow/ReloadFiles/Current*/RotateMode/EditorTarget, plus the BuiltinPoses.generated.cs export → Developer. `Poses` (l.27) and `Effective(string,int,float[])` (l.276) → DELETE (no caller; "other mods by reflection" shims). |
| Modules/PlayerBody/Animation/Loco.cs | 823 | Body partial: player Mixamo graph (42 base slots, UpperRig copy, actions, turns, jumps, aim lift, arm pose), driven by game input; aim-lift editor keys | Pres | REWRITE: duplicate of ModCharacter+CharPlan (see the duplication table above). Keep graph/rig building as the shared CharacterRig. `AimKeys` (l.309) → Developer. |
| Modules/PlayerBody/Animation/LocoPlan.cs | 341 | Unity-free slot table, clip stand-ins, blend weights, upper choice, turn/jump curves | Pres | MOVE. |
| Modules/PlayerBody/Animation/ModAPI.cs | 203 | Static public API: Ready, clip list, weapon key/kind/poses/reload facts, Attach/Find/LateTick | Pres | MERGE-INTO the Pres ModAPI service. `AimLiftOf` (l.81) → DELETE (not in IAnimationApi, no caller). `Find` (l.177) is an O(n) scan → dictionary by Animator instance id. |
| Modules/PlayerBody/Animation/ModCharacter.cs | 564 | `ModAPI.Character`: per-NPC PlayableGraph + cloned UpperRig, grab/pose weapon, procedural spine/aim/arm IK | Pres | MOVE as the shared character driver. Fix §6 items: allocation at l.351, rig clone at l.208-260, no LOD. |
| Modules/PlayerBody/Body/Arms.cs | 75 | Re-skins the game's FP arm/kick-leg renderers with the character texture (scan every 3 s) | Pres | MOVE. Dead left-arm branch: `Model.ArmsLeft()` is always null, so `_matLeft` and `IsLeftArm` (l.57) are dead → DELETE. Share the renderer scan with Game.GameArmsShown. |
| Modules/PlayerBody/Body/Body.cs | 1423 | Player body: clone Flexa rig, body/shadow/FP-arms/inside meshes, graph, melee/throw/strike timing from mouse, foot/car/dead/FP views, props, sway, crouch, legacy procedural walk | Pres | SPLIT: Create/FlexaPrefab/Clip/BuildGraph(Mixamo)/meshes/SetVisible/SetMesh/CapMaterial/ShiftFirstPerson/FitFirstPerson/LateDead/Ragdoll/props (UpdateProp/ShowProp/PoseProp/AlignProp/TryWeaponMuzzle)/pose fade → shared CharacterBody. WatchAttack/StartMelee/UpdateMelee/WatchThrow/ThrowAim/PunchAim/BeginStrike and the Input reads in LateFootInner → PIA. LateCar/LateCarInner/CarBase/LearnCarYaw/LearnCarThrow/LateWheelArms → Pres SeatPose. Non-Mixamo fallback (BuildGraph l.358-395, Walk l.1310, MirrorBody/procedural walk branch in LateFootInner l.796-860, the `[Animation (fallback clips)]` entries) → DELETE candidate; it runs only when the shipped bundle is missing. |
| Modules/PlayerBody/Body/CharacterModels.cs | 122 | Native NPC character sources (Merchant, Bodyguard ...) found by a Resources scan; glb skin for Female/Gungirl/Shoota; bakes non-readable native meshes | Pres | MOVE. MERGE the glb-skin path with NPC/Women/Gungirl.cs model building. Today the player's Gungirl/Shoota come from NPC via `Services.CharacterAssets` (l.45); Pres should own those assets. |
| Modules/PlayerBody/Body/CharacterPortraits.cs | 48 | Character names/order/canonical id; loads portrait PNGs (settings, TAB) | Pres + HUD | SPLIT: Names/Order/Canonical → Pres character catalog (ModAPI query). Prepare/Picture (UI textures) → HUD. |
| Modules/PlayerBody/Body/Equipment.cs | 336 | Visible carried equipment on the body: backpack, 2 long guns, 2 sidearms, 2 blades, binoculars, flashlight + glow | Pres | MOVE. `Classify` (l.50) → MERGE-INTO the shared weapon catalog (§5). Feed it from an inventory snapshot so NPC followers can use it too. |
| Modules/PlayerBody/Body/EquipmentInventory.cs | 214 | Reads vanilla weapon slots, or Apocapocket/Apocainventory slots through compiled reflection | Compatibility + Core | SPLIT: Resolve/Bridge (Apocapocket/Apocainventory) → Compatibility, published as a Core ModAPI `PlayerInventorySlots` query. Vanilla Read/FirstItem → Core player-inventory query (CleanHUD reads the same slots). |
| Modules/PlayerBody/Body/FemalePain.cs | 105 | Female pain/death sound swap (PlayRandomSound/SetAudioClip patches); male/female shout clips for NPC Senses | Pres | MOVE (voice follows the character model); `PlayerShout` → character voice query. See §8. |
| Modules/PlayerBody/Body/Gltf.cs | 417 | Unity-free glTF/glb skinned-model reader | Core | MERGE-INTO Core.Assets.Gltf. It duplicates NPC/Women/Gltf.cs; the only difference is the log call at l.317. |
| Modules/PlayerBody/Body/InventoryModel.cs | 72 | Swaps the TAB-screen player picture RawImages to the character PNG | HUD | MOVE. |
| Modules/PlayerBody/Body/Json.cs | 117 | Minimal JSON reader for glTF | Core | MERGE-INTO Core Json. Identical copies: NPC/Women, Towns/GasStation, Patrols/WarRig; Survival/StewPot differs by one blank line. CleanHUD/Json.cs is a different 122-line variant. |
| Modules/PlayerBody/Body/Model.cs | 364 | Body mesh variants (Full/NoHead/NoArms/ArmsOnly/NoHeadRightArm/LeftArmOnly), inside/cap meshes, texture loads | Pres | MOVE. `ArmsLeft` (l.340, "Max only", always null) → DELETE. |
| Modules/PlayerBody/Body/PickAssist.cs | 176 | DoMousePick postfix: TP screen-space pick assist; FP car-shift re-cast; blocks dash parts in a TP car | CamSvc owner | MOVE. Route through a Core Hooks dispatcher: Performance/PlayMaker/MousePickFeature.cs:142 patches the same target. |
| Modules/PlayerBody/Body/Projectiles.cs | 35 | CreateObject postfix: a blast lance thrown from the driver's seat ignores own-car colliders | Gunplay | MOVE. |
| Modules/PlayerBody/Body/Props.cs | 304 | Weapon prop catalog from NPC prefabs/world items, kind classification, instantiate/mirror into hand, muzzle/barrel | Pres + shared catalog | SPLIT: KindOf/KindOfName/Norm → shared weapon catalog (Core ModAPI, data owned by Gunplay). Build/Find/ItemModel/SceneItem/Instantiate/CopyForMount/TryMuzzle/Barrel/MirrorToRight/BindWorld/OnSceneLoaded → Pres. |
| Modules/PlayerBody/Body/Ragdoll.cs | 90 | Jointed capsule ragdoll on mixamo bones at death | Pres | MOVE; pass the ignore-collider set in instead of reading `Game.Player` (l.137). |
| Modules/PlayerBody/Camera/Cave.cs | 41 | Reads the CaveDarkness FSM bool every 0.25 s to pause the cutaway | CamSvc / Core | MOVE (or Core `PlayerState.InCave`; Weather/Survival can reuse it). |
| Modules/PlayerBody/Camera/ChaseCameraCompat.cs | 74 | ChaseCamera 0.2.5 type adapter: maps `Apocaplayer.*` type names to WW types + a config facade | Compatibility | MOVE. Keep the type map (§9). Stop the endless 1 s discovery job after the first post-chainload attempt. |
| Modules/PlayerBody/Camera/FirstPersonJump.cs | 15 | Body partial `TryJumpHead` (animated head while jumping, used by the Guns head hitbox) | Pres | MERGE-INTO CharacterBody (it is misplaced in Camera/). |
| Modules/PlayerBody/Camera/FirstPersonSway.cs | 111 | SmoothLookAt prefix: frame-rate-independent FP weapon sway | Pres | MERGE-INTO FP weapon motion (with AimTransition and Guns WeaponTransition). |
| Modules/PlayerBody/Camera/OcclusionCutaway.cs | 384 | TP cutaway: a second camera view without blockers, composited by a shader | CamSvc owner | MOVE. |
| Modules/PlayerBody/Camera/Perf.cs | 134 | VerboseLog 10 s report: FPS, worst frame, GC, own time per part, cameras, PC/display/mods | Debug | DELETE. Scheduler timing and the F7 Performance page cover it; MERGE the PC/GC/camera census into Debug telemetry. |
| Modules/PlayerBody/Camera/ThirdPerson.cs | 563 | TP camera (onPreCull matrices), orbit, wheel zoom, aim zoom, dynamic crosshair, FP view-model hiding, car camera key, Jump→climb hook | CamSvc owner + HUD + player traversal | SPLIT: Tick/Zoom/ComputeView/PreCull/EndOfFrame/HideViewModel/ShowViewModel/Off/CarShiftEye → CamSvc owner. BtnJump branch of `BeforeGetButtonDown` (l.118-121) → player traversal. MoveCrosshair/FindCursor/RestoreCrosshair (l.450-504) → HUD crosshair service (duplicates HUD/Crosshair/CrosshairController.cs:71-142 lookups). LateCrosshair eye/muzzle rays (l.390-430) → shared aim-ray query (Gunplay). |
| Modules/PlayerBody/Climbing/ClimbGeometry.cs | 191 | Player-capsule ledge detection, clearance, climb path | Player traversal (§8) | MOVE; candidate to share with NPC/PoiNav/PoiNavBake climb-link detection. |
| Modules/PlayerBody/Climbing/Climbing.cs | 235 | ClimbingController: jump/key-triggered player ledge climb, kinematic path, FSM suspend, FP weapon hide | Player traversal + Pres lease | MOVE. |
| Modules/PlayerBody/Climbing/Ladders.cs | 396 | LadderController: takes over the UseLadder FSM, rung snap, slide, mantle; writes `Services.PlayerLadder`/`Side`/`Slide*` | Player traversal + Pres | MOVE. Replace the raw Services statics with ModAPI query/events (§7). |
| Modules/PlayerBody/Climbing/NpcTraversal.cs | 110 | `INpcTraversalAnimation`: ladder PoseClip or climb clips on NPC Animators; suspends the ModAPI character | Pres | MERGE-INTO one traversal lease with PlayerTraversal. |
| Modules/PlayerBody/Climbing/PlayerTraversal.cs | 227 | Player animation lease (clip/PoseClip), eye follow, foot-on-rung IK; ModAPI player-traversal queries | Pres | MERGE-INTO the unified traversal lease (foot IK then works for NPCs as well). |
| Modules/PlayerBody/Climbing/PoseClip.cs | 168 | Loads a baked .wwpose humanoid muscle animation; Blend via HumanPoseHandler | Pres | MOVE; load at Init (Sliced). Today it builds an Avatar synchronously on first ladder use. |
| Modules/PlayerBody/Vehicles/Car.cs | 379 | Cockpit facing reset, ignition hotkey → panel MousePickEvent, cassette/volume/headlight keys, engine state via reflection, hint text + OnGUI | Vehicles + HUD | SPLIT: Tick (cockpit reset, controls, cassette/volume), TickIgnition, BeforeMousePick, Patch, FindControls/Part, FindEngine/Member/EngineRunning, SetVolume/RadioVolume → Vehicles. UpdateHints/AddHint/AddActionHint/BindingSig/FindGameText/KeyName/OnGUI/GuiWanted → HUD, reading a `VehicleControlsState` query. |
| Modules/PlayerBody/Vehicles/CarSeat.cs | 121 | Finds the car's PlayerModel_Sit, hides the game driver, retargets its pose onto the mixamo body | Pres | MOVE (SeatPose; unify with Patrols/Crews/Pose.cs). `FindSource` (l.98) has no caller → DELETE. |
| Modules/PlayerBody/Vehicles/IgnitionControl.cs | 153 | Injects an "Ignition" row into the game Controls screen; persists ignition-key.txt | Vehicles | MOVE. Row injection goes through the Core input service. |
| Modules/PlayerBody/Vehicles/VehicleHud.cs | 79 | IMGUI ignition/handbrake/cassette icons, else CleanHUD's `IVehicleStatusDisplay` | HUD | MOVE. |
| Modules/PlayerBody/Vehicles/WheelArms.cs | 453 | Arm IK on steering wheel/handlebars, passenger arm rest, per-wheel grip setups; Steering prefix zeroes input when the wheel is missing; forces returnToCenter | Pres + Vehicles + Developer | SPLIT: Bind/Solve/SolveHandlebars/RestPassengerArms/Reach/Load/LoadPath/FittedWheel → Pres SeatPose. `Patch`/`BeforeSteer` and `_steering.returnToCenter = true` (l.102), which are gameplay changes, plus `MissingWheel` → Vehicles. `Draw` page and `Save` → Developer. |
| Modules/PlayerBody/Vehicles/WheelGripEditor.cs | 242 | Live numpad steering-wheel grip editor: mirror/copy/paste, hint | Developer | MOVE. |

## 2. Cross-module coupling

### 2.1 Used by PlayerBody

| file:line | used | why |
|---|---|---|
| Body/CharacterModels.cs:45 | `Services.CharacterAssets.TryPaths` (provided by NPC/Women/WomenFeature.cs:15) | Gungirl/Shoota player models live in NPC. The direction is inverted: Pres should own the character assets. |
| Vehicles/Car.cs:102 | `Services.VehicleIgnition.TryPlayer` (EngineSimCompat) | ignition via Engine Sim |
| Vehicles/VehicleHud.cs:27 | `Services.VehicleStatus.TryShow` (CleanHUD) | status icons |
| Camera/ThirdPerson.cs:203 | `Services.Map.Open` (CleanHUD/QuestMap) | mouse-wheel arbitration, which belongs in CamSvc |
| Camera/ThirdPerson.cs:397-424 | `Services.Guns.PlayerProjectilesEnabled`, `BlocksProjectile` | crosshair muzzle ray |
| Camera/ThirdPerson.cs:399 | `Services.Animation.TryPlayerWeaponMuzzle` | calls its own service through Core |
| Climbing/Ladders.cs:78,174,365 | writes `Services.LadderSlidePhase/LadderSlideSpeed/PlayerLadderSide/PlayerLadder` (raw static fields, Services.cs:19-23) | read by NPC/PoiNav/PoiNav.cs:209-216 and PoiNavTraverse.cs:256,331,352,371,377 |
| Climbing/Ladders.cs:328 | reads `Services.LadderClimbers` (written by NPC PoiNavTraverse.cs:195-206) | player ladder blocked by NPC climbers |
| Game.cs:44,55; Body/PickAssist.cs:45; WeaponsPackShadows.cs:33 | `GameRefs.Player/PlayerCameraHolder/MainCamera/InGame` | refs |
| Animation/Crawling.cs:29-34 | `Hooks.SetVelocityDo/GetButtonDownUpdate/GetButtonDo/ProcessEvent/EnterState`, `SaveBus.Busy`, `FsmUtil`, `DiskResources` | low crawl |
| Vehicles/WheelArms.cs, Vehicles/WheelGripEditor.cs | `SaveBus.Busy`, `ToolWindow.CloseThen/IsOpen`, `ApocasetterBridge.InputBlocked` | editor |
| Animation/GunPose.cs, Climbing/Ladders.cs, Vehicles/Car.cs | `ApocasetterBridge.InputBlocked` | input gate |
| Body/FemalePain.cs:27,39,48 | `Core.VoiceAudio.LoadWav` | wav decode (already consolidated) |
| PlayerBodyModule.cs:293 | `Core.TunedSettings.InstallProfiles("PlayerBody")` | SP tuned defaults |
| PlayerBodyModule.cs:336 | `Hotkeys.Register(..., NoOp, Never)` | registers context-polled keys only for conflict reporting |
| Body/CharacterPortraits.cs:226, Body/Model.cs:325, Animation/Crawling.cs:25, Climbing/PoseClip.cs:171 | `DiskResources` | runtime assets |

### 2.2 Provided by PlayerBody

| member | signature | set at |
|---|---|---|
| `Services.Animation` | `IAnimationApi`: `int ApiVersion; string Version; bool Ready; bool PlayerThirdPerson; AudioClip PlayerShout; bool TryPlayerJumpHead(GameObject, out Vector3); bool TryPlayerWeaponMuzzle(out Vector3); string[] ClipNames(); bool IsPlayerClip(string); AnimationClip GetClip(string); string WeaponKey(string); string WeaponKind(string); bool HasWeaponPoses(string); bool WeaponPose(string,string,out Vector3,out Quaternion); string ReloadClip(string); float ReloadClipSeconds(string); float RoundSeconds(string); bool ReloadsOneRoundAtATime(string); bool CocksAfterShot(string); float NativeSpeed(string); int Count; IAnimatedCharacter Attach(Animator); IAnimatedCharacter Find(Animator); INpcTraversalAnimation BeginNpcTraversal(Animator,string); float LadderCycleRise; float LadderToeDepth` | PlayerBodyModule.cs:338, cleared at :357 |
| `IAnimatedCharacter` | `Animator, Valid, WeaponKey, WeaponKind, Reloading, Pumping, HandsClip, ActionClip, UpperClip, SpeedCap, ManualVelocity{get;set}, Velocity{get;set}, Crouched, Aiming, Firing, Airborne, AimPitch, TurnInPlace, WalkToStop, Suspended {get;set}; SetWeapon(Transform[,string]); Shoot([float]); Reload(float[,int]); CancelReload(); Pump(); Jump(); Kick(); Throw([bool]); Strike([float]); Dispose()` | AnimationService.cs:71-116 |
| `INpcTraversalAnimation` | `bool Valid; float Phase{get;set}; float Length; void Dispose()` | NpcTraversal.cs:15 |
| `Services.PlayerLadder`, `PlayerLadderSide`, `LadderSlidePhase`, `LadderSlideSpeed` | raw public statics, not an interface | Ladders.cs |

Consumers of `Services.Animation`:
- **NPC:** Brain/ApBody.cs, Detection/Senses.cs:1286, PoiNav/PoiNavTraverse.cs:109-134,262,303.
- **Guns:** EmptyGunSound.cs:24, Tracers/Tracers.cs:335,1387, Camera/Ironsights.cs:38.
- **Patrols:** Crews/PlayerAnims.cs, Crews/Rider.cs:190.
- **Quests:** QuestFollower.cs:30-37.

### 2.3 Rule violations and hidden couplings

- **Core → module type references** (the other direction from the rule, still a violation):
  - Core/UI/CorePages.cs:5 aliases `PlayerBodyModule`.
  - CorePages.cs:75 calls `InventoryModel.PrepareSettingsPictures()`.
  - CorePages.cs:91-101, 140, 148, 240-243 and 291 read/write PlayerBody `ConfigEntry` statics: BodyFirstPerson, FullBodyOnlyOnFoot, EmptyHandArms, FirstPersonArmsOnlyOnFoot, ClimbingOn, ThirdPersonOnFoot, OcclusionPrototype, VehicleHotkeyHint, VehicleStatusHint, WeaponAdjust, Character.
  - CorePages.cs:181-191 calls `CharacterPortraits.Order/Picture`.
  - Fix: a Core settings registry (modules publish "Game page" entries) plus a HUD portrait query.
- **No PlayerBody → other-module type reference** was found; all such traffic goes through Services.
- **Third-party reflection into PlayerBody internals:**
  - ChaseCamera resolves `Apocaplayer.ThirdPerson/Game/Plugin/OcclusionCutaway/Cave` to WW types (ChaseCameraCompat.cs:51-62) and then binds their members by name.
  - Weapons Pack's `UpdateThirdPerson(object, object[])` receives a `Body` instance (WeaponsPackShadows.cs:30-31; `_prop` field read at l.24/40).
  - Renaming or moving these classes or members breaks both bridges (§9).
- **Services ladder statics** are module-to-module shared state without an interface (PlayerBody writes, NPC reads; NPC writes `LadderClimbers`, PlayerBody reads).

## 3. Harmony patches & scheduler jobs

### 3.1 Patches

| file:line | target | kind | via | per-call cost / filter |
|---|---|---|---|---|
| PlayerBodyModule.cs:307 | `GetButtonDown.OnUpdate` | prefix (skip) | Hooks | `ThirdPerson.BeforeGetButtonDown`: reference-keyed FSM classification (ThirdPerson.cs:94-127). The cache is never pruned until scene load. |
| Animation/Crawling.cs:30 | `GetButtonDown.OnUpdate` | prefix (skip) | Hooks | `Crawling.Button`: first check is `!Active` = Running + `Game.InCar` (ActiveStateName compare) + Dead + `MoveState == "Crawling"` on **every** GetButtonDown of every FSM, every frame (§6). |
| Animation/Crawling.cs:31 | `GetButton.DoGetButton` | prefix (skip) | Hooks | `HeldButton`: same `!Active` cost on every GetButton. |
| Animation/Crawling.cs:32 | `Fsm.ProcessEvent` | prefix | Hooks | `WeaponEvent`: `!Active` on **every FSM event in the game** (§6). |
| Animation/Crawling.cs:33 | `Fsm.EnterState` | postfix | Hooks | `Stance`: cached Movement FSM reference first. Cheap. |
| Animation/Crawling.cs:29 | `SetVelocity.DoSetVelocity` | prefix | Hooks | `Velocity`: reference compare first. Cheap. |
| PlayerBodyModule.cs:308 | `ActivateGameObject.DoActivateGameObject` | prefix (skip) | Hooks | `ThirdPerson.BeforeActivateGameObject`: static `AimZoom`. Cheap. |
| PlayerBodyModule.cs:309 | `CreateObject.OnEnter` | postfix | Hooks | `Projectiles.AfterCreateObject`: config bool, Running, `Game.InCar`, `Fsm.Name == "Attack"`, then `StartsWith("blastlance")`. Per spawn. |
| PlayerBodyModule.cs:310 | `SmoothLookAt.DoSmoothLookAt` | prefix (skip) | Hooks | `FirstPersonSway.BeforeSmoothLookAt`: dictionary by action. The dictionary grows with every NPC SmoothLookAt action and is swept only on scene load (FirstPersonSway.cs:66,102). |
| PlayerBodyModule.cs:311 | `MouseLook.OnUpdate` | prefix (skip) | Patches | `return !Orbiting`. Cheap. Quests patches a sibling target, `MouseLook.DoMouseLook` (harmony.md l.224). |
| Animation/AimTransition.cs:26 | `iTweenMoveTo.OnEnter` | prefix | Patches | `Fsm.Name != "AimDownSights"` first; FieldInfo Get/SetValue per ADS press (low frequency). |
| Body/PickAssist.cs:29 | `ActionHelpers.DoMousePick(float,int)` | postfix | Patches | **Shared target**: Performance/PlayMaker/MousePickFeature.cs:142 has its own `[HarmonyPatch]` prefix. Should become a Hooks dispatcher. `FieldInfo.GetValue/SetValue` boxes a RaycastHit (l.40,42,53,57,60). |
| Body/FemalePain.cs:57 | `PlayRandomSound.OnEnter` | prefix + finalizer | Patches | statics first (config, Female), then FSM name. Per sound. |
| Body/FemalePain.cs:60 | `SetAudioClip.OnEnter` | prefix + finalizer | Patches | same, Health/playerDeath. |
| Vehicles/Car.cs:95 | `MousePickEvent.DoMousePickEvent` | prefix (skip) | Patches | `!_ignitionPress` static first. Cheap. |
| Vehicles/WheelArms.cs:64 | `NWH Steering.CalculateSteerAngles` | prefix | Patches | reference compare against the cached local Steering. **Gameplay** patch (zeroes steering input). Belongs to Vehicles. |
| WeaponsPackShadows.cs:26 | `ChainsawPrototype.Plugin.UpdateThirdPerson(object,object[])` | prefix + postfix | Patches (optional) | postfix uses `prop.GetValue(body)` reflection on every call (l.40) and allocates `GetComponentsInChildren` on carrier change. |
| Camera/ChaseCameraCompat.cs:41 | `ApocaChaseCamera.ApocaplayerBridge.FindType` | prefix | Patches (optional) | discovery only. |
| Camera/ThirdPerson.cs:189 | `Camera.onPreCull += PreCull` | non-Harmony callback | – | runs for every camera's pre-cull while hooked. |
| Camera/OcclusionCutaway.cs:90 | `AddComponent<OcclusionCutaway>` on the main camera (OnRenderImage l.350) | MonoBehaviour | – | extra full render per frame while the cutaway is on. Untimed by the Scheduler. |

### 3.2 Scheduler jobs

| name | kind | interval/budget | file |
|---|---|---|---|
| Ignition control | Interval | 0.5 s | PlayerBodyModule.cs:322 → IgnitionControl.Tick |
| Climbing | EveryFrame, EarlyUpdate | – | :323 |
| Climbing (physics) | EveryFrame, EarlyFixedUpdate | – | :324 |
| Ladders | EveryFrame, EarlyUpdate | – | :325 |
| Ladders (physics) | EveryFrame, EarlyFixedUpdate | – | :326 |
| Body update | EveryFrame, Update | – | :327 (also runs Car.Tick, ThirdPerson.Tick, WheelArms.Tick, Arms.Tick, Perf.Frame) |
| Body late | EveryFrame, LateUpdate | – | :328 |
| ModAPI characters | EveryFrame, LateUpdate | – | :329, all NPC characters, no LOD |
| PlayerBody.WheelArms | EveryFrame, LateUpdateLast | – | :330 |
| NPC traversal poses | EveryFrame, LateUpdateLast | – | :331 |
| Weapon pose hint + vehicle HUD | Gui (predicate `BodyRunner.GuiWanted`) | – | :332. The predicate is true whenever driving and not paused (Car.cs:352), so IMGUI runs every driving frame even when CleanHUD draws the status and hints are off. |
| PlayerBody.Crawling | EveryFrame, EarlyUpdate | – | Animation/Crawling.cs:34 |
| ChaseCamera discovery | Interval | 1 s, never removed | Camera/ChaseCameraCompat.cs:24 |
| (coroutine) EndOfFrameLoop | WaitForEndOfFrame loop | every frame, **not Scheduler-timed** | PlayerBodyModule.cs:347 |
| (coroutine) FemalePain.LoadClips | one-shot | 1 frame after Init | PlayerBodyModule.cs:304 |

Job names other than `PlayerBody.WheelArms` and `PlayerBody.Crawling` break the `<Id>.<Thing>` naming convention (AGENTS 5.4).

## 4. Logging

| switch / helper | where | call sites | proposed level |
|---|---|---|---|
| `[PlayerBody / ModAPI] VerboseModApi` (tools.cfg) | PlayerBodyModule.cs:240; ModCharacter.cs:392 (per-frame CharPlan.Log for every character) | 1 use (+ F7 page entry) | DELETE the switch; per-frame character decisions → **level 2** |
| `PlayerBodyModule.Info(s)` | PlayerBodyModule.cs:84 (gated by Core VerboseLog; identical to `Verbose`) | 34: Anims 2, GunPose 2, Model 2, Climbing 3, Ladders 4, Perf 4, ThirdPerson 2, WheelGripEditor 2, BodyRunner 2, others 1 | load/ready summaries (bundle, model, poses, ladder/climb finished) → **1**; Perf/ThirdPerson zoom diagnostics → **2** |
| `PlayerBodyModule.Verbose(s)` | :85 | 63: Body 14, WheelArms 7, Loco 5, Car 5, Props 4, FemalePain 4, IgnitionControl 4, Equipment 3, others | view-mode changes, car found, character change, controls row added → **1**; props/equipment/sway/pick/aim/wheel traces → **2** |
| `Warn`/`Error` | :86-88 | 34 / 23 | always (Core rate-limit) |
| `Core.Log.Always` | PlayerBodyModule.cs:339,401; Crawling.cs:36; WeaponsPackShadows.cs:26; ChaseCameraCompat.cs:46 | 5+4 | startup/migration → keep Always |
| `PlayerBodyModule.VerboseOn` direct checks | 14 sites (Perf.Frame, WheelArms trace l.206, Climbing/Ladders) | – | → `Log.Level >= 2` |
| Perf.cs 10 s report | Perf.cs:44-64, 105-132 | – | MERGE-INTO Debug telemetry (**2**) |
| WheelArms input trace (`_traceInput`, 2 s window) | WheelArms.cs:48-49,205-208 | – | **2** |

There is no own `ManualLogSource` and no `Debug.Log`.

## 5. Redundancy & dead code

**Duplicate utilities:**
- Json.cs, Gltf.cs and Animation/Bindposes.cs equal NPC/Women/{Json,Gltf,Bindposes}.cs. Gltf differs in one log line (l.317), the others only by namespace.
- Json.cs also equals Towns/GasStation/Json.cs, Patrols/WarRig/Json.cs and Survival/StewPot/Json.cs (one blank line).
- Two near-identical model build paths: Model.Build/CharacterSkin (Model.cs:29-103, CharacterModels.cs:41-69) vs NPC/Women/Gungirl.cs:382-420.

**Two systems doing one job:**
- Player animation (Loco.cs/Body.cs) vs NPC animation (ModCharacter.cs + CharPlan.cs); see the table at the top.
- UpperRig clone: Loco.cs:117 and ModCharacter.cs:208.
- Upper avatar mask: Body.cs:1141 and ModCharacter.cs:192; Body.BuildGraph l.377 builds a third.
- Traversal leases: PlayerTraversal vs NpcTraversal.
- Seated poses: CarSeat + WheelArms vs Patrols/Crews/Pose.cs + BusPassengerPose.cs + Rider.cs seat graph.
- FP weapon motion: AimTransition + FirstPersonSway vs Guns/Camera/WeaponTransition.cs.
- PlayableGraph builders for player clips: Body, ModCharacter, NpcTraversal, PlayerTraversal, Patrols Rider.cs:453, NPC DogAnimations.

**Weapon-name classifiers (5+):**
- Props.KindOfName (Props.cs:32)
- Equipment.Classify (Equipment.cs:50)
- ModAPI.FsmFlag name fallback (ModAPI.cs:124-127)
- NPC/Brain/ApBody.cs:57
- NPC/Detection/WeaponRanges.cs:45
- Guns/Tracers/Tracers.cs:748

All should become one Core ModAPI weapon catalog (data owned by Gunplay).

**Duplicated scans:**
- The camera holder's SkinnedMeshRenderers are scanned by Game.GameArmsShown (Game.cs:254, every 1 s) and by Arms.Tick (Arms.cs:28, every 3 s).
- The HUD cursor is looked up by ThirdPerson.FindCursor (ThirdPerson.cs:491-497: GameObject.Find ×4 + Resources RectTransform scan) and by HUD/Crosshair/CrosshairController.cs:71-142.
- The eye ray is cast per frame by ThirdPerson.LateCrosshair (l.403/419), per shot by Guns Tracers, and for picks by Performance MousePickFeature.
- Body graphs and NPC graphs each load and measure clip phases separately (Loco `_phaseOf` vs ModCharacter static `_phases`).

**Dead or unused code:**
- `GunPose.Poses` (GunPose.cs:27) and `GunPose.Effective(string,int,float[])` (l.276): no callers.
- `ModAPI.AimLiftOf` (ModAPI.cs:81): no caller, not in the interface.
- `AimLift.Elbow(double[]...)` (AimLift.cs:227): test-only.
- `CarSeat.FindSource` (CarSeat.cs:98): no caller.
- `Model.ArmsLeft` (Model.cs:340) is always null, which makes `Arms._matLeft` and `Arms.IsLeftArm` (Arms.cs:38-41,57) dead.
- MP stubs: `Crawling.DownedProbe` (Crawling.cs:11, never assigned, so `Downed` is always false and the branches at l.13,44,78 are dead), `InstallMultiplayer` (l.38) and `CaptureCrawlMotion` (CrawlBody.cs:15). Delete them or replace with a ModAPI seam.
- `Game.CarFsm`/`DriveUse` (Game.cs:440,468): no WW caller (possibly ChaseCamera, §9).
- Unused public ModAPI surface: `ModAPI.PlayerRoot`/`BeginPlayerTraversal` are public but only called internally.
- The non-Mixamo fallback graph and procedural walk (Body.cs:351-395, 1310-1360, LateFootInner l.796-860) plus 11 `[Animation (fallback clips)]` tools.cfg entries are only reached when the shipped bundle is missing. Retire them.

**Legacy migrations:**
- Retire after one more release (user data, warn once): GunPose 0.5/0.6 grip conversion (`LoadLegacy`/`ConvertLegacy`, GunPose.cs:88-188; files weapon-grips.cfg, legacy-base-lines.txt).
- `LadderSpeed` old-default bump (PlayerBodyModule.cs:193): retire.
- WheelArms 12/60/84-field grip formats (WheelArms.cs:387): retire after one release.
- KEEP: Apocaplayer/Apocaclimber cfg migration chains (`Mig`, `ClimbEntry`, `Slider`, `MigKey`), `MigrateSidecars` and `LegacyIgnitionKey`. They are first-run only and stay at least one release.
- Pistoleer/Mechanic → Bodyguard/Merchant canonicalisation (CharacterPortraits.cs:10; PlayerBodyModule.cs:103,243) is a config value migration: keep.

**Other redundancy:**
- `Hotkeys.Register(..., NoOp, Never)` (PlayerBodyModule.cs:336) exists only for conflict reports. The keys are polled again through `KeyMap.Code` + legacy `Input`. Core input should own both.
- `Info` and `Verbose` (PlayerBodyModule.cs:84-85) are the same function.

## 6. Performance hot spots

| file:line | issue | fix |
|---|---|---|
| Animation/Crawling.cs:13,64,75,84 | `Active` is evaluated as the **first** check of hooks on every GetButtonDown, GetButton and **every FSM ProcessEvent**. It does `Game.InCar` (ActiveStateName + string compare) and a `MoveState` string compare. | Compute a `static bool ActiveNow` once per frame in `Crawling.Tick` (EarlyUpdate) and check it first. |
| Animation/ModCharacter.cs:351 | `new Dictionary<string,float>()` per frame per character holding a weapon with no pose weights | static reusable dictionary |
| Animation/ModCharacter.cs:208-260 | per Attach: `Instantiate` of the whole NPC object, then three passes of `GetComponentsInChildren<Component>` + `DestroyImmediate`; also `Enum.GetValues` (l.119) | build the UpperRig from bones only (or one stripped template per avatar), pooled |
| BodyRunner.cs:329 / ModAPI.cs:188 | every attached character evaluates graph + rig + IK every frame; culling forced to AlwaysAnimate (ModCharacter.cs:176,238), no distance/visibility LOD | LOD: far or offscreen → skip rig/IK, lower rate |
| ModAPI.cs:177 | `Find(animator)` is a linear scan (callers probe per NPC) | dictionary by instance id |
| Body/PickAssist.cs:40,42,53,57,60 | `FieldInfo.GetValue/SetValue` on `mousePickInfo` (boxing RaycastHit) every pick frame; OverlapSphere(128) + `GetComponent<PlayMakerFSM>` per collider in TP | compiled static-field getter/setter (Expression), cache `Usable` per collider id |
| WeaponsPackShadows.cs:40 | `prop.GetValue(body)` reflection on every pack UpdateThirdPerson (per frame in FP) | internal accessor via ModAPI |
| Vehicles/Car.cs:288 | `PropertyInfo.GetValue` (boxing) on NWH engine `IsRunning` per frame while driving | compiled delegate, or read `VehicleController.powertrain.engine.IsRunning` directly (NWH is referenced already in WheelArms) |
| GameBindings.cs:10-21,38-45; Climbing.cs:144; Car.cs:105-109,145-150 | `Find(name)` linearly scans InputStorage with string compares; called every frame idle (Jump check) and several times per frame while driving (IgnitionAction tries 3 names) | cache the KeyAction per storage instance and reset on scene load |
| Game.cs:130 | `StandingHeight` does `GetFsmFloat("standingHeight")` per call; about 12 call sites per frame in Body/Ladders/Traversal | cache the FsmFloat like `_healthVar` |
| Body/Props.cs:55,74-76,113,129 | `Build()` scans `Resources.FindObjectsOfTypeAll<Transform>()` and **re-runs on every Find while the catalog is empty**; ItemModel/SceneItem scan all GameObjects (retried every 2 s for a model-less weapon, Body.UpdateProp) | build once per scene (Sliced); cache misses |
| Body/Body.cs:328, CharacterModels.cs:25 | `FlexaPrefab` runs `FindObjectsOfTypeAll<GameObject>` every 2 s while Flexa is missing (body rebuild loop); CharacterModels runs `FindObjectsOfTypeAll<Animator>` every 5 s while a source is missing | one Sliced prefab index in Core shared with NPC/Patrols (they also scan prefabs) |
| Body/Body.cs:341,344 | `Clip()` does two `FindObjectsOfTypeAll<AnimationClip>` per missing clip (cached, including null) | prefab/clip index |
| Body/InventoryModel.cs:35,42 | `FindObjectsOfTypeAll<Texture2D>` and `<RawImage>` every 2 s for up to 30 s after each scene | HUD owns the TAB widget reference |
| Camera/ThirdPerson.cs:491-497 | `GameObject.Find` ×4 + `FindObjectsOfTypeAll<RectTransform>` once per second while the crosshair is missing | HUD crosshair service |
| Camera/ThirdPerson.cs:403,419 | 150 m eye RaycastNonAlloc + muzzle ray every frame in TP with the dynamic crosshair | share with Guns aim ray / Ironsights |
| Camera/OcclusionCutaway.cs:175-218 | per new blocker: `GetComponentsInChildren<Renderer>`, `new List`, `ToArray`; RoofOrVehicle does ToLowerInvariant + `GetComponents<Component>` + `GetType().Name` per ancestor | cache per root id; type check via `is VehicleController` |
| Camera/OcclusionCutaway.cs:350 | second full scene render per frame (off by default) | measure, document cost |
| Body/Body.cs:211-322, Model.cs:29-103, Anims.cs:38, PoseClip.cs:164 | synchronous heavy work in play: rig clone, glb parse + mesh build, texture decode, bundle LoadAllAssets, Avatar build, on first body/first character change/first ladder | do it at Init or Sliced (AGENTS 5.4) |
| Body/Arms.cs:28; Game.cs:254 | GetComponentsInChildren of the camera holder twice (1 s and 3 s) | one scan |
| Camera/FirstPersonSway.cs:66; ThirdPerson.cs:94 | caches keyed by action/FSM for all FSMs incl. NPCs, pruned only on scene load | sweep on interval, or reference-compare against the player camera's FSMs first |
| Camera/Perf.cs:99 | `FindObjectsOfTypeAll<Camera>` each 10 s window while MapCheat is absent (VerboseLog) | delete with Perf.cs |
| Vehicles/Car.cs:352 | Gui predicate keeps IMGUI on every driving frame | predicate = hint text non-empty OR the IMGUI status fallback is actually drawing |
| Animation/Loco.cs:334-341 | `GetComponentsInChildren` + string concat per frame (aim-lift editor only) | Developer-only, acceptable |
| Projectiles.cs:95-101 | per blast-lance spawn in car: `GetComponentsInChildren<Collider>` ×3 | per spawn, rare; OK |

## 7. ModAPI / multiplayer events

These must use broadcastable data (ids, positions, enums), never Unity object references. "charId" = the Core-assigned character id: the player = local player id; NPCs = NPCAI ids.

| event / query | publisher → consumer | payload |
|---|---|---|
| `CharacterAttached` / `CharacterDetached` | Pres → NPCAI/Patrols/Quests | charId, rig kind (Human/Female/Dog...), identity string |
| `CharacterInputs` (per tick, state stream) | NPCAI/PIA → Pres (and MP) | charId, pos, yaw, planar vel, flags (crouch, aim, fire, airborne, crawl, seated), aimPitch, weaponKey |
| `CharacterAction` | NPCAI/PIA/Patrols → Pres | charId, action (Shoot/Reload/CancelReload/Pump/Jump/Kick/Throw(L/R)/Strike), seconds, rounds |
| `CharacterWeaponChanged` | Pres | charId, weaponKey, weaponKind |
| `TraversalStarted` / `TraversalEnded` | Pres (player) / NPCAI (NPC) | charId, kind (Ladder/ClimbWaist/ClimbHigh/Crawl), ladderId or ledge positions, result (finished/cancel reason). Replaces `Services.PlayerLadder*` |
| `PlayerLadderState` query | Pres → NPCAI | ladderId (POI-local or scene id), side vector, slide phase/speed |
| `LadderClimbers` query | NPCAI → Pres | feet positions |
| `CharacterDied` / `RagdollSpawned` | Pres | charId, pos, velocity, vehicleId |
| `CharacterModelChanged` | Pres → HUD/Quests | charId, identity (Male, Female, Merchant, ...) |
| `SeatPoseChanged` | Pres/Vehicles | charId, vehicleId, seat (driver/passenger/turret/bike) |
| `CameraModeChanged` + `PlayerView` query | CamSvc → Guns/HUD/Quests | mode (FP/TP/Peek/Orbit/AimZoom), view pos/rot/fov, crosshair screen pos |
| `AimRay` query | Gunplay/CamSvc | eye origin/dir, hit point, muzzle point |
| `PlayerEquipmentChanged` | Core inventory → Pres/HUD | slot → itemKey list, drawn slot |
| `VehicleIgnitionPressed`, `VehicleEngineState` | Vehicles → HUD/MP | vehicleId, keyOn, cranking, running |
| `VehicleControlsState` (cassette, volume, handbrake, headlights) | Vehicles → HUD/MP | vehicleId, playing, volume, handbrakeOn, lightsOn |
| `ProjectileSpawned` (blast lance from a seat) | Gunplay | ownerId, vehicleId to ignore, pos, dir |
| `CharacterVoice` | Pres → audio/MP | charId, cue (pain/death/shout) |

## 8. Unmapped or ambiguous code

| code | options | recommendation |
|---|---|---|
| Camera rig: ThirdPerson, OcclusionCutaway, Cave, PickAssist, CarShift eye, EndOfFrame reset, FP view-model hiding | Presentation vs Core camera service | **Split.** The rig implementation goes to **Presentation**: it is bound to body bones (occlusion points, ragdoll death focus, traversal eye follow, FP body shift). **Core** gets a thin `CameraService` with arbitration (mouse wheel, FOV, worldToCamera owner: Map/QuestMap, Ironsights, ThirdPerson, Quests BlockLook) plus a `PlayerView` state query and events. Guns/HUD/Quests use only Core. |
| FirstPersonSway, AimTransition (and Guns WeaponTransition) | Presentation vs Gunplay | Presentation (visual weapon positioning). Gunplay keeps ironsight *gameplay* (zoom FOV/accuracy). |
| Player ledge climbing / ladders / low-crawl **gameplay** (ClimbGeometry, ClimbingController movement, LadderController movement, Crawling input blocks) | no target row (player movement); NPCAI owns NPC climb logic | Keep in **Presentation** under `Player/Traversal` as the player-control layer, flagged. Alternatively, add a "Player" sub-area of Core. Share ClimbGeometry with NPCAI PoiNavBake through ModAPI only. |
| Projectiles.cs (lance ignores own car) | Gunplay vs Vehicles | Gunplay. |
| FemalePain / shout clips | Presentation vs Quests (voices) vs Survival | Presentation (character audio profile); Quests voices consume it. |
| PickAssist | camera vs HUD vs Core input | Presentation camera sub-area (it depends on the TP view matrix); patch via a Core Hooks dispatcher shared with Performance's MousePickFeature. |
| Equipment/EquipmentInventory | Presentation vs HUD vs Compatibility | Equipment → Pres. Inventory read → Core query. Apocapocket bridge → Compatibility. |
| InventoryModel, CharacterPortraits.Prepare/Picture, VehicleHud, Car hints | HUD | HUD. |
| WheelArms `BeforeSteer` + forced `returnToCenter` | Presentation vs Vehicles | Vehicles (they change driving behaviour). |
| IgnitionControl, Car ignition/cassette/headlight keys | Vehicles vs Core input | Vehicles; Controls-row injection uses the Core input service. |
| GameBindings/KeyMap | Core | Core input (also replaces `Hotkeys.Register(NoOp, Never)`). |
| GunPose/AimLift editors, WheelGripEditor, PlayerBodyPages PoseEditor/Steering page | Developer | Developer, through Pres ModAPI only (no internal types). |
| ChaseCameraCompat, WeaponsPackShadows, Apocapocket bridge | Compatibility | Compatibility; they need Pres ModAPI members to replace the reflection into internals. |
| Perf.cs | Debug | Delete; fold the census into Debug telemetry. |
| DogAnimations (NPC) | Presentation vs NPCAI | Clip playback, blending, bone fade and IK → Pres; bite/jump/rest decisions → NPCAI. |
| ApBody (NPC), PlayerAnims/Rider/Pose (Patrols), QuestFollower (Quests) | – | They stay as consumers in their modules but call only Pres ModAPI. Rider's own PlayableGraph and Pose's limb poses move into Pres SeatPose. |

## 9. Save/config compatibility constraints

- **Module Id `PlayerBody`** is used in several places:
  - the config section prefix: settings.cfg `[PlayerBody]` (Enabled + F6 entries), `[PlayerBody / Climbing]`, `[PlayerBody / Camera culling]`, `[PlayerBody / Vehicles]`;
  - tools.cfg `[PlayerBody / Editors|Camera|First person|Car seat|Camera culling|Climbing|Animation|Animation (fallback clips)|Model|ModAPI]`, including 8 `Pose key: ...` entries (GunPose.cs:425);
  - `[Keys]` entries "Observe (third person)", "Climb (alternative key)", "Cassette start/stop", "Cassette volume down", "Cassette volume up";
  - the Harmony id `<GUID>.PlayerBody`;
  - `Dir` = `plugins\WildWasteland\PlayerBody\` and `DataDir` = `config\WildWasteland\PlayerBody\`.

  Splitting this into Presentation/Vehicles/HUD/Developer needs `cfg.Migrate` from the WW sections, which today's first-run-only `Legacy` path does not cover: it only migrates when the WW cfg is missing. A Core "renamed key" migration for existing WW configs is required. Keep `TunedSettings.InstallProfiles("PlayerBody")` profiles in step.
- **Key names and defaults to preserve.** CorePages "Game" page and the RESET defaults reference these entries directly: Character (enum `Gender` names incl. legacy Pistoleer/Mechanic, canonicalised), ThirdPerson, ToggleObserving, DynamicCrosshair, BodyFirstPerson, FullBodyOnlyOnFoot, FirstPersonArms, FirstPersonArmsOnlyOnFoot, FirstPersonArmSkin, ReplaceDriver, EquipmentOnBody, FemalePainSounds, SmoothWeaponSway, SmoothAimDownSights, PickAssist, Climbing, JumpTriggersClimbing, Ladders, CameraCulling, CameraCullingInVehicles, VehicleStatusHint, VehicleHotkeyHint, IgnitionKey, LanceIgnoresOwnCar.
- **Old-mod migration sources must stay readable** for at least one release: `com.denis.apocalypter.apocaplayer.cfg` (sections General, Debug, CAMERA, VEHICLE, Occlusion, climbing, `[Weapon grip]`), `com.denis.apocalypter.apocaclimber.cfg`, `config\Apocaplayer\camera.cfg`, and `config\Apocaplayer\*` sidecars (MigrateSidecars).
- **Sidecar files** in `config\WildWasteland\PlayerBody\`: weapon-poses.txt (`weapon|Clip = x,y,z,rx,ry,rz`), aim-lift.txt (`weapon|clip[@crouch]=...`, 13 floats), ignition-key.txt (`Key,AltKey`), legacy-base-lines.txt, weapon-grips.cfg, BuiltinPoses.generated.cs, and `WheelGrips\<prefab-<prefabId>|mesh-<name>|handlebars>.txt` (12/60/84/96 floats). They must move with their owner or be read from the old path for one release.
- **Plugin assets** in `plugins\WildWasteland\PlayerBody\`:
  - Models: Player_female.glb/png, Player_male.glb/png, Player2_female_arms.png, Player2_female_kick.png, apocaplayer_anims.bundle, apocaplayer_climbing.bundle, apocaplayer_camera.bundle, Hud\{ignition,handbrake,cassette}.png.
  - Sounds: Female\FEMPAIN1-4.wav, FEMDEATH.wav, shout_male/female.wav.
  - Portraits\*.png (user-editable, extracted when missing).
  - asset-index names: `PlayerBody.Crawl.low-crawl.bundle`, `PlayerBody.Ladder.climbing-ladder.wwpose`, `PlayerBody.CharacterPortraits.<Name>.png`, `PlayerBody.Textures.female_kick.png`.
  - build.sh:21 and WildWasteland.csproj:33 remap `PlayerBody/Portraits` → `LegacyPortraits`, `CharacterPortraits` → `Portraits`, and `Textures/female_kick.png` → `Models/Player2_female_kick.png`.
  - Renaming the module folder must update these, the asset index, the user-edited PNG locations, and the tools.cfg `Model`/`Texture`/`ArmsTexture`/`AnimationBundle` paths (relative to the module dir).
- **ES3/SaveTypes:** PlayerBody saves no ES3 keys and no saved components. Core/SaveTypes.cs:33 aliases `Apocaplayer, Apocaplayer` → `WildWasteland.Modules.PlayerBody` (unused today). If the namespace becomes `...Presentation`, update or keep the alias; it is harmless either way. `ES3Prefab.prefabId` is only read, as the WheelGrips file key.
- **Third-party binding by name. Do not rename without updating the bridges:**
  - ChaseCamera 0.2.5 resolves `Apocaplayer.ThirdPerson/Game/Plugin/OcclusionCutaway/Cave` (ChaseCameraCompat.cs:51-62) and then binds their static members by name (e.g. ThirdPerson.On/CarShift/ViewPos..., `Game.CarFsm`/`DriveUse`, and the facade's Enabled/OcclusionPrototype/OcclusionInVehicle/ThirdCarHeight). Keep a facade with the same member names in Compatibility.
  - Weapons Pack passes a `Body` to `UpdateThirdPerson`; WW reads `Body._prop` and `Body.WeaponPropVisible`/`Root`.
  - Public `ModAPI`, `ModAPI.Character` and `PlayerTraversal` are the documented third-party API gap (docs/modules.md:100).
- **ConflictsWith** GUIDs `com.denis.apocalypter.apocaplayer` and `com.denis.apocalypter.apocaclimber` (PlayerBodyModule.cs:28) must stay on whichever module absorbs them; ReplacementGuard depends on them.
- **`IAnimationApi` member semantics** used by NPC/Guns/Patrols/Quests must be preserved, or adapters provided, during the transition. `ApiVersion` = 1 and `PluginVersion` = "2.3.9" are compared by consumers (PlayerBodyModule.cs:21).
