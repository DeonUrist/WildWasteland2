# Gas station test — 2026-10-20 (Testing)

## Concrete slab tiling / native interior floor (17:00 build)

Denis clarified that the large slab must use **concrete_1, tiled without distortion**, and the interior must use **floor2**. Removed our earlier rock5_text slab remap and concrete alias for floor2. Native concrete_1 remains ES3 material4156000037244653851; floor2 uses8376223270176389302, whose native material537 has the actual floor2 texture2045 and normal map1654 (the other floor2-named variants use a different main texture). Both are validated by main-texture name at load.

Cause of slab stretching: its exported UV range was0..1 across a32.193x34.282m slab. Replaced only that primitive's UVs with orthonormal per-face projection at **one repeat per2 authored world metres** (accounts for the GLB node's1.5 scale). Slab128triangles are deindexed to384vertices to preserve UV seams, copying the exact original positions/normals per triangle corner; triangle order, winding and membership unchanged. Reused every other primitive and retained all seven named material slots, supplied corrected normals, transforms, interior UVs and collider. The existing importer recalculates tangents for the new UVs. Source/render GLB now101,720bytes SHA256 `54BB6DA70755BEC0A09D3D48CFA3F8A8094469CE87F2CC64414B9AA690899CEA`. Original repo file backup `V:/Repos/Apocalypter/_releases/gasstation-before-tiled-slab_20261020_165949_975214.glb`. The separate collider/nav geometry/debug scale remain unchanged. Future slab geometry re-exports should preserve this metric UV layout or deliberately re-unwrap it.

Windows Release/Deploy=false **zero warnings/errors**. Game closed/WW active/assets checked. Installed exact updated GLB, current external asset index and staged DLL; all313 indexed assets present. Build/stage/installed MD5 **A311B67F892176FB5B2DD2B4BC51E66F**; stage `V:/Repos/Apocalypter/_releases/WildWasteland_gas-slab-floor_20261020_170041_643.dll`. Prior DLL E88920809D98DF7611EB242B80975BC3 plus model/index backed up beside it with `gas-slab-floor-before_20261020_170041_643` stem. Current ladder fixes and other shared-tree work retained. No game/harness/config/save/collider edits/commit/push/release. Next launch/load: check repeated concrete on slab top/sides, native floor2 inside, normals/shadows unchanged; material logs now show Building->concrete_1 and Building->floor2. Runtime visual acceptance remains Denis's.

## Repo normals edit / disk-only models (15:42 build)

Denis edited the prepared repo GLB in place:87,908bytes, seven named material slots retained, SHA256 `9587326427CEA2A81C6999D644FC6130A58F90CC2F7D3A946179566ED020E8C4`. Copied it byte-for-byte to the active plugin `Towns/Models/gasstation.glb`. Previous model preserved at `V:/Repos/Apocalypter/_releases/gasstation-before-repo-normals_20261020_154048_104.glb`. No normals/UV/metadata edits by agent.

Denis explicitly rejected duplicate model embedding. TownGasStation.LoadMesh now reads only external Towns/Models files and reports missing model paths; removed missing-only extraction. The csproj and build.sh resource globs exclude only `src/Modules/Towns/Embedded/Models/*.glb`. Source GLBs remain at the same repo paths for authoring despite the historical Embedded folder name; they are no longer DLL resources. Runtime models remain installed separately. Other embedded assets/loaders were not changed. No need to rebuild the DLL for later disk model edits; restart/reload the world to clear the model cache.

Windows Release/Deploy=false zero warnings/errors. Game closed/standard WW active/all relevant assets checked; build/stage/installed MD5 `4FAE2B69EBCB55E29FA1D3FCEA790D71`. Stage `V:/Repos/Apocalypter/_releases/WildWasteland_gas-disk-only_20261020_154223_710.dll`; prior DLL `WildWasteland_gas-disk-only-before_20261020_154223_710.dll` beside it. Current shared-tree NPC footstep hearing changes (documented separately in AGENTS.md) are also compiled; no edits to them. No live config/save/collider edits/game/harness/commit/push/release. Denis checks normals/materials after restart; agent has not verified visuals.

