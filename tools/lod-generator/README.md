# Wild Wasteland LOD generator

A standalone batch tool and WW runtime feature. Run it yourself after changing game assets or installed model mods; no LLM, Unity editor, Blender, or compiler is needed to generate packs. Requires Windows, Python 3.10+ and Node.js 18+ on PATH. The first run downloads pinned UnityPy and meshoptimizer dependencies into this tool's own folders. The runtime support must already be built into your Wild Wasteland DLL.

## Generate and install

Close the game, then run from this directory:

```powershell
.\Generate.cmd -Install
```

For a different game location:

```powershell
.\Generate.cmd -GameDir 'D:\SteamLibrary\steamapps\common\Apocalypter' -Install
```

The command scans all native Unity asset files and active mod bundles under `BepInEx/plugins`, extracts each unique eligible mesh, and generates two lower-detail variants. It writes its working cache to `work/`, its pack and report to `output/`, and with `-Install` publishes the pack to `BepInEx/plugins/WildWasteland/Performance/LOD/`. Source assets, DLLs, game configuration and saves are not changed by this tool. Omit `-Install` to generate without installing.

Launch the game yourself. Enable the Performance module, choose a non-Vanilla Performance preset, then enable **Generated mesh LODs** under Developer > Performance > Rendering. The feature is On by default within non-Vanilla presets. Look for `[Performance] RC9 ... generated meshes indexed` in `BepInEx/LogOutput.log`. The Developer stats show matched renderers, unmatched unique meshes, reserved cache bytes, read failures and zoom state. New tuning entries live in `WildWasteland/tools.cfg`; existing values are preserved.

The runtime swaps only `sharedMesh`, using projected size, LOD bias and transition hysteresis. Originals remain close up; medium/far objects use the generated meshes. An active game camera with FOV at or below 30 degrees restores originals, including binoculars and scopes. It preserves existing renderer visibility, materials, doors, wheels, rigs, colliders, gameplay scripts, loot and navigation. Native culling and LOD groups continue to operate. Originals are restored during saving and on feature shutdown.

## Custom GLB and procedural models

Models constructed by WW or another mod at runtime are not necessarily present in a Unity asset file. To include them:

1. Load your game and let the relevant models appear. Native asset extraction does not require this step.
2. Under the Generated mesh LODs stats, click **Export loaded models for LOD generator**. This also works before any pack is installed, with the feature enabled in a non-Vanilla preset. The sliced authoring capture writes readable originals to `Performance/LODInput/`; it does not modify them. Capture can cause brief pauses while serializing large meshes.
3. Wait for `Exported ... meshes` in the stats/log, close the game, then rerun `Generate.cmd -Install` and restart. The generator automatically consumes those exports, preferring directly decoded native assets where available.

Repeat the export after loading additional custom model types. It can only capture meshes that are loaded and CPU-readable. Unreadable native or bundle meshes are handled by offline extraction. A custom runtime mesh that is unreadable and absent from any source bundle stays original. After changing procedural/GLB model versions, move obsolete `LODInput` files out of that directory before capturing again; conflicting identities are deliberately excluded.

## Quality and reruns

```powershell
.\Generate.cmd -Preset Conservative -Install
.\Generate.cmd -Preset Balanced -Install
.\Generate.cmd -Preset Aggressive -Install
.\Generate.cmd -Include 'D:\MyModelBundles' -Install
.\Generate.cmd -Force -Install
```

Balanced is the default. NPC/skinned meshes always use Conservative limits. Meshoptimizer uses an error bound, normal/UV/color/skin attributes and locked submesh borders; reaching the target triangle count is not guaranteed. Existing vertices' attributes and bone weights are preserved, and unused vertices are removed from generated variants. Materials and moving parts are never merged. `rules.json` contains case-insensitive mesh-name regular expressions to exclude from generation. Deleting an exclusion and rerunning is sufficient; `-Force` forces asset extraction after an extractor change or to bypass the source stamp cache.

`output/report.json` lists generated models and triangle/error counts, unchanged models, exclusions, and decode/generation failures. Tiny meshes below 200 triangles, non-triangle topology, blend-shape meshes, ambiguous identities and unsupported data stay original. A model that cannot shrink at least 5% within the quality limits is also left alone. The scanner processes all input meshes, but deliberately does not force a destructive reduction of every mesh.

Extraction and generation are cached. Packs use immutable SHA-256-addressed payloads; the catalog is published last, so an interrupted generation retains the previous catalog. Installing refuses a running game or a disabled WW installation. Old unreferenced payloads are retained for safe replacement; they are not loaded by the runtime. Restart the game after replacing a pack. Runtime checks source sizes/timestamps, payload checksums, vertex channels, submesh counts and indices; stale/corrupt packs fall back to originals with an error. Pack catalogs contain local source paths and should be regenerated on the destination PC rather than distributed as portable game assets.

## Runtime costs and limits

Runtime decoding and disk IO happen on worker threads, with at most two reads in flight. Unity mesh creation/upload happens on the main thread, one generated level per scheduler step. A large first upload can exceed the nominal slice budget; there is no runtime simplification. Generated GPU meshes are shared by matching source meshes and released on scene change/disable. The configurable cache cap limits reserved payload bytes, not total process/GPU memory; driver overhead and temporary decode buffers are additional. When the cap is reached, additional models remain original until the scene/cache resets.

Discovery uses Performance's shared scan, including skinned renderers, with tile/CreateObject events and a 15-second fallback scan for other mods' spawns. Dynamically replaced meshes are left to their owner. Player and held first-person meshes are excluded. Existing camera culling distances still apply during zoom.

This implements mesh LOD for loaded objects. It does not generate distant unloaded world cells, terrain LOD, billboards, texture atlases or merged exterior proxies. It reduces geometry/skinning work; it does not by itself reduce draw-call count, AI or vehicle simulation. Visual quality and FPS gains need in-game comparison on the same scene and settings.

## Acceptance check

Compare the same saved scene with **Generated mesh LODs** Off and On, leaving the other Performance settings fixed. Walk toward/away from a POI, vehicle and NPC; expect stable silhouettes and progressive detail changes. Use binoculars/scope; expect `zoom: original meshes`. Check NPC movement, wheel/door movement and materials. Save, return to menu and reload; expect original asset references and working objects. Disable the feature; all managed objects should immediately use their original meshes. Report any `[Performance] RC9 keeps original mesh` errors and the pack's `report.json`.

Third-party tools: [meshoptimizer](https://github.com/zeux/meshoptimizer) (MIT), [UnityPy](https://github.com/K0lb3/UnityPy) (MIT). Their package license files remain installed with their dependencies. Generated packs are derived from your local assets; this tool does not grant redistribution rights to those assets.
