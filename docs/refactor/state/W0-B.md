# W0-B Core utilities - state

Brief: docs/refactor/work-packages.md "W0-B Core utilities". Worktree /home/claude/wt/W0-B, branch refactor/W0-B.
Rule: new files only (src/Core/Util/**, src/Core/Game/{listed}.cs); do not modify existing files; no Core/Util/Assets.cs.

## Checklist
- [ ] 1. Json (Core/Util/Json.cs)
- [ ] 2. Glb (static mesh, gas-station superset)
- [ ] 3. Gltf (skinned)
- [ ] 4. Bindposes
- [ ] 5. Audio (WAV decode + Level + LoadAsync)
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
5333abe (base, COMPILE OK)

## Next action
Item 1 Json: read all 8 copies.

## Blockers / questions for Denis
(none yet)

## Notes for a resuming agent
- Compile: sh tools/refactor/compile.sh /tmp/w0b-build
- Name decisions are recorded here as they are made.
