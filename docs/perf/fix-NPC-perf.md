# NPC module performance fixes (implements notes/review-NPC-perf.md sections 2 and 4), 2026-10-14

Tree: /home/claude/ww/head (uncommitted, diff against the "base" commit). Build: `build.sh` zero errors, the 2 known CS0675 warnings only.
Built DLL: /tmp/wwfix/WildWasteland.dll (md5 d2557f64e62db49a2a684c5a5c8a31ac); the base build is /tmp/WildWasteland_before.dll.
All changes are inside `src/Modules/NPC/`; 5 files, 71 insertions / 35 deletions; no renames, no setting changes, no new scheduler jobs.
Rule kept: the AI's decisions (routes, cells, timing) are the same; every item is a cost change. The one caveat is in section 3.

## 1. What changed, per item

### 1 Component short cut in Nav.Next (review 2a) - `Navigation/Nav.cs` 757-762, field `Structure.StepAtBake` 45, set in `BeginBake` 300
Before building the goal's field, `Next` compares the bake's area ids: `s.Comp[from] != s.Comp[goalCell]` (goal inside the footprint) means the
field would say `Dist[from] = +inf`, so `d0 = inf` is set directly and the existing "no map route" branch (ExitCell ...) runs as before.
Verified that the rules are identical: `ComponentsStep` (Nav.cs 452) floods over `Move` = `Step(a,b)` plus, on a diagonal, `Step(a, (bx,az))`
and `Step(a, (ax,bz))`; `Build` (1363-1364) uses `Step(i0,j)` plus `Step(i0, (cx,z))` and `Step(i0, (x,cz))` - the same two corner cells. Both go
through `EdgeKind` with `_relax = false` and `_noHop = false`: the bake runs from `Nav.Tick`, never inside `PatrolPoints` (`_noHop`) or the
relaxed builds (`_relax`, both set in try/finally around one call); `Next`'s goal field is built with both flags false too. `Phase == 2` and
`Edges != null` at both times, so `EdgeKind` takes the same branch. The short cut is applied only for an inside goal (the outside-goal field is
ring-seeded; left alone) and only while `MaxStep` has not been LOWERED live since the bake (`StepAtBake`): the edge bits were baked with the old
value, raising it changes nothing (walk bits exist only for pairs within the old step, hop bits are independent), lowering it closes edges the
areas still contain, and then `Comp` and `Build` would disagree. In that one (F7 tuning) case the field is built as before.

### 2 NPC-side field through the cache (review 2b) - `Nav.cs` 1000 (`PickOutExit`), 1072 (`ExitCell`), `ExitField` 971-989
`Build(s, goal, true, from)` ignores `goal` when `inside` is true, so the field from the NPC's own cell is now taken from `ExitField` with the
exact key space `3L << 40 | from` (no 2.9-cell tolerance, so the distances are the very ones the old private build produced) and forced to a
FULL expansion (`until = -1`, both callers scan every cell). The `Return(mine)` calls are gone (the cache owns the array; the memo objects
`_exits` / `_outMemo` never held it). Shared by every NPC standing on that cell and across goal-bucket changes.

