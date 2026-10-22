# NPC module performance review (0.1.x snapshot /home/claude/ww/now, 2026-10-14)

Scope: read-only review of `src/Modules/NPC/**` against the measurement Denis took (F7 Performance after a raid on a raider camp, 6301 frames, ~70 fps: whole mod 0.93 ms/frame avg, worst frame 332 ms; NPC 0.485 avg / worst job 268 ms; Senses 0.008 avg / 9.6 worst, Nav 0.195 / 18.6, Brain 0.062 / 61.9; the 268 ms job was below the fold). Settings in play: `[NPC / Nav] CellSize 0.5, FieldSeconds 1, BakeBudgetMs 1`, `[NPC / Debug] VerboseLog = true, ShowNavigation = true`, `[NPC] ScaleWithActors` default **false** (NpcModule.Config.cs:107), log full of `Nav: <npc> has no map route to its goal on Camp_7(Clone) (goal inside, cell ..., its area 29269 cells, 26087 under open sky, ...)` about once a second per NPC.

Line numbers refer to the snapshot. "Build" below always means `Nav.Build` (Nav.cs:1310), the full-grid Dijkstra.

---

## 0. Summary of findings

1. The NPC module has **no `Scheduler.Sliced` job at all** (NpcModule.cs:82-98): everything is `EveryFrame` with internal gating. Nav baking and nest building have their own Stopwatch budgets; the route computations (`Nav.Build`, `ExitCell`, `PickOutExit`) have none and run to completion inside a Brain think or an Idle leg think.
2. On a 29 269-cell map one `Nav.Build` is a Dijkstra over ~26 000 reachable cells x 8 neighbours with ~20 `EdgeKind` calls per cell (Nav.cs:1325-1343, 640-654): **~15-25 ms on Mono**. The "no map route" path costs 2-3 Builds + a full-grid scan per cache miss (Nav.cs:752-767, 1061-1076); the outside-goal path (`PickOutExit`) costs 1 Build + an `Array.Sort` of all reachable cells + **up to 6000 Bresenham walks** (`OutClear`, Nav.cs:987-1006), i.e. up to a few hundred ms.
3. The 268 ms spike is almost certainly the **Idle** job (first row below the fold), reached through `Idle.LegThink -> Nav.Next -> OutNext -> PickOutExit` for raiders walking (home / to a gunshot ghost / to a flank point) toward a goal outside the camp footprint (section 1.4). Brain's 61.9 ms is the same machinery (2-3 Builds in one think) or the one-off `ReloadSounds.ClipsOf` scan of every PlayMakerFSM in memory (ReloadSounds.cs:26).
4. The repeated "no map route" line is `ExitCell` (Nav.cs:1101-1104): per (structure, NPC area, 4 m goal bucket) every 2 s, plus every time the player crosses a 4 m bucket. Each repeat = 1 Build from the NPC's cell + a scan of all cells; the goal field (1 s, 2 m bucket) and the exit field (1 s) are rebuilt independently. A negative cache is not even needed: the map's connected-component ids already answer "unreachable" in O(1) (section 2).
5. Settings Denis can apply today with no behaviour change: `ShowNavigation off`, `VerboseLog off` (and make sure `NavDump` is off). Settings with a small behaviour change that cut the spikes: `FieldSeconds 1 -> 3`, `ScaleWithActors on`. `CellSize` is the only lever on the Build cost itself but it changes which gaps the map sees (section 3).
6. Code: the component-id short cut (5 lines), caching the NPC-side field in `ExitCell` via `FieldFor` (2 lines), lazy Dijkstra for route fields (~40 lines) and replacing the two `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` scans are the high-value, behaviour-preserving changes (section 4).

---

## 1. Per-job cost model

Registration order (NpcModule.cs:82-98) = the F7 "AI overview > Frame time" row order: Senses, Nav, Brain, Idle, Passthrough (0.25 s), Burrow, Pounce, Webs, Nests, Ambush (0.5 s), DogBite, Aim.Sweep (10 s), WeaponRanges.Sweep (30 s), Burrow.Late, Pounce.Late, DogBite.Late, Overlay (Gui, enabled only with ShowNavigation). So "below the fold" starts at **Idle**.

Hook/patch handlers (Senses.BeforeGetDetections, Brain.BeforeSetVelocity, DogAnimations.BeforeDamageEvent on `Fsm.ProcessEvent`, ...) are **not** in any job's time: they run inside PlayMaker's own update. NPC registration (`Senses.Make` Senses.cs:1488-1534 with 3x `GetComponentsInChildren` + reflection; `Brain.Make` Brain.cs:1602-1640; `DogAnimations.RegisterRig` with LINQ, DogAnimations.cs:126-162) happens there, so a spawn wave costs frame time that F7 attributes to nothing.

### 1.1 Senses (Senses.cs:121-178) - measured 0.008 avg / 9.6 worst

