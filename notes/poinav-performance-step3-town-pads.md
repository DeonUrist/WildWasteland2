# PoiNav step 3 and town foundations (2026-10-21, Testing)

Denis requested step 3 of the performance plan, then explicitly requested planning
town placement and flattening its area before placing buildings. This supersedes
the previous no-town-terrain-writes policy with Vistas. Steps 4 and 5 are untouched.

## PoiNav projection

PoiNav.Build now queues terrain-node projection. A named NPC scheduler sliced job,
PoiNav.Build, has a 500 microsecond budget, with shared per-frame ceilings of 64
terrain rays and 256 visited nodes. Each step handles one node; an individual ray
is indivisible, so the time budget is not a strict latency guarantee. Graph creation,
asset parsing and initial world-coordinate transformation remain synchronous.

Instances are not eligible for routing or the navigation viewer until projection
finishes. During preparation, Nav.Next falls back to the grid maps; the Inside
check still recognizes the pending POI to preserve travel's floor-aware arrival.
Cached movement rejects rebuilding instances. Unloaded roots are discarded from
the queue; rescanning retries interrupted instances. Reset clears the queue/caps.
Floating-origin shifts translate pending world positions along with ready ones.

Files: NPC/PoiNav/PoiNav.cs, NPC/PoiNav/PoiNavView.cs, NPC/NpcModule.cs.

## Town foundations

Previously Vistas mode sampled only a building's entrance, leaving slopes free to
rise through the rest of its footprint. Layout planning already reserves the full
lot/corner/parking/station extent inside TownPlan.Radius. Before construction,
NewSite takes the actual applied terrain height at the planned town center, aligns
lot entrances to that level, rebuilds the walk layout and flattens the full town
disk, with at least 8 m of feather beyond it. Existing XZ identities are retained.

TownGroundPad validates terrain bounds/height range before writing. It captures
the resulting owned core/feather samples and replays only those samples after the
existing Vistas TerrainEdited event, retaining current terrain outside its mask.
Repeated callbacks use the captured targets rather than repeatedly blending the
feather. Terrain identity/resolution/size guards reject stale targets. Subscription
is removed on shutdown; site/world removal releases the pad with its site.

No Vistas access roads are added. Non-Vistas generation keeps its existing worker
town-pad stamping. Gas stations outside towns and the starting-tile seam are not
changed. The bounded height update is synchronous and may itself have a loading/
grading cost; it is not covered by the PoiNav projection budget. Existing loaded
towns need to reload/rebuild their sites to use the new placement. No guarantee is
made about every authored mesh's below-entrance foundations or interiors before
Denis tests them.

Files: Towns/TownGroundPad.cs (new), Towns/TownData.cs, Towns/TownPlanner.cs,
Towns/TownsModule.cs. No Core/config/save-format/asset edits. Runtime indices and
configs/saves remain unchanged. Prior unrelated generated-LOD work is retained.

## Build and handover

Windows Release/Deploy=false: zero errors, two existing Route.GlideAt/Glides CS0649
warnings. No game, harness or profiler was run. No commit/push/release/Verified.

Stage: V:/Repos/Apocalypter/_releases/WildWasteland_poinav-step3-town-pads_20261021_135440_668.dll
MD5: 6CF5B76DC99ED3CCAD8B3F0D7CF87FB3

NOT DEPLOYED: Apocalypter was running. Close the game before installing this DLL;
recheck disabled manifests, matching runtime index/assets and installed hash.

Denis tests:

1. After installing, first visit a large town or Camp_7/Camp_15. Compare first-load
   frame time and inspect the NPC.PoiNav.Build scheduler job. Grid navigation may
   run briefly before ladders/climbs become available. Check streaming/reload and
   floating-origin travel while projections are pending.
2. Reload/generate a town with Vistas. Require `[Towns] foundation prepared before
   buildings: <key>; radius ... m`. Inspect all building entrances, uphill walls,
   parking and its station, plus the feather edge. Expect level ground prepared
   before placement, without hills crossing the ground floors.
3. Let Vistas finish road grading, then leave/return, save/reload and toggle Towns
   off/on. Check that buildings remain supported and ground stays out of rooms.
   Check a non-Vistas town as well. Geometry, frame cost and pad appearance remain
   unverified until Denis reports back.
