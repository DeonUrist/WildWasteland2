# Weather preview artwork

User-provided thunder: source `sound/thunder.mp3` is installed externally at `Weather/sound/thunder.mp3`, beside `sandstorm.wav`; include it when packaging Weather. It is not embedded in the Unity bundle. SHA256 `50C22A0D11A3D3DAB8B4502F22F3412626AD9E7B7D68E59A39FC285C3E823C31`. Runtime loads MPEG asynchronously once and queues thunder from each visible strike's hurricane distance /343 m/s. See notes/weather-thin-thunder.md.

**Eyewall/native-dust follow-up:** incoming wall/sky retained. Local dust borrows the installed native SandStorm/Sand Player visual (ES3 prefab 5697600806714206537); native texture/material, cloud velocity, shape and lifetime modules remain, with browner/lower-saturation start color. The game assets are not embedded/redistributed. Custom local dust emitters and terrain probes are removed. Hurricane bounds 1450x1600x1450 carry an animated hollow 3D shell at 12/18/24 samples, including an interior eye. See notes/weather-eyewall-native-dust.md; older filled-core/local-dust descriptions are superseded.

**Current:** notes/weather-cohesive-front.md supersedes the historical recovery descriptions below. Broad opaque dust body, rolling 24/48/80 wall + 3/4/6 canopy detail per section, native moving ground dust (two capped systems), coordinated native Azure sky/sun/ambient, 4/6/8 fog samples and filled 8/12/18 vortex integration. Old near streak mesh and generated cutouts are inactive. Only our authored procedural atlas/density are used. No source-mod artwork/code is redistributed. The 16-frame flow resource remains historical/loaded, but the current integrated vortex animates its 3D density directly. Compile artwork using build-assets.ps1 -BundleOnly; no scene previews required.

Current smoke recovery: generated smoke-detail-v2.png is rejected and disconnected from active materials/bundle dependencies. Wall/ceiling uses native distance-sorted particles with SmokeParticles.shader, our original animated procedural dust-loop.png, radial alpha feathering and opaque-depth fades. No imagegen cutout is rendered. Near gusts retain shader particles. Vortex uses filtered baked shading over a continuous Gaussian opacity profile; old 8/12/18 integration below is superseded. Atmosphere blends from depth before transparent geometry, without reading/copying scene color. See notes/weather-smoke-recovery.md; older generation provenance remains historical.

The embedded `weather.bundle` targets Unity **2020.3.49f1**, Windows x64, Built-in rendering. No runtime scripts/MonoBehaviours are bundled. Do not rebuild with Unity 6/URP/HDRP.

Current wall, canopy, near dust and vortex assets are original Wild Wasteland procedural artwork. `tools/weather-assets/bake-animated-dust.py` uses deterministic noise (seed 837219), offline density projection and approximate single scattering to author:

| Resource | Format/layout | Runtime use |
|---|---|---|
| dust-loop.png | 2048×4096 RGBA, 32 frames, 4×8 cells | Changing lit cloud shape; 20-second cycle/per-puff phase, Balanced/Max frame interpolation. |
| dust-flow.png | 1024×1024 RGB, 16 frames, 4×4 cells | Looping flow/domain warp in vortex shading, 12-second cycle. |
| vortex-noise-128.bytes | 128³ R8, 2 MiB source; ~2.29 MiB with runtime mips | Shared filtered Texture3D density; bounded 8/12/18 samples in Low/Balanced/Max. |
| smoke-detail-v2.png | 1254×1254 RGBA, four equal quadrants | Original generated smoke shape/detail, one extra wall/canopy sample. |

Smoke detail was created with the built-in image_gen tool/imagegen skill, then imported with mipmaps; the checked-in PNG is the reproducible source, not output of the procedural bake. Exact generation/edit prompts, source paths and alpha inspection are in tools/weather-assets/Unity/Assets/Weather/Imported/smoke-detail-v2.provenance.md. Low-alpha colored fringe RGB is discarded by the shader and only grayscale detail modulates the common brown palette. Continuous UV deformation and the 32-frame bake animate the static detail. Biased mip sampling softens large cloud edges.

Shaders perform animation/sampling. No runtime fluid/CPU particle simulation, per-puff Update or per-frame uploads. The RGBA atlas would occupy about 42.7 MiB with mips if uncompressed; actual GPU size depends on platform import/compression. Flow is about 4 MiB at RGB8 including mips; filtered density is ~2.29 MiB including mips. Bundle size is compressed storage, not a VRAM measurement.

Wall sections use 72/180/288 puffs; the existing bottom row is lighter rolling sand ahead of the wall. Critical canopy has 16/24/32 puffs per section. Near dust has 384/768/1152 cards (one quarter wisps/gust sheets, three quarters grit). Vortices integrate coherent density inside a bounded box, with broad ground skirt, upper flare, opaque-depth clipping and regional inside-only reveal. Geographic density stays independent of quality. Final terrain/sky fog precedes weather geometry and near gusts in one AfterImageEffects command buffer; screenshot follow-up remains in authoring review.

Wall/columns share muted brown tint and bounded non-emissive alpha-over shading; lightning is deliberately emissive. Canopy/local sky treatment darkens the upper scene without native Azure/light overrides. Lightning/backscatter needs no Light/shadow component. Game appearance/performance needs Denis's review.

## Earlier CC0 resources

Retained cloud-stamps.png came from four static **JangaFX Cloud Pack** VDBs rendered in two directions with Blender 4.5.3. Current animated materials use dust-loop.png instead. Earlier DustShell assets and CC0 stamps do not supply the new animation.

Source: https://jangafx.com/software/embergen/download/free-vdb-animations
Original archive CloudPackVDB.zip, SHA256 `3676ba4c7ac431446731f956f98e925ad3db31354b8b4adcb137dc0250595f73`. Included LICENSE.txt confirms CC0 and is retained alongside this file.

No ApocaDustStorm code, formulas, generated atlas or movie pixels included. Its source was inspected only; adaptation/bundling requires author permission. Reproduction/provenance: `tools/weather-assets/`. Raw downloads/portable tools remain outside source control in `V:\Repos\Apocalypter\_tools\WWWeatherAssets`.

Diagnostic bundle update: keyword-gated WW_DIAGNOSTICS_ON variants add data readouts/isolation to SmokeParticles, VortexVolume and Atmosphere through WeatherDiagnostics.cginc. No new artwork, normal-path density/color/sample changes or additional production render pass. Build with -BundleOnly; diagnose in Denis's game.

Spiral/perimeter update: active hurricanes reuse the accepted wall's SmokeParticles material and dust-loop atlas, with GPU spiral centre/size animation of paused puffs. Legacy VortexVolume assets are no longer drawn. Atmosphere gains a requested texture-free multiplicative world-darkening pass (no camera-color read/copy). No new artwork downloaded/generated. Native Stones retain original sprite privately tinted dark; simulation size/lifetime/orbit adjusted to the wider storm.

Defined-shell update supersedes the puff geometry above: hurricanes now use curved overlapping patches on an annular profile, sharing the accepted dust-loop stamps/shading. Full-precision UV1 stores geometry controls; two shared smoke material instances cull the exterior/interior appropriately. Leading-only ground rollers append to the existing wall banks. No artwork/color/config/stone changes or downloads. See notes/weather-shell-front-roll.md.
