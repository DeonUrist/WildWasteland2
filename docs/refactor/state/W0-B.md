# W0-B Core utilities - state

Brief: docs/refactor/work-packages.md "W0-B Core utilities". Worktree /home/claude/wt/W0-B, branch refactor/W0-B.
Rule: new files only (src/Core/Util/**, src/Core/Game/{listed}.cs); do not modify existing files; no Core/Util/Assets.cs.

## Checklist
- [x] 1. Json (Core/Util/Json.cs)
- [x] 2. Glb (static mesh, gas-station superset)
- [x] 3. Gltf (skinned)
- [x] 4. Bindposes
- [x] 5. Audio (WAV decode + Level + LoadAsync)
- [ ] 6. Textures (Load + cache + Destroy on shutdown)
- [ ] 7. Access (one accessor facade)
- [ ] 8. KeyMap
- [ ] 9. GameBindings
- [ ] 10. WorldIndex
- [ ] 11. WorldTiles
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
02b292c (COMPILE OK; state line updated in next commit)

## Next action
Item 6 Textures: read the 18 LoadImage sites.

## Blockers / questions for Denis
(none yet)

## Notes for a resuming agent
- Compile: sh tools/refactor/compile.sh /tmp/w0b-build
- Name decisions are recorded here as they are made.
- Json: lenient CarTemplateFile parser (comments, trailing commas, line-number errors) + StoreJson writer; no name clash with module Json copies (compiled).
- Glb: verbatim gas-station copy (vertices/mesh names identical; colliderGeometry=false = StewPot/WarRig behaviour); added Load(byte[], name, dir) overload. GlbModel is public in Core.
- Gltf: verbatim PlayerBody copy; Load(path, owner) prefixes the sparse warning with the caller's module (PlayerBody/NPC). Bindposes: identical copies; added TryGet(bone, out Matrix4x4).
- Audio: DecodeWav = Guns/NPC Wav verbatim (broadest); LoadWav = VoiceAudio API (now accepts all formats); TryLoadWav = Rider (null on failure); NormalizePeak = Quests; Level/Process = NPC Women Level; LoadAsync(path, type, cb, decompress) returns cancellable Audio.Load.
