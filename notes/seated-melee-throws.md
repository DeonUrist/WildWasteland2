# Seated melee, blast lances and grenade throws — 2026-10-19, Testing

## Standard WW fists/rotation/camera follow-up — 2026-10-19

Denis explicitly requested the same final MP melee/fists visibility, correct camera position and rotation in standard WW. The existing pending seated melee/lance/grenade changes below were already present and remain. The missing fists path and final camera fit are now ported from WWMP HEAD6faf77b plus its working tree, source installed DLL MD5 `85FB6F59FB09BF2DA81E161C8CC76746`.

Native fists are named hands and classified Kind.None; the previous seated path only watched Kind.Melee. Body.cs now reuses WatchAttack/PlayStrike for hands, clears stale strike/upper weights on equipping, includes all eleven upper bones during punches and blends from knee-rest. Empty hands and idle fists rest on legs. Fists retain the same torso aim and95/80-degree whole-body-turn hysteresis as weapons even while idle; the superseded MP fist-turn exclusion is not ported. Native first-person gameplay arms and damage timing stay native.

The local first-person body now follows its posed head after seat rotation/pose fade/capture, using FirstPersonBodyBack, the existing .075m model eye height and CarShiftEye, with existing base/custom seat offsets afterward. This keeps the first-person view in front of the head when torso/hip rotation displaces it. Third-person body/seat anchoring and the native camera/physics are unchanged. The normalized LateCar/LateCarInner source blocks match final MP; the standard WW missing-wheel path stays intact and no MP passenger/API/packet code is introduced.

One source file,26 added/six removed lines relative to task start. Existing SP port/other shared changes preserved. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false -o bin/Release/seated-fists-fit_20261019` passed zero warnings/errors. Unique stage `bin/Release/WildWasteland_seated-fists-fit_20261019_154346.dll`, built/staged MD5 `1C7467990C29848956F1AC168C3D7710`, size86,309,888 bytes. Not installed under the standing standard-WW no-deployment policy. No config/save/assets/Core/new jobs/hooks, game launch/harness, commit/push/release. Full current working tree is included in the build; runtime appearance/performance remains Testing.

When Denis installs this standard build: enter a car and check empty hands/idle fists rest on legs; tap/hold Fire then release, expecting upper-body punches then neutral. With fists, melee and gun, look sideways/back/up/down and repeatedly cross the turn threshold; expect weapon-style turning and no back-of-neck obstruction in first person. Repeat third person, male/female/other models, swaps, gun firing/reload and existing melee/lance/grenade behavior. Regress holster, exit/re-enter, missing-wheel handling and on-foot attacks. No new log lines; existing verbose Strike/Punch entries confirm punch execution but do not prove fit.

## Original melee/lance/grenade fix

Denis reports invisible, static melee weapons in third-person vehicles locally and when observed over multiplayer. He requested upper-body swings following the first-person attack and additionally asked for visible/thrown blast lances and animated grenade throws in SP and MP. The common fix is applied to both WWMP and standard WW; their other pending changes remain intact.

## Cause and change

`PlayerBody/Body/Body.cs` enabled seated animation only for guns or an active throw. Its early return discarded melee props, and LateCar never called the existing WatchAttack/PlayStrike path. The seat pose also overwrote all three animated spine rotations, retaining only eight arm bones. Held lances took a static seat-arm branch and active throws used rifle-idle prop weights.

LateCar now observes native melee Attack state/timer changes and reuses the existing first-person-timed melee/combination upper layer. Melee props remain in the hand, and arm plus spine rotations survive the seat pose for melee and active throws. Held lances participate in the existing animated arms path and retain their world-item model; active melee/lance prop fits use the actual upper clip instead of RifleIdle. Wheel/passenger hand IK yields to these actions using its existing owner gate. Ordinary gun animation retains its eight-arm-bone behavior and the existing turned-seat behavior is unchanged.

This uses the established retargetable third-person clips synchronized to native first-person attacks; it does not copy raw native FP arm-bone curves onto the humanoid body. Native FP melee controllers have no humanoid avatar (`Sources/data/hierarchy/scene1_game/PlayerCameraHolder.txt`), so those raw clips cannot be assigned directly to this humanoid graph. First-person gameplay arms remain native. The native Attack FSM remains the source of hit timing, repeat cadence, projectile creation, ammo use and lance removal; no new damage/throw authority or projectile creation is introduced.

Source evidence: `Sources/fsm/scene1_game/PlayerCameraHolder.md`, melee Animation/Attack around lines6119–6180; blastlance Attack around4597–4660; grenade Animation/Attack around2147–2218. The existing WatchThrow/PlayThrow clock handles lance and grenade starts and continues across native weapon holstering/release. Spine retention now preserves the full upper-body contribution of those throws. Actual fit and release visuals still require Denis's test.

WWMP handshake23 already sends the fitted held-item identity/transform and final bones for driver/passenger pose modes2/3. Correcting the local pose therefore reaches host-to-guest, guest-to-host and guest-to-guest observers through the existing stream; no protocol/Core changes are required. Network damage/explosion behavior is outside this presentation fix and remains native/host-authoritative as before.

One source file per repo, **17 additions / nine removals** against task-start copies. No config/save/asset changes, new scheduler job or Harmony target, Core/other-module changes, game launch/harness, commit/push/release. Source/runtime status remains Testing.

## Build and deployment

Both Windows Release builds passed with **zero warnings/errors**, using `-p:Deploy=false` and separate output directories `bin/Release/seated-melee_20261019/`.

| Repo | Unique stage | MD5 | Installed |
| --- | --- | --- | --- |
| WWMP | `bin/Release/WildWastelandMP_seated-melee_20261019_150529.dll` | `5754C7A6D731F459324A9F741A4C7A46` | Yes; built/staged/installed hashes match. Games closed, both WW/WWMP disabled manifests absent. |
| Standard WW | `bin/Release/WildWasteland_seated-melee_20261019_150529.dll` | `451E447FE90ADC30320F12C2785D6088` | No, per standing no-base-deployment policy. |

Builds include their current shared working trees. The combined MP build also includes the previously staged upright vehicle-load fix. All multiplayer peers must restart with matching DLLs. No gameplay/performance acceptance is implied by compilation or deployment.

## Denis's checks

1. Restart host and both guests with the deployed build. In third person as driver and passenger, equip machete, knife/shiv and wrench; tap and hold Fire. Expect a visible held weapon and one upper-body swing per native attack, with continued swings while holding and no wheel/knee IK overriding the action. Repeat first/third-person switching, aim left/right/up/down and male/female/native character choices. Holster, swap to a gun, leave/re-enter the car and check normal hands/gun behavior returns.
2. Equip both blast-lance variants while seated: expect a visible lance before throwing, the right-hand throw motion, disappearance when native release consumes it, and the native projectile/explosion. Throw a grenade while unarmed and while holding melee/gun: expect the upper-body throw to continue across temporary holstering, with native grenade consumption/projectile and normal held-item recovery afterward. Try successive throws; each native throw should animate once.
3. Repeat with host acting and guest observing, guest acting and host observing, then guest acting and the other guest observing. Expect matching held models, swings, torso motion and release without a stuck weapon. Repeat offline in WWMP and, when Denis installs the staged standalone build, in standard WW.
4. Regress gun aim/fire/reload, wheel hands, passenger knee-rest hands, seat transitions and on-foot melee/throws. No new logging is added. Existing VerboseLog-enabled `Melee: first-person swing ...`, `Strike: ...`, `Throw: ...` and prop/view messages can confirm path execution; their absence with logging off is expected. Visual fit, synchronization and performance remain unverified until these checks.
