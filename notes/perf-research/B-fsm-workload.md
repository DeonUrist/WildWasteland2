# Research B — Vanilla FSM workload audit (game content layer)

Static audit 2026-10-15 for `notes/plan-Performance.md`. Cost figures are estimates for Mono, not measurements; FSM/action counts come from the decoded dumps in `V:\Repos\Apocalypter\Sources`.
Citations `fsm/<scope>/<file>.md:Lnn`, `code/...cs:Lnn` relative to Sources. Short forms: `GM` = fsm/scene1_game/__GameManager__.md, `PCH` = fsm/scene1_game/PlayerCameraHolder.md, `PL` = fsm/scene1_game/Player.md, `POL` = fsm/prefabs/Poloska.md (stands for all 14 vehicles; same FSM set, different lines), `BJ` = fsm/prefabs/Boltjaw.md (stands for NPCs), `FSMCS` = code/PlayMaker/HutongGames.PlayMaker/Fsm.cs.

Method: live FSMs = enabled component on an active object (hierarchy dumps, inherited inactivity). Actions classified per-frame (everyFrame / repeatInterval / never-finishing with OnUpdate), timed (Wait, RandomWait, NextFrameEvent, delayed SendEvent/DestroySelf), callback (TriggerEvent…), instant. Resting state: follow FINISHED/instant events from start to the first state with per-frame actions, or an idle state waiting for callbacks/events.

## 0. Cost model facts
- Every enabled PlayMakerFSM is a MonoBehaviour with `Update()` (code/PlayMaker/PlayMakerFSM.cs:L426-L432). Idle FSM pays Unity dispatch + `Fsm.Update` (push/pop, `UpdateDelayedEvents`, `UpdateState`) + `UpdateStateChanges` looping all states (FSMCS:L2184-L2232, L2662-L2672) ≈ 0.4–0.8 µs/frame.
- Per-frame actions = virtual calls over `ActiveActions` (FsmState.cs:L348-L359); a compare/clamp ≈ 0.1–0.3 µs.
- Targeted events scan every enabled FSM (FSMCS:L2525-L2640). ~4000 live FSMs: targeted send ≈ 0.1–0.3 ms + ~32 KB garbage; with sendToChildren ≈ 0.5–1.5 ms; BroadcastAll allocates `FsmEventData` per FSM.
- FsmList membership kept in OnEnable/OnDisable with O(N) `List.Remove` (PlayMakerFSM.cs:L420-L455): deactivating an object with k FSMs costs k×O(N).
- RestartOnEnable default true (FSMCS:L2115, L2280): every SetActive(true) re-runs start-state init.
- Same-frame loop limit: entering a state `MaxLoopCount` times in one frame disables the whole PlayMakerFSM (FSMCS:L2742-L2746). Culling sweeps set maxLoop=10000 and process the whole list in one frame (§3.2).
- MousePick cache holds one query (code/Assembly-CSharp/HutongGames.PlayMaker/ActionHelpers.cs:L178-L209): reused only for identical mask and not-longer distance.
- SetProperty/GetProperty: reflection + boxing every call, one lookup per dotted segment (FsmProperty.cs:L246-L370) ≈ 1–3 µs, 20–60 B. CallMethod = MethodInfo.Invoke with boxed return.

## 1. Runtime census
### 1.1 Per-instance load by group
| Group | Prefabs | Live FSMs/instance | Per-frame actions at rest | Idle share | Culled when |
|---|---|---|---|---|---|
| Scene scene1_game (excl. pre-baked MapMagic/Tile 0,0) | 1 | 326 | ~238 (~44 MousePick) | 69% | never |
| Vehicle shell | 14 | 99–116 (avg 106) | 79–88 (avg 80) | 47% | **never deactivated**; destroyed only >5 km |
| Trailer | 3 | 15–17 | 13 | 43% | as vehicles |
| Item / car part | 320 | avg 7.5 (engine 21, radio 14, wheel 11) | avg 1.4 | 85% | deactivated >1 km by 5 s sweep |
| Weapon (world prefab) | 23 | ~10 | ~0 | 99% | as items |
| NPC idle | 51 | avg 11.6 | avg 5 (Detection 3, Health 1, Sound3 2) | 77% | deactivated >1 km |
| NPC fighting | – | ~16 (+Attack, Damage, Rotate, Unstuck) | ~16 + 2–4 raycasts | – | – |
| POI (Camp/Building/Cave/Wreck) | 38 | avg 20 (Camp_7 71, Camp_14 57, Cave_6 34) | avg 6.3 (Camp_7 32, Cave_6 22) | 85% | lives with its MapMagic tile; no distance culling |