| When | What | Scales with |
|---|---|---|
| every frame | loop over all agents, skip unless `now >= a.NextLook`; `Thrown()` reads one FSM state string | agents (cheap) |
| per agent per `LookInterval` 0.15 s (+ stagger) | `Look`: player candidate = 1 cone test + up to 2 raycasts (`Visible`, 281-315); then **every other agent** of a hostile faction within SightRange: distance + cone + up to 2 raycasts (216-224, only when `HostileNpcAround`) | agents, and agents^2 when two factions that hunt each other are loaded (raiders vs coyotes / mutants) |
| once per second | maintenance: `Storm.Refresh` (Storm.cs:90-130, `GetComponentsInChildren<Transform>` once per new storm), `FindPlayer`, `Relation` per Coyote, `RefreshSpiderLights` (Senses.Spiders.cs:37-66: walks **ArrayList_Items**, every item in the world, calling `PrefabOf` - the `_prefabOf` cache is cleared above 4096 entries (Senses.cs:1652), so with > 4096 live items it thrashes and allocates a `go.name` string per item per second) | items in the world (only with spiders registered) |
| per gunshot / blast / shout | `Noise` (773-825): two agent loops, a `List.Sort` with a closure, a `new HashSet` when there are more responders than `GunshotResponders`, 1 ghost GameObject; `BeforePlayerShot` (VanillaEvents.cs:107-134) does `GetComponents` + `GetComponentsInChildren<Transform>(true)` on the weapon per shot - only when `WeaponRanges.ProjectilesEnabled` is false (Guns module off) | agents |

Expensive calls: raycasts per look (fine), `Resources.FindObjectsOfTypeAll(_azureType)` every 10 s until the Azure clock is found (Senses.cs:473 - typed, cheap), `Resources.FindObjectsOfTypeAll<GameObject>()` in `FlexaPrefab` (Senses.cs:1132: **every GameObject in memory**, hundreds of ms) - only on the player's shout, once per 30 s until found, and it runs in Core's hotkey job, not here. Log calls: most `Log(a, "..." + ...)` build the string **before** the `SensesLog` check (e.g. 199, 514, 538, 558) - allocation on state changes only, not per look.

Verdict: cheap, scales linearly; nothing here produced the spikes. The 9.6 worst is a maintenance second (storm GetComponentsInChildren or the item walk).

### 1.2 Nav job (Nav.cs:95-111) - measured 0.195 avg / 18.6 worst

The job only discovers and bakes; routing is called from Brain/Idle and is billed there.

| When | What | Scales with |
|---|---|---|
| every frame, forever | `SweepStep` (180-223): pops up to **250 hierarchy nodes per frame** (GetInstanceID, dictionary lookup, `childCount`, `GetChild` push per child; `t.name` + `GetComponent<Rigidbody>` only for nodes not yet in `_nodeKind`); a full pass over every scene root to depth 4, then a 2 s pause, then again | scene size (not NPC count). ~0.15-0.25 ms/frame on its own = most of the 0.195 avg |
| every 0.5 s | `Pick` (128-157): loop over known structures | structures |
| every frame while a structure bakes | `BakeStep` with budget `BakeBudgetMs` x0.5..x4 (159-172): per cell up to 32 raycasts + 2 `CheckCapsule` + 1 raycast (`Floor`, 491-516); edge pass 4 x (linecast + capsule cast [+ capsule cast]) per cell (338-378); component flood fill (423-458) | grid size, bounded per frame |
| when a structure is found | `Make` (269-289): `GetComponentsInChildren<Collider>(true)` on the whole camp; `Scan` recursion with `t.name` per node | per structure, one-off; `_structures.RemoveAll(lambda)` every pass (non-capturing, no alloc) |

The 18.6 worst = a frame in which several camps were found (Make + Scan) or the x4 "Needed" bake budget overshooting on cave cells (32-ray columns).

### 1.3 Brain (Brain.cs:152-264) - measured 0.062 avg / 61.9 worst

| When | What | Scales with |
|---|---|---|
| every frame, every registered NPC (also Mode.Off, also far away) | `SimpleBandits.Has` (dict), `Engaged` (`fsm.ActiveStateName`, 295-304), `TryRig` throttle, **`Drive`** when the NPC has an Apocaplayer body (1138-1200: `ApBody.Frame`, 1 raycast when `|vy| > 0.5`, `WeaponRanges.WeaponOf` when `WeaponDirty` or no weapon found yet every 0.5 s - see below), `ReloadSounds.Tick` while reloading, `RotateTowards` | registered NPCs (every ground NPC that ever ran its Movement FSM, Brain.cs:1700-1708) |
| once per second | `_nextCount` sweep: dead removal, `TickScale` by camera distance (171), `_interval` (176: 0.1 s unless `ScaleWithActors`) | NPCs |
| per think = `_interval x TickScale` (0.1 s / 0.2 / 0.4) per **engaged** NPC | `Think` (306-401): `Senses.KindOf`, cover checks (3 s), `LineOfSight` 1 linecast (ranged), then `MoveToward` (404-463): `Blocked`, **`Nav.Next`** (routing, see 1.6), `HopAhead`, `NavLegClear` 1-2 casts, `NavSteer` 1-5 capsule casts + `StepOver` 1-2 rays, or `Steer` (731-890): `OverlapCapsuleNonAlloc` + `TouchDir` per touching collider (`ComputePenetration` for mesh colliders) + **FeelerCount (7) CapsuleCasts** + DropCheck 1-3 rays + `Scout` (989-1027: 12 capsule casts x up to 4 rays, at most every 0.5-2.5 s when the straight line is blocked) + `AvoidFriends` (899-952: loop over **all agents**, 0-2 capsule casts) | engaged NPCs; `AvoidFriends` O(agents) per think -> O(n^2)/interval |
| per stuck | `LogAhead` (BrainLog only: `RaycastAll` + `OverlapCapsule` + StringBuilder, 1357-1390) | |
| first reload per weapon key per scene | **`ReloadSounds.ClipsOf`** (ReloadSounds.cs:18-51): `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` = every FSM in memory incl. prefabs, with `f.gameObject` / `scene.IsValid()` native calls per element; cache cleared on scene load | FSMs in memory (tens of thousands): **30-100 ms, one-off per key** |
| per weapon toggle | `AfterActivate` (1287-1298) marks `WeaponDirty` on **any** ActivateGameObject of the NPC's FSMs (muzzle flash on/off every burst) -> next frame `WeaponRanges.WeaponOf` (WeaponRanges.cs:81-100): `GetComponentsInChildren<Transform>(true)` on the whole NPC + a `Substring` per bone name | gunmen x bursts; also every 0.5 s per gunman holding nothing recognised (Brain.cs:1144) |

