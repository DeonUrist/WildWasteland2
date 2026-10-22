# Shared character driver — step 1

Source implementation completed on 2026-10-22 in the main checkout, without subagents. Gameplay acceptance remains deferred until all refactor work is merged.

## Result

Player and humanoid NPC animation now use the same `ModAPI.Character` + `CharPlan` implementation. Deleted `Body.Loco.cs` and the player-only graph builder, cloned upper rig, locomotion/turn/upper-rig timing, arm-lift/spine-twist implementation and strike sequence tables. `Body` retains the model, meshes/shadows/equipment, native hit-line alignment, capsule/view fitting and seat/traversal/crawl adapters.

`PlayerInputAdapter` observes native attack/reload/cocking/jump/kick/grenade inputs and feeds the common plan. Native reload phase, per-round insertion, measured first-person melee duration and timed throw release are inputs rather than a second driver. The common resolver retains the previous native `enemy_1_attack` melee fallback when the bundle lacks Melee; its fallback lookup is cached once per scene and shared by characters, never a per-frame resource scan.

The common graph builder owns the main animation layers and upper-body skeleton graph for both clients. The player main graph is manually evaluated once at the existing body placement point, with its unused native Animator controller removed/restored by the driver. The global character job skips this instance. NPCs retain their automatic graph and existing distance/visibility update tiers. Manual evaluation reconstructs the player pose before procedural changes, including when paused or returning from a disabled seat Animator.

Seat arms use the common plan's upper layer over the native seated pose. Crawl and climb clips remain temporary overlays on this same graph; ladders retain their existing pose/traversal lease. Seat transitions clear locomotion actions while preserving an active native throw/melee/reload clock. Traversal/crawl transitions reset transient actions and resynchronize the plan. Death suspends the shared graph before native ragdoll ownership, and scene/model/module teardown disposes it. Failed construction releases partial graph/rig objects. Character lifecycle events identify the player as `Entities.Player`; the presentation query reports its actual attachment and inputs.

## Source verification

- Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false --no-restore`: zero warnings/errors.
- Partial-method check: 55 declarations, zero missing implementations. Removed the previously orphaned optional Patrols `BindPoses` declaration/call; pose binding is already Presentation-owned.
- Summary check: zero missing summaries across the 13 changed animation/adapter source files.
- Source inspection: no player `PlayableGraph.Create`, `BuildMixamo`, `BuildUpperRig`, `LateMixamo`, duplicated punch/combo tables, or generic arm-lift/spine-twist driver remains in Body/player code. Main/upper-rig creation lives in the common Character implementation.
- `git diff --check`: clean.

No game, runtime harness, deployment, push or release was performed. A clean build verifies source integration, not Unity animation behavior or performance.

## Combined acceptance later

Check player/NPC locomotion and weapon kinds; crouch/turn/jump/landing; gun aim, magazine/per-round reload and pump; native melee chains and grenade/lance release across weapon changes; FP/TP fitting/shadows/equipment; seat aiming, turned pose and wheel arms; crawl/ladder/climb entry/exit; pause/resume, death, model/scene reload and module toggles. First-person fitting, root-motion sway, retargeted bones and clip blends still need that combined game acceptance.

Rider/animal presentation separation and the other Shared adapters remain step 2. Wave C namespace/forwarder/packaging work remains separate. This completes the requested common player/humanoid-NPC driver source step, not the full refactor.
