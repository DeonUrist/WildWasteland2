# Proposed WW incorporation scope

This is new feature work authorized in principle by Denis's request, not a fix for an existing issue.
Denis approved the listed Core/CleanHUD/Performance integration scope on 2026-10-16. The first manual visual milestone is implemented; see `notes/weather-preview.md` for its status and review steps.

## User-visible behavior

- Independently moving storm areas, visible as actual bounded regions on the map and from outside in the world.
- Ordinary dust: low visibility, cosmetic lightning and cosmetic flying debris, no health damage, movement penalties or vehicle forces.
- Mad Max hurricanes: a dust envelope with funnel cores, lightning/debris, wind/lift/torque affecting valid nearby physics bodies, and native-compatible damage.
- Visual quality follows the existing Performance presets. Movement, footprint, dangerous area, force rules and damage do not change with visual quality.
- Distant/unloaded storms continue travelling as lightweight data. They do not create distant active physics or instantiate entire storm volumes.

## Runtime milestones

1. **Visual/manual-spawn milestone:** bundle loader, pooled wall/cloud/funnel visuals, shared scheduler, clean teardown. No automatic spawns or damaging forces yet.
2. **Moving-area milestone:** stable world coordinates, shared storm footprint, map fill/outline/movement heading, outside-to-inside local fog transition.
3. **Atmosphere milestone:** shader-animated lightning, dust flashes and cosmetic debris; sun/night color integration and shelter handling.
4. **Hurricane milestone:** cache nearby valid bodies, apply force smoothly in physics steps, integrate existing damage events without duplicating native storm effects.
5. **Persistence/quality milestone:** sidecar storm state, sleep/time-jump behavior, Low/Balanced/Max effects and player controls, diagnostic counters.

Denis verifies each build in game. Native SandStorm spawn suppression/replacement must be scoped to the Weather module and reversible.
No game automation, mock gameplay simulations or temporary game test harness is proposed.

## Protected files that need approval before changes

AGENTS.md section 0.1: "Do not touch src/Core/, another module ... unless the fix cannot work without it. If it must, stop and ask Denis first, saying which file and why."

The following are the specific anticipated integration boundaries; final edits will use the smallest existing mechanism:

| File | Necessary change |
| --- | --- |
| `src/Core/Plugin.cs` | Register the new Weather module with existing lifecycle and scheduler ownership. |
| `src/Core/Services.cs` | Define a small shared weather-region/visual-budget contract, so CleanHUD and Performance do not reference Weather implementation types. |
| `src/Core/UI/CorePages.cs` | Expose ordinary-storm/hurricane controls in World settings and approved developer controls using the existing UI. |
| `src/Modules/CleanHUD/CleanHudModule.cs` | Attach/detach a map-area renderer through the existing map lifecycle; new area-rendering code remains a separate small file in CleanHUD. |
| `src/Modules/Performance/PerformanceModule.cs` | Publish preset policy through the shared contract on settings/lifecycle changes; preserve existing preset meanings. |

Primary implementation lives in new `src/Modules/Weather/` files. A new CleanHUD weather-map renderer handles map projection/clipping and reads the shared region contract.
No csproj/build.sh/reference/language-version change is expected. New `.cs` and embedded assets are already included by existing globs.
Existing NPC/Patrols optional ApocaDustStorm bridges are not changed speculatively; any necessary compatibility work will be explained before editing those modules.
If integration needs other protected files, pause and name them rather than expanding silently.
Documentation changes are limited to the relevant inventory rows after implementation.
No commits, pushes, releases, issue closure or Verified status changes are authorized by this asset preparation.

## Known native behavior, verified in the offline source

- `Sources/fsm/prefabs/SandStorm.md` defines Move, SandPlayer, and three Tornado/TornadoDamage pairs.
- Move initially faces the player, then uses local velocity 20-30 m/s. It is not a persistent geographic weather model.
- Tornado uses Explosion in bursts; TornadoDamage sets Bodypart.Damage then sends Damage.
- `Sources/code/Assembly-CSharp/HutongGames.PlayMaker.Actions/Explosion.cs` allocates Physics.OverlapSphere results on each action execution.
- Native force/damage and new Weather forces must not run simultaneously on the same replacement storm.
- Azure local weather support exists, but requires checking its active profile/property path before adoption; do not assume adding a zone gives an appropriate local visible storm.

## Performance policy proposal (not benchmark results)

| Tier | Visual policy |
| --- | --- |
| Low | Few shell/cloud layers, sparse shader-driven debris, bolt geometry/cloud flashes, no extra real lights. |
| Balanced | More formation variety, soft-depth dust where the camera supports it, a small collision-aware cosmetic debris pool, budgeted cloud relighting. |
| Max | Extra local detail/viewing angles and richer baked-light response; brief unshadowed lights only if measurements support them. |

Vanilla preset / Performance module disabled: use a defined Weather-owned default (initially Balanced), not a disappearing storm or altered gameplay.
Physics remains bounded in all tiers. Additional real car collision work cannot be made free by a visual shader.
The GPU strategy starts with simple meshes and bounded overdraw. Add a reduced-resolution dust target only if measurement justifies its depth/composite overhead.
The first bundle contains a two-dimensional texture approach, not runtime VDBs or fluid simulation.
