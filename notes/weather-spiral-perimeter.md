# Wall-surface hurricanes, enclosed boundary, independent sand tint and darkening — Testing

Denis accepted the external wall's texture/color response on build 1D24F0AFF1F84C65EA4E593BC1B8E93D, but rejected the hurricane's remaining seams/circular bands. He explicitly requested the same wall surface moving in a spiral, independent native sand color, dark orbiting stones, depth-based darkness (0–1, 1 full dark) and walls around every edge. Preserve existing user colors and the approved wall atlas/shading.

Latest matching log establishes local sand now emits (`Local sand live: particles=1`, AlwaysSimulate) and two stone pools initialized. Initialization does not establish stone visibility. The previous source used approximately 1 m sprites with 1 s life, colored like the bright hurricane at a roughly 580 m orbit: poor projected size/contrast at this scale. No new sand-emitter experiment is part of this revision.

## Renderer changes

- Stop submitting the rejected VortexVolume cube. Its legacy assets remain loadable, but no longer define the hurricane surface. New WeatherVortexSmoke uses the **same SmokeParticles material, 32-frame dust atlas, radial feather, color/shading and native billboard expansion as the accepted wall**.
- Upload static puff points only at bank creation/quality change: 64/120/192 visible puffs per hurricane on Low/Balanced/Max. Each point stores quantized angle, height and radial jitter. Vertex shader advects its centre upward and around the axis, varies radius/sway and expands puffs into a broad upper swirl. Phase wraps fade at bottom/top. Clear eye remains at least roughly 300 m; same geographic locations/counts and critical-region reveal. Geometry has no angular UV join or ray-march sampling planes. This does not prove absence of every artifact: transparent billboard sorting uses reference positions while shader positions move; Denis checks actual continuity and motion.
- Full boundary walls: WeatherPerimeter caches an arc-length layout on WeatherRegion's existing 64-point rounded/wavy polygon (same map shape). Up to 48 overlapping banks per region, target 1500 m arc spacing; bank pool capacity 192 for four storms. Local bank normals face inward, with no duplicate procedural profile displacement. Retain the accepted wall material, atlas, opacity, bottom billows and canopy. All edges have banks, including sides/rear. Frustum culling retains only submitted visible sections. Terrain conformity remains based on region ground height; no new full-perimeter terrain scan is added.
- The previous core filter is superseded by this requested surface replacement, not layered with another ray-march noise experiment. Diagnostic core shape/full now select neutral-stamp versus full smoke atlas. IDs: boundary bank = region*48+section+1; hurricane = 1000+region*80+column.

## Controls

Same location: Settings > Developer > Weather > Weather visual preview > Storm colors. Existing three color keys retain values; two new config entries:

- settings.cfg [Weather / Colors] **Vanilla sand effect color**: fourth RGB group, independent from Main tint. Missing entry defaults to the existing Main tint value so it retains the previous sand appearance on upgrade. Current cloud particles recolor only on palette edits/tint-view transitions.
- settings.cfg [Weather / Storms] **Storm darkening**: 0–1 slider, default .35. Extra dimming = setting * smoothed regional inside-depth weight. 0 disables extra dimming; 1 reaches black in the deep interior (inside weight snaps at .995). Other existing storm haze/lighting effects still apply at 0. Enter/leave fades continue while moving; UI is intended to remain readable.

Darkening uses one texture-free multiplicative DrawMesh pass at AfterImageEffects on the selected camera, attached only for a nonzero amount. Blend Zero SrcColor multiplies the completed world, including particles. It does **not** sample/blit/copy camera color, allocate render textures or add image-effect components. Existing pre-transparent atmosphere stays in place. Remove/release commands and reset globals on suspend/load/clear/shutdown. This is an explicitly requested additional rendering pass and requires GPU/UI compatibility testing by Denis; compilation cannot establish either.

## Stones

Keep capped 1/2/3 visual pools, 32/64/96 particles per pool, native orbital particle simulation and no collisions/damage/rigidbodies. Increase sprites to 3–9 m (native parent scale 100), life 2–4 s, slower orbital 0.25–0.55 rad/s and rise 40–100 m/s. Private dark brown material replaces hurricane-tinted bright material; no shared native material is modified. `[Weather] Dark stones live:` logs a nonzero count or timed zero report once per pool. It confirms simulation, not subjective readability.

## Test in game

1. Ordinary storm: view and cross front, side and rear. All boundary edges should show the approved wall surface, with soft overlap/entry and no open rear. Check different terrain heights and map footprint.
2. Critical storm: wait 40 gameplay seconds and approach a column from within the storm. It should have the wall's smoke surface moving continuously upward/in a spiral, flare into the ceiling, and surround an open eye. Compare outside/inside and turn/look up at former seams. Send a short moving clip; stills cannot establish motion.
3. Change only Vanilla sand effect color to a distinct temporary hue: local clouds change, wall/Main/hurricane colors retain their values. Set it back afterward.
4. Try Storm darkening 0, .5 and 1 while walking from edge to centre, then leave/clear. At 1 deep interior should be black while HUD remains visible; 0 removes this extra dimming. Clear restores normal world lighting.
5. Diagnostics OFF and game unpaused: approach the lower outside edge of a critical vortex for dark orbiting stones. Check `[Weather] Dark stones live:` and send LogOutput.log if absent. High tint similarity must no longer hide them.
6. Compare Low/Balanced/Max at the same view. New native sorting/billboard overdraw and full-perimeter coverage have real costs; no performance equivalence claimed.

Scope: WeatherPalette/Visuals/Smoke/Debris/Atmosphere/Module, new WeatherVortexSmoke and WeatherPerimeter, two shaders and embedded bundle, related docs (~270 source lines added/changed). No Core/other module changes, no game automation or simulated visual tests. Build/deploy recorded below.

Unity 2020.3.49f1 -BundleOnly succeeded. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with zero errors (10 existing non-Weather warnings), after correcting a missing Core namespace import in the new class. Game closed and disabled-install manifest absent. Installed MD5 matches build: **2EA1878509BE14600FD289132A238EFE**. Bundle SHA256: `81511EBAEC9E93D5CB128637C9287C37B83A67203C7EA2B668642DFAA37BFB2E`. Unique DLL/source/authoring/baseline-log record: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-spiral-perimeter_20261016_194923`. Status Testing; no in-game visual/performance result for this revision yet.
