# Wild Wasteland

**Wild Wasteland 1.0.9** is a single-player gameplay overhaul for **Apocalypter**, built for BepInEx 5. It brings a visible player body, more capable enemies, travelling vehicle crews, expanded combat, quests, survival features and a unified interface into one configurable mod.

Saving now waits for pending terrain work and holds streaming through publication. Location-discovery updates use same-directory file replacement on Windows; real write errors still retain the save safety checks.

Choose which modules to use and adjust them through the in-game settings. Wild Wasteland shares their loading, saving and update work so the included systems can interact without running duplicate standalone plugins.

**Towns** adds settlements with faction populations, merchants and gas stations beside separate parking lots. Toggle it under F3 > Modules, set its chance in WORLD > POI, and tune building/population/loot bounds under Debug > Developer > Towns. Remove the separate WildWastelandTowns DLL. Existing names/settings/layouts import; ownership needs five actual faction spawns. Merchants visit real towns; settled convoys leave a guarded truck. Laboratory entrance doors are inactive on town copies; original POI entrances work normally. [Testing notes and acceptance steps](notes/towns-merge-2026-10-20.md).

With **Vistas World Gen / World Amp**, Towns and added POIs leave terrain heights unchanged. Towns use naturally flatter sites, place buildings on existing ground and build no access roads or parking pavement. Fewer towns may fit; town bus/convoy visits that require access roads are unavailable in this mode. Starting-tile seam behavior remains under testing.

