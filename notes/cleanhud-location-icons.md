# Specific location icons (2026-10-21, Testing)

The 31 new PNGs were in the installed `CleanHUD/Icons` folder, rather than the repository. Copied them into `src/Modules/CleanHUD/Icons` without changing the installed files.

`MapIcons` now indexes descriptive names such as `Camp_13_radio_tower.png` by their native `Camp_13` identity; exact filenames take priority. `LocationMarkers` selects that icon at registration, with existing category defaults for locations without specific artwork. Camp clearing still follows the location identity and preserves the cleared white color. Existing map/compass obfuscation switches continue to show `unknown.png` before discovery and the specific icon afterward. Off settings and discovery/save semantics are unchanged. Three existing files changed (15 additions/3 removals), plus the imported artwork.

Windows Release / Deploy=false succeeded: zero errors, two existing NPC CS0649 warnings. Game process Apocalypter PID 42472 was running, so the DLL was staged outside the game, not deployed. No game launch, harness, config/save edits, commit, push or release.

After deployment and restart, Denis tests a recognizable Camp/Building/Wreck on both map and compass: disable obfuscation and expect its specific artwork even before discovery; enable obfuscation and expect a question mark until discovery, then its specific artwork. Save/reload should retain discovery. Off should keep icons hidden. Clearing a hostile camp should still turn its icon white. The existing startup log `Map/compass icons loaded from disk: ...` confirms loading; visual correctness remains for Denis to verify.