Allocations per think: `Nav.NearestReachable`'s sort lambda captures `s, x, z` (Nav.cs:604 - only when the NPC's own cell is blocked); `SetMode` reason strings are correctly gated by `BrainLog` (337, 368, ...); `Trace` lines only with NavDump + VerboseLog.

The 61.9 ms worst is either one `ReloadSounds` scan (the first NPC reload of a weapon kind in the session) or a think with 2-3 Builds (section 1.6); both fit.

### 1.4 Idle (Idle.cs:169-274) - below the fold; the prime suspect for 268 ms

| When | What | Scales with |
|---|---|---|
| every frame, every agent | state classification, `SimpleBandits.Has`, `RunInPlace` every 0.5 s for standing NPCs (`GetCurrentAnimatorStateInfo`), **`Drive`** for every moving walker (557-578): animator state check + **1-5 `CapsuleCast`** (first free heading breaks, so 1 cast in the open, 5 when blocked) + rotation + velocity | walkers per frame |
| per 0.2 s per walker (home / patrol / search round / **ghost walk**) | `LegThink` (402-428) -> **`Nav.Next`** (409) -> same routing as the brain, including `OutNext`/`PickOutExit` for goals outside the footprint; `Hop` 2 rays | walkers |
| once per NPC at home | `Nav.PatrolPoints` (Nav.cs:1522-1559): 16 bearings x up to 5 distances, each a cell walk + `CapsuleCast` (<= 80 casts) + `new List<KeyValuePair>` | one-off, retried every 10 s up to 30x while the map is not baked |
| per search start | `Nav.SearchPoints` (1609-1643): patrol points, or 12 x 3 x (2 rays + capsule sweep + linecast) | per search |

Why Idle is the 268 ms candidate: in a raid the raiders inside the camp are sent to **gunshot ghosts** (`GunshotResponders 4`, plus everyone within 25 m) and **flank points** (`FlankDistance 15`, FlankWidth 10 m off the line, Senses.cs:670-682), which usually lie **outside the camp footprint** (the player shoots from outside). The walk to a ghost is Idle's (Brain goes to Mode.Walk, Brain.cs:322-327; Idle.InvestigateThink -> LegThink every 0.2 s). For an outside goal `Nav.Next` takes `OutNext` (Nav.cs:912-955) and, on the first think, on every goal move > 8 m, whenever the NPC drifts off its clear line (`p.Out`), and whenever the exit field does not reach the NPC (`attempt > 0`), it calls **`PickOutExit`** (979-1017):

- `Build(s, goal, true, from)` - a full Dijkstra from the NPC's cell: ~15-25 ms here;
- keys for every reachable big-area cell + `Array.Sort(_ocKeys, _ocIdx, 0, cnt)` of ~26 000 floats: ~3-5 ms;
- `OutClear` (892-910) for the cells in cost order until one has a clear straight line to the goal, **up to `MaxOutTests` = 6000** walks; each walk is a Bresenham line to the footprint edge (up to ~W+H = 340 steps on this map) with `Move` = 1-3 `EdgeKind` per step. Inside a walled camp or a cave with the goal behind the wall, thousands of cells fail: 6000 x ~150 x 2 x ~40 ns = **~70-120 ms**, plus the Build and the sort = **100-250 ms in one call**. The memo (`_outMemo`, 5 s, keyed by structure / area / 8 m goal bucket) is bypassed and not written when `fresh` is true (986, 1007).

