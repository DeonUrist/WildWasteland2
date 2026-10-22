# Native Quests integration preparation

Status: native cutover implemented on 2026-10-18, Testing. See [port-Quests.md](port-Quests.md) for implementation, build/deploy and remaining in-game acceptance. The design below is retained as the agreed contract.

## Agreed behavior

Denis stated that the Quests author granted him maintenance rights and requested a native Wild Wasteland integration. The follow-up clarified the two supported modes:

| Installed content | Result |
|---|---|
| Wild Wasteland, no Apocatalker | Current WW UI plus all Quests functionality, including NPC quest interaction, hiring/dismissal, followers and reputation. Quest-related conversation only; no imported Talk story/chatter. |
| Wild Wasteland plus Apocatalker | The same Quests/hiring functionality plus Apocatalker's existing dialogue content, in the current UI. |

The standalone Apocalypter Quests DLL will cease to be a dependency. Quests must not require Apocatalker to initialize, offer work, turn in quests or hire followers. Apocatalker remains an optional content provider; its absence must not produce a missing-dependency warning. This does not absorb or disable Talk's unrelated content when installed.

## Reviewed source and installation

- Source: `V:/Repos/Apocalypter/ApocalypterQuests/Quest Mod`, version **1.8.2**, ten C# source files. This is an unpacked source folder with no Git repository; do not invent an upstream commit.
- [Source manifest](quests-1.8.2-source-manifest.json) pins source/default data/docs/examples and every supplied voice file by SHA256, and inventories 86 config bindings by source file/line. Its aggregate fingerprint is `85dc7822106ea5783d7eebb5b5c40f19b36a39392562ca417a17cbe7612e285b`.
- The source folder's built DLL matches the installed-but-disabled 1.8.2 DLL byte for byte: SHA256 `0ea98f1088738804cc088076d201a5bf57d277c84d65399c7cb054c0dd265904`. This identifies the supplied build artifact; it does not substitute for building the future port.
- Apocasetter currently stores Quests under `BepInEx/cache/Apocasetter/disabled/local.apocalypterquests/plugins/ApocalypterQuests/`. Its manifest reports 1.8.2. Talk's disabled manifest reports 2.0.0. The latest game log says `[QuestsCompat] Talk/Quests not loaded; compatibility idle`. Preserve these disabled folders and their customized data.
- Supplied voices: **76 files, 34,534,926 bytes**, actors `LostSilver666` and `Soldier96`. Preserve actor credits, quest IDs, phase filenames and custom voice-folder overrides. Do not replace current recordings with the older 1.8.0 inventory.
- The older decompile and [2026-10-15 plan](plan-Talk-Quests.md) describe 1.8.0. Use the available 1.8.2 source as the implementation baseline. This plan supersedes the old proposal to introduce a different primary quest-save store.

## Preserve these contracts

1. Keep all seven QType values and meanings: fetch, kill, goto, talk, clear, takeover, deliver. Retain handwritten chains, generated jobs, deadlines/game-clock behavior, rewards, giver cooldowns, camp ownership/garrisons, voice playback, reputation trading and hiring.
2. Preserve ES3 key **`ApocalypterQuests`** and the **`AQ1`** text serialization unchanged, including completed IDs/count, giver stamps, faction reputation/stamps, ownership overrides, hire identity/expiry/kind, takeover walkers and active quest fields. Unknown/unreadable data remains protected by the existing `saveBlocked` behavior. No primary-format conversion or empty-book overwrite.
3. Preserve the native save-file/cache precedence and backup fallback rules. `Plugin.BackupPath` currently uses `BepInEx/config/ApocalypterQuests_saves/<sanitized-slot>.txt`; the implementation explicitly avoids using backups merely because a readable ES3 slot lacks quests. Keep old fallback files readable. SaveBus.AfterSave retains the current session-generation and slot guards; explicitly cover save-and-quit, where isPlay/AfterSave may not occur.
4. Migrate every original `local.apocalypterquests.cfg` entry once, without clearing shared settings or overriding an existing WW value. Split gameplay controls and developer tuning using ModuleConfig; do not expose all 86 bindings as new gameplay clutter. Preserve quest-log J, follower-dismiss controls, user volume 0..10 and custom folder values.
5. Import `quests.txt`, `factions.txt`, quest-chain text and voices only when the WW destination is missing. Search both the active legacy install and its disabled-cache manifest location; the normal active-folder helper alone cannot find this installation. Keep author/user files intact.
6. Retain WW's delivery recovery, current/former patrol-crew quest restrictions and per-slot crew history, follower animation/steering ownership, reload holds, saved passenger/mount restoration, origin-safe flags, deterministic per-quest colors, journal and message-log notifications.