Player's assembled car ≈ shell (~110 FSMs, ~88 per-frame) + ~22 parts (engine 21, radio 14, radiator 15, 4 wheels ×11, exhaust/doors/hood/trunk 7, seats 6, gauges 6, bumpers 6, steering 4, gear lever 5, suspension 8, forced induction 9) ≈ **340 FSMs, ~135 per-frame actions**. Spawned traffic cars are bare shells (`checkWheel` rests in "no wheel", POL:L2862-L2875).

### 1.2 Scenario estimates (60 fps)
| Scenario | Live FSMs | Per-frame action calls | FSM-layer CPU (est.) |
|---|---|---|---|
| A. On foot idle at base [player car, 6 cars within 5 km, 300 items within 1 km, 20 POIs, 4 idle NPCs] | ≈ 4.0k (326+340+640+2250+400+46) | ≈ 1.4k | ≈ 2.4 ms tick overhead + 0.4 ms actions + 0.35 ms raycasts/reflection ≈ **3 ms** |
| B. Driving between POIs [10 cars, 40 items, 20 POIs] | ≈ 2.4k | ≈ 1.4k (cars dominate) | ≈ 2–2.5 ms **+ 5–40 ms spike every 5–10 s** (culling + item (de)activation) |
| C. Fight at Camp_7 [8 NPCs fighting, ~150 fresh items, 6 cars] | ≈ 3.0k | ≈ 1.3k + ~30 raycasts/frame + 10–30 full FSM scans/s | ≈ 2.5 ms + POI-entry spike of 100–200 targeted sends |

Conclusions: (1) FSM **count** costs more than their actions (~70% idle but paying the tick); items largest by count, vehicles largest by per-frame actions and never culled by distance. (2) Every car within 5 km runs ~80 actions/frame even 4 km away and kinematic (5 reflection reads, 1 reflection write, 2 MousePickEvent raycasts). (3) Stutter = bursts: same-frame culling sweeps, item (de)activation, full FSM scans during spawn and save/load, BroadcastAll.

### 1.3 What dominant groups do at rest
| Group | Resting behaviour |
|---|---|
| Vehicle `vehPart_Attach` ×23/car (POL:L1584; wheel L2918-L2935) | `partAttached`: GameObjectHasChildren every frame (wheels: GetChildCountAdvanced + IntCompare) |
| Vehicle checkWheel ×4, getEngine, getRadiator, GetChild, Shake ×2, BumperCheck, WindshieldType, checkSuspension, HingeAdjustVisible (POL:L2862, L1929, L2517, L2085, L1906, L581) | second set of child-count polls on the same hinges |
| Vehicle `Pedal` ×3 (POL:L3583-L3610) | reflection GetProperty `VehicleController.input.*` + Lerp + SetPosition every frame on every car |
| Vehicle `RpmGear` (POL:L296-L301) | two 3-level reflection reads/frame; read by FuelConsumption, Shake, ExhaustSmoke via GetFsmFloat |
| Vehicle `Handbrake` (POL:L1524-L1531) | reflection write `input.Handbrake=1` every frame + MousePickEvent (2 m, mask [0]) |
| Vehicle `Start` (POL:L1426-L1440) | MousePickEvent every frame |
| Vehicle `HingeFollow` ×3 (POL:L3502-L3536) | GetPosition, SetPosition, LookAt(PlayerCamera) every frame |
| Vehicle `gearChange` (POL:L2202) | GetFsmInt + 6 IntCompare every frame |
| Vehicle DistanceKinematic, TireWear, Speed, CrashDamage, FuelConsumption (POL:L255-L268, L312-L322, L305, L151, L1196-L1210) | GetDistance/GetSpeed/GetFsmFloat + compare every frame |
| Item `CheckTag` (fsm/prefabs/bumper_10.md:L19-L39; engine 3.8L…md:L109) | GameObjectCompareTag every frame waiting for tag change |
| Item `Condition`, `LiquidAmount` (wheel_1.md:L60-L70, 3.8L…md:L639) | FloatClamp every frame as a watch |
| Engine AirCooling, exhaust ExhaustSmoke (3.8L…md:L35, exhaust_v8.md:L54) | GetFsmFloat + compare |
| Item data FSMs: ID, ItemName, Value, imageUI, weaponType, saveItemVar, de_Attach, Colliders, LockPhysics.off | no actions — ~6 of 7.5 FSMs per item are pure tick overhead |
| NPC `Detection` (BJ:L178-L216) | SensorGetDetections + GetFsmFloat + FloatChanged every frame |
| NPC `Health` (BJ:L236-L249), `Sound3` (BJ:L364) | health compare every frame; GetFsmFloat + FloatChanged |
| POI `LightFlicker` (~50 lights, e.g. Camp_2.md:L264-L270) | Perlin, multiply, clamp, SetLightIntensity every frame for every light in every loaded POI |
| __GameManager__ | `Ammo` 30 actions/frame (GM:L74-L127: 10 IntClamp + 20 UiTextSetText into hidden PlayerSheet); `UI_Manager` 15/frame (GM:L861-L888, 7 string allocs); `ArmorBackpack` 6; `DayTime` 5 (§3.4) |
| PlayerCameraHolder (79 live) | 22 FSMs with MousePick + MousePickEvent pairs (PCH:L435, L533, L649 … L2105-L2110, L2235); `Weapons off 2` polls 3 buttons (PCH:L2375) |
| Player | `Movement Standing` 10 actions/frame incl. **2× SetProperty CapsuleCollider.height every frame** (PL:L513-L527); `GroundTest` SphereCast every frame (PL:L267) |
| Options canvases | 12 UiToggleGetIsOn every frame (Graphics_Canvas.md:L42, L73 …), polling while hidden (menus hide by disabling the Canvas component) |

