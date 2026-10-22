# Single-player 1.0 player wiki rewrite — 2026-10-19

Denis requested the public wiki rewritten for all current 1.0 features, explicitly
as gameplay guidance rather than technical terminology. Source baseline:
`368a5ca` / tag `v1.0`. Shared files in the local hierarchical wiki matched public wiki
HEAD `f18105e` before the rewrite. The remote-only obsolete Automatic-Step-Up
page is retained as a pointer, without claiming a feature absent from current source. A separate Git checkout is at
`V:/Repos/Apocalypter/_staging/ww-wiki-1.0-gameplay-20261019`; the original snapshot
is preserved under `_staging/ww-wiki-1.0-snapshot-before`.

During the rewrite, source `9cc5219` / released `v1.0.1` and wiki `c5abf61`
landed. The publishing checkout was fast-forwarded to include that history;
the guide now includes the opaque-fog correction and links to 1.0.1 downloads.

38 canonical player guides cover movement/body/cameras, combat/inventory,
enemy awareness/tactics/wildlife, patrols/merchants/vehicles, War Rig and its
abandoned POI, weather, quests/hiring, cooking, HUD/exploration, saves/autosaves/
backups/cleanup, installation/migration/compatibility and performance/support.
121 original slugs retain short pointers to the relevant current guide. They
are omitted from the new homepage/sidebar. All prior technical tables and RC
logs are replaced; the old backlog is a pointer to actual optional-mod guidance.

Evidence: released README and notes/release-1.0.md; current CorePages,
SaveLibraryPage, SaveSystem/TunedSettings/HealthStacks; embedded DefaultSettings;
module gameplay/settings and WarRig/PoiEncounter; NPC behavior and former feature
pages checked against release changes. Effective defaults, not stale RC settings,
are used. Exact optic-reveal distances are omitted because supplied tuning changed.
High-ground scouting extends exploration/compass range; tutorial/Codex and
optional Vistas region/landmark banners are included.
Hiring requires NPC and Patrols support as well as Quests. Native quest voices
remain disabled. No MP-only features are presented as included.

`_Navigation.json` selects the new player-guide generation mode. The small
wiki-index.py dispatcher retains historical behavior for old snapshots;
wiki-gameplay.py validates unique names, page membership, old-link destinations
and internal links and generates the four standard outputs. WIKI-FORMAT and
AGENTS section 11 document the user-requested replacement format so later index
runs do not restore the development-tracker homepage. JSON now exports player
guide categories/summaries/version rather than project-board metadata.

Validation: run the index generator on the hierarchical snapshot and flattened
publishing checkout; check all generated links and diff whitespace; confirm the
only main-repository changes are documentation and wiki tooling. No game source,
DLL, release assets, live configs, saves, project-board statuses or deployment
are changed. No game build is needed for this documentation-only task.

Published wiki commit: `924c35a0509c5792eda455309ca4dc580d8de368`.
Remote master matches the local commit. Both hierarchical and flattened generation
passed; all 292 internal links across 162 Markdown pages resolve, 121 old URLs
remain useful, and the flattened checkout matches the source snapshot. The
published rewrite is 164 files (38 canonical guides plus aliases, indexes and
navigation/export JSON). Main-repository tooling/docs changes remain uncommitted
per the standing rule; runtime source and release history are preserved.
