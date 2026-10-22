# Wild Wasteland 0.1.27 RC24

This experimental prerelease includes the CleanHUD settings rework and menu fixes since RC23. In-game appearance, gameplay and performance verification remains Testing.

- F3 uses CleanHUD's shared panel, uppercase atlas typography for labels, values and wrapped descriptions, 54-unit rows, right-side checkbox toggles, text choices, framed key bindings and filled gradient sliders. The existing eight-color palette also appears in F3 and colors its headings, controls and borders.
- A fixed yellow-gold WILD WASTELAND entry appears immediately above SETTINGS in the title and ESC menus. It opens Game configuration and returns to the existing paused menu when closed. The menu entry stays gold when another accent is selected.
- Performance keeps its enable switch, Vanilla/Low/Balanced/Max presets, measurements and Reset counters / Write census file buttons. Everything below that preview moves into Developer: individual feature modes, shadow mismatches, feature statistics and tuning overrides. Developer still requires Enable Debug in F6 and Developer mode at the bottom of F3 Debug. Existing setting keys, defaults and saved values are preserved.
- Graphics settings have an explicit Apply Display button. The standalone-replacement restart notice stays behind language/confirmation dialogs.
- Survival and Patrols add Stew Pot and War Rig to optional Apocaspawner through one shared Core catalog dispatcher, avoiding their former direct patch conflict. Optional mods remain separate installs.

Install the full archive into `BepInEx/plugins`. All RC23 assets are retained; preserve customized external files. No new config entries or save-format changes are introduced.

## Check after installing

1. Confirm `Loading [Wild Wasteland 0.1.27]` in the BepInEx log. Open F3; the log should also show `Settings opened: CleanHUD atlas UI, 54-unit rows`.
2. Check Game, Modules and Performance: text, rows, toggles, choices, slider dragging, numeric editing, key bindings and scrolling should use the new layout. Change the accent and check headings, control fills and borders.
3. Performance should end at Reset counters / Write census file. Enable Debug in F6, then Developer mode in F3 Debug. Developer should contain all per-feature performance controls and Show tuning overrides. Turn Developer mode off and verify they disappear with that page.
4. Open WILD WASTELAND from title and ESC menus. It should sit directly above SETTINGS and remain gold with any accent. Back / Escape should return to the menu; closing from ESC must keep gameplay paused until Continue.
5. Change graphics display choices and use Apply Display. With optional Apocaspawner installed, confirm Stew Pot and War Rig remain available without a catalog patch conflict.

Verification is the Windows Release compilation and DLL deployment. Denis performs in-game verification; no issues are closed and the Apocasetter index remains unchanged.

Build validation: Windows Release compilation passed with zero errors and ten existing warnings. DLL MD5: FD023EC87B52CF24706B53EDD67DB200.
