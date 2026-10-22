# Adversarial review of notes/fix-NPC-perf.md (the lazy-Dijkstra / field-cache / scan changes), 2026-10-14

Tree: /home/claude/ww/head, uncommitted diff against the "base" commit (48ccc3b): `src/Modules/NPC/` Navigation/Nav.cs, Brain/ReloadSounds.cs,
Animals/Burrow.cs, Debug/DebugOverlay.cs, Detection/Senses.cs; harness `tools/nav-equivalence/` (untracked). Read: the whole diff, the whole
of Nav.cs new and old (`git show base:src/Modules/NPC/Navigation/Nav.cs`), the harness, the callers of Nav.Next (Brain, SimpleBandits, Idle),
GameRefs, GunsModule's rig lookup, Burrow.FindEffect. Build: `build.sh` zero errors, the 2 known CS0675 warnings. Harness extended (section 3)
and re-run: 10 seeds x 400 pairs, RESULT: equivalent (numbers in section 3).

**Verdict: no correctness bug found in the code; nothing changed in `src/`. The claim "every settled Dist value and every Next answer equal
the full build's" holds by argument (section 1) and under a harness that now also covers the relaxed exit, the 16/32 eviction, a lowered
MaxStep and a shared moving goal. The one non-identity the author declared (which cached field a think gets, within the existing 2.9-cell
tolerance) is real and now measured (section 2.1): it is a cache-population effect, not a distance error.**

## 1. Checked and found correct (ordered by how hard each was to convince myself of)

### 1.1 Lazy Dijkstra readers (brief item 1)
The invariant `Dist[i] <= Settled  =>  Dist[i] final` holds: edge costs are > 0 (c1/c2 + NearCost/HopCost, all added), the binary heap pops
keys in non-decreasing order, stale entries are skipped before the `i0 == until` test, and the asking cell's neighbours are relaxed BEFORE
`Expand` returns (Nav.cs 1356-1370: the `return` is after the neighbour loop), so a resume never skips relaxations. A cell with tentative
`Dist == Settled` is final too (any later relaxation gives nd > Settled). An unsettled cell's Dist is NOT distinguishable from +inf by a
reader, so every reader was traced to the expansion it needs:
- `Next` 762 / 775, `OutNext` 952: `FieldFor/ExitField(.., until: from)` then `Dist[from]`: `from` is popped or the heap is drained (then
  +inf is the true answer; the old full build gave +inf too).
- `Downhill` (Next's loop, `Descend`, `RelaxedNext`): reads the 8 neighbours of a settled `cur`; a neighbour with final value < Dist[cur] <=
  Settled has been popped (its predecessor was), a neighbour with final >= Dist[cur] has tentative >= final so is never picked on either
  side; the strict `<` scan order is unchanged, so the pick is identical. Each next `cur` has Dist < previous, so it is settled too.
- `GridSight`, `OutClear`, `Move`, `Step`: read no distances.
- `FieldFor`'s tolerance test 1306: `Settle(s, f, goalCell)` expands up to the goal cell first (drains the heap when it is unreachable, as
  the old full build was), then `Expand(f, until)`.
- `PickOutExit` 1000, `ExitCell` 1072 (+ `Dump(mine.Dist)`): `ExitField(.., 3, -1)` = full (`H == null` after).
- `ExitCell`'s relaxed `rel` 1097, `RelaxedNext` 1230, `TraceDump` 864-865: the 4-argument `Build` = full; `Dump` on "baked" gets null.
- Nothing outside Nav.cs reads `Field`/`Fields` (grep: Services.cs only mirrors Root/Box/Baked/FloorY/W/Cell; Patrols reads FloorY only).

### 1.2 Heap resume and pools (brief item 2)
- A heap is in exactly one place: a live partial field's `H`, or `_heaps` (pushed only when `Expand` completes - `H = null` first - or
  `Return` runs, which nulls `H`), or garbage (`_heaps` full). `Build` pops one and `Clear()`s it. No aliasing.
- Eviction while mid-expansion is impossible: `Expand` is synchronous, nothing re-enters Nav; the eviction code runs before `Build` and only
  touches entries other than the one being built. Every caller that holds a `Field` across another `FieldFor/ExitField` call reassigns it
  (`Next` 762 -> 774, `OutNext`'s attempt loop); `ExitCell` and `PickOutExit` hold `mine` across no cache call (`Build` relaxed uses only
  `_pool`, never `s.Fields`).
- `_relax` / `_noHop` on resume: `FieldFor` and `ExitField` (the only places that cache lazy fields) are never reached under either flag:
  `_relax` is set in try/finally around the three full `Build`s (1095, 1229) and around `RelaxedNext`'s Downhill/GridSight (1235); `_noHop`
  around `PatrolPointsNoHop` (ClearLine, no fields). A field in `s.Fields` was built and is resumed with both false.
