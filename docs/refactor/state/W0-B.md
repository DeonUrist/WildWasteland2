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
- [ ] 10. WorldIndex
- [x] 11. WorldTiles
- [ ] 12. FsmIndex
- [ ] 13. FsmPause
- [ ] 14. PrefabCatalog
- [ ] 15. ItemSpawner.RegisterSaved
- [ ] 16. Clock
- [ ] 17. Names
- [ ] 18. Layers
- [ ] 19. Factions
- [ ] 20. CameraArbiter
- [ ] 21. PlayerState
- [ ] 22. check_ownership + report docs/refactor/reports/W0-B.md

## Last good compile (commit)
9f509e4 (COMPILE OK; state line updated in next commit)

## Next action
Item 10 WorldIndex (uses WorldTiles.OnApplied).

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
