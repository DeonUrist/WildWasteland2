# Wild Wasteland 2 — 2.0.10

Yard Hounds use their native animal butchering again, regardless of the Cannibal perk. Their Scrapyard faction tag previously included them in the human-corpse list: without Cannibal, butchering was blocked; with it, they produced human loot. Corpses with native butchering now remain on that path.

Cooked food retains its nutrition tooltip when a Survival cooking bonus is applied. The replacement tooltip previously put the text into a PlayMaker variable's name instead of its displayed value, leaving the tooltip blank. This corrects the displayed text without changing the nutrition bonus.

No configuration entries or save formats change. Existing cooked items carrying a saved cooking bonus regain their nutrition tooltip when loaded.

Install the complete `WildWasteland2` folder from the ZIP into `BepInEx/plugins/`, retaining all included assets and `asset-index.tsv`. BepInEx 5 is required. Existing WW2 saves and settings remain supported. For manual updates from WW2 2.0.5 or earlier, follow the folder-transition instructions in the README and [2.0.6 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.6). Use only one Wild Wasteland variant and disable absorbed standalone mods as described in the README.

In-game acceptance remains **Testing**. Butcher a Yard Hound using a knife, machete or shiv, with and without Cannibal: expect three raw dog meats and no human loot. With Survival skill above zero, grab raw meat, cook it, then hover repeatedly and after save/reload: expect the nutrition tooltip to remain visible with the cooking bonus. No dedicated success log lines were added.

Validation: Windows Release build/deploy passed with zero warnings and errors. Complete-package CRC and byte-for-byte verification passed: 597 entries, including 593 runtime assets, the DLL, icon and README.

Installed/package DLL MD5: `B561611A62F6B5E805D0EDE0E43668EC`

Archive: `WildWasteland2-2.0.10.zip` (230,155,442 bytes)

Archive SHA256: `4ab0111afa2e441c5becf4d01a01fe5b6219a4e2c17fb3bfde9c86e0d9524f5c`
