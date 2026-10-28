# Vista 1.2.0 bridge — 2026-10-28 — Testing

Denis requested support for the newly installed Vista while preserving 1.0.7, adding WW town work after Vista generation, and withholding player control until initial loading completes. He approved expanding beyond the normal 50-line/one-module scope into Compatibility, POI/Towns and the loading presentation if necessary. No Core or HUD source changes were needed. Unrelated existing working-tree edits were preserved.

## Cause and inputs

The live log reports `TypeLoadException` for `A.E` in World Amp 1.2.0. The old bridge hardcodes the 1.0.7 obfuscation map. In 1.2.0 those types and members have different names; its road-registration overload also adds a long identity parameter, and terrain writes now commit through a queued job instead of the old synchronous grading method.

Inspected installed dependency: `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/VistasWorldGen.dll`, assembly 1.2.0.0, MD5 `3CD45A8B12202F068DD145514148319D`. Inspection-only decompile: `V:/Repos/Apocalypter/_tools/decompiled/VistasWorldGen-1.2.0-20261028/`. Existing 1.0.7 decompilation and `Sources/systems/save_load.md` supplied the comparison and native handoff state. No dependency was modified or redistributed.

## Change

Approximately 185 added/removed lines across six C# files:

- `VistasWorldGen.cs` retains the old hook targets, selects the separate 1.2.0 bindings by the installed loading-gate type, includes Water/World settings in regenerative road-cache identity, and records hooks for removal in reverse order. Dependent transpilers are removed while their field metadata and overloaded-field resolver still exist.
- `VistasWorldGen.120.cs` binds 1.2.0 places, scouting, weather, population, road capture, committed terrain and loading presentation. Its finalization postfix reports the terrain after Vista's sliced commit, allowing existing town-pad replay and routing snapshots to run.
- `VistasRoads.cs` retains the 1.0.7 constructor branch and adds 1.2.0 typed delegates. Readiness waits for the native loading gate, MapMagic, spawn track, requested/completed road batches, worker inactivity, the terrain-commit queue, current main-tile paint/grade revisions and Vista's own highway geometry completion predicate. Intentional excluded geometry uses Vista's existing completion rule. The bridge does not accept Vista's timeouts as proof that road work completed.
- `VistasBridge.cs` publishes the version-specific road provider, registers loading readiness/progress under the actual Compatibility module (optional bridge scopes are not ModuleHost entries), and removes patches before clearing metadata.
- `VistasEngineHud.cs` selects the new heat-gauge draw target while preserving the older target.
- `TownsModule.cs` displays pending Vista town siting/access-road work and reserves the subsequent progress range for construction. Existing holds continue to cover buildings, walls, paths/navigation and residents. Saved town positions remain unchanged.

With the WW loading presentation active, only Vista's loading draw is suppressed. Its generation gate and background work continue. With WW presentation absent, Vista can still draw its own screen. No new config entries or save formats. The existing behavior resetting Vista `Debug.Overlay` false at startup remains.

This completes the requested bridge adaptation, not runtime acceptance or every potential highway feature. “Loaded” means the initial generated/applied terrain and pending WW towns; the world continues streaming as the player travels.

## Remaining integration work and limits

1. **Highway-aware navigation and access selection.** Existing `IWorldGenFacts.RoadLines` describes XZ centrelines with Y=0; town access and strategic traffic sample terrain heights. It does not communicate elevated deck height, barriers, valid exits or lane connectivity. A proper surface/exit contract is needed before claiming traffic can use every elevated highway or that town connectors always meet a legal highway exit. This patch preserves native highway geometry and signs; it does not create that routing contract.
2. **WW town names on highway signs.** Towns are connected through the existing native polyline registration, but this does not register their names/destinations with a highway sign system. A separate destination integration is required if signs should advertise WW towns. No sign-text compatibility claim is made.
3. **Later streamed terrain and saved surroundings.** Initial readiness cannot guarantee completion of future streamed terrain. Later roads or changed generation settings can affect existing town surroundings. The 1.0.7 Add/Off cleanup writer limitation remains documented in the earlier audit. Additional 1.2.0 terrain writers and riverbed presentation need dedicated coverage before claiming every edit reaches all WW consumers.
4. **Prior ownership conflicts remain separate.** Engine heat versus WW traffic heat switches/EngineSim, far-clip/terrain Performance ownership and high-altitude follower recovery were identified in the 1.0.7 audit. This version adapter does not resolve them or establish whether every one still reproduces in 1.2.0. Older Vista-only seam defects also need a new-version retest.

