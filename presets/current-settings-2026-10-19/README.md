# Current tuning for standard Wild Wasteland

Captured from the main WWMP config on 2026-10-19 and selected for standalone SP. The embedded copies in CleanHUD/Embedded/DefaultSettings now supply defaults to BepInEx Bind. Existing saved SP values and legacy migrations take precedence; RESET uses these tuned defaults. Multiplayer names, shared vision and player display keys are excluded. SP ignition keeps I.

Includes current settings.cfg/tools.cfg plus only the active public-main General/Keys entries. .cfg.txt filenames deliberately survive the repo ignore rule. Orphan entries in the captured reference do not become new bound settings. Native game graphics/input settings and other mods' configs are not imported.

Profiles are embedded under DefaultProfiles and installed only after the original migration, and only when the destination file is absent. The ignition-key cache and generated pose source are excluded. Existing pose, wheel, layout and key-backup files are never overwritten.

Examples: fresh/missing autosave interval 20 minutes, warning30 seconds, five autosaves/backups, screenshots on, driving autosaves off; cleanup off; map edge499.3507/opacity1, routes Ambiguous; default Male, current camera/aim/wheel values. Version1.0.1 corrects the unintended captured opacity0.463468 to1, including an exact-value upgrade for saved copies of that default. Other existing SP choices remain as saved.
