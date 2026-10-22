# Town parking and perimeter prototype — Testing, 2026-10-20

Implemented and deployed after Denis explicitly approved the full Towns/Patrols scope and confirmed the large square Scrapyard warehouse camp. He explicitly requires parking outside the walls but close, and defers replacing the Scrapyard banner for Coyotes.

## Final behavior and deployment

- Saved buildings keep their transforms. New paths4 migrates only bare parking and dependent numeric paths; the parking near edge is **6 m outside the east wall**, on the existing flattened town pad. The footprint is world-cardinal, with 10 m clearance around building bounds and a minimum 48 m side. Pad radius never shrinks and includes perimeter corners.
- Owned Coyote/Scrapyard towns receive heavy native Camp_14 metal fences and four cardinal **open** gateways. The original native entrance banner/support is reused with horizontal widening to 14 m between posts, in a 15 m wall opening. Original banner art remains on both factions. This prototype does not add animated/closable gate leaves.
- Native meshes and textures stay shared; wall material clones are cached per native source and enable instancing. Each native-style wall section has its native BoxCollider dimensions. Geometry is constructed through the existing scheduler, at most one piece/frame with 40 ms spacing. No per-frame hierarchy scan, material per piece, new Update loop or new Harmony target.
- Pedestrian graph includes gate portals and inside/outside corner routes. Wall crossing rejection prevents selecting an edge through a solid section. Existing per-building maps remain. Sorted entrance candidates accept the first validated nearest portal. Walled towns prioritize street graph preparation within the existing bounded map-preparation work. A separate small cached perimeter graph provides a gate approach fallback.
- Vehicles use a cached 16-node graph with 3 m wall clearance and building-envelope rejection. Existing Pilot travel steering follows gate detours at up to 6 m/s and disables ramming during the detour. If the dense interior has no safe vehicle connection, a car waits at a reachable outside gate. This is not a promise that every road between buildings can accommodate a truck. The offscreen road worker blocks the town footprint; arriving traffic's parking approach remains outside the east wall. Already parked physical vehicles are not teleported to the new reservation.
- Ownership can be established during native pre-spawn; this reopens pending perimeter construction through the same build job. Final native loading handoff waits for walls and cached approach graphs. Existing construction save guards remain. A failed perimeter construction is stopped after the existing two-attempt error bound and logged, retaining the town rather than hanging loading indefinitely.
- Default-on config entry: `[Towns / Debug] Town wall prototype`; reload/restart after changing. No live settings were edited by the agent. Ruins/animals/mutants are not fortified. Walls derive from the saved town plan; native loot/NPC state remains unchanged.
- Reusable authoring kit: `tools/town-wall-assets/NativeKit`, also installed missing-only at `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland/Towns/Perimeter`. Contains exact native OBJ meshes, PNG textures, material JSON, original Camp_14 hierarchy and `perimeter-kit.json` with colliders, native/prototype scales, transforms and SHA256 provenance. Runtime shares the graph's native assets directly; the exported kit is preserved for future asset authoring, not loaded through a new custom model parser. Exporter: `tools/town-wall-assets/export-native.py`. 16 files, 40,842,929 bytes; custom existing files are never overwritten.

Windows Release build succeeded with zero warnings/errors. Unique stage: `V:/Repos/Apocalypter/_releases/WildWasteland_town-walls_20261020_031619.dll`. Build/stage/installed MD5 **DD854EF5E95FA0A609BDF9080D7CF5BB**. Game closed, both disabled manifests absent, active runtime assets checked. Only WW DLL and newly requested missing perimeter assets installed. Saves/configs/other plugin files unchanged. No game launch, harness, commit, push or release. Existing unrelated working changes preserved. Implementation adds two Towns classes and modifies Towns layout/store/factory/navigation/settings/module plus the Patrols Pilot gate approach; no Core changes.

## Denis's acceptance checks

1. Restart and reload an owned human town. Buildings/remaining population/loot must be at the saved positions as loading ends. Parking is outside and near the east wall. Ruin settlements have no perimeter.
2. Walk through north/east/south/west openings. Solid wall sections block movement. Native banner is retained, including at Coyote towns. Confirm no unexpected gaps or floating panels; visual fit remains unverified.
3. Draw NPCs through different gates; they must detour through openings instead of pushing at solid walls. Follow a merchant/convoy and drive a bus/truck through the openings. NPC drivers may wait outside if the interior is too narrow; they must not ram the wall to reach an interior target.
4. Save under a new name, reload, leave/return through streaming: same buildings, parking and perimeter, with no duplicated walls or residents. Existing parked physical vehicles retain their saved positions; test new arrivals for the new close reservation.
5. Check frame pacing and loading duration, including a large town. Runtime performance and pathfinding remain unmeasured/unverified; no claim of accepted FPS.