## Target ownership and file mapping

Create `src/Modules/Quests/` with a ModuleBase entry point, source provenance and explicit teardown. Keep saved component/type names and native object/prefab identities intact. Do not create another BaseUnityPlugin, Harmony instance, SaveLoadGame watcher or independent global update runner.

| Source / current adapter | Port destination or treatment |
|---|---|
| `Plugin.cs` | Quests module lifecycle plus the existing quest engine; bind config separately, map Update/LateUpdate/GUI work onto named scheduler jobs. Replace Info.Location with module data roots. |
| `Quests.cs` | Preserve Quest/QuestBook/QuestFile/QType/Phrases/QuestGen and AQ1 parser/writer behavior. |
| `Factions.cs`, `Takeover.cs`, `Game.cs` | Port mechanics; use existing Core game/origin references and published sliced item/people discovery instead of repeated full scene/FsmList scans. |
| `Hire.cs` | Preserve hire/expiry/dismissal, footsteps/follow/combat, boarding/dismount, magazine/reload and target handoff. Convert WW integrations to typed services; never require absent standalone NPC/Patrol DLLs. |
| `Voice.cs`, `FactionDump.cs` | Module config/data ownership, cache disposal and scheduled diagnostics. Retain 1.8.1 loudness normalization and 1.8.2 data. |
| `MapLink.cs` | Native Services.Map/Compass first; preserve objective/return flags and known giver fallback positions. Immersive Map remains an optional external adapter if retained. |
| `TalkHook.cs` | Split quest conversation operations from the optional Talk adapter. No unconditional Talk lookup during Quests initialization. |
| `QuestsCompat/DeliveryRecovery.cs` | Compare with 1.8.2's built-in DeliveryRecipientCheck/CancelStranded/ReturnGoods/DeliveryStolen. Integrate the intended WW-specific camp checks once; remove duplicate recovery timers/transpilers. |
| `QuestsCompat/QuestFollower.cs`, `CrewHistory.cs`, `QuestsPerf.cs` | Carry behavior/cache improvements into native code. Keep old crew-history files readable. Replace accessors/transpilers against owned Quests methods with direct calls. |
| `QuestsCompat/PluginLifetime.cs`, `Binding.cs` | Remove Quests-DLL bindings. Retain only code actually needed for the optional external Talk model, with independent lifetime/teardown. |
| CleanHUD `QuestTracker`, `QuestJournal`, `QuestNotifications` | Consume a typed Core quest read model/events and actions instead of private fields or patches against `ApocalypterQuests.Plugin`. Preserve pooled rows, scrolling, colors, toast semantics and UI-off behavior. |

### Quest and hiring interaction without Talk

- Extract conversation operations required by OnTalkOpen/BuildButtons/RunTalkAction into an owned model: selected NPC/name, chat lines, stable choices, action callbacks, revision and close. The quest/hire engine uses this contract rather than a particular external plugin/UI.
- Add the no-Talk NPC interaction controller inside Quests. Reuse the sliced people cache and existing faction/alive/hostile/crew checks; honor reach, aimed NPC and interaction priority. Do not intercept vehicle entry, pickup, commerce or another modal. Use a rebindable interaction key and publish a prompt only for an eligible NPC.
- Choices must include quest offer/accept/decline, progress/turn-in/return goods, hiring/dismissal and existing quest-module reputation actions. Generate the text the quest engine needs; do not load Talk's unrelated dialogue database in this mode.
- CleanHUD renders this model with the current panels, selected font, scrolling and notifications. Keep a functional quest-only fallback if CleanHUD is disabled; turning off styling must not disable quests/hiring.
- EditorSession owns pause/cursor/game-input exclusion. Apocasetter 2.1.2 suppresses EventSystem.Update/Button.Press while blocked: owned choices must have working session-local input dispatch, rather than merely re-enabling the EventSystem. Clear pending presses and release ownership on close/load/shutdown/focus changes.

### Additional content with Talk installed

