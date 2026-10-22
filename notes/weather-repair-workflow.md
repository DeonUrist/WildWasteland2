# Weather repair workflow — evidence before changes

Requested by Denis after screenshot codex-clipboard-fdac9998-48d4-4c30-8049-118ae8db81de.png. This document specifies the implementation and verification sequence. The approved first milestone adds diagnostics only; visual corrections require the observations described below.

## Presentation target (Denis's latest clarification)

Presentation quality is the acceptance criterion; using vanilla sand, any specific system or any asset pack is not a requirement. The reference codex-clipboard-64f412af-9317-4b59-9043-bbf03bd94623.png establishes the desired vortex form and motion, with **width as the intended difference**: ours has a wider uneven rotating column, continuous circulation spreading into a broad circular cloud ceiling, turbulent ground dust and varied fine airborne particles/debris. Retain the requested eye and interior view. Keep the existing desaturated brown storm palette and blend columns inside the critical storm. The reference's blue sky/white clouds are not a requested palette change. Do not substitute a straight cylinder, crossed pictures or a detached cap.

## 1. Established facts and unresolved causes

- Installed baseline MD5, checked while writing this plan: **11B29FB7102ECDA2A4884C468B26E214**. Preserve this DLL and snapshot the current Weather sources/authoring inputs before starting diagnostics. An earlier staged build preserves the incoming wall/sky appearance Denis accepted.
- The current log confirms the native Sand Player clone loaded, the native Azure fog scope executed, and critical mode began. Missing assets or a missing native fog callback are not supported explanations for the supplied frame. Those logs establish execution, not visual correctness.
- Source confirms hurricanes use a ray-marched 3D density field in a box. The screen appearance resembles curtains; that does not prove the renderer uses crossing image planes.
- Source confirms wall/canopy detail uses separate banks of camera-facing particles, sorted within each bank. Cross-bank/volume transparency is a candidate to inspect, not an established cause of seams.
- The screenshot establishes hard seams/cuts, banding/stretching, fine grain, pale/dark atmospheric separation and poor volume readability. It does not identify the responsible passes or establish animation direction, rotation quality, temporal stability or performance.
- The causes of the grain, planar-looking cuts and look-angle-dependent pale hills remain open. Previous source-based explanations of these effects were not validated in game and must not be treated as proven.

## 2. Scope and operating rules

Preserve moving region geometry/map data, density settings, ordinary/critical sequencing, accepted incoming wall/sky appearance and audio. Work stays in src/Modules/Weather and tools/weather-assets, with related Weather documentation only. No game installation assets/configs or another module are changed for diagnosis. Follow AGENTS.md scope gates before an implementation exceeds its existing authorization.

Denis runs the game and supplies observations/recordings. The agent does not launch, control or screenshot the game, or create test scenes/harnesses/simulations. Authoring only compiles required bundle assets with -BundleOnly. Real gameplay rendering is the visual reference.

Keep diagnostic features default-off and separate from normal output. Do not change normal shader math, artwork, color, opacity, sample count, quality or emission as part of adding diagnostics. No scene-color blits/readbacks, extra production fullscreen passes, per-frame logs, scene searches or CPU live-particle loops. Diagnostics use existing jobs/camera callbacks and log only when selected state changes.

One identified cause and one corrective patch per round. If the correction fails its stated test, revert that patch before the next attempt. Accepted corrections accumulate; rejected experiments do not. A successful compile confirms compilation only.

## 3. Baseline and repeatable observation

1. Snapshot only Weather source/authoring files plus bundle and installed DLL into a unique external revision folder; write checksums and retain the original supplied screenshot. Do not reset or commit the shared worktree.
2. Record settings, performance preset, camera mode, resolution, time of day and weather state. Record current region origins/headings/sizes, ages, Weather clock and relevant random seeds in a diagnostic record. Record selected camera pose/FOV and the actual render matrices on diagnostic state changes, not every frame.
3. Add a Weather-only freeze control: freeze region translation, phase age, shader clock and owned particle simulation, while Denis can still move/look. Do not freeze or modify the game's weather controller, time, saves or other modules. Restore recorded storm state across comparisons; do not teleport or automate the player/camera.
4. Use Balanced for diagnosis. Fix camera framing and background landmarks for each comparison. Resolution and image effects stay identical. Native particle comparisons use the same seed and identical source parameters, with tint as the sole difference between the paired views.
5. Preserve five viewpoints: incoming front (accepted reference), crossing the boundary, the reported bad interior view, approaching one hurricane, and inside its eye. At the pale-hill viewpoint, Denis keeps the same hill visible while pitching down/level/up. Animation is assessed in a short recording, not a still.

