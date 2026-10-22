# Wild Wasteland 1.0.5 — Towns, gas stations and ladders

## What's new since 1.0.4

- **Towns are included.** Settlements mix native buildings and camps, with faction populations, loot, perimeter walls, faction gates and editable town names. Coyote towns select a merchant camp when one is available. Town merchants are prepared with the town, without requiring entry into their small spawn trigger.
- **Separate parking beside gas stations.** The town bus and visitors use an open concrete parking apron beside the station, with a two metre gap. The station and parking stay outside the town walls. Existing towns update their derived access paths and pad bounds when reloaded.
- **Town laboratory doors are inactive.** Building_3 can still appear in towns, but its laboratory entrance remains solid scenery without an interaction prompt or teleport. Real POI laboratory entrances keep working. The underground exit on town copies remains usable for saves already inside.
- **Roadside gas stations.** Stations can appear beside desert roads, with shop loot and a configurable chance. Their enemy spawners are disabled for now. The station dressing and latest navigation data are included.
- **Player ladder climbing and sliding.** Climb with weapons stowed, slide down, and jump to let go. The shared animation follows ladder rungs and leaning ladders, with first-person placement corrections.
- **NPC climbing and upper-floor navigation.** Human NPCs can use baked POI routes, ladders, climbs and drops. Melee enemies can follow a player on ladders, with spacing between climbers; ranged enemies can approach an end. The release includes baked navigation data and developer tools for editing POI navigation and static dressing.
- **Footstep awareness.** A moving player within five metres can be detected regardless of facing when there is a clear line. The radius is configurable.
- **Crew and loading improvements.** Stopped vehicle crews can respond to nearby attacks and return when the threat is gone. Merchant bus boarding uses its front door. Vehicle template preparation has a larger loading budget.
- **Terrain and cloud fixes.** Towns can use published Vistas road hints and preserve their prepared ground after terrain edits. Town navigation cleanup no longer includes parking shells in the building overlay. Hurricane cloud banks share the body's transparency sorting queue.

## Updating

Install the **complete ZIP** into `BepInEx/plugins/`. Runtime assets now live beside the DLL, with an `asset-index.tsv`; a DLL-only update from 1.0.4 is incomplete. Preserve customized external artwork, models, layouts and pose files. Personal configs and saves are not included.

Remove the separate WildWastelandTowns plugin before using the integrated Towns module. Choose modules through F3 > Modules and town/world options through WORLD. Advanced POI navigation and dressing editors are under Developer.

Existing town records remain readable by this version. After a town is saved with the updated paths, older builds that only accept path version 6 cannot read that town record.

## Validation

Windows Release build passed with zero errors and two existing NPC CS0649 warnings. The package contains the runtime assets and required license notice, with no documentation, personal settings or saves. Gameplay, visuals, navigation and performance remain **Testing**; a successful build is not in-game verification.

After updating, reload a town and confirm the separate parking, merchant availability and inactive laboratory entrance. Test a real laboratory POI entrance and exit, then save/reload. Also check player/NPC ladder travel and return to menu/new world.