- Preserve Talk's own dialogue/profile/flag rules. Keep its existing user text files and public API behavior; do not copy Talk into Quests or replace its content with quest-only lines.
- A narrow optional adapter feeds the same quest/hire actions into the current Talk conversation. When Talk supplies NPC detection/opening, the no-Talk quest opener stands down for that interaction: one prompt, one window, one click/keypress, one quest offer and one hire transaction.
- Retain the current CleanHUD Talk content rendering and scheduling/modal/lifetime behavior where necessary. Keep that optional adapter independent of quest logic so Quests works without its DLL, and Talk can keep serving other content when the quest module is disabled.
- Retire the Quests-facing compatibility wrapper, not the functionality it currently supplies. If a Talk-only adapter retains the old QuestsCompat config identity during migration, label/document it clearly; do not silently delete the user's enable preference. No required Quests DLL reflection, scheduling prefix or plugin-lifetime patch remains in the finished native module.

## Cutover sequence

1. **Baseline:** use this manifest, map every config key, list user-data/voice migration sources and preserve the reviewed source externally. No active plugin/config changes during preparation.
2. **Native engine:** port the quest model, saving and scheduler lifecycle under Quests; move follower/camp/world-cache behavior into native paths. Add a typed Core quest UI/actions service instead of cross-module class references.
3. **Independent interaction:** implement quest/hire NPC interaction and the current CleanHUD view without Talk installed. Verify quest acceptance/turn-in and hiring before adding optional dialogue content.
4. **Optional Talk:** attach additional Talk content through the small adapter and verify interaction ownership in both modes. Retain shared/default story profiles only when Talk exists.
5. **Replacement:** register Quests in Plugin, remove its `local.apocalypterquests` SoftDependency and QuestsCompat quest branch, and declare Quests.ConflictsWith containing `local.apocalypterquests`. The recommendation-list removal was brought forward into the first implementation build at Denis's request; this does not mark the gameplay cutover complete. The normal installer/restart mechanism prevents double ownership if a standalone copy is active. Do not auto-disable Apocatalker; it still adds content and may serve other mods.
6. **Cleanup/report:** remove obsolete Quests private reflection/transpilers, update docs/modules.md and docs/harmony.md and release assets/attribution, preserve debug visibility/config migration, and leave status Testing. Do not discard source hooks until each has an owned replacement.

## Acceptance checklist for the implementation

- Windows Release/Deploy=true build, zero errors; report copied DLL MD5. No game launch/harness in this preparation. Future gameplay checks follow the repo's current verification rules.
- With both standalone DLLs absent: correct prompts and current UI; quest offer/accept/decline, fetch/kill/goto/talk/clear/takeover/deliver progress, returns and cancellation all work; no dependency or reflection error.
- With Quests absent and Talk present: its additional content and Quests/hire choices coexist in one interaction; flags/actions/profile persistence remain; no duplicate handlers, offers or charges.
- Hire ranged and melee NPCs without Talk: no missing-mod warning with WW NPC/Patrol providers present; follow, dismiss, expiry, combat/reload, boarding/dismount and saved hires retain behavior. Current/former patrol-crew quest restrictions remain, while a dismounted former crew member may be hired.
- Load existing AQ1 saves with active quests, reputation, ownership, walkers and a hire; compare semantics, then save/reload two different slots and test save-and-quit/autosave. Empty and unreadable slots must not import another slot's state or destroy unknown data.
- Test 1.8.2 empty delivery recipients and missing goods together with WW camp readiness; exactly one recovery result/notification and no accidental theft penalty.
- Check map/compass flags and stable colors across origin jumps, giver unload/reload, completion and UI/module toggles; no stale markers or full scene scans.
- Check voice actor/line selection, normalization/volume/custom folders and preserved authored quest/faction files. Ensure existing files win over defaults.
- Disable/re-enable Quests and CleanHUD; close conversations, clear jobs/handlers/markers and restore NPC/vehicle input/ownership. Talk content remains independently usable when installed.
- Profile attributable jobs with idle, active quests, takeover walkers and a hired passenger. No claimed performance improvement before measured results.

## Preparation outcome

Preparation completed; native runtime, optional Talk adapter, typed UI/follower integration and migration are now implemented. Gameplay/performance acceptance remains pending. The source manifest is unchanged; original disabled installs and custom content remain preserved. See the port report for the current build and tests.
