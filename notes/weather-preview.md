# Moving weather: animated visual milestone (Testing)

**Current revision:** [weather-smoke-recovery.md](weather-smoke-recovery.md) supersedes the rejected revision 3 rendering/assets described below. It removes generated cutouts and the failed AfterImageEffects scene-color blit, uses native sorted smoke particles, depth-only alpha fog and non-repeating cloud noise. Geography/settings/critical logic remain intact. Keep the old MD5s below only as diagnostic history.

Denis approved Weather and Core/CleanHUD/Performance integration on 2026-10-16. This is original feature work, not an ApocaDustStorm port. Previous density/front DLL: `FA744B35A0D6A7BAB4CF83685D64BD99`. Its static puffs, shallow columns and angular footprint motivated this revision.

Animated revision built/deployed with zero errors, ten existing warnings. Built/installed/staged DLL MD5: `74C4CC2D42DD42BA043FC3766AF5D297`. Unique staged copy: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\WildWasteland_0.1.28_weather-animated-volume_20261016_151138.dll`. Bundle: 9,257,991 bytes, SHA256 `3D3FDB5C594AE6AB85B7E38FA75067065FABC89B9A89278A302AFB4BEA2DC2BC`. Game remains untested by the agent.

## Screenshot follow-up: revision 3 for game review

Denis's screenshots/log confirm the above exact DLL loaded, default area 30.24 km²/eight columns, with critical escalation. Reported defects: speckles, exterior columns drawing over the front, pale interior objects/fog, repeated volume bands and barely visible near dust. Also requested: browner exterior sky/dark cloud blending and lighter sand boiling ahead of the front. Denis reviewed three periodic editor-picture revisions (external Output/Reviews) and asked to see revision 3 in game first. The MD5 above identifies the previous game build; revision 3 deployment is recorded separately below when complete.

- Rendering order is now one reused AfterImageEffects command buffer: final terrain/sky fog first, weather meshes next, nearby gusts/grit last. The former AfterImageEffectsOpaque pass left transparent columns outside the fog/color composition and allowed later image effects to change dust color. Simply placing depth fog after all transparent effects erased their detail using terrain depth; the explicit ordering avoids that problem. No native exposure/light values are changed.
- Per-region geometric interior reveal suppresses hurricane volumes outside and fades them in through the boundary. Shader palette is encoded for active color space, backlight fog preserves hue, and foreground grading compresses brightness/tints toward brown.
- Density texture now has mipmaps (~2.29 MiB total), with filtered lower-frequency shading, no screen-pixel jitter and reduced detail contrast. This targets the visible bands/speckles; screenshot-specific artifact removal still needs Denis's retest.
- Exterior sky gets a soft regional brown/cloud veil around the wall. Existing bottom-row puffs become lighter low leading billows without adding a draw. Canopy puffs are broader/flatter; review coverage and isolated bulges with Denis.
- Near counts are 384/768/1152 (quarter wisps/gust sheets, remainder grit), in a 32×10×32 m camera-relative volume. Brighter/darker sheets, larger grains, radial headwind orientation and coherent gust pulses replace barely visible sparse streaks. Analytic grains/sheets skip animated-atlas sampling. The fog air color also gains moving density variation rather than one flat color. No per-grain CPU simulation, upload, component or extra full-screen pass.
- Four original detailed smoke cutouts (smoke-detail-v2.png) now vary the wall/canopy shape and lighting, with per-puff mirroring, rotation, color variation and continuous UV deformation. They complement the original 32-frame animated density. Generated with built-in image_gen, not copied from reference movies. Exact prompts/tool/source: tools/weather-assets/Unity/Assets/Weather/Imported/smoke-detail-v2.provenance.md. Extra detail sampling costs GPU bandwidth. Alpha below .03 is removed; biased mip sampling and analytic border fades soften enlarged cutouts, especially the cloud canopy.
- Remaining visual feedback: the front looks too much like separate shapes and needs a more cohesive wall/rolling folds. The overhead layer should merge from vortex tops into a broad swirling cloud ceiling with lit folds, as in Denis's latest reference, without a flat black/blue sky. He requested game testing before more art changes; revision 3 is frozen for that test, not declared visually accepted.

This section supersedes the previous revision's counts/render stage below until the handover is finalized.

### Revision 3 game checks

Built/deployed revision 3 with zero errors, ten existing warnings. Built/installed/staged DLL MD5 `F603522019910881F3E6DE6DE37F49AD`. Unique staged copy: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\WildWasteland_0.1.28_weather-review3_20261016_170956.dll`. Embedded bundle: 10,188,005 bytes, SHA256 `11FFDF64A33C951501BF925FEC52A7CAEB3BFE6C35B05FC76CDE38C83ED5ACF1`. No agent game test or new in-game performance measurement. No new config entries. About 300 source lines changed in Weather rendering/shaders and asset builder, plus original smoke texture/provenance and handover docs; Core/other modules were not changed by this screenshot follow-up.

