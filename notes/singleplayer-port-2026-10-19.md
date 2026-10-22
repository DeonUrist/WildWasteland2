# Recent WWMP improvements implemented in standard WW — 2026-10-19

Status: **Testing**. Ported into the standard `V:/Repos/Apocalypter/WildWasteland` working tree, based on HEAD `7208ca0` (0.1.40 RC37), preserving the pending pot and seated-melee fixes. Source is the current WWMP working tree and its October19 porting handover, including the final `VehicleLoadPlacement.Stand` camera correction associated with MP installed MD5 `1EB949FB2DFA36E65B3F6D4E0D0DF11C`. This task did not modify the MP implementation, deploy SP, launch the game, run a harness, change live configs/saves, commit or publish a release.

## Implemented feature checklist

| Area | Implemented in this SP working tree |
| --- | --- |
| Save library — S01 | Unlimited named snapshots; five screenshot rows/page; separate save/world names, date and distance from start; search, preview, Manual/Autosave/Backups views; explicit overwrite/delete/restore/load confirmations; visible deletion/history and backup-folder access. No player counts or hosting tabs. |
| Durable saves — S02 | Native serialization with input/time freeze before capture, validated staged publication, complete companion copies, durable undo journals, bytes/timestamp rollback, interrupted-write recovery and loaded-source write protection. Fresh worlds have unique working identities. |
| Native loading/Continue | Independent native `loadgame` FSM adapter; newest published SP snapshot for Continue; legacy SaveGame1–10 readable; confirmed in-session loads return through title; native load/save buttons open the same library. No ApocalypterMP dependency. |
| Autosaves — S03 | Separate retained snapshots, visible native-font compass countdown, 30/15-second notices and three-second final warning; combat/activity/vehicle checks, ten quiet seconds and full warning restart; last-moment combat cancels a scheduled save before disk mutation. |
| Vehicles — S04/S19 | Manual/exit saves while seated, optional driving autosaves, no forced exit when saving. On loading a seated save, stop/brake the vehicle, clear velocities, wait for main terrain/standing clearance and stand beside it. |
| Final camera correction — S19 | Detach the deserialized camera holder from the player/seat, restore player yaw and the on-foot camera anchor, then enter Look.Reset to recapture mouse yaw/pitch. This removes the proven player/camera rotation-feedback mechanism; the reported symptom still needs a runtime retest. |
| Apocasaver compatibility — S05 | WW owns saving/history; loaded standalone Apocasaver runners/callbacks/save patches stand down after the replacement installs. Apocasetter remains available. Stable hinge keys and hand-pose metadata are preserved. |
| Distant world history — S05 | Capture native static-location state before MapMagic pooling/reset and restore retained ES3 records on placement; preserve distant changes through save/load without retaining unloaded objects. |
| Conservative cleanup — S06 | Default-off selection of positively identified exhausted disposable packs/non-resource zombie remains, minimum distance/time and custody/reference protection; existing abandoned-patrol cleanup exposed separately in SYSTEM. |
| Save participants — S07 | NPC senses, Patrols cache/file/backup, POI history, map fog/discoveries, quest/crew state and supported legacy companions participate in publication/rollback. Cancelled saves clear delayed writes; swallowed persistence failures reach the transaction. Restoring a backup also removes newer companions absent from that backup, with a complete undo copy first. |
| Inventory — S08 | Click reserve ammo, Grenade, Battery, Nuts, Bandages or First Aid to select a quantity with slider/+/- and Drop/Cancel. Medical x/3 counts use the live equipped pack; split without healing or losing the remainder. Native item prefabs/registration/quantities are retained; grenades drop individually. |
| Characters — S09 | Male, Merchant, Bodyguard, Sprokka, OrganicMechanic, Female, Gungirl and Shoota; picture-only settings row and matching TAB portraits; legacy aliases; unreadable native-mesh baking and all first-person mesh cuts. Female asset paths remain available with NPC simulation disabled. |
| Crawling — S10 | Separate stance holstering from Tick, require initialized/enabled Movement before state forcing and avoid recursive stance entry. Existing Low Crawl animation/clearance/controls retained. |
| Scene atmosphere — S11 | Scope the native Azure gameplay-camera fog material to its own unfiltered blit branch and restore it in the finalizer. Weather on removes native distance whitening; WW storm walls/dust remain; Weather off restores native haze. Replaces the failed density-fade attempt. |
| NPC/ModAPI poses — S12 | Blend all ten existing per-arm pose offsets after aim lift; no pose-format change or network rig registration. |
| Compass — S13 | Optional marker labels with distance, resized text and refreshed identity when a pooled marker changes. Public Marker.Label is additive and cloned with its style. |
| Recommendations — S14 | Optional title-menu recommendations default off; required restart/conflict warnings remain independent. SP update checking/version/URLs retained. |
| Map controls — S15 | Fog enabled, radius, edge and opacity grouped under Debug > CleanHUD > Map fog of war; WORLD/Developer duplicate rows removed; existing keys and Debug visibility retained. |
| Landed bolts — S16 | Retry missing bolt-model discovery and shift unparented landed visuals with FloatingOrigin. Native damage/attachment behavior retained. |
| Loading presentation — S17 | All18 supplied numbered PNGs and exact tips, plus separate BG_MAIN; embedded missing-only extraction. Continuous eased aspect-preserving UV drift, black backing/4% crop and a different image/tip every ten unscaled seconds. Existing custom images/tips remain; missing tips in an older JSON fall back to the new embedded content. |
| Head look — S18 | Local seated camera-directed neck/head motion without requiring gun arms, residual foot head pitch and mapped humanoid spine compensation for NPC/ModAPI head pitch. |
| Seated combat — A11 retained | Visible melee and held blast lances, native FP-timed retargeted upper-body swings, retained torso motion for lance/grenade throws and normal gun/wheel ownership transitions. This retains the previous common SP fix; it does not copy raw nonhumanoid FP bone curves. |
| Stew Pot — A10 + follow-up | One shared material slot for every GLB submesh on both renderers, retaining body and both handles; null material/texture guards in ES3 template references. |
| Small shared corrections | Animated-character kick .55s timing/.15s fade; immediate seated gunner target-change checks; god-mode exclusion only for the actual player target; refreshed local collision-player/car accessors; weather-force ground data sampled independently of camera rendering. |
| Current tuning | Captured compatible MP settings supply BepInEx Bind defaults, including 20-minute autosaves, 30-second warning, five autosaves/backups, screenshots on, driving autosaves/cleanup off and current camera/map tuning. Saved SP values and legacy migrations win; RESET uses tuned defaults. Current aim/weapon/wheel/layout profiles install missing-only after migration. SP ignition keeps I. [Readable preset/profiles](../presets/current-settings-2026-10-19/README.md). |

