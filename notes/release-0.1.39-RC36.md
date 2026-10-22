# Wild Wasteland 0.1.39 RC36

Experimental prerelease. Gameplay, visual and performance verification remains Testing.

## Low Crawl

Y (or your rebound Crawl key) now uses the supplied Low Crawl animation. Move forward/back with W/S; A/D no longer strafes while crawling. The pose holds when stopped and reverses for backward movement. The animation is embedded in the DLL, with no separate asset installation.

Entering crawl holsters the weapon through the game's own weapon states, preserving the item and ammunition. Drawing, firing, reloading, throwing, jumping and kicking are blocked while crawling. On exit, select the weapon to draw it again. Native forward/back speed, capsule, camera, keybindings and overhead standing clearance remain in use. This singleplayer release adds no MP downed-state system.

## Camp shopkeepers

Coyote Merchant, Mechanic and Organic Mechanic NPCs are excluded from guard idle home assignment and patrol walks. Previously the nearest-guard-spawn fallback could send these shopkeepers away from their authored shop spots, including after loading. Ordinary guards, combat and explicit travel orders retain their behavior. Already-displaced merchants in existing saves are not teleported back.

## Quest conversations

Moving the mouse during an open quest conversation no longer turns the player's view. The guard covers CleanHUD and fallback dialogue, including with PlayerBody disabled, and releases when the conversation closes. Native mouse look previously read the axes directly despite the existing cursor/button guards.

Authored quest voicelines are temporarily disabled while the recording set is incomplete. Text, choices and rewards remain available; voice files and saved voice settings are retained. Other NPC/player sounds and optional Talk's own dialogue audio are unchanged.

## Installation

Extract the full ZIP into `BepInEx/plugins`, preserving customized assets. RC35's full assets and improved male/female player models are retained. No personal configs or saves are bundled. No new config entries, save keys or save-format changes. The Low Crawl authoring source is retained in the repository under `tools/crawl-assets`.

## Checks for Denis

1. Confirm startup reports `Loading [Wild Wasteland 0.1.39]`, all enabled modules start, and there are no patch conflicts or initialization failures.
2. Draw a loaded gun and press Y/rebound Crawl. Check native holstering without lost items/ammo; W/S forward/back; A/D no strafe; mouse rotation; held pose while stopped. Fire/reload/grenade/slot/jump/kick inputs must remain blocked. Check both camera views and character models, entering during firing/reloading, low overhead clearance on exit, manual redraw after exit, save/load and PlayerBody off/on.
3. Revisit a camp with the three shopkeeper types. With ShowNavigation on, wait through idle walk/return intervals: no Idle home/patrol assignments or Idle markers for shopkeepers; ordinary guards retain them. Repeat after save/load and hire/dismiss if available. Explicit travel orders must still work.
4. Open a quest conversation with F/rebound key, move the mouse while hovering/scrolling/clicking, then CLOSE/Escape. The view stays fixed while open and resumes without accumulated turning. Repeat with CleanHUD off, PlayerBody off and after loading. Accept/remind/turn in a quest: text/rewards work silently and other sounds remain audible.

Startup confirmation lines:

- `[PlayerBody] Low Crawl ready: native Y stance, forward/back only, weapons holstered.`
- `[Quests] Conversation mouse-look guard installed.`
- `[Quests] Quest voicelines temporarily disabled (recording set incomplete).`

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with zero warnings and zero errors. Built and release-staged DLL MD5: `24B8065AC9FF4454F51CC1E2B5993CE1`. No game is launched by the agent. Release staging remains under `V:\Repos\Apocalypter\_releases`; the existing no-base-game-deployment policy remains in force while MP testing continues. No Apocasetter-Index update or promotion to Verified.