The ghost moves every time the player shoots from a new spot (Noise merges per source within 1 s, Senses.cs:738-744), so `p == null` (goal moved > 8 m, Nav.cs:918) or a new memo bucket is the normal case during a raid. Several walkers in one frame compound it (Idle thinks are staggered only by `(_stagger % 10) * 0.02 s`, Idle.cs:182). Two or three such picks in one frame = the 268 ms. (Brain's `MoveToward` takes the identical path for a chasing NPC whose target stands outside the footprint; its 61.9 ms worst shows that at least the cheaper variants happened there too.)

Other below-the-fold candidates, ranked lower:
- **Burrow** (Burrow.cs:126-166, Late 355-368): per burrower per frame cheap; but `FindEffect` (371-394) = `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` on the first puff within 45 m of the camera, retried once a minute while the `Burrower_Effect` prefab is not in memory; cache dropped on every scene load (46). Same magnitude as the ReloadSounds scan (30-100 ms), one-off.
- **Nests** (Nests.cs:162-188): `Build` is budgeted (1 ms) but checked only between steps; one step can be `Webs.Orb` (Webs.cs:118-187: ~20 rays + up to ~2000 strips + `Mesh` creation + `UploadMeshData`) or `Webs.Sheet` (221-269: 81 rays + O(45^2 x 3) nearest-knot search + mesh) = 2-6 ms; `Remember`/`SaveLayouts` writes the whole layout file synchronously when a nest finishes (86-95, 327); first web ever: `Shader.Find` + texture creation. Only with spiders at the place: not a raider camp.
- **Overlay** (Gui job, DebugOverlay.cs:43-49): with ShowNavigation on, every repaint builds a label string + draws 2 `GUI.Label` (shadow + text) + a `GUI.DrawTexture` per ghost (Senses.cs:1710-1715, no distance cull), per non-idle agent (1716-1730, no cull), per idle NPC within 200 m (Idle.cs:589-627) and per structure within 150 m (Nav.cs:1697-1717, `new List<GameObject>()` per frame at 1709). 30 NPCs + 10 ghosts = ~100 labels = 1-4 ms/frame of pure overhead plus a few KB of garbage per frame; first use of a new glyph size rebuilds the font texture (one-off 5-20 ms).
- **DogBite / DogAnimations** (DogBite.cs:74-100, DogAnimations.cs:376-471, 596-618): per animated dog per frame: `Eligible` (several native checks), `Grounded` 1 ray when idle, in combat `TargetPoint` + `InReach` (263-276) **twice** (TryAttack and the `close` test at 465), each doing `target.GetComponent<Collider>() ?? GetComponentInChildren<Collider>()`, `ClosestPoint`, 1 linecast; LateTick: `SampleAnimation` on the whole hierarchy + 2 bone loops; lying dogs within 60 m: 2-4 rays (`LevelFront`, `PawsOnGround`). ~0.1-0.3 ms per dog per frame; linear in dogs, never a 268 ms spike.
- **Passthrough** (0.25 s): O(n^2) pair loop over agents with cheap tests + `Nav.NearWall` (BakedAt loop) per agent; `Physics.IgnoreCollision` per collider pair on new pairs and re-applied for all pairs every 3 s. Negligible at camp sizes.
- **Webs**: per frame `Flash(false)` loops all webs (cheap); `Tint` 1 s. Negligible.
- **Pounce**, **Ambush**: 0.2 s / 0.5 s gated, a few casts. Negligible.

### 1.5 Memory / GC

Per-frame garbage with the current settings comes from: overlay strings and the `dead` list (ShowNavigation), log strings (VerboseLog), `WeaponOf` arrays + ~150 `Substring`s per call, `Nav.SweepStep` names for new nodes, `NearestReachable`'s closure, `Noise`'s closure/HashSet per shot, `PatrolPoints`/`SearchPoints` lists. Field arrays are pooled (`Rent`/`Return`, Nav.cs:54-67; 8 per size) and the Dijkstra heap is shared (1315), so Builds themselves do not churn. Mono GC pauses land in whichever job allocates the trigger, which is one way a 150-330 ms worst can show up in PlayerBody and Patrols as well - but the 268 ms in NPC has a sufficient direct explanation (1.4).

### 1.6 Routing cost per `Nav.Next` call (Nav.cs:715-795), shared by Brain and Idle

Always: loop over all structures with `Inside`/`NearestReachable` (usually the own cell), `CellOf`, `NearestWalkable` 9x9, `GridSight` (a cell walk). Cheap.

Then one of:
- **goal inside, reachable**: `FieldFor` (1286-1307): cache hit = dictionary lookup; miss (field older than `FieldSeconds`, or the goal moved to another 2 m bucket, or the bucket's cached goal cell is not within 2.9 cells by path) = **1 Build** (expands the goal's whole component, even if the NPC stands 3 cells away). `Downhill` x16 + `GridSight`.
- **goal inside, not reachable** (the log line): `FieldFor` miss = 1 Build over the goal's component (small when the player stands on a platform/roof - that is what "its area 29269 cells" vs an inside goal means: the goal cell belongs to another component of >= 30 cells); `ExitCell` (1050-1113) memo miss = **1 Build from the NPC's cell over the NPC's component (~26 000 cells)** + a scan of all cells + the log line (+ a relaxed Build when the area has no open sky and no ring cell - not this case); then `FieldFor(exit cell)` miss = **1 Build from the exit cell**. Memo lifetimes: goal field 1 s / 2 m bucket, exit memo 2 s / 4 m bucket (only when an exit was found), exit field 1 s.
- **goal outside**: `OutNext` -> `OutClear` from the own cell (one line walk) -> if blocked: `PickOutExit` (above) when the plan is missing/stale/fresh, then `ExitField` (959-976, 1 s cache, 1 Build on miss), `Descend`.

Cost drivers: **grid size** (n = W x H; a Build visits every reachable cell regardless of where the NPC is; `PickOutExit` touches every reachable cell at least twice), and the **number of distinct (structure, area, goal bucket) combinations** alive at once; NPC count only through how many of them miss the shared caches in the same frame. Camp_7 at CellSize 0.5 m is 29 269 cells (~85 x 85 m incl. the 4 m margin): a Build here is 15-25 ms in Mono; at CellSize 0.75 it would be ~13 000 cells.

---

## 2. The "no map route" repeat: what is redone, and the cheapest correct fix

Trace for one failing NPC (goal inside the footprint, in another component than the NPC): `Brain.Think` every `_interval x TickScale` (0.1 s at < 40 m from the camera) -> `MoveToward` -> `Nav.Next`:

1. `FieldFor(s, goal, true, goalCell)` (752): the field from the goal cell. Rebuilt every 1 s (`FieldSeconds`) and every time the player moves to another 2 m bucket. Expands only the goal's component - cheap if that is a small platform, full price if the player stands in the big area and the **NPC** is the one on an island.
2. `d0 = inf` -> `ExitCell(...)` (761): memo hit only if the same (structure, NPC area, 4 m goal bucket) was answered < 2 s ago **and** an exit was found. Miss: `Build(s, goal, true, from)` - **full Dijkstra from the NPC's cell over the NPC's area (26 087 open + roofed = 29 269 cells)**, the n-cell scan computing `d + |goal - c|` for every reachable cell, the `NavLog` line (string with ~25 concatenations, written through BepInEx to console + file), `Return(mine)`. One per NPC area per 2 s, more while the player moves: the line in Denis's log "every second or so for several NPCs" = several NPCs in different areas/structures (camp + its cave, a hut interior with its own component) and a moving player.
3. `FieldFor(exit)` (765): the field from the chosen exit cell. Rebuilt every 1 s and whenever the exit changes (it follows the player). Full component expansion.
4. `GridSight`/`Downhill` - cheap.

So per failing NPC-area and per second: **2-3 full Builds (30-75 ms) + 1 full-grid scan + 1 log line**, repeated for as long as the player stays where the map cannot reach him; `FieldSeconds` is the only knob on it today and only halves it at best. Nothing of this changes the answer: the map does not change between repeats, and the answer only depends on (NPC area, goal cell).

Cheapest correct changes, no behaviour change:

a) **Component short cut** (Nav.Next, before line 752): the bake already computes connected areas with the exact same step rule as Build (`ComponentsStep` uses `Move` = `Step` + the two corner `Step`s, Nav.cs:447 / 701-707; Build uses `Step` + the same corner checks, 1336-1337; neither is relaxed or no-hop at that point). Therefore `s.Comp[from] != s.Comp[goalCell]  <=>  f.Dist[from] == +inf`. Add: `if (s.Comp != null && s.Comp[from] != s.Comp[goalCell]) { d0 = inf; f = null; } else { f = FieldFor(...); d0 = f.Dist[from]; }` and let the existing `if (float.IsInfinity(d0))` branch run. Step 1 disappears entirely for the failing case. (~5 lines, pure.)

