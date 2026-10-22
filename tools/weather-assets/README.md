# WW moving storms: asset authoring

Current hurricane pipeline: see notes/weather-full-volume.md. Full depth-clipped volume replaces shell slabs; new original 128³ RGBA32 billow artwork is required. Before a bundle build, run `python tools/weather-assets/bake-hurricane-billows.py <external Unity>/Assets/Weather/Imported/hurricane-billows-128.bytes --detail <external Unity>/Assets/Weather/Imported/vortex-noise-128.bytes` from the repo. Copy the changed shader/builder sources into the external project, then use -BundleOnly. Editor review is permitted only when explicitly requested: WeatherSandReview compiles copied actual WeatherVortexSmoke/WeatherSmoke sources plus the WeatherVisualDetail enum in the external Editor folder, and runs without -quit (it exits itself after writing images). These are asset renders, never game verification. Latest costs/counts supersede the historical sections below.

Diagnostic milestone: SmokeParticles, VortexVolume and Atmosphere compile WW_DIAGNOSTICS_ON variants using WeatherDiagnostics.cginc. The off variant retains baseline rendering math/artwork/sample counts. Compile only with -BundleOnly; no simulated visual previews for diagnosis. See notes/weather-diagnostics.md for actual-game isolation and replay limits.

**Eyewall update:** VortexVolume now integrates a hollow rotating/rising 3D smoke ring at 12/18/24 samples, with no cylindrical UV seam. Runtime bound scale is 1450x1600x1450, with a roughly 300 m clear eye. Active local sand particles now come from the installed native Sand Player prefab/material, not the custom billow atlas. Accepted incoming wall/sky artwork is retained. See notes/weather-eyewall-native-dust.md.

**Current cohesive front:** see notes/weather-cohesive-front.md. Runtime wall/canopy counts = 24/48/80 + 3/4/6 per section; local dust uses two bounded native simulations (160/360/640 each), replacing the near streak mesh. Fog = 4/6/8 samples, filled vortex = 8/12/18. Native Azure sky/light colors are camera scoped. For fixes compile the bundle with `build-assets.ps1 -BundleOnly`, which skips authoring-scene previews. Historical counts/render paths below are superseded.

Current recovery uses native sorted particles plus the original procedural 32-frame density atlas. Generated smoke-detail cutouts and the AfterImageEffects scene-color blit were rejected and are disconnected. The depth-only atmosphere quad runs at BeforeForwardAlpha with alpha blending, no scene copy or temporary color target. Smoke shader: SmokeParticles.shader; material: smoke-particles.mat. Low/Balanced/Max per section = 72/144/192 wall + 12/20/28 ceiling particles. Vortex uses a continuous analytic profile with filtered density (old integration-step counts below are historical). Sky uses continuous 3D noise; repeating fog noise was removed. See notes/weather-smoke-recovery.md for current behavior/test steps. Source geometry/movement/map/config remain intact. The authoring bank Play/Pause must precede SetParticles or initial particles disappear. Revision-05-smoke previews are the current editor art; older images/material references below are history, not accepted visuals.

Prepared external workspace: `V:\Repos\Apocalypter\_tools\WWWeatherAssets`. Keep raw downloads, portable tools, Unity Library/Generated/Output and temporary renders there. This directory contains reproducible sources, not game automation. Runtime handover: `notes/weather-preview.md`; source comparison: `notes/weather-visual-comparison.md`.

## Rebuild current animated artwork

1. Use installed Python with NumPy, SciPy and Pillow. On Denis's machine this is `C:\Users\chura\AppData\Local\Programs\Python\Python310\python.exe`. No new tool installation is needed.
2. Run from the external workspace, using the checked-in script:

```powershell
& 'C:\Users\chura\AppData\Local\Programs\Python\Python310\python.exe' 'V:\Repos\Apocalypter\WildWasteland\tools\weather-assets\bake-animated-dust.py' '.\Bakes\animated-dust'
```

The deterministic bake authors a 32-frame lit looping cloud atlas, 16-frame flow atlas and 128³ R8 scalar density. It performs offline texture work only, never inside Unity/the game. `--noise-only` regenerates only the scalar density. It does not author a physical fluid simulation or include movie pixels/Dust Storms resources.

3. Copy dust-loop.png, dust-flow.png, vortex-noise-128.bytes and animated-dust.provenance.json into the external Unity/Assets/Weather/Imported folder. The checked-in Imported folder already contains the authored outputs. Copy checked-in Unity/Assets/Editor/WeatherAssetBuild.cs and Unity/Assets/Weather/Shaders into the corresponding external project locations when changing them.
4. Build with matching **Unity 2020.3.49f1**, Built-in rendering:

```powershell
.\build-assets.ps1
```

