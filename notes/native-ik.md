# Native IK — Testing, 2026-10-21

Deployment follow-up: Denis explicitly requested install. Game closed; both exact disabled manifests absent; normalized runtime indexes equal and all361 assets present. Installed the exact staged DLL only, verified MD5 D19B1439F82CD30D6A64AE815FD819E4. Previous DLL backed up as V:/Repos/Apocalypter/_releases/WildWasteland_native-ik_20261021_222123_160.before.dll. No rebuild, live assets/index/config/layout/save changes or game launch. This supersedes the pending-deployment paragraph below.

Denis reports Juggernaut moves without leg motion, and toggling the performance feature off/on restores it even under Visuals. He explicitly requested that WW stop interfering with IK.

RC6 previously prefixed FastIKFabric.LateUpdate, skipping invisible rigs or rigs beyond the preset distance (25/40/60m). The first skinned renderer alone determined visibility. Native Juggernaut has FastIK chains for legs/arms/head (Sources/data/hierarchy/prefabs/J/Juggernaut__2.txt); native Movement/Run plays juggernaut_run and pushes the Rigidbody (Sources/fsm/prefabs/Juggernaut.md; Sources/data/animators.md:112). The precise failed live IK state is not logged; Denis's toggle test and explicit removal request authorize removing this optimization rather than adding diagnostics or another gate.

Removed the entire RC6 IK prefix, distance knob binding, cache and skipped-update counter. No distance or renderer-visibility check can now suppress native FastIK through Performance. Existing off-screen animator culling remains; the feature title/help now describe that alone. Authored replacement-body animation systems elsewhere are outside this performance change. Existing saved feature key is unchanged; obsolete live IK distance entries are not edited or read.

Windows dotnet Release/Deploy=false build: zero errors, two existing NPC PoiNav CS0649 warnings. Stage: V:/Repos/Apocalypter/_releases/WildWasteland_native-ik_20261021_222123_160.dll; MD5 D19B1439F82CD30D6A64AE815FD819E4. Not deployed: game running. No game launch/harness/live config/asset/save edits/commit/push/release.

Denis closes the game for DLL-only deployment, then checks Juggernaut run/attack at close range and beyond25/40/60m, off-screen→visible and Max/Balanced/Visuals presets. Legs should animate without toggling the feature to recover. Native IK should no longer appear as an RC6 Harmony target. Gameplay/performance remain unmeasured by the agent.
