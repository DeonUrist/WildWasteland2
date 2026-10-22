# Planned town allegiance and construction — Testing, 2026-10-21

Denis explicitly requested town allegiance, population and loot be decided when the town becomes known, with walls placed with structures and distance controlling actual spawning only.

The observed wall delay was construction, not proven renderer culling: `Owned` required five actual faction NPCs; population was distance-gated, and only then could `NeedsWalls` become true. The supplied runtime log showed the visit/arrival, `ownership confirmed`, then `bent perimeter built` for the same town. The layout planner had already selected a faction, but `Assign` forced Condition back to Ruined and spawned residents later promoted it.

Four Towns sources, small targeted change:

- TownData: ownership now follows the accepted plan's Coyotes/Scrapyard faction, independently of spawned member count. FactionMembers remains an actual-spawn counter, with its existing save field unchanged.
- TownPopulation: Assign classifies from the planned faction; Created no longer promotes allegiance or logs late ownership confirmation. Population/loot allocation is unchanged, deterministic for the existing key/settings and respects saved completed/partial groups.
- TownsModule: allocate full-town population/loot budgets when the applied/resumed site receives its save record, before the building queue begins. The existing build queue then places eligible walls after buildings without waiting for arrival or actors. Native arrival range/actor pacing remains unchanged.
- TownStore: reclassify loaded records from their saved faction; existing names, positions, faction, spawn counters, loot completion and save version/format retained. Previously unspawned human towns now have their intended inhabited allegiance and perimeter immediately. Existing explicit developer population/loot setting changes still rebalance pending groups as before.

Windows Release/Deploy=false passed zero errors/two existing NPC CS0649 warnings. Staged combined DLL `V:/Repos/Apocalypter/_releases/WildWasteland_planned-town-allegiance_20261021_180203_680.dll`, MD5 `60BFEE850A5F03201F1BF0A3D365A479`. NOT deployed under Denis's hold; it includes the pending wall LOD/material guard and other held shared-tree changes. No game/harness/config/save/asset/index edits, commit/push/release/Verified.

Denis's test after deployment: load a save with an unvisited Coyotes/Scrapyard town on applied main terrain; `[Towns] town rolls ... faction=..., Inhabited, population=...%, loot=...%; planned before construction` must precede lot construction. `bent perimeter built` should happen during town construction before approaching the town. After construction, inspect remotely with binoculars; existing RC9 panel behavior applies. Arrival alone should queue/spawn residents; there must be no faction or wall-appearance transition caused by resident count. Test partially/completely looted saves, killed residents, menu/reload and module off/on for duplication/progress regressions. Unloaded/draft-only terrain still has no built town geometry; frame-budgeted construction remains progressive. Runtime acceptance remains Denis's.
