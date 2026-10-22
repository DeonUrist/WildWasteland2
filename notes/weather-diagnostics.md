# Weather diagnostic milestone — Testing

Diagnostics only: no visual defect is claimed fixed. Cuts, banding, grain and pale hills need layer ownership and controlled intervention. Target: the reference's uneven rotating column continuously merging into circular overhead circulation, wider in our case, with ground dust/fine airborne particles. Presentation quality is the criterion; vanilla fidelity is not required.

Baseline MD5: `11B29FB7102ECDA2A4884C468B26E214`. Preserved source/assets/log/DLL/checksums: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\diagnostic-baseline_20261016_184033`.

## Find the controls

Open WW Settings (default F3; use the configured key if rebound). Enable Debug is the public WW option in Apocasetter/F6 if Debug is hidden. In Settings > Debug expand Core and enable Developer mode. Then Settings > Developer > Weather / Weather (visual preview) > Weather visual preview. Developer search can find `Weather diagnosis` and reveal the category.

No config entries added. Diagnostic controls are temporary; clear, load/scene change and module shutdown reset them. Existing preview/area controls remain.

## First test: one bad interior view

1. Use Balanced and the same graphics/resolution/camera mode as the bad screenshot. Spawn a critical storm, wait at least 40 gameplay seconds, enter and frame the cuts/grain with recognizable terrain. Do not run the original Dust Storms event concurrently for this series.
2. Enable diagnostic controls and click **Capture snapshot and freeze**. Close Settings. Record Normal with Normal data output; the top-left label identifies each selected view.
3. Keep Normal data output and use **Next layer**: Engine baseline, Regional atmosphere, Wall only, Canopy only, Wall and canopy, One hurricane - shape, One hurricane - full. Capture the label with each picture; note which layer first shows the reported cut. Different camera framing is not proof.
4. For the pale hill compare Native fog scope OFF and ON while pitching down/level/up at that same hill, preferably on video. Return to Normal afterward.
5. Disable diagnostics; confirm normal rendering/movement resumes. Send labeled captures/clip and LogOutput.log. Snapshot: `BepInEx\config\WildWasteland\Weather\diagnostic-snapshot.bin`.

Investigate local clouds next only if ownership points there: native tint and brown tint are adjacent presets of the same visual-only Sand Player clone, forcing native emission/cap and full opacity. One-shot recoloring changes existing frozen particles without changing their position/velocity/lifetime. Source gradient evaluation uses each particle seed for color choice; native color-over-lifetime/material flow remain. This comparison does not mandate vanilla in the final renderer.

## Data views

Initially change layer presets with data output Normal. Use these only for the identified owner. They are diagnostic encodings, not artwork:

- Layer IDs: each bank has a distinct color; bank ID = region*12 + section + 1. Core ID = 100 + region*80 + column. Atmosphere is purple. Native local clouds have no data shader.
- Opacity: monochrome alpha, opaque debug output over submitted surfaces. Black zero-opacity rectangles intentionally show card/bounds coverage; do not confuse them with production defects.
- Entry/exit: logarithmic red/green entry/exit and blue path length. Smoke cards instead report card depth, scene depth and gap. Atmosphere reports the last nonempty region interval.
- Linear scene depth / World ray direction: fullscreen logarithmic depth or RGB world direction * .5 + .5. Other WW geometry is hidden.
- Density only: vortex transmission-weighted modulation; atmosphere optical depth; smoke atlas alpha. Unsupported on native clouds.

One-hurricane presets lock the nearest hurricane in the first eligible region. Select hurricane again resets selection. Shape-only retains current bounds/profile/reveal/sample count with constant noise modulation/color and no light/haze contribution; full restores current shading. No replacement geometry is introduced.

## Freeze / replay limits

Freeze holds Weather clock, region translation/phase ages, owned palette/sun direction, ground probes and particle simulation. Player/camera, native world time/sky controller and borrowed sand shader UV time remain live. Depth, culling, coverage and camera-dependent inside/eye weights respond to movement to expose projection/depth faults.

Hand-written binary snapshot stores regions/ages, settings/policy/resolution, camera matrices/pose/FOV metadata, palette, cached ground offsets and current particle seed/positions/velocities/sizes/rotations/colors/lifetimes. Restore validates save/seed/area settings and warns on policy/resolution mismatch. It never moves the camera/player or changes native weather/time. Match recorded framing/time/graphics manually across sessions. This is diagnostic state, not a game save or a full replay of Unity particle internals.

`[WeatherDiag]` logs state changes, camera matrices/graphics, selected core and capture/restore. No streaming files/logs. Particle readback happens only on capture/restore or tint transitions. GUI has a field-only wants predicate; diagnostic shader math is keyword-gated into WW_DIAGNOSTICS_ON. Off retains baseline math/artwork/sample counts. Normal equivalence and usefulness require Denis's game check.

## Scope / build

New WeatherDiagnostics.cs; diagnostic wiring in WeatherModule/Visuals/Atmosphere/Sky/Dust; three shaders plus one include and rebuilt embedded bundle. Roughly 500 source lines added/changed, plus related documentation. No Core/other module edits belong to this milestone. Unity 2020.3.49f1 -BundleOnly succeeded. Windows deploy/hash recorded below. No game automation or simulated visual verification.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with zero errors (10 warnings outside Weather). Installed DLL MD5 matches build: **5D5022B6DF961245E7EF972780946149**. Bundle SHA256: `1E5A6F920B8A7A46AB9B898B7F03215BDED4B0D14A5DBFFB79953DB7DC9DD37C`. Unique DLL/source/authoring/checksum record: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-diagnostics_20261016_190001`. Game was closed and disabled-install manifest absent before deployment. Status remains Testing; no in-game result or measured performance yet.
