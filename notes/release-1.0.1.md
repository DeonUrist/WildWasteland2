# Wild Wasteland 1.0.1

Fog of war on the map now defaults to **1.0 opacity**, fully hiding unexplored terrain while retaining the soft reveal edge. The 1.0 tuning capture accidentally supplied 0.463468 even though CleanHUD's code fallback was already1.0.

The embedded defaults and readable tuning reference are corrected. Existing saved/legacy-migrated values equal to that exact unintended default upgrade to1.0 when settings bind; other customized opacity values remain. No new config key, exploration data change or live settings/save edits. Version and README updated; all other1.0 runtime assets retained.

Windows Release/Deploy=false build passed with zero warnings/errors. Built/staged/packaged DLL MD5: `053D2F60484D27487B809CA02FEA8807`; unique stage `V:/Repos/Apocalypter/_releases/WildWasteland_1.0.1a.dll`.

Archive `WildWasteland-1.0.1.zip`: 218,473,207 bytes,198 files; SHA256 `936d68b11191849c70652fce1edf7b6017afede8c6d4ce31100797ee53e7cb8f`. Contains runtime assets/plugin and required weather license notice, with no documentation, personal configs or saves. All177 retained runtime asset entries and18 loading images verified by the package tool.

Denis checks an unexplored area: terrain should be fully hidden, explored terrain and soft-edge geometry unchanged. Restart with the old0.463468 value and expect1; a different customized opacity should remain. Existing exploration should survive save/reload. No new diagnostics. No game execution/harness or standard-SP deployment; status remains Testing.

Official Apocasetter-Index registration/deprecations were approved and pushed as831bfce; the generated catalog validated the WW1.0 ZIP. After publishing1.0.1, refresh the generated Index and confirm it offers1.0.1 with the matching archive SHA256. Unrelated pending Index icon support and SP wiki-tool work are excluded from this release.
