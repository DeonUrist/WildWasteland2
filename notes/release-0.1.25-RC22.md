# Wild Wasteland 0.1.25 RC22

CleanHUD title and ESC menus gain an adjacent LANGUAGE picker and eight accent choices. This RC includes the latest label, alignment, color-order and screen-edge spacing fixes.

- With ApocaLanguage loaded and its ShowLanguageButton setting enabled, LANGUAGE opens a CleanHUD panel beside the main menu, aligned to its top edge. Duplicate native corner buttons stay hidden. English language names use the same font as the main menu, fixing the invisible labels caused by using a file-loaded font in legacy Text. Choosing a language calls ApocaLanguage's original SelectLanguage method. ApocaLanguage remains optional and is not included.
- Eight small swatches sit in one frame at screen bottom-centre, raised clear of the screen edge. Order: White, Gray, Red, Yellow, Green, Mint, Blue, Purple. Green has a stronger phosphor tint; Mint is sampled from the supplied crosshair image (#00FF91).
- Accent changes apply to CleanHUD's yellow text and gold/beige borders, including the HUD, inventory, dialogue, journal, map frame and styled save-name prompt. The three owned embedded panel textures recolor in a 500 microsecond scheduler slice. Item textures, loading backgrounds, swatch colors and semantic warning/quest colors keep their own colors.
- The choice is saved in `BepInEx/config/WildWasteland/settings.cfg`, `[CleanHUD / Menu] Accent`, default Yellow. Existing settings and save formats are retained.
- The redundant Apocasetter corner MODS button is hidden while CleanHUD owns the menu, including its separate persistent fallback canvas. The menu-list MODS action still opens the original window; turning Clean menus off restores native buttons. Named button discovery also works after translation.
- Distance travelled on the ESC menu uses brighter, slightly larger text with a stronger shadow and a thin accent-colored bar, matching the quest readout style.
- The full asset package carries forward RC21's models, climbing/dog bundles, sounds, UI theme, icons, loading screens and Patrols assets. If WW was disabled in Apocasetter, enable it with the full plugin folder restored before deploying a DLL alone. This local install's missing assets were recovered from its disabled cache during preparation of the menu changes.

All previous RC21 features remain included, including optional Engine Sim 1.7.0 compatibility. Engine Sim and ApocaLanguage DLLs/assets are not redistributed. Preserve customized external PNGs when updating; extract the release into `BepInEx/plugins`.

Feature status remains Testing. No issues are closed and no Apocasetter index registration is changed.

## Check after installing

1. Restart and confirm `Loading [Wild Wasteland 0.1.25]`, the expected module summary and no disabled jobs or failed hooks.
2. Open LANGUAGE on both title and ESC menus. Every English language name should be visible in the main menu font, the panel tops should align, and no native right-edge dropdown should appear. Select a language; ApocaLanguage logs `Language: <code>`. Escape/X should close the picker without resuming gameplay.
3. Check all eight swatches, their order and the bottom margin at the usual resolution. Select Mint, White and Gray, then inspect menu text, the gameplay HUD, inventory and map borders. Restart to confirm the accent persists. Choose Yellow to restore the original accent.
4. With ApocaLanguage absent, LANGUAGE should be absent and other menu actions should work normally. With Clean menus off, native menus and ApocaLanguage's own language button should return.
5. Confirm the restored climbing bundle loads (`Integrated climbing ready`), the menu background/icons/sounds are present, then repeat the usual save/load and optional Engine Sim checks.
6. On title and ESC screens, confirm MODS appears only in the menu list and still opens Apocasetter. Turn Clean menus off/on to check native-button restoration. Travel, open ESC, and check the distance readout and its accent bar; change accents and confirm the bar follows the selected color.

Validation: Windows Release build with Deploy=true passed with zero errors and eight existing warnings. Built/deployed DLL MD5: A7BBA1BCC59086F5BC5461AF0AAED788. In-game verification is performed by Denis.
