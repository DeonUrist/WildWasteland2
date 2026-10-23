# Perk roster and mental-break lifecycle — 2026-10-23 — Testing

This supersedes the Cannibal effect and immediate perk-choice behavior in survival-perks.md. All ten roster entries are insanity perks, stored as permanent bits. Skills still start at 0/100 with no progression or skill effects. Existing Cannibal saves retain their unlock with the new butchering effect.

| Perk | Effect |
|---|---|
| Cannibal | Enables human butchering: two raw human meats and an attachable skull. No food-insanity immunity. |
| Iron gut | Half insanity gain from all Food FSMs and stew, raw or cooked. Negative/removal effects are unchanged. |
| Bloodbath | First enemy kill starts a chain; every following enemy kill within five game seconds of the previous gives +5 health. Sleep/load resets the chain; herbivores do not count. |
| Mutant | Mutant/MutantSanity sight, footsteps and player-related hearing ranges are halved in NPCAI. Humans/animals keep their ranges. |
| Wasteland roulette | On waking, chooses among registered weapon kinds; that kind does 1.5x damage until the next wake. The chosen kind persists in the world save and appears in the perk hint/notification. |
| Revel in fire | Ignores player-body damage, damage events and physical force from explicitly player-owned explosions. Enemy blasts still affect the player. |
| Death wish | 2x outgoing damage below 50 health (strictly below, native maximum 100). |
| Corpse obsession | On waking within two 3D metres of an active corpse: +20 health in addition to Survival's normal +10 (30 total). |
| Paranoia | Pale white arrows for up to 32 registered hostile NPCs within 30 m. Uses the existing Damage direction HUD switch. NPC registry reconciliation is 4 Hz, with pooled arrows following their targets each HUD tick. |
| Sadist | 1.5x melee damage, including the native unarmed/player attack path. |

Damage multipliers multiply together. Perk damage queries run at impact for Gunplay projectiles (including penetration/parts) and native damage writes; the fitted-wheel melee fallback uses the same query. ICombatSources carries explicit native grenade/lance/explosion ownership and follows triggered explosive items. Optional external weapon implementations can use these ModAPI contracts; arbitrary external damage code is not patched by this change. Mutant/Paranoia consume NPCAI providers.

## Mental breaks

- At 100 insanity: start 60 seconds of normal gameplay. No new episode effects yet. Manual and forced sleep activation are blocked. Menus, pause, death and ordinary sleep do not advance the clock.
- At the end of that minute: open three randomized unowned insanity-perk choices. Award only after the completed episode, when the player selects one. Reset insanity to 35 after selection. With fewer than three unowned perks, remaining choices can repeat; with all owned, choices retain the existing perks without duplicating them.
- Every 300 seconds of normal gameplay: random chance = owned insanity-perk count × configured chance per perk, capped at 100%. These episodes also last 60 seconds and block sleep, but end with **no perk, no choice, and no insanity reset**.
- Debug > Survival > **Mental break** starts the reward-bearing 60-second episode, for testing. Close the settings window to advance time.
- Developer > Perks > **Chance per insanity perk (%)** is a live 0–100 setting, default 5. Stored in tools.cfg `[Perks / Mental break]`. At 0 there are no random episodes; reaching 100 still guarantees an episode.
- Owned insanity-perk squares have red borders. Selection cards have a red accent. Descriptions keep the smaller font-measured wrapped-line presentation.

Remaining episode time, reward/no-reward state and the five-minute countdown use `WildWasteland.Survival.MentalBreak.v1` in the verified ES3 transaction. Perk flags and roulette kind use version 2 of the existing `WildWasteland.Perks.Player.v1` record, with a reader for the original Cannibal-only record. No sidecars or renamed save/prefab IDs.

## Ownership and performance structure

PERKS owns roster, unlocks, direct IPlayerPerkEffects queries and PlayerWokeUp/NpcKilled action consumers. Survival owns native sleep recovery, food insanity, human-butchering gates and the mental-break clock. Gunplay owns native/projectile damage integration and ICombatSources. NPCAI supplies INpcEnemies from its existing agent registry and applies the detection factor. HUD draws the owned icons and reuses the direction-arrow implementation.

No new scene scan or physics query runs per frame. Corpse roots join Core's existing sliced WorldIndex and are checked only on waking. Effects use bit checks/numeric queries; kill/wake actions use the owner-scoped event bus. Paranoia is gated by the perk/HUD visibility/switch, reconciles the existing registry at 4 Hz, and warms at most one arrow per frame. Ownership/action caches are swept once per second and cleared on scene changes/shutdown. These design bounds are not a measured performance claim; Denis owns ModProfiler acceptance. No game, harness or simulation was run by the agent.

## Human meat missing from Apocaspawner after a reload

Confirmed in Denis's log with installed MD5 DFFB80AE8EE8450AFB8D7377A7CF343F: initially 407 catalog entries, meat(16), and four successful meat_human_raw spawns; after a world reload, 404 entries and meat(14), despite cached-template readiness. HumanFood.Prepare returned early for persistent cached templates without registering them in the replacement scene's ES3 manager. It now re-registers raw meat, cooked meat and skull in the current manager before returning. IDs, objects and native item behavior are retained. Log confirmation: `[Survival] Registered meat_human_raw with current save manager.` (also cooked/skull).

