# Save blocking and safeguards — 2026-10-24

Research against `3bc86b0` (released WW2 2.0.5). No affected-player log or screenshot was available. This is a source audit, not a diagnosis of that player's report. No production code, save files, settings or game installation were changed, and no game, harness or simulation was run.

## Findings that matter for the reported gray button

`CREATE NEW SAVE` requires `CanSave`, a nonblank trimmed save name and a nonblank trimmed world name. The save name deliberately starts empty on a new world generation; the world name is normally supplied automatically. See `src/Modules/Saves/UI/SaveLibraryPage.cs:36`, `:150`, `:159`.

The page does not explain missing names, missing world references or pending requests. Its footer shows `RecoveryError`, a generic reload-required message, or a library/preview error. It does not show `SaveSystem.Status`. In particular, a location-history error can require a reload **before the first manual save**, while the footer incorrectly says "The last save failed." See `SaveLibraryPage.cs:109`, `Framework/WorldHistory.cs:75`, `:218` under `src/Modules/Saves/`.

Most active-operation gates also close/prevent the entire tools window: `Core/Scheduling/LoadingProgress.cs:35` combines visible loading, native save/load activity and the Saves provider's broader Busy state; `Core/UI/ToolWindow.cs:51` closes an already open window. Consequently, a gray button that remains visible in an otherwise usable library is more directly explained by blank names, `ReloadRequired`, unavailable recovery readiness, or unavailable world references/state. A busy/requested flag should normally hide/block the menu as well. This is an inference from the guards, not proof about the affected PC.

## Exact gates and how they clear

| Gate | Effect and lifecycle | Evidence |
|---|---|---|
| Empty/whitespace save or world name | Disables Create. Enter text. Opening a new world resets the save-name field. | `UI/SaveLibraryPage.cs:36`, `:161` |
| Missing Player or SaveLoadGame, title scene, or SaveBus state other than `isPlay` | `LocalReady` is false. Wait for the actual world/load to complete; a broken native load must be diagnosed. ESC pause itself is allowed. | `Framework/SaveSystem.cs:53`, `:55`; native `Sources/fsm/scene1_game/SaveLoadGame.md:54`, `:91` |
| Pending save request or active transaction | Disables Create and normally blocks the entire menu. Success or tracked coroutine cleanup should clear it. | `UI/SaveLibraryPage.cs:150`; `Framework/SaveSystem.cs:340`, `:386`; `SavesModule.Runtime.cs:8` |
| Recovery not ready / recovery error | `Ready` becomes true only after journal recovery succeeds. Any unresolved journal in the backup tree can stop recovery for all worlds. Correct the actual reported problem and retry recovery; retain the journal and backup bytes. | `Framework/SaveSystem.cs:142`; `Framework/SaveFiles.cs:175` |
| Reload required after a started save fails | Intentionally blocks new writes because live state may be ahead of restored disk state. Successful rollback does not prove live module state was restored. Load an intact snapshot/new world through the normal flow. | `Framework/SaveSystem.cs:386`, `:161` |
| Reload required after location-history capture/restore fails | Can happen during terrain pooling without any manual save attempt. Prevents saving incomplete streamed-world history. Original cause is recorded in Status/log. | `Framework/WorldHistory.cs:69`, `:89`, `:201` |
| Post-commit cleanup failure | The save may already be published, but the current session requires reload. The generic "last save failed" footer is inaccurate for this case too. | `Framework/SaveSystem.cs:343` |
| Saves unavailable/disabled or partially initialized | Request rejects when not Installed; shutdown sets Ready false. Mandatory save/terrain/history hooks must initialize successfully. Fix the initialization failure, not the admission guard. | `Framework/SaveSystem.cs:102`, `:119`, `:251`; `SavesModule.cs:34` |
| Confirmation open | The background page is disabled until its confirmation is resolved. This is UI ownership, not a persistence failure. | `UI/SaveLibraryPage.cs:100`, `:219` |

Paths in the table are relative to `src/Modules/Saves/` unless explicitly qualified.

## Button enabled does not guarantee a request is accepted

The library, `SaveSystem.CanSaveNow`, `SaveSystem.Request` and `SinglePlayerSaving.Request` use different predicates. For example, the library omits health, exit-flow and serialization checks; the lower adapter also checks its own pending flag and Loading. This can produce a clickable button followed by refusal. See `UI/SaveLibraryPage.cs:150`, `Framework/SaveSystem.cs:140`, `:249`, `Framework/SinglePlayerSaving.cs:61`.

Save/world names containing control characters or `<`/`>` are rejected at request time. The button only checks trimmed nonemptiness. World-name validation returns false without a notice. The library closes before calling Request and discards its bool result, so a rejected request loses the editable screen. Some other refusal paths also return false silently. See `UI/SaveLibraryPage.cs:233`, `Framework/SaveSystem.cs:251-262`.

Combat, driving and the autosave enable switch are **not manual-save admission gates**. Combat and, by default, driving defer scheduled autosaves. Ordinary ESC pause is not checked by LocalReady. Screenshots are optional and capture exceptions are caught, so a thumbnail failure alone should not permanently disable saving. See `Framework/SaveSystem.cs:79-84`, `:267`, `:298`, `:487`.

## Failures after admission