### 3 Longer life for fixed-cell fields (review 2c) - `Nav.cs` 969-976 (`FixedLife = 5f`)
Fields in key spaces 2 (exit cell of `OutNext`) and 3 (NPC cell) are seeded at one fixed cell and depend on nothing but the map: they are
valid for `max(5 s, FieldSeconds)` instead of `FieldSeconds`. The eviction sweep (16 / 32 entries per structure) still uses `FieldSeconds` for
"expired", so goal fields are not kept longer by it. NOT changed: the `FieldFor(.., true, exit)` at Nav.cs 774 (the exit field in `Next`'s
no-route branch). It uses the bucketed key with the 2.9-cell tolerance and may legitimately return a field seeded at a neighbouring cell from
the cache; giving it its own exact key would change which cached field it gets (see section 3), so it keeps the old lookup - it only profits from
item 4 (expanded up to the NPC's cell instead of the whole area).

### 4 Lazy Dijkstra (review 4) - `Nav.cs` `Field` 52-54, pools 57 / 71 / 97, `Build` 1323-1341, `Expand` 1343-1374, `FieldFor` 1297-1319, `ExitField`
`Field` gets `H` (its own pooled `Heap`, null once complete) and `Settled` (key of the last popped cell). `Build(s, goal, inside, goalCell, until)`
seeds and calls `Expand(s, f, until)`, which pops until cell `until` is popped (or the heap is empty = whole area, `until < 0`). `Expand` on a cached
field resumes exactly where the pops stopped (same heap state, same push order) so every settled distance is bit-identical to the full build's.
Invariant used by the readers: a cell with `Dist <= Settled` is final (any later relaxation gives `nd > Settled`); `Downhill` only moves to cells
with `Dist < Dist[cur]` where `cur` is settled, so those are settled too; `GridSight` / `Descend` read no other distances. The `Heap` class is now
`internal` (it is a field of the internal `Field`); the one shared `_heap` became an 8-entry pool `_heaps` (returned when a field completes or is
`Return`ed; cleared in `OnSceneLoaded`, `Shift` returns heaps through `Return`).
Readers of `Dist`, classified:
- `Next` 762 / 775 (`Dist[from]` + `Downhill`): `FieldFor(..., until: from)` - partial.
- `OutNext` 951-952 (`Dist[from]`, `Descend`): `ExitField(..., 2, from)` - partial.
- `FieldFor`'s cache test `f.Dist[goalCell] <= 2.9 cells` 1306: `Settle(s, f, goalCell)` expands up to that cell first (so a stale-looking +inf on an
  unsettled cell never causes a rebuild the old code did not do; an unreachable goal cell drains the heap, as the old full build had).
- `PickOutExit` 1000 (sort of every reachable cell), `ExitCell` 1072 (n-cell scan + `Dump(mine.Dist)`): `ExitField(..., 3, -1)` - full.
- `ExitCell` relaxed `rel` 1097, `RelaxedNext` 1230, `TraceDump` 864-865: the 4-argument `Build` = full (they run under `_relax`; a resume under
  other flags would be wrong, so these never become partial).
- `Dump` (area tint) only receives full fields. `Downhill`, `Descend`, `GridSight`: see above.
Cost: the per-pop work is unchanged (`Settled` is written only on return), the full build measures the same as before (section 2).

### 5 FindObjectsOfTypeAll scans - `Brain/ReloadSounds.cs` 26-29, `Animals/Burrow.cs` 46-47
ReloadSounds: `Core.GameRefs.PlayerCameraHolder.GetComponentsInChildren<PlayMakerFSM>(true)` (the guns are under
PlayerCameraHolder/PlayerCamera/WeaponsArm/Parent/<key>; the existing filters - FsmName "AnimSound", scene object, name == key, a "WeaponsArm"
ancestor - stay); the old all-of-memory scan only when the holder is not found. The one-off 30-100 ms spike in the Brain row on the first reload
of each weapon kind per scene is gone. If two different objects named <key> with an AnimSound FSM both sat under WeaponsArm the first one found
could differ from the old scan's order; one per gun is the game's layout.
Burrow: `_effect` (the `Burrower_Effect` prefab asset, not a scene object) is kept across scene loads while it is alive; a Unity-null (asset
unloaded) still triggers `FindEffect` as before, with its once-a-minute retry. Removes one scan per scene load.

