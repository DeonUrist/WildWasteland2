# External runtime assets — 2026-10-20 (Testing)

Denis requested all embedded assets on disk if this can preserve performance, after rejecting duplicate gas-station GLBs in the DLL. This explicitly expands the earlier Towns-only task to asset loaders/Core/build/deployment/packaging across the mod.

## Result

All former payload resources are external files. The source `src/Modules/*/Embedded/` directories retain their authoring paths so existing edits/tools remain valid, but neither Windows csproj nor cloud build.sh embeds them. Windows builds put the complete payload under `bin/Release/RuntimeAssets/`, with `asset-index.tsv`. That index maps the existing logical names to physical paths, rather than attempting to recover directory boundaries from names containing periods.

The final staged build is **3,294,208 bytes** versus the installed **89,990,656 bytes**: about3.14MiB vs85.82MiB, a96% reduction in DLL size. Final index has313 files/86,896,168payload bytes (~82.87MiB), including the gas-station GLBs. This is a DLL-size comparison, not a claim that total installed assets shrink by the full difference: assets formerly loaded only from the DLL now need their first disk copy. Existing disk/embedded duplicates no longer ship inside the DLL.

- `Core/DiskResources.cs` reads/validates the small index once at `Assets.Init`, caches logical-name/path mappings and the enumeration array, and returns read-only FileStreams. The index is required beside the DLL. No recursive runtime scan, per-frame path search or fallback extraction from the DLL remains.
- Former stream/enumeration consumers use the same parsers and decoded caches. Existing custom models/images/audio, saved settings, profiles and names retain their current precedence/locations. Large files map directly to current runtime paths: NPC/Models and Sounds, Survival/Models and Sounds, LoadingScreens, PlayerBody/Portraits and Models, HUD, Towns/Perimeter and Models. Quests voices stay under Quests/Voice Actors. Small authored defaults stay in separate default directories where reset/missing-only configuration seeding requires them.
- Weather and Low Crawl bundles now use `AssetBundle.LoadFromFile`; removed managed byte-array copies/LoadFromMemory. Bundle contents, shaders, textures, geometry and loaded animation clips are unchanged.
- The legacy PlayerBody.Portraits.male.png is stored under PlayerBody/LegacyPortraits to avoid colliding with the current CharacterPortraits.Male.png on Windows. Both logical names remain available. Source files are unchanged.
- The csproj stages files and writes the index. Deploy=true adds only missing asset files (preserving editable existing files), then publishes the index/DLL. Cloud build.sh produces the same external layout with a space-safe asset loop and no resource compiler arguments; it was edited only, never executed on Denis's PC.
- `tools/package-release.py` includes the exact source payloads and generated index, admits only these known authored text defaults rather than arbitrary configs/saves, includes Towns, rejects case-insensitive destination collisions, and retains required notices. No archive/release was created. The first deployment/release of this DLL requires the asset files/index together; a DLL-only update onto an old install will fail with a clear missing-index message.

## Performance boundary

No new gameplay/frame-loop disk I/O or repeated image/model decoding was added. Existing decoded caches and bake-template reuse are retained; cached resource-name enumeration now avoids repeated reflection array generation. Bundles load directly from files rather than first copying their whole compressed payload into a managed byte array. These code-path properties support preserving steady-state performance, but do not measure FPS or cold-start timing. The agent cannot run/profile the game under AGENTS.md §0.3; Denis must compare cold/warm loading and the affected features. Do not claim zero loading regression or an observed speedup from build success.

## Build / deployment

Windows Release/Deploy=false passed **zero warnings/errors**. No game/harness/mocks/simulation was run. This build includes the existing shared-tree Towns/POI dresser, Patrols loading-budget and NPC footstep work; those changes were retained, not rewritten by this migration.

Final stage: `V:/Repos/Apocalypter/_releases/WildWasteland_disk-assets_20261020_155154_733.dll`.
MD5: **5A88DD94AEA710E3ABFF8A40197E41F0**.
Matching staged index/receipt use the same stem with `.asset-index.tsv` / `.deployment.json`.
Earlier15:49 stage7AA338207B27872C1D9052B9ED8E8FF3 is superseded (legacy portrait collision fix and final diagnostics).

Standard WW is active, both known disabled manifests absent, existing runtime roots present. Across the two preparation passes,164 missing disk files were added and copied bytes verified; final index resolves313 distinct Windows paths and existing files were preserved. The final pass found no payload differences. Corrected gas-station render remains SHA2569587326427CEA2A81C6999D644FC6130A58F90CC2F7D3A946179566ED020E8C4.

**Deployed after Denis confirmed game closure (15:57).** Rechecked zero game processes, standard WW enabled and required runtime roots; all313 indexed file paths exist with no Windows path collisions. Published the exact staged index and DLL together. Installed MD5 **5A88DD94AEA710E3ABFF8A40197E41F0** matches stage; installed index SHA256 matches stage. Previous DLL preserved outside plugin scan at `V:/Repos/Apocalypter/_releases/WildWasteland_before-disk-assets_20261020_155721_670.dll`. Corrected render-model hash remains unchanged. Deployment receipt updated. No game launch/control, live config/save edits, commit, push or release; runtime/performance acceptance remains pending Denis.

## Denis's acceptance

After publication/restart: startup reaches enabled modules without missing-asset/index errors; F3/menu fonts, HUD icons, portraits and18 loading screens render; Low Crawl animates; weather assets load; native/female NPCs and voices, quest voices/content, Stew Pot and vehicle defaults work; town banners/names/bake caches and corrected gas station remain aligned. Edited files and existing saved settings must remain intact. Compare cold/warm load time and representative FPS with the preceding build; report feature-specific asset errors or stalls before treating performance as verified.
