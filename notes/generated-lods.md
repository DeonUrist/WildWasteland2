# Generated mesh LOD pipeline — Testing, 2026-10-21

Denis requested a standalone DynDOLOD-like generator and WW runtime support for distant objects, vehicles and NPCs, usable without an LLM. Existing CullLods only provided size-based culling; it did not supply simplified geometry.

Implemented within Performance and `tools/lod-generator/` (about 1,100 executable source lines plus instructions/configuration):

- Standalone `Generate.cmd` / PowerShell setup, native/mod Unity asset extraction through UnityPy, meshoptimizer WebAssembly simplification, cached reruns, conservative skin handling, error/border limits, deduplicated SHA-256 payloads, transactional catalogs, exclusions and coverage report. No Unity editor or compiler needed to generate packs. Python 3.10+ and Node 18+ are prerequisites; dependencies install locally to the tool.
- Shared renderer scan now optionally delivers skinned renderers without changing other clients. RC9 swaps only scene renderer sharedMesh references; preserves originals, materials, transforms, rig/door/wheel components, collision and navigation. Defaults follow non-Vanilla presets. Projected-size thresholds/hysteresis, FOV<=30 zoom restoration, saved-mesh restoration, scene/disable cleanup, two worker reads, main-thread mesh upload and a configurable payload cache cap.
- Explicit Developer export of loaded readable originals for GLB/procedural meshes. Generator automatically consumes these exports on the next run. Offline native and mod-bundle decoding does not require visiting locations or launching the game.

## Validation and installation

Windows Release/Deploy=false passed with zero errors and the two existing NPC CS0649 warnings. No game launch, game automation, harness, test project, public release, commit or push. No existing configuration or save files were edited.

Ran the actual standalone command, including first-run dependency setup, full native/active mod-bundle extraction, generation, a cached rerun and installation. Final report:

- 755 unique candidates; 613 generated models, including 20 skinned models.
- Generated set: 835,296 original triangles; LOD1 702,598; LOD2 504,858 (about 16% / 40% fewer). These are unique-mesh geometry counts, not rendered-scene counts or FPS measurements.
- 2,899 exclusions (mostly already-small meshes), 141 unchanged within quality limits, one generation failure (`lightning-max`, invalid/empty simplifier output). Those models retain their originals. Native/bundle decode failures: zero.
- Installed optional pack: `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland/Performance/LOD/`; 529 unique content-addressed payloads, 79,015,528 bytes. Catalog has 613 mesh mappings and 11 source stamps. Catalog SHA256 `73B636BAD4B22FECC5B047CEE0B036E743843A5519DEA62232FE8A6DCCBF8A4B`.

Checked no game process and no exact WW disabled manifest (disabled WWMP is a different plugin), matching live/build asset indexes and all 361 indexed assets present. Staged and installed DLL MD5 `245AF024A356E19B92B466F76C27A694`. Unique stage `V:/Repos/Apocalypter/_releases/WildWasteland_generated-lods_20261021_015825_273.dll`; previous installed DLL is the same stem `.before.dll`. Installed DLL only; shipped index/assets retained. The generator separately installed its new optional generated pack.

## Denis's test

1. Launch the game yourself. In Performance use a non-Vanilla preset and Generated mesh LODs On. Confirm `[Performance] RC9 613 generated meshes indexed` and increasing matched renderers in Developer stats.
2. Compare the same scene with RC9 Off/On, keeping other settings fixed. Walk toward/away from POIs, NPCs and cars. Check silhouettes, textures, NPC joints, doors and wheel motion.
3. Raise binoculars/scope. Developer view should show `zoom: original meshes`; close visible details should return. Existing camera culling distances still apply.
4. Save, return to menu and reload. Objects must keep original asset references and function normally. Turn RC9 Off and confirm immediate original meshes.
5. To cover custom GLB/procedural types, load them and click Export loaded models for LOD generator, wait for the export completion log, close the game and run `tools/lod-generator/Generate.cmd -Install` again. See the tool README for alternative paths, quality presets, exclusions and stale exports.

Visual quality, runtime allocation/upload spikes, cache sizing, world/scene lifecycle and FPS benefit remain unverified in game. This is mesh LOD for loaded objects, not a distant unloaded-world proxy/texture-atlas/imposter system. It does not reduce draw calls, AI or physics automatically. Blend-shape meshes and unsupported/ambiguous data are intentionally retained. First mesh upload and explicit export can exceed a nominal scheduler slice. Leave Testing; Denis performs acceptance.
