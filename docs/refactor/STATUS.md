# WW2 refactor - coordinator status

Repo: local `V:\Repos\Apocalypter\WildWasteland2` (branch `refactor/main`), continued from the cloud Wave A checkpoint. Plan: `docs/refactor/README.md` (section 0 amendments first), `work-packages.md` (local execution override first).
Imported from WildWasteland HEAD 2e98d0b (WW 1.0.9) on 2026-10-22. Baseline compile: OK (39 warnings).

## How to resume
1. Read this file and `state/pre-B-cleanup.md`, then the state of any package marked in progress. Read the package's brief, relevant inventories and all requests/reports addressed to it before implementation.
2. Active wave: **Wave B**, with package agents running in parallel as Denis clarified. B01, B02 and B03 use isolated worktrees `V:\Repos\Apocalypter\WW2-B01`, `WW2-B02`, `WW2-B03` on `refactor/B01`, `refactor/B02`, `refactor/B03`. Read each worktree's state file and commits to resume; do not recreate/reset them. Keep D15 step commits and each report/request file up to date.
3. Follow B01 ownership and frozen contracts. Its first preparation step is to inventory existing Core/_from* and CorePages contributions and consolidate the W0/A requests (including AtomicFile backup support and Clock's Quests fallback). Do not remove forwarders still used by pending packages or bypass the package's acceptance gates.
4. On this PC use Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` and the applicable `python -B tools/refactor/` lints. Retain full compiler diagnostics; never infer code cleanliness from the old cloud warning count. CS0618 remains temporarily suppressed until Wave C. No WSL/Mono/game launch/deployment.
5. Merge only completed, reviewed package branches into `refactor/main`; after each merge run `check_partials.py` and the Windows build, update this file and record outstanding cross-package requests. Dispatch subsequent Wave B packages as agent slots free up; complete Wave B before Wave C. Historical cloud instructions apply only in cloud workspaces.

## Local pre-B checkpoint (2026-10-22)
- Cleanup complete; see `reports/pre-B-cleanup.md` and `state/pre-B-cleanup.md`. Windows Release/Deploy=false: **0 warnings, 0 errors** with the existing warning settings.
- Partial methods: 215 declarations, 0 missing implementations. Wave A file-map check: 0 open items. Comment check on the three modified config/key helper files: 0 missing summaries.
- Whole-source lint baseline remains **1,078 cross-module reference findings** and **6,563 members without summaries across 723 source files**. These are heuristic findings recorded for Wave B/C, not passing gates or a claim of 1,078 independent bugs. No blanket suppression was added.
- D12 cleanup removed the unused migration helpers and NPC helper arguments; current config entries, asset paths and save formats are unchanged. Wave B/C remain pending.

## Claude takeover (2026-10-22 evening)
Denis stopped Codex; Claude completes Wave B and C (see work-packages.md "Claude takeover"). Merged: B01+B02 (coord-wiring), B03 -> refactor/main 25b90be; Presentation/NPCAI partial entries re-applied. B04 continues on claude/B04 (= main + save-bootstrap WIP; session-compat's 3 extra commits to integrate), B17 on claude/B17 (= main + Codex B17 WIP, merge conflict left for the agent). D19: delete weather diagnostics (B14).
Batches: 1 = B04, B17, B09, B12, B15; 2 = B05, B06, B07, B08; 3 = B10, B11, B13, B14, B16, B18; then Wave C.

## Waves
| wave | package | branch | state | merged | notes |
|---|---|---|---|---|---|
| W0 | W0-A Core kernel | refactor/W0-A | done | 37c5df5 | contracts frozen; requests in requests/W0-A.md |
| W0 | W0-B Core utilities | refactor/W0-B | done | 67f9b61 | requests in requests/W0-B.md |
| A | A1..A8 | refactor/A1..A8 | done | all merged | compile OK (36 warnings); check_partials fixed 1 (EngineSim BindCompatibility, original was empty); questions answered as D18 |
| B | B01 Core-Kernel | refactor/B01 | accepted in integration | 5dd7b94 | Core ownership/lifecycle complete; independent build and source gates pass; reports/B01.md |
| B | B02 Core-Optimization | refactor/B02 | accepted in integration | ee96efb | source gates and combined Windows build passed; reports/B02.md |
| B | B03 Debug | refactor/B03 | accepted in integration | 8028483 | authored ownership audit, source gates and combined Windows build passed; reports/B03.md |
| B | B04 Saves | refactor/save-bootstrap | active | | bootstrap integrated; full framework conversion in progress |
| B | B17 Compatibility | refactor/B17 | active | | session bootstrap reviewed; full bridge conversion in progress |
| B | B05..B16, B18 | | queued | | dispatch as parallel-agent slots free up, following guide dependencies |
| C | C | | not started | | |

## Coordinator decisions
- 2026-10-22: Denis clarified execution: complete waves in order, with multiple package agents working in parallel using the guide. Implement toward final ownership/architecture; do not add temporary scaffolding merely for intermediate tests. Coordinator handles cross-owner wiring requests and asks Denis about uncovered design/behavior decisions. B01/B02/B03 dispatched in parallel; three package slots available alongside coordinator.
- 2026-10-22: Completed the user-requested pre-B redundancy cleanup across module boundaries. Details and remaining package obligations are in `reports/pre-B-cleanup.md`; the local execution override in work-packages.md supersedes historical cloud-only instructions. B01 is next, not started.
- 2026-10-22: Denis authorized local continuation in WW2, starting with redundant navigation fields. Removed Route.GlideAt, Route.GlideTime and Route.Glides after confirming they have no readers or writers in WW1 or WW2. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: 0 warnings, 0 errors. No deployment. This is the latest local build result; the Wave A table retains its historical cloud compiler count. Existing CS0618 suppression remains until Wave C.
- 2026-10-22: WW 1.0.10 MapFog AtomicFile fix ported (e942e98); rule D17 (no File.Replace / File.Move over existing files) added to README.
- 2026-10-22: CS0618 suppressed in csproj until Wave C (W0-A request).
- 2026-10-22: W0-B questions: KeyMap.FromKeyCodeName was scheduled for deletion if unused (D12); removed in pre-B cleanup after confirming no callers. Broader WAV decoder accepted.

## Device mirror
Originally a cloud mirror; Denis authorized local continuation on 2026-10-22. Preserve local commits and changes when synchronizing with cloud work; do not reset this checkout to an older cloud checkpoint. Local continuation started from 473fef9 (Wave A merged). Active Wave B branches are listed above; agent work remains isolated until reviewed integration.

## Open questions for Denis
(none)

- 2026-10-22 resume: Coordinator accepted B02 and B03 in `refactor/coord-wiring`; Windows Release/Deploy=false after shared-services and Vistas integration passed with zero warnings/errors. B02/B03 references and summaries pass; combined partial check: 181 declarations, none missing. B03 authored-commit audit: 106 changed paths, none outside base ownership plus 60 exact approved exceptions recorded centrally. B01 consolidation and the Saves bootstrap integration remain active. No deployment or runtime verification.

- 2026-10-22 interruption recovery: pending B01 merge conflict resolved as `293efaa`, preserving canonical Core fix installs and generic session takeover while removing old direct Saves/Vistas bootstrap. Combined Windows Release/Deploy=false: zero warnings/errors; Core dependency findings zero; 178 partial declarations, none missing. Agents resumed for B01 completion, full B04 and full B17. Session bootstrap review recorded separately; no game/deploy/runtime claim.

- 2026-10-22: B01 accepted at5dd7b94 after independent coherent Windows Release/Deploy=false build0warnings/errors, strict Core refs0/comments0in102files (including reviewed cave protocol), partial175/0missing, 11Core asset hashes/361unique index rows verified. Main stays a tracker while full B04/B17 conversions are active; their partial checkpoints are reviewed in coordinator integration, not reported as completed packages.