Logs: `[Towns] perimeter kit: Camp_14 ...`, `[Towns] perimeter built <key>: ... four cardinal open gates; parking outside east gate.`, existing load-handoff waiting/ready and ownership-confirmed lines. Missing native kit or bounded preparation errors are warnings and must be reported during the test.

## Initial investigation and plan (historical)

Requested: bring parking closer to buildings, surround towns with allegiance with native camp metal walls, provide one large gate at each cardinal exit, integrate pedestrian and vehicle routing, and preserve reusable wall/gate assets in the mod folder.

## Findings

- Current parking uses the furthest building corner's radial distance plus 30 m. For long dense rows this overestimates the distance to the nearest usable edge. A closer reservation must use oriented building footprints and vehicle clearance, rather than subtracting an arbitrary constant from the radius.
- `TownParking.Outside`, `TownPlanner.BuildLayout`, and the saved-path migration in `TownPlanner.Make` own reservations. Existing building transforms must remain authoritative. Current path format is 3; any migration must remain readable and move only the bare parking reservation and dependent paths.
- Native camp `wall_metal_1` exists, with shared mesh/material and BoxCollider. Standalone hierarchy record: Sources/data/hierarchy/prefabs/W/wall_metal_1.txt. Camp_4 and Camp_13 include it; Camp_7 also has decorated metal/fence assemblies. It is a small panel; do not mistake it for the requested large gate.
- The inspected camp hierarchy and asset inventory have no asset explicitly named gate. Exact large-gate selection remains unresolved; do not substitute a small fence or fabricated gate and describe it as the native requested model.
- Town pedestrian routes use a small visibility graph plus per-building maps. Runtime navigation validates graph links with capsule casts. Wall colliders alone can reject routes without providing alternative gate routes: add gate portals and connected interior/exterior perimeter links.
- Traffic's route worker consumes numeric town bounds; town approach/parking legs are appended in Patrols/TownTraffic. Drivers already use obstacle casts. Wall collision alone cannot provide vehicle navigation: numeric perimeter exclusions and explicit gate approaches must agree with the physical geometry.
- Repo AGENTS requires approval before exceeding about 50 changed lines or a second module. Scope approval and gate identification have been requested; no production changes made for this feature yet.

## Proposed implementation

1. Preserve all saved building positions and rotations. Calculate occupied footprints once. Place the undrawn parking rectangle close to a verified clear building edge with room to turn and approach.
2. Derive one stable rectangular perimeter from occupied footprints with a pedestrian clearance strip. Cardinal gates mean world north/east/south/west, irrespective of the town row rotation. Keep parking reachable outside a suitable gate.
3. Apply walls only after the existing actual population ownership threshold is met. Keep ruin towns unfortified. Establish perimeter identity from the saved town plan; never reroll buildings to fit a wall.
4. Reuse the selected native wall and gate geometry/materials, cache source assets once, and create static collision geometry. Preserve a local reusable asset package and source manifest in the Towns mod asset folder. Resolve the gate's opening/collider behavior from the actual selected model before implementing it.
5. Build perimeter sections within the existing scheduler/frame budget. Avoid a separate Update loop, live hierarchy scans, per-instance materials, or a whole-town navigation bake.
6. Add gate and perimeter nodes to pedestrian routing; validate clear connections to existing entrances. Route cars around physical walls and through clearance-checked vehicle entrances. Avoid introducing a path through dense building gaps.
7. Reconstruct walls during loading with the existing Towns handoff. Reload and streaming returns must reproduce the same layout and collision geometry without duplication. Existing loot, inhabitants, identity and wounds remain native saved data.

## Validation after approval and implementation

Windows Release build only for the agent; Denis runs gameplay checks. Verify close parking with a bus/truck, all four gate crossings on foot and by car, NPC pursuit through gates, convoy/merchant arrival and departure, ruin versus owned settlement behavior, save/reload and streaming return, original building positions, and frame pacing during large-town construction. Runtime performance remains unmeasured until tested. No commit, push or release requested.
