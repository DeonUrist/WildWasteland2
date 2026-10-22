# Tutorial and Codex panels — pending RC32 test

Denis reports the native Tutorial and Codex cannot be opened because WW UI remains above them, and requests owned UI retaining their content plus removal of the startup tutorial suggestion.

## Cause and native behavior

Menu.BuildMain previously sent Clicked to the two native button FSMs without changing its own main screen. Menu.Tick keeps the replacement root active while MainMenu_Canvas is enabled; its canvas sorts at least 50 and above the native menu. The source buttons enable Tutorial_Canvas/Codex_Canvas and deactivate only the native Continue button, so WW's own main menu and background remain over the new content.

- `Sources/fsm/scene1_game/MainMenu_Canvas.md:23` and `:226` describe both native open actions.
- `Tutorial_Canvas.md:22` describes Back/Cancel, disabling the native canvas, restoring Continue and deactivating all seven pages. Page actions cycle the original seven pages.
- `Codex_Canvas.md:323` describes the corresponding close behavior for its 15 pages; `:164` describes the native CodexContent discovery bool, placeholder image, found texture and revealed name. These FSMs retain ownership of discovery/unlock state and the existing CODEX save key.
- `Sources/data/hierarchy/scene1_game/Tutorial_Canvas.txt` contains the original heading/description Text components, native VideoPlayers with RenderTexture-backed RawImages, and static player-sheet, GodHands, G29 and character illustrations. Borrow those rather than cloning players or loading replacement videos.
- `__GameManager__.md:217` describes BeginnerTutorial's three-second wait, TutorialImage banner and TutorialArrow, plus its persisted TutorialBool. Removing presentation must not write that saved flag or skip content.

## Change

Menu.cs is now partial. Its Tutorial/Codex rows open MenuReference.cs panels, switch the owned screen away from main, and hide the native source canvas with Menu's reversible CanvasGroup capture. Native buttons still open, page and close the source screens; their videos and discovery FSMs keep running. Native references and text/images are cached for the current page; a 0.2 s check refreshes the owned body only when page/content/unlock/font inputs change. Video textures update naturally, without readbacks or new VideoPlayers.

Tutorial shows both original topics and illustrations/videos, with wrapped atlas text and a scrollable body. Codex shows the original category title and cards; undiscovered entries retain native placeholder pictures and hidden names. Previous/Next use the current native page's button FSM, preserving its wrap-around order. Back/Escape invokes native Back and returns to the paused menu without resuming. The settings page's existing Cancel guard also protects these screens. Native canvases opened externally are adopted; a short open grace period follows the existing native-screen pattern.

Only owned UI objects are destroyed. Source images/textures, render textures and VideoPlayers are borrowed and never disposed, moved or rewritten. Disabling Clean menus restores the native screen at its current page; re-enabling adopts it again. Scene/module teardown clears the owned references and restores captured groups. The pause status row yields to the reference-screen footer.

The exact startup TutorialImage and TutorialArrow use the same reversible hiding mechanism, including while WW settings or loading owns input. BeginnerTutorial still runs; TutorialBool, native tutorial content, Codex discoveries and save formats remain unchanged. No new config entry, Harmony patch, scheduler job or per-frame scene scan.

## Build and Denis's checks

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`: zero warnings/errors, DLL deployed. Built/installed/staged MD5 `8A81F3E20BFF47686FA286C3A115A2D1`, version remains 0.1.34. Unique artifact `_releases/WildWasteland_0.1.34_RC32-test-reference-c.dll`. Approximately 220 lines across Menu.cs and new MenuReference.cs, plus this note/AGENTS. Prior pending log/War Rig/flashlight fixes remain included. No game launch/automation; UI behavior remains Testing.

1. Restart, load or start a world, wait beyond three seconds: no startup tutorial image or arrow, including while the first-run WW settings panel is open.
2. ESC > TUTORIAL: the new panel replaces the menu rows. Cycle all seven pages forward/back, check videos play and all text/static pictures remain available via wheel/scrollbar. The final Next wraps to page one.
3. Back/Escape returns to ESC's main screen while still paused; a second Escape resumes. Reopen repeatedly without overlapping panels or stray native controls.
4. ESC > CODEX: cycle all 15 native pages, checking known pictures/names and unknown placeholders. Scroll both rows, return to gameplay and discover something, then reopen and check its native unlock appears. Existing progress survives load.
5. Switch accent/CleanFont and reopen. Toggle Clean menus off with a reference screen open: the native screen returns at that page; on restores the new panel. Check another save/new world does not inherit entries from this UI.

Optional WW global VerboseLog confirms `Menu: opened native Tutorial in CleanHUD (7 pages)` or `Menu: opened native Codex in CleanHUD (15 pages)`. Leave it off for the quiet-log test. No commit/push/release until Denis accepts the combined test build; then publish RC32 using RC31's description as requested.

## Fit correction after Denis's test

Denis confirms both panels open but reports pictures/videos requiring scrolling. The Codex's two rows occupied 726 units in a 500-unit viewport. MenuReference.cs now sizes its rows to the viewport (236-unit cards for the native two-row pages, 160-unit maximum picture height, with measured caption space). Both rows occupy 492 units, and pictures retain their aspect ratio. Scroll input and the scrollbar are disabled while the body fits.

Tutorial's panel grows from 700 to 960 units high, starting at y=60 and ending at 1020 in the 1080-unit layout. It replaces the large APOCALYPTER heading while open, restoring the heading on Back. Its content viewport grows from 500 to 760; each video/picture is capped by the remaining space below the unchanged wrapped text. Footer navigation remains inside the frame. Native content, paging, videos, unlocks and save/config behavior are unchanged. This is a layout correction in MenuReference.cs only, approximately 35 changed lines.

Windows Release/Deploy=true passed with zero warnings/errors. Latest built/installed/staged MD5 `E7BAF0F73053331C90517FEF5201D81A`, still version 0.1.34; artifact `_releases/WildWasteland_0.1.34_RC32-test-reference-fit-d.dll` supersedes the earlier hashes. No game was launched. Restart and cycle all 15 Codex and seven Tutorial pages: both Codex rows and all Tutorial text/videos should fit without scrolling, with correct aspect ratios and accessible Back/Previous/Next. Check both fonts and that Back restores the normal menu heading. No new log lines; the existing optional page-open lines still apply. RC32 is not released pending Denis's acceptance.
