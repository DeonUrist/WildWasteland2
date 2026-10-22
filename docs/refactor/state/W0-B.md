# W0-B Core utilities - state

Brief: docs/refactor/work-packages.md "W0-B Core utilities". Worktree /home/claude/wt/W0-B, branch refactor/W0-B.
Rule: new files only (src/Core/Util/**, src/Core/Game/{listed}.cs); do not modify existing files; no Core/Util/Assets.cs.

## Checklist
- [x] 1. Json (Core/Util/Json.cs)
- [x] 2. Glb (static mesh, gas-station superset)
- [x] 3. Gltf (skinned)
- [x] 4. Bindposes
- [x] 5. Audio (WAV decode + Level + LoadAsync)
- [x] 6. Textures (Load + cache + Destroy on shutdown)
- [x] 7. Access (one accessor facade)
- [x] 8. KeyMap
- [x] 9. GameBindings
- [x] 10. WorldIndex
- [x] 11. WorldTiles
- [x] 12. FsmIndex
- [x] 13. FsmPause
- [x] 14. PrefabCatalog
- [x] 15. ItemSpawner.RegisterSaved
- [x] 16. Clock
- [x] 17. Names
- [x] 18. Layers
- [x] 19. Factions
- [ ] 20. CameraArbiter
- [ ] 21. PlayerState
- [ ] 22. check_ownership + report docs/refactor/reports/W0-B.md

## Last good compile (commit)
8442471 (COMPILE OK; state line updated in next commit)

## Next action
Item 20 CameraArbiter: read the 3 camera projection writers (PlayerBody, Performance RenderShared GameCameras, ...).

## Blockers / questions for Denis
(none yet)

## Notes for a resuming agent
- Compile: sh tools/refactor/compile.sh /tmp/w0b-build
- Name decisions are recorded here as they are made.
- Json: lenient CarTemplateFile parser (comments, trailing commas, line-number errors) + StoreJson writer; no name clash with module Json copies (compiled).
- Glb: verbatim gas-station copy (vertices/mesh names identical; colliderGeometry=false = StewPot/WarRig behaviour); added Load(byte[], name, dir) overload. GlbModel is public in Core.
- Gltf: verbatim PlayerBody copy; Load(path, owner) prefixes the sparse warning with the caller's module (PlayerBody/NPC). Bindposes: identical copies; added TryGet(bone, out Matrix4x4).
- Audio: DecodeWav = Guns/NPC Wav verbatim (broadest); LoadWav = VoiceAudio API (now accepts all formats); TryLoadWav = Rider (null on failure); NormalizePeak = Quests; Level/Process = NPC Women Level; LoadAsync(path, type, cb, decompress) returns cancellable Audio.Load.
- Textures: TextureSettings expresses each old site exactly (defaults = Unity defaults); cache per (source,settings), negative cache, reloadIfChanged by write time, Release(owner) needs ModuleHost wiring (request W0B-1).
- Access (src/Core/Util/Access/, partial static class Access): Call/Get (NativeAccess), Read/Call(MethodInfo) (OptionalAccess), FastReflect builders verbatim, FieldBatch (BatchCompiler), Queue (CompileQueue; job name <owner>.AccessCompile, Telemetry mark via Queue.OnFirstBatch). Old classes untouched (Wave A makes them wrappers).
- KeyMap: identical copies ported. GameBindings: PlayerBody API + Held(); name->KeyAction cache per InputStorage instance/list/count (old Find was a linear scan per call). CleanHUD KeyCatalog is UI-specific and not merged.
- WorldTiles: OnPrepare/OnFinalized(worker)/OnApplied/OnMoved/OnEdited with owner, copy-on-write handler arrays, per-handler try/catch, worker errors logged on main thread; Edited source = VistasWorldGen.TerrainEdited += WorldTiles.EditedSource (Wave A wiring).
- WorldIndex: WatchName/WatchPrefix/WatchComponent<T>/Watch(filter) + PruneBelow; Found lists with sliced destroyed-sweep (Removed(id)); roots from scene load, WorldTiles Applied, QueueRoot, Rescan; Core sliced job 'Core.WorldIndex' 300us; self-subscribes SceneManager.sceneLoaded. NOTE: compile.sh prints COMPILE OK from a stale outdir on failure - always use a fresh outdir / grep 'Compilation succeeded'.
- FsmIndex: IFsmIndexProvider (PM1 registers in Wave A); ByOwner fallback = GetComponents filtered isActiveAndEnabled; ByName/All via Track(owner,name) and one sliced FsmList pass (256/step, 0.5 s) publishing atomically; Int/Float/String/Bool/Go var helpers.
- FsmPause: verbatim Performance logic + FsmSleep bit 8, dynamic Bit(owner, reason), Own/ReleaseAll(owner), Paused, ClearCopiedFlag (Awake postfix rule).
- PrefabCatalog: Patrols ScanExact name rules (exact, ES3 override, fold preferring ES3Prefab) + ById(prefabId) + AssetFsms + Item(name); sliced warm 'Core.PrefabCatalog', sync completion on early query; invalidated on scene load / ES3 count change (Refresh). Domain catalogs (Patrols kinds, Towns spawn profiles) stay in their modules on top of it.
- ItemSpawner: Counter/List(Items|Cars)/NextId/Name/RegisterSaved/Unregister; numbering IncrementFirst (5 copies) vs UseThenIncrement (Quests) selectable; list = exact name then contains rule; cached per instance.
- Clock: GameHours (Quests semantics, -1), Day, HourOfDay, Ready (TrafficReinforcements' Initialized check), Dark (Patrols DayLight verbatim); cached vars, throttled 1 s lookup, reset on scene load. Quests' QuestWorld.All fallback dropped (GameManager is always the owner).
- Names: Base (Quests), StripClone (SaveCleanup), RenderBase (RenderShared k>0+TrimEnd), RemoveClone (Nav), CloneNumber (LastIndexOf+TryParse), BaseEquals/BaseStartsWith non-allocating.
- Layers: named layer numbers + shared masks (bit-identical to the literals they replace); 12/13 unnamed.
- Factions: Stance enum, symmetric owned relations Set/SetPlayer/Relation/ToPlayer/Hostile, IFactionSource membership chain Of(go), RemoveAll(owner); no data until Quests/Patrols feed it (Wave B).