- Arrays replaced under a field: there is no re-bake. `BeginBake` runs only while `FloorY == null`; a failed bake sets `Baked = true,
  FloorY = null` (Next skips it); a resumed bake keeps its arrays and is not `Baked` (fields exist only for baked structures: Next 730).
  "8 new floor collider(s)" is `_floorCols` for the feelers' IsFloor, not a grid refresh. `Shift` is the only FloorY change and it Returns
  and clears every field (1772-1776). `OnSceneLoaded` clears `_structures` (fields die with them) plus `_pool`/`_heaps`.

### 1.3 Component short cut (brief item 3)
`ComponentsStep`'s `Move` = `Step(a,b)` + on a diagonal `Step(a, (bx,az))` and `Step(a, (ax,bz))`; `Expand` 1363-1364: `Step(i0,j)` +
`Step(i0, (cx,z))` and `Step(i0, (x,cz))` - the same two corner cells. `EdgeKind` is symmetric (it normalises the pair), both run with
`_relax = _noHop = false`, `Phase == 2` and `Edges != null` for every baked structure. Flood-fill reachability == "Dijkstra gives a finite
value" (every relaxation is finite). `Comp` is always non-null for a baked structure with `FloorY` (phase 2 allocates it); a baking one is
never routed on; `from` and `goalCell` are walkable grid cells (NearestReachable / NearestWalkable).
The `StepAtBake` guard is CONSERVATIVE, not needed for correctness: EdgeKind(S) for a live step S is `walkStep(S) && bit` or a hop bit;
lowering S only removes edges (Comp "disconnected" stays true), raising S changes nothing (walk bits only exist for pairs within the bake's
step, hop bits are independent). So the short cut would be valid at any live step; the guard merely costs a field build after an F7 lowering.
The only hole in Comp-vs-Build agreement at all is MaxStep changed DURING a bake (edge bits baked with two values), which both the old
and the new code share and which the guard does not address - F7 tuning while a bake runs, negligible.