The installed editor path is `C:\Program Files\Unity\Hub\Editor\2020.3.49f1\Editor\Unity.exe`. The script runs hidden batchmode, without -nographics because it renders artwork previews. Do not upgrade to Unity 6/URP/HDRP. Review asset-build.log if it fails. The build creates Output/wwweather-prototype.bundle and seven PNG previews: billows-outside, billows-boundary, billows-interior, hurricane-columns, hurricane-lightning, hurricane-interior, hurricane-ground-skirt. These omit native Azure, game post-processing/camera mods and real terrain; they are artwork previews only.
5. Deliberately copy the successful Output/wwweather-prototype.bundle to `src/Modules/Weather/Embedded/weather.bundle` in the WW checkout. The authoring script never deploys into the game.
6. With the game closed and the disabled-install check complete, build/deploy from Windows PowerShell in WW:

```powershell
dotnet build WildWasteland.csproj -c Release -p:Deploy=true
```

Denis performs all game tests. Do not launch/screenshot/automate the game to inspect artwork.

## Runtime quality policy

| Feature | Low | Balanced | Max |
|---|---:|---:|---:|
| Animated wall puffs per visible section | 72 | 180 | 288 |
| Critical canopy puffs per section | 16 | 24 | 32 |
| Near wisps/gust sheets + fine grit | 384 | 768 | 1152 |
| Adjacent atlas-frame blending | Off | On | On |
| Bounded vortex density samples | 8 | 12 | 18 |
| Regional fog samples | 2 | 4 | 6 |
| Lightning segments, active pulses only | 12 | 64 | 120 |

Wall sections are at most 1.8 km, frustum culled. Column count follows shared area × configured density and never changes with quality. Columns use a shared mipmapped Texture3D (~2.29 MiB), depth-clipped integration, deep placement, broad feet and a top flare into a dark canopy. Regional interior reveal hides volumes outside. Near batch mixes one-quarter wisps/gust sheets with three-quarter fine streaks. No runtime particle/fluid simulation, per-grain components, realtime lights or shadow passes. One global height probe at most every .15 seconds fits nearby visible column feet; full-front terrain fitting remains unfinished.

Current screenshot follow-up is deployed for Denis's game review. Final terrain/sky fog, weather geometry and nearby gusts now draw in that order in one AfterImageEffects command buffer; native image effects do not recolor the final dust composition. The existing bottom wall row supplies lighter rolling leading sand without another draw. Review snapshots use unique paths under external Output/Reviews so earlier pictures remain comparable. They are editor artwork, not game screenshots.

Revision 3 was selected by Denis for an in-game look before further tuning. Near volume is 32×10×32 m, with contrasting wisps/sheets/grains and coherent gust pulses. The four original detailed smoke cutouts in Imported/smoke-detail-v2.png complement the 32-frame bake; built-in image_gen generation/edit prompts and provenance are retained in Imported/smoke-detail-v2.provenance.md. Copy that checked-in PNG too when rebuilding the external project; the procedural bake does not regenerate it. Wall/canopy adds one detail-atlas sample, with biased mip alpha feathering; no additional runtime CPU image work. Wall cohesion and a swirling cloud ceiling remain requested art improvements after the game test.

Animation saves CPU simulation, not GPU blending/bandwidth. Volume sampling, full-resolution fog and overlapping cards need game GPU measurement. No zero-cost or movie-equivalence claim is established. First-use bundle/shader loading can hitch; compare steady state separately. The RGBA atlas must import at maxTextureSize 4096 to retain its 512-pixel cells.

Unity shader gotcha found during authoring: reversed smoothstep thresholds are not portable. Use `1-smoothstep(low,high,value)` for inverse fades; reversed arguments made the bounded volume disappear on the matching editor backend. Keep texture/frame interpolation independent of gameplay density.

## Earlier CC0 pipeline, retained for provenance/optional artwork

JangaFX Cloud Pack and portable Blender 4.5.3 were already downloaded to Sources/cloud and Tools. Archive/Blender checksums and the included CC0 LICENSE.txt are retained outside the checkout. Four static cloud VDBs rendered in two directions produced the earlier eight-shape cloud-stamps atlas. Current animated materials use the new original dust-loop atlas instead.

Optional historical reproduction, from the prepared workspace:

```powershell
python .\fetch-source.py cloud
python .\prepare-blender.py
.\bake-clouds.ps1
python .\pack-atlas.py .\Bakes\cloud-stamps .\Bakes\cloud-stamps-final.png --frames 8 --columns 4 --variants --trim
```

These static shapes are not a looping fluid animation. Never use independent trim on animation frames. The source downloads are [JangaFX's CC0 VDB library](https://jangafx.com/software/embergen/download/free-vdb-animations) and the [official Blender 4.5 release directory](https://download.blender.org/release/Blender4.5/). See `src/Modules/Weather/ASSETS.md` for archive provenance/memory details.

No Dust Storms code, generated atlas or formula is shipped: its license requires author permission for adaptation/bundling. Inspection notes retain the pinned source links. Automatic weather, damaging hurricane physics, debris/audio, full terrain conformance, shelter/cab suppression and actual headlight beams remain subsequent milestones, per the approved incorporation scope.