### 6 Bake budget: not a bug, left as is
1122 ms / 281 frames = 3.99 ms/frame = `BakeBudgetMs` 1 x 4: `FrameBudget()` (Nav.cs 168-177) multiplies by 4 while `Needed` (an NPC stood inside the
structure and asked for a route before the map existed - the raiders at home in Camp_7 - set by `Next`, cleared when the bake finishes) and the
frame is under 25 ms; x3 with the player at the structure, x2 above 90 fps, x4 while paused, x0.5 below 45 fps. The check itself is fine-grained
(per cell in phases 0 and 1, every 256 ops in phase 2; the overshoot is one cell's raycasts) and `Nav.Tick` is one `EveryFrame` job, one bake at a
time. F7 > NPC AI & navigation > AI overview shows "Bake cost ... (an NPC waits: x4 budget)" while it happens. Changing the multiplier would change
the budget's meaning (how fast a needed map appears), so it stays; the row to read is that one.

### 7 Overlay - `Debug/DebugOverlay.cs` 18-19, 24, 37; `Detection/Senses.cs` 1709-1714, 1721; `Nav.cs` 1418, 1738
`DebugOverlay.Mark` / `Label` return before any GUI work beyond `Range` = 200 m (camera-space depth), the Senses ghost / agent labels skip the
string building for anything farther than 200 m from the camera (the Idle overlay already had that cull), and `Nav.DrawDebug` reuses one list
instead of `new List<GameObject>()` per frame (and reads `Player()` once). Debug only; label text and placement within 200 m unchanged.

Not done (not in the brief): review item 8 (hoist `NpcModule.NavMaxStep.Value` out of `EdgeKind`), ~10-15 % of every build / line walk, pure;
item 5 (`PickOutExit` worst case) - the 6000 `OutClear` walks remain the biggest single cost (section 2, "goal outside").

## 2. Verification

### A. Offline equivalence test - `tools/nav-equivalence/` (NavCheck.cs + run.sh), new
`MANAGED=... BEPCORE=... sh tools/nav-equivalence/run.sh /tmp/wwfix /tmp/WildWasteland_before.dll [seeds=6] [pairs=400]`
Loads the built DLL and the base DLL side by side on plain mono (the base assembly is renamed with Mono.Cecil from BepInEx/core; a copy of
UnityEngine.CoreModule.dll is rewritten so `Time.time` reads a static field the test sets and `Object.GetInstanceID` returns a fake pointer -
the only engine icalls on the tested paths; roots and owners are uninitialised Transform / GameObject objects, no Physics is reached because
NPCs stand on cells of areas >= MinArea). Both sides get the same synthetic structures by reflection (`Nav.Structure`: random walkable / blocked
cells with heights, thin walls with gaps, hop edges, a sealed island = its own area; `Components` of each DLL computes the areas, compared
between the two), 100-200 x 100-200 cells, 10 seeds, config bound like the config harness (defaults: CellSize 0.5, MaxStep 0.25, FieldSeconds 1).
Compared: (i) old full `Build` vs new full `Build` (whole array, bitwise), then the new lazy field after three `Expand` stages (every settled cell
equals the full field, the asked cell is settled, no tentative value below its final one) and after a forced full expansion (whole array);
(ii) `Nav.Next` old vs new: result, next waypoint (bitwise), pathLeft, `TraceInfo()` (structure, cell, area, goal cell / outside, EXIT, path, wp)
and `LastReason` - cold (every call alone, caches emptied, time 100 s apart; goals 3 cells away / random inside / random blocked / outside the
footprint) and warm (24 NPCs with owners thinking every 0.1-0.2 s for 12 s toward a moving player, a fixed cell and outside points, walking along
the waypoints; caches, memos and plans live on both sides).
Result (10 seeds x 400 cold pairs + warm runs): **20 912 checks: 120 fields, 20 432 Next pairs (4 000 cold, 1 000 of them outside goals, 3 372
routed, 839 via an exit cell; 16 432 warm), 0 mismatches.** Exit code 0; any mismatch prints `MISMATCH ...` and exits 1.

### B. Micro-benchmark (same harness, 184 x 227 = 41 768 cells, 30 590 walkable, largest area 28 554; mono on this container, ms per call, 100 calls)
| case | before | after |
|---|---|---|
| full `Build` of a field (what every FieldFor miss cost) | 19.3-20.3 | 20.3 (same) |
| `Build` lazy, NPC 3 cells from the goal | - | 0.33 |
| `Build` lazy, random far goal | - | 10.5-11.2 |
| `Next`, cold cache, goal 3 cells away (incl. straight-line early outs) | 15.5 | 0.27 |
| `Next`, cold cache, random far goal | 20.4 | 10.6 |
| `Next`, cold, goal in another area ("no map route": old = goal field + NPC field + scan) | 29.0 | 21.8 (one full NPC-side build + scan) |
| `Next`, warm, goal in another area, 4 NPC cells, 0.1 s apart | 2.2 | 1.8 |
| `Next`, cold, goal outside, line out blocked (`PickOutExit`) | 49.8 | 37.0 |
Lazy expansion visits the cells within path distance of the NPC instead of the whole area: 50x cheaper for a near goal, 2x for a random far goal
on this grid; the outside-goal pick is still dominated by the `OutClear` walks (review item 5, not touched). In game the Build itself is in the
same 15-25 ms range the review estimated, so these ratios carry over.

### C. `build.sh` zero errors (2 known warnings); `tools/config-harness/run.sh /tmp/wwfix` -> RESULT: OK (139 main / 545 tools entries).

## 3. What is not bit-identical, and why it was left so
- Cache population. `FieldFor` shares a cached goal field with any goal cell within 2.9 cells by path for `FieldSeconds`. Which field a think
  gets therefore already depended on what other NPCs asked in the last second. Item 1 no longer inserts a field for an unreachable goal, item 2
  adds key-space-3 entries (one per NPC cell) that make the 16 / 32-entry eviction run more often, and item 3 keeps exit fields longer: another
  NPC asking for a goal within 2.9 cells of one of those can now get a field seeded at its own cell where it would have reused the neighbour's
  (or the other way round). The distances of any one field are identical; only the cache-sharing choice can differ, within the tolerance the
  code already allows. The warm test (16 432 thinks with shared caches) hit no such case; it is a timing coincidence, not a rule change.
- `MaxStep` lowered live in F7 after a bake: the short cut (item 1) switches itself off for that structure; a lazily resumed field would mix
  the two step values the same way the old cache did within its lifetime (old fields were not rebuilt on a live change either).
- The `ReloadSounds` ordering note in item 5.

## 4. In-game checklist for Denis
1. Build `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`, start, `Loading [Wild Wasteland ...]` in `BepInEx\LogOutput.log`.
2. F7 > Debug & performance > Performance > reset right before a camp raid (same settings as the measurement: FieldSeconds 1, CellSize 0.5).
   After it, F7 > NPC AI & navigation > AI overview > Frame time, compare with the review's numbers (NPC 0.485 avg / Brain worst 61.9 /
   Nav 18.6 / Idle worst below the fold ~268): expected Brain and Idle worst well under half, Nav unchanged (bakes are not touched), Senses unchanged.
   `Overlay` row (only with ShowNavigation on): avg should drop from the 1-4 ms range when NPCs / ghosts are farther than 200 m.
