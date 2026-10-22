# Vehicle template loading budget — 2026-10-20 — Testing

Denis reported slow vehicle preparation during the loading screen and approved a loading-only budget increase while preserving gameplay limits, both reserve instances and FSM preprocessing.

## Cause and change

The cache previously used the same named 500 us scheduler slice during world loading and gameplay, additionally stopping at the shared Core frame budget (2 ms in the inspected live tools.cfg). PackStructure's yields are driven directly by the cache iterator; they do not impose fixed inter-part sleeps. The inspected prior runtime log finished 33 structural variants, each targeting two prepared reserves.

VehicleBuildCache now separates the existing one-step implementation from its scheduling entry points. The ordinary Step keeps the 500 us scheduler slice and shared frame limit outside loading. During SaveBus.Busy with neither Saving nor SaveSystem.Busy, a named Patrols.Vehicle load preparation job instead advances the same steps for a 12 ms slice per frame, independently of the shared Core budget. This follows the existing Towns loading burst approach without changing Core or Towns. An indivisible Unity clone or FSM preprocess can exceed the slice; the loop stops between operations.

Both reserve instances, dormant structure construction, native FSM preprocessing, native saved vehicle restoration and the existing final loading hold are retained. All existing availability, error and lifecycle paths remain in use. Scene/module reset clears the timing watches. No config entries, save formats, template files, asset changes or new hooks.

Two Patrols source files, 33 additions/one removal. Existing unrelated gas-station/Towns working-tree changes are retained in the build.

## Diagnostics

Always-visible start/completion logs, independent of verbose logging:

- `[Patrols] Vehicle load preparation started: 12 ms/frame budget; 2 reserves per variant.`
- `[Patrols] Vehicle load preparation finished: N structural variant(s); X ms elapsed, Y ms work.`

Elapsed time covers the preparation batch including intervening frames and waits for native references. Work is the summed time inside the loading slices. These measure template-cache preparation only, not native saved-object loading, saved crew preparation, terrain/towns, later vehicle activation or overall loading time. A later warm request can produce another batch. Runtime speedup remains unmeasured.

## Build and deployment

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: zero warnings/errors. Game closed, standard WW disabled manifest absent, active runtime assets checked. Copied only the staged DLL to the standard active WildWasteland folder; build/stage/installed MD5 all `471C6DFB64B9D1E793401A308FE3C20A`.

- Stage: `V:/Repos/Apocalypter/_releases/WildWasteland_vehicle-load_20261020_145022_694.dll`
- Previous DLL backup: `V:/Repos/Apocalypter/_releases/WildWasteland_vehicle-load-before_20261020_145022_694.dll` (MD5 `1AC948496B9DE66BD2290AA78170A3D0`)

No game launch, automation, harness, live config/save edits, commit, push or release.

## Denis's acceptance

1. Restart and load the same save. Vehicle preparation should advance faster, while the loading screen continues updating. Compare the completion timing lines across equivalent loads; no prior exact-duration baseline exists in the inspected log.
2. Require the new preparation-finished line and the existing `Load handoff ready: vehicle builds cached, saved patrol AI prepared; native vehicle/item deltas retained.` before play. Investigate any cache-failed or scheduler-exception lines.
3. Once playing, check ordinary traffic/template spawns still use prepared vehicles and preserve their normal behavior. Reload a saved damaged/stripped vehicle and confirm its existing parts, cargo and condition remain unchanged.
4. Repeat with a new world to cover initial generation. Actual load speed, loading responsiveness and gameplay frame pacing remain Testing until Denis checks them.
