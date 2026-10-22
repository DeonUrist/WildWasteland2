# Patrol POI assault and AI gearbox — Testing, 2026-10-21

Denis approved the Patrols-only scope above 50 lines, requested proper gear changes, and explicitly requested no deployment. Eight Patrols sources, 241 added/11 removed lines including new CrewPoi.cs; existing shared-tree work is preserved. No Core/NPC/config/save-format/asset edits, commit, push, release, game launch or harness.

## Cause and behavior

Previously CrewCombat required a recent unanswered attack, a slow car and a nearby attacker before dismounting. A seated crew able to answer with gunfire could therefore remain in its car at a POI. The ordinary driving target remained the player rather than an exterior parking destination.

CrewPoi reads the existing Core.Services.Npc.Structures adapter at most twice per second across all cars; named native camps/buildings/caves/wrecks/starter area are detected by their navigation bounds (including the legacy bake margin), with a small vertical tolerance. No new scene scan or NPC/Core dependency is added. A hostile non-merchant/non-convoy world patrol with remembered player contact or the player as its combat enemy chooses an exterior parking point. It does not discover an unknown hidden player merely through POI membership. ReturnToCar and the NPC module must be enabled.

Parking surveys twelve terrain/occupancy candidates outside the bounds, rejects excessive slope and straight approaches crossing the expanded POI footprint, and uses the nearest accepted point. A car already standing outside and within 30 m can stop where it is if surveyed spots are occupied. Unsuccessful surveys retry at most every two seconds per car. A worker RoadPlanner request supplies a terrain route to the destination; the car brakes while it completes, follows its waypoints and corner speed limits, and falls back to existing local vehicle avoidance if no certified route is available. There is no guarantee of arrival at every obstructed site; Denis must test actual paths, corners and parking clearance. Existing vehicle recovery/stuck behavior remains.

Approaching patrols retain shooting permission throughout the POI engagement, independent of detection-memory expiration. Weapon range, firing arc and line-of-sight checks remain. The driver brakes at the parking destination; below 1 m/s the normal door/dismount path releases driver, passenger and human turret crew. Existing NPC CrewOrder and Brain navigation provide on-foot pursuit, including the new baked POI routes and ladder/climb traversal when data and links are available. No second pedestrian pathfinder is added.

CrewReturn recalls a POI assault when the player leaves that same POI, becomes non-hostile/unavailable, or moves beyond the parked-car engagement radius. The radius is at least 120 m, increased to the initial dismount distance plus 30 m for large POIs to avoid an immediate recall. Re-entering before boarding finishes can resume the engagement. Original seat order, health, driver replacement and car-usability checks remain. At the end the POI enemy is cleared, the original journey reattaches, and player pursuit pauses for ten seconds so the patrol can depart. Fully stuck/unusable cars retain the existing stranded-crew path. Regroup membership is runtime-only and does not survive reload, as before.

Cargo convoys are distinct from ordinary patrols: convoy journey targeting, gunfire and retaliation/pursuit overrides ignore a player inside a detected POI, as Denis originally requested. Non-player faction combat is preserved. The exclusion does not apply to ordinary patrols approaching their parking destination.

## Gearbox evidence and ownership

The pilot previously requested first gear whenever it needed forward motion but supplied no higher manual gears. Native Rustcargo and Duke serialized transmissionType=0 (Manual); Sources/fsm/prefabs/Rustcargo.md, Bool_AutomaticGearbox, also makes the player's global manual/automatic option apply to the car. NWH TransmissionComponent shifts upward by native RPM logic only in automatic mode.

InputControl now pauses native gearbox/clutch/H-shifter/gear-button FSM writers while AI owns the car, temporarily converts Manual to Automatic, enables the automatic clutch, disables hold-to-keep-in-gear, and uses explicit direction shifts with unswapped reverse throttle. Existing CVT/other non-manual modes are retained. Release restores the prior transmission, clutch, direction-shift, held-gear, input-swap and input-provider settings and the FSMs it paused. Ownership is idempotent; reinitialization and component destruction release it. This uses native shift points/ratios rather than changing gearing or engine power.

## Build receipt

- Windows Release, Deploy=false: zero errors, two existing NPC PoiNav CS0649 warnings.
- Final stage: `V:/Repos/Apocalypter/_releases/WildWasteland_patrol-poi-gearbox_20261021_163808_306.dll`.
- MD5: `19CA5E73B48510C4BC70A22D4418F279`.
- **Not deployed, per Denis.** Earlier 16:35 stage is superseded by this build's route and gunfire follow-up.

## Denis's game checks

1. Let a hostile ordinary patrol notice you, enter a POI, and remain visible where possible. It should navigate to exterior parking and continue normal gunfire during the approach, then stop, open doors and attack on foot without needing a fresh unanswered hit. Repeat after detection memory would normally expire.
2. Use an upper floor or ladder with current baked POI data. Crew should use the existing links; ranged/melee ladder behavior follows the NPC system. Check both a camp and a building, an already close car, occupied parking, corners and sloping ground.
3. Leave that POI or move beyond the engagement radius. Surviving crew should return, board, retain wounds and resume the original patrol. Check a dead driver, returning before boarding completes, unusable car and world/origin changes. Reload has the existing runtime-only regroup limitation.
4. Stand inside a POI near a cargo convoy: it should keep its journey and ignore the player, including seated gunfire. Ordinary patrol cars should still shoot until dismount. Non-player faction fights should continue.
5. Set player gearbox/clutch to manual, observe patrol acceleration on a sufficiently long straight road, and check gears rise above first as native RPM thresholds are reached. Check downshifts, reverse recovery, parking/reboarding, taking the car and module shutdown restore player controls/settings. Low speed or turns can legitimately keep first gear.

With Patrols verbose logging, look for `POI assault: <car> parks outside <POI>`, `POI assault: parked; crew attacks on foot using NPC navigation`, existing `Return to car: fight over`/`regroup ended`, and `POI assault: ended; crew returns to patrol`. Route failure logs explicitly identify the local-avoidance fallback. `AI gearbox: ... Manual -> Automatic; native RPM shifting, automatic clutch` confirms takeover; the Pilot overlay shows actual gear. No in-game behavior or performance has been verified by this build.
