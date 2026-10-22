# Shared step 2 — module boundaries

Source implementation completed on 2026-10-22, locally on `refactor/main`, following the completed shared character-driver step. This closes the remaining distributed private partial implementations. It does not certify gameplay acceptance or the Wave C global cleanup.

## Final ownership

- Compatibility owns logical external inventory, external item names, Immersive Map coexistence, dust-storm fields, external vehicle factories, Part Adjustment and Motorcycle Mod reflection/hooks. Consumers query explicit Core contracts. The new optional vehicle/map bridges each have their own resource scope and switch. Apocasaver remains removed.
- Vehicles owns native wreck mechanics, light/brake operations, suspension and runtime vehicle template resolution. Patrols retains destruction decisions, saved wreck flags, authored condition rolls and crew policy. Native operations no longer reach private traffic components; traffic shutdown is supplied through `IVehicleCrewWreck`.
- Presentation owns reload/shout audio, rider rigs, held lance visuals and voice sources, animal rig sampling and sand-effect pools. NPCAI owns canine decisions, native damage gates and procedural animal timing. A shared canine handoff record replaces the private Presentation/NPCAI partial. Compatibility supplies its own companion actor projection and movement operations, without constructing a private NPC Agent.
- Patrols owns rider targeting, escort, throw/leap/dismount decisions, native landing control and traffic sensing. Gunplay owns actual lance spawning, collision fuse, blast and player damage. Presentation supplies disposable rider/landing animation leases, preferring the common character driver and preserving the existing clip fallback.
- Rider tuning is bound once by its actual Patrols controller under `[Patrols / Riders]`; the old NPCAI forwarders and redundant module-entry bind methods are removed. Dog Companion has one authoritative Compatibility switch; the extra NPCAI switch is removed. D12 applies: no old-section migration.
- Loot owns merchant stock lookup and quest item discovery/matching/consumption through `IMerchantGoods` and `IQuestItems`. Quests supplies the liquid threshold per item request and keeps quest-specific actor acceptance projections over its existing Core-backed QuestWorld. The unused NPC copy of that projection/index was deleted. Shared item results contain explicit native references; they are transient query records, not a new save format.
- Canonical native weapon name/caliber metadata lives in Core. HUD consumes the live weapon catalog with native metadata when unavailable; Presentation and NPC fallback queries no longer call the private Gunplay catalog. Gunplay's native shot observer calls its own weapon implementation and typed NPC shot policy.

## Lifecycle review

Rider and landing visual leases are released on disposal, Presentation shutdown and scene changes. Rider release restores native held-renderer visibility and root-motion state. Canine rigs re-register after Presentation replacement; companion registration also recovers after a rig registry reset. Native jump/dash holds, drag, waits, bone state, culling and IK are restored through the actual owners. Companion native distraction suppression remains governed by NPC control even when companion animation is unavailable.

External providers withdraw with their individual Compatibility scopes. Their reflection caches are either cleared explicitly or held by the withdrawn provider instance. Native handbrake lookup is cached by car, retries missing levers and clears on Vehicles scene/shutdown boundaries. A missing wreck provider cannot mark a vehicle destroyed while skipping the native destruction operations. A missing item provider cannot silently complete a consuming quest turn-in.

Persistent state remains in the selected ES3 transaction. No sidecars, fallback imports, live configuration changes or asset relocation were introduced. Rider voice playback retains the existing shipped Patrols audio path; the final asset/packaging audit belongs to Wave C.

## Source verification

- Windows Release, `Deploy=false`: zero warnings and errors; full rebuild and subsequent changed-source builds passed.
- Partial-method checker: 46 declarations, zero missing implementations.
- New `tools/refactor/check_shared_boundaries.py`: zero partial types spanning module ownership roots. Its positive/negative fixtures confirm that it rejects the same partial type in two owners and permits distinct namespaces.
- Changed-source summaries: zero missing in 108 C# files. `git diff --check` passes.
- Global reference heuristic: four remaining findings, not a passing global gate. Two are identifiers (`CharacterPoseDraft.Aim` and `Agent.Crew`), and two are Vehicles' retained legacy namespace imports in `VehicleAttachmentsApi`/`WarRigWorldApi`. Wave C must rename the remaining legacy namespaces and rerun the global gates without blanket exceptions.

No game, gameplay harness, deployment, push or release was run. Combined in-game acceptance stays deferred until all work is merged.

## Next

Proceed with Wave C: canonical namespaces and remaining obsolete Services/event/settings forwarders; final registry, ownership/file-map, resources and packaging; current documentation and removal of temporary CS0618 suppression. Do not restart completed Wave B packages or Shared steps 1–2.