## 2. Polling loops — top 30 by instances × executions/s (scenario A)
Only 14 genuine timer loops exist, all ≤0.5 Hz; almost all polling is an everyFrame compare used as "wait until".
| # | FSM / state (citation) | Inst | Exec/s | Polls | Replacement |
|---|---|---|---|---|---|
| 1 | vehPart_Attach `partAttached` (POL:L1584, L2918) | 161 | ~12k | hinge child count | event-driven (OnTransformChildrenChanged) or 5 Hz |
| 2 | Pedal ×3 (POL:L3583) | 21 | 3.8k (1.3k reflection) | VehicleController input | C# direct reads, only within 20 m |
| 3 | HingeFollow ×3 (POL:L3502) | 21 | 3.8k | hinge pose | only while HingeAdjustVisible on |
| 4 | gearChange `H` (POL:L2202) | 7 | 2.9k | gear | player's car only |
| 5 | CheckTag (bumper_10.md:L19) | ~450 | 27k (cheap) | own tag | postfix on SetTag action sends the event; or 5 Hz |
| 6 | checkWheel ×4 (POL:L2862) | 28 | 3.4k | child count | merge into #1 |
| 7 | LightFlicker (Camp_2.md:L264) | ~60 | 14k | Perlin → light | C# manager 20 Hz, skip >150 m |
| 8 | RpmGear (POL:L296) | 7 | 840 reflection | rpm, gear | compiled getter; 10 Hz when engine off |
| 9 | Handbrake `HandbrakeOn` (POL:L1524) | 7 | 420 refl. writes + 420 raycasts | – | set once on entry; raycast only within 3 m |
| 10 | Start `off` (POL:L1433) | 7 | 420 raycasts | aiming at ignition | gate on player distance |
| 11 | DistanceKinematic (POL:L255) | 7 | 840 | distance > 2000 m | 1 Hz |
| 12 | TireWear (POL:L312) | 7 | 840 | speed > 1 | 4 Hz |
| 13 | FuelConsumption `off` (POL:L1199) | 7 | 840 | rpm > 30 | 4 Hz in `off`; keep `on` per frame |
| 14 | Shake `off`/`idle` ×2 (POL:L1906, L2113) | 14 | 0.8–2.5k | engine child, rpm | event from #1; 10 Hz |
| 15 | getEngine, getRadiator, GetChild, BumperCheck, WindshieldType, checkSuspension, HingeAdjustVisible | 49 | 2.9k | has child | event from #1 |
| 16 | Speed, CrashDamage (POL:L305, L151) | 14 | 840 | speed | 5 Hz except player's car |
| 17 | Condition FloatClamp (wheel_1.md:L60) | ~60 | 3.6k | clamp | 2 Hz |
| 18 | LiquidAmount FloatClamp (POL:L1213, Building_7.md:L382) | ~80 | 4.8k | clamp | 2 Hz |
| 19 | AirCooling `off` (3.8L…md:L35) | 7 | 840 | rpm | 4 Hz |
| 20 | ExhaustSmoke `idle` (exhaust_v8.md:L54) | 7 | 840 | rpm | 4 Hz |
| 21 | NPC Detection `notDetected` (BJ:L187) | 4–10 | 0.7–1.8k | sensor | 10 Hz (sensor pulses every 0.5 s: data/hierarchy/prefabs/B/Boltjaw.txt:L27) |
| 22 | NPC Health (BJ:L248) | 4–10 | 240–600 | health < 1 | event from Bodypart `damage` |
| 23 | NPC Sound3 (BJ:L372) | 4–10 | 0.5–1.2k | float changed | 5 Hz |
| 24 | GM Ammo (GM:L97) | 1 | 1.8k | clamp + text | only while PlayerSheet open; else 2 Hz |
| 25 | GM UI_Manager (GM:L873) | 1 | 900 (420 allocs) | HUD | 10 Hz, only on change |
| 26 | GM DayTime `day` (GM:L390) | 1 | 300 (120 reflection) | Azure | C# 0.2 s timer (§3.4) |
| 27 | PCH MousePick pairs ×22 | 22 | ~2.6k raycasts | look target | one shared raycast (§3.1) |
| 28 | UiToggleGetIsOn (Graphics_Canvas.md:L42) | 12 | 720 | toggle | only while canvas enabled |
| 29 | Sell board `trigger` (Sell_Items_mechanic.md:L60) | ~3 | 360 | distance < 500 m | 2 Hz |
| 30 | AddSphereCollider (POL:L2952-L2979) | 28 | 98 (14 reflection) | wheel radius | stop after first set; refresh on wheel change |

