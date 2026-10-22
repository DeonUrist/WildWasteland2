# W0-A requests (changes outside W0-A ownership)

1. **WildWasteland.csproj** (coordinator / Wave C): add `<NoWarn>$(NoWarn);CS0618</NoWarn>` to the main PropertyGroup until Wave C deletes the
   `[Obsolete]` forwarders. W0-A marks `Services.*`, `Log.Info/Always(string)/Warn(string)/Error(string)/Verbose`, `ModuleConfig.Migrate`,
   `Legacy.*`, `SaveBus.ReportFailure(string)` obsolete as the brief asks; there are ~770 old log calls and ~250 Services uses, so
   `dotnet build` would otherwise print hundreds of CS0618 warnings (compile.sh and build.sh already pass `-nowarn:618`).
2. **B04 Saves**: subscribe to `SaveBus.FailureReported` (or own `SaveBus.ReportFailure`) and remove the direct `SaveSystem.Fail` call
   in `src/Core/Persistence/SaveBus.cs` (marked `REFACTOR-REQUEST W0-A`). `Log.Error` no longer fails saves.
