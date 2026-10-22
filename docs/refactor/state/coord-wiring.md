# Wave B coordinator integration (2026-10-22)

Branch `refactor/coord-wiring`, worktree `V:\Repos\Apocalypter\WW2-wiring`. Main remains a tracker until the complete reviewed integration is ready. Preserve every existing worktree and local edit.

## Completed

- Tools owns LOD export and authoring; Compatibility owns the detected Sigma FSM guard.
- Typed patrol/vehicle/EngineSim ownership and patrol retention registrations replace Core gameplay coupling.
- Towns/Patrols load readiness contributes to the central barrier.
- Core owns canonical native prefab registration and transactional item registration; caller facades remain for later packages.
- Core theme assets stage under their owner. All 11 Core payload hashes match; all 361 indexed payload names and runtime paths are unique.
- B02 Optimization accepted through `ee96efb`; B03 Debug accepted through `8028483`. Their source/reference/comment gates pass. B03 authored-commit ownership audit: 106 changed paths, zero outside package plus 60 exact approved exceptions. Exceptions affect ownership checks only, not source-gate scope.
- Integrated B01 shared services `94d140f` and summary-only UI assistance `b4eee9c`.
- Vistas runtime hooks/config subscription now follow Compatibility lifetime; real IWorldGenFacts provider added (`b018f8b`, `0eabdc2`). Compatibility must initialize before consumers.

## Current checks

Combined Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` after shared-services/Vistas changes: zero warnings, zero errors. Combined partial scan: 181 declarations, none missing. B02 summaries: zero missing across 51 files; B03: zero across 16. No game/harness/deployment/live configuration or save changes.

## Active agents / next integration

- `b01_kernel`, `WW2-B01`: final Core service consolidation, camera ownership, inventory queries, ReplacementGuard HUD view, independent InventorySupplies bootstrap, summaries/gates/report. Avoid root-reserved SessionIntegrations, SessionMetadata and the exact Plugin/ModuleRegistry session-contribution sites.
- `b02_optimization`, `WW2-save-bootstrap`: Saves lifecycle/bootstrap/provider support, checkpoint `5885f8b`; still depends on B01 removal of old InstallFixes partial and PlayerState.Health. Reviewing cancellation/disposal, native menu fallback when disabled, and integrating B03 metadata consumer. This is support for Core integration, not B04 completion.
- `b03_debug`, `WW2-session-compat`: explicit once-per-session Apocasaver takeover behind a generic Core session registry, generic session metadata service, real Saves consumer coordinated with B02. Required takeover hooks survive ordinary Compatibility toggles; resolve targets before destructive changes and fail visibly on takeover failure.

Merge these completed checkpoints after inspection, resolve only actual integration conflicts, run source gates and one coherent Windows build after final code changes. Record exact additional ownership exceptions. Do not mark B01 or B04 complete before their full package gates/report.

## Remaining plan

B04..B18 full package work remains queued. Dispatch as slots free up according to the guide; complete Wave B before Wave C. Runtime verification stays Denis's planned final gate; no runtime or performance claims follow from a compile.
