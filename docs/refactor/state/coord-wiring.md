# Wave B coordinator wiring (2026-10-22)

Branch refactor/coord-wiring, worktree V:\Repos\Apocalypter\WW2-wiring. Scoped support for B01/B02/B03; no package completion claim.

## Steps
- [ ] Move LOD export to Tools ownership, authoring page/lifecycle and D13 paths; update offline generator paths consistently.
- [ ] Move SigmaMod guard to Compatibility ownership, detect once at initialization, preserve state-owner initialization and order before Core optimization startup.
- [ ] Register typed patrol/vehicle/Engine Sim ownership and patrol culling protection; move gameplay-specific retention out of Core.
- [ ] Convert Towns/Patrols final-load checks into the shared Core load barrier without changing readiness conditions.
- [ ] Integrate with B01/B02 final APIs; run appropriate comment/reference/partial checks and Windows Release build before acceptance.
- [ ] Report/commit reviewed wiring and hand off to B16/B17.

## Last good compile
Base 66df101 shares ee95de2's Windows Release 0 warnings/0 errors. New wiring must be verified against B01/B02 APIs before integration.

## Next action
Implement the two agreed ownership moves; coordinate Core APIs/order with B01/B02. No game/harness/deployment.

## Blockers / questions
None on intended ownership: README4.3/6.10/6.12 and assets/A1.tsv prescribe these moves. Pending B01/B02 API integration is an implementation dependency, not a user decision.
