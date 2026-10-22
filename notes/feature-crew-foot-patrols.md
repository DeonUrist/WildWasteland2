# Crew on foot, return to car, foot patrols, map zoom, debug switches (2026-10-15, on 0.1.22 RC19) - Testing

Requested by Denis after a cave fight where dismounted crew kept running into a wall once their enemies were dead (reset to idle only after reload).

## Cause of the stuck run (proven by code, not by log)
Dismounted crew are simple convoy bandits. When their target died, Brain went to Mode.Off without stopping the vanilla Movement FSM's Run state (SetMode only stopped it when leaving a still mode), and Idle skipped simple bandits entirely, so Idle.RunInPlace (made for exactly this) never ran. Not a perf-pass regression; nothing of this runtime state is saved, hence the reload "fix".
Fix: Idle applies the RunInPlace check to idle simple bandits (2 s grace after a jump-off); Brain SetMode(Off) from a moving mode with no target left sends Animal_Idle (Move only acts in Attack chase/hide states).

## Behaviour
- Pathfinding: SimpleThink chases via Brain.MoveToward (Nav.Next / OutNext / NavLegClear / feelers / progress / hops); hold rule unchanged.
- Awareness: a crew member targets the player only after noticing him through normal sight/hearing (NoticeSeconds..NoticeFar) or with fought-memory (90 s, refreshed while fighting; handed over at dismount when the car had contact with the player). While remembered, losing sight keeps the player tracked at his real position (crew only). Patrols offers the player as an on-foot goal only within FootTargetRange (350 m).
- Return to car (Debug, default on): when all living members are idle for 3 s they walk back (Idle travel leg), re-seat in original order, a passenger/gunner takes the wheel if the driver is dead, and the car leaves only when everyone living is aboard (timeout 120 s while calm, 900 s overall). Cancelled when the car is destroyed/exploded/flipped/taken/out of fuel/stuck/unloaded (a stuck car never takes its crew back: they become a foot patrol); then members become stranded. Not saved across reload.
- Foot patrols (Game > World, default on, needs world traffic): "Scrapyard travelers" / "Coyote travelers" (random faction, 2-5 people rolled at random from that faction's NPC types; no editor groups, no distance/boss progression; Coyote shop keepers excluded) leave from the same loaded, uncleared camps car patrols use (within Camp traffic range) but only ones at least FootPatrolMinSourceDistance (500 m) from you, so you never see them leave walk out to a far location (Building_1-7 / Wreck_1-8 / Cave_1-7 / Starter_Area / Coyote camps, outside other camps; the same location list roaming Coyote patrols use, anywhere within one map chunk, >= FootPatrolMinPoiMetres 1000 m preferred) and back to their camp over the same land routes as cars (1.3 m/s simulated, real within 280 m, simulated again beyond 400 m). Loaded destination: they become camp residents. Unloaded destination: they vanish. Generated foot patrols only leave from camps out of sight (>= 500 m), so on-foot groups seen leaving loaded areas are always car-fight aftermath (no car / stuck car). Stranded crew (no usable car, 30 s out of combat) are wrapped into a foot journey to the nearest uncleared same-faction camp.
- Map: foot patrols are not inked and never on the compass; a coloured dot with a black outline + "<Faction> <Group>" label only with Map Routes On. Coyote walkers keep their crew tags: Quests lets the player hire them on foot but they never give quests. Mouse wheel zooms the map in to 1.5x (never out past native), reset on close.
- F3 Debug: all toggles in a "Switches" block under Logging, grouped by module ("(module off)" marked).

## New config
settings.cfg: Patrols "Foot patrols" (true), "Return to car" (true).
tools.cfg: NPC / Senses FoughtMemorySeconds 90; NPC / Idle TravelRunBeyond 25; Patrols / Traffic tuning FootPatrolMaxActive 2, FootPatrolSpawnChance 35, FootPatrolCheckSeconds 60, FootPatrolSpeed 1.3, FootPatrolLoadRange 280, FootPatrolUnloadRange 400, FootPatrolMinPoiMetres 1000, FootPatrolMinSourceDistance 500, StrandedIdleSeconds 30, ReturnToCarTimeoutSeconds 120, ReturnToCarMaxDistance 300, DismountMemorySeconds 90, FootTargetRange 350.

## Perf
No new scheduler jobs or per-frame scans: CrewReturn on the crew's 0.5 s gate (random phase), foot journeys on the WorldTraffic 0.25 s tick / 1 Hz population / 1 Hz stranded check, compass 0.5 s, map labels 1 Hz, walkers through the existing capped Idle/Brain thinks; one walker instantiated per frame at most. Crew chasing now thinks at the brain interval (inside the per-frame cap) instead of a fixed 0.6 s.

## Test (VerboseLog global + NPC VerboseLog)
1. Cave fight, kill their enemies: `Brain: <crew> Chase -> Off (target lost ...)`, possibly `Idle: <crew> was running in place ... now stands`; they stand, then `Return to car: ... fight over, N crew walk back`, `... is back in <car> ...`, `regroup ended (everyone aboard), driving on`.
2. Leave a cave after fighting crew: `sight loss: ... - kept: remembers fighting the player`; they follow you out through the exit. Untouched crew stay idle until `notices the player at N m (convoy crew on foot)`.
3. Kill the driver: `<name> will take the wheel`. Take/destroy the car: `regroup ended (...)`, ~30 s later `Foot patrols: N crew without a car (...) walk to <camp>`.
4. Foot patrols (FootPatrolSpawnChance 100, FootPatrolCheckSeconds 5): `Foot patrol <name> (N walkers) leaves A for B`, `materializes`, map dot with Routes On, save/reload keeps them; arrival `arrived at <camp>: N new resident(s)`.
5. Map wheel zoom in/out, markers/fog/routes aligned; third-person and ironsight zoom unaffected while the map is open.
6. F3 Debug: Switches block on top, each toggle once; editor switches still open their editors.