b) **Reuse the NPC-side field** in `ExitCell` (1061) and `PickOutExit` (988): `Build(s, goal, true, from)` ignores `goal` when `inside` is true; it is exactly the field `FieldFor(s, pos, true, from)` would cache under the NPC's own cell bucket - shared by every NPC standing in the same 2 m bucket and across goal-bucket changes for `FieldSeconds`. To keep it bit-identical use an exact variant (`FieldFor` with a flag that requires `f.GoalCell == from` instead of the 2.9-cell tolerance, or a second dictionary key space `(3L << 40) | from`). Then step 2's Build runs once per NPC cell per `FieldSeconds` instead of once per (area, goal bucket) per 2 s, and the memo stays as it is. Remove the `Return(mine)` calls accordingly (the cache owns the array). (~6 lines, pure.)

c) **Exit fields never go stale**: a field seeded at one fixed cell (`FieldFor(.., true, exit)` at 765, `ExitField` at 959) depends on nothing but the map. Give fields whose goal is an exit cell the memo's lifetime (2 s for `_exits`, 5 s for `_outMemo`) or simply `max(FieldSeconds, 5 s)`; the 16/32-entry cap per structure still bounds memory. (~3 lines, pure: the same cell gives the same distances.)

d) Optional negative cache for the **remaining** work (the n-cell scan in `ExitCell` after the memo expired with an unchanged goal bucket): not needed once (a)-(c) are in; the scan is ~1 ms.

With (a)-(c) the failing case costs one Build per NPC cell bucket per `FieldSeconds` plus dictionary lookups; the log line stays once per memo miss (and is silent with VerboseLog off).

---

## 3. Settings-only recommendations (F7 / F6, today)

Pure cost, no behaviour change:

