# Wild Wasteland 1.0

The single-player release brings the recent shared gameplay improvements into standard Wild Wasteland, including the final seated fists/body/camera changes. All pending SP source, artwork, tuning profiles and implementation records are included in this release commit.

## Main additions

- Named screenshot save library with search, separate manual/autosave/backup views, confirmation dialogs, deletion history and backup restoration.
- Native save transactions, complete companion-file capture, interrupted-write recovery, static world history and legacy-slot loading/Continue integration.
- Separate autosaves with a visible countdown and combat deferral; manual/exit saving in vehicles and optional driving autosaves. Loading a seated save parks the car, places the player beside it and restores the on-foot camera, including the latest camera-parent rotation fix.
- Partial inventory drops for ammo, grenades, flashlight batteries, Nuts, Bandages and First Aid.
- Eight playable appearances and portraits, improved head/arm posing and crawl guards.
- Seated melee, visible blast lances and grenade throws, plus the final fists follow-up: knee-rest blending, punches, torso/whole-body turns and first-person head/body alignment.
- All 18 current loading illustrations, matching tips, title background and animated transitions.
- Tuned compatible defaults and missing-only profile installation; existing saved SP settings and migrations retain precedence. Ignition remains I by default.
- Stew pot handles/body render across all GLB submeshes; landed-bolt visuals, weather/haze, compass labels, map fog placement and shared targeting fixes are included.

Existing Low Crawl, motorcycle poses, merchant idle protection, quests and the other integrated modules remain included. Multiplayer transport, roster, sleeping players and host controls are outside this release.

## Installation

Extract `WildWasteland-1.0.zip` into `BepInEx/plugins/`. Preserve custom assets and back up saves before updating. Disable replaced standalone plugins, including Apocasaver; Apocasetter remains supported. Installation and the current feature overview are in the repository README.

The ZIP contains only the plugin and runtime assets, plus the required weather license notice. It excludes the README, historical release notes, documentation folders, source, personal configs and live saves. All retained runtime assets come from the verified published RC37 archive; current loading artwork replaces its older defaults. Default tuning and new profile/portrait resources are embedded in the DLL.

Twelve SYSTEM save/cleanup settings and the optional Debug recommendation setting are new in this port. Fresh/missing saving defaults: 20-minute interval, 30-second warning, five autosaves/backups, screenshots on, driving autosaves and cleanup off.

## Validation status

Windows Release build passed with **zero warnings and zero errors**. Built/staged/packaged DLL MD5: `8E14EC54659A074EC2CDC1F3AAC70E8E`. Unique DLL stage: `V:/Repos/Apocalypter/_releases/WildWasteland_1.0a.dll`.

Release archive: `V:/Repos/Apocalypter/_releases/WildWasteland-1.0.zip` (218,473,303 bytes, 198 files). SHA256: `c81217b21acf80b96ce54c68d2abce39a7501b37cd7a5a43c61abf96d3adc945`. The package tool retained 177 unchanged runtime asset entries, removed 57 documentation entries from RC37 and verified all replacement bytes/CRCs, the single plugin DLL and the complete 18-screen set. The required JangaFX license notice remains. Packaging is reproducible through `tools/package-release.py` with the published RC37 archive hash.

Windows Release build uses `-p:Deploy=false`; standard-WW deployment remains disabled while MP testing continues. Gameplay, visual fit, save-integrity/failure recovery and performance remain **Testing**. No game launch, harness, live configuration/save edits or Apocasetter-Index update.

Denis should check seated punches/weapon swaps/side-and-back turning in both camera modes, vehicle save/reload with the mouse initially still, native and styled save/Continue/backup workflows, partial drops, character switching, pot handles/drinking/reload and the loading artwork. Detailed checks and implementation records: [SP port](singleplayer-port-2026-10-19.md), [seated attacks and fists](seated-melee-throws.md).