Already released SP behavior remains included: Low Crawl, merchant idle exclusion, quest dialogue mouse-look freeze and temporary voice suppression, motorcycle independent tuning/defaults, player/world/HUD settings, forced-off color grading, traveller hearing and the female kick atlas. This port does not reimplement or discard them.

## Standalone integration and compatibility

The former offline MP adapters are replaced by `Core/SaveRuntime.cs` and local `SaveSystem`, `SaveFiles`, `SaveCombat`, `SaveWarning`, `SaveCleanup`, `SaveCompatibility`, `WorldHistory` and `SinglePlayerSaving`. Generated `NativeAccess` only reads native ES3/terrain members; no multiplayer transport or fake multiplayer service is introduced. Native local supply prefabs are cached in a budgeted discovery job. The native Wait dispatcher is shared with the pre-existing Patrol load gate; MouseLook is shared by local save freezing and the existing quest dialogue guard, with one Core patch per target.

New save identities are `WW_live-*`, `WW_save-*` and `WW_auto-*`; metadata/journals use `WW-SAVE-1`, and backups use native save root `/WildWasteland-Saves/Backups`. Existing native/module prefab IDs, serialization formats and standalone paths remain. New WW-owned keys are `WildWasteland.SaveCommit` and `WildWasteland.CleanupProtected`; Apocasaver hinge/pose keys retain their established names. No MP saves or launch roots are imported automatically.

Twelve SYSTEM saving/cleanup config entries and the optional Debug recommendation entry are new to SP. Captured orphan settings remain unbound reference data; multiplayer name/display/shared-vision entries are excluded. Config/profile/portrait/artwork overrides are preserved. No native graphics/input settings or other mods' configs are imported. The supplied Screen12 `gearm ammo` text is preserved verbatim.

## Build and stage

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false -o bin/Release/sp-port_20261019 -v:minimal` passed with **zero warnings and zero errors**. Two earlier compiler passes exposed adapter/member and settings-wrapper errors; those are corrected in the final successful build.

- Stage: `bin/Release/WildWasteland_sp-port_20261019_153538.dll`
- Built/staged MD5: **`A6772DB0CD483B17DFD57B5ABACFAD98`**
- Size: 86,309,376 bytes; current 0.1.40 identity retained.
- **Not deployed** under the existing standard-WW policy. Required new portraits, loading artwork/tips, settings defaults and tuning profiles are embedded. Existing standard-WW asset folders (GLBs, animation/voice bundles, theme, etc.) remain required as before.

Compilation and hash matching establish a consistent staged artifact, not save-integrity, rendering or performance acceptance. Status stays Testing.

## Denis's manual acceptance

1. Use the standard-WW stage with ApocalypterMP absent. Test native/CleanHUD-off and styled save/load/Continue; create two new worlds; create/overwrite/cancel/delete/restore named snapshots; confirm screenshot/world/distance and distinct autosaves/backups. Check old native slots remain readable. Exercise interrupted/failed-write recovery on disposable save copies before relying on the new engine.
2. Drive away, save while seated, quit/reload old and new seated saves, then leave the mouse still: expect a stopped/braked car, supported standing player and detached stable camera, no crawl or spinning. Then look, walk, enter/drive/exit normally. Relevant log: `On-foot camera restored: ... parent=none ...`; `In-vehicle save loaded: player standing beside parked ...`.
3. Trigger combat during countdown/final warning and vehicle combat, then end it: require ten quiet seconds and a restarted full warning. Check manual/exit saves, CleanHUD-off saving overlay and preserved pause/input behavior. Explore/change a distant site/POI, save-and-quit, reload/restore and compare placed items, quests, fog/discoveries, NPC senses and Patrol state together.
4. Split every supply type, exhaust/change/refill medical packs, cancel/pick up and save/reload; expect exact selected counts with no healing/duplication. Test all eight characters/portraits, NPC off, native/female models, foot/seated head look, gun poses, melee/lance/grenade actions and wheel hands. Regress crawl/clearance and landed bolts across an origin shift.
5. Inspect all18 screens/tips, wide/narrow aspect ratios and ten/twenty-second transitions. Check existing custom config/profile/artwork files remain, fresh/missing values use the captured tuning, and map-fog settings appear only under Debug. Compare the same mountains with Weather on/off and storms approaching/inside/cleared; retain regional dust/walls. Inspect both pot handles/body on spawned/loaded pots, drink and save/reload. Validate visual fit and performance before declaring Verified.