Asset inventory answer: about83MiB currently under embedded resource roots, including Quests voices32.96MiB, Weather18.36MiB, NPC16.13MiB, CleanHUD8.13MiB and Towns gate textures. Some extract missing disk files; others load resources directly. Disk-model fallback embedding does not accelerate gas-station loading: it still reads/parses the disk file, with an extra extraction write only if missing. No loading benchmark or general claim of resource-vs-disk superiority.

## Native rock foundation correction (15:39 build)

Denis reported the bottom should use native POI ground/rock. Cause was our explicit native concrete mapping of the exported `concrete_1` foundation, rather than the `rock5_text` material used by native `camp_base_rock` (verified Building_1/4/6 and Camp_14 hierarchies). Added native material ES3 ref8405214706577465858 with main-texture validation `rock5_text` (native texture path1566), and remapped only that slab. The separate `floor2` interior retains its previous concrete alias. One Towns source file, four added/two removed lines; no GLB, UV, geometry, collider, scale, config or navigation changes.

Windows Release/Deploy=false zero warnings/errors. Game closed, both disabled-manifest paths absent and active assets checked; build/stage/installed MD5 `27BC3BA8F224FC2F07735576A69A726E`. Unique stage `V:/Repos/Apocalypter/_releases/WildWasteland_gas-foundation_20261020_153902_348.dll`; previous DLL `WildWasteland_gas-foundation-before_20261020_153902_348.dll` beside it. Includes the POI dresser. Restart/reload and check slab rock appearance; log `Gas station material: Building -> rock5_text` for the current joined model. Runtime visual acceptance pending Denis; no game/harness/commit/push/release.

Denis requested the final render GLB path to correct normals: `src/Modules/Towns/Embedded/Models/gasstation.glb` is the prepared final render asset matching the installed `Towns/Models/gasstation.glb`. Preserve its seven named material slots, especially separated sign/debris. `V:/Blender/gasstation.glb` is the last raw source with generic/combined material metadata, not this prepared version; collider remains separate.

## Corrected normals / merged export replacement (14:52 build)

Denis corrected wrong surface normals and requested the latest `V:/Blender/gasstation.glb` installed. This supersedes the earlier render model hashes, but keeps the separate collider, debug scale 1, and two-sided shadow setting.

The new source is 86,536 bytes, SHA256 `CABEDB22E8DCDCDE17AFC6D740F2B9549F57EBF94B18BF685BC52E908273609A`, 2,276 vertices / 1,412 triangles, with the same 32.193 x 11.263 x 34.282 m dimensions as the collider. It is now one node named `Building`, with six generic material slots and no image references. A literal copy would fail the former node-based foundation/material lookup, and its final shared material would put one texture on both the sign and debris.

Prepared the embedded/runtime GLB with explicit material labels for the existing native texture assignments. Kept all supplied POSITION/NORMAL/UV data and node transforms; the source in V:/Blender remains untouched. The shared last primitive was partitioned into sign (396 triangles) and debris (86 triangles), using complete triangle-coordinate correspondence against the prior separate named parts after accounting for their changed origin. All 482 triangles matched. Index order is only regrouped into the two material slots, without changing triangle winding or membership. Other slots map to brick_6, concrete_1, floor2 (concrete alias), metal_rusted_21 and metal_rusted_24. No geometry/normals were regenerated. The prepared GLB is 86,956 bytes, SHA256 `971DF83FE9C8549F74451DF0B1D181DBAE38BE5D1145FFF76E4F25DB3FBF905F`.

Three importer/setup lines now accept meaningful material names before falling back to old generic-material node names and find the slab through its material label as well as its node name. Future merged exports should retain these meaningful material labels and separate sign/debris slots; generic labels on a single joined node erase the old texture correspondence.

