# Wild Wasteland 2 — 2.0.7

Crawling in first person now uses the same hand visibility checks as standing and crouching. When native weapon hands are visible, the body's crawling hands are hidden, preventing two sets of hands from appearing together. Empty-handed crawling still shows the crawling animation.

The crawl condition previously bypassed the native-arm visibility check. Two lines in Presentation's `BodyRunner.cs` now route crawling through the existing check, retaining the tool-hand exception and traversal behavior. No configuration entries or save formats change.

Install the complete `WildWasteland2` folder from the ZIP into `BepInEx/plugins/`, retaining all included assets and `asset-index.tsv`. BepInEx 5 is required. Existing WW2 saves and settings remain supported. For manual updates from WW2 2.0.5 or earlier, follow the folder-transition instructions in the README and [2.0.6 release notes](https://github.com/DeonUrist/WildWasteland2/releases/tag/v2.0.6). Use only one Wild Wasteland variant and disable absorbed standalone mods as described in the README.

Validation: Windows Release build/deploy passed with zero warnings and errors. In-game acceptance remains Testing. No new diagnostic log lines were added.

In-game test: enter first-person crawl with weapon hands visible; expect only the weapon hands. With empty hands, expect the crawling hands. Check transitions between crawling, crouching and standing for duplicate or missing hands.

Installed/package DLL MD5: `73406FF7A953639D71FFAC67024871BD`

Complete-package CRC and byte-for-byte verification passed: 597 entries, including 593 runtime assets, the DLL, icon and README.

Archive: `WildWasteland2-2.0.7.zip` (230,151,955 bytes)

Archive SHA256: `a74d90f821e2bff952aaa437e5ac37fe0a5df6550362a2f31e82b7c03cfb5517`