- Native hand-held object release has an eight-second admission/drop deadline and an eight-second settling bound. A busy/unreleased item cancels before serialization. `Framework/SaveHeldItem.cs:45`.
- Terrain work must be quiet twice and has a 120-second timeout. Its barrier is released by the save iterator's finally. A timeout before transaction Begin should permit a later retry. `Framework/SaveTerrain.cs:35`; `Framework/SinglePlayerSaving.cs:95`, `:121`.
- Native serialization has a separate 120-second timeout. Completion requires observing native SaveGame, then `isPlay`, registry `off` and no pending AfterSave callbacks. A failed transaction rolls back and requires reload. `Framework/SinglePlayerSaving.cs:105-126`.
- A participant can reject publication: unreadable/incomplete NPC, patrol, quest, perk, magazine, exploration, town or other embedded state. Examples include unfinished merchant rerolls and deferred patrol vehicles with Vehicles disabled. These usually fail an attempted save and can explain a *later* disabled button; they are not direct initial UI gates. `SavesModule.cs:68`; `Perks/MerchantPerks.cs:176-178`, `Patrols/Persistence/Persistence.cs:310-324`, and other `SaveBus.ReportFailure` callers under `src/Modules/`.
- File access, backup verification, cache flush, missing required records, transaction-stamp mismatch and publication/readback failures abort publication. Actual permissions, locks, disk space or third-party interference require the affected exception; none is established here. `Framework/SaveFiles.cs:73-120`, `:136`; `Framework/SaveSystem.cs:310-396`.
- Yield-based timeouts cannot interrupt a main-thread serializer or filesystem call that never returns. A diagnostic stall timer must not claim that it can safely unlock such a save.

## Code paths worth hardening

1. **Explain admission consistently.** Add one Saves-owned, side-effect-free admission result with a reason and use it in the library, manual request and provider. Keep form validation in the same decision path. Cover not installed, recovery/readiness, reload required, missing world/FSM state, active operation, death, names and destination. Distinguish a temporary wait from a recovery/reload requirement.
2. **Preserve the actual failure.** Show the first blocking cause and actionable instruction on the save page. Separate failure cause from the mutable progress/notification Status string. A history failure should name history; post-commit cleanup should say the save completed but reload is needed. Do not overwrite either with "last save failed."
3. **Retain the form on refusal.** If admission changes after the page closes, retain both names and reopen/report the rejected request through the existing page lifecycle. Never queue a second request while the first owns the world.
4. **Make start failure release pending flags.** Both request layers set pending flags before `SaveRoutines.Start`. If coroutine launch fails before the save iterator enters its try/finally (e.g. no Core runner), disposing an unstarted iterator does not execute that finally; the callers have no catch to undo those pending flags. Harden that specific failure path in Saves. This is a conditional code risk, not a reproduced cause on the other PC. Evidence: `Framework/SaveSystem.cs:261`, `Framework/SinglePlayerSaving.cs:64`, `Framework/SaveRoutines.cs:23`, `src/Core/Scheduling/Scheduler.cs:157`.
5. **Make abort cleanup exception-safe.** Abort calls `SaveCleanup.Rollback` before disk rollback, the reload latch and pending-flag cleanup, without isolating an exception from that call. A secondary cleanup failure could prevent later recovery/flag cleanup. Each restoration needs its own protected attempt and final state cleanup; retain both the original failure and recovery failures. Evidence: `Framework/SaveSystem.cs:388-396`, `Framework/SaveCleanup.cs:184`.
6. **Add bounded diagnostics, not forced unlocking.** Log a snapshot on opening a blocked save page and when its reason changes: version, reason, native state, generation, Installed/Ready, pending/transaction/serialization/loading/exit flags, active-file validity and preserved failure. Track phase age without per-frame log spam. Startup's "saves ready" line currently runs before RecoverFiles; report actual recovery completion separately. Existing terrain/serialization timeouts should stay intact.

The first implementation round should address the visible admission reason and diagnostic snapshot entirely inside Saves. That makes the affected player's next reproduction decisive while preserving the current guards. Treat startup/abort hardening as separate reviewable changes; do not stack them as speculative fixes for the report.

## Protections to retain

The framework already creates flushed, hashed undo backups before mutation; atomically publishes staged files; verifies native required records, embedded key readback and a per-transaction stamp; separates manual/autosave snapshots; and retains unresolved/deleted-save backups. `Framework/SaveFiles.cs:73-151`, `:211`; `Framework/SaveSystem.cs:310-383`; `src/Core/Persistence/SaveKeys.cs:60`.

Do not clear Ready/RecoveryError/ReloadRequired/Busy/request flags on an arbitrary timer, force the native FSM to `isPlay` to enable the button, discard corrupt records as empty state, or delete recovery journals to silence a block. Cancellation must belong to the active save flow and preserve its rollback result. A diagnostic export or manual recovery must copy original files first and keep the native save format.

## Evidence needed from the affected computer

1. Exact WW version and a screenshot of the full SAVE GAME page with both name fields and footer visible; say whether the rest of the menu responds.
2. Whether saving was ever attempted in this session, whether an autosave failed, and whether the problem starts after loading, travelling/terrain streaming or a merchant interaction.
3. In current WW2, enable Debug and set **Debug → Logging → Logging level = Main actions (1)** before reproducing. Deep traces (2) are only needed for the terrain barrier trace. Earlier advice using the old VerboseLog switch name was outdated for this release. Current setting source: `src/Modules/Debug/DebugModule.cs:41`, `:89`.
4. Start/load the affected world, wait for loading to finish, open SAVE GAME, fill both fields and record the disabled state. Keep the full `BepInEx/LogOutput.log`, starting from startup, before another launch overwrites it. Relevant existing messages include `SaveLoadGame: ... -> ...`, `Save recovery needs attention:`, `Location history unavailable`, `Location history could not be preserved`, `Save failed:`, `Save committed, but cleanup could not finish`, and `SaveBus.StateChanged handler`.

There is currently no complete admission-state log line, so absence of a save error is insufficient to prove the button is blocked only by its names. Do not use this machine's unrelated log as evidence for the other installation.