[Download 1.0.9](https://github.com/DeonUrist/WildWasteland/releases/tag/v1.0.9) · [Feature wiki](https://github.com/DeonUrist/WildWasteland/wiki) · [Report a problem](https://github.com/DeonUrist/WildWasteland/issues)

## Player body and movement

See your character in first and third person, with visible equipment, matching inventory portraits and a choice of eight appearances: Male, Merchant, Bodyguard, Sprokka, Organic Mechanic, Female, Gungirl and Shoota. First-person body and arm options, camera distance and weapon poses can be adjusted separately.

Climb obstacles, climb ladders with weapons stowed, slide down, or jump to let go. Use Low Crawl to move forward or backward close to the ground, with standing clearance checked before getting up. Vehicle poses include steering-wheel hands, motorcycle handlebar grips and passenger poses. Looking around turns the head and upper body; wider seated turns let the character face the action. Fists, melee weapons, blast lances and grenades use upper-body attacks while seated, including visible held weapons and throw animations.

## Enemies and encounters

NPCs react to sight and sound, investigate the last known position, search, take cover and manage their firing bursts. Moving players can be heard within five metres with a clear line, regardless of facing. Baked POI navigation lets human NPCs climb, use ladders and reach upper floors. Their awareness is based on what they can perceive rather than continuous knowledge of the player's position. Camp navigation, idle activities and creature behaviors give settlements and the surrounding wasteland more activity.

Female raiders and crew are included, with configurable population settings. Dogs, ambushing and burrowing creatures, pouncing enemies and spider nests add variety to encounters. Camp merchants and mechanics retain their shop locations.

## Traffic, vehicles and the War Rig

Meet persistent raider patrols, convoys, motorcycles, travelling Coyote merchants and faction battles. Vehicles carry live drivers, passengers and gunners who can fight from their seats or continue on foot. Crews, vehicle damage, missing parts, cargo and trailer state participate in saving and loading.

The War Rig adds a tanker trailer and associated encounters. Vehicle templates and advanced seat, turret and route tools are available for players who want to customize traffic. These tools are kept in Debug and Developer settings so ordinary play does not require them.

## Combat and inventory

Gunplay includes physical bullets, tracers, range falloff, vehicle-part damage, per-gun magazine tracking and hold-to-unload controls. Combat feedback offers configurable crosshairs, hit markers, damage numbers and headshot audio.

The replacement inventory shows your character, equipment and supplies. Click equipment to drop it, or click an ammo, grenade, flashlight-battery, Nuts, Bandages or First Aid row to choose how much to drop. Medical supplies show their current capacity. The quantity dialog lets you keep the rest of the stack.

## Quests and survival

Take authored and generated jobs, build faction reputation, follow colored objectives through the tracker and journal, and hire eligible followers. Friendly NPC interaction works without an additional dialogue mod; optional Talk integration remains available. Quest conversations hold the view still while you choose a response. Supplied quest voice playback is currently disabled while the recordings are being completed.

Stew pots can be found in natural loot and used for filling, drinking and cooking, with spoilage and optional pouring. Moving sandstorms and hurricanes bring dust walls, changing light, thunder and dangerous winds; their intensity and presentation are configurable.

## HUD, map and menus

CleanHUD provides configurable vitals, weapon and ammo displays, quick slots, vehicle status, a strip compass and a message log. Map exploration and fog of war reveal the places you visit; unexplored areas default to full opacity (1.0), with a soft reveal edge. Binoculars help identify distant locations and traffic. Map, compass and world-traffic visibility can be configured independently.

Styled title, pause, inventory, journal and settings screens share the game's font and a choice of accent colors. Loading and saving use 18 illustrated screens with tips, gentle image movement and progress reporting. External icons, loading artwork and supported layout files can be customized.

## Saves and autosaves

Create named snapshots without being limited to the native ten-slot list. The save library has screenshot previews, world names, dates, distance from the starting area, search and separate Manual, Autosave and Backups views. Loading, overwriting, deleting and restoring require confirmation.

Saving captures the world together with supported module data, including patrols, NPC knowledge, quests and map exploration. Staged writes, backups and interrupted-save recovery protect the publication of a snapshot. Native legacy saves remain available in the library. Keep the full set of companion files when moving saves between installations.

Autosaves have their own retention and a visible countdown. Combat delays them until activity has settled, then restarts the warning. Fresh settings use a 20-minute interval, a 30-second warning and five retained autosaves and backup versions. Manual and exit saves work in vehicles; driving autosaves are optional and off by default. Loading a seated save parks the vehicle and places you safely beside it with the on-foot camera restored. Optional save cleanup is off by default.

## Installation and updating

1. Install **BepInEx 5** for Apocalypter. [Apocasetter](https://github.com/DeonUrist/Apocasetter-Index) is recommended for mod management and access to its Mods menu.
2. Download the release ZIP and extract it into `BepInEx/plugins/`. The result must include `BepInEx/plugins/WildWasteland/WildWasteland.dll` and its asset folders.
3. Disable the standalone mods replaced below. With a compatible Apocasetter installer, Wild Wasteland can queue their disabling and request a restart. Resolve that notice before playing.
4. Start the game and open **WILD WASTELAND** from the title or pause menu, or press **F3** by default, to choose your settings and modules.

The ZIP contains the plugin, runtime assets and required asset license notice. Documentation and release history stay on GitHub.

When updating, back up your saves and preserve customized external artwork, models, layouts and pose files. Avoid overwriting your custom assets with package defaults. Existing Wild Wasteland settings and supported standalone settings are retained; captured tuning supplies defaults for new or missing settings. Missing supported profiles and embedded artwork are installed automatically without replacing existing files.

### Standalone mods included or replaced

Disable these standalone plugins before using their integrated features. Keep their configuration and data files for migration.

| Included system | Standalone plugins replaced |
| --- | --- |
| Player body and climbing | Apocaplayer, Apocaclimber |
| NPC AI and population | NPC Overhaul / NPCAI, Women of Wasteland |
| Combat, magazines and combat HUD | Gunplay, Apocaunloader, Gunplay HUD, HealthStacks |
| HUD, menus and map | CleanHUD, Immersive Map |
| Patrols and vehicle encounters | Apocapatrol, War Rig, Merchant Object Spotter |
| Quests | Apocalypter Quests |
| Survival | Stew Pot |
| Saving | Apocasaver |
| Shared runtime handling | iTweenGuard |

Remove the old **Apocaraider** monolith manually as well. Apocasaver's save engine is replaced by Wild Wasteland's integrated saving; **Apocasetter remains supported**.

### Optional mods

**Engine Sim, ApocaChaseCamera, Apocapocket, Apocatalker, ApocaLanguage, Vistas World Amp and Dog Companion** have optional integration and stay separately installed if you use them. They are not bundled. Engine Sim retains its simulation and settings, ChaseCamera retains its camera, and Apocapocket retains its additional item slots. Their own installation requirements still apply. Other AI overhauls may overlap with the NPC module; enable only the system you intend to control NPC behavior.

## Settings and controls

The default **F3** window groups ordinary choices under **PLAYER**, **WORLD**, **HUD** and **SYSTEM**, alongside module switches and Performance. Debug and Developer expose advanced tools when enabled. The window pauses single-player gameplay. Apocasetter's **F6** Mods menu provides the Wild Wasteland entry and window-key/debug options.

**WORLD** includes Fog of war on/off, available with Debug disabled. Reveal radius, edge fade and opacity are under **Debug → CleanHUD → Map fog of war**.

Rebind actions through the Controls screen. Useful defaults include **Y** for Crawl, **I** for Ignition, **J** for the quest journal, **F** for friendly NPC interaction and **Delete** to dismiss a hire. Existing saved bindings take precedence.

Settings live under `BepInEx/config/WildWasteland/`, chiefly `settings.cfg` and `tools.cfg`; the main plugin file is `BepInEx/config/com.denis.apocalypter.wildwasteland.cfg`. Module data and editable profiles use subfolders. Runtime assets are under `BepInEx/plugins/WildWasteland/`, including `LoadingScreens/` and `CleanHUD/Icons/`.

Performance optimizations are optional and experimental. Its presets and measurements let you choose a configuration and inspect module costs; actual results depend on your world and installed mods.

## Compatibility and support

Wild Wasteland 1.0.9 is the **single-player** project. Multiplayer-specific features belong to the separate WWMP build. Mod-created vehicles and components require their supporting modules when loading; removing the mod cannot make the base game restore objects it does not know.

Some features remain marked **Testing** in the [wiki](https://github.com/DeonUrist/WildWasteland/wiki). The release compiles successfully; that does not establish complete gameplay, save-recovery or performance verification across every setup.

For a problem report, include the version, reproduction steps, relevant optional mods and `BepInEx/LogOutput.log`. Enable verbose logging only when collecting a requested trace. [Open an issue](https://github.com/DeonUrist/WildWasteland/issues) for bugs or feature requests.

Quest voice recordings credit **LostSilver666** and **Soldier96**. Redistributed weather artwork retains its supplied JangaFX license notice.

## Building from source

On Windows with .NET SDK 6 or newer and the game installed:

```powershell
dotnet build WildWasteland.csproj -c Release -p:Deploy=false
```

Set `-p:GameDir="D:/path/to/Apocalypter"` if needed. Build output is `bin/Release/WildWasteland.dll`. Contributor instructions and technical records are in [AGENTS.md](AGENTS.md) and [docs](docs/).
