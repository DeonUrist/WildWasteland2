# Wild Wasteland's own lag sources: convoy spawning and NPC logic (#133 follow-up)

Read-only code review of src/Modules/Patrols and src/Modules/NPC on top of 0.1.26 (2026-10-15). Line numbers refer to the #133 working tree. Costs are estimates from the code, not measurements. Before trusting the in-game numbers, re-measure with `[Debug] VerboseLog` off. One convoy build writes 60-100 log lines, and some call sites build their strings even when logging is off (see 4).

## High impact
1. **Collision-ignore pairs: every occupant collider against every car collider, in one frame.**
   - Code: Patrol.cs:315-318 and 402-405 (SeatOccupant); Rider.cs:63-66, 687-688 and 828-829.
   - Cost: 100-300 car colliders × 15-30 NPC colliders = 2,000-7,000 `Physics.IgnoreCollision` calls per occupant, each a PhysX pair re-filter. Roughly 10-50 ms per seating frame.
   - Repeats: `IgnoreCollisionsIfChanged` (Crew every 2 s, Rider every 1 s) re-applies all N×M pairs whenever a weapon prop toggles.
   - **Done in 0.1.26g:** seated drivers, passengers and turrets have no contacts at all; only bullets hit them. `Rider.NoContacts` turns every collider into a trigger and every Rigidbody kinematic; the game's raycasts and WW Guns still hit them, because NPC roots and heads carry Bodypart FSMs. All seat-time and periodic `IgnoreCollision` pairs are removed; the periodic check now only turns newly added colliders into triggers (`Rider.KeepNoContacts`). The old pairs also hid the crew from the car's own CarAttack trigger (layer 10), so `Ram.BeforeSetFsmFloat` / `BeforeSendEvent` now stop a car from ram-damaging anything in its own hierarchy. Pairs remain only for one-off cases: the fresh NPC that jumps off when a rider bails, the thrown lance, and the ejected corpse vs the car.
2. **Full prefab scans on the spawn path.**
   - **Local follow-up implemented, Testing:** exact/case-insensitive name indexes persist until invalidation; FindAny misses no longer rescan. A named warmup job observes scene/ES3/mod-factory changes and direct ES3 list count changes. Template availability also observes the cache generation. Build/deployment/runtime evidence is recorded in `docs/perf-log.md`; the cost estimates below remain unmeasured.
   - Code: `Prefabs.Exact` (Patrol.cs:1221-1246) runs `Resources.FindObjectsOfTypeAll<GameObject>` plus LINQ. A miss rescans every 30 s, and template validation (Templates.cs:64-84) re-checks every 5 s. `Prefabs.Scan` (1281-1316) runs `FindObjectsOfTypeAll<PlayMakerFSM>`, which also deserializes the actions of prefab assets.
   - Cost: 30-150 ms per scan.
   - Fix: build the exact and case-insensitive indexes once per scene during load. A miss stays a miss for that scene; invalidate only when ModVehicles or ES3 register something.
3. **The builder is sliced by item count, not by time, and the scheduler can't see it.**
   - **Follow-up implemented, Testing:** `Spawning/BuildQueue.cs` serializes assembly under `Patrols.Build` (1 ms slice, existing 2 ms whole-frame budget). Parts/cargo yield after each item; native frame/physics waits remain. Once assembled and registered, ignition waits may overlap the next assembly. Scene/module reset cancels requests and releases their callbacks; queued positions retain world coordinates across origin shifts. Instantiate remains indivisible; the slice is not a hard upper bound on a native call.
   - Code: `Patrol.Build` (133-339) instantiates 4 parts, 4 cargo items or 1 occupant per frame. One occupant frame does Instantiate, PassengerGuard, Dress, Pose (`GetComponentsInChildren`) and a PlayableGraph. Convoys and world traffic start a new build every 0.7 s, so builds overlap. It runs as a plain coroutine, with no budget and no F3 attribution.
   - Fix: one global build queue as a `Scheduler.Sliced` job with a time budget of about 3-4 ms; one build at a time; one step per part, cargo item or occupant stage.
