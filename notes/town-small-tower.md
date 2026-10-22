# Small tower and irregular town border — 2026-10-20 (Testing)

Denis accepted the first wall prototype visually and requested less straight walls, 1–3 corner towers with ladders facing inward, and cheap wreck/tree dressing with navigation. He clarified that the small tower must be extracted alone and separately baked; the rest of its camp is excluded.

## Extracted assembly

The small Camp_9 observation tower consists of an upright shipping container, two canopy/support subtrees, three platform railings and the native ladder (seven selected subtrees, nine mesh instances). Camp_8 has a similar assembly. Camp_13's 22.7 m radio tower is **not** used. Runtime creates one inactive `TownsTower` template from the whitelist, retaining exact native meshes, materials, solid colliders and tagged player ladder trigger. It never instantiates a camp root; no spawners, camp save FSMs, residents, loot, furniture, lights, flag or nearby trees are copied into the tower.

`tools/town-wall-assets/export-tower.py` exports the same whitelist from the existing offline dump into `SmallTower/`: exact OBJ meshes, material JSON and PNG textures plus `tower-only-hierarchy.txt` and `small-tower.json`. It checks nine mesh instances, ladder presence and absence of FSM/rigidbody components. The old dump omits a manifest entry for the existing `metal_rusted_17_normal__1141.png`; the exporter resolves its unique exact path-ID filename. Runtime shares the original loaded assets; these files preserve a separate reusable authoring kit, not a new model decoder.

## Separate reusable navigation

At the first wall construction needing a tower, the existing NPC baker samples only the extracted assembly on a temporary flat ground collider at y=-128. Renderers are suppressed. This is production asset preparation, sliced through Towns construction, not a game launch/test harness. The current native bake job is preserved around each slice. The ground is separate from the structure's collider identity. The existing writer publishes `NPC/NavMaps/Custom/TownsTower-<geometry hash>.wwnav` under WW's configuration directory. Later loads reuse that file and placed tower instances discover their own yaw-rotated map. Temporary geometry, pending I/O, floor registrations and move-mask queue entry are cleaned up. Changed NPC bake settings naturally produce a new geometry/settings key. If NPC navigation is disabled no bake is forced.

The .wwnav file is **not precomputed by the agent**: it is produced on Denis's first load with this DLL. Existing navigation describes ground clearance around the tower. Native player ladder climbing remains; this adds no NPC ladder-climbing behavior or guards stationed on the upper platform.

## Border behavior

- Saved buildings stay fixed. Paths version 5 refreshes only parking/access/street links; binary TownStore version remains 6, generator 9/layout 8. Earlier DLLs cannot read paths5 files written by this build.
- Gates remain on four cardinal axes, with the same 15 m opening and close exterior east parking reservation. Fence runs have deterministic shallow outward bends and clipped corners, up to 8 m beyond the former rectangle. One shared outline drives visible wall panels and pedestrian/vehicle collision routing. Exterior ring nodes and terrain/travel envelopes cover the widened border.
- Deterministic 1–3 distinct corners receive small towers inside the wall. The ladder's native +X/-Z side is rotated toward town center. Towers are temporary streamed geometry, rebuilt from town identity without altering native save records.
- At most 128 single candidate attempts place up to 24 static wrecks/native dry trees in the border strip. Buildings, gate cross, measured street edges and other props retain clearance. Wrecks have one box collider, trees retain native trunk capsules. Collider footprints feed the existing building-map overlap overlay and cached walker/vehicle graph. Shared materials/meshes; no vehicle AI or rigidbodies on wrecks, no town-wide bake, new hook/job/config setting or live save/config edits.
- Placement remains within the existing one-piece/frame, 40 ms spacing and nearest-town priority. Navigation geometry is conservative; vehicles may wait outside a gate where no safe interior route exists. No measured FPS claim.

## Acceptance for Denis

1. Restart/load an owned town: buildings/loot stay fixed and walls/towers finish before loading handoff. Check bent corners for panel joins and tower/wall clearance, and close parking outside east gate.
2. Count 1–3 towers and climb each ladder from the town-facing side. No copied camp enemies, loot, lights or signs should appear.
3. First load should log `isolated small-tower navigation ready`; inspect the named `.wwnav`. Reload and confirm `Nav: loaded map TownsTower` / custom-cache reuse rather than another full town/camp bake.
4. Walk/drive through all gates; draw NPCs around walls, tower bases, wrecks and tree trunks. Follow a bus/truck; safe waiting is preferable when buildings leave no interior vehicle clearance.
5. Save to a new name, reload, stream out/back: same outline, towers and dressing; no duplicate structures/residents or displaced saved buildings. Check load duration and FPS with several owned towns.

Final Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with zero warnings/errors. Unique stage `V:/Repos/Apocalypter/_releases/WildWasteland_town-small-tower_20261020_034016.dll`; built/staged/installed MD5 **231494FEB50C413269BCB0D5E38681F5**. Apocalypter was closed, both disabled-manifest locations absent, active runtime assets present. Copied only this DLL and missing tower source-kit files (19 files/49,526,777 bytes) to the standard WW installation; existing/custom assets protected. No live save/config edits, agent game launch, gameplay verification, commit, push or release.