Clear previous previews; spawn Dust storm ahead and inspect leading rolling sand, texture repetition, skyline blend and the marked tiny artifacts. Clear and spawn Critical dust storm preview ahead; wait 40 gameplay seconds, confirm columns stay hidden outside, then drive inside and inspect column edges, brown object color, visible gust sheets/grains and motion. Check Low/Balanced/Max, first/third person, scopes, clear/module disable and map/ESC cleanup. Confirmation: `[Weather] Interior-only hurricane reveal; filtered density, brown final composition, exterior cloud veil and leading rolling sand. No new config entries.` and `[Weather] Final dust composition attached to ...`. Native weather/exposure stays untouched. No new config entries.

## Current behavior

- Manual previews only; four-region cap, 20 m/s movement, six-second entry fade, ten-minute expiry and twenty-second exit fade. No automatic weather, save data, damaging forces, debris or thunder audio. Native/standalone weather remains untouched.
- Default front is 12 km wide and 3 km deep, with an uneven leading edge, rounded ends and narrower, fading rear. A shared 64-point boundary supplies map/area; CPU interior weight and GPU fog use the matching rounded profile. Frustum-culled wall sections span at most 1.8 km.
- Existing settings.cfg entries `[Weather / Storms] Hurricanes per km²` (.25, range 0–1), `Front width (km)` (12, 6–20), `Depth (km)` (3, 1–4) apply live. Count is rounded shared polygon area × density, independent of quality, using an 80-position reusable layout. Default still gives eight columns. Use the tool/log's actual area rather than the old trapezoid's 31.5 km². No config entries added in this revision.
- Critical previews start as ordinary dust; columns/lightning/backlighting/canopy escalate together at 20–40 gameplay seconds. Ordinary previews never escalate. Columns now sit deep behind the front, with the same brown palette, a coherent rotating density volume, broad ground skirt and flared top blending into dark cloud puffs. Visible columns within 2.5 km get cached Ground-layer height probes, at most one globally per .15 seconds. This does not fit the entire wall to terrain.
- Original offline artwork: 32-frame lit RGBA billow animation (2048×4096, 4×8), 16-frame flow atlas (1024×1024, 4×4), 128³ R8 density texture (2 MiB). Shaders animate these assets; no runtime fluid/particle simulation. Balanced/Max blend adjacent atlas frames; Low samples one frame. No movie pixels or Dust Storms code/assets copied.
- Dust remains non-emissive, muted brown and bounded under overlap. Critical upper puffs form a dark canopy. The existing local depth-aware atmosphere also darkens/tints sky pixels from inside, without altering native lights, Azure or RenderSettings. Intermittent emissive lightning and distant shader scatter retain contrast without new Light/shadow passes.
- Low/Balanced/Max: 72/180/288 animated wall puffs per visible section, 16/24/32 canopy puffs per critical section, 128/256/384 nearby wisps/grit, 8/12/18 samples per visible vortex, 2/4/6 regional fog samples. One quarter of nearby cards are broad wisps; the rest are fine wind streaks. Geographic count/positions never change with quality. Performance off/Vanilla uses Balanced.
- One Weather scheduler job, cached meshes/materials/property blocks and reused frustum buffer; no per-puff components, scene scans or per-frame texture uploads. The selected-camera AfterImageEffectsOpaque pass uses actual inverse view/projection matrices and WW-prefixed globals. Clear/off/load/menu detaches it. Map geometry refreshes at most 5 Hz while open with immediate visibility gates.
- Assets load on first explicit spawn. Separate first-use loading/shader hitches from steady state. Overdraw, atlas bandwidth, volume samples and full-screen fog cost GPU time. Matching-editor artwork previews do not establish game frame times or final Azure/post-processing/camera behavior.

