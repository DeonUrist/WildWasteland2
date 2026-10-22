# Weapons Pack integration — Testing, 2026-10-21

## Dismemberment disabled at Denis's request, 22:13

Denis reports dismemberment does not work and requested commenting it out. ChainsawLimbs.cs is excluded with #if false; its Cut/Tick/Clear/save hooks are commented out. Chainsaw kill detection and human witness fear remain; no mesh cutting/debris/save restoration code is compiled. This supersedes all dismemberment behavior/test instructions below. Includes the pending first-person shadow fix.

Windows Release/Deploy=false: zero errors, two existing NPC warnings. Game subsequently closed; both exact disabled manifests absent, normalized runtime index equal, all361 assets checked. DLL-only deployed from V:/Repos/Apocalypter/_releases/WildWasteland_no-dismemberment_20261021_221332_317.dll, MD5 3DFCBD255C3C2BD941AFEEBA0DE93F38; prior same stem .before.dll. Live index/assets/config/layouts/saves preserved. No game/harness/commit/push/release. Denis checks no limb removal/debris, retained human chainsaw witness fear/burning fear, and FP pack shadows.

## First-person shadow follow-up, 22:12

Denis reported FP wrench/rifle carrier shadows, with TP correct. Cause: Body.ShowProp keeps the native carrier in ShadowsOnly for FP; the pack's UpdateThirdPerson calls HideThirdPerson outside TP, restoring that carrier. New optional PlayerBody/WeaponsPackShadows bridge hooks that exact pose completion. Closed delegates read the pack's final held model source without per-frame reflection argument arrays. Cached mesh-only ShadowsOnly copies follow its final world transform; native carrier renderers are suppressed only in eligible FP and restored before the next pack update using the body's current intended visibility. TP/unequip/pause/save yields to existing behavior. Mesh copies/renderer arrays are rebuilt only on model/carrier change; no new update job or scene scan. No pose/attack/animation edits.

Windows Release/Deploy=false: zero errors, two existing NPC warnings. Staged V:/Repos/Apocalypter/_releases/WildWasteland_weapons-pack-shadows_20261021_221208_167.dll, MD5 677C6BB6B0DABFFC11664F3CBA87F7BB. Not deployed: game process37500 running. Existing installed DLL from the preceding integration remains unchanged. No game/harness/commit/push/release.

Denis tests both weapons in FP sunlight while idle/LMB/RMB; shadow should match the actual weapon with no carrier shadow. Switch FP↔TP, native wrench/rifle↔pack weapon, holster/drop and toggle first-person body; native shadows should restore and TP remain correct. Save/load and inspect startup `[PlayerBody] Weapons Pack: first-person weapon shadows use pack models.` Visual placement and shadow appearance remain Testing.

Denis requested WW-style weapon icons, burning-debuff retreat for humans/animals/mutants, chainsaw-kill bodypart removal and nearby human-enemy fear only, machete slash contact audio, and 3x footstep radius while the chainsaw is drawn (otherwise 1x). Existing animations retained; no constant per-enemy polling added.

## Changes and evidence

