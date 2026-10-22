# Quest conversation input and temporary voice suppression

2026-10-19 — Testing. Denis requested both WWMP and singleplayer WW fixes, commits and DLL builds.

Native quest conversations free the cursor through EditorSession and block button actions, but MouseLook.DoMouseLook reads the mouse directly without consulting those hooks or cursor visibility. Player's Look FSM uses MouseLook on PlayerCameraHolder before copying yaw to Direction (Sources/fsm/scene1_game/Player.md:467; Sources/code/Assembly-CSharp/HutongGames.PlayMaker.Actions/MouseLook.cs).

The same PluginRuntime prefix as WWMP skips player-owned MouseLook.DoMouseLook while the native conversation is open. It covers OnUpdate and non-repeating OnEnter without changing cached angles or the existing PlayerBody orbit hook. It follows the existing close/reset/shutdown lifecycle and works with CleanHUD or the fallback UI, including PlayerBody off. No Core changes, new scans/jobs or time-scale changes.

Voice.Say now has a temporary static readonly false gate before file discovery/loading/playback. Keep recordings, decoder and saved voice settings for restoration. Authored quest lines are silent, including the optional Talk quest adapter; other NPC/player sounds and Talk's own dialogue remain as before. No config, save-format or protocol changes. Two source files, seven added/one replaced lines, plus patch inventory and documentation.

Denis should open a native quest conversation (F or rebound key), move the mouse while hovering, scrolling and clicking choices, then close via CLOSE/Escape. The view must stay fixed while open and resume normally on close without accumulated turning. Repeat with CleanHUD off, PlayerBody off and after loading a world. Ask for work, accept/remind/turn in a recorded quest: text/rewards must work silently while other sounds remain audible. Existing singleplayer repo-only/no-deployment policy is retained.

Startup: `[Quests] Conversation mouse-look guard installed.` confirms patch installation; `[Quests] Quest voicelines temporarily disabled (recording set incomplete).` confirms voice policy initialization. Runtime checks remain Denis's. No game launch, harness, push or release.

Windows Release/Deploy=false passed with zero warnings/errors. Built/staged MD5 `8BFFC327B7E225E820A605A41BFCFF22`; unique stage `bin/Release/WildWasteland_quest-ui-voices_20261019_011657.dll`. Not installed, preserving the standing base no-deployment policy.