## Build and deployment

`dotnet build WildWasteland.csproj -c Release -p:Deploy=true` in Windows PowerShell: **0 warnings, 0 errors**. Built and installed WW MD5: **`53B28AE814F5A5B04629A20EB2BDC064`**. Installed to the current WW2 project target, `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland2/WildWasteland.dll`. Unique local copy: `bin/Release/WildWasteland_2.0.10_vista120_20261028.dll`. Apocasetter's disabled-WW manifest was absent. No game was launched, no runtime harness was written or run, and no commit/push/release was made. The compiled checkout also contains pre-existing edits from other work.

## Denis's in-game checks

1. Restart with Vista 1.2.0. Require `Vistas 1.2.0 compatibility ready:` and `Vistas 1.2.0 loading: native generation, committed roads/highways/signs, then WW towns before player control.` There must be no bridge-installation or Harmony error.
2. Start a new world with roads/highways enabled. WW's loading screen should remain through Vista generation and then show town-site/access and construction stages where selected. Player control must wait for completion; buildings/residents should not continue initial assembly after control is handed over. Normal streaming during travel remains expected.
3. For deliberate town coverage, use an unused test world with Town chance (%) = 100 and New POI near spawn enabled, then restore those settings. A candidate can still be rejected for lack of safe terrain/access; the settings do not override safety checks. On a successful candidate the log must order `Vista roads ready; town siting begins`, `Vista road connector registered`, foundation/building preparation and `[POI] Load handoff ready: town buildings and population restored before play.` The independent `Vista load handoff ready:` must also precede play.
4. Save to a test slot, quit to title, reload. Existing towns retain their sites and complete restoration before control. Inspect highway exits/signs and town connections for overlap. A terrain-based connector is not evidence of legal elevated-deck/exit routing.
5. With the game closed, test the same WW DLL with the previous Vista 1.0.7 installed instead (one Vista DLL at a time). Require `Vistas 1.0.7 compatibility ready:` and normal new/load, town connectors, region banners and weather behavior. This adapter retains the previous bindings; backward runtime behavior still needs this acceptance check.
6. Check quitting and a Compatibility/Vistas World Gen off/on cycle for teardown errors. If loading stalls or a new hook fails, retain that run's complete LogOutput.log and the installed WW/Vista MD5s; enable WW Debug level 2 for Vista's routine diagnostics on the next reproduction.

Status remains **Testing**. Compilation does not establish 100% runtime loading correctness.

## Load-error follow-up: generic global calls

Denis's next run used the expected installed MD5 `53B28AE814F5A5B04629A20EB2BDC064`. It passed the initial place/weather eligibility bindings but failed installing the weather transition with HarmonyX `Invalid argument for call NULL`. The bridge therefore still did not start; the initial build was not a runtime success.

The installed transition IL calls Vista's generic string decoders declared on `<Module>`. The installed MonoMod `ReflectionHelper._ResolveReflection` resolves global method definitions with `Module.GetMethods` and `Extensions.Is`, then closes generic instances; an unmatched method becomes null. HarmonyX copies this result into the original call operand and rejects it during WriteTo. WW's injected `Transition` helper exists in the compiled DLL, and the log contains no failed AccessTools lookup for it.

The follow-up extends the existing Vista-scoped reflection resolver for those global method references. During 1.2.0 initialization, Cecil reads the original DLL metadata, verifies its MVID against the loaded assembly, and maps unique global method names to `Module.ResolveMethod` using their original definition tokens. Generic call references are closed with their original type arguments. Duplicate names are rejected rather than selecting an overload arbitrarily. The map stays available through hook removal and is cleared on shutdown. The older 1.0.7 path and non-Vista references retain their existing behavior. Approximately 33 added lines across `VistasWorldGen.cs` and `VistasWorldGen.120.cs`; no configuration/save changes or dependency edits.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true`: **0 warnings, 0 errors**. New built/installed MD5 **`4487C87EB7071AD4D7AE04D21FCE1216`** supersedes the first handover. Unique local copy: `bin/Release/WildWasteland_2.0.10_vista120_globalcalls_20261028.dll`. No game launch or runtime harness. Restart and require `Vistas 1.2.0 compatibility ready:` with no `Invalid argument for call NULL` or bridge-installation error before testing new/load and town completion. Runtime acceptance remains with Denis.
