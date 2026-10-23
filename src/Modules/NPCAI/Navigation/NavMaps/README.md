# Persistent POI navigation maps (Testing)

WW reads compatible vanilla maps from DLL resources `NPCAI.NavMaps.Vanilla.<filename>` first, then optionally from
`BepInEx/plugins/WildWasteland/NPCAI/NavMaps/Vanilla/<prefab>-<signature>.wwnav`. These shipped maps are read-only at runtime.
Missing/custom maps bake once and save to `BepInEx/config/WildWasteland/NPCAI/NavMaps/Custom/<prefab>-<signature>.wwnav`.
Further instances and future game sessions load the map at their own position, height and yaw. These are shared prefab templates,
not per-slot or per-seed save data. Normal walkability, routing and clearance settings still apply.

The binary contains root-relative floor heights, obstacle reasons, open-sky flags, walk/hop edges and prefab floor-collider flags.
Connected areas and movement masks are rebuilt without physics sampling after loading. Reading and writing use the existing bake slices;
an incomplete write stays in a temporary file, and cannot replace a complete map. A version/signature/length/checksum failure rebakes.
The signature covers the game build, static collider geometry/transforms, root scale, cell size, margin and maximum step.
Tilted/mirrored roots or unsupported collider shapes retain the original per-instance bake.

Native Camp/Building/Cave/Wreck numbered roots and Starter_Area are discovered by name. Custom static POIs can use those numbered naming
conventions or the native `SpawnerStatic` tag on the root, without a Rigidbody. Map-layer icons do not count as roots. A custom mod with
its own naming/tag/registration system needs a discovery adapter; arbitrary unrelated scene objects are not treated as POIs.

The template captures the first instance's flattened ground and surrounding margin along with its structure. Loaded grid cells now validate
terrain burial and floor support before becoming usable. A mismatch rebakes only that instance in the normal sliced pipeline; the result is
never written to the shared disk cache or memory template. The multi-floor POI graph separately checks its nodes, walk segments and traversal
endpoints against terrain (20 cm tolerance), then requests the existing Tools baker for a detached instance graph when needed. A missing or failed
Tools baker leaves that graph unavailable and grid navigation remains the fallback. Runtime graph bakes never save authored files. Flat matching
placements retain the shared bake. Isolated authoring stages skip world-terrain validation. Test terrain joins as well as interior navigation.
Runtime movement still checks actual obstacles. The grid retains one floor per X/Z cell and
does not add upper floors or ladder traversal.

Navigation setup now includes POI authoring catalog types, including TownsGasStation and saved custom POIs. POI supplies inactive geometry
copies with the committed scenery layout, built one prop per frame; no gameplay markers/spawners run. Setup copies bypass live POI dressing
to prevent duplicated scenery. Reopen setup after adding a new type. With Debug VerboseLevel 2, verify `shared bake terrain validated` on a
matching placement, and `terrain mismatch` followed by `instance terrain bake ready` on a hillside. Cycle to TownsGasStation, REBAKE, place
a test NPC and send it through the entrance. These behaviors remain Testing; Denis performs gameplay and frame-time verification.

## In-game verification (Denis)

1. Set [Debug] VerboseLevel = 2 (deep log; the old NPC VerboseLog switch is retired).
2. Approach a location without a shipped map and wait for `Nav: saved map <prefab>`. Confirm the named `.wwnav` file exists in Custom.
3. Have an NPC follow around a wall/through an entrance, then save, quit the game completely and reload near the same POI.
4. Expect `Nav: loaded map <prefab>` with the same dimensions/walkable count, without `Nav: baked <prefab>` for that instance.
   The Navigation & baking page's loaded custom-cache count should increase. Repeat the NPC movement test.
5. Visit another instance of that prefab, including a different yaw/elevation. Expect the same filename to load and navigation to follow
   the placed walls/entrances, with usable terrain joins. Test a cave floor and Patrols' camp arrival/departure anchors too.
6. On NPC Debug, click **Reset custom pathfinding baked data**. Expect `Nav: Reset custom pathfinding:` with the file/live-map counts.
   Custom cache files are removed and nearby custom maps are rediscovered/rebaked; shipped maps remain in memory/on disk/in
   the DLL. The cache can refill immediately while the player stays near a custom POI. Failed file deletion is reported, not counted as success.
7. For an embedded-pack test, restart with [Debug] VerboseLevel 2 and visit a vanilla POI. Expect `Nav: loaded map` with `(vanilla)` and
   `embedded:NPCAI.NavMaps.Vanilla.<filename>`, plus an increased vanilla count. Reset custom data again: the embedded map must remain
   usable without a new physics bake. Check NPC movement around walls/through entrances and a different rotation/elevation.

## Capturing the release pack

The optional collection aid is tools.cfg `[NPC / Debug] HighlightUnbakedMapPois` (default false). Visible CleanHUD map icons for POI
types with no completed saved bake are light purple (#C79EFF), overriding allegiance/cleared/ambiguous colors. Once a bake is saved, the
normal color returns automatically. This is per prefab type, so another instance of an already captured type retains its normal color.
Fog and marker Off still apply; compass colors are unaffected. The lookup indexes cache/export/shipped files on scene/enable/reset and
updates after saves/loads, without filesystem checks in the map draw loop. Startup logs `Nav: unbaked POI map tint on; N POI types have
saved bakes`. To revert, turn off **Highlight unbaked POIs on map (light purple)** on NPC Debug, or set `HighlightUnbakedMapPois = false`.
That choice persists and normal map colors return immediately. Existing saved values are retained when installing the embedded pack.

The initial embedded pack contains 41 captured maps covering all 39 vanilla POIs (3,494,265 bytes), including two signatures each for
Camp_10 and Building_7. Denis captured them on 2026-10-17/18 and requested their inclusion. Embedded loading remains Testing.

For future captures, with custom POI mods disabled and default navigation settings, visit the 39 native
locations: Camp_1-16, Building_1-7, Cave_1-7, Wreck_1-8 and Starter_Area. NPC Debug > **Export vanilla pathfinding maps** collects matching
native-name candidates into `config/WildWasteland/NPCAI/NavMaps/VanillaExport/` and logs missing locations. This does not designate a custom
replacement of a native prefab as vanilla: capture must use clean native geometry. Exports are retained by custom reset.

After in-game acceptance, copy those `.wwnav` files to `src/Modules/NPCAI/Embedded/NavMaps/Vanilla/`. The existing build glob embeds them in
the DLL automatically. Ship that DLL; users need no initial bake for matching geometry/settings. Missing variants still fall back to the
custom cache. No binaries are fabricated by the build or by an agent running the game. Bump `MapVersion` in `Navigation/NavMaps.cs` when
sampling/clearance/edge rules change; incompatible geometry/settings automatically select another signature. Custom layouts that are
edited without changing mesh metadata should have their cached data reset before retesting.