Windows Release/Deploy=false build: **0 warnings, 0 errors**. With game closed, WW active and runtime assets checked, installed the prepared model and rebuilt DLL. Stage `V:/Repos/Apocalypter/_releases/WildWasteland_gas-station-normals_20261020_145254_201.dll`; build/active installed MD5 `3D668ED762590165D092CECB7539AF4D`; installed GLB hash matches prepared embedded asset. Previous model: `V:/Repos/Apocalypter/_releases/gasstation-before-normals_20261020_144910_305.glb`; previous DLL: `V:/Repos/Apocalypter/_releases/WildWasteland_before-gas-station-normals_20261020_145254_201.dll`. Collider source/runtime files and settings were not edited this pass. No game launch/automation/harness, native save edit, commit/push/release.

Restart/reload to load the corrected normals. Retest the wall left/perpendicular to the entrance for the reported shining/flashing line, then check materials and collider alignment. Normals and shadow visual acceptance remain **Testing** pending Denis.

## Collider / authored scale / wall shadow follow-up (14:05 build)

This section supersedes the original shared render/collider and unscaled asset details below. Denis supplied `V:/Blender/gasstation_collider.glb`, re-exported the render model with authored 1.5 scale, and explicitly requested the in-game debug scale be set to 1. He identified the flashing line as light shining through the wall left of the entrance, perpendicular to it; it stays fixed on the building.

- New exact embedded/deployed render GLB: 88,892 bytes, 2,291 vertices / 1,414 triangles, SHA256 `94EBD969913BF1F6C2D595E369ED71D84EC22DCFC3EBDD3A01EFDF2600C831FC`.
- Exact embedded/deployed collider GLB: 72,836 bytes, 1,923 vertices / 1,136 triangles, SHA256 `5B87311D14F9DB5BDA6D29CB3179DD3EFBB89AF97F0F26FA38098061D52F5FE6`.
- Both exports already measure 32.193 x 11.263 x 34.282 m. No extra size normalization is applied. Their object origins differ: after normal GLB handedness conversion the collider needs translation approximately (+1.10047, -0.42868, -0.32891) m to align with the render shell. Runtime validates matching dimensions, aligns centers, and shifts both upward by about 1.21518 m so the highest foundation-slab surface is local Y=0 despite the changed Blender pivot. Source files remain untouched.
- The collider's single node is literally named `Collider`. The reader now has an explicit collider-geometry mode so the existing `COL*` special hull path does not discard that node or reverse the non-convex mesh's intended winding. Only its imported Mesh is assigned to MeshCollider; there is no collider renderer. The visible MeshFilter continues using the detailed render GLB. Both inherit the same root debug scale.
- The navigation signature now hashes all collider-file bytes through its mesh name and includes the aligned collider bounds/root scale. A new normal custom-map bake is selected automatically; the previous render-mesh bake is retained but does not match this collider. Bottom-floor cell verification now raycasts the supplied simpler collider.
- The GLB sets every material `doubleSided=true`, but the original reader ignored that flag and the MeshRenderer inherited one-sided shadow casting. The reader retains the flag and the station uses `ShadowCastingMode.TwoSided` when needed. This corrects the source/import mismatch and is the local correction being tested for the reported wall light leak. No global light bias/cascade/material edits; the exact visible leak is not claimed fixed before Denis retests. Unity documents normal-bias light bleeding and two-sided shadow casting as a possible correction: https://docs.unity3d.com/2020.3/Documentation/Manual/ShadowPerformance.html .

Windows Release/Deploy=false build: **0 warnings, 0 errors**. DLL staged and manually deployed with game closed, WW active, disabled manifest absent, and runtime assets checked. Stage `V:/Repos/Apocalypter/_releases/WildWasteland_gas-station-collider_20261020_140536_929.dll`; build/installed MD5 `1AC948496B9DE66BD2290AA78170A3D0`. Updated only the two explicitly supplied runtime GLBs, the WW DLL, and the exact `[Towns / Debug] Gas station scale` entry from **1.5 to 1**, as requested. Previous DLL/model/settings preserved in `V:/Repos/Apocalypter/_releases/gas-station-before-collider_20261020_140536_929/`. Both installed model hashes match the supplied files; the scale value was read back as 1. Future missing-only extraction still preserves local edits. No native save edit, game launch/automation/harness, commit/push/release.

