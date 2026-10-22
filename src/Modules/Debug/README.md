# Debug capture and profiling

Integrated WW2 Debug module. Windows build:

```powershell
dotnet build WildWasteland.csproj -c Release -p:Deploy=false
```

The former standalone profiler is integrated into WW2's Debug module. Enable Debug, press F4 (rebindable), and choose a measurement or capture. No separate DLL, logger, runner or configuration is used. Debug owns the shared level 0/1/2 policy; detailed probes require level 2 and are removed when that level drops. Captures live under `BepInEx/config/WildWasteland/Debug/Captures/`.

F4 opens a rebindable, non-pausing menu. Overview gives frame pacing, GC/render counters and scheduler costs; other tabs show jobs, managed method timings, module state, per-system trace controls, collection counts and existing WW developer tools. Logging follows the shared Debug level; no separate logger or per-system log switch is created. Opening an original WW editor enables WW Debug/Developer visibility flags.

## Capture for an LLM

1. Start with the cheap overview. For attribution, open **Systems & tracing**, install WW entry points. Optional **Discover BASE GAME probes** adds Game.PlayMaker, Game.Actions, Game.Scripts, Game.Terrain and Game.Save scopes from loaded managed assemblies. Install only relevant native scopes first. Deep mode is for a focused suspect system.
2. Hooks install one method per frame and may hitch. Wait until the queue is empty. Hundreds/thousands of hooks can take time; install at title/pause, then resume and warm up. No method timing runs during installation. Patch failures are exported.
3. In **Logs & capture**, label the scenario, e.g. `Camp_7 combat, Performance ON, same save/camera`. Click **Capture A**. Menu closes, waits five seconds, resets measurements, records thirty seconds, exports and retains A in memory. Play normally during the recording.
4. Change the Performance module yourself through WW settings. Verify its actual state. Repeat the same warmed-up location, camera and action with the same installed probes. Label it and click **Capture B**. It exports B together with A and a comparison. Restart loses the in-memory baseline; exported captures remain comparable independently.
5. Provide the capture folder to the LLM. Start with `analysis.md`, `capture.json`, `builds.txt` and `comparison.md` if present. All files are under `BepInEx/config/WildWasteland/Debug/Captures/<UTC-unique-id>/`; completion writes the exact path to the plugin log and menu.

`capture.json` includes a schema version, units in field names, frame percentiles, GC, hardware/render settings, all bound WW/developer settings, actual/requested module state, installed plugins, method totals since reset, latest scheduler/counter snapshot, coverage/failures and limitations. `baseline-A.json` preserves A. Additional CSVs contain frames, scheduler jobs, method rates and cumulative raw ticks; trace.log retains the latest 512 lines, with bounded event queues and drop counts. The default is no continuous logging and no installed method hooks.

Manual **Reset / Export** and monitor pause/resume also work. Optional summaries, slow calls and lifecycle logging use selected scopes' Trace checkbox. Exception type/message is captured for instrumented methods. Optional thread allocation timing adds cost and may be unavailable. Collection counts are a manual reflection snapshot; they can initialize the selected type and are structural clues, not CPU times.

## Interpretation

Inclusive times overlap; do not add methods/jobs together. Self time subtracts only instrumented children. Main/worker self times are separate. Coroutine/async MoveNext measures CPU execution segments, not waiting. Some small/inlined/native/generic calls cannot be hooked; missing Unity counters remain unavailable. Raw method totals are since hook installation; JSON totals are since reset. Job maxima/errors are WW lifetime values. Frame percentiles cover the last 8192 frames; JSON flags longer windows. Scheduler/counter rates are latest snapshots, not whole-run averages.

Captures warn about short windows, dashboard/paused frames, state transitions, coverage changes and current settings differing from reset. They cannot detect every transient setting change or prove that two combat encounters had identical activity. Instrumentation and logging add overhead; compare runs with matching probes, allocation settings and logging. Native callers can include WW Harmony callbacks; measured native entry cost is not automatically pure base-game cost. Unattributed time must never be declared vanilla CPU cost by subtraction.

Performance OFF still runs other WW modules. A true WW-disabled baseline needs an external profiler because the Debug module is part of WW. No game launch, gameplay verification or measured performance result is implied by a successful build.
