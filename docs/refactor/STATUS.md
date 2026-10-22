# WW2 refactor - coordinator status

Repo: cloud `/home/claude/ww2` (branch `refactor/main`) mirrored to `V:\Repos\Apocalypter\WildWasteland2`. Plan: `docs/refactor/README.md` (section 0 amendments first), `work-packages.md`.
Imported from WildWasteland HEAD 2e98d0b (WW 1.0.9) on 2026-10-22. Baseline compile: OK (39 warnings).

## How to resume
1. Read this file, then `docs/refactor/state/<package>.md` of any package marked in progress.
2. Worktree per package: `git -C /home/claude/ww2 worktree add /home/claude/wt/<package> -b refactor/<package> refactor/main` (or reuse the existing branch: `worktree add /home/claude/wt/<package> refactor/<package>`).
3. Start the agent with its brief from work-packages.md + "resume from docs/refactor/state/<package>.md".
4. Merge finished branches into refactor/main (`git merge --no-ff refactor/<package>`), run `sh tools/refactor/compile.sh`, update this file.
Reference DLLs for compile.sh: copy `Apocalypter_Data/Managed` and `BepInEx/core` from the game folder to `/home/claude/refs` (cloud session), or set MANAGED/BEPCORE.

## Waves
| wave | package | branch | state | merged | notes |
|---|---|---|---|---|---|
| W0 | W0-A Core kernel | refactor/W0-A | done | 37c5df5 | contracts frozen; requests in requests/W0-A.md |
| W0 | W0-B Core utilities | refactor/W0-B | done | 67f9b61 | requests in requests/W0-B.md |
| A | A1..A8 | refactor/A1..A8 | done | all merged | compile OK (36 warnings); check_partials fixed 1 (EngineSim BindCompatibility, original was empty); questions answered as D18 |
| B | B01..B18 | | not started | | |
| C | C | | not started | | |

## Coordinator decisions
- 2026-10-22: Denis authorized local continuation in WW2, starting with redundant navigation fields. Removed Route.GlideAt, Route.GlideTime and Route.Glides after confirming they have no readers or writers in WW1 or WW2. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: 0 warnings, 0 errors. No deployment. This is the latest local build result; the Wave A table retains its historical cloud compiler count. Existing CS0618 suppression remains until Wave C.
- 2026-10-22: WW 1.0.10 MapFog AtomicFile fix ported (e942e98); rule D17 (no File.Replace / File.Move over existing files) added to README.
- 2026-10-22: CS0618 suppressed in csproj until Wave C (W0-A request).
- 2026-10-22: W0-B questions: KeyMap.FromKeyCodeName deleted in Wave C if unused (D12); broader WAV decoder accepted.

## Device mirror
Originally a cloud mirror; Denis authorized local continuation on 2026-10-22. Preserve local commits and changes when synchronizing with cloud work; do not reset this checkout to an older cloud checkpoint. Local continuation started from 473fef9 (Wave A merged). Wave B remains not started.

## Open questions for Denis
(none)
