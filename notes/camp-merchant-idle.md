# Camp merchant Idle exclusion

2026-10-19 — Testing. Same requested AI fix as WWMP: Coyote Merchant, Mechanic and Organic_Mechanic must keep their dedicated shop spots.

Idle.Resolve previously admitted them by Coyotes faction and, without an EnemySpawn origin, assigned the nearest free guard spawn point in the structure. The same fallback applies after loading. Idle.Make now classifies the three exact native prefab identities once with cached Senses.PrefabOf and marks their controllers excluded/resolved before any home claim. Follower release retains that exclusion. Existing Idle gates suppress automatic assignments/walks and its markers/designations; explicit travel orders still use TravelTick. Native positions, Senses/Brain combat and ordinary guard idle behavior remain. No automatic teleport of merchants already displaced in a save.

One NPC source file, Idle.cs: 5 added/2 removed lines. No Core/config/protocol/save-format/patch changes. Previous uncommitted crawl work is retained in this build.

Windows Release/Deploy=false passed with zero warnings/errors. Built/staged MD5 `C14ED4BD0A3FF67D432DD6A07391FEF1`; unique stage `bin/Release/WildWasteland_camp-merchants_20261019_013848.dll`. Not installed under the standing base repo-only policy. No game launch/harness, commit/push/release.

Denis should load/revisit a camp with all three merchant types, enable ShowNavigation and wait through normal idle walk/return intervals: no Idle homes/patrol points/controller labels or autonomous Idle walks for those shopkeepers, while ordinary guards retain them. Repeat after save/load and hire/dismiss if available; explicit merchant/crew travel must still work. Sensory combat/state labels remain separate. No new log; identify the DLL by hash. Runtime/performance pending.