### 1.4 Exact key space 3 and FixedLife (brief item 4)
Keys cannot collide: inside goal keys use bits 0-39, outside goal keys set bit 40 (+0-39), space 2 = bit 41, space 3 = bits 40+41 with the
cell in bits 0-18 (W*H <= 360 000). `Build(s, goal, true, from, -1)` ignores `goal` when inside (1331), so the key-3 field equals the old
private `Build(s, goal, true, from)` bit for bit (the harness's warm/eviction/relaxed runs compare the exits picked from it). A key-3 / key-2
field depends on FloorY, Edges, Near, HopCost/NearCost and the live MaxStep only; of those only MaxStep can change live (a 5 s instead of
1 s window of mixing, the old cache had the same kind of window). The expired sweep still uses FieldSeconds, so a busy cache evicts
fixed-cell fields after 1 s (cost only).

### 1.5 ReloadSounds / Burrow / overlay (brief item 6)
- ReloadSounds: `GameRefs.PlayerCameraHolder` is `GameObject.Find("PlayerCameraHolder")` (1 s retry, Unity-null safe, so a destroyed holder
  falls back to the old scan); GunsModule finds the rig as PlayerCameraHolder/PlayerCamera/WeaponsArm/Parent, the same tree. `GetComponentsInChildren(true)`
  includes inactive guns like FindObjectsOfTypeAll did; the filters (AnimSound, scene object, name == key, WeaponsArm ancestor, first match)
  are unchanged. Only ordering among duplicate `<key>` objects could differ (author's note). A gun FSM with a WeaponsArm ancestor OUTSIDE the
  holder would no longer be found - none exists in the game's layout (Tracers.cs 806 relies on the same fact).
- Burrow: `_effect` is a prefab asset (`!scene.IsValid()`, no parent), `_clip` the clip its own SetAudioClip references; both are kept only
  while `_effect` is alive (Unity-null check), `_nextFind` still reset, `Fx` tests `_clip != null` before use. No behaviour change.
- Overlay: the 200 m cull is by camera depth in Mark/Label and by Euclidean distance in Senses.DrawDebug (depth <= distance, so anything
  within 200 m of the camera is still drawn by both); Nav's structure labels keep their 150 m player cull. `_dead` list reuse is safe
  (single-threaded, cleared per frame). Debug only.

### 1.6 Build / allocations (brief item 7)
mcs (C# 7) builds; definite assignment of `f`/`d0` is fine. Hot paths: `Next`/`FieldFor`/`ExitField`/`Expand` add no allocation; `Build`
allocates the `Field` object as before and a `Heap` only when the pool is empty (a new one per concurrently-live partial field, see 2.2).

## 2. Risks that remain (none is a distance error)

### 2.1 Which cached goal field a think gets (declared in fix-NPC-perf.md section 3) - CONFIRMED, measured, left as is
`FieldFor` shares a field between goal cells within 2.9 cells by path. The new code populates `s.Fields` differently (no entry for a goal
in another area; key-3 entries per NPC cell), so the 16-sweep / 32-clear moments differ from the old code's, and then one side reuses a field
seeded at G0 while the other rebuilds one seeded at G1 (|G0 G1| <= 2.9 cells). Harness phase "evict 2" (16 NPCs chasing one goal jittering
inside its 4 x 4 bucket while 32 others churn the cache with goals in other areas / outside): 3 such thinks in ~4 000 (0.1 %) over 10 seeds:
pathLeft differed by 0.3 m, waypoint identical in all three (a 2-seed run with 100 pairs showed 0.2 and 1.2 m, same waypoints); a different waypoint is possible in principle (the two fields descend from different
seeds). The control phase "evict 3" (the same churn with every goal in the NPCs' own area, so both caches evolve alike) shows 0 differences
over the same load, which pins the effect on cache population, not on the lazy distances. It is within the tolerance the pre-change code
already applied between NPCs; in play it needs 32 distinct goal buckets on one structure within FieldSeconds. If it should go away: keep
the key-2/3 fields in a separate per-structure dictionary (own 16/32 limit) so the goal-field cache evolves as before - the short cut's
non-insertion would remain the only difference.

### 2.2 Memory: one heap per live partial field
Before: one heap for the whole mod. Now every partial field in a cache holds its own (initial n/4 entries x 8 bytes: ~80 KB on a 40k-cell
camp, ~0.7 MB on a 600 x 600 grid), up to 32 per structure until it completes or expires (1 s goal fields, 5 s fixed cells), plus the
8-entry pool. Worst case on the largest grids ~23 MB transient per structure; typical camps a few hundred KB. Bounded; not seen as a problem.

### 2.3 StepAtBake guard direction (see 1.3)
Unnecessary but harmless; could be dropped to keep the short cut on after an F7 lowering. Not changed (behaviour-neutral either way).

### 2.4 ReloadSounds "none found" is cached per key for the session (pre-existing)
If the holder exists but the gun object is not under it yet when the first NPC reloads, an empty list is cached; the old scan had the same
rule (scene objects only), so not a regression - noted because the new path depends on the holder being the gun's ancestor.

## 3. Harness: gaps found and what was added (tools/nav-equivalence/NavCheck.cs)
Covered before: hop edges (70 % of eligible pairs), thin walls with gaps, a sealed island (own area), inside / blocked / outside goals, the
exit-cell path (ExitCell + exit field), OutNext/PickOutExit (outside goals, blocked lines), warm caches with 24 NPCs. NOT covered before:
- the RELAXED exit (`ExitCell` open == 0 && !edge -> relaxed Build -> `RelaxedNext`): Open was random 80 %, so no sealed roofed area existed.
  Added a roofed room sealed by a ring of no-floor cells (Why 1, crossable under `_relax`), every 8th cold pair starts inside it:
  331 relaxed exits over 10 seeds, all equal.
- EVICTION at 16 and 32 entries: added 48 NPCs with 48 distinct goal buckets thinking every 0.1 s (phase 0: own area; phase 1: goals anywhere
  incl. other areas and outside): both caches peak at 32 (clear-all) on every seed, 0 mismatches.
- MaxStep LOWERED live after the bake (the StepAtBake guard): added a cold block with NavMaxStep 0.12 (bake 0.25) on both sides, 0 mismatches.
- the shared-moving-goal stress and its control (section 2.1), counted apart from mismatches.
- Counters now print relaxed exits, eviction thinks, lowered-step pairs and shared-field differences; phases keep time monotonic.
Still not covered (and why it does not matter for the claim): `_noHop` (PatrolPoints needs Physics.CapsuleCast; it never touches fields);
`Shift`/`OnSceneLoaded` mid-run (pure clears, read in 1.2); `TraceDump`/`Dump` (debug, full builds, unchanged code); the in-game
`Time.time` cadence (warm phases use 0.05 s steps).
Final run: `MANAGED=... BEPCORE=... sh tools/nav-equivalence/run.sh /tmp/wwfix /tmp/WildWasteland_before.dll 10 400`:
68 956 checks: 120 fields, 68 476 Next pairs (4 000 cold, 1 000 of them outside goals, 3 345 routed, 1 240 via an exit; 16 424 warm;
47 052 eviction-phase thinks; 1 000 with MaxStep lowered; 331 relaxed exits), 0 mismatches, 3 shared-field differences (2.1; seed 6,
pathLeft +-0.3 m, same waypoints). Exit code 0, RESULT: equivalent. Wall time ~16 min on this container.
Micro-benchmark unchanged from the author's table (old full Build 19.5 ms, new 19.4; lazy near goal 0.34 ms; Next cold near 14.7 -> 0.30 ms,
far 20.5 -> 11.1, other area 28.3 -> 22.0, outside 48.2 -> 38.1).
