# POI dresser — 2026-10-20 (Testing)

Denis requested a bindable Developer tool to dress custom POIs with game props and reuse those placements cheaply. Explicitly approved the full >50-line Towns-only implementation and chose static dressing. Also explicitly required floating-origin/map-shift handling.

## Use

1. Enable Debug and Developer mode in WW settings. In **Developer > Towns > POI dresser**, bind **POI dresser** (default None), or use **Open POI dresser**.
2. Approach a loaded custom POI within 200 m. The nearest ready custom POI is selected; the gas station is explicitly registered even with NPC navigation disabled. Other tagged static custom POIs use the existing NPC discovery. Native numbered Camp/Building/Cave/Wreck POIs and the small tower kit are excluded.
3. The world pauses. Search the left catalog for native mesh names. This lists individual static mesh parts, including parts from larger prefabs, rather than copying entire scripted native objects.
4. Select a mesh, point at the building/ground and LMB to place repeatedly. Wheel rotates in 15-degree yaw steps. RMB + WASD flies the camera, Q/E down/up, Shift faster. Surface-height offset and local X/Z snap grid are on the right (grid 0 disables snapping).
5. SELECT then click a placed mesh's bounds, or choose its row on the right. Edit POI-local position, Euler angles, scale and solid-collider flag. MOVE follows the surface, COPY duplicates, DELETE removes. Ctrl+Z/UNDO restores snapshots (up to 32 before the history resets). Esc cancels placement/movement, then closes. Closing discards unsaved edits.
6. SAVE LAYOUT publishes the reusable type layout and closes. All loaded instances of that type rebuild in paced steps; future instances use the saved layout. Solid geometry is included before navigation discovery/baking.

## Placement and performance

- `PoiDressLayout.cs`: validated version1 JSON; ES3 native component reference IDs are decimal strings, with exact mesh-name validation. Position ±500 local units, scale .01–20, maximum512 props/type; malformed or missing native sources report errors and retain the authored file. Atomic same-directory replacement, temp readback and previous-file `.bak`.
- Authoring assets are `BepInEx/plugins/WildWasteland/Towns/Layouts/<POI-type>-<hash>.json`. These belong to the POI type and are reusable across worlds/seeds/instances. They are not world-save sidecars and do not modify the Towns world record. Keep/share this folder with custom POI assets. The backup is an authoring backup, not world-save data.
- `PoiPropCatalog.cs`: catalog snapshot on first editor use, sliced enumeration and cached stripped templates. Shared native meshes/materials; positive native mesh scales. Excludes skinned meshes, live scene objects, >100k-vertex meshes and >80m mesh bounds. Only MeshFilter/MeshRenderer and optional copied solid Box/Sphere/Capsule/Mesh colliders survive. No native script/FSM, item identity, Rigidbody, trigger, light, particle or gameplay interaction is copied. Native direct colliders are used by default when available; manually enabling collision on a mesh without one uses a non-convex mesh collider.
- `PoiDressing.cs`: one global pending queue, at most one prop activation per frame inside a500us shared-budget scheduler job. No ready-site scan or per-prop job in normal frames. Static template caching avoids repeated native prefab/script initialization; no per-instance material clones. Rendering/physics costs and indivisible Unity instantiation/collider cooking remain; no zero-cost/FPS claim. Large layouts take several seconds to finish at one prop/frame.
- Nav.Make prefix gates only custom static POIs until dressing is complete. Explicit layout-save invalidation closes/removes only affected live structure maps; the existing NPC baker discovers the completed geometry. Collider-local transforms feed the existing map hash, so layouts reuse matching bakes and changed solid layouts select a different cache. The rare editor save/clear adapter uses reflection outside normal spawning/frame paths; no NPC/Core source changes.
- `PoiDresser.cs`: EditorSession owns pause/cursor/input blocking and restores camera/session on close, focus loss, lifecycle reset or module shutdown. No gameplay script components are attached to props/camera/POIs. Draft/ghost renders have no colliders. Numeric placement allows stacks; surface picking uses the actual building/world colliders, not draft props.
- **Map shifts:** committed props, drafts and ghosts are children of the actual POI root and store only local position/rotation/scale. They follow its native movement/rotation/scale automatically. OnOriginShift adds the supplied scene delta to cached free-camera/restore positions and selection-outline points. No scene/world position is serialized. Navigation hashes already compose child-local matrices independently of origin shifts.

## Build and deployment

Four new Towns files (~788 lines) plus TownSettings/TownsModule lifecycle/UI integration and a gas-station root getter. One new settings.cfg key `[Towns / Editors] POI dresser`, InputSystem Key.None. No live config/save/model edits; prior gas station/Patrols shared-tree work retained.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: **zero warnings/errors**. Game closed; both known disabled-manifest paths absent; standard WW theme/player/NPC/Patrols/gas-station assets present. DLL-only deployment to standard WW, build/stage/installed MD5 **2B64201463D073C451852422D7EAB643**.

Stage: `V:/Repos/Apocalypter/_releases/WildWasteland_poi-dresser_20261020_151327_916.dll`.
Previous DLL: `V:/Repos/Apocalypter/_releases/WildWasteland_poi-dresser-before_20261020_151327_916.dll` (outside plugin scan).

No game launch/control/screenshots/harness, commit, push, version bump or release. Runtime acceptance belongs to Denis.

## In-game acceptance (pending)

- Bind/open near gas station; catalog populates and search finds meshes; camera moves while player/NPC simulation stays paused. Close restores camera/cursor/control without firing/attacking.
- Place several props, edit local transforms/collision, move/cancel, copy/delete/undo. Discard restores original dressing; Save publishes `[Towns] POI dresser saved TownsGasStation: N static props; <path>` and paced committed props appear once.
- Reload and stream out/back: same placements, no duplicates, no world-save-generated dressing objects. Compare another instance with different yaw/scale when available.
- Cross a native floating-origin threshold, return, edit and save again: props stay aligned, local JSON values remain stable, camera returns correctly; cached bake remains reusable.
- Place a solid barrel/crate obstructing an interior route, save and let baking finish. Melee Scrapyard test actors route around it. Disable collision on a decorative mesh and confirm it does not obstruct walking/nav.
- Measure catalog-opening latency, large-layout construction and steady FPS/physics cost. Build success does not verify these or rendering/native reference coverage. Invalid layout/missing asset errors must retain the file, rather than silently saving an empty replacement.
