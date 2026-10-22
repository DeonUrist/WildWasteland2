# Talk / Quests compatibility - 0.1.7 RC4

Release update: Denis requested push/release after the initial local compatibility build against 0.1.6 RC3. The published candidate is 0.1.7 RC4, with the same compatibility implementation and a numeric version bump. All gameplay/performance status remains Testing.

Status: Testing. User chose an optional compatibility bridge for the installed mods instead of the full port in plan-Talk-Quests.md. Latest GitHub release checked on 2026-10-15: v0.1.6, Wild Wasteland 0.1.6 RC3. Working HEAD: 9223808. No external DLL/source edits, commit, push or release publication.

## Required standalone mods

Keep Apocatalker 2.0.0 (`com.example.apocalypter.talk`) and Apocalypter Quests 1.8.0 (`local.apocalypterquests`) installed, including their original adjacent dialogue/quest/faction/voice/theme data. The inspected copies are in ModDev/ModCheck; neither DLL was found under BepInEx/plugins during implementation. This bridge does not install them. Other versions fail the compatibility startup check instead of applying unknown private bindings. Without either mod, the bridge is idle.

Reviewed DLL md5: Talk `518f44ad8318d62bdcb1aff739291e93`; Quests `589c5185d0825ae0a7dd1601b55d6054`. Decompiled sources are read-only research outside this repo in V:/Repos/Apocalypter/_tools/decompiled/.

## Six accepted decisions

1. Preserve ES3 key `ApocalypterQuests`, serialization and original fallback-backup behaviour. Invoke original SaveBook through SaveBus.AfterSave after the native flush, guarded against slot/session changes. No new quest save format or migration.
2. Preserve Quests hiring/following/seat logic. Route Inform and idle exclusion through Services.Npc. While hired, its brain yields steering and body control; Quests ammo/reload deadlines gate native fire. WW's animation API supplies weapon poses and shot/reload clips. The seated pose retains its leg/spine placement while the animation API controls arms. Patrols' passenger guard uses the explicit Quests target and reload gate. Original direct-damage fallbacks in Quests are retained.
3. Native map flags and compass distances without Immersive Map. Turn-in markers are green diamonds, follow a live giver and otherwise retain the source fallback position. Keep original Immersive Map support in parallel when installed. Positions retain floating-origin anchoring.
4. CleanHUD dialogue uses the existing smoked menu plate and GlyphText, wrapped chat/choice lines, scrolling and original source-button callbacks (including AQ quest buttons). The native model stays alive but transparent. CleanHUD HUD off or module off restores the source window. Original talk hint remains provided by Talk.
5. Preserve Talk's default shared `BepInEx/config/ApocalypterTalk.story.txt` and optional manual `ApocalypterTalk.story.<StoryProfile>.txt`. Quests continues setting its original flags through TalkApi. No automatic slot separation.
6. Keep numeric Plugin.VERSION 0.1.6, targeting current RC3. This is a local compatibility build, not a published release.

## Scheduling and configuration

New settings.cfg entry: `[QuestsCompat] Enabled`, default true, bound by ModuleHost. No copied Talk/Quests config entries; all original settings/data remain authoritative. No csproj, dependencies, compiler or SDK changes.

QuestsCompat jobs: Talk frame, Talk cursor, Quest frame, Quest poses, Quest windows, Quest logic (0.5 s), World cache (500 us slice), Marker providers (0.5 s). CleanHUD jobs: Dialogue window, Quest map markers, Quest map lookup (1 s). Source Unity callback prefixes admit only scheduler invocations. The original embedded Tick and SaveLoadGame watcher are suppressed. Full-FsmList item/people scans use a published, sliced candidate cache; changing quantities, health and positions remain read live. Original Talk input hooks, Quests death hooks and optional Immersive Map hooks remain native-owned.

This makes work attributable to WW and slices candidate discovery; it does not hard-preempt Hire.Update, Takeover.Update, Unity rendering, voice decoding, spawning or the source mod's remaining work inside a callback. No measured performance claims: Denis must profile in game. See docs/harmony.md for every compatibility target.

The bridge also retains Talk's persistent dialogue model and repairs plugin-instance null checks inside the source hooks/API. This follows the repo's documented native destruction of BepInEx plugin components on scene loads. World-object null checks keep their Unity behaviour. If the source components have already been destroyed, disabling the compatibility module cannot restore their native Unity callbacks; restart the game to run those mods fully standalone. The closed source dialogue model remains available for re-enabling compatibility.

## In-game verification - Denis

1. With the game closed, install both original folders (DLL + data) under BepInEx/plugins. Launch with the deployed WW DLL. Confirm log lines `[QuestsCompat] Apocatalker 2.0.0 scheduled`, `Apocalypter Quests 1.8.0 scheduled`, `Follower bridge ready` and `[CleanHUD] Apocatalker dialogue bridge ready`. There must be no compatibility hook failure or InitFailed. No installed mods: expect `Talk/Quests not loaded; compatibility idle`.
2. Load a save, approach an NPC and press the configured Talk key (default F). Confirm the CleanHUD dialogue, readable long replies, scrolling choices and working quest/AQ buttons. CLOSE and Escape must restore cursor, movement and time. Repeat with PauseWhileTalking=false; world time should keep moving. WW settings must not open over a conversation.
3. Accept a goto/clear/takeover quest with a position. Check a gold flag on both M map and compass, with compass distance. Complete its objective: returning quests show a green diamond at the giver. Walk/drive over 1 km: marker positions stay correct. Turn in/abandon: markers disappear.
4. Fetch items, verify live stack counts and hand-in/rewards. Kill a target, verify its kill/clear objective still advances through the standalone death hook. Optional Immersive Map installed: its original quest markers remain present too.
5. Hire a ranged NPC (e.g. Bolter/Pistoleer). Walk, run and stop; it follows with WW weapon animations and does not return to its camp. Fight an enemy and exhaust a magazine: verify WW firing/reload clips, audible reload, firing held until reload finishes and continued following afterward. Look for `[QuestsCompat] Follower animation attached: ...`.
6. Enter a car, wait for it to board, and drive near an enemy within passenger range/arc. Verify seated legs, aiming/firing arms, shots at the enemy, reload hold, no shot at the player/car, and normal exit/following. Dismiss the follower: original NPC control returns. Test melee followers as well (their native attack animation must remain available).
7. Save, wait at least 2 seconds, reload: quests, reputation, takeover and hire data remain. Source log should contain `Saved ... quests (slot ..., ... game save ok ...)`. Load another slot and back. Story profiles must behave exactly as the original mod, including shared default flags. Check autosave too if Apocasaver is enabled.
8. Toggle CleanHUD HUD off while talking: the original Talk window should appear with working choices, then switch back when enabled. Disable/re-enable the CleanHUD module: map/compass markers restore. Toggle QuestsCompat off/on in Modules: no duplicate updates/markers, and quest progress remains. Native callbacks return if their components are still alive; otherwise restart to compare standalone behaviour, as described above.
9. Profile idle world, five active quests, takeover walkers and a hired passenger. Compare whole-game ModProfiler capture with compatibility off/on. F3 Modules/Performance should list the jobs above; verify World cache remains sliced, and report worst/average Quest frame/Quest logic costs. Do not mark Verified until these in-game checks pass.

Build verification is the Windows Release deploy build only; no game launch, mocks, simulations or runtime harnesses were run by the agent.
