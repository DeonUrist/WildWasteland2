# Weather live colors / dense walls / visible debris — Testing

Denis reports the density-filter revision is better and now requests dense hurricane/external walls, three independent in-game colors, local dust/clouds and flying stones. Keep the accepted density filtering. New work remains entirely in Weather and authoring shaders, plus related documentation.

## Controls

WW Settings (default F3) > Developer > Weather > Weather visual preview > Storm colors. If Developer is hidden: Enable Debug in public WW/Apocasetter settings, then Settings > Debug > Core > Developer mode. Each of the three settings has Red/Green/Blue sliders and numeric inputs (0–1), with a readable hex value and one reset button. Values save in settings.cfg [Weather / Colors]:

- External wall color: incoming front and ceiling billows.
- Main tint color: regional haze, camera-scoped native sky/ambient/sun tint and local sand clouds. The borrowed terrain textures/UI are not modified; existing native lighting/post-processing still contribute to the result.
- Hurricane color: rotating shell and its visual stones.

Defaults share muted brown (.55,.43,.33). Changes apply live, including while owned storm time is frozen. Palette changes recolor current local cloud particles once, without per-frame scanning. Sun color/intensity and sky/ambient changes restore after the selected camera and on clear/shutdown. Config binding uses normal per-entry defaults, preserving existing settings.

## Dense walls

Hurricane extinction coefficient changes .008 -> .05; clear eye and soft shell/height boundaries remain. External wall/ceiling stamp extinction changes 2.6 -> 8 and removes the .58–.78 opacity multiplier, keeping soft radial edges/depth fade. This makes the bodies denser without adding samples or layers, while the crossing transition and geographic inside-only core reveal remain. Visual density is not a physical collider. No claim that every viewing angle/silhouette now matches the reference; Denis checks that the dense bodies hide background from inside/outside and that the eye is still clear.

## Dust / rocks

Latest log confirms source assets initialized but contains no requested post-unfreeze emitter capture. Earlier zero-particle frozen capture is retained as evidence, not a tint failure. Source native Sand Player uses PauseAndCatchup culling, while WW gates its Renderer per camera and starts with an empty stopped system. This visibility-dependent simulation mode conflicts with the separately gated camera renderer. Set only that mode to AlwaysSimulate so this bounded camera-local emitter can generate particles independently of its previous render visibility. Preserve native emission, size, flow/lifetime/shape and 60/100/100 caps. A once-only normal-play `[Weather] Local sand live:` line reports actual particle count/time/rate/culling after startup; this correction's runtime success remains unverified. If still zero, investigate that line rather than adding another speculative emitter patch.

Flying rocks were not previously submitted at all. New WeatherDebris clones only the native Sand tornado loop/Stones visual child (particle system + renderer; no FSM/physics/audio). Native sprite/orbital motion, 100x native parent scale, radius aligned with our wider shell (580 m). Disable collision/trigger modules explicitly. At most 1/2/3 active pools on Low/Balanced/Max, caps 32/64/96 particles each, rate half cap/second. Eligible critical columns must be visible, within 1800 m and over half formed. No damaging forces, rigidbodies, scene scan or managed per-particle simulation. Existing Weather tick owns all pool work; pools stop when unused, clear/load/module off and freeze with diagnostics. Native simulation/rendering adds bounded cost and still needs measurement. `[Weather] Visual stone pool ready:` confirms asset/pool creation, not visual acceptance.

## Test

1. Start ordinary preview ahead; approach and cross it. Wall body should conceal background, with soft silhouette and gradual entry. Tune External wall color with RGB; only wall/ceiling color changes directly.
2. Critical preview, wait 40 gameplay seconds. Approach a column from within the storm, then enter its eye. Its shell should be much denser from either side; central eye stays open. Tune Hurricane color separately.
3. Set Main tint color to an unmistakable temporary hue to establish control ownership, then desired brown. Check haze/local cloud/sky and terrain light respond, UI does not. Reset colors if needed.
4. With diagnostics OFF and game unpaused, wait at least three seconds inside the ordinary storm; check moving local dust clouds. Approach a nearby critical column for orbiting stones. Send a short clip and LogOutput.log if either remains absent. New normal-play log lines need no debug switch/manual snapshot.
5. Clear previews: native sun/sky/ambient and audio restore; no particles/stones remain. Test Low/Balanced/Max for secondary particle scaling. No performance acceptance inferred from compilation.

Files: new WeatherPalette.cs and WeatherDebris.cs; WeatherModule/Visuals/Sky/Dust wiring and two shader coefficients (roughly 190 added/changed source lines), embedded bundle and documentation. Three new Color config entries. No Core or other module changes. Build/deploy recorded below.

Unity 2020.3.49f1 -BundleOnly and Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with zero errors (10 existing non-Weather warnings). Game closed; disabled-install manifest absent. Installed MD5 equals build: **1D24F0AFF1F84C65EA4E593BC1B8E93D**. Bundle SHA256 `C9036956B9054FB8250C2E1197160AF6038E69F60CA74E6D126334BD97289387`. Unique DLL/source/authoring record: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-live-colors_20261016_192743`. In-game appearance/emission/performance remains Testing.