## 3. Expensive actions in hot states
### 3.1 Raycasts
- Camera picking: 22 MousePick/MousePickEvent pairs, 13 distinct masks at 2 m → the one-entry cache mostly misses: ~30–44 raycasts/frame, each with own `Camera.main` + `ScreenPointToRay`; each nearby vehicle adds 2 (Handbrake mask [0], Start). Exact fix: patch `DoMousePick` to one `RaycastAll` per frame with the union mask; answer each (mask, distance) with the nearest hit in that mask.
- Not steady: item LockPhysics (bumper_10.md:L60-L98) casts once 3 s after enable (re-casts on reactivation); vehicle TrailerLoad (POL:L407-L460) runs 0.55 s after spawn then disables itself.
- Necessary: player `GroundTest` SphereCast.
- NPC combat: Attack `trigger` (BJ:L41-L59) SensorGetDetectionRayHit + 2 LookAt every frame + 2 Raycasts every other frame; Damage (BJ:L142) another RayHit → ~3–4 casts per fighting NPC per frame.
### 3.2 Item-culling sweeps (GM:L405-L482, NewGO_ArrayList.md:L11-L35)
- Not sliced: `ArrayListGetNext` fires its loop event from OnEnter → whole list in one frame; per entry 3 state entries + 5–6 actions ≈ 3–6 µs. `ArrayList_Items` holds every spawned item, NPC, corpse and map icon within 5 km (active or not). 2000 entries → deactivation sweep alone 6–12 ms.
- Phase-locked: four sweeps (5/10/10/5 s game-time waits) start together at load → same frame every 10 s ≈ 25 ms at 2000 entries + SetActive cost (§3.8).
- Silent failure past 10,000 entries: PlayMaker disables the FSM component (FSMCS:L2742) → culling stops for the session (reachable with "perma base").
- Cars never distance-culled: CarSpawn/TrailerSpawn add only to `ArrayList_Cars` (MapMagic.md:L110); the deactivation sweep walks only `ArrayList_Items`; beyond 2 km `DistanceKinematic` only makes cars kinematic (POL:L255-L268).
- Minor bugs: `DeleteDestroyedItems` skips the entry after each removed null; `DeleteFallingItems` destroys anything not above y=-10000 (GM:L439-L447).
### 3.3 Reflection every frame
Per vehicle: Pedal 3, RpmGear 2 (3-level), Handbrake 1. Player Movement 2× SetProperty CapsuleCollider.height (PL:L516-L518) — also rebuilds the physics capsule every frame. DayTime 2 CallMethod. Camp_7 `setPos` 2 CallMethod (Camp_7.md:L886-L896). Scenario A ≈ 60–80 reflection calls/frame ≈ 0.1–0.2 ms, ~3 KB/frame boxing garbage.
### 3.4 Get/SetFsm* every frame
After first lookup ≈ 0.2–0.4 µs (by-name variable lookup). Hot: NPC Detection/Sound3, FuelConsumption, AirCooling, ExhaustSmoke, gearChange, GM effect FSMs.
**DayTime runs every frame, not every 0.2 s**: `day` has everyFrame CallMethods + IntChanged, so FINISHED back to `wait` never fires (GM:L390-L395; systems/world_ui.md:L54 documents the intended 0.2 s). everyFrame=false alone breaks it (IntChanged stores "previous" in OnEnter after GetDay) → reimplement in C#.
### 3.5 Strings
Per-frame UiTextSetText: Ammo 20, UI_Manager 7, ArmorBackpack 4, DayTime 1, FPS 1. `Text.text` skips rebuild when unchanged; the cost is creating the strings (~10–30 allocs/frame, 0.5–1 KB/frame).
### 3.6 SendEvent to GameObject/GameObjectFSM (each scans every enabled FSM)
| Where | Sends | When |
|---|---|---|
| POI `spawn` (Wreck_5.md:L51-L63, MapMagic.md:L106-L110) | 2 per spawned object | POI entry: 100–200 sends in one frame ≈ 20–60 ms, 3–6 MB garbage |
| NPC Health `drop` (BJ:L262-L276) | 2 per drop | on death |
| NPC Attack `trigger` (BJ:L47), Detection (BJ:L188), Damage (BJ:L149) | 1 each | ~1/s per fighting NPC, every hit |
| Vehicle TireWear (POL:L316, L321) sendToChildren | 1 | crossing 1 m/s, car start |
| Vehicle Start `off` (POL:L1440) sendToChildren | 1 | spawn, engine stop |
| Save_NewGO_ArrayList (NewGO_ArrayList.md:L62, L85, L99, L117; load L77, L89, L104, L123) | 1 sendToChildren per car, item, sandstorm, spawner | every save/load: ~1–5 s on a large world |
| vehPart_Attach `Over` (POL:L2939) | 1/frame | while hovering a hinge with a part in hand |
Fix with identical results: prefix on `SendEventToFsmOnGameObject`/`BroadcastEventToGameObject` resolving targets per GameObject (see research A R1–R3 for exact order preservation).
### 3.7 BroadcastAll (8 senders in the game scene)
Senders: `DayChanged` (GM:L401, once per 48 min), Coyote faction Friendly/Enemy (GM:L536, L541), `Load Settings` (SaveLoadGame.md:L19, L36), `Save Settings` (SaveGame_Canvas.md:L375, MainMenu_Canvas.md:L44, LoadGame_Canvas.md:L110).
**Every hit on a Coyote re-broadcasts**: weapon `CheckFriendly → CoyotesHit` (PCH:L2947-L2960) sends FactionCoyotesEnemy to GM; global transition `→ CoyotesAttacked → PlayerEnemy` broadcasts again on every hit ≈ 1–3 ms + ~0.4 MB garbage each, 5–10/s with automatic weapons. Fix: broadcast only on friendly→enemy change.
### 3.8 Activation churn
- Every item crossing 1 km → ~7.5 OnDisable each O(N) `FsmList.Remove`, all in the sweep frame; driving away from a 100-item POI ≈ 10–30 ms in one frame.
- Re-activation re-runs CheckTag start-up which re-adds a Rigidbody: `vehPartRemoved` (bumper_10.md:L27-L34) `AddComponent Rigidbody` on parts that ship with one (data/hierarchy/prefabs/B/bumper_10.txt:L2) → "Can't add component" (AddComponent.cs DoAddComponent) + Unity's own message — likely two error logs with stack traces per part spawn/reactivation. Verify in LogOutput.log.
- Save re-activates everything: `SaveGameActivateItems` re-activates every item before saving (systems/save_load.md:L86) → full re-initialisation storm.

