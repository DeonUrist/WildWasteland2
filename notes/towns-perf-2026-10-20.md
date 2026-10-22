# Towns placement and pathfinding-map performance pass — 2026-10-20 (Testing)

Denis asked for the review in `claude/towns-placement-perf-review-2026-10-20.md` (project doc) to be implemented in full,
with two explicit requirements: towns yaw (0–360° town yaw plus ±9° per-lot tilt), and the cars/trees placed between the
walls and the buildings must be respected by the pathfinding maps. Not implemented: R7 (keeping streamed-out towns dormant
instead of destroying them) — it touches native objects-pool/saved-object semantics (AGENTS 5.10) and needs a Sources check
first; offered as a follow-up once this pass is accepted. The sub-rect terrain re-export was also left out (seam risk at the
rect border).

## What changed

Construction (TownsModule.BuildStep, Towns only):
- Only native prefab activations are paced (one per frame, 40 ms apart in play): buildings and towers. Wall pieces, planned
  props, tower-bake slices, gate-graph rows, overlay and link steps run inside the existing 700 µs slice.
- Building placement is split in two halves: `TownFactory.Stage` deserializes the clone under the inactive staging root
  (20 ms before the next slot), `TownFactory.Building` activates it in the next slot. Two smaller spikes instead of one.
- Loading mode: while `SaveBus.Busy && !Saving` (new game generation or a load) or the final handoff is waiting, prefab
  spacing is 0 and a new per-frame job `Towns / Load construction` (predicate = one bool) runs `BuildStep` for up to
  `[Towns / Debug] Loading build budget (ms)` (default 12, range 0–40) beyond the shared 2 ms Core budget. No Core change.
- Preparation budget is work-based: `PrepSteps` (60,000 overlay/link steps) with a 60 s wall-clock safety net instead of
  8 s; a timeout now logs `[Towns] preparation budget reached <key> after N steps / S s; overlay ..., street routing ...`.
- `Physics.SyncTransforms()` once after the border phase and once before the first street-link cast (was per prop/tower).
- Interior pre-spawn groups are spaced 40 ms apart during play (no spacing while loading).

Overlay / dressing (new `TownOverlayPlan.cs`):
- `TownDressingPlan.Make` places towers, border trees/wrecks and the wreck garden numerically, with the exact random
  sequence the runtime `TownBorderProps`/`TownDebris` used, so existing towns look the same. Rotation is handled per item:
  every box is the AABB of the rotated collider box (towers/trees from prefab colliders via `TownAssets.ColliderBox`,
  car shells from mesh bounds and the same scale rule), in the town-centred world-axis frame of `TownOutline`.
- `TownOverlayPlan.Build` projects neighbouring building templates, the wreck garden and the dressing boxes into each lot's
  instance grid on the MapMagic worker (`Finalized`), from the `TownBakeData` templates Towns already decodes per prefab.
  Lot frames: root ← lot yaw ← town yaw, the same transforms `TownGround` uses. Result per lot: solid cells, edge bits to
  clear, and dressing-only solid cells (applied only once walls/towers/props exist).
- `TownOverlap` applies a matching patch in one step per grid (dimension check, otherwise the old runtime projection is the
  fallback), then the existing sliced Near/components phases. `Reapply(true)` re-projects with dressing when ownership is
  confirmed after the first overlay. The commit log line says `(planned overlay, with perimeter dressing)` or `(runtime
  projection)`.
- `TownBorderProps` and `TownDebris` instantiate the planned items (no rejected attempts); `TownWalls.Step(out spawned)`
  reports tower activations for pacing. Gate graphs get the complete blocker list at construction.
- `Applied` hands a worker-computed overlay to a retained record plan with the same layout; `DressingFor` computes the
  dressing on the main thread when no overlay reached a plan (module restart, retained record with a different layout).

