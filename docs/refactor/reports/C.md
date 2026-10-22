# Wave C — 2.0 transition handover

Date: 2026-10-23. Status: source integration and clean installation complete; gameplay acceptance remains Testing with Denis.

## Completed implementation

- Canonical module namespaces and ownership; no module-private cross-owner partial implementation.
- Actual typed Core providers replace obsolete Services/event forwarders. Player and humanoid NPC input/plans share the character driver. Compatibility owns external discovery; HUD owns external dialogue presentation.
- Removed unused wrappers and the CS0618 warning suppression. Persistent world records, descriptions and thumbnails remain inside the selected ES3 transaction. No Apocasaver dependency or legacy-save import.
- Recovered 128 missing external payloads into canonical source owners from the hash-verified 1.0.9 archive, preserving current authored sources and removing 17 duplicate profile copies. See state/asset-transition.json. Missing climbing and headshot payloads were packaging omissions, not harmless startup errors.
- Self-contained packaging now consumes the build asset index. No previous release archive is needed to build/package 2.0. Current module/install documentation replaces the old 1.x overview; historical provenance remains archived.

This broad transition was already underway before the final clean-setup request. It spans roughly 487 tracked changed files plus new source/assets; most source edits retire obsolete callers and normalize ownership. Changes remain local and uncommitted. No push, tag or release publication.

## Final build and installation

Windows command: `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`.

Result: **0 warnings, 0 errors**. No game or harness launched. Both exact Apocasetter disabled manifests were absent before deployment; the game was closed.

Installed DLL: `E:\SteamLibrary\steamapps\common\Apocalypter\BepInEx\plugins\WildWasteland\WildWasteland.dll`.

MD5: `D1ABF39E5587707D797ADB1422A5A24D`.

**535 indexed runtime assets** copied into the fresh plugin folder, with every installed payload byte matching the build. Installed index and DLL also match. All seven animation/weather bundles have UnityFS headers. Required audio, 18 loading images and the weather license are included. This checks installation completeness, not Unity runtime playback.

Local archive: `V:\Repos\Apocalypter\_releases\WildWasteland-2.0.0_20261023_011653.zip` (538 entries, 211546011 bytes). SHA256: `2e73eb72cf046e176eeb235deaf70aae6832592b045939a51af8b91be78ab150`. Package CRC and all payloads round-tripped successfully; it contains a single main DLL and no optional third-party DLLs.

## Requested clean setup

Archived 75 top-level entries, including the previous plugin installation, WildWasteland configuration, historical CleanHUD data, native save settings, existing native/WW/MP saves, old metadata sidecars and recovery folders. Backup: `V:\Repos\Apocalypter\_releases\WW2-clean-setup-20261023_011559`.

The backup contains reset-manifest.json and completion.json. Active saves/settings were cleared by moving them outside the game/save/config roots. WW and native file-based settings regenerate with defaults on startup. Other plugins' configuration and DLLs were preserved; Windows registry/player preferences were not edited. No manually populated replacement configuration or empty fabricated saves were created.

## Denis's acceptance

1. Launch the game normally. Confirm Wild Wasteland 2.0.0 loads and there are no missing climbing-bundle or headshot/sound-file errors.
2. Confirm the save list is empty and start a new game; check that WW options show defaults.
3. Exercise climbing/body animation and sound feedback, then save, return to menu and reload. The saved world and WW state should restore together.

The previous reported game load is encouraging but is not acceptance of this final binary. Runtime hook inventory regeneration and combined gameplay acceptance remain with Denis. The historical XML-summary audit was not expanded into a comment-rewrite pass; the final verification followed the current build-only instructions. No issue was closed or promoted to Verified.