## Denis's in-game steps

1. Launch the deployed build. F3 > Modules: enable Weather (visual preview). Enable Debug in F6 > Wild Wasteland and Developer mode in F3 > Debug to expose tools.
2. Load a save on open terrain. F3 Game > World: density .25, width 12, depth 3. Developer > Tools > Debug & performance > Weather visual preview > Clear weather previews, then Dust storm ahead. The menu closes; front starts about 700 m ahead and approaches at 20 m/s.
3. Open M: expect a wide irregular rounded area, dense leading edge/faint rear and travel arrow, without sharp corners. ESC/settings must hide the overlay.
4. Cross the boundary: haze should build gradually and remain inside. Look up for dusty sky and around for animated billows/fine grit. Leave: atmosphere must fade by position. Dust storm around player offers a direct interior check after six seconds.
5. Clear, then Critical dust storm preview ahead. Front starts about 2 km away. Wait at least 40 gameplay seconds, then drive well inside. Expect deeper brown columns, broad dusty feet, dark upper canopy and intermittent lightning: less exposed outside, readable inside. Defaults give eight model locations, not all visible from one view. Density zero removes columns/lightning/backlights; read the tool count after size/density changes.
6. Compare Low/Balanced/Max on the same view/route. Low reduces interpolation and volume detail; geographic count/position stay fixed. Compare clear vs active CPU and total frame time after first-use loading. Include the render callback in ModProfiler: scheduler self-time excludes rendering.
7. Check first/third-person driving, scopes/binoculars, night, sloped ground, map/ESC, scene/load cleanup and module disable/re-enable. Clear weather previews must remove all local effects. Stack two ordinary previews: increased opacity is expected, yellow HDR emission is not.

## Confirmation logs

- `[Weather] Animated dust atlas 32 frames; baked 3D vortex density with 8/12/18 bounded samples, flared ground skirts, dark canopy, 16-frame flow and fine grit; rounded irregular front. No realtime lights or shadow passes.`
- `[Weather] Layered preview loaded; Low/Balanced/Max 72/180/288 animated billows per front section, 16/24/32 canopy puffs, 128/256/384 near wisps/grit, 2/4/6 fog samples; no particle simulation.`
- `[Weather] Front layout: ...` gives actual area/count.
- `[Weather] Spawned critical rounded-front VISUAL PREVIEW; ...`
- `[Weather] Preview entering critical phase: same-color columns forming inside the dust wall over 20 seconds.`
- `[Weather] Local dust atmosphere attached to <camera>; bounded depth fog and GPU wisps, native weather retained.`
- Global WW VerboseLog adds `Local atmosphere: boundary transition` / `interior` / `outside`.

## Comparison and reproduction

See [weather-visual-comparison.md](weather-visual-comparison.md) for the reference implementation and remaining gaps. Its license requires permission for adaptation/bundling; new animation, geometry and shaders are independently authored. Earlier CC0 provenance remains in ASSETS.md.

Authoring/bake instructions: `tools/weather-assets/README.md`. External workspace: `V:\Repos\Apocalypter\_tools\WWWeatherAssets`. Seven Output PNGs are editor artwork previews, not game screenshots. Rebuild with Unity 2020.3.49f1 Built-in, copy Output/wwweather-prototype.bundle to src/Modules/Weather/Embedded/weather.bundle, then Windows build/deploy. Movie-quality equivalence and game CPU/GPU performance remain unverified.
