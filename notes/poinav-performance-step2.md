# PoiNav performance: step 2 (2026-10-21, Testing)

Denis requested the second step of the performance plan, following the cached
movement waypoint work. Implementation stays in NPC/PoiNav/PoiNav.cs.

- Goal motion requests a new plan when its selected destination node changes;
  the former 1.5 m displacement and four-second periodic triggers are removed.
  The melee/ranged player-ladder endpoint selection remains.
- Plan attempts, including unsuccessful ones, are spaced by at least 0.5 game
  seconds per NPC. Arrival and ladder reversal preserve the cooldown. Invalid or
  empty routes still retry; changing graph/instance and excessive route deviation
  also request replanning.
- At most two PoiNav A* searches start per Unity frame. Deferred requests continue
  a usable route, or wait at their current position until a later think. This is
  a search-count cap, not a millisecond budget or a FIFO fairness guarantee.
- Graphs cache 2 m local XZ buckets. Nearby start/goal lookups use those buckets,
  preserving original node order, height scoring and start visibility preference.
  Long-range ground-node searches prune occupied buckets with a planar distance
  bound before checking their nodes. Tilted roots retain the exact full scan.
  Buckets share graph lifecycle; retained route cooldowns are swept for destroyed
  owners and reset with the world. No per-query collection allocation is added.

Windows Release/Deploy=false succeeded: zero errors, two existing CS0649 warnings
(Route.GlideAt/Glides). No game/harness/profiling was run. Steps 3–5, config and save
formats are untouched. Existing step 1 and unrelated generated-LOD work are included
in the combined build.

Stage: V:/Repos/Apocalypter/_releases/WildWasteland_poinav-step2_20261021_134732_440.dll
MD5: 47620F4E38E95154B2B6B9CB13BC8AC0

DLL-only deployed with no game process or either base disabled manifest present.
The installed index matches the build after path-separator normalization, and all
indexed runtime files exist. Built/staged/installed MD5 matches; previous DLL is
preserved beside the stage as .before.dll. Assets/index/configs/saves unchanged.
No commit, push, release or Verified promotion.

Denis tests a moving-player fight at Camp_7/Camp_15 and walking through a large
town, comparing ModProfiler/frame time against step 1. Check that NPCs keep chasing
through turns, floors and ladder transitions; under simultaneous demand some may
briefly wait for a search slot, then resume. Check world reload and ladder reversal.
Expected: fewer A* calls as the player moves within the same destination-node area,
with search spikes spread over frames. The cap does not bound the cost of one A*,
instance construction or the old grid pathfinder. FPS and behavior remain unverified.
No new confirming log lines were added.
