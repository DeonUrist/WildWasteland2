# Verification

Run before every build that goes to Denis's game, and fully before any release. Record results (pass/fail, notes) in the commit message or docs/perf-log.md. 0.1.0 has never run in the game: the first run is the whole list, in order.

## A. Load
1. Game closed; DLL copied to `BepInEx\plugins\WildWasteland\WildWasteland.dll`; md5 on disk equals the build. Assets present (`theme\`, `PlayerBody\Models\`, `NPC\Models\`, `Guns\Sounds\`, `HUD\Sounds\`, `Patrols\`) or the old plugin folders still there for the fallbacks.
2. The nine absorbed standalone DLLs are out of `BepInEx\plugins\` (in `BepInEx\disabled-by-WildWasteland\`).
3. `BepInEx\LogOutput.log`:
   - `Loading [Wild Wasteland 0.1.0]`, no Harmony exceptions, no `PATCH CONFLICTS`.
   - one line `[<Id>] started (N patches)` per module (PlayerBody, NPC, Guns, HUD, Patrols) with no `, M FAILED`.
   - the startup summary, format:
     ```
     Wild Wasteland 0.1.0 modules:
       PlayerBody (Player body & animation): on, patches N
       NPC (NPC AI & navigation): on, patches N
       Guns (Guns, ammo & tracers): on, patches N
       HUD (Combat HUD): on
       Patrols (Patrols, traffic & War Rig): on, patches N
       core hooks: CreateObject.OnEnter[1/6] SendEvent.OnEnter[3/0] ... FloatingOrigin.Jump[0/0]
     ```
     Every module **`on`** (not `off by config`, `off: standalone mod present - ...`, `off: init failed - ...`). Every hook entry has `[prefixes/postfixes]` and **no `(NOT PATCHED)`** suffix.
   - First-run migration lines (`[<Id>] migrated <old cfg> [section] key = value`) on the very first start only; `SaveTypes:` lines only with VerboseLog.
   - No `job '...' threw 20 times in 10 s - job DISABLED` and no `errors in 10 s - module DISABLED` during the first 2 minutes (main menu + loading + play). The first five stack traces per module in the log are the ports' real bugs: report them.
4. Same check with each soft dependency missing (Apocasetter, Apocasaver, Dog Companion, ApocaDustStorm, MotorcycleMod, Apocaspawner, Apocapocket) and with each absorbed standalone mod put back (its module must report `off: standalone mod present - <name> <version> (<dll>)` and nothing of it runs twice).

## B. Menus
5. **F6** (Apocasetter): the "Wild Wasteland" page exists and shows the sections in bind order: `GENERAL` (Apocasetter hidden), `KEYS` (Tool window + every module key), `PLAYERBODY`, `PLAYERBODY / CLIMBING`, `PLAYERBODY / CAMERA CULLING`, `PLAYERBODY / VEHICLES`, `NPC`, `NPC / DETECTION`, `NPC / PATHFINDING`, `GUNS`, `GUNS / PROJECTILES`, `GUNS / RANGES`, `GUNS / AMMO`, `GUNS / BOSSES`, `HUD`, `PATROLS`, `PATROLS / SPAWNING`, `PATROLS / WORLD TRAFFIC`, `PATROLS / MERCHANTS`, `PATROLS / COMBAT`, `PATROLS / BALANCE`, `PATROLS / WAR RIG`, `DEBUG` (folded). `Enabled` is the first entry of every module section; key entries show CLICK TO REBIND; migrated values show CHANGED where Denis's old cfg differed from the default (F3 / F5 patrol keys, Max patrols 8, ShowNavigation is in F7, etc.).
6. **F7** opens the tool window (ledger skin, "WILD WASTELAND · TOOLS"), the game pauses (timeScale 0, cursor free), the game's input is blocked; Esc and CLOSE close it and the game resumes one frame later. Left column groups in order: Wild Wasteland, Player body & animation, NPC AI & navigation, Weapons & HUD, Patrols & vehicles, Seat & turret poses, Merchants, Debug & performance. Modules page: every module RUNNING. Performance page: jobs listed with times, none "(off)". Harmony patches page: Conflicts 0, no FAILED row.
7. Each editor opens from F7 (game loaded, standing in the world):
   - Patrols & vehicles -> Patrol editor -> OPEN: the ledger opens (F7 closed first), Esc closes it.
   - Patrols & vehicles -> Template recorder: button enabled only while seated in a vehicle; opens the recorder.
   - Seat & turret poses -> Turret spot editor: toggle on, stand near a raider car, markers appear, numpad moves the spot after F7 is closed, SAVE writes the template.
   - Seat & turret poses -> Bus passenger pose editor: toggle on near a seated bus passenger, numpad edits, SAVE / RESET.
   - Merchants -> Merchant item placement: toggle on near a merchant bus, numpad edits, SAVE layout.
   - Player body & animation -> Weapon pose editor: toggle on, third person with a weapon drawn, numpad edits, SAVE now.
   - NPC AI & navigation -> Hazards: "Place a test web where you look" / "Remove test webs".
   - Weapons & HUD -> HUD: TEST HIT / TEST HEADSHOT show the marker / number / sound.
   All editor hotkeys are None by default; binding one in F6 `[Keys]` makes it work in the game too.

## C. Lifecycle
8. New game -> play 2 min -> save -> load that save -> load a different slot -> back to main menu -> load again. No exceptions, no duplicated objects, no module running twice (check counts on the F7 pages / log).
9. **Runtime off -> on per module** (F6 or F7 Modules page), in the world, one module at a time: off = the log says `[<Id>] stopped (OffByConfig)`, the game's own behaviour returns at once (PlayerBody: vanilla arms / driver / crosshair; NPC: vanilla sensors and movement, ghosts and webs gone; Guns: bullets vanish, vanilla hits; HUD: vanilla crosshair; Patrols: no spawns, editors closed, cars stay) and no WW errors follow; on = `[<Id>] started (N patches)` again and the feature is back (PlayerBody rebuilds the body, HUD re-applies the crosshair, Patrols spawns again). Repeat off/on twice. Also retry a module that failed Init (`InitFailed`) from the toggle.
10. **Old save load**: load a save made with the standalone mods that contains patrols with crews, a motorcycle and a War Rig (Denis: SaveGame2 / 4 / 5 / 6 / 8 / 9 have Apocapatrol sidecars). Expected: motorcycles and the War Rig are restored (`Hooks.GetPrefab` resolves ids 735710202610050001 / 735710202610090001; `core hooks: ... ES3.GetPrefab[2/0]`), crews are re-seated, raider cars drive again, with VerboseLog `SaveTypes: Apocapatrol.Crew, Apocapatrol -> WildWasteland.Modules.Patrols.Crew` lines appear and `SaveTypes.Resolved > 0`; per-gun magazines (`Apocaunloader.Magazines`) are back; NPC senses sidecar restored (`config\WildWasteland\NPC\Saves\` or the old `NPCAI\Saves\`). Save again with WW and reload: identical.
11. **New save without WW**: a save made with WW (step 10's second save) loads in a game with WW removed (and the standalone mods still disabled): the world and the player load; raider crews, motorcycles and War Rigs are missing or inert - that is the documented behaviour (same as a standalone-mod save without the mods), nothing else may be broken. Put WW back: everything returns.
12. Save/load round trip keeps every module's state (patrol state, magazines, NPC knowledge, War Rig layout).

## D. World
13. Drive > 1 km in one direction (floating-origin shift): nothing WW tracks jumps, warps or loses its target (bullets in flight, floating damage numbers, NPC ghosts / waypoints / nav boxes, traffic pilots, a climb in progress).
14. Leave an area > 5 km and come back: WW handles objects destroyed by the game (no NREs, caches swept).
15. Enter a car / 3rd-person camera / exit: no item thrown out, no FSM restarted by WW. Blast lance from the car does not hit the own car.
16. HUD with Guns `Projectiles` off (F6): numbers / marker / headshot sound come from the vanilla observer; with Guns on: no double numbers.
17. Camp fight: NPCs see, search, take cover, pace bursts; tracers from NPC guns; player headshots counted once; dogs bite; a nest spawns webs.

## E. Performance (ModProfiler + F7 Performance page, see docs/perf-log.md)
S1 idle open world, S2 camp fight with 4+ NPCs, S3 driving across a tile boundary with an origin shift, S4 save load (patrols + bike + War Rig), S5 convoy / faction battle with many vehicles. Compare against the same scenario with the equivalent standalone mods. Fill the 0.1.0 table in docs/perf-log.md.
