# First correction after layer isolation — Testing

Evidence supplied by Denis: 12 labeled captures, diagnostic DLL MD5 `5D5022B6DF961245E7EF972780946149`, matching installed checksum and current LogOutput.log. Baseline log/snapshot/bundle/shader/DLL preserved under `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-isolation-evidence_20261016_191134`.

## Established observations

- Engine baseline and regional atmosphere do not show the hurricane ribbon pattern.
- Wall/canopy isolated views show broad soft blobs, separate from the full hurricane ribbons; they are not proof that all wall/ceiling art is accepted.
- One hurricane shape-only versus full: the full density/shading path introduces the pronounced streaks/ribbons and sharp internal lines. This establishes ownership, not an individually proven cause within that path. The camera framing differs slightly between captures, so keep that limitation in the comparison.
- Native fog OFF/ON does not visibly remove these core defects. No further fog/tint correction is justified by this series.
- Both local-cloud images are empty. Log at Weather clock 67.1232758 explicitly records `Local-cloud snapshot: count=0`, simulation Local, shader Hovl/Particles/SoftNoise. Freeze preserves an empty system and prevents subsequent emission, including in forced-cloud presets. These images do not establish a color/material failure or prove whether the emitter is empty when running.

## Narrow correction and prediction

VortexVolume sampled the 128-cubed density at fixed mip 1 for both noise frequencies, independently of distance between ray samples. Runtime remains 12/18/24 samples; density includes up to a 96-cell lattice plus 2.17x detail and height twist. The shader has no filtering for details smaller than its march interval. This is a source-established sampling mismatch; the proposed causal link to the photographed ribbons remains subject to the controlled retest.

Replace only the two fixed LODs with the texture-space march footprint, including the analytic derivative of rotation/sway with height. The fine read adds log2(2.17) to the mip. No sample-count, color, shape, velocity, texture/artwork, opacity or fog changes. Same two texture reads; additional bounded GPU arithmetic, no managed CPU simulation or extra draws. Performance remains unmeasured.

Prediction: compared at the same frozen pose, full hurricane density should lose high-frequency corrugation/ribbons while shape-only is unchanged. It may become smoother and lack the eventual cinematic detail: this is the continuity gate, not final art approval. If the reported pattern remains, revert this filtering correction before another candidate. This round does not widen the column, add ceiling circulation or change dust emission.

WeatherDust adds one snapshot log line (no emitter changes) for native time, culling, speed, loop, emission/rate, renderer visibility and active state. Native serialized source `Assets/raw/ParticleSystem/unnamed__101578.json` has loop/prewarm true, 100 particles/second and PauseAndCatchup culling; runtime emission/culling must be checked before choosing a gust fix. Local-space particle snapshot replay needs separate correction before using particle positions as a world-space reproduction; do not use Restore for the gust test.

## Denis's short test

1. Compare the existing bad view using One hurricane - full and shape with freeze on. Keep the camera fixed and send both images. Report whether the ribbon/cut pattern disappeared or only blurred.
2. Choose Local clouds - brown tint, turn freeze OFF, close Settings and wait 3 gameplay seconds without moving. Reopen and Capture snapshot and freeze. Send that image and LogOutput.log. This distinguishes an empty frozen capture from an emitter that cannot generate particles. No need to repeat the 12-picture sequence.
3. Return preset/data to Normal and disable diagnostics when finished. Normal storm logic, accepted front/sky and audio should behave as before.

Change size: about 12 shader lines and one WeatherDust diagnostic log line, embedded bundle and related documentation. No config entries or cross-module changes. Build/deploy status below.

Unity 2020.3.49f1 -BundleOnly and Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded, zero errors (10 existing non-Weather warnings). Game closed, no disabled-install manifest. Installed MD5 matches build: **C4C81E88BC70DEB611EB04291E19BEA4**. Bundle SHA256 `29E5902AEA1A48DA989A429A37E8BBE3363AE0F0F49C1E03C525511979E52220`. Unique DLL/source/assets record: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-density-filter_20261016_191334`. Denis's visual/performance retest pending.
