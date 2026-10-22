# Saves bootstrap checkpoint

Scope: coordinator-delegated real Saves lifecycle/provider wiring. This is not completion of B04.

- [x] Separate worktree prepared at e3bb88e (`refactor/save-bootstrap`).
- [x] Inspect existing save runtime, hooks, jobs, configuration and module toggle ordering.
- [x] Make Saves own binding, startup, shutdown, ISaves and typed event handlers.
- [x] Decouple delegated Core consumers and remove Core save startup calls.
- [x] Review failure, disable/re-enable, loading and paused-drop behavior.
- [x] Windows Release build with Deploy=false, partial consistency and report.

Last good coherent build: `293efaa`, Windows Release/Deploy=false, zero warnings and errors. Partials: 178 declarations, zero missing implementations. See reports/save-bootstrap.md. No runtime verification claim.

Next action: full B04 in this branch, following state/B04.md; coordinator reviews cross-owner UI/persistence movement and generic UI command registration before implementation.

Blockers: none for completed bootstrap. Full B04 cross-owner coordination in progress.