## 4. Candidates (benefit/risk order)
Tools: (a) runtime FSM edits (`Actions[i].Enabled=false`, replace a state's Actions array, change Wait.time/everyFrame); (b) disable PlayMakerFSM + C# replacement writing the same FSM variables and sending the same events; (c) Harmony on PlayMaker runtime; (d) per-action throttle prefix on OnUpdate for whitelisted (FSM, state) pairs `(frame+id)%N==0`. Apply from a postfix on `PlayMakerFSM.Awake` keyed by (FSM name, root prefab).
- **C1 Idle-FSM fast path** — very high (~1–2 ms/frame scenario A), low risk. Skip Fsm.Update work for started, settled FSMs with 0 active actions and 0 delayed events (research A §4 has the exact sleep design).
- **C2 Component-resolved event targets** (§3.6) — high (removes POI-entry and save/load stalls), low risk; 0.1–1.5 ms → ~2–10 µs per send. Verify: time a save with 1000+ items; all SaveGame/LoadVar handlers fire.
- **C3 Shared camera pick** (§3.1) — medium (~40 raycasts → 1), low risk.
- **C4 Distance-cull other cars** — very high in A/B (~500–1000 fewer actions/frame), medium risk. Lower-risk form: beyond 50 m disable Pedal, HingeFollow, gearChange, RpmGear, Handbrake, Start, TireWear, Speed, CrashDamage, FuelConsumption, Shake; re-enable near. Full form: SetActive(false) beyond 800 m except player's car and cars with trailers; re-activate all before save. Verify: compare culled car FSM states vs control after 5 drive-away/return cycles; save contents.
- **C5 Sliced, de-phased C# culler** — high on hitches (10–40 ms → <1 ms/frame), low–medium risk. Disable the four GM/NewGO sweep FSMs; one component reading the ArrayList proxies, 64 entries/frame, sqrMagnitude with 950–1050 m hysteresis, ≤8 SetActive/frame, correct null compaction, honours "perma base"; 10k cap disappears.
- **C6 Event-driven vehicle hinges** — medium-high (~35 actions/frame/car), medium risk. Hinge component using `OnTransformChildrenChanged` sends the same next/back events the polls would. Verify attach/detach every part type on 3 vehicles; HUD cursor; engine shake/radiator links.
- **C7 Compiled access for Pedal/RpmGear/Handbrake** — medium, low risk. One component per car: rpm/gear every frame for player's car, 5 Hz otherwise; pedals within 20 m; handbrake once when parked (check whether NWH clears input every frame).
- **C8 Throttle "wait until" polls** (§2 #5, #11–13, #16–20, #23, #29) — medium, low risk. Never throttle states that accumulate per-second values (FsmFloatAdd perSecond in FuelConsumption `on`). Verify fuel/tyre wear over 10 min within 1%.
- **C9 One C# LightFlicker manager** (incl. Light_Flicker `FSM`, Light_Flicker.md:L12) — 20 Hz, lights within 200 m.
- **C10 HUD/GM FSMs** (Ammo GM:L74, UI_Manager GM:L861, ArmorBackpack GM:L129, DayTime GM:L382) — update only while PlayerSheet open else 2 Hz; strings on change; DayTime in C# (fixes per-frame bug).
- **C11 Coyote broadcast storm** (GM:L525-L551) — on enemy event go to a state that only resets the timer, or suppress the broadcast when OnOff==1. Verify first-hit hostility, timer reset, friendly broadcast after 600 s.
- **C12 Duplicate Rigidbody AddComponent** — prefix on `AddComponent.OnEnter` returning the existing component; optionally RestartOnEnable=false on the 460+ pure-data item FSMs. Verify no "Can't add component".
- **C13 Player Movement collider writes** (PL:L513-L527) — write height only while the tween runs. Verify crouch/crawl/stand under low ceilings.
- **C14 Hidden option canvases** — disable 12 toggle-polling FSMs while closed; re-enable in postfix on the ActivateComponent2 that opens them.
- **C15 NPC self-sends** (BJ:L47, L188) — mostly solved by C2.
Not recommended: changing culling FSM Wait times (moves the spike); removing NPC Health poll without an event source (it is the death trigger).

## 5. Verification harness
Every 5 s group `PlayMakerFSM.FsmList` by (FSM name, active state) → count + sum of ActiveActions.Count; time PlayMakerFSM.Update per FSM name (sample 1 frame in 30); count Fsm.Event by target type and BroadcastEvent calls; log frames >8 ms with which sweep ran. Targets: FSM layer ≤1 ms/frame in A; no culling frame >5 ms; save time halved on a 1000-item world; zero "Can't add component".