Deliverable: baseline record/checksums and a short capture checklist using fixed named views. Gate: the reported bad view can be reproduced and held still before evaluating alternatives.

## 4. One diagnostic build with explicit layer ownership

Add temporary controls on the existing Weather tool page. Each preset displays/logs its name and active layers. Start with the reported bad viewpoint; do not require every preset at every viewpoint. Re-enable the full normal scene after each isolation series.

| Preset | Draw/override state | Question answered |
|---|---|---|
| Normal | Current production output, unchanged | Does the baseline reproduce? |
| Engine baseline | All WW weather rendering and native sky/fog overrides off | Is grain/pale terrain already present without our weather? |
| Regional atmosphere | WW regional fog/sky only; no wall/canopy/core/local particles | Are seams/bands in atmospheric integration or sky coverage? |
| Wall only / canopy only / both | Selected particle layer, without other WW weather layers | Which bank/layer owns visible card edges, stretch or ground-contact problems? |
| One hurricane, shape only | One selected hurricane; original shape field with constant density modulation/color, no noise, light or haze | Do cuts survive without texture/shading or overlapping volumes? |
| Same hurricane, full shader | Same pose, size, state and sampling, restoring original modulation/shading | Does adding texture/light create the bands/grain? |
| Vanilla cloud copy, native tint | Only our visual-only native clone, with source parameters restored | Does the clone actually reproduce native clouds under baseline rendering? |
| Same clone, brown tint | Identical native parameters, changing tint only | Does recoloring reveal/amplify grain or alter blending/readability? |
| Native fog scope off / on | Same composed scene, changing only our native fog override | Does the fog intervention change the pale hill/angle dependence? |

Also provide diagnostic output for the owner being investigated: opacity/optical depth, entry/exit distances, sampled linear scene depth and reconstructed world-ray direction. Add a bank/object ID view for boundary attribution. These inspect the actual game render path; they are not replacement artwork or simulated previews.

Deliverable: one diagnostic DLL, source record and exact toggle/capture sequence. Build/deploy according to section 8 below. Gate: each reported artifact has an owning layer or interaction. If ownership is not established, collect the specific missing view/log; do not start an art revision.

## 5. Turn observations into a proven cause

For each defect, record: screenshot/clip and diagnostic state -> reproducible code path -> narrowly defined intervention -> predicted result -> observed result. A candidate becomes a cause only when the controlled intervention removes the defect and restoring it brings the defect back, with other conditions fixed. This ownership/effect test does not by itself prove the final artistic correction is satisfactory.

| Observation | Next examination and decision |
|---|---|
| Seam remains in one shape-only hurricane | Inspect its ray construction, bounds, clipping, front/back shell intervals and sample-position continuity using entry/exit/opacity views. Do not tune noise or feathering. |
| Shape-only volume is clean; full shader introduces bands | Isolate density texture modulation, noise coordinates/filtering, lighting and haze one at a time. Check actual texture mip/format/import state and coordinate continuity. |
| Each isolated object is clean; combined output cuts | Inspect transparent draw order, camera-facing bank interactions and overlaps. Establish the offending ordering/occlusion relationship before choosing a composition change. |
| Grain exists in the native cloud-only view | Compare native source/clone material keywords, texture/scale, transform/scaling mode, lifetime/velocity settings, opacity/emission and image effects. Correct a demonstrated mismatch; if the native material itself owns it, document that before changing native appearance. |
| Grain appears only after tint or other layers are added | Inspect that exact color/blend/composition interaction; retain the native motion/shape. Do not replace it with another custom emitter. |
| Grain remains in engine baseline | Identify the native rendering/image-effect contribution. Any required change beyond Weather needs a concrete scope discussion, not an unrelated module edit. |
| Pale hills respond to native fog scope toggle | Trace fog density/color restoration and actual pass ordering. Correct the proven fog interaction and preserve normal weather restoration. |
| Pale hills persist with native scope off | Use scene-depth/world-ray views to inspect depth/UV/projection handling and identify any later native pass. Revert an ineffective fog intervention; do not stack exposure/tint patches. |
| Ground/roof boundary exists only in wall/canopy layers | Compare geometry/opacity/depth coverage at the boundary. Determine whether placement, depth fade, silhouette or separate layer colors make the discontinuity. |

