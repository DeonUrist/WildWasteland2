# Low Crawl backport

2026-10-19 — Testing. Denis requested the supplied Low Crawl animation, native Y crawl without strafe, weapon holstering, and the same change in singleplayer WW.

PlayerBody/Animation/Crawling.cs loads the embedded 2.333333-second humanoid clip and subscribes existing input, velocity, event and stance dispatchers. CrawlBody.cs supplies full-body playback/idle/reverse movement and lifecycle cleanup. Native capsule, camera, forward/back speed, rebindings and standing clearance remain; sideways velocity is zero. Native Weapons Activate/off 2 and WeaponInHand Deactivate holster without dropping/recreating items; weapon redraw/fire/reload/grenade and jump/kick are blocked while crawling. Exit leaves the weapon holstered until manually selected. First-person arms use the crawl pose. No Core changes, new config or save-format changes.

Original FBX and source hash, Unity Editor 2020.3.49f1 builder and import settings are in tools/crawl-assets. The bundle is embedded; no separate game-folder assets need installation. The original tilted-prone fallback does not reappear during exit. Body destruction/death/seating/scene/module lifecycle restores the normal graph.

MP uses the same common source plus a separate adapter for native downed-state input and existing packet-82 final-pose synchronization. This base backport introduces no new singleplayer bleedout system; there is no native MP downed state in singleplayer WW.

Windows Release/Deploy=false passed with zero warnings/errors. Built/unique stage MD5 `6FA22BA64D4F3DBDD2118D02AF60EBE8`, stage `bin/Release/WildWasteland_low-crawl_20261019_013433.dll`. Not deployed under the standing singleplayer repo-only policy. Six C# files plus embedded bundle/authoring/docs. No game launch/harness, commit/push/release.

Denis should draw a loaded gun and enter Y/rebound Crawl: holster without losing gear/ammo, W/S forward/back, A/D no strafe, mouse rotation, held pose when stopped. Fire/reload/grenade/slot keys/jump/kick must not activate while crawling. Check first/third person, skin ground contact/culling and enter/exit while firing/reloading. Y/Crouch should retain overhead clearance and normal redraw after exit; reload world/change character/toggle PlayerBody off/on without stuck graph ownership. Startup `[PlayerBody] Low Crawl ready: native Y stance, forward/back only, weapons holstered.` confirms initialization. Runtime appearance/performance remain unverified; asset import and DLL compilation do not test gameplay.
