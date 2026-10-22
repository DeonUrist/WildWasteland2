# WW2 refactor - coordinator status

Repo: local `V:\Repos\Apocalypter\WildWasteland2` (branch `refactor/main`), continued from the cloud Wave A checkpoint. Plan: `docs/refactor/README.md` (section 0 amendments first), `work-packages.md` (local execution override first).
Imported from WildWasteland HEAD 2e98d0b (WW 1.0.9) on 2026-10-22. Baseline compile: OK (39 warnings).

## How to resume
1. Read this file and `state/pre-B-cleanup.md`, then the state of any package marked in progress. Read the package's brief, relevant inventories and all requests/reports addressed to it before implementation.
2. Next package: **B01 Core-Kernel**. No Wave B branches/worktrees exist at this checkpoint. From this repo create an isolated checkout: `git worktree add ..\WW2-B01 -b refactor/B01 refactor/main`. Create `state/B01.md` before editing code; keep D15 step commits and the B01 report/request files up to date.
3. Follow B01 ownership and frozen contracts. Its first preparation step is to inventory existing Core/_from* and CorePages contributions and consolidate the W0/A requests (including AtomicFile backup support and Clock's Quests fallback). Do not remove forwarders still used by pending packages or bypass the package's acceptance gates.
4. On this PC use Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` and the applicable `python -B tools/refactor/` lints. Retain full compiler diagnostics; never infer code cleanliness from the old cloud warning count. CS0618 remains temporarily suppressed until Wave C. No WSL/Mono/game launch/deployment.
5. Merge only completed, reviewed package branches into `refactor/main`; after each merge run `check_partials.py` and the Windows build, update this file and record outstanding cross-package requests. Historical cloud instructions apply only in cloud workspaces.

## Local pre-B checkpoint (2026-10-22)
- Cleanup complete; see `reports/pre-B-cleanup.md` and `state/pre-B-cleanup.md`. Windows Release/Deploy=false: **0 warnings, 0 errors** with the existing warning settings.
- Partial methods: 215 declarations, 0 missing implementations. Wave A file-map check: 0 open items. Comment check on the three modified config/key helper files: 0 missing summaries.
- Whole-source lint baseline remains **1,078 cross-module reference findings** and **6,563 members without summaries across 723 source files**. These are heuristic findings recorded for Wave B/C, not passing gates or a claim of 1,078 independent bugs. No blanket suppression was added.
- D12 cleanup removed the unused migration helpers and NPC helper arguments; current config entries, asset paths and save formats are unchanged. Wave B/C remain pending.

## Waves
| wave | package | branch | state | merged | notes |
|---|---|---|---|---|---|
| W0 | W0-A Core kernel | refactor/W0-A | done | 37c5df5 | contracts frozen; requests in requests/W0-A.md |
| W0 | W0-B Core utilities | refactor/W0-B | done | 67f9b61 | requests in requests/W0-B.md |
| A | A1..A8 | refactor/A1..A8 | done | all merged | compile OK (36 warnings); check_partials fixed 1 (EngineSim BindCompatibility, original was empty); questions answered as D18 |
| B | B01..B18 | | not started | | |
| C | C | | not started | | |

## Coordinator decisions
- 2026-10-22: Completed the user-requested pre-B redundancy cleanup across module boundaries. Details and remaining package obligations are in `reports/pre-B-cleanup.md`; the local execution override in work-packages.md supersedes historical cloud-only instructions. B01 is next, not started.
- 2026-10-22: Denis authorized local continuation in WW2, starting with redundant navigation fields. Removed Route.GlideAt, Route.GlideTime and Route.Glides after confirming they have no readers or writers in WW1 or WW2. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: 0 warnings, 0 errors. No deployment. This is the latest local build result; the Wave A table retains its historical cloud compiler count. Existing CS0618 suppression remains until Wave C.
- 2026-10-22: WW 1.0.10 MapFog AtomicFile fix ported (e942e98); rule D17 (no File.Replace / File.Move over existing files) added to README.
- 2026-10-22: CS0618 suppressed in csproj until Wave C (W0-A request).
- 2026-10-22: W0-B questions: KeyMap.FromKeyCodeName was scheduled for deletion if unused (D12); removed in pre-B cleanup after confirming no callers. Broader WAV decoder accepted.

## Device mirror
Originally a cloud mirror; Denis authorized local continuation on 2026-10-22. Preserve local commits and changes when synchronizing with cloud work; do not reset this checkout to an older cloud checkpoint. Local continuation started from 473fef9 (Wave A merged). Wave B remains not started.

## Open questions for Denis
(none)
