# Shared integrations handoff

This is the next phase after the remaining Wave B owned packages. Denis authorized proceeding in one session without subagents. Source/package acceptance is separate from full-port and in-game acceptance.

1. Consolidate the distributed player/NPC character driver and seated crew/perception/follower behavior. Existing Crew, PassengerGuard, RiderGuard, QuestFollower, Hire and Takeover partials still cross physical owners and share private state. Preserve actual movement, seats, attacks/reloads, actor identity, expiry and teardown; close these with real typed providers and coherent lifecycle ownership. See reports/B11.md and state/B18.md.
2. Consolidate remaining Core/Compatibility legacy module-entry adapters, especially HUD theme/settings/menu/session hooks and TalkBridge conversation state. Verify provider withdrawal, restart, scene/load/save behavior and hook/job ownership before deleting the adapters. See state/B10.md and the prior B01/B17 requests.
3. Retire obsolete Services/event forwarders and old PlayerBody/NPC/Guns entry points only after all actual consumers have converted. Review reverse partial ownership explicitly: the zero-finding owner-folder scans intentionally do not certify shared private partials.
4. Complete Wave C global namespace/ownership/file-map checks, resource and release packaging, build/tooling updates, current module documentation and removal of the temporary CS0618 suppression. D12/D20 override old migration/sidecar requirements in historical plan text.
5. Merge all work before Denis performs the combined in-game acceptance from package reports. No intermediate game/harness/deployment is authorized by this handoff.

Preserve D20 throughout: all persistent world records, descriptions and thumbnails stay in the selected ES3 save under its verified transaction. External authored files, settings, derived geometry and recovery journals are separate. Do not reintroduce old sidecars from historical branches.

## Current implementation checkpoint

- Guns shell removed from registry and source. Actual magazine save/load/cache hooks now belong to Gunplay, with save implementation physically in Gunplay.
- Presentation consumes NpcSpawned for female appearance restoration; NPCAI no longer calls the foreign presentation implementation during registration.
- PassengerGuard combat, attachment and visual aim handling consolidated into NPCAI. Patrols publishes actual traffic combat policy through IOccupantCombatPolicy; callers use IOccupantCombat. Driver-only FSM cache stays with Patrols while NPC mutation stays with NPCAI. Follower combat adapter is wholly Patrols-owned and delegates actual combat.
- Follower seat adapter is wholly Patrols-owned and calls actual IFollowerSeatPresentation; pose/profile buffers remain Presentation-owned. Removed unused follower ResolveType reflection mapping.
- Windows Release/Deploy=false build passes with zero warnings/errors. These are completed source slices; the remaining shared driver, other distributed partials, legacy entries and Wave C work are still active. No gameplay/harness/deployment.

- External quest conversation composition completed: Compatibility owns reflection/hooks/buttons/flags and publishes IExternalQuestConversation; native quest session remains Quests-owned. Typed callbacks replace reflection into the Quests plugin. External provider replacement invalidates queued choices and releases exact-owner callbacks.
- Empty NPC shell removed from registry and its distributed empty entry partials deleted. Existing settings consumers now use the actual settings authority; reverse implementation dependencies remain to close with the remaining runtime partial consolidation.