4. **The settle frame scans the whole car several times and builds strings even when logging is off.**
   - **Follow-up implemented, Testing:** one short-lived FSM snapshot feeds SetPartConditions/Headlights/RaiderMusic; Crew.Init shares one for Drive/Handbrake/de_Attach. Expensive Describe/per-condition strings are guarded. No persistent hierarchy cache or borrowed Performance type crosses modules.
   - Code: SetPartConditions, Headlights, RaiderMusic and Crew.Init each scan all FSMs with `GetComponentsInChildren<PlayMakerFSM>(true)` (200-400 FSMs). `Suspension.Describe` and per-part strings are built even when VerboseLog is off.
   - Fix: collect FSMs per part at Attach, or use the registry service below; guard log strings with `Log.Verbose`.

## Medium impact
5. **Per-occupant reflection and bone scans** (Passenger.cs:131-160, 222-230, 341-370 plus `GetValue` at 10 Hz; `Rider.AutoSpot`). Fix: cache per action type and per prefab; use compiled getters instead of `GetValue`.
6. **Paint does `Directory.Exists` per material** (Paint.cs:384-391), up to 4 per lookup, in the body-Instantiate frame. Fix: resolve the folder layout once in `Preload`.
7. **NPC registration is synchronous and uncapped.**
   - **Follow-up implemented, Testing:** `NPC.Rigs` queues eligible brains under the scheduler, at most one ApBody.Attach per frame; skips stale/seated/inactive entries and clears on scene/shutdown. Brain/Senses reuse per-registration component snapshots. This avoids assuming identical layouts across female/modded prefab instances; full registration and first glTF loads remain indivisible.
   - Rig building (`Brain.Make` → `ApBody.Attach`: PlayableGraph, AvatarMask, `GetComponentsInChildren<Component>`) happens inside the SetVelocity prefix or `RegisterPending`, all in one frame. Fix: build at most one rig per frame.
   - `Senses.Make` calls `GetComponentsInChildren` 3 times per agent. Fix: cache per prefab.
   - The first Gungirl of each variant loads a glTF in one step. Fix: prewarm at world-ready.
8. **Fight-time costs.**
   - `Pilot.OnCollisionStay` allocates a Collision per contact per physics step. Fix: add it only in the Charge/Travel states.
   - Dusk/dawn `LightsTick` scans every car in one frame. Fix: slice it.
   - `Explode` does about 8 full-hierarchy scans per wreck.
9. **Navigation.**
   - `Nav.BeginBake` allocates arrays up to 600×600 (~2.5 MB) per bake, which triggers GC. Fix: pool them.
   - `Brain.NearestHide` calls `FindGameObjectsWithTag` every 10 s.
   - Field `Expand` has no budget.

Low impact: the Hooks handlers that run for every FSM are already well filtered.

## Performance-module infrastructure for WW
- `Core.Services.Perf`, provided by Performance:
  - `Mark(tag)` for spike attribution (patrol-build, seat, npc-rig, prefab-scan).
  - `FsmsOn(GameObject)`, a view of the FsmRegistry, instead of `GetComponentsInChildren<PlayMakerFSM>`.
  - `Wake(fsm)` / `WakeTree(go)` from FS1, for WW code that changes FSM variables or re-enables FSMs.
  - `PrewarmActions(prefab)` to queue FS4 and FS2 compiles at world-ready, so the first convoy doesn't fall back to reflection.
- `Core.Services.Culling { Protected, BeforeFarDelete, IsActivelyDriven(car) }`: replaces the reflection in PatrolsBridge and lets CarLod (CT2) treat raider cars that are parked, setup-only or wrecked. Today it skips every car with any Patrols component.
- Move per-car and per-occupant MonoBehaviour updates (Crew, PassengerGuard, Pose, RiderGuard) and the build coroutines under Scheduler jobs, so they appear on F3 and respect the frame budget.

## Suggested order
1. Items 1 and 2.
2. Item 3, the global time-budgeted build queue.
3. Item 4.
4. Items 5-9.
5. Re-profile with VerboseLog off and Telemetry marks.