| Setting | Now | Set | Effect |
|---|---|---|---|
| `[NPC / Debug] ShowNavigation` (F7 > NPC AI & navigation > AI overview / Debug) | **true** | false | disables the `NPC.Overlay` Gui job (NpcModule.cs:99,109): ~1-4 ms per frame with 30 NPCs and 10 ghosts, the per-frame label strings and `new List` (Nav.cs:1709), the `Remember` dictionary writes (1390-1394). Nothing else reads it. |
| `[NPC / Debug] VerboseLog` (= `NavLog`, `BrainLog`, `SensesLog`, NpcModule.Config.cs:321) | **true** | false | no "no map route" / "way out of" / Brain state / Senses lines: each is a 20-30 part concatenation plus a BepInEx console + disk write (the console path is the slow part on Windows); `Nav.Probe` raycasts + StringBuilder when the brain's nav reason changes (Brain.cs:433-437); `LogAhead` RaycastAll/OverlapCapsule on every stuck (Brain.cs:1441, 1475); the `Trace` lines. Keep it off except when reproducing an issue. The global F6 `[Debug] VerboseLog` has the same effect on these lines (LogAdapter, NpcModule.cs:41) - check both. |
| `[NPC / Debug] NavDump` | ? | false | with VerboseLog it adds a per-second `Trace` line per moving NPC and 0.5-4 MB BMP writes (`Dump`, Nav.cs:1121-1200) on every no-route (per structure per 20 s) and on every chase end. If it is on, this alone explains multi-hundred-ms frames. |
| `[NPC / Senses] StormRadius` 2500 | - | leave | pure arithmetic (Storm.cs:147-164), no cost. |
| `[NPC / Nav] BakeBudgetMs` 1 | - | leave (0.5 if bakes visibly stutter) | caps the Nav job's per-frame bake slice; x4 when an NPC waits for the map; no effect on routing cost. |
| `[NPC / Nests] FrameBudgetMs` 1, `BuildRange` 520, `FreeRange` 750, `SpotsTried` 70 | - | leave | only matter with spiders at a place; the budget is honoured between steps (one web mesh can overshoot by a few ms). Lower `BuildRange` only delays when nests appear. |

Behaviour-affecting, worth it for a raid:

| Setting | Now | Set | Effect / cost |
|---|---|---|---|
| `[NPC / Nav] FieldSeconds` | 1 | 2-3 | Builds for a goal that stays in the same 2 m bucket (a player standing / crouching) are 2-3x rarer; `ExitField` and the exit field in `Next` likewise. Behaviour: a route to a target that moves **within its bucket** is up to 3 s old; a target that crosses a 2 m bucket still gets a fresh field at once (key change), so chases feel the same. Minimal. |
| `[NPC] ScaleWithActors` (F6, Pathfinding) | false | true | think interval 0.1 -> 0.2 s at 6-10 engaged brains, 0.3 at 11-20, 0.5 above (Brain.cs:176). Halves/quarters the number of `Nav.Next` calls, feeler fans and `AvoidFriends` loops in a raid; Builds are cache-limited so the spike height does not change, but the cache-miss **rate** per second does when NPCs stand in different areas. Behaviour: steering reacts 0.1-0.4 s slower in big fights (the AGENTS.md 5.4 precedent). Recommended for Denis's machine. |
| `[NPC / Nav] CellSize` | 0.5 | 0.6-0.75 (only after a test) | the one lever on Build / PickOutExit cost (n ~ 1/CellSize^2: 0.75 m -> 2.25x cheaper). Behaviour: the map stops seeing gaps narrower than ~CellSize + body (doorways and gaps between huts; with `BodyR` 0.35 a 0.75 m cell makes ~1 m passages marginal), and `MaxStep` is judged over a longer step. Existing bakes are kept until the scene reloads. Try 0.6 first and watch for raiders refusing doorways. |
| `[NPC / Senses] GunshotResponders` 4, `GunshotNearRange` 25 | - | lower only if wanted | fewer walkers = fewer Idle ghost walks (= fewer `PickOutExit`). Behaviour: fewer raiders come looking. |
| `[NPC / Senses] FlankDistance` 15 / `FlankWidth` 10 | - | 0 to test | the flank point is 10 m off the line and often outside the footprint -> an outside goal -> `PickOutExit`. Setting FlankWidth 0 sends them straight (a behaviour change; useful as an A/B to confirm the cause in the log, see section 5). |
| `[NPC / Senses] LookInterval` 0.15 | - | leave | Senses is 0.008 ms avg; no gain. |
| `[NPC / Brain] FeelerCount` 7 | - | leave (5 if desperate) | 2 capsule casts less per think; coarser steering; the feelers are not where the time goes. |
| `[NPC / Brain] ReactionTime` 100, `MaxDistance` 150 | - | leave | MaxDistance already drops far NPCs to vanilla. |
| `[NPC / Idle] WalkRadius` 200 | - | leave | patrol walks are 0.2 s thinks with cached map points; cheap. |

Core: `[Core] Frame budget (ms)` does nothing for NPC (no Sliced jobs).

---

## 4. Code recommendations, ranked by impact / risk

All in `src/Modules/NPC/`; "pure" = identical decisions and timing of the AI.

1. **Nav.Next: component short cut for unreachable inside goals** - `Navigation/Nav.cs` 736-754. Pure (same connectivity rule, section 2a). Gain: removes 1 of the 2-3 Builds per "no map route" miss; with the player on a platform the removed Build is the small one, with the NPC on an island it is the 26k-cell one. ~5 lines. Risk: none if `s.Comp` is null-guarded (it is set in phase 2 before `Baked`).

