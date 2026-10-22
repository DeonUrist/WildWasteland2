# Research A — PlayMaker runtime performance audit (Apocalypter, Unity 2020.3 Mono)

Static audit 2026-10-15 for `notes/plan-Performance.md`. Nothing measured; µs/ms figures are estimates and N (the runtime `PlayMakerFSM.FsmList.Count`) is unknown until Phase 0 telemetry runs.
Paths are relative to `V:\Repos\Apocalypter\Sources\code\`. `Fsm.cs` = `PlayMaker/HutongGames.PlayMaker/Fsm.cs`, `PMF.cs` = `PlayMaker/PlayMakerFSM.cs`.

## Static facts (from Sources fsm/**, PLAYMAKER_ACTIONS.tsv, data/script_usage.tsv)
- 8129 FSMs (7209 in prefabs); avg 3.09 states, 10.2 actions per FSM. Vehicles 118–147 FSMs each plus parts; Camp_7 89; PlayerCameraHolder 363.
- 82,877 action instances of 235 types; 149 types override OnUpdate/OnLateUpdate/OnFixedUpdate/OnActionUpdate (54,916 instances).
- `RestartOnEnable` true on 7919 FSMs; no FSM uses `ManualUpdate`.
- System events used in transitions: only FINISHED (3190) and UI BOOL/FLOAT VALUE CHANGED (38). Nothing handles DISABLE, BECAME VISIBLE/INVISIBLE, TRIGGER/COLLISION or MOUSE events. Triggers/collisions only via `TriggerEvent` (1023 enter, 584 exit) and `CollisionEvent` (30). No used action overrides `FsmStateAction.Event()`.
- SendEvent targets: GameObjectFSM 2506, GameObject 1965 (286 with sendToChildren), BroadcastAll 13 (Load/Save Settings, DayChanged, Coyote faction), everyFrame 379 (377 = vehicle `vehPart_Attach` hover `Cursor_Wrench` → `{G:UI_Canvas}`), delay>0 51. Event payloads almost unused (11 GetEventFloatData).
- FsmLog.LoggingEnabled is forced false in builds (PMF.Awake) → logging is NOT a cost.
- Cost model (Mono JIT): native call such as `.gameObject` ~15–30 ns; Unity `==` on a live object ~5–10 ns; native→managed Update message ~0.1–0.3 µs.

## 1. O(N) and allocation-per-call paths
### 1.1 Event sending — `Fsm.Event(FsmEventTarget, FsmEvent)` Fsm.cs:2429-2485 (every send first runs `SetEventDataSentByInfo`, Fsm.cs:2385)
- GameObject target → `BroadcastEventToGameObject` (2549-2582): allocates `FsmEventData` (2555) + `new List<Fsm>` (2557); loops all N FSMs (`fsm==null`, `.gameObject`, `==go`) ≈35–50 ns each → ~0.2 ms at N=5000; one more `FsmEventData` per receiver (2578). With sendToChildren every non-matching FSM runs `IsFsmChildOfGameObject` (2584-2605, parent-chain walk) → ~1–2 ms per event.
- GameObjectFSM target → `SendEventToFsmOnGameObject` (2615-2642): `new List<PlayMakerFSM>(FsmList)` (2622) = 40 KB garbage at N=5000; scan until first name match (N/2 avg, N if none) → 0.1–0.2 ms.
- BroadcastAll (2525-2539): list copy + `FsmEventData` per FSM (2534) ≈ 0.85 MB at N=5000. `PMF.BroadcastEvent` (PMF.cs:529-538) copies too. `PMF.FindFsmOnGameObject` (143-153) O(N) but unused.
- Load/spawn produce event storms: 1000 sends × 0.2 ms = 200 ms hitch.
### 1.2 ProcessEvent / UpdateStateChanges
- `ProcessEvent` (2322-2383): `Active` getter (688-698: two Unity null checks + `owner.gameObject`) + linear transition scan, for every target even if unhandled.
- `UpdateStateChanges` (2662-2673) loops over every state (`ResetLoopCount`) after each Update/event.
### 1.3 Delayed events
- `UpdateDelayedEvents` (2212-2231) every frame: two Clear, AddRange, two loops even when empty. Each `DelayedEvent` allocates itself + `FsmEventData`.
### 1.4 Name lookups
- `FsmVariables.GetVariable` (1036-1052) = Dictionary; local miss → global lookup. `GetFsmX` (1153-1255) allocates a new detached variable on every miss.
- `ActionHelpers.GetGameObjectFsm` (Assembly-CSharp/HutongGames.PlayMaker/ActionHelpers.cs:80-95): `GetComponents<PlayMakerFSM>()` array alloc; on miss `Debug.LogWarning(... + go.name)` (~0.1–1 ms). Used by every Get/SetFsm* when target/name changes. (WW Core `QuietFsm` already replaces this lookup when its switch is on.)
- `EnableFSM` (EnableFSM.cs:49) `GetComponents<PlayMakerFSM>()` every OnEnter.
### 1.5 FsmProperty (SetProperty 3848, GetProperty 294) — FsmProperty.cs
- Per call `SetValue` (246) / `GetValue` (328): `CheckForReinitialize` (444, Unity `!=`), up to 15 `IsAssignableFrom`, reflective `PropertyInfo/FieldInfo.Get/SetValue` (ReflectionUtils.cs:194-256) with boxing + `object[]` → ~0.5–1.5 µs, 50–100 B garbage. Multi-level paths (`input.Throttle`) = one reflective read per level + struct write-back. Target change → `Init()` (403) again (`Split('.')`, GetProperty/GetField per level).
- Targets: VehicleController 1197, WheelController 846, Rigidbody 766, ConfigurableJoint 448. Per-frame: vehicle input/engine paths. `CallMethod` (107) = `MethodInfo.Invoke` + boxing.
### 1.6 Lifecycle
- No re-deserialization on enable: `InitData` (1857-1903) guarded by `[NonSerialized] initialized`. RestartOnEnable (2095-2128) only re-enters the start state (re-runs init actions such as GetChild/GetComponent chains on every SetActive(true)).
- Full deserialization on every Instantiate: `PMF.OnAfterDeserialize` (554-560) → `InitData` → `LoadActions` per state: `byteData.ToArray()` (ActionData.cs:640), `Activator.CreateInstance` per action (697), `FieldInfo.SetValue` per field (744-757 → LoadActionField 807), then `Awake` (1808) loops actions → ~2–4 µs/action ≈ 3–6 ms per vehicle (145 FSMs). Verify `Preprocess()` doesn't run in the build (`preprocessed` is serialized).
- `fsmList.Remove(this)` O(N) using `UnityEngine.Object.Equals` (10–25 ns/element): OnDisable (PMF.cs:455) scans ~N/2 → 30–60 µs per FSM; OnDestroy (PMF.cs:464) is a guaranteed full miss (OnDisable already removed it) → 50–125 µs. DISABLE processed twice (PMF.cs:453 and Stop→SendDisableEvent Fsm.cs:2268-2279). At N=5000: deactivating Camp_7 ~3–5 ms in Remove alone; destroying a vehicle ~20 ms; culling 500 FSMs 15–30 ms.
- Proxy components baked into prefabs: TriggerEnter 823, TriggerExit 486, LateUpdate 122, FixedUpdate 123, CollisionEnter 38, OnGUI 49.
### 1.7 Other per-frame global loops
- `PlayMakerGUI.OnGUI` (PlayMakerGUI.cs:349-399), once per GUI event: `fsmList.Clear()+AddRange(FsmList)` (364-365) then all N FSMs: null, `Active`, `HandleOnGUI`, loop active-state actions calling virtual `OnGUI` → 0.3–0.8 ms per event, 1–3 events/frame. Useless for vanilla content: only `ActivateGameObjects` (flags its own GUI component, finishes in OnEnter) and `SetMouseCursor` (5 uses; no-op while PlayMakerGUI exists) override OnGUI.
- 47 `PlayMakerOnGUI` components (FSMs containing ActivateGameObjects) — dispatch never reaches an active action (FsmState.cs:310-313).
- `PMF.OnBecameVisible/Invisible` (540-548) run the full Event path on every renderer visibility flip; no FSM handles them.
### 1.8 Trigger/collision proxies (PlayMakerTriggerEnter.cs etc., Fsm.cs:3076-3130)
- Per callback per target FSM: null checks, `Active`, handler flag, `TriggerName = other.gameObject.name` (3080, string alloc; GetTriggerInfo used 0 times), loop active actions, `Event(TriggerEnter)` nobody handles. No Stay proxies → cost per enter/exit only.
### 1.9 Minor
- `FsmEventTarget.Self` allocates 4 objects per access (only collection proxies; events disabled on all 202).
- `FsmStateAction.Init` (178-184) calls `fsm.GameObject` (Unity `!=` + `.gameObject`) per action per state entry; `GetOwnerDefaultTarget` (2815) → `GameObject` (789) repeats it.
- Global variables `{G:...}` resolved once into references (no per-call global lookup).

## 2. Replacements (each: Off / Shadow / On switch; Shadow = run vanilla, compute new result side-effect-free, compare, count+log mismatches)
Harmony patches private Unity message methods fine on Mono.
- **R1 Registry of enabled FSMs** (base for R2–R7): postfix `PMF.OnEnable`, prefixes `OnDisable`/`OnDestroy`. Keep a monotonically increasing stamp per FSM when vanilla appends to `fsmList`, a `Dictionary<goInstanceId, list>`, an in-list flag. fsmList only appends and Remove preserves order → fsmList order == ascending stamp; per-GO lists keep that relative order. If anything edits FsmList directly, shadow catches it → rebuild by re-stamping.
- **R2 SendEventToFsmOnGameObject O(k)**: prefix returning false: null→return; `SetEventDataSentByInfo`; snapshot that GO's list into a pooled array (stack of pools — ProcessEvent re-enters); empty fsmName → ProcessEvent all in order; else first whose `Fsm.Name` matches (read at dispatch time). Equivalent: vanilla snapshots before dispatch (FSMs enabled during dispatch not targeted; disabled ones no-op via `Finished`). No FsmEventData copies on this path.
- **R3 BroadcastEventToGameObject O(k)/O(subtree)**: null→return; copy `EventData`; `SetEventDataSentByInfo`; targets = GO's list, or `go.GetComponentsInChildren<PlayMakerFSM>(true, pooled)` filtered by in-list and sorted by stamp; excludeSelf removes first `.Fsm == this` (reference); per target fresh `EventData` copy + ProcessEvent; restore. Optional fallback to vanilla when subtree FSM count > N/4 (PlayerCameraHolder 363).
- **R4 BroadcastEvent**: pooled snapshot; keep per-target FsmEventData copy and excludeSelf. Do not filter non-handlers: vanilla ProcessEvent calls `Start()` on a not-yet-started FSM (Fsm.cs:2328).
- **R5 Fast fsmList removal**: transpile OnDisable/OnDestroy to a helper: return if not in list; else reference-compare + RemoveAt. Exactly equivalent (`this` alive in both; `Object.Equals` of two different live objects is false).
- **R6 GUI loop / OnGUI component / visibility events**: PlayMakerGUI loop iterates only FSMs having an OnGUI-overriding action (computed at Init, in-list, stamp order); disable `PlayMakerOnGUI` when all OnGUI-overriding actions in its FSM are `ActivateGameObjects`; visibility-event prefix returns false when the FSM has no transition on BECAME VISIBLE/INVISIBLE, no output-event listener, no event-handling active action (keep vanilla when FSM not started).
- **R7 Trigger proxies**: lazy `TriggerName` from stored collider; skip the unhandled system Event under R6 conditions + started.
- **R8 Compiled FsmProperty accessors**: static cache (target type, path) → member chain + property type + compiled getter/setter (DynamicMethod/Expression; Reflection.Emit works on Mono); replicate `ReflectionUtils.SetMemberValue(MemberInfo[])` incl. struct write-back; typed float/bool/int/enum paths; same `IsAssignableFrom` order. ~1 µs → 30–60 ns, no garbage. Shadow: compare read values / read back after writes.
- **R9 Faster action loading on Instantiate**: compiled field setters + compiled constructors per action type; skip `byteData.ToArray()` when empty. Same values → exact. Do not pool action objects.
- **R10 Get/SetFsm* and GetGameObjectFsm**: non-allocating `GetComponents(typeof(PlayMakerFSM), list)` (same order); warning rate-limited once per (GO, name); cache resolved variable per action instance; never cache misses.

## 3. Top game-used actions: per-call cost × usage (eF = instances with everyFrame)
| # | action | uses (inst/FSMs) | eF | per-call cost | optimized replacement |
|---|---|---|---|---|---|
| 1 | SendEvent | 4484/1965 | 379 | O(N) scans; list copy; 1–2 ms with children | R1–R3 |
| 2 | GetChild | 3479/1550 | 0 | recursive subtree search; enumerator per level, name string per child, tag string; tens–hundreds µs on vehicles; reruns on every re-enable | allocation-free loop same order; CompareTag only when tag defined; opt-in validated cache (shadow) |
| 3 | SetProperty/GetProperty | 3848/294 | 238/108 | reflection, boxing, re-Init on target change | R8 |
| 4 | EnableFSM | 3574/1138 | – | GetComponents alloc; OnDisable pays O(N) remove + double DISABLE | List overload; R5 |
| 5 | Get/SetFsm* family | ~7.8k | 1056 GetFsmFloat, 380 SetFsmFloat, 178 GetFsmGameObject, 139 GetFsmInt | Unity compare + string compare + dict hash; GetComponents + warning on target change; alloc on missing var | R10 |
| 6 | ActivateGameObject | 1632/860 | 43 | SetActive → enable/disable of every FSM in subtree | R5 |
| 7 | GameObjectHasChildren | 1216/441 | 1178 | childCount + full Fsm.Event every frame (vehicle slot polling) | content (plan §4) |
| 8 | Wait/RandomWait/NextFrameEvent | 2406/274/1895 | – | trivial but keeps FSM awake | – |
| 9 | GetButtonDown (+Up 109, GetButton 29) | 1589/665 | – | linear search of 53 key names per frame per active action; exception per frame if name missing | name→key dictionary invalidated on rebinding |
| 10 | PlaySound | 1070/939 | – | `PlayClipAtPoint` creates/destroys GO+AudioSource per call | pooled one-shot sources, identical setup |
| 11 | CreateObject | 531/446 | – | Instantiate → reflective action loading | R9 |
| 12 | TriggerEvent | 1504/886 | – | trigger-name alloc per FSM per callback; unhandled system event | R7 |
| 13 | GetComponent | 1426/801 | 0 | ~0.1–0.2 µs per state entry | low |
| 14 | UiTextSetText | 4533/839 | 406 | cheap; per-frame new strings from ConvertFloatToString (191 eF)/StringAppend | ConvertFloatToString: reuse last string when value+format unchanged |
| 15 | Raycast | 793/354 | 211 every 2nd frame | physics + layer-mask rebuild | cache mask |
| 16 | GetChildCountAdvanced | 313/125 | 313 | per-frame child loop; logs error every frame when target null | rate-limit error |
| 17 | MousePickEvent | 239/61 | 239 | Camera.main + ray + raycast; one-entry cache | per (frame, mask, distance) cache |
| 18 | DestroyComponent/AddComponent | 907/615 | – | component calls; destroying FSMs hits R5 | – |
| 19 | GetName/StringAppend/SetName | 880/411/389 | – | string alloc | content |
| 20 | SensorGetDetections (Micosmo) | 210/57 | 210 | per NPC per frame; "All" mode (6) LINQ allocs | alloc-free All path |
| 21 | ES3 Load/Save (+LoadAll/SaveAll 170 each) | 511/500 | – | 398 use File location: every call re-opens and parses the whole save | save-system: one ES3 cached file per save/load |
| 22 | FindGameObject | 121/50 | – | global GO scan, ms-scale | name cache validated by alive+name+active (shadow: Find order unspecified for duplicates) |
| 23 | iTweenRotateTo | 154/135 | – | Hashtable + component add | – |
| 24 | SetMaterial | 176/92 | – | `renderer.material` clones material per call | content decision |
| 25 | CallMethod | 107/74 | – | Invoke + boxing | compiled delegate |
Cheap, no work: FloatCompare, IntCompare, FloatAdd, Get/SetPosition, GetParent, GetOwner, BoolTest, SetLayer, SetTag, SetParent, GameObjectIsChildOf, GameObjectCompareTag.

## 4. Idle-FSM overhead and "FSM sleep"
### 4.1 Cost
Enabled FSM in a finished state with no active actions pays per frame: native→managed Update (~0.1–0.3 µs) + `PMF.Update` (PMF.cs:426-432) → `Fsm.Update` (Fsm.cs:2184-2210): owner null check, push, `UpdateDelayedEvents`, ActiveState getter, `FsmState.OnUpdate` (348, early return), `UpdateStateChanges` (all states), pop ≈ 25 calls, 0.1–0.25 µs. States holding non-updating active actions (TriggerEvent) also pay `StateTime +=`, a virtual call per action and `CheckAllActionsFinished`. **Total ≈ 0.2–0.5 µs per idle FSM per frame → 1–2.5 ms at N=5000.** Statically 10,923 of 25,148 states contain per-frame work.
### 4.2 Sleep design (keeps Unity Update order; no central loop)
- Flag = the FSM's own `ManualUpdate` (PMF.Update already skips Fsm.Update). Vanilla never sets it; record FSMs that start with it set and never touch them. It is serialized: clear it in postfixes on `PMF.Awake`/`OnAfterDeserialize` unless originally set (Instantiate of a sleeping object copies true). Alternative side-table Update prefix costs a 20–40 ns lookup — eats most of the gain.
- Sleep condition (postfix on `Fsm.Update`): started, not finished, active state entered, not switching, no breakpoint, has active state; no pending delayed events (AccessTools FieldRef to private list); no active action whose type declares its own OnUpdate below FsmStateAction (static cache; FsmStateActionAdvanced counts as overriding); finished-actions list empty; and either the state's private `finished` flag is set, or there are active actions and at least one blocks finishing.
- FixedUpdate/LateUpdate proxies call Fsm.FixedUpdate/LateUpdate directly — unaffected.
- Wake hooks (clear flag): (1) `Fsm.ProcessEvent` (all routes incl. other mods, UI proxies, delayed events); (2) `Fsm.Event(...)` overloads (wake sender); (3) `Fsm.SwitchState` (SetState, GotoPreviousState, ReEnterState, transitions), `Fsm.Start`, `Fsm.OnEnable`, `Fsm.Stop`; (4) `Fsm.DelayedEvent(...)` both overloads — wake the **sender** (delayed list lives on the sender); (5) `FsmStateAction.Finish()` (actions finishing from callbacks/coroutines; FINISHED fires on next Update); (6) physics/other callbacks `Fsm.OnTrigger*`, `OnCollision*`, `OnParticleCollision`, `OnControllerColliderHit`, `OnJointBreak*`, `OnAnimatorMove/IK`, `OnGUI`; (7) public `FsmSleep.Wake(fsm)`/`WakeAll()` for mods that mutate actions/delayed events directly, called on scene load and config toggle.
- Not wake sources: variable changes (idle state reads nothing), action coroutines (Unity scheduler), Fixed/LateUpdate proxies. A woken still-idle FSM re-sleeps after one Update.
- Semantic differences: `StateTime` stops while asleep (only GetTimeInfo/BlockEvents read it, unused; can add elapsed on wake); `ResetLoopCount` skipped (counts only grow on state entry = wake path).
- Shadow mode: don't sleep; for would-sleep FSMs snapshot active state, active-action count, finished flag, delayed count before Fsm.Update, assert unchanged after with no event sent.
### 4.3 Native Update call
Not avoidable per instance (Unity registers Update per class; disabling fires OnDisable; ManualUpdate + central driver keeps native call and changes ordering vs NWH). Sleep removes the managed part (~40–60% of idle cost). Removing native dispatch needs an Update-less PlayMakerFSM subclass swapped at prefab level — not phase 1.

## 5. Instrumentation (debug switch)
Every 5 s: `FsmList.Count`; sleep-eligible/asleep; SendEvent/s by target type; `fsmList.Remove` calls + avg scan length; actions loaded per Instantiate; PlayMakerGUI.OnGUI calls/frame; GetGameObjectFsm warnings; GetButtonDown key-name misses. ProfilerMarkers/Stopwatch accumulators for event dispatch, OnDisable/OnDestroy, PlayMakerGUI, FsmProperty, action loading. Shadow mismatch counters per replacement in the F7 page.
