# Women of Wasteland inside NPC

Status: Testing. Source: [WomenOfWasteland 1.0.0](https://github.com/DeonUrist/WomenOfWasteland), commit `21d05c6`. Denis requested integration in NPC and population controls in the in-game WW Debug settings.

## Implementation

- Source `Gungirl.cs`, `Voice.cs`, `Gltf.cs`, `Json.cs`, `Bindposes.cs`, `Level.cs`, `Wav.cs` move to `src/Modules/NPC/Women/`, namespace `WildWasteland.Modules.NPC`, original class names retained. Source Plugin/Runner are replaced by NPC config/lifecycle, Core hooks, scheduler jobs and hotkeys.
- NPC owns `INpcAppearance` through `Services.NpcAppearance`. Patrols invokes it after setting role names, preserves the name in existing driver/passenger save fields, and transfers appearance to bailout/turret dismount and manually spawned carcass replacements. Existing traffic turret-name capture already preserves female identity. A transient template flag prevents gender rerolls during rematerialization. Female variants retain their native ranged-human detection, guns, skeletal poses and faction.
- Gungirl/Shoota are explicit driver/passenger/turret editor choices; their native base assets remain Flexa/Sprokka, including corpse aliases. With NPC off, alias resolution keeps native fallback bodies and persistent female names. Native prefab IDs, ES3 keys and Patrols binary schema do not change. No new components on saved actors.
- `CreateObject.OnEnter` joins the existing shared dispatcher, after other NPC subscribers and before Guns. The hook filters by active framework and cached prefab name, applies selection/name identity immediately and queues dressing.
- Shutdown restores captured meshes/materials, accessory visibility, local voice/action/array references, shout timers and corpse labels, stops the loader and destroys generated assets. Names remain persistent identities. Re-enable reconstructs appearance from those names.

## Config and assets

All new settings are in `WildWasteland/settings.cfg`. WW Settings > Debug > NPC AI & navigation > **NPC population** exposes EnableFemaleNpc (true), FemalePopulation (33), FemaleCrew (true). Adjacent **Female appearance & voices** exposes both model/texture/hide-part entries plus voice folder, loudness matching, multiplier and shout intervals (defaults unchanged). The debug spawn binding defaults None. No new population entries in the Apocasetter/F6 cfg or tools.cfg.

Each absent setting imports the standalone cfg first, then Apocaraider; existing WW values win. The original hidden values become bound settings with their old defaults. Relative assets resolve under `plugins/WildWasteland/NPC`, with a legacy plugin-folder fallback. Old Models/Sounds files, including extra OGG replacements, copy only if missing. Seventeen committed default resources are embedded in NPC/Embedded/WomenOfWasteland and extracted if absent, so DLL deployment includes a complete default install. The source checkout's uncommitted human_hurt_2.wav change is untouched and excluded from embedded defaults.

Standalone WoW is a soft dependency. If loaded, only the built-in female framework is skipped (logged), leaving NPC AI active. Disable the standalone plugin and restart to use integrated female crew. The source mod is never patched or disabled by WW.

## Verification and performance

Windows release/deploy build: **passed**, zero errors and seven warnings in existing code. Deployed DLL MD5: `43ce49b08bf6bba30f1206bb2afebf3e` (matches build output). Unique build snapshot: `bin/Release/WildWasteland_0.1.10w.dll`. No game launch, harness, simulation or runtime claim. Standalone and integrated runtime profiling is for Denis. Global repeated renderer scans are replaced by a 500 us scene traversal and registration/spawn queues; native mesh layouts/models/textures warm at startup. Voice loading/normalization uses the shared runner at startup; pending NPCs cache their components. Native uploads, first-use uncached skeleton layouts and one NPC's voice FSM edit are still indivisible, requiring ModProfiler checks. No performance measurements are claimed.

## In-game checks for Denis

1. Disable standalone WomenOfWasteland if installed and restart. Open the WW Settings menu (current default F3), enable access to Debug if needed, and open Debug > NPC AI & navigation > NPC population. Confirm the three population controls are there and not in Apocasetter/F6.
2. Set EnableFemaleNpc and FemaleCrew on, FemalePopulation to 100. Use a Scrapyard vehicle template with Flexa driver, Sprokka passenger and a Flexa/Sprokka turret. Spawn it through the existing patrol editor. Expect female bodies and voices, correct seated/gunner animation, and normal driving/firing. Other NPC types and Coyote crews stay unchanged.
3. Save/load with driver and passenger alive: expect the same female variants, health and crew behaviour. Force a bailout and turret dismount: expect the same female identity on foot. Kill one: expect its matching female corpse and label. Traffic crews must keep their variants after leaving/re-entering their physical range. Standalone saved-car turret reconstruction retains its pre-existing limitation; traffic records preserve turrets.
4. Set FemalePopulation to 0, spawn fresh Flexa/Sprokka crew: expect males. Explicit Gungirl/Shoota template choices must remain female. Save/load or virtualize those male crews after raising population to 100: expect existing males to remain male. Turning FemaleCrew off must stop random crew conversion while ordinary native spawns still follow FemalePopulation.
5. Toggle NPC off then on: expect original rendering/sounds while off and restored female appearance on, without duplicate runners/voices or errors. Check saved females with new conversion disabled, including older scale-marked Flexas.
6. With NPC VerboseLog enabled, look for `[NPC] Women of Wasteland 1.0.0 integrated; female crew conversion available.`, `Gungirl: ... body swapped`, `Shoota: ... body swapped`, and `Female voice: ... sound reference(s) replaced`. No CreateObject hook, female model/voice, or scheduler errors. Compare WW Performance/ModProfiler against the same baseline scenarios.

Do not mark Verified until Denis completes in-game checks.
