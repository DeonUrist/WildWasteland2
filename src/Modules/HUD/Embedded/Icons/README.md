# Map and compass icons

Install this folder as `BepInEx/plugins/WildWasteland/CleanHUD/Icons/`.
These PNG files are loaded from disk, not embedded in WildWasteland.dll.

Replace `bolt.png` to change the bolts currency icon above the health/armor panel. A 128x128 transparent PNG is suitable; it displays at 26x26 HUD units, tinted gold with a black outline. Restart after replacing it.

Replace `building.png`, `other.png`, `camp.png`, `cave.png`, `wreck.png` or `unknown.png` to change location/POI icons on both map and compass. `Building_*` uses `building.png`; unclassified locations use `other.png`. `diamond.png` remains the generic compass API fallback. `player.png` is the map player chevron. Keep the filenames; restart the game after editing. Module off/on also reloads the images.

Location-specific PNGs override those category defaults: `Camp_13_radio_tower.png` matches `Camp_13`, for example. An exact `Camp_13.png` takes priority if present. Map and compass show the specific icon when obfuscation is off, or after discovery when obfuscation is on; undiscovered obfuscated locations keep `unknown.png`. Off settings still hide markers.

Use white silhouettes on a transparent PNG background: the game applies faction/marker colors. Higher-resolution PNGs are accepted; draw size is controlled by the UI. The player chevron stays 18 x 21 screen pixels, with a 22 x 25 outline.

Updates should copy missing defaults only and preserve customized files. Ship this folder alongside the DLL in release archives.
