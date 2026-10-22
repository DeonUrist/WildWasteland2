# Wild Wasteland 0.1.8 RC5

Release candidate 5 fixes companion hiring and updates map, compass and quest presentation. Numeric BepInEx version **0.1.8**; tag **v0.1.8**. All RC4 assets and compatibility features are retained. Gameplay and performance verification remain **Testing**.

## Changes

- Fix the Quests 1.8.0 follower hook: its method is `TryMount`, not `Mount`. The failed binding previously rolled back the compatibility module, leaving the original NPCAI/Convoys requirement. Hired followers can now reach WW's existing NPC, animation, reload and passenger combat providers. Read the `Hire.Active` property without a missing-field warning.
- Distinct white cave and wreck symbols on the native debug map and compass. Camp symbols are red for Scrapyard and green for Coyotes. Map icons have no name labels.
- Game → HUD controls: **Compass Off/On** above Convoys on compass; **Map Off/On**, **Map markers Off/Ambiguous/On**, then **Map Routes Off/Ambiguous/On**. Ambiguous markers use a white `?`; ambiguous routes use gray Unknown labels. On restores real symbols, colors and full route names.
- Compact dialogue answer rows with gold/yellow text matching the HUD palette. Original dialogue and quest callbacks are preserved.
- Quest notifications such as NEW QUEST use CleanHUD's existing message log. CleanHUD off restores the original dialogue window and notifications.
- Active quest objectives, distance and time use a CleanHUD tracker in the top right, below Insanity when shown. Multiple quests stack vertically, wrapping long text into separate rows. The original ShowTracker setting remains authoritative; CleanHUD off restores the original tracker.

## Settings and saves

Four settings.cfg entries are added: `[CleanHUD / HUD] Compass=true`; `[CleanHUD / Map] Enabled=true`, `Markers=On`, `Routes=On`. A missing Routes value adopts an existing disabled/ambiguous Patrols route setting. Existing configuration and saves are preserved, including ES3 key `ApocalypterQuests` and Talk's global/manual story profiles.

## Installation and verification

Close the game and extract `WildWasteland-0.1.8-RC5.zip` into `BepInEx\plugins`, updating `WildWasteland\`. This is the complete asset package. Keep **Apocatalker 2.0.0** and **Apocalypter Quests 1.8.0** installed separately with their original data folders; they are optional dependencies and are not bundled. Remove/disable absorbed standalone mods listed in README, including standalone CleanHUD. Immersive Map remains supported when installed.

After restarting, confirm `Loading [Wild Wasteland 0.1.8]` and `[QuestsCompat] Follower bridge ready`, without InitFailed. Hire an eligible NPC and test following, shooting, reloading, car boarding/firing/exiting and dismissal. Check all HUD modes, cave/wreck/camp symbols, compact gold answers and quest notifications. [Detailed test steps](https://github.com/DeonUrist/WildWasteland/blob/v0.1.8/notes/patch-map-hiring-dialogue.md).

Validation: Windows Release/Deploy build passed with zero errors and seven existing warnings. Built/deployed DLL MD5: `B64AD34DA74EF5AA4FC0EFD9E7425F3E`. in-game verification and performance profiling remain Denis's task. This candidate does not mark features Verified or update the Apocasetter index.

[Previous candidate: 0.1.7 RC4](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.7) · [Feature documentation](https://github.com/DeonUrist/WildWasteland/wiki)