3. Same raid with `[NPC / Debug] VerboseLog = true` once, read the log:
   - `Nav: <npc> has no map route to its goal on Camp_...` - should appear at most once per (NPC area, 4 m goal bucket) per 2 s as before, but
     the frame it lands in must be cheaper (one full build at most, then cached 5 s): watch Brain "last" in the same second.
   - `Nav: <npc> way out of <structure> toward x,z: ... (N/M cells tested, T ms, from here)` - T should be ~25 % lower than before for the same N;
     the N itself is unchanged (same exit chosen).
   - `Reload sounds for <key>: ...` lines still list the same clips as before (compare with an old log: mag_out / mag_in / ... per gun).
   - `Burrow: sand effect found (...)` once per session instead of once per scene load.
   - `Nav: baked ...: ... ms over N frames` - unchanged ms per cell (bake not touched); AI overview > Bake cost shows "(an NPC waits: x4 budget)"
     while raiders wait for a map - that is the 4 ms/frame of the measurement, by design.
4. Behaviour spot checks: raiders inside a camp still route to the player (same waypoints: F7 ShowNavigation cyan "nav" marks), a target on a
   platform / roof still makes them head for the open-sky cell, walkers to a gunshot ghost outside the camp still leave through the same gate.
5. If anything looks different: `tools/nav-equivalence/run.sh` is the first thing to re-run with the two DLLs; it prints the first 40 mismatches
   with cell, area and the TraceInfo of both sides.
