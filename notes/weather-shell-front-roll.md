# Defined hurricane shell and leading sand rollers — Testing

Denis confirms rocks are visible and wall/Main/sand colors are perfect on installed build `2EA1878509BE14600FD289132A238EFE`. Preserve those values and effects. His cyan hurricane test shows amorphous cloud masses instead of a readable column. He requests a defined hurricane visible from inside its eye and ground clouds rolling outward ahead of the leading wall only.

Matching log confirms native sand emits and both dark stone pools have nonzero live particles. No sand, stone, palette, darkening, map, movement or critical-event logic correction is part of this round. Existing config files are not overwritten. Before-change source/log evidence: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-shape-before_20261016_201132`; full prior source/bundle/DLL already retained in its deployed-build folder.

## Cause / replacement

The rejected hurricane used independent 500–1100 m camera-facing puffs. Spiral centre motion did not constrain their vertices to an annular surface: puffs extended hundreds of metres away from the reference profile. The screenshot establishes failed form; source establishes that the representation has no column-surface constraint. Do not keep adjusting that unconstrained puff size/opacity as if it defined a solid column.

Replace that core representation in WeatherVortexSmoke with curved, overlapping patches lying on one explicit uneven annular profile. Use the **same accepted wall atlas, smoke shader, stamp opacity, feather and shading**. Patch UVs cover complete smoke stamps, so no single longitude UV seam. Overlap hides patch edges. The vertex shader raises and orbits each complete patch, flares the profile toward the ceiling and fades its wrap as a whole; vertices do not independently wrap across the full height into stretched triangles. Profile radius near ground remains roughly 640 m and the eye remains open. Geometry coordinates now use full-precision UV1 data instead of reconstructing billboard centres from COLOR channels. No return to rejected ray-marched cube/noise.

Two shared private material instances select outside- or inside-facing triangles according to the camera's position relative to the local shell radius. From the eye, the inward faces show the same textured moving surface; from outside, outward faces define the silhouette. Near-surface camera-distance fade remains. The side test uses the mean radius; crossing the small irregular shell margin still needs Denis's video check for culling transitions.

Three lazy shared meshes: Low/Balanced/Max = 96/160/240 curved patches, 3360/5600/8400 vertices, 4608/7680/11520 triangles. One Graphics.DrawMesh per visible hurricane. No native PS per hurricane, CPU particle loop, per-frame vertex upload or camera-facing puff sorting. First-use mesh construction allocates; GPU vertex/overlap cost changes and remains unmeasured. All new meshes/materials dispose with Weather. Legacy assets remain loadable but unused for core rendering.

## Front rollers

WeatherVisuals tests each boundary bank's outward normal against storm velocity: dot > .45 * speed. Only leading banks append 8/12/20 ground puffs (Low/Balanced/Max) to their existing paused particle buffer; sides/rear keep their accepted wall/canopy particles without this new outward stream. Cached Leading flag rebuilds only on layout/quality/leading-state changes.

The same smoke shader/atlas/tint handles rollers. A dedicated leading-puff flag (green=1) moves them roughly 360 m outward beyond their starting distance, with low rolling vertical motion and fade-in/out before phase wrapping. Hue is unchanged; leading dust uses the existing lighter leading shading. No new draw/material/system/job or runtime particle simulation. Existing wall-body/canopy values remain unchanged. Boundary walls still surround the whole region.

## Short in-game check

1. Diagnostics OFF. Wait 40 gameplay seconds for a critical storm. Keep current settings/colors, or use the existing cyan hurricane test. From far enough away to see its base and top, check a continuous shaped shell rather than disconnected masses, and coherent rising spiral motion.
2. Walk into the eye and turn 360 degrees/look up: textured inward walls and upper flare should be visible around you, with a clear centre. Cross the shell again and report disappearing faces or sharp patch edges. Send a short clip covering outside -> eye.
3. Approach the incoming ordinary front. Low rolling clouds should travel outward ahead of it. Check a rear edge: the wall remains, but the new forward-moving cloud stream must be absent there. Ground-height conformance remains the previous region-height baseline.
4. Confirm approved color response, local sand and dark orbiting rocks retain their appearance. No need to repeat the full diagnostic sequence; normal startup logs identify the shaped-shell revision and existing live sand/stone counts.

Files: WeatherVortexSmoke representation replaced, WeatherSmoke/Visuals leading-row wiring, SmokeParticles shader vertex geometry/face culling/front roll; embedded bundle and related docs (~130 source lines changed/added). No new settings or Core/other-module changes. Unity authoring -BundleOnly only; Denis runs all game/visual/performance checks.

Unity 2020.3.49f1 -BundleOnly and Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with zero errors (10 existing non-Weather warnings). Game closed; disabled-install manifest absent. Installed MD5 matches build: **F48AE7406AB07812D789D6C113F98321**. Bundle SHA256 `E85FA08FE87E9AE4FBCBBCD08D2E081923643728C12A8DD8BCC1428FF92C4589`. Unique DLL/source/authoring record: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\weather-shell-front-roll_20261016_201752`. In-game shape/eye/motion/leading-only behavior and costs remain Testing. Startup identifies `Shaped spiral eyewall: curved wall-atlas shell, inward/outward faces and clear eye; front-only outward rolling sand; legacy volume/puff renderer replaced.`
