# Saves bootstrap checkpoint

Scope: coordinator-delegated real Saves lifecycle/provider wiring. This is not completion of B04.

- [x] Separate worktree prepared at e3bb88e (`refactor/save-bootstrap`).
- [x] Inspect existing save runtime, hooks, jobs, configuration and module toggle ordering.
- [x] Make Saves own binding, startup, shutdown, ISaves and typed event handlers.
- [x] Decouple only the delegated Core consumers and remove Core save startup calls.
- [ ] Review failure, disable/re-enable, loading and paused-drop behavior.
- [ ] Windows Release build with Deploy=false, relevant static gates, report.

Constraints: current WW2 save formats and keys remain unchanged; no migration, deployment, game or harness execution. Compatibility wiring belongs to coordinator. B04 full framework/UI refactor remains pending.

Current blockers: none. Last good coherent build: coordinator e3bb88e base, zero warnings/errors (reported by coordinator); this branch has not yet built changed code.

Implementation checkpoint: owner-scoped framework hooks/jobs, symmetric event/config subscriptions, tracked iterator disposal, active save/load toggle rejection, ISaves state and normal warning-path autosave requests implemented. Two temporary HUD GUI registrations are owner-bound by coordinator exception. Core native InventorySupplies/VehicleLoadPlacement bootstrap and canonical PlayerState.Health are B01 dependencies.

First changed build: blocked at expected dependency boundary CS0759 SaveRuntime.InstallFixes (B01 removing old Core partial); no success claim yet. Initial --no-restore invocation required normal restore; subsequent normal Windows Release build restored project successfully.
