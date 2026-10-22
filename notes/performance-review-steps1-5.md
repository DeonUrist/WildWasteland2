# Review of performance steps 1–5 (2026-10-21)

Follow-up: Denis requested all four findings fixed. See
[performance-review-fixes.md](performance-review-fixes.md) for the implementation,
deployment and pending runtime checks. Findings below describe the reviewed prior
snapshot, not the subsequent fixed source.

Scope: current combined source, read-only review; no fixes or new build/deployment.
Installed DLL MD5 checked: DE342A69A8F1C65CA4FB3A9F237CECB9, matching the last
entrance-routing handover. Available game log contains no before/after profiler
measurements establishing the FPS gain. No game/harness/profiler was run.

Decision: expect a substantial reduction in the targeted recurring CPU work, but
do not declare the regression resolved. Fix the focused issues below, then measure
before making wider optimization changes. A* call-count/raycast-count ceilings
are not CPU millisecond ceilings or promises about GPU-limited FPS.

## Findings, all P2

1. Cached movement expires before a valid distant NPC think. PoiNav.SteerDir and
   Walk reject guidance older than 0.75 seconds (PoiNav.cs:219,244). Idle's base
   think period is 0.2 seconds, with scale4 beyond80m (Idle.cs:104,344), yielding
   0.8-second thinks. Early thinks only help when near a waypoint. A long walking
   leg can therefore expire between valid thinks; Walk releases its walker, and
   the next think recreates it, including collider arrays/lists and IgnoreCollision
   setup. This threshold previously survived because per-frame TryNext refreshed
   Guided. Make cache validity account for scheduled think intervals and invalidate
   explicitly on stopped/changed routes rather than expiring a valid far route.

2. A* slots are spent repeatedly on discarded direct paths. PoiNav.cs:541–545 runs
   Path before detecting a link-free, same-surface, clear route, then clears Steps
   and returns grid fallback. Subsequent TryNext considers empty Steps unusable
   regardless of unchanged GoalNode and repeats after0.5s. This is bounded but can
   consume the entire two-search budget with ordinary direct walks. Cache known
   fallback outcomes with appropriate invalidation/cheap visibility revalidation.
   Also record deferred work and use fair scheduling if profiling shows backlog;
   the existing cap has no FIFO fairness guarantee and each search is synchronous.

3. Step3 does not bound all first-use construction. Rescan -> PoiNavStore.Get still
   synchronously reads/splits/parses missing types. Build immediately calls GraphOf,
   whose Graph constructor runs NearestNode twice per link; that method scans every
   node. It does not use the newly built graph buckets. Repository assets imply:
   Camp_7:5795 nodes/321 links,3,720,390 endpoint candidate visits; Camp_15:5191/496,
   5,149,472; Building_4:5061/383,3,876,726. These are static loop counts, not timing
   measurements. All instance coordinate transforms are also synchronous. The
   sliced500us/64-ray job covers terrain projection only. Extend bucket use to graph
   link attachment and stage the remaining first-use work before calling loading
   hitches solved; prioritize according to a cold-load profile.

4. Step5 removes exterior perimeter detours for outside-to-outside travel. In
   TownsModule.cs:595–598, an outside NPC with an outside goal now skips the town,
   even if the segment crosses its walls. Previously Walls.Walkers could route
   around it. Ordinary NPC movement/feelers remain, but the special detour is gone;
   a passer-by can be sent toward the compound and may stall at a long wall. Keep
   entrance-first behavior for inside destinations and add cheap cached exterior
   avoidance only for journeys intersecting the town. Test routes crossing towns
   and routes passing beside them; do not reinstate full town graph work for every
   outsider.

## What is established by source

- Step1 removes TryNext and its route-tail walks/linecasts from SteerDir/Walk;
  movement reads a cached point and uses a reused RaycastNonAlloc buffer.
  Floor rays remain per moving NPC/frame, as intended. Think-rate route queries
  should be dramatically fewer than twice-per-movement-frame queries.
- Step2 enforces a0.5s minimum between plan attempts per retained route and at most
  two PoiNav A* starts per frame. Runtime start/goal scans use2m buckets for upright
  roots; tilted roots deliberately use the exact full scan. Long-range ground
  queries still visit occupied buckets. Nearest-goal queries are not themselves
  spaced0.5s once Planned is old and the endpoint remains stable.
- Step3 caps projection across instances and withholds incomplete instances from
  routing/viewing; loading is not universally sliced (finding3).
- Step4 admits registered interior groups only within50m of group origin and
  spaces creation sequences0.15s globally. It removes the three reflective senses
  prefixes in favor of typed flags. Existing saved/already-spawned enemies are not
  removed when distant. Pending-group scans and hearing's all-agent scan remain;
  investigate their measured cost before replacing them.
- Step5 rejects irrelevant towns before structure lookups/internal graph queries,
  and only uses the street/perimeter graph after entering the contour. Bounds,
  contour and four-gate selection still cost a little outside; vehicle routing
  was outside this change.

The additional town foundation feature is a separate potential hitch source:
TownGroundPad.Apply allocates a bounded GetHeights array on each Vistas callback
and synchronously calls SetHeights when restoration is needed (lines45–51). Profile
preparation/late grading separately; it is outside PoiNav.Build's budget. Town
rendering, physics and sustained simulation of visited populations also remain.

## Next decision and measurements

First fix findings1 and4 for correctness, and2 for avoidable search pressure.
Address3 to finish first-load work, preferably guided by its timing. Then Denis
compares the same scene/save/NPC counts/settings in a Camp_7/Camp_15 fight, large
town walk, and cold entry to a new area. Record median and worst/99th-percentile
frame time, CPU/GPU limitation, A* starts/time/deferred count, PoiNav.Build time,
interior spawn/hearing time and foundation-grading time. Some routing work still
runs in FSM hooks; the scheduler Performance page alone cannot attribute all of it.
Use ModProfiler traces for those paths. No runtime improvement percentage or
restoration to1.0.4 performance is established by this review.