- CleanHUD routes the pack's PackHudIcon override back through the shared white silhouette renderer. Only ChainsawModel/FlamethrowerModel geometry is rendered, excluding inherited carrier meshes. Two stable cached icon keys; no new image assets.
- NPC observes Weapons Pack 1.6.4 FlamethrowerIgniteStatus.Touch, BeginEnding and OnDestroy. First successful burning debuff contact starts fear; end/destruction releases it. Repeated contacts exit after a dictionary lookup. No damage-tick/Advance/Update fear polling. Cover movement uses the existing Brain think budget, Hide cache and Nav.CoverSpot; unavailable-cover searches retry at most every two seconds. Existing ground-brain eligibility remains; flying/seated bodies, followers and underground travellers do not acquire a new movement driver.
- Exact ProcessHit overload records native Health death comparisons and qualified corpse CreateObject actions. Only an actual killing contact on a living target can mark its exact corpse; normal loot is excluded. Registered living hostile humans within 25m and unobstructed receive six seconds of fear. Animals/mutants do not receive chainsaw witness fear.
- Corpse-local skinned-mesh copies remove the nearest supported limb's weighted triangles and produce temporary detached physics debris. Native skeleton/body/colliders/loot/identity remain. Bone-attached accessories hide. Only the newest kill retains loose debris, expiring after 15 seconds. A two-second compatibility job performs one-time binding and temporary-state cleanup; no per-limb Update components. Cut corpse mesh ownership ends when the corpse disappears or the module resets.
- SaveBegin restores original meshes/accessory visibility; SaveEnd reapplies cuts. Cuts are cosmetic runtime state, not retained after reload. No new save format/components on saved roots.
- Guns routes the pack's contact-sound method to Tracers.StabClip, the same cached sound WW uses for machete blade hits. Pack fallback remains if the sound is unavailable.
- StartPreview/StopPreview events cache ChainsawDrawn. Existing footstep detection multiplies FootstepRadius by 3 while drawn, otherwise 1; movement, cave-web exclusion and obstruction rules are retained. No extra hearing scans or polling. Default 5m becomes 15m; standing still remains silent.

Native evidence: Sources/fsm/scene1_game/PlayerCameraHolder.md:6139 (machete Attack, actorHitSound knife_hit; WW replaces blade contact with StabClip); Sources/fsm/prefabs/Gunnar.md (Health creates Gunnar_Dead before deleting living root); Gunnar_Dead.md (bone Bodypart receivers, no native severing action). Dismemberment here is cosmetic mesh work, not a native severing API.

Pack source compared: main commit 6495d20891e812fb5f81a5c75024bb291e721d9c, plugin 1.6.4. Existing installed log loads pack1.6.4/WW1.0.7 and confirms pack body/HUD/equip bridges.

## Build and deployment

Windows Release/Deploy=false: zero errors, two existing NPC PoiNav CS0649 warnings. Game closed; both exact disabled manifests absent; normalized runtime index equal and all 361 indexed assets present. DLL-only deployment preserves live assets/index/config/layouts/saves. No Core/csproj/pack edits, settings, version bump, commit/push/release, game launch or harness.

- Final stage: V:/Repos/Apocalypter/_releases/WildWasteland_weapons-pack_20261021_215920_498.dll
- Previous DLL: same stem .before.dll
- Installed MD5: B8170B6FB9D7BD8C7AEAA8DC8CA7F226
- Earlier 21:56 stage is superseded by the footstep addition.

## Denis's checks and limits

1. Equip both pack weapons: white WW silhouette icons, unchanged working animations. Switch to native weapons and back.
2. Chainsaw LMB/RMB contacts use the machete slash, while engine/fuel/animations retain pack behavior.
3. Ignite a human, animal and ground mutant with reachable nearby cover. Retreat lasts with the burning debuff and releases on expiry. Normal unignited damage must not start this fear. No usable cover means normal behavior until the bounded retry; unsupported flying/seated bodies retain existing AI eligibility.
4. Kill with either chainsaw attack. A supported readable skinned corpse loses a limb; visible hostile humans within25m retreat for6s. Distant/wall-hidden humans and nearby animals/mutants do not receive chainsaw fear. Nonlethal hits and existing corpse hits do not retrigger removal.
5. Walk behind an enemy at 5–15m with default FootstepRadius5: chainsaw out should be heard; holstered returns to5m. Walls still obstruct and standing still produces no footsteps. Check draw/holster, drop, death, scene/load transitions.
6. Save/reload and module toggles restore native meshes for saving. Cuts intentionally do not survive reload. Check female variants/bosses separately.

Startup logs announce the NPC, CleanHUD and Guns Weapons Pack bridges. NPC VerboseLog/BrainLog adds retreat: burning debuff, retreat: witnessed chainsaw kill, and Chainsaw severed messages. Report contract/missing-bone/Chainsaw limb mesh warnings.

Visuals and performance are unmeasured. Mesh cutting/baking is synchronous on a kill, supported readable rigs only, with open triangle boundaries rather than capped anatomy. Actual seams, native ragdoll collision, long-session memory and frame cost require Denis's game test. No universal limb/FPS/Verified claim.
