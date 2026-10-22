# Town entrance routing / step 5 (2026-10-21, Testing)

Denis requested that NPCs going to town first approach an entrance, then use town
navigation only after entering. This extends the original step 5 distance-first
optimization into a two-stage routing policy.

The Towns Nav.Next prefix previously called TownNavigation.Waypoint, then potentially
TownBakeBridge.Root and the perimeter walker graph for every site. The internal
TownNavigation had a radius guard, but the perimeter fallback's Root lookup ran
before its relevance was known, even for unrelated distant NPCs.

TownSite now caches TownApproach: the existing town contour and four cardinal gates.
TownsModule.Waypoint first rejects sites when both position and goal are outside
their radius. An NPC outside the contour whose goal is inside is directed toward
a facing gate's outer point (16 m outside), then the inner gate point (5 m inside)
when aligned with the opening. This is numeric gate selection, with a nearest-gate
fallback for points between the contour and its rectangular frame. It runs no
structure lookup, physics cast or street/perimeter graph query. NPCs outside with
outside goals retain ordinary NPC navigation, including passers-by; Towns no longer
provides their previous exterior perimeter detour.

Once inside the contour, existing street/building and exit routing runs. The bridge
still supplies the intermediate goal to the normal NPC Nav.Next implementation.
If normal navigation returns no local map leg during an approach, the gate waypoint
is retained without an extra TownNavigation.Clear cast. Ordinary brain/Idle movement
and obstacle checks remain. Original destination and remaining-distance accounting
are retained by the bridge. Vehicle routing is unchanged.

There is still small bounds/contour/four-gate selection overhead outside: this is
not a literal zero-cost dispatch. Actual movement still uses normal NPC pathfinding.
No per-query collections, per-NPC route registry, scheduler job, config, save-format
or Core change. Cached geometry belongs to each site and stays relative to its
map anchor, including floating-origin shifts.

Files: TownApproach.cs (new), TownData.cs, TownsModule.cs, TownBakeBridge.cs. Existing
steps 1–4, town foundations and unrelated shared-tree changes retained.

Windows Release/Deploy=false: zero errors/two existing Route.GlideAt/Glides CS0649
warnings. Built/staged/installed MD5: DE342A69A8F1C65CA4FB3A9F237CECB9.
Stage: V:/Repos/Apocalypter/_releases/WildWasteland_town-entrance-routing_20261021_142734_977.dll.
DLL-only deployed with game closed, both base disabled manifests absent, matching
normalized runtime index and all361 indexed files present. Prior DLL retained as
the stage's .before.dll; index/assets/configs/saves unchanged. No game/harness/
profiling/commit/push/release/Verified promotion. No new confirming log lines.

Denis checks an NPC travel order into a walled town from each side/corner: expect
outer-gate alignment, entry through the opening, then the usual streets/building
route. Check a destination change/aborted approach, leaving town, unwalled towns,
outside parking destinations, passers-by alongside and across towns, save/reload
and origin shifts. Pay attention to exterior obstacles and NPCs already between
the contour and its rectangular frame: gate selection is numeric, while ordinary
NPC navigation must handle obstructions. Measure runtime cost with ModProfiler;
behavior and performance remain unverified.
