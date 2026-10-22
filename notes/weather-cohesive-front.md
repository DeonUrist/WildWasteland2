# Cohesive weather front — 2026-10-16 — Testing

**Follow-up:** incoming wall/sky accepted by Denis; hurricane/local-dust/fog changes in [current handover](weather-eyewall-native-dust.md) supersede those parts below.

Denis's game screenshots at 17:43–17:44 reject the recovery renderer: brown column against blue sky, transparent wall, suspended identical streaks and U-shaped vortex tops. Existing region movement/map/density logic remains intact. This revision is authorized by Denis's request to reuse the reference's principles and fix the wall. Only Weather, its authoring assets and related documentation are changed. No source-mod code or assets are copied/adapted.

## Source comparison correction

The linked **v0.1.26** resolves to `c9f1d2cf19f3c1e03344e0c75cd3c6e82796d5d2`. Earlier notes about HaboobWall/HaboobTransition and their animated atlas describe later main `927962f67024de217ced655c8a2622300ebd3c45`, not this release. Do not describe those as v0.1.26 features.

Exact-tag StormAssets creates a **96×96 static procedural alpha texture**, using an ordinary alpha-blended particle shader. StormVisuals uses world-space particles with terrain pickup, size/opacity lifetime curves and distinct low ground dust/rising plumes. Emission is batched at 10 Hz; velocity targets are updated at 20 Hz while Unity integrates movement each frame. StormLighting/AzureAtmosphere coordinate the native sky, ambient/directional lighting and atmospheric colors; StormFog/StormHorizon/StormDistanceFog maintain matching visibility and backdrop. The coherent atmosphere/motion explains the stronger appearance, rather than higher-resolution artwork. These are source observations, not a measured performance comparison. Sources:

- https://github.com/Jaime-Wolf/Dust-storms/blob/v0.1.26/Source/StormAssets.cs
- https://github.com/Jaime-Wolf/Dust-storms/blob/v0.1.26/Source/StormVisuals.cs
- https://github.com/Jaime-Wolf/Dust-storms/blob/v0.1.26/Source/StormLighting.cs
- https://github.com/Jaime-Wolf/Dust-storms/blob/v0.1.26/Source/AzureAtmosphere.cs

## Causes and implementation

- Sky: old closest-horizontal-point projection covered a bounded central silhouette, yielding the tall brown column. Remove that projection; broad proximity-driven sky coverage extends through the zenith with continuous 3D cloud variation. Sky rays now integrate region dust beyond the camera far plane, while opaque terrain still clips dust by its real depth. Previously a far-plane sky depth could terminate before the approaching region.
- Cohesion: WeatherSky temporarily tints the native Azure sky color uniforms and dims the actual sun/ambient lighting for the selected gameplay camera. Values are captured/restored around camera rendering, and restored on suspension/disable. It does not change Azure controller weather/profile/time, exposure, save data or GI probes; no scene search/reflection. Native APIs/property names were checked in Sources/code/Assembly-CSharp/UnityEngine.AzureSky/AzureSkyRenderController.cs and AzureShaderUniforms.cs. Real game post-processing remains a runtime verification gate.
- Wall: continuous regional extinction supplies solidity beneath moving billow detail. Ordinary ceiling increases from 480 to 1050 m; the 1500 m critical ceiling remains. Smooth uneven crest, soft sides and thinning rear are preserved. Wall billboards span the body into its cloud roof, with rolling GPU displacement, stronger bounded alpha and broad overlapping canopy coverage. Particle overlap is reduced to 24/48/80 wall + 3/4/6 canopy per visible section; layout uploads remain conditional. No scene-color read/blit/copy is restored.
- Local motion: delete the old near-mesh draw from the active path. WeatherDust uses two native world-space particle systems (low scud/rising plumes), finite randomized travel/lifetime/size/rotation, native growth and opacity curves, and randomized 32-frame atlas phase. Emission batches run at 10 Hz in the existing Weather job. One layer-14 terrain ray per batch refreshes one of eight persistent upwind source patches. Low/Balanced/Max caps are 160/360/640 per layer, maximum emitted 20/40/70 per second per layer at full strength before rejected placements. No managed loop over live particles, per-grain objects, particle collisions or new update job. Ground dust starts gently within roughly 450 m of the area, and strengthens inside. Pauses/clear/load/off stop it; camera jumps clear obsolete terrain sources.
- Vortices: replace the single closest-point estimate and huge upper radius with 8/12/18 bounded density samples through a filled widening profile. The upper volume fades into the shared roof, avoiding the previous isolated oversize rim. Actual visual removal of U shapes must be confirmed in game.
- Audio: includes the previously prepared WeatherAudio implementation using installed Weather/sound/sandstorm.wav. One shared loop, silent beyond 1200 m/full source volume within 150 m and inside; native master volume respected. The external WAV is not copied or embedded.

Approximately 400 renderer/shader lines changed or added, plus the earlier approximately 115-line audio follow-up. Sources: WeatherSky.cs, WeatherDust.cs, WeatherAtmosphere.cs, WeatherSmoke.cs, WeatherVisuals.cs; three shaders in tools/weather-assets. Asset builder adds BundleOnly compilation so no authoring scene is rendered for this fix. The external matching Unity 2020.3.49f1 editor successfully compiled the asset bundle. No game launch/automation/screenshots, test harness or simulations were used.

## Game test — Denis

1. Restart with this DLL. Clear previews, select **Dust storm ahead**, look along the full front and above it while approaching. Expect a continuous dense wall, rolling lighter billows and a broad brown/cloudy transition through the zenith, without a brown central pillar against blue sky. Check terrain/player textures remain intact.
2. Enter the storm on foot and in a car. Expect low dust crossing the ground and rising, changing plumes with varied speeds/scales; the old uniform diagonal streak layer is gone. The shared WAV should grow from silence to full volume near/inside.
3. Clear; spawn **Critical dust storm preview ahead**. Wait 40 gameplay seconds, then enter. Columns remain hidden outside; inspect filled upper shapes merging into the roof, not isolated U-shaped rims. Check lightning remains visible.
4. Clear/disable the module: original sky/lighting/audio must return. Compare Low/Balanced/Max steady state, camera turns, first/third person and pause/resume. Record native particle/render GPU time as well as scheduler time; zero performance loss and cinematic equivalence have not been established.

Startup confirms `[Weather] Cohesive front revision:` and `[Weather] Native moving ground dust:`. Audio confirms `[Weather] Storm sound loaded:`. No new configuration entries. Build/deploy MD5 recorded below when complete.

Build/deploy complete: Windows dotnet build Release -p:Deploy=true succeeded with zero errors and ten existing warnings. Built/installed MD5: 202B891642584A3FBC49165BFF9FBF3E. Unique staged copy: V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\WildWasteland_0.1.28_weather-cohesive-front_20261016_180056.dll. Bundle SHA256: 0B7C460277166953F58DDE6E6DD70A573008E07B78E30B8C40EF15058CAB1332. Game was closed and disabled-install manifest absent. Includes audio. No new settings. No game visual/performance test by agent; remains Testing.

