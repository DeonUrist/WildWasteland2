# Saves bootstrap checkpoint

Scope: coordinator-delegated real Saves lifecycle/provider wiring. This is not completion of B04.

- [x] Separate worktree prepared at e3bb88e (`refactor/save-bootstrap`).
- [ ] Inspect existing save runtime, hooks, jobs, configuration and module toggle ordering.
- [ ] Make Saves own binding, startup, shutdown, ISaves and typed event handlers.
- [ ] Decouple only the delegated Core consumers and remove Core save startup calls.
- [ ] Review failure, disable/re-enable, loading and paused-drop behavior.
- [ ] Windows Release build with Deploy=false, relevant static gates, report.

Constraints: current WW2 save formats and keys remain unchanged; no migration, deployment, game or harness execution. Compatibility wiring belongs to coordinator. B04 full framework/UI refactor remains pending.

Current blockers: none. Last good coherent build: coordinator e3bb88e base, zero warnings/errors (reported by coordinator); this branch has not yet built changed code.
