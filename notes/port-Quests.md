# Native Quests integration

Publication update: Denis explicitly requested commit, push and release of all pending changes in 0.1.36 RC33. The historical no-release statements below describe the earlier port handover. RC33 builds/deploys with zero warnings/errors; built/installed/staged DLL MD5 `F165942C49354E5A5D89E595776802D4`. Quests settings are now released bindings, with migration as documented below. Gameplay/performance acceptance stays Testing.

## Dialogue speaker colors — 2026-10-18, Testing

Denis requested inline speaker colors: `You:` uses the active UI accent; NPC prefixes such as `Bolter:`/`Merchant:` and the NPC window heading use gray (#A0A0A0), with dialogue body text remaining white. Cause: native chat had no NPC prefixes and CleanHUD drew whole lines white; optional Talk's rich-text speaker colors were stripped before atlas rendering. CleanHUD now adds missing native NPC prefixes and gives only the leading speaker characters a separate mesh color. Existing AccentTint recolors the player prefix on accent changes; no rich-text parser or borrowed Talk-content changes. Changes are confined to four CleanHUD files (DialogueWidgets, GlyphText, QuestDialogue, TalkDialogue), about 35 changed lines. No config entries or save changes.

Test: talk to a Bolter/Merchant, ask for and accept work; player lines show accent `You:`, NPC lines gray `Bolter:`/`Merchant:`, body white. Change accent and reopen: only player prefixes follow it. Check long wrapped replies, quest turn-in, and optional Talk content for matching colors and one speaker prefix. Windows Release/Deploy=true passed with zero warnings/errors; built/installed MD5 **F2A9E42A63F09121B3AA503BD77C7DEA**, including the wheel copy/forward-facing entry follow-up. No game or harness run by the agent. In-game acceptance remains Testing.

Status: Testing, 2026-10-18. Native cutover implemented, compiled and deployed; awaiting Denis's gameplay verification.

## Source and ownership

Apocalypter Quests 1.8.2 supplied source: `V:/Repos/Apocalypter/ApocalypterQuests/Quest Mod`, no Git metadata. Denis states the author granted maintenance rights. Immutable [source manifest](quests-1.8.2-source-manifest.json) aggregate SHA256: `85dc7822106ea5783d7eebb5b5c40f19b36a39392562ca417a17cbe7612e285b`. The port includes the 76 supplied WAV recordings (34,534,926 bytes), credited to LostSilver666 and Soldier96, with original quest/phase filenames.

The previous compatibility layer required a separate Quests DLL. `QuestsModule` now owns the 1.8.2 engine, authored/generated jobs, reputation, factions, takeovers, walkers, delivery recovery, voices and hiring. Quests works without Apocatalker. Talk remains an optional provider of its original stories, flags and dialogue choices. Standalone Quests conflicts with the native module and is handled by WW's existing replacement/restart policy. Apocalypter Quests is removed from suggested mods; Apocatalker was not listed. Existing disabled installs are retained.

## Implementation map

| Files | Responsibility |
|---|---|
| `src/Modules/Quests/QuestsModule.cs`, `Plugin.cs`, `PluginRuntime.cs` | Module lifecycle, named scheduler work, quest engine and guarded save/load sessions; plain engine class, no BaseUnityPlugin or independent global runner |
| `Quests.cs`, `SavedWalk.cs`, `Game.cs`, `Factions.cs`, `Takeover.cs` | Original data/parser/generation/native mechanics; explicit capture/restore of ownership and walkers |
| `QuestWorld.cs`, `QueryCache.cs` | 500 microsecond discovery slices, published people/items/POIs, bounded lookup caches and nonalloc ray buffers |
| `Hire.cs`, `QuestFollower.cs`, `CrewHistory.cs` | Original hire/seat snapshots plus typed NPC, animation and Patrols APIs; current/former crew restrictions; release AI/collisions/poses on shutdown |
| `NativeConversation.cs`, `TalkHook.cs`, `MapLink.cs`, `Voice.cs` | F interaction, fallback conversation, optional Talk content/actions, stable flag colors and original voice decoding |
| `QuestConfig.cs`, `QuestConfigMigration.cs`, `QuestContent.cs`, `Embedded/` | All 86 source settings, missing-entry/file migration and embedded default content/recordings |
| `src/Core/QuestServices.cs`, `OptionalAccess.cs`, Services/SaveBus/ModuleHost/Plugin/ReplacementGuard | Typed quest presentation and follower APIs, Cecil-generated optional accessors, world generation guard, deferred conflict initialization and registration |
| CleanHUD QuestDialogue/QuestTracker/QuestJournal/QuestNotifications; TalkDialogue | Native typed presentation; retained optional Talk presentation; owned-button dispatch while Apocasetter blocks native UGUI |
| Patrols Crews/QuestFollowers.cs, Passenger.cs | Typed seat pose/combat providers, provider-owned fire checks and target/reload handoff |
| QuestsCompat module and Binding | Narrow Talk-only scheduling/modal/lifetime adapter; former Quests reflection patch files removed |

## Config, content and saves

All 86 source defaults/ranges remain represented. Missing WW entries migrate independently from `local.apocalypterquests.cfg`; existing WW values win. Player settings go to module-prefixed settings.cfg sections; tuning goes to tools.cfg and the Quests & hiring tool page. J and Delete migrate to WW hotkeys; the native F interaction is a new hotkey. The source standalone Apocasetter opt-in is retained only as an inert legacy entry. Existing source QuestRange values are not rewritten.

Custom `*.txt`, `*.txt.example` and Voice Actors trees import from active and Apocasetter-disabled Quests folders, only for missing destinations. Embedded defaults/recordings then fill missing files under `plugins/WildWasteland/Quests/`. Custom voice-root overrides remain intact. Original installs/configs/custom files are not deleted.

The ES3 key `ApocalypterQuests`, AQ1 records/escaping/enum values and legacy `config/ApocalypterQuests_saves/<slot>.txt` backup are retained. Disk/default/cache loading and backup-only-if-ES3-unavailable follow the source contract. Unknown/unreadable saves block writes. SaveBus writes synchronously at wait/SaveGame for save-and-quit, then guarded AfterSave reconciliation; callbacks cannot cross module sessions or slots. Runtime off/on snapshots unsaved state only for the same world generation and slot. No second save watcher is installed. Numbered crew history retains the old `QuestsCompat/Saves/<slot>.crew.txt` path.

Navigation always uses native map/compass flags, with quest-ID colors also snapshotted into tracker rows. Delivery recovery requires a ready camp, fresh people scan and 15 seconds of active play near an empty destination; unknown/unloaded/spawning camps do not count as empty. Returns retain `WW:return-empty-delivery` in existing save fields and bypass theft handling; failed recovery uses normal quest failure. No automatic recovery-giver respawn. Current/former crew cannot offer new work; only seated crew are barred from hiring.

## Acceptance for Denis

1. Keep standalone Quests disabled and Talk absent. Start WW, find `[Quests] Native Quests 1.8.2 ready:` then `[Quests] World ready:` in LogOutput.log. Optional Talk absence must be informational only. Quests must be absent from suggested mods.
2. Load an existing quest save. Aim at a living friendly human within 4 m and press F (or rebound key). Ask for work, accept, close, reopen, turn in and inspect J. Check progress, rewards, deadlines, reputation, stable tracker/flag colors and moving return giver. Confirm one action/reward per click.
3. Hire on-foot NPC, follow/fight/reload, enter/exit a car, test passenger aiming/fire and Delete dismissal. Seated crew cannot be hired. Current/former crew cannot offer jobs; existing turn-ins remain available. Switch Quests off/on and check normal NPC movement/input and retained same-world progress.
4. Empty delivery: reach a ready empty camp, wait 15 active-play seconds; objective changes to return goods. Unloaded/spawning camps must not fail it. Cleared camps/missing return recipients use normal failure without theft treatment.
5. Save, save-and-quit, load, autosave, new game and switch between two slots. Confirm active/completed quests, rep, hire, ownership/walkers and crew history stay with their slot. Verify old AQ1 saves and future/unreadable-data protection using copies.
6. Turn Clean HUD off/on: conversation, journal, tracker and notifications remain usable with one visible presentation. Check choice scrolling, Escape, focus loss and module disable restore cursor/input, including with Apocasetter present.
7. Enable Apocatalker separately and restart: one Talk conversation has existing content plus quest/hire choices; scripted quest actions/flags, story profiles and original Talk settings remain intact. Disable the compatibility scheduling switch and confirm quest choices still attach. Repeat a saved quest turn-in with Talk absent.

No game launch, harness or simulation is performed here. ModProfiler baseline/ported comparison and all gameplay acceptance remain pending; sliced discovery does not make voice decoding, spawning or UI rendering preemptible. No performance gain is claimed. No commit, push or release is requested.

## Build/deploy

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with zero errors and zero warnings. Installed DLL: `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland/WildWasteland.dll`. Built and installed MD5 both equal `5A806852F90A8BC3C1D92F6C491C1EB5`. The disabled WW manifest was absent before deployment. No game was launched.

Approximately 8,600 native module C# lines, largely preserved 1.8.2 source, plus the new CleanHUD dialogue/Core services; about 1,300 former compatibility lines removed/replaced. The source manifest and original disabled installs remain intact. All 86 source config entries are now live WW bindings, with a new F talk binding and Quests module switch. Wiki pages/index generated locally (126 pages, 11 modules); no commit/push/release.

The earlier foundation DLL MD5 A3285F5E4F341AEFABD773C8A48DD7F1 does not identify this full port. In-game acceptance and comparative performance measurements remain pending.