2. **ExitCell / PickOutExit: take the NPC-side field from the cache** - `Nav.cs` 988-998 and 1061-1076, 1110 (section 2b), exact-key variant of `FieldFor`. Pure. Gain: the biggest Build of the failing path runs once per NPC cell bucket per `FieldSeconds` instead of per (area, goal bucket) every 2 s / 5 s; shared across NPCs standing together. ~8 lines. Risk: low (the array must not be `Return`ed; make `_outMemo`/`_exits` not hold it).

3. **Longer life for fixed-cell fields** (exit fields) - `Nav.cs` 962-964 (`ExitField`) and the `FieldFor(.., true, exit)` at 765 (give it a distinct key space so the 2.9-cell tolerance does not apply). Pure. Gain: 1 Build per second per exit -> 1 per 5 s. ~4 lines. Risk: none.

4. **Lazy Dijkstra for route fields** - `Nav.cs` `Build` 1310-1345, `FieldFor`, `Field` class 51. For fields used only by `Next`/`OutNext`/`RelaxedNext` (not `ExitCell`/`PickOutExit`/`TraceDump`, which scan all cells and need the full field), stop expanding as soon as the asking NPC's cell is popped and keep the heap on the Field (own `Heap` per partial field, pooled); on the next request for a cell not yet settled, resume. Dijkstra's invariant makes every read the code does exact: `Dist[from]` is final when popped, and `Downhill` only ever moves to cells with `Dist < Dist[cur]`, which are all settled (anything with a smaller tentative key would have been popped first); `GridSight`/`Descend` read no other distances. The cache-validity test `f.Dist[goalCell] <= 2.9 cells` may now see +inf for an unsettled cell -> it rebuilds (also partially), same waypoint. Gain: for an NPC d cells from the goal the expansion is ~ the cells within path distance d instead of the whole component: typically 5-20x cheaper per Build during a chase inside a camp. ~40 lines. Risk: moderate (two new code paths: resume, and "full" vs "partial" callers); flag `Field.Complete` so the full-field callers can top up a partial field instead of rebuilding.

5. **`PickOutExit` worst case** - `Nav.cs` 979-1017. Pure options are thin: (i) memo `fresh` picks too, keyed by (structure, from cell, goal 8 m bucket) for 1-2 s - identical inputs give identical output, hit rate low but non-zero when a walker re-picks several times from the same cell (`p.Out` toggling); (ii) hoist the per-step config read out of `EdgeKind` (item 8) - the 6000 line walks are ~1 M `EdgeKind` calls. Anything that changes **which** exit is chosen or **when** (lower `MaxOutTests`, a time budget that returns "no exit" early, slicing the scan over frames while the NPC keeps its last waypoint) is a behaviour change (timing or choice) and should be a Denis decision; the sliced variant (keep the previous plan, answer `handled = true` with the old exit until the new pick finishes) is the least visible one and would cap the job at a few ms. Gain of (i)+(ii): maybe 20-40 % of the spike; of the sliced variant: the spike is gone.

6. **Replace the two `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` scans** - `Brain/ReloadSounds.cs` 26 and `Animals/Burrow.cs` 377. ReloadSounds already filters to FSMs under the player's `WeaponsArm` (29-31): walk `Core.GameRefs.PlayerCameraHolder.GetComponentsInChildren<PlayMakerFSM>(true)` instead (same objects, same "AnimSound" + name filter) - pure, ~6 lines, removes a 30-100 ms spike on the first reload of each weapon kind per scene (it lands in Brain). Burrow: keep `_effect` across scene loads when it is still alive (prefab assets survive; drop the `_effect = null` in `OnSceneLoaded` 46 unless destroyed) - pure, 1 line, removes the per-scene scan from the Burrow job; the once-a-minute retry while the prefab is absent stays (or raise it to 5 min).

7. **Overlay: cull and stop allocating** - `Detection/Senses.cs` 1706-1731 (no distance cull on ghosts/agents; `string` per label per frame), `Navigation/Nav.cs` 1709 (`new List<GameObject>()` per frame), `Debug/DebugOverlay.cs`. Add the 200 m cull the Idle overlay has, cache each label string on the agent/ghost and refresh it 4x per second. Pure (debug only). Gain: only when ShowNavigation is on, 1-4 ms/frame -> < 0.5. ~25 lines.

8. **Hoist config reads out of the Dijkstra inner loop** - `Nav.cs` `EdgeKind` 645 (`Mathf.Max(0.1f, NpcModule.NavMaxStep.Value)` per call: a property call + Max for ~500 k calls per Build) and `WallNext` 693, `LowWayClear` 1663. Read it once into a static at the top of `Build`, `GridSight`-heavy callers, and `BakeStep`. Pure. Gain: ~10-15 % of every Build / OutClear walk. ~6 lines. Risk: none (live config edits apply from the next call).

