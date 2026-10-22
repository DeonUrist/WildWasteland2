# Wild Wasteland 1.0.9 — Authored patrol vehicles

- Patrol groups can use authored truck and bus templates. Runtime roster composition and Coyote group eligibility no longer exclude entries because they are classified as trucks.
- The patrol editor now allows adding these templates and includes them in its vehicle count and entry status.
- Template availability, faction compatibility and explicit patrol-size/spawn-limit settings still apply. Existing saved groups retain their recorded roster and losses; updating does not rebuild them.

Includes all 1.0.8 Weapons Pack/BloodFX compatibility and native IK changes. Optional companion mods, generated LOD packs and the developer profiler remain separate installations. Extract the full ZIP into `BepInEx/plugins/`, preserving customized external files and backing up saves.

The reported Scrapyard Truck War Rig encounter containing one car was not conclusively diagnosed: detailed journey logging was off, and the installed rig was tagged `small`, so the removed truck filter alone does not establish that encounter's cause. This release implements the requested roster rule; gameplay remains Testing. Check a newly generated patrol with authored trucks/buses, including duplicate entries, and verify the editor count matches the roster at 100% group size.
