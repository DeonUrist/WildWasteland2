# Map controls, hiring and dialogue patch (Testing)

Approved scope: Core HUD controls, CleanHUD location/map rendering and Patrols route rendering, plus the user's follow-up hiring, compact gold answers and quest notification requests. Denis subsequently requested commit, push and release as 0.1.8 RC5.

Cause of failed hiring: installed Quests DLL md5 589c5185d0825ae0a7dd1601b55d6054 matches reviewed 1.8.0. The log records `Follower hook failed: Mount`, followed by QuestsCompat InitFailed. The actual method is TryMount(GameObject). Correct that hook so provider-based MissingMods, Inform, follower ownership, animation/reload and passenger type/target hooks remain installed. Avoid a misleading missing-field warning for the Active property.

Location classification uses the existing mapIcon discovery and root names: white cave silhouette for Cave_*, separate white wreck silhouette for Wreck_*, camp icon red for Scrapyard and green for Coyotes (native Camp_10/15/16, matching Patrols' classification). Map positions use ground-level roots, not elevated icons. The shared compass retains its 1 km location cap; the map displays discovered loaded locations. Native icon render output is hidden/restored through its captured forceRenderingOff flag; native objects, FSMs and colors remain intact for exploration/save logic. Icon textures are cached at module startup and disposed on shutdown.

F3 Game/HUD order: Compass Off/On above Convoys on compass, Locations on compass, Map Off/On, Map markers Off/Ambiguous/On, Map Routes Off/Ambiguous/On. Compass Off hides direction strip and all markers. Map Off closes/disables native MapCheat. Marker Off hides replacement/native symbols; Ambiguous shows white question marks without labels, On shows real symbols/colors. Map icons have no name labels in any mode. Routes Off hides traffic overlay; Ambiguous shows gray Unknown routes/groups, On shows faction colors/full names. Existing Patrols route settings remain the fallback when CleanHUD is disabled. New settings.cfg entries: Compass=true, Map Enabled=true, Markers=On, Routes=On; Routes seeds Off/Ambiguous from prior DebugMap/hide route type values when no new saved value exists.

Dialogue answers use 20 px gold/yellow glyphs, 24 px line height, 36 px minimum rows and 4 px gaps; chat stays at its current size. Quest AddToast notifications enter the existing CleanHUD log, including notifications accepted while talking (shown as the HUD becomes visible). CleanHUD off retains source notifications/window.

Denis verifies after restart:

1. Startup has `[QuestsCompat] Follower bridge ready` and no Mount/TryMount binding failure or InitFailed. Talk to an eligible NPC with enough reputation: hiring must no longer request separate NPCAI/Convoys. Walk/run/stop, fight, exhaust a magazine, board/shoot/exit a car and dismiss. Following remains Quests-owned; weapon/shot/reload animations and passenger targets use WW providers. Look for `Follower animation attached`.
2. F3 Game/HUD: verify control order. Toggle Compass Off/On: whole strip and markers disappear/return. Map Off prevents M opening the native map; On restores access. Existing settings save across restart.
3. Near cave, wreck, Scrapyard camp and Coyote camp, compare M-map and compass: distinct icons, white/white/red/green. Compass locations remain capped at 1 km. Map markers Off hides all symbols, Ambiguous shows ? without identity/color leakage, On restores icons. Native symbols must not double underneath. CleanHUD disable restores original map icons.
4. Map Routes: Off removes route overlay; Ambiguous has gray Unknown groups/routes; On restores colored routes and full names. Marker mode and route mode work independently. ESC shows no overlay.
5. Dialogue: shorter gold answer rows, wrapped long options, working AQ quest buttons and scrolling. Accept/finish/fail a quest: its notification appears in the same CleanHUD log as native messages, with no centered duplicate. Disable CleanHUD: original window/toasts return.

6. With multiple active quests, verify objective/distance/time text uses CleanHUD's font, warm white text, gold accent and shadow in the top right. Each quest has a separate stacked row; long descriptions wrap. Enable Insanity and raise it above zero: quests move below the icon; at zero or with Insanity disabled, they move up. ShowTracker off hides them; CleanHUD off restores the original tracker. No duplicate tracker appears.

Agent verification: Windows Release/Deploy build only; no game automation or runtime harness. Performance/gameplay remain Testing.
