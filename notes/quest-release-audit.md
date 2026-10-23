# Quest release audit - 2026-10-23 (Testing)

Reviewed GitHub release bodies v0.1.0 through v1.0.10, quest-related tagged compatibility patches (v0.1.9/v0.1.32), and current native owners. This is a source audit; Denis performs gameplay acceptance.

| Release | Current result |
|---|---|
| [RC4/RC5](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.8): follower TryMount, combat, dialogue, notifications | Present through native Hire, NPCAI QuestFollower, Patrols QuestFollowers and HUD quest views. |
| [RC6](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.9): require a live talkable delivery recipient | Superseded by RC7. The agent's attempted restoration was a mistake and has been reverted: unloaded/empty destinations remain eligible. The actual fix resolves faction from the owning camp registry or town plan, never child names. Enabled towns are delivery/discovery/check-up targets; their building clones are excluded as separate camps. |
| [RC7](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.10): return goods, deadlines, death, readiness, moving giver, no respawn | Present. Enabled-collider recipient check was omitted; restored, with seated actors also excluded like native interaction. Recovery remains for a recipient lost after selection. |
| [RC8](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.11): stable matching flag colors, tracker clearance | Present in MapLink.QuestColor, PluginRuntime rows and HUD QuestTracker. |
| [RC9](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.12): current/former crew restrictions | Present, including acceptance/hiring/bailout persistence. Early RunTalkAction guard incorrectly compared `give` instead of `quest.give`; corrected. AcceptQuest already prevented the actual grant. |
| [RC14](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.17): journal | Present: native key/unpaused lifecycle, full text/completed count, scrolling and presentation fallback. |
| [RC19](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.22): scheduled work/caches | Native named jobs and shared WorldIndex/FsmIndex, cached FSM/object queries and nonalloc raycasts replace external patch machinery. No performance claim. |
| [RC23/RC33/RC34](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.36): native cutover, optional Talk, ownership | Present; external version gate/duplicate QuestsCompat hooks retired. Legacy migration/sidecars intentionally superseded by WW2 D12/D20 (docs/decisions.md); not restored. |
| [RC36](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.39): mouse-look guard, silent incomplete voices | Present in PluginRuntime and Voice.PlaybackEnabled. |

No other missing quest behavior confirmed in this release/source review. Existing accepted destinations are retained. Town delivery recovery waits for the town to be built, then retains the existing 15-active-second/fresh-scan return flow. No new live-recipient offer restriction or skipped-recipient trace remains.