Deliverable: an evidence table naming confirmed causes and rejected candidates. Gate: each corrective change has a code-level reason and a falsifiable predicted result. Unknowns remain explicitly unknown.

## 6. Correction order and acceptance gates

Execute these stages in order, using the causes found above. Do not preselect a new shader architecture or asset pack before the relevant diagnosis. A single proven mechanism may resolve several listed symptoms; avoid separate compensating fixes for each symptom.

1. **Rendering continuity:** fix the demonstrated bounds/depth/order/integration discontinuity. Validate with shape-only output, camera rotation, lateral movement and near/inside/far views. Gate: no visible seams, cube/triangle cuts or disappearing sections. Opacity reduction is not acceptance if it merely conceals the discontinuity.
2. **Local dust presentation:** use isolated native/brown cloud views as a comparison to locate current defects, then choose the renderer/resources supported by that evidence. Gate: natural moving smoke clouds, wind-driven sand gusts and varied fine particles, soft fades, no abrupt following and no dense uniform speckled veil in the composed scene. Vanilla fidelity is not the acceptance gate. Document the cost of any changed particle cap or rendering approach.
3. **Hurricane shape and interior:** use the now-clean rendering to establish the wider reference form, irregular wall thickness, continuous silhouette and central eye. The rising column must continuously widen into circular overhead circulation, with turbulent dust grounding its base. Gate: the surrounding smoke is visible from outside and inside, with no sheet intersections, artificial rim or detached roof. Verify entering/leaving the shell on video.
4. **Hurricane motion and smoke structure:** establish large, medium and fine features at consistent world scale. Verify a feature rotates around the axis and travels upward; measure apparent rates from the recording before tuning them. Gate: coherent rising circulation, soft irregular billows, no corrugated/tiled/stretched pattern, no obvious loop or popping. Author/change textures only if an isolated texture deficiency has been established.
5. **Atmospheric integration:** combine the accepted wall, ground dust, hurricanes and ceiling under a common palette/visibility model. Gate: natural depth/overlap, a gradual ground-to-wall and wall-to-roof transition, visible broad light/shadow structure, no detached cream band, and no angle-dependent bleaching of the fixed background hill. Recheck the accepted incoming view after every change here.

If any gate fails, stop that stage, retain its evidence and revert the failed correction. Do not advance to finer artwork while structural defects remain.

## 7. Performance and lifecycle validation

Measure before and after at the same saved views/settings. Compare no WW weather, ordinary storm and critical storm for Low/Balanced/Max. Record available CPU/GPU/frame-time measurements and relevant native rendering/particle work; Weather scheduler self-time alone is incomplete. Keep FPS cap/VSync/graphics conditions identical and record them. GPU/CPU usage percentages and a single FPS screenshot do not establish cost.

Retain the repo's shared managed budget (whole WW average <=0.5 ms/frame, worst <=2 ms) and verify Weather fits the remaining budget; do not assign that whole budget to Weather. Report measurements the available tools cannot provide. No zero-performance-loss claim without comparable evidence. If visual acceptance and the measured budget conflict, present the measured tradeoff before cutting features.

Quality tiers can scale secondary detail, particle counts and integration work while preserving a coherent wall, correct depth, a readable eye and the same gameplay region/count logic. Confirm pause/resume, first/third person, optics, map/ESC, clear, module off/on and load/scene cleanup. Clearing Weather must restore native sky/fog/light/audio. Remove or leave all diagnostic flags off for the normal handover.

