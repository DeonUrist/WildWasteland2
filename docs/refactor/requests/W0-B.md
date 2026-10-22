# W0-B requests (changes needed in files W0-B does not own)

All new Core utilities are unused until Wave A/B switches callers. These wirings belong to the owners named below.

| id | file (owner) | change | why |
|---|---|---|---|
| W0B-1 | src/Core/ModuleHost.cs `Cleanup(IModule)` (W0-A, then A1) | call `Textures.Release(m)`, `WorldIndex.RemoveAll(m)`, `WorldTiles.RemoveAll(m)`, `FsmIndex.RemoveAll(m)`, `FsmPause.ReleaseAll(m)`, `Factions.RemoveAll(m)`, `CameraArbiter.RemoveAll(m)` | owner-scoped registrations/resources are released on module shutdown (README 11, AGENTS 6) |
| W0B-2 | src/Core/Plugin.cs teardown (W0-A / A1) | `WorldTiles.Uninstall()`, `Textures.ReleaseAll()` | plugin unload |
| W0B-3 | src/Core/VistasWorldGen.cs (A1, later Compatibility) | `TerrainEdited += WorldTiles.EditedSource` (or call `WorldTiles.RaiseEdited(tile)` from GroundEdited) | WorldTiles "Edited" channel source |
| W0B-4 | Performance FsmRegistry (A1) | implement `IFsmIndexProvider` (Ready = Tracking, On = FsmRegistry.On) and `FsmIndex.SetProvider(...)` while tracking | FsmIndex.ByOwner exact PM1 answers |
| W0B-5 | Performance FsmEdits/FsmSleepFeature (A1) | switch to `Core.FsmPause` (FS1 uses bit `FsmPause.FsmSleep`), Awake postfixes call `FsmPause.ClearCopiedFlag` (or one Hooks.FsmAwake subscriber) | one ManualUpdate owner |
| W0B-6 | Performance CompileQueue users (A1) | use `Access.Queue`; set `Access.Queue.OnFirstBatch = () => Telemetry.Mark("compile")` | keep the Telemetry mark |
| W0B-7 | tools/refactor/compile.sh (W0-A) | delete the output dll before compiling (or test the mcs exit status) | with a reused outdir it prints `COMPILE OK` even when compilation failed (seen in W0-B) |
| W0B-8 | src/Core/Plugin.cs OnSceneLoaded (W0-A / A1), optional | WorldIndex, FsmIndex, PrefabCatalog, Clock and PlayerState subscribe `SceneManager.sceneLoaded` themselves (lazily, on first use); if a deterministic order relative to module `OnSceneLoaded` is wanted, replace with explicit calls | ordering |
