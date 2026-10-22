# Compass groups and nearby locations — 2026-10-15

The CleanHUD API already rendered marker icons and distances, but Wild Wasteland did not feed its Patrols group records or native location targets into it. Added two separate HUD controls: CleanHUD / HUD ShowConvoys and ShowLocations, both true. ShowConvoys covers all vehicle roles; merchants yellow, Coyotes green (including convoys), patrols red and other convoys orange.

Seven source files (~220 changed lines): Core.Services supplies the inter-module contract; CorePages exposes the controls; CleanHudModule adapts the existing public API; TrafficCompass owns group reconciliation using registered lists; PatrolsModule registers/clears it; SceneCache collects native mapIcon parents and queues applied terrain; LocationMarkers owns native map targets within 1 km and its tile-event lifecycle. No new Harmony targets, scene scans per frame, texture work, save fields or update loops. Two 0.5 s jobs reuse cached records; terrain work uses the existing 500 microsecond slice. Runtime timings remain unmeasured.

Validation: Windows dotnet build WildWasteland.csproj -c Release -p:Deploy=true passed with 0 errors, 7 existing warnings. Built and deployed DLL MD5 match: 4564EBED5E35C24BDED034F98D34E23A. The compass follow-up is included in 0.1.5 RC2; verification remains Testing. The MD5 above identifies the initial 0.1.4 local build.

Denis's in-game checks:
1. Launch and open SETTINGS (saved key, F3 by default), Game → HUD. Convoys on compass and Locations on compass (1 km) should both be On.
2. Face a merchant, Coyote group, hostile patrol and hostile convoy. Expect one yellow, green, red or orange car silhouette per group respectively, with distance updating as you move. Coyote convoys remain green; merchant escorts do not duplicate the merchant marker.
3. Approach a native camp/wreck/location within 1 km and face it. Expect a camp silhouette with distance; leave the radius and it disappears.
4. Toggle either control off/on independently, then CleanHUD and Patrols off/on, and load a save. Expect the selected category to hide/return without duplicates; locations still work with Patrols off.
5. Approach newly streamed terrain and travel far enough for a floating-origin shift. Location/group directions and distances should remain correct. Captured/destroyed groups should lose their markers when retired.
6. Enable Debug if checking scheduler timings: Modules → Performance lists CleanHUD.Location markers, Patrols.Compass markers and the existing Scene key discovery job. The startup summary should show no new patches/failures. No new routine log spam is added.

Release validation: numeric version 0.1.5 / RC2; Windows Release build/deployment succeeded with 0 errors (7 existing warnings). Installed/build DLL MD5 E5977630DF9C1D676157FABF3DDC7245. Both new settings default true; in-game/profiler checks remain Testing.
