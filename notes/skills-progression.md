# Skills progression — 2026-10-23, Testing

Supersedes the inactive-skill notes and Sadist effect in earlier survival/perks notes. No commit, push or release requested. Existing unrelated navigation, POI and character work is preserved.

## Effects and progression

All twelve skills start at 0 and cap at 100. Every +1 point costs exactly 100 XP; fractional progress is retained. Bonuses interpolate linearly. Developer > Perks offers one XP rate per active skill. Action rates default to 10 XP; Athletics defaults to 200 XP/km on foot and Driving to 100 XP/km driven. Rates accept 0–10000, with 0 disabling XP for that skill. Crafting has no progression or effect.

| Skill | At 100 | XP action |
|---|---|---|
| Melee | +50% weapon melee damage, excludes fists/kicks | Creature killed with a melee weapon |
| Pistols | 50% less pistol tracer spread | Creature killed with pistol |
| SMGs | 50% less SMG tracer spread | Creature killed with SMG |
| Rifles | 50% less rifle/sniper/crossbow/launcher tracer spread | Creature killed with those types |
| Shotguns | 50% less shotgun tracer spread | Creature killed with shotgun |
| Unarmed | +50% unarmed damage, excludes melee weapons | Creature killed by fists/kicks |
| Athletics | +50% forward running speed | On-foot travel, 200 XP/km default: +1 point per 500 m |
| Barter | Purchases -50%; sales and quest payout +50% | Successful purchase/sale, completed quest |
| Crafting | No effect | None |
| Medicine | +50% healing-item recovery | Bandage/FirstAid use that actually restores health |
| Survival | +50% nutrition produced when cooking | One grabbed raw item cooked, or each 10 raw nutrition converted in a last-handled stew pot |
| Driving | 5x impact damage, starting at 1x at skill 0 | Driven travel, 100 XP/km default: +1 point per km |

SMGs have their own skill, shown between Pistols and Rifles in TAB and Developer. Rifles group sniper rifles, crossbows and launchers. Thrown explosives have no skill category. Ranged skills scale existing player tracer offsets; zero native spread remains zero and NPC spread is unchanged. Shotgun distribution keeps the existing accuracy curve and scales its radius. Skill melee multipliers combine with existing perk multipliers; Melee and Unarmed never affect one another. Punches and kicks both use Unarmed for damage and kill XP. Vehicle kills count for human-kill insanity and Bloodbath, but grant no weapon-skill XP.

Driving replaces the existing fixed Vehicles ram multiplier for the player's current car only: 1 + skill × 0.04. NPC-driven cars retain the Vehicles setting. Existing bumper/speed rules, minimum impact speed and own-crew exclusion remain. This effect is owned by Vehicles' existing collision adapter.

Barter changes Buyable's price read before both its hover and affordability check; its actual currency action awards XP only after success. Native sales retain base stock prices, multiply the payment once, and refresh displayed board prices when Barter levels. Quest payout/offer text/journal use a live query without rewriting the saved base reward. Quests' reputation shop also uses the same price queries and reports successful transactions. No XP for declined/unaffordable attempts.

Medicine affects the native healing-item slots, not food, sleep, Bloodbath or Corpse obsession. Normal sleep remains 10 health (+20 with Corpse obsession near a corpse).

Survival records successful grabs and adding an ingredient to a pot. The last handler owns future cooking credit. A non-player handler reported through ICookingSkills.Touch replaces local-player credit; no skill/XP is awarded to the local player for that handler. Raw-to-cooked CreateObject transfers the nutrition factor to the new cooked item once, before consumption; eating it or moving it afterward does not reapply a bonus. Stew multiplies only new raw-to-cooked production and retains fractional progress toward each 10 raw nutrition. Adding already-cooked food, reheating, pouring or drinking never grants cooking XP. Bonused cooked food contributes its baked nutrition when added to stew.

Sadist now halves insanity gained from killing humans: Scrapyard +2 becomes +1; Coyotes +20 becomes +10. Aggression (+5) and human butchering (+10) are not reduced. No melee damage bonus remains on Sadist. Native CarAttack attribution covers run-over kills, without treating unrelated NPC passengers' attacks as the player's.