9. **Nav discovery sweep cadence** - `Nav.cs` 178, 221. 250 nodes per frame with a 2 s pause is ~0.15-0.25 ms every frame for the whole session (= most of Nav's 0.195 avg) to find structures MapMagic streams in. Lower to 100 nodes/frame and/or pause 5-10 s. **Behaviour (timing only)**: a new camp is found a few seconds later; `Nav.Spawned` (235) still catches CreateObject spawns at once and `Pick` only looks within 3 x BakeRange. Alternatively restrict the walk to `MapMagic/Tile*/Objects` like `Nests.Pois` (Nests.cs:242-259) does - coverage change, needs Denis's OK.

10. **`WeaponOf` churn** - `Brain/Brain.cs` 1287-1298 (`AfterActivate` marks dirty for any toggle, including the muzzle flash of every burst) and `Detection/WeaponRanges.cs` 81-100 (`GetComponentsInChildren<Transform>(true)` + `Substring` per bone). Only re-find when the toggled object is under a hand bone or is the current weapon; cache the hand transforms per NPC. Pure. Gain: one 100-200 element array + ~150 strings less per burst per gunman (GC). ~15 lines. Risk: low (keep the 0.5 s retry while nothing is held).

11. **DogAnimations `InReach` twice per frame with `GetComponent`** - `Animals/DogAnimations.cs` 263-283, 312-319, 465: cache the target collider per target like `DogBite.cs` 89 does, and reuse the `TryAttack` result for the `close` test (same inputs in the same frame unless `StartAttack` changed the pose - compute once before). Pure. Gain: 2 `GetComponent` + 1 `ClosestPoint` + 1 linecast per dog per frame. ~10 lines.

12. **Idle.Drive capsule fan** - `Brain/Idle.cs` 557-578. Already 1 cast per frame in the open (first free heading breaks). Gating the fan to every other frame or 0.05 s and keeping the last heading is a **timing change** (50 ms steering latency for walkers); gain ~0.03-0.15 ms per walker per frame. Low priority given the measurements.

13. **Eager log strings** - `Senses.cs` `Log(a, "..." + ...)` call sites that run per look/think (199, 514, 538, 558, 1249), `Nav.cs` 1013-1015 (the "way out of" line is fully built before `LogOn`? no - it is inside `if (LogOn)`, fine), `Idle.Log` (583-586: callers concatenate first, e.g. 218, 305, 397). Wrap in `if (NpcModule.SensesLog.Value)`. Pure; GC only; small.

14. **`RefreshSpiderLights` item walk** - `Senses.Spiders.cs` 58-65 + `Senses.cs` 1652: raise the `_prefabOf` cap (or skip the clear when it is the item walk that fills it) so > 4096 items do not re-read `go.name` every second. Pure; only with spiders registered.

15. **`AvoidFriends` / `FriendInTheWay` / `Passthrough` O(n) and O(n^2)** - fine at 20-50 agents (a few microseconds per think); a spatial bucket is not worth the risk now. `Brain.Tick` per-frame `ActiveStateName` per NPC: a managed property, no alloc; leave.

Not recommended: moving Brain/Idle/Senses to `Interval` jobs (they already gate internally and the per-frame part is the body control that must run every frame); `Scheduler.Sliced` for Builds without the lazy expansion (a half-built field cannot be used by `Downhill` unless the invariant in item 4 is respected - item 4 is the sliced form in practice).

---

## 5. What to measure next

1. F7 > Debug & performance > Performance > **reset** right before the raid, then after it read F7 > NPC AI & navigation > **AI overview > Frame time**: the rows `Idle`, `Burrow`, `Nests`, `DogBite`, `Overlay`, `Pounce`, `Webs`, `Passthrough` (avg / last / worst). Expectation: `Idle` worst in the 100-300 ms range, `Overlay` avg 1-4 ms while ShowNavigation is on, the rest < 1 ms worst.
2. Same raid with `ShowNavigation = false`, `VerboseLog = false`, `NavDump = false`: NPC avg should drop by the Overlay row; the worst should not change (it is routing).
3. One raid with `VerboseLog = true` only to **read the Nav timings** in `BepInEx\LogOutput.log`: lines `Nav: <npc> way out of <structure> toward x,z: ... (N/M cells tested, T ms, from here)` (Nav.cs:1013) give the `PickOutExit` cost and how many `OutClear` walks it took; `Nav: <npc> has no map route ...` frequency per NPC = the ExitCell miss rate; `Nav: baked <structure>: W x H cells ... ms over N frames` gives the grid sizes in play (29k here). Also look for `Reload sounds for <key>: ...` (the first-reload scan) and `Burrow: sand effect found` (the FSM scan) with their timestamps against the spike.
4. A/B for the cause of the 268 ms: `[NPC / Senses] FlankWidth = 0` and `GunshotResponders = 1` for one raid (fewer outside-goal walkers) - if the Idle worst collapses, it is `PickOutExit` from the ghost walks.
5. After the code changes: `FieldSeconds` back to 1 and compare Brain / Idle worst; the `(N/M cells tested, T ms)` numbers; `AI overview > Bake cost` (ms per cell) as a sanity check that bakes were not touched.
6. ModProfiler Focus on `Nav.Build`, `Nav.PickOutExit`, `Nav.ExitCell`, `Idle.Tick`, `DogAnimations.SampleDog` (named methods, AGENTS.md 5.4) for the per-call distribution rather than the per-job worst.
