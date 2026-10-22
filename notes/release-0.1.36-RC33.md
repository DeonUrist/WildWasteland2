# Wild Wasteland 0.1.36 RC33

Experimental prerelease containing all pending changes since RC32. In-game acceptance and performance comparison remain Testing.

## Steering-wheel hands

- Hands fit the actual wheel size and follow its rotation. Ten calibrated native wheel profiles are included as defaults; existing local profiles stay intact.
- Repeated hand-over-hand turns cover multiple wheel revolutions. Regrips use slower, deliberate transitions. Releasing steering returns the hands to their side grips while the wheel straightens.
- Each wheel uses its own right-hand SIDE/TOP setup mirrored onto the left, including shoulder, elbow and wrist adjustments. Editing either side updates the pair. Arm reach retains a small elbow bend without stretching the bones. The previously reported left-turn jerk still needs an in-game retest.
- The live editor has separate SIDE/TOP poses, hand/shoulder/elbow targets, preview, save and cross-wheel copy/paste. Enable Debug and Developer mode, sit in a car, then press Numpad decimal. Numpad 0 selects a target, 5 switches hand move/rotate, 9 selects SIDE/TOP and 3 switches preview/live. Use 8/2, 6/4 and 7/1 to adjust, Shift for fine steps, Enter to save, / to copy and * to paste. Changes also save on key release.
- Taking a tool hides the extra right first-person body arm, including while seated. The full third-person body stays visible. Entering a car centres the view forward once, retaining normal looking afterwards.

## Quests and hiring included

- Apocalypter Quests 1.8.2 now runs inside Wild Wasteland: authored/generated jobs, deliveries, factions, reputation, takeovers, voices and hired followers. The 76 supplied voice recordings credit LostSilver666 and Soldier96.
- Disable standalone Apocalypter Quests before playing. Apocasetter can queue its disable and request a restart. Apocatalker remains optional for its stories and dialogue; Quests works without it.
- Aim at a friendly living human within 4 metres and press F to talk, ask for work, turn in a quest or hire. J opens the journal; Delete dismisses a follower. Keys are rebindable.
- CleanHUD presents conversations, journal, tracker and notifications, with matching quest flag colors. Player speaker prefixes follow the UI accent; NPC names are gray and dialogue text stays white. Fallback quest windows remain available with Clean HUD off.
- Existing Quests settings, custom text and voices migrate only where WW has no value/file. Original AQ1/ES3 quest records and fallback backups remain supported. Current/former vehicle crew cannot offer new jobs; on-foot crew remain hireable. Ready empty delivery camps can change the objective to returning the goods after 15 active-play seconds.

## HUD and saving

- Manual saves show the loading artwork and progress bar with SAVING. Menu clicks and Escape are blocked until the save finishes, including an active Apocasaver transaction. Autosaves retain AUTOSAVING and Apocasaver's own completion timing.
- With optional World Amp installed, overheating power-cut and engine-seizure messages use CleanHUD's message log. Temperature remains on the vehicle gauges; World Amp's simulation and settings remain unchanged.

## Installation and testing

Extract the complete ZIP into `BepInEx/plugins`, preserving customized assets. It contains the full RC32 asset set plus the updated plugin and notes. Wheel defaults, quest defaults and voice recordings are embedded and fill missing files on use. No personal config or save is bundled.

New live WW bindings include the Quests module switch, its source settings and F/J/Delete actions; missing settings import individually. Existing user settings win. Wheel controls add no config entries and retain the existing per-wheel profile format.

For the steering retest, turn right and left from centre in each wheel model, continue through multiple revolutions, then release: mirrored alternating grips should stay on the rim and settle at the sides, without the earlier left-elbow onset jump. Check a tool in first and third person, then edit SIDE/TOP elbow positions and reload the wheel profile.

For Quests, check `[Quests] Native Quests 1.8.2 ready:` and `[Quests] World ready:` in the log, accept/turn in work, hire/dismiss, and save/load an existing quest slot. Repeat with Talk absent and optionally present. Test manual save, save-and-quit and autosave: one presentation, menus locked during saving and restored afterwards. Detailed checks: [wheel guide](WW_CAR_ARMS_GUIDE.md) and [Quests port report](port-Quests.md).

Windows Release build and deployment passed with zero warnings and zero errors. Built, staged and installed DLL MD5: `F165942C49354E5A5D89E595776802D4`. No game launch or runtime test was performed by the agent.