## Save/API/performance

Existing WildWasteland.Perks.Player.v1 key now writes payload version 5: twelve levels, unchanged perk flags/roulette, twelve fractional XP values. Readers retain versions 1–4; missing Driving/SMGs and XP start at zero. Existing names/storage indices and perk flag bits are unchanged; Driving and SMGs are appended in storage, with a separate display order placing SMGs between Pistols and Rifles. Cooking ownership, baked nutrition and per-pot raw credit use a verified SaveBus key WildWasteland.Survival.CookingSkills.v1, keyed by native saved item names. Corrupt state blocks saving rather than silently overwriting it.

IPlayerSkills provides numeric effect queries; PlayerSkillAction and PlayerWeaponKill carry completed actions. ICookingSkills owns last-handler attribution and stew production reporting. ICombatSources.RecordHit ties weapon type/unarmed to the victim before damage, so corpse creation can award exactly one kill action and other attackers replace player credit.

Travel samples existing cached player/car state at 4 Hz, uses world coordinates across floating-origin shifts, keeps foot/driven distance separate, excludes pause/loading/death/airborne foot movement and rejects large teleports. No new per-frame scene scan, overlap or raycast. Native action/item caches are pruned once per second and restore original action references on shutdown. Cooking restore uses the existing sliced WorldIndex, not a new scene walker. Design bounds only: no gameplay/performance measurement was run by the agent.

TAB lists twelve skills with separate value and XP columns and brief effect-only hints. Crafting says "No effect yet." Existing wrapping remains.

## Persevere

The reward-bearing mental-break decision has four cards: three offered insanity perks and **Persevere**, which grants no perk, sets Hunger and Thirst to 100, Fatigue to 80, and Insanity to 35. It is not added to the permanent roster or counted for random-break chance. Resolving it clears the current offered choices, so the next reward-bearing episode can prepare a fresh set. Chance-triggered episodes still end without showing a decision.

The supplied `C:/Downloads/persevere.png` was copied to `src/Modules/Perks/Embedded/Icons/persevere.png`; the runtime asset path is `plugins/WildWasteland/Perks/Icons/persevere.png`, installed missing-only. Existing customized runtime icons are preserved. Four centred cards retain wrapped descriptions and use the ordinary accent for Persevere, red for insanity perks. No save key, perk bit or existing choice anchor name was renamed.

## Denis's test steps