NPC Nav (`NavMaps.cs`, `Nav.cs`, `NavMapTools.cs`, Denis approved the module change):
- In-memory template cache keyed by `MapKey`: after a map is read from the embedded/disk `.wwnav` (or freshly baked and
  saved), its root-relative floors, Why/Open/Edges, Near, components and floor-collider flags are kept; the next instance
  of the same prefab/settings clones them (`Nav: cloned map <name> from memory`) and skips ReadMap and ComponentsStep.
  Never cached: town-instance bakes that `TownOverlap.Writable` keeps off disk. `Reset custom pathfinding baked data` drops
  non-vanilla templates; limit 96 templates. New `Structure.LoadedTemplate`/`MapSaved` fields.

Runtime routing:
- `TownNavigation.Waypoint` memoises its answer per 1.5 m position/goal cell for 0.4 s (invalidated by anchor moves);
  street links are physics-rechecked only when they pass within 2.5 m of the perimeter or 3 m of a dressing/garden box
  (`Casts`/`Accepted` counters), the rest trust the plan's numeric validation against the templates.
- `TownVehicleGates.Waypoint` memoises node visibility per 2 m cell for 0.5 s.

Files: Towns (TownsModule, TownOverlap, TownOverlayPlan [new], TownBorderProps, TownDebris, TownWalls, TownNavigation,
TownPerimeter, TownFactory, TownAssets, TownData, TownSettings, InteriorPopulation); NPC (Nav, NavMaps, NavMapTools).
Pre-change copies of the wholesale rewrites: `notes/towns-perf-before/`. No save-format change (overlay/dressing plans are
derived, never written); one new settings.cfg entry.

## Build

Cloud mono mcs 6.8 (AGENTS 7.2) against Denis's staged game DLLs: 0 errors, 36 warnings (the two known Nav CS0675 and the
pre-existing ActionDataPort CS0252 set). `build.sh` itself fails on the Quests voice files with spaces in their names
(CS1508 duplicate resource ids); the build used an equivalent null-delimited resource loop with the csproj LogicalName
rule. Stage: `bin/Release/WildWasteland_towns-perf_20261020_0520.dll`, MD5 **D3F327C9A0B460D13532F2D97AA1DEA1**
(transferred in three parts; the parts sit in `bin/Release/_to_delete/`, `bin/src-stage.tar`, `bin/src-delta.tar` and `bin/TownOverlayPlan.cs.sync` are
transfer leftovers — all git-ignored). NOT deployed: standard WW is currently disabled by Apocasetter
(`cache/Apocasetter/disabled/com.denis.apocalypter.wildwasteland/` holds the whole plugin folder) while MP is in use, so a
DLL-only copy would create an incomplete active install. A Windows `dotnet build -c Release -p:Deploy=true` after re-enabling
standard WW is the normal path. The config harness was not run (new entry `Towns / Debug / Loading build budget (ms)`).

## Acceptance for Denis

1. Re-enable standard WW, deploy, start. Startup lists `Towns (Towns): on`; the Performance page shows the new
   `Towns / Load construction` job (idle outside loading).
2. Load `merchantnearcity`: `[Towns] Load handoff waiting/ready` timestamps should be much closer; `[Towns] generation
   ended <key>: ...; N ms` per town; `[Towns] global blockers <key>: ... (planned overlay, with perimeter dressing)`.
   No `preparation budget reached` warnings.
3. Walk/drive to a walled town: same building/wall/tower/tree/wreck layout as before (same seeds); NPCs route around the
   border trees and wrecks and the towers, not through them; vehicles still use the four gates.
4. Drive to a new 12–32 lot town during play: frame pacing with the halved building spikes; walls/props appear in a few
   frames after the last building instead of over seconds.
5. NPC Debug > VerboseLog: `Nav: cloned map Building_N from memory` for the second instance of any POI type, including
   camps outside towns; `Reset custom pathfinding baked data` still rebakes custom maps.
6. Save/reload/stream out and back: identical layout, no duplicate props, overlay line repeats as planned.
