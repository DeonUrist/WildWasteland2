# Interior population: step 4 (2026-10-21, Testing)

Denis requested interior enemies only within 50 m, spaced slightly in time. The
later clarification allows modest spawn cost; no custom asynchronous instantiation
or per-enemy background construction was added. Step 5 remains untouched.

Code/native evidence: Building_3 EnemySpawn_Wild_V1 and Camp_7 EnemySpawn_Scrapyard_V1
run trigger -> getRandom int -> getItem -> spawn, then spawn -> getRandom int until
the native count is complete. This loop creates an entire group synchronously.
Sources/fsm/prefabs/Building_3.md:191 and Camp_7.md:493 document it. Camp_10's
merchant uses the same sequence. Spacing group starts alone would not pace enemies.

InteriorPopulation now checks 3D player distance to each registered group origin
(50 m inclusive). Groups outside range stay queued. The event leading from trigger
or spawn into the next native creation sequence is deferred until in range and the
shared 0.15-second allowance is available, with an additional one-admission-per-frame
guard. Deferral happens before the native selection/count increment, not halfway
through creation. Resume uses that same native event only if its source state is
still active. Moving out of range pauses the remainder; existing actors stay alive.
Native names, counts, registration, completion/boss checks and population variety
remain owned by the native FSMs/existing code. Town merchants share the range gate.

Nearby groups may complete during the native terrain-loading wait, with the same
pacing; distant groups no longer hold TownReady/loading handoff open. Long paced
sequences no longer hit the former two-second stalled-group stop. On Towns shutdown,
held native events are released with pacing bypassed so live FSMs are not stranded.
World reset clears pending work, timing and per-agent flags.

The three reflection-based Senses prefixes are removed. Agent.Interior clears
ambush distance, and Agent.Deaf gates sound assignment and gunshot fight-memory
renewal directly. InteriorPopulation updates the flags in its existing 0.25-second
hearing job using typed agents and cached names; existing 3-nearby-hostiles/range
hearing policy is retained. A typed Senses helper releases an old sound investigation
when its agent becomes deaf. Sight and hit responses remain enabled. Flags clear
when Towns stops. No new config or save format.

Files: Towns/InteriorPopulation.cs, NPC/Detection/Senses.cs, and one TownsModule
shutdown call (alongside prior town-pad changes). Other shared-tree changes retained.
No game/harness/profiler/commit/push/release/Verified promotion.

Windows Release/Deploy=false: zero errors/two existing Route.GlideAt/Glides CS0649
warnings. Stage V:/Repos/Apocalypter/_releases/WildWasteland_poinav-step4_20261021_140739_412.dll.
Built/staged/installed MD5: 61FE7291A75B3EEEA0BF4B616A751EB1.
Game closed, both base disabled manifests absent, runtime index matches after path
normalization, all 361 indexed files exist. DLL-only deployed; prior DLL preserved
as the stage's .before.dll. Assets/index/configs/saves unchanged. Combined build
includes steps 1–3, town foundations and existing unrelated local work.

Denis tests: approach a fresh interior group from beyond 50 m. Expect no advance
spawn outside the radius, then staggered enemies on approach rather than a full
group in one frame. Step out mid-group, return, and check the remainder resumes.
Save/reload a partial and completed group; confirm counts do not duplicate and
completed/dead groups remain complete. Check a town, Camp_10 merchant, loading far
from towns and Towns disable/re-enable. Existing saved enemies are not despawned.
The group-origin radius does not mean every authored spawn point lies within 50 m.
Each individual Instantiate is still synchronous and its cost is unmeasured.
Log: `[Towns] interior spawn within 50 m: ...; enemies spaced by 0.15 s, native sequence retained.`