Retest after restart/reload: model has the same intended 1.5-authored size with debug scale 1; player collision aligns with visible walls/doorways/floor; three Scraffa use the new collider bake; the fixed wall beside the entrance has no shining/flashing line. Logs add `Gas station meshes aligned`, `separate gasstation_collider.glb; shadows=TwoSided`, and the normal new `Nav: saved map TownsGasStation` / population-ready lines. If the wall leak persists, do not stack global shadow changes; get a screenshot of that wall and revisit the local geometry/shadow diagnosis. All remain **Testing**.

Denis requested a test of `V:/Blender/gasstation.glb` before adding it to normal town building rolls, including the same mesh as collider, a bottom-surface navigation bake, nearby-camp placement, melee Scrapyard residents, and a temporary debug scale setting. He explicitly approved the >50-line Towns scope. No other module or Core source was edited.

## Model and native materials

The supplied GLB is embedded byte-for-byte as `Towns.Models.gasstation.glb` (87,796 bytes; SHA256 `F615BC511FFD697DFCF8E2D12D8F1C5D851B7F95A49C4DDDA3075125B77F3FAE`). It has 7 mesh nodes / 7 imported submeshes, 2,271 vertices and 1,414 triangles. Bounds at scale 1 are about 21.462 x 7.509 x 22.855 m (X/Y/Z). Its concrete slab top is at local Y=0, slab bottom about -0.529; the sign is the highest part. Blender transforms are baked by a Towns-local copy of the existing WarRig static GLB/JSON reader, including handedness, winding, normals and UV conversion. The only reader addition retains node names per submesh.

All four requested texture names exist in `Sources/data/assets_inventory.tsv`: `concrete_1` (two native texture entries), `brick_6`, `debris_garbage_0026_01_s`, `metal_rusted_21`. The actual GLB has no texture/image references and generic `Material.00x` material names. Nodes also explicitly name `metal_rusted_24` and `metal_rusted_9`, which are native materials/textures too. Runtime borrows the corresponding native materials through existing ES3 scene reference IDs and validates their main-texture names; no game texture/material is modified or disposed.

| GLB node | Assigned native material | ES3 material reference |
|---|---|---|
| concrete_1 | concrete_1 | 4156000037244653851 |
| brick_6 | brick_6 | 6769621802523452942 |
| floor2 | concrete_1 (inferred floor assignment) | 4156000037244653851 |
| metalrusted21 | metal_rusted_21 | 3912141191000562135 |
| metal_rusted_24 | metal_rusted_24 | 3797490793376262977 |
| metal_rusted_9 | metal_rusted_9 | 4505092729398911849 |
| debris_garbage_trash | debris_garbage_0026_01_s | 8270357246983211973 |

IDs/names were checked against `Sources/data/es3_references.tsv`. On first enabled preparation, the model extracts only if missing to `BepInEx/plugins/WildWasteland/Towns/Models/gasstation.glb`. Existing edits there win; the source in `V:/Blender` is not a live runtime dependency. Restart/reload reads the extracted copy again. All GLB bytes feed the mesh name hash, so geometry edits invalidate the normal NPC signature even if bounds and vertex count remain identical.

## Test behavior

Two new settings in `WildWasteland/settings.cfg`, `[Towns / Debug]`:

- `Test gas station = false`: Debug > Towns > Switches.
- `Gas station scale = 1`: range 0.1–5, Developer > Towns > Gas station test (Enable Debug and Developer mode). Uniform render/collider scale. Reload the world after changes so terrain and maps agree.

The extra page also reports test status and the loaded scale. Enabling in an already loaded world requires a reload; disabling removes the live test objects on the next normal tick, with original terrain restored on generation/reload. The ordinary New POI near spawn setting does not block this explicit debug test.

During main-tile preparation, snapshot the native starting camp transform in MapMagic coordinates and the imported footprint. On the worker, choose the nearest eligible ring outside camp (about 58 m at scale 1), preferring the least earthwork on that ring. Reject sites outside the main tile or too close to native POIs/saved towns; require a smaller camp distance than existing captured POI centers. If none fits, warn rather than moving/removing an existing POI. Existing TownLateTerrain shapes a level circular pad with an 8 m feather and removes loose generated objects there, preserving native POI prefabs. The whole pad is added to new-town avoidance. Saved town layouts retain their positions.

