# Wild Wasteland 0.1.4 RC1

Release candidate 1 includes all current local source changes and the reviewed default settings. BepInEx uses numeric plugin version **0.1.4**; the GitHub tag is **v0.1.4** and this release is marked prerelease.

- CleanHUD 1.5.1 is integrated as an independent module, including menus, compass, message log, vitals, quick slots and weapon silhouettes. Standalone CleanHUD must be disabled or removed. Scene/key discovery and icon preparation use the shared scheduler and caches; no main-thread key-worker joins.
- Vehicle ignition, handbrake and playing cassette/volume use CleanHUD vector icons beside Insanity, retaining PlayerBody's fallback and controls.
- Persistent loading/autosave presentation uses five bundled ScreenN.png images and editable screens.json tips. CleanHUD's font stays throughout scene transitions; LOADING/AUTOSAVING is bottom left, tips near top left. Apocasaver presentation hooks cover the full autosave transaction. Quit YES/NO labels are centered.
- PlayerBody weapon editor supports weapon/left-arm/right-arm targets, rebindable context controls, and combined weapon + arm/aim-lift copy/paste. Conditional editor keys and cassette aliases no longer appear as global hardcoded controls in CleanHUD.
- Third-person projectiles originate at the visible weapon muzzle and converge on the eye-ray aim target. The dynamic crosshair checks muzzle obstruction. Crossbows show native bolt meshes in flight/impacts, track loaded bolts on the held prop and transfer attachments to corpses. Patrols crew blast-lance targeting uses the player-body path.
- Internal SETTINGS has Game / Modules / optional Debug pages; F6 shows the settings key and Enable Debug. POI world discovery and always-on HealthStacks/iTween/quiet-FSM integration are included.
- All **691 active settings** were reviewed. Ten differing installed choices become defaults: settings key F3; CleanHUD needs/numbers on; Vanilla crosshair; toggled observing on; camera occlusion off; camera distances 3.26 / 5.38 m; Patrols debug spawn controls and traffic debug-map routes on. **Toggle Ironsights remains off by explicit request.** Existing configs and first-run standalone migration are preserved. [Complete defaults audit](https://github.com/DeonUrist/WildWasteland/blob/v0.1.4/docs/defaults-rc1.md).

Validation: Windows Release build/deployment succeeded with **0 errors** (7 existing warnings). Build, installed and packaged DLL MD5: **8C700DED83EE607C870BEF46172DC2D3**. No config entries were added/removed in the defaults pass. In-game verification and profiling remain with Denis; feature statuses stay Testing where applicable.

Install: unzip `WildWasteland-0.1.4-RC1.zip` into `BepInEx\plugins`, creating `WildWasteland\`. The archive includes the DLL, all external assets, five loading screens, editable tip catalog, README and complete default-settings audit. It includes the current installed Patrols asset revisions. Remove/disable absorbed standalone plugins (Apocaplayer, Apocaclimber, NPCAI, Gunplay, Apocaunloader, Gunplay HUD, CleanHUD, Apocapatrol, War Rig, Merchant Object Spotter; remove the older Apocaraider monolith too). Keep legacy config/data for migration. Apocasetter is recommended; Apocasaver remains optional.

In-game checks: verify `Loading [Wild Wasteland 0.1.4]` and the startup module summary without patch failures/conflicts; open SETTINGS with your saved key (F3 by default on fresh settings), verify the Game and Modules pages, then check ordinary loading and a scheduled Apocasaver autosave. Expect the numbered image/tip and the same CleanHUD font throughout, returning to gameplay after completion. Existing saved values, including toggle aiming, are intentionally retained.

ZIP SHA256: `D7C457DA0D5F6510EF9832E00FD6028088AE0C45D0A079DFEEE52EA26F66486D`.

[Feature documentation](https://github.com/DeonUrist/WildWasteland/wiki) · [Project board](https://github.com/users/DeonUrist/projects/4)