1. Load an existing world: ten earlier skills and perk flags remain; Driving starts at 0 and all XP starts at 0 for older saves. Save/reload after partial XP and a new point: both persist. Another slot/new game starts independently.
2. Developer > Perks: set a weapon's XP/action to 100 for quick checks. Kill one creature: that skill gains exactly +1. Wounding/attacking a corpse gives no XP; an NPC finishing the kill gives none. Check ordinary tracers, shotgun pellets, native hits with player projectiles off, melee and unarmed. Animal kills count.
3. Melee 100 vs Unarmed 0: knife/machete damage is +50%, fists/kicks unchanged. Reverse skills: fists/kicks +50%, knife/machete unchanged. Sadist gives no melee damage modifier. Ranged skills 100 halve existing offsets/spread radius without increasing damage.
4. With Barter 0 then 100, compare a sufficiently expensive item: purchase quote/check/payment becomes 50%; sale board/payment becomes 150%, rounded to whole nuts. Buy/sell once gives one action, unaffordable attempts give none. Complete a quest: payout and toast use 150% base and one action is awarded. Check reputation shop trades too.
5. With Medicine 0 vs 100, a bandage heals 25 vs 37.5; first aid heals 50 vs 75, capped by native health handling. One consumed item gives one action. Full-health refusal gives none; food and sleep give no Medicine XP.
6. With Survival 100, grab raw meat and cook it: cooked nutrition is 150% of its normal cooked value and one action is awarded. Re-grabbing/eating/reheating gives none. Unhandled food has normal nutrition and no local cooking XP. Save a bonused cooked item and a grabbed raw item, reload, then eat/cook: bonus/ownership survive.
7. Grab a stew pot or add raw meat, cook 9 raw nutrition: no action yet. Cook the next 1: one action. Cooked output uses the existing conversion setting × Survival factor (e.g. default 2 × 1.5 at skill 100). Save after fractional raw credit and reload: credit resumes. Pouring/drinking/pre-cooked additions do not award XP. A handler change through the cooking API removes the earlier handler's fractional credit.
8. At default Athletics rate, walk/run about 500 m on foot: +1 point. Riding, paused time, jumping/falling and teleports must not count as foot travel. At skill 100 forward run speed is 7.5 vs native 5; walking/crouching unchanged. Driving 1 km gives +1 Driving point and no Athletics XP; rate changes take effect live.
9. Compare equal-speed run-over impacts at Driving 0/50/100: 1x/3x/5x baseline native bumper damage, replacing the fixed 4x on the player's car. NPC car multiplier remains configured. A human run-over kill grants the normal killing insanity; with Sadist it grants half. Coyotes aggression may also add its separate +5. Consecutive run-over kills can trigger Bloodbath; none award weapon XP.
10. Hover all twelve TAB skills: descriptions state only their effects, fit the existing hint panel, and XP has its own column. Crafting stays inactive. SMGs appear between Pistols and Rifles; an SMG kill increases only SMG XP, and its spread uses only SMG skill.
11. Debug > Survival > Mental break; play 60 seconds. Verify three red-accent perk cards plus the fourth Persevere card with its supplied icon. Choose Persevere: no new perk/icon or increased random-break chance, Hunger/Thirst=100, Fatigue=80, Insanity=35. Repeat a reward-bearing episode: the three perk offers prepare again. A chance-triggered break still shows no decision and grants nothing.

Confirming log lines: `Skill increased: <name> +N -> value.`, `Player skills loaded: 12; ...`, verbose `Ram: car hit -> ... damage (x...)`, Survival insanity logs for aggression/killing with the separate amounts, `Persevere selected: no insanity perk granted.`, `Persevere: hunger=100; thirst=100; fatigue=80; no insanity perk granted.`, and `Mental break resolved: Persevere; insanity reset to 35.`

## Initial build and deployment (before Persevere/SMGs)

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with **0 warnings and 0 errors**. Apocalypter was closed and the Apocasetter disabled-WW manifest was absent before deployment. Built and installed DLL MD5 both equal **0A43DE9028AF2EFA077AF4FC228069B0**. Named copy: `bin/Release/WildWasteland_2.0.0_skills_progression_20261023_164046.dll`.

About 650 code lines added/changed across PERKS (`PerksModule`, its Skills partial and `NativeSkills`), Survival (`CookingSkills`, stew production and kill insanity), Gunplay attribution/spread, Quests transactions, Vehicles ram scaling, HUD skill rows and the required Core API/shared-action dispatchers. The earlier cause was display-only skills with no XP/effects and a fixed Vehicles ram multiplier; those now use the typed action/effect paths described above. Ten Developer XP-rate config entries were added under `[Perks / Skills]` in tools.cfg. No game, harness, simulation or further tests were run; Denis performs in-game and performance acceptance. No commit/push/release was made.

## Final build with Persevere and separate SMGs

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with **0 warnings and 0 errors** and deployed the complete follow-up. Built and installed DLL MD5 both equal **35692E1065D00F6948753F028DECB23F**. Named copy: `bin/Release/WildWasteland_2.0.0_skills_persevere_smgs_20261023_164611.dll`. Apocalypter was closed; the disabled-WW manifest was absent. No further tests were run.

The follow-up changes `PerksModule` (separate SMG data/display order/save migration and non-awarding Persevere choice), `MentalBreakView` (four cards), `Insanity.Choose` (Persevere needs/reset), HUD skill rows (twelve), and the typed skill enum; roughly 70 additional changed lines and the supplied icon. Total new Developer XP-rate entries are now eleven, including **SMGs XP per action** (default 10). Persevere does not alter the ten-perk permanent roster. No commit/push/release was made.