The applied object `TownsGasStation`, tagged `SpawnerStatic`, has one MeshFilter and one non-convex MeshCollider sharing exactly the same imported Mesh, with 7 native material slots and no Rigidbody. Its pivot is 0.1 m above the level pad, embedding the foundation and providing a small walkable lip. There is no separate proxy/floor collider. The normal NPC discovery/baker samples the lowest valid floor with its ordinary clearance/edge/connected-area rules and publishes `NPC/NavMaps/Custom/TownsGasStation-<hash>.wwnav`; positive root scale and GLB-content mesh identity participate in the signature. This is a runtime bake, not an agent-generated navigation file.

Within 500 m of the player, wait for a baked connected roofed area, filter cells to the imported ground surface and ray-verify the actual mesh beneath them, then pick three locations at least 3 m apart. Spawn one native Scraffa per 0.25 s tick, using native prefab ID `2524626245796737373` (Sources identifies Scraffa as Scrapyard melee). Keep their native senses/combat and register their bodies with the existing NPC bridge. They chase normally when they notice the player; there is no forced aggro/alternate AI. A too-small model with insufficient human clearance reports the issue and does not spawn unsafe NPCs.

The station is excluded from the normal town catalog and town records. Test NPCs are not added to native world object lists, and their `saveItemVar` FSM is disabled. The instance and surviving test NPCs clear on load/new world/scene change/tile move/module shutdown. Native death loot/corpses retain ordinary game behavior. Markers use existing map/compass services. Floating-origin shifts update pending spawn coordinates. No save format/version, native prefab, hooks in another module, or live config/save edits.

## Build and installation

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: **0 warnings, 0 errors**. About 657 added / 4 removed C# lines across 6 Towns files, including the copied 344-line GLB/JSON reader. No runtime harness or agent game launch.

- Stage: `V:/Repos/Apocalypter/_releases/WildWasteland_gas-station-test_20261020_133109_292.dll`
- Build/stage/active installed MD5: `A47C3F6B95D80797148B71CA2D78D4A0`
- Previous DLL backup: `V:/Repos/Apocalypter/_releases/WildWasteland_before-gas-station-test_20261020_133109_292.dll` (MD5 `FCFCFF15BEB5CE31DEB672EA27DFA4D8`).

At initial deployment, standard WW was physically in Apocasetter's disabled cache (its enable was queued); all runtime assets were there and the game was closed. Updated only that cached DLL, preserving the old DLL outside plugin scanning and retaining the enable/disable state. Denis then ran the game to apply his queued enable. Final disk check found the exact test DLL at the active `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland/WildWasteland.dll`, with the former cached DLL absent. The game was closed at that check. No agent mod-set switch, game automation, commit, push or release.

## Denis's acceptance steps

1. Leave the toggle off once: no test station or raiders should appear.
2. Enable **Test gas station**, leave scale **1**, reload near the starting camp. Look for the nearby building marker and the station, with all materials visible and its slab joining the terrain.
3. Wait for the three raiders. Walk against the walls, around the columns and onto the floor to check the shared collider. Draw the raiders through entrances and around walls/corners: they should follow the baked ground floor without targeting the roof or walking through solid walls.
4. Change scale to 0.8 or 1.2, reload, and repeat. The visible model, collision, pad, map signature and spawn positions should scale together. Very small scales may legitimately have no human-height interior.
5. Reload once more: one station, at most three new unsaved test raiders. Disable and reload: no station/raiders, with normal town layouts retained. Normal death loot is not a station save record.

Always-on confirmation lines: `[Towns] Gas station material: ...`, `Gas station model loaded`, `Gas station test placed`, `Gas station test ground-floor bake ready`, `Gas station test ready: 3 native melee Scraffa`. Normal NPC output records `Nav: saved map TownsGasStation` or `Nav: loaded map TownsGasStation`; enable NPC NavLog for detailed fresh-bake statistics. Visuals, runtime placement, collision, navigation and performance remain **Testing**, pending Denis.
