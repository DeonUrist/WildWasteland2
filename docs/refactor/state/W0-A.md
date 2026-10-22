# W0-A Core kernel - state

Branch `refactor/W0-A`, worktree `/home/claude/wt/W0-A`.

## Checklist (brief steps)
- [ ] 1. Move kernel files into `Core/<Area>/` folders (README 3.3), namespace unchanged
- [ ] 2. ModApi, ModEvents, EntityId/Entities, IEventTransport, BroadcastAttribute; ModuleHost.Cleanup calls RemoveAll
- [ ] 3. All contracts + events of core-modapi.md 8-9; Services/QuestServices interfaces moved; Services.* [Obsolete] forwarders
- [ ] 4. Log levels/generic overloads, old wrappers [Obsolete]; Log.Error no SaveSystem.Fail; SaveBus.ReportFailure; Trace stub; [Debug] VerboseLevel
- [ ] 5. Scheduler (Removed, phase Interval, name prefix warn); Hooks table-driven, FsmEnable removal fix, Calls all kinds, timing behind Trace.On, new dispatchers
- [ ] 6. IModule ProvideApis/Settings; ModuleConfig.Migrate + Legacy.* obsolete no-ops; stop seeding settings.cfg from old main cfg
- [ ] 7. Delete SaveTypes old-assembly aliases; Sidecars registry + SaveBus.Key + typed SaveBus events
- [ ] 8. SettingsShell API (contribution path next to existing rows)
- [ ] 8b. Plugin.VERSION = "2.0.0"
- [ ] 9. Skeleton modules (Debug, Presentation, NPCAI, Gunplay, Vehicles, Loot, Tools, Developer, Compatibility, Saves) registered after existing ones
- [ ] 10. tools/refactor/*.py work against new layout
- [ ] Report docs/refactor/reports/W0-A.md, ownership check

## Last good compile (commit)
(none yet)

## Next action
Step 1.

## Blockers / questions for Denis
(none)

## Notes for a resuming agent
- W0-B creates src/Core/Util/** and src/Core/Game/{WorldIndex,WorldTiles,FsmIndex,FsmPause,PrefabCatalog,ItemSpawner,Factions,Clock,Names,Layers,CameraArbiter,PlayerState}.cs - never create those.
