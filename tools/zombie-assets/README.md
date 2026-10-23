# Zombie animation assets

Run `build-assets.ps1` in Windows PowerShell with installed Unity 2020.3.49f1. The script creates a separate authoring project at `V:\Repos\Apocalypter\_tools\WWZombieAssets`; it does not open the game. Source downloads remain unchanged and outside this repository.

Inputs supplied by Denis:

| File | SHA256 |
|---|---|
| Scary Zombie Pack.zip | E1B3020686F2046C2EA1A07F69112C3D6D2AA1FB1E923B74D69FE6BF105016DC |
| Zombie Stand Up.fbx | 185C603260BD4747ECCEBC3485064C54931CD13AA9159A201F3C05D1CD8D5A75 |
| Zombie Stand Up (1).fbx | 8E6CD987A74A244A1F0468CC66418DB48031F93C8877F1DA29E0FDE78D70B723 |
| Zombie Agonizing.fbx | 67C0B2D9FAC6F6F2781865D191AB480F7DA9236A5F609D7A8D7742F1E9220152 |
| Zombie Idle.fbx | A114B9DACAACD717A220F42CAFD1170B35135796C4EA981E9DA7629A940BB2E9 |
| Zombie Scream.fbx | AE138F21974AD5DB17BB280F055C0FB7EB3A9A25470A5BD4634833E14561FB0A |
| Zombie Crawl.fbx | 4CBC610AE7F42FBFAA183EAA25264E2A17E7706B63B391A9022EFF5632DBA97F |
| Zombie_Run.fbx | 7236F6DD4B977C7238577E3606CBF2CC1E256E5932CD9FAA936595C7F667B2C4 |
| Zombie_Stand_Up.fbx | 3D531D72D431C2B5C13C65B86A12D0C078BC419AA47F1F7A928A9D9BBFE2628E |

`ZombieAssetBuild.cs` imports 14 humanoid clips, retains vertical poses, bakes planar root motion in place, and builds `src/Modules/Presentation/Embedded/Models/zombie-anims-v3.bundle` for Windows64. The supplied loose FBXs replace the pack's idle, run, scream and crawl; Zombie Agonizing replaces the earlier dying substitution. Locomotion loops; actions and stand-up clips do not. All three stand-up beginnings supply resting poses (side, belly, back); reversing the matching stand-up supplies its lie-down transition. The underscore-named Zombie_Stand_Up is the third style. The X Bot model and zombie death clip are excluded: native rigs and ragdoll death remain in use.

2026-10-23: revised Unity asset build succeeded, all 14 clips reported `humanMotion=True`, bundle 3,863,553 bytes. Run is 0.8 seconds and the new back-to-standing take is 3.1 seconds. A new bundle filename allows missing-only deployment without overwriting an installed asset. This verifies asset import and packaging only. In-game retargeting, ground contact, transitions and behavior need Denis's acceptance.

Runtime behavior lives entirely in Presentation (`Character/Zombies`). Native Movement/Detection and the NPC spawn event discover both vanilla and Apocaspawner clones, with one rig built per frame. Half of new zombies begin lying down, randomly choosing among the three stand-up beginnings. Each zombie retains its chosen side/belly/back style for every rest and get-up. Quiet standing zombies use the newly supplied standing Agony clip and return directly to standing (its RootT.y remains .72-.89, unlike the old dying clip which fell to .08). Crawling is disabled. Stand-up and reverse lie-down use at most the first 3.2 seconds, removing the approximately three-second standing tail visible in the first two source get-up curves; a pursuing zombie resumes native Animal_Run and forward velocity on completion.

Punch animation uses the existing CharacterRig upper-body AvatarMask over a continuously timed Run base. The run phase belongs to the rig and continues through attack start/recovery instead of resetting with the punch clock; legs and root stay on the running clip while the upper layer plays the punch.

At Denis's request, the evaluated running pose now rotates each cached upper leg four degrees inward around the rig's forward axis. This applies after each fresh animation sample, including running punches; idle, rest and traversal poses are not narrowed. Zombie Runner and Blast Zombie may route over existing authored POI climb/drop links. Zombie paths exclude ladders; other NPC capabilities retain their existing behavior. Climbing leases the existing ClimbWaist/ClimbHigh animation; drops use the existing jump-loop pose when available. The zombie graph detaches its Animator output and suspends decisions for the lease, blocking melee during traversal, then resumes running. Kinematic POI walking reads transform motion for its run/walk pose. This adds no arbitrary-world ledge detection or new links; the POI must already have recorded navigation.

The upright one-second punch now runs in its committed wind-up direction, like the canine running bite, with a native Rigidbody sweep against obstacles. Speed is 5-7 m/s through the first half-second, then 2 m/s through recovery. The native AttackRaycast length increases from its authored 1.2 m to at least 2 m and restores on release. Damage is permitted only at .3-.5 seconds while the victim remains in front of the committed direction (dot >= .7), and still requires native ray intersection; native damage amounts/cooldowns stay unchanged. No broad commit bypass is used while sending native back events, so nested sensor events must obey the same wind-up. Scream rolls occur only on a new real-player target at 12 m or more (25%); there are no melee scream rolls, and closing within 4 m interrupts a scream. Imported ground-bite/crawl clips remain in the bundle but are not selected. The existing canine movement lease suppresses brain steering during owned actions without hiding the zombie from sensing. Presentation pauses native Rotate and Unstuck through its own FsmPause claim during owned actions, then releases that claim. This prevents native Unstuck's upward impulse (Sources/fsm/prefabs/Zombie_Runner.md:307) during stand-up, without a duplicate Rotate.DoRotate Harmony patch. Scene changes, death and disabling restore native animation/movement state.

`settings.cfg` adds `[Presentation / Zombies] ZombieAnimations = true`. The Animation framework tool page shows the toggle, bundle state and active count. With global verbose logging, `Zombie animations:` lines report registration (including resting style), stand-up, `running after GetUp`, distant sighting screams, running-punch reach/contact timing and authorized native hits.
