# Pre-Wave B cleanup (local coordinator, 2026-10-22)

Base: `577046c` on `refactor/main`. Denis requested cleanup of the reported redundant members and preparation to follow the existing plan. This is a coordinator cleanup across package boundaries, not completion of any Wave B package.

## Steps
- [x] Recheck the reported symbols and the D12 deletion decisions; exclude future ModAPI contracts and runtime callbacks.
- [x] Remove the ten unused members and NPC settings helpers' obsolete argument pairs; also remove WhyName(), whose only caller was the deleted CellText().
- [x] Remove unreferenced D12 migration helpers, retaining live compatibility forwarders.
- [x] Update local execution/handoff instructions and affected package requests.
- [x] Run partial-method/file-map checks, review the diff and build on Windows with deployment disabled.
- [x] Record results and commit the completed cleanup (this checkpoint; see git log).

## Last good compile (commit)
This cleanup checkpoint: Windows Release, Deploy=false, 0 warnings / 0 errors with the existing CS0618 suppression. Partial methods 215/215 implemented; Wave A file-map check 0 open; modified helper/config comment check 0 missing. Full baseline findings and local diagnostic paths are in reports/pre-B-cleanup.md.

## Next action
Cleanup complete. Start B01 Core-Kernel from refactor/main in a separate branch/worktree; create state/B01.md and inventory its W0/A requests before implementation. Follow STATUS.md and work-packages.md's local execution override. No Wave B package has started at this checkpoint.

## Blockers / questions for Denis
None for this cleanup. Wave B implementation remains pending; contracts are frozen.

## Notes for a resuming agent
- Local Windows continuation is authorized; do not overwrite this checkout with the historical cloud mirror.
- Windows PowerShell + dotnet only; no WSL/Mono or game launch. Do not deploy unless Denis requests it.
- Preserve current config keys/defaults/ranges/order, save formats and asset paths.
- The broader Wave B unification and D13 asset moves are outside this cleanup.
