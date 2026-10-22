# W0-A Core kernel - state

Branch `refactor/W0-A`, worktree `/home/claude/wt/W0-A`.

## Checklist (brief steps)
- [x] 1. Move kernel files into `Core/<Area>/` folders (README 3.3), namespace unchanged
- [x] 2. ModApi, ModEvents, EntityId/Entities, IEventTransport, BroadcastAttribute; ModuleHost.Cleanup calls RemoveAll
- [ ] 3. All contracts + events of core-modapi.md 8-9; Services/QuestServices interfaces moved; Services.* [Obsolete] forwarders
- [x] 4. Log levels/generic overloads, old wrappers [Obsolete]; Log.Error no SaveSystem.Fail; SaveBus.ReportFailure; Trace stub; [Debug] VerboseLevel
- [ ] 5. Scheduler (Removed, phase Interval, name prefix warn); Hooks table-driven, FsmEnable removal fix, Calls all kinds, timing behind Trace.On, new dispatchers
- [ ] 6. IModule ProvideApis/Settings; ModuleConfig.Migrate + Legacy.* obsolete no-ops; stop seeding settings.cfg from old main cfg
- [ ] 7. Delete SaveTypes old-assembly aliases; Sidecars registry + SaveBus.Key + typed SaveBus events
- [ ] 8. SettingsShell API (contribution path next to existing rows)
- [ ] 8b. Plugin.VERSION = "2.0.0"
- [ ] 9. Skeleton modules (Debug, Presentation, NPCAI, Gunplay, Vehicles, Loot, Tools, Developer, Compatibility, Saves) registered after existing ones
- [ ] 10. tools/refactor/*.py work against new layout
- [ ] Report docs/refactor/reports/W0-A.md, ownership check

## Last good compile (commit)
steps 1,2,4, COMPILE OK

## Next action
Step 3: contracts (src/Core/ModApi/Contracts/*.cs) and events (src/Core/ModApi/Events/*Events.cs); then Services forwarders.

## Blockers / questions for Denis
(none)

## Notes for a resuming agent
- Step 1 moved: Boot/{Plugin,Runner}, Modules/{ModuleBase,ModuleHost,ModuleConfig}, Scheduling/{Scheduler,WorldWork,LoadingProgress}, Harmony/{Hooks,Patches}, ModApi/{Services,QuestServices}, Game/{GameRefs,Origin}, Input/Hotkeys, Persistence/{SaveBus,SaveTypes}, Log/Log. Assets.cs and TunedSettings.cs stay (B01/A1).
- Namespace decision: ModApi/ModEvents/EntityId/Entities/contracts live in `WildWasteland.Core` (a `WildWasteland.Core.ModApi` namespace would shadow the `ModApi` class inside Core). Event structs go to `WildWasteland.Core.Events`.
- Log: [Debug] VerboseLevel (int 0/1/2) bound in Plugin; Log.Level = max(VerboseLevel, VerboseLog?1:0). Log.Error no longer calls SaveSystem.Fail; SaveBus.ReportFailure(owner,msg) still forwards to SaveSystem.Fail (B04 moves it) and raises FailureReported.
- W0-B creates src/Core/Util/** and src/Core/Game/{WorldIndex,WorldTiles,FsmIndex,FsmPause,PrefabCatalog,ItemSpawner,Factions,Clock,Names,Layers,CameraArbiter,PlayerState}.cs - never create those.