Gate: accepted visuals remain correct across tiers/lifecycle; performance results and any remaining limitations are recorded, not inferred.

## 8. Build, deploy and handover — every diagnostic/corrective build

1. Finish the narrowly scoped source change and its predicted result/test instructions. No unrelated work or commits.
2. If assets changed, compile only with the matching Unity 2020.3.49f1 authoring project using -BundleOnly, then embed the successful bundle. Do not create visual test scenes.
3. Check the disabled-install manifest and whether the game is closed. Never kill/retry around a locked DLL. Follow AGENTS.md if closure/restoration is needed.
4. Run Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`. Zero errors are required. After success, stop verification loops; only required deployment/hash/staging/documentation remain.
5. Compare built and installed MD5, preserve a uniquely named DLL and source/asset revision record, and state exactly which diagnostic or correction it contains.
6. Hand Denis the cause/evidence (or diagnostic question), files and approximate changed lines, installed MD5, exact short in-game actions, expected result and confirming log lines. Denis supplies the runtime result. A deployed build remains Testing.

## Immediate next milestone

Implement **only the diagnostic controls/repeatable snapshot in sections 3–4**. Keep normal rendering identical to baseline when diagnostics are off. Use one diagnostic build to locate the seams, grain and atmospheric split. No new hurricane artwork, emitter replacement, guessed projection flip or fog workaround is part of that milestone.

Baseline preserved before diagnostic edits: `V:\Repos\Apocalypter\_tools\WWWeatherAssets\Builds\diagnostic-baseline_20261016_184033`, including source/authoring inputs, installed DLL, log and SHA256.csv. Diagnostic implementation/handover is tracked in `notes/weather-diagnostics.md`; in-game ownership and causes remain pending Denis's captures.

Diagnostic milestone built/deployed with zero errors: installed MD5 **5D5022B6DF961245E7EF972780946149**. Normal artwork unchanged by design; runtime equivalence awaits the first test. No corrective/art stage has started.

Denis supplied the complete labeled isolation series. Full hurricane density/shading owns the ribbons; shape-only removes the pattern, and fog OFF/ON does not resolve it. Both local-cloud views were frozen with zero particles (confirmed in log), so their absence is not a tint comparison. First narrow corrective round: step-aware density filtering instead of fixed mip 1; details, falsifiable prediction and reduced two-part test in notes/weather-density-filter.md. No art/architecture or gust-emitter correction stacked into this round.

Denis reports filtering is better, with remaining transparency and palette mismatch. He explicitly expands this round to three live colors, denser external/hurricane wall bodies, missing dust/clouds and flying stones. Implementation/test details: notes/weather-live-colors.md. Deployed zero-error build MD5 **1D24F0AFF1F84C65EA4E593BC1B8E93D**. Source incompatibility corrected: native visibility-based Sand Player simulation culling versus WW camera-gated renderer; actual nonzero count must be confirmed by the new normal-play log. Stones were absent because no visual layer submitted them; bounded native visual pools now added. This is not cinematic/performance acceptance.

Next accepted evidence: Denis approves external wall texture/color control; matching log confirms local sand now emits, stone pools initialize. Remaining hurricane seams/bands lead to his explicit requested replacement: use the same wall material/surface spiraling around an eye. Scope adds independent vanilla-sand tint, dark readable stones, all perimeter edges walled and depth darkening 0–1. Implemented/deployed **2EA1878509BE14600FD289132A238EFE**, zero errors; notes/weather-spiral-perimeter.md records new architecture, costs, limits and short test. No legacy volume cube is drawn. Further visual/performance acceptance remains Denis's test.

Denis confirms rocks visible and current wall/Main/sand colors perfect. Cyan hurricane test rejects the unbounded billboard-puff form. Replace that failed core representation (do not stack size/opacity guesses) with an explicit curved annular smoke surface, outside and eye-facing materials, same wall stamps/shading. Add front-only outward ground rollers to existing wall banks; all accepted settings/effects retained. Deployed **F48AE7406AB07812D789D6C113F98321**, zero errors. Details/tests/costs in notes/weather-shell-front-roll.md; in-game acceptance pending.