## Icons

The existing Cannibal artwork is retained. Nine named placeholder PNGs are indexed from `src/Modules/Perks/Embedded/Icons/` and installed missing-only under `plugins/WildWasteland/Perks/Icons/`: iron_gut.png, bloodbath.png, mutant.png, wasteland_roulette.png, revel_in_fire.png, death_wish.png, corpse_obsession.png, paranoia.png, sadist.png. Replace installed PNGs and restart to load new artwork. Existing customized icons are preserved during deployment.

## Denis's in-game checks

1. Verify the installed MD5 below. In Apocaspawner spawn raw/cooked human meat and skull. Return to title, load the world, reopen the catalog: all three remain available; the registration lines repeat for the new manager.
2. Start without Cannibal: human corpses have no butcher result; animals still butcher normally. Obtain Cannibal after a completed episode: knife/machete/shiv human butchering yields exactly two meats and one skull, +10 insanity. Raw/cooked meat has its original full insanity effect without Iron gut.
3. Debug > Survival > Mental break, close settings, and try sleeping manually and through fatigue. Sleep is blocked for 60 gameplay seconds. Pause for 20 real seconds: those do not count. No perk is granted at episode start. At completion, select among three roster cards: perk is unlocked, insanity becomes 35, TAB shows a red-bordered icon and wrapped hint. Repeating breaks offers unowned entries.
4. Developer > Perks: set chance per perk to 100 after owning a perk. Play five minutes below 100 insanity: a random episode starts. After one minute, verify **no choice and no new perk**. Return the chance to 5 (or preferred value). At 0 random episodes stop, but insanity 100 still triggers the normal reward episode.
5. Save partway through an active break and reload: remaining duration resumes, sleep remains blocked. Save/reload after a perk and after roulette selection: flags and chosen weapon kind persist; another slot/new game does not inherit them.
6. Damage the player enough to observe recovery. Sleep alone: +10 health capped at 100. With Corpse obsession and a corpse within 2 m at wake: +30 total. Move beyond 2 m: +10. Test on foot and seated. With roulette, wake notices/hints report the chosen type; compare that type with another type (1.5x vs base).
7. With Iron gut compare human raw (+15 instead of +30), human cooked (+12.5 instead of +25), ordinary raw/cooked meals and stew. Butchering/attacking/killing insanity is not reduced. Cannibal alone does not reduce food insanity.
8. With Bloodbath kill three enemies with gaps under 5 s: first gives no bonus; next two restore 5 each. Wait over 5 s before another kill: no bonus until another qualifying kill. Check melee, projectile and explosive attribution; other NPCs' kills must not heal the player.
9. Compare melee damage with Sadist (1.5x), health 49 vs 50 with Death wish (2x only at 49), and matching roulette type (1.5x). Combined melee roulette + Sadist + Death wish is 4.5x. Test Gunplay projectiles both on and off, unarmed, and fitted wheel damage.
10. With Revel in fire throw a grenade at your feet or use your own blast lance: no player damage/shove. Enemy explosions still damage you. Test a fuel container triggered by your shot and its chain reaction.
11. With Mutant compare acquisition on foot and seated in equal daylight/darkness/storm conditions: mutant distance is halved, human distance unchanged. Footsteps and player noise are halved for mutants. With Paranoia, nearby registered hostiles have pale arrows, allies do not; received-hit arrows stay red. Turn Damage direction off: both disappear. Re-enable, move beyond 30 m, and kill/despawn targets: pale arrows reconcile without leftovers.

Logs: `Mental break started: 60 gameplay seconds...`, `Mental break ended: choose an insanity perk.`, `Insanity perk selected: ...`, `Mental break resolved: ...; insanity reset to 35.`, `Random mental break started...no perk reward.`, `Random mental break ended: no perk awarded.`, `Sleep completed: normal healing +10...`, `Corpse obsession: waking bonus +20 health (30 total).`, `Wasteland roulette: ...`, and verbose `Bloodbath: consecutive kill +5 health.` No commit/push/release requested.

## Build and deployment — 2026-10-23

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with **0 warnings and 0 errors**. The deployed DLL and build output both have MD5 **A68532CB37D2BB5A0A5003319FA7EC9B**. Apocalypter was closed; no disabled WW manifest was present in Apocasetter's BepInEx cache. A named copy is staged at `bin/Release/WildWasteland_2.0.0_perks_roster_20261023_161245.dll`. No game execution or further tests were performed. Gameplay and performance acceptance remain with Denis.

The roster pass adds roughly 700 lines across the PERKS roster/controller, Survival mental-break/sleep paths, Gunplay combat adapter, NPCAI sensing provider and HUD presentation, plus the small typed ModAPI contracts and named icon assets. Existing unrelated navigation, POI and character work was preserved. Developer configuration added: `[Perks / Mental break] Chance per insanity perk (%)` (default 5, range 0–100).
