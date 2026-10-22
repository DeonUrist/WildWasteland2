# Four performance review fixes (2026-10-21, Testing)

Denis requested all four findings from performance-review-steps1-5.md fixed.
Six source files changed; no Core/config/save-format/asset changes. Earlier steps,
town foundations and unrelated shared-tree work are retained.

## 1. Guidance lifetime

Removed the fixed0.75s guidance expiry from movement and Guiding. A cached waypoint
remains usable while its route/instance is valid. Query entry invalidates its prior
answer; arrival/drop/rebuild/inactive or destroyed root/module gates also invalidate
it. Idle.Stop invalidates guidance when an active walk stops, and Brain.SetMode
invalidates when leaving deliberate movement. StopGuiding immediately restores a
walker's physics unless traversal owns it. The existing0.25s undriven-walker cleanup
remains. An0.8s distant think interval no longer expires an otherwise valid route.

## 2. Direct-walk fallback cache

After a search proves a link-free same-surface visible leg, Route records that
surface. Later queries reuse ordinary navigation without another A* while the
selected endpoint/instance/graph match, the current feet remain on that surface
and a visibility check remains clear. Runtime buckets locate the feet node.
Changed endpoint, floor, visibility, instance/graph or a player ladder causes normal
replanning under the existing0.5s/two-search cap. Drop clears the fallback marker.
This removes repeated searches of an unchanged direct leg; the first confirming
search and cheap think-rate revalidation remain. No unreachable-path cache added.

## 3. First-use construction

Runtime Rescan uses PoiNavStore.GetAsync. Main-thread code captures candidate paths;
serialized worker work reads/parses plain POI data and calculates numeric bounds.
Scene APIs, Transform work, logging and cache publication remain on the main thread.
Config-before-shipped precedence and cached misses remain. Explicit editor/baker
Get remains synchronous. Forget/reset/synchronous overrides cancel pending
publication; stale tasks cannot overwrite a newer editor/save/world cache. An
already running read/parse may finish, but its cancelled result is discarded.

Graph construction is now phased through PoiNav.Build's existing sliced job:
one bucket/adjacency node, one walk edge or one traversal link per step. Link
attachment queries2m local buckets with the original2.5m/weighted-Y/surface rules
and lowest-index tie behavior, replacing full-node endpoint scans. Each instance's
TransformPoint work now runs in that job alongside terrain projection. Shared graph
construction completes before instance projection/readiness is published. Pending
origin shifts translate only initialized world nodes; later nodes use current roots.

The nominal500us sliced budget and64-ray/256-work-unit frame ceilings remain shared
across instances. Array allocations and the lightweight root inventory still occur
on the main thread; each individual step is indivisible. A* itself remains
synchronous, capped by search count rather than elapsed milliseconds. No claim of
zero hitches or a measured frame budget is made.

## 4. Exterior passer-by detours

TownApproach precomputes an exterior rectangle12m beyond the town frame and its
four-corner perimeter costs. Outside-to-outside journeys whose segment crosses
wall runs select a visible exterior corner route and cache it per owner/destination.
Subsequent queries advance/reuse it, or discard it when a direct leg becomes clear;
goal changes/obstructed corner legs cause replanning. Numeric contour tests only:
no town structure lookup, physics cast or internal street/perimeter walker query.
Normal NPC navigation/feelers handle ground and loose obstacles along that leg.
Dead owner entries are swept when the cache grows, the cache is bounded and dies
with its TownSite. Coordinates remain map-anchor-relative through origin shifts.

The broad rejection also checks the journey segment's distance to the town when
both endpoints are distant, so long crossing journeys are not missed. Unrelated
journeys still skip town work cheaply. Inside destinations retain entrance-first
behavior; inside NPCs retain internal routes. Vehicle behavior is unchanged.

## Build/deployment and Denis's checks

Windows Release/Deploy=false: zero errors, two existing Route.GlideAt/Glides CS0649
warnings. No game/harness/profiling/commit/push/release/Verified promotion.

Stage: V:/Repos/Apocalypter/_releases/WildWasteland_perf-review-fixes_20261021_144014_101.dll
Built/staged/installed MD5: 6AD5D96F0C587217A14FF710BAFAB7DF.
Game closed; both base disabled manifests absent; normalized index matches; all361
indexed assets present. DLL-only deployed, prior DLL at the stage's .before.dll.
Index/assets/configs/saves unchanged.

1. Observe residents walking more than80m away: expect no periodic physics/route
   reset between distant thinks. Stop/resume orders and transition into/out of
   combat; expect no stale waypoint movement.
2. Walk/fight along a direct same-floor POI leg, then turn behind an obstruction,
   change floor and climb a ladder. Expect ordinary direct motion while clear and
   proper replanning when conditions change. Profile A* calls/time to quantify.
3. First enter Camp_7/Camp_15/Building_4/a large town. Navigation initially uses
   grid fallback while loading/building. Inspect PoiNav.Build and first-entry frame
   times. Reload/change worlds or travel across an origin shift while work is
   pending; no stale data/partial graph should become visible.
4. Send an NPC from outside one side of a walled town to an outside destination on
   the opposite side, including both endpoints far away. Expect an exterior detour.
   Check adjacent-side/corner journeys, changed orders, entrance-first travel and
   ordinary routes alongside a town. Props/terrain and real gate clearance still
   need Denis's runtime test.

No new successful-operation log lines were added. Background read/parse failures
log `[NPC] POI navigation file ... ignored:` or `POI navigation load failed:` on
the main thread. FPS improvement remains unmeasured.
