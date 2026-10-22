# Wild Wasteland 0.1.5 RC2

Release candidate 2 adds vehicle-group and nearby-location markers to the CleanHUD compass. Numeric BepInEx version **0.1.5**; tag **v0.1.5**. All RC1 features, reviewed defaults and external assets are included.

- Vehicle groups show one car silhouette and distance per group. **Merchants yellow, Coyotes green (including Coyote convoys), patrols red, other convoys orange.** Traffic members and merchant escorts do not create duplicate group markers. Physical groups follow their leader/truck; virtual traffic uses the stable world coordinates. Parked/manual merchants and editor-spawned groups are supported.
- Native locations show a camp silhouette and distance **within 1 km**. New terrain queues discovery through the existing sliced job, and targets follow floating-origin shifts.
- **SETTINGS → Game → HUD** adds separate **Convoys on compass** and **Locations on compass (1 km)** switches, both **On by default**. New settings.cfg entries under `CleanHUD / HUD`: `ShowConvoys` and `ShowLocations`. Convoys covers all four vehicle categories. Existing settings and toggle-aiming defaults are preserved; F6 is unchanged.
- Marker reconciliation uses two named 0.5 s scheduler jobs and cached vehicle/location records. Terrain discovery shares the existing 500 microsecond slice; icons and distance text use the existing compass API/pools. No new per-frame scene scans, texture decodes, Harmony targets or save keys.

Validation: Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with **0 errors** (7 existing warnings). Built/installed/packaged DLL MD5: **E5977630DF9C1D676157FABF3DDC7245**. In-game behavior and profiling remain pending with Denis; feature statuses remain Testing.

Install: unzip `WildWasteland-0.1.5-RC2.zip` into `BepInEx\plugins`, creating `WildWasteland\`. Includes the DLL, all RC1 assets, five loading screens with editable `screens.json`, README, the RC1 default audit and RC2 compass test steps. Keep configuration/data files when updating. Disable/remove the absorbed standalone mods listed in README; standalone CleanHUD otherwise prevents the built-in module from starting.

Test: launch and confirm `Loading [Wild Wasteland 0.1.5]` plus the module summary without patch failures/conflicts. Open SETTINGS (saved key, F3 by default) → Game → HUD and toggle each new control separately. Face nearby groups to check the colors/distances; approach and leave a location's 1 km radius; load a save and switch CleanHUD/Patrols off/on to check cleanup and duplicates. Locations should work with Patrols off. Terrain streaming/origin shifts and scheduler timings still require in-game verification. [Detailed steps](https://github.com/DeonUrist/WildWasteland/blob/v0.1.5/notes/compass-markers.md).

ZIP SHA256: `56627DE69576908F7A952BFDB1A78F27716AA604915436BD906FF9E7ABC49638`.

[Release candidate 1](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.4) · [Feature documentation](https://github.com/DeonUrist/WildWasteland/wiki)
