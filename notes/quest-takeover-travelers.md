# Takeover traveler settlement - 2026-10-23, Testing

Denis approved changes across Quests, Patrols and POI and removal of the legacy implementation. Core contracts, save keys, native names and prefab IDs are unchanged. Verification is the required Windows Release/deploy build; Denis performs gameplay acceptance.

## Behavior

- Completing a native-camp takeover publishes the existing `CampOwnershipChanged` event for the exact camp identity and changes the owner to Coyotes. POI persists ownership, resets the previous cleared/resident observation, suppresses the original human spawners and retains ownership through discovery refresh and save/load.
- Patrols creates one saved reinforcement foot journey. It waits for a known, uncleared Coyote camp other than the destination, selects the nearest by horizontal centre distance and asks the existing road planner for the route. It uses the existing 2-5-member Coyote traveler pool, speed, NPC AI Travel orders, formation, combat and virtual/physical transitions. No source NPC has to be loaded.
- The normal moving map marker is visible once the route is ready. Source discovery or routing does not fabricate a nearby group or use the old travel-time teleport. With no known source, the request waits and logs why.
- An arrival while unloaded is retained in the traffic save, with no travelling icon. When the destination loads and the player approaches, surviving members materialize through the normal foot-patrol spawn queue. All surviving members must materialize before settlement.
- Arrival uses the existing resident handoff: remove travelling actor tags, register native saved NPCs, bind them to the destination camp and send them inside using NPC AI. The journey is then removed. Residents remain ordinary camp inhabitants; deaths do not trigger the removed automatic garrison replacement.
- Patrols/world traffic and NPC AI must be running for travel and materialization. Virtual queued requests survive switching world traffic off/on; an already physical group follows the existing toggle-off rule and is released into ordinary native NPCs.

## Removed legacy behavior

Removed Quests' private walker controller, its raycast-steered travel, stuck-hop/timeout teleporting, local wandering and proximity-based automatic garrison spawning. Deleted `World/Takeover.Camps.cs`. `Takeover.Walkers.cs` retains shared giver-prefab/follower obstacle helpers and a one-time handoff for old saved `W` records; it never clones old reinforcement groups. Hired-follower movement is unchanged.

Seven obsolete Quests takeover controls are no longer bound: `WalkerSpawnDistance`, `GroupSize`, `WalkSeconds`, `GarrisonSize`, `GarrisonRange`, `GarrisonCooldownGameHours`, `PatrolAtCamp`. Existing config text is not deleted. The traveler settings apply instead. Giver recovery and the quest's target/radius/reward settings remain.

POI's existing world-location record gains an optional `captured` flag; old records lacking it load normally. The existing traffic `reinforcement` and foot-member fields retain requests, routes, identities, health and deaths. Existing quest ownership records reconcile without requesting duplicate groups. Old numeric save keys and AQ1 `W` parsing remain readable.

## Denis's test

1. Enable Patrols/world traffic, NPC AI, map traffic markers and quests. Accept a takeover of a Scrapyard native camp; finish the defenders. Expect immediate Coyotes ownership and one queued traveler group.
2. Check the logged source is the nearest known uncleared Coyote camp, excluding the destination. Watch the map marker travel from there along the normal route. Observe the walkers nearby using standard NPC navigation.
3. Save/reload during travel. Expect one group with the same surviving member identities/health, not a second group or a spawn near the captured camp.
4. Let the marker arrive while away from the destination, save/reload and approach. Expect all surviving members at the camp, no travelling marker and normal camp AI. Repeat arrival while watching the destination.
5. Save/reload after settlement; residents and Coyotes ownership must remain. Leave and revisit. Killing the residents must not cause the old Quests automatic refill. Confirm old Scrapyard spawns do not return.

Always-on confirming lines: `[POI] Camp ownership: <camp> -> Coyotes`; `[Patrols] Takeover travelers queued for ...`; `[Patrols] Takeover travelers: <source> -> <destination>; nearest Coyote camp, ...`; `[Patrols] Takeover travelers settled at ...: N permanent resident(s).` A missing source reports `Takeover travelers waiting for a known Coyote source camp`.
