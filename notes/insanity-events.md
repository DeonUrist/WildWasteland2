# Insanity events — 2026-10-23, Testing

Supersedes the 60-second event and reset-to-35-for-every-choice behavior in earlier notes. The old clock and Choose code hardcoded 60 seconds/35 insanity and had no type-specific effects or protection against native sanity-removal buffers. Denis's previous log confirms a completed 60-second episode followed by Bloodbath resetting insanity to 35.

## Current behavior

- Each event randomly picks **VERTIGO** or **NO MORE DEATH**, lasting 30 seconds of normal gameplay. Pause, sleep and death do not advance the existing gameplay clock. Sleep activation remains blocked during either type.
- Reaching 100 (or Debug > Survival > Mental break) starts a reward-bearing event. Insanity stays at 100 throughout it and the required decision. After the event: choose one of three insanity perks, resetting insanity to **0**, or choose Persevere, granting no perk and setting Hunger/Thirst=100, Fatigue=80 and Insanity=35.
- Random chance events keep their starting insanity value frozen, as Denis clarified. After 30 seconds, that same value resumes and no choice/perk is granted. The five-minute chance check and existing Developer chance-per-perk setting remain.
- **VERTIGO** holds the native AlcoholEffect at full weight for the event, using its existing lens-distortion animation. The prior alcohol state/weight/enabled flag is restored when the event ends; a prior native timed effect resumes from its restored state.
- **NO MORE DEATH** uses the native Weapons/off 2 holster path, preserving slot items and ammo. It blocks weapon-slot draws and WeaponInHand activation, suppresses arm-model activation and temporarily disables the native attack/animation FSMs, including hands and kick. At the end those enable flags restore; weapons remain holstered until drawn normally. Moving, trading, healing and driving are not blocked.
- Native sanity-add/remove timers are suspended while insanity is locked, and new buffer writes are rejected. Direct Survival insanity additions and stew consumption respect the lock. The authoritative value is also pinned early, late and before serialization. Reward resolution clears deferred sanity buffers so the chosen reset value does not immediately rebound; random events restore normal processing at the frozen value.
- Both types play native mutant/monster sounds at random positions 8–22 m away every 3–6 seconds. Three reusable 3D sources cap concurrency; sounds pause with gameplay and stop at event completion. Positions track floating-origin shifts. These are hallucination audio only: no enemies, colliders or AI noise events are spawned.
- The HUD's top-right number becomes **INSANITY: VERTIGO** or **INSANITY: NO MORE DEATH** during the event, then returns to the normal numeric display. The event label remains visible if only the ordinary insanity-number option is off, while the parent HUD's normal visibility still applies. Its width reserves space for adjacent vehicle-status icons.
- Perk descriptions no longer mention HUD settings or implementation conditions. Paranoia reads "Sense nearby enemies within 30 metres." Existing effect gates remain unchanged.

## Files, save compatibility and performance

Main changes: Survival `Insanity.cs` plus new `InsanityEpisode.cs`, stew's direct sanity write, PERKS roster/debug/footer text, HUD `HudView.cs`, and the existing Core `IMentalBreak` contract. Approximately 330 added/changed code lines. No other in-progress navigation/POI/character edits were changed. No new configuration keys; the existing chance setting description and debug hint now say 30 seconds.

The save key remains `WildWasteland.Survival.MentalBreak.v1`, with payload version 2 adding event type and frozen value. Version 1 is still accepted; an old active duration is capped at 30 seconds and gets one of the two new types. New saves preserve the exact remaining time, type, reward/no-reward state, frozen insanity and five-minute countdown. No perk flags or item IDs change.

Audio clips are borrowed from the shared prefab catalog in a 500-microsecond scheduler slice; no new full-scene resource scan or physics query. Rig discovery is cached with the existing one-second resolve job. Model/FSM enumeration happens on entering No More Death, not every frame; active enforcement uses only cached references. Owned audio and temporary flags restore on scene change/module shutdown. This is a structural performance assessment, not an in-game measurement.

## Denis's acceptance steps

1. Verify the installed DLL MD5 below. Debug > Survival > Mental break, close settings: the top-right label names one of the two types. Repeat after resolving to see the other type. Both must end after about 30 gameplay seconds; pausing for ten real seconds must not count.
2. During a reward event consume sanity pills/alcohol, eat food/stew and perform an action that usually changes insanity. It must remain 100. Sleep must be rejected. Choose a perk: it becomes 0. In another event choose Persevere: Hunger/Thirst=100, Fatigue=80, Insanity=35; no new perk.
3. VERTIGO: the alcohol distortion starts immediately and lasts the whole event, then clears (or restores the earlier alcohol effect). Creature sounds come from changing directions/positions; pause silences them, and completion stops them. Loading/returning to title must leave no effect or sound behind.
4. NO MORE DEATH: start with a drawn weapon. It holsters. Try weapon slots, fists, kicking, picking up a weapon into an empty selected slot, healing and any optional pocket slot controls: no weapon/hands/kick may appear or attack. Inventory items and magazines remain. After the event, draw a weapon and kick normally. Healing/trading/driving continue to work during the event.
5. Save partway through each type and reload: same type, frozen value and remaining time resume; temporary restrictions/effects restore when complete. Save while awaiting a choice and reload: insanity remains locked at 100 until the selected outcome.
6. With an owned insanity perk, set Developer > Perks > Chance per insanity perk (%) to 100 and remain below 100 insanity. After the five-minute check, the random episode freezes that starting value, then resumes it after 30 seconds with **no perk decision**. Restore the desired chance afterward.
7. Hover Paranoia and other perks in TAB and the selection screen: descriptions contain gameplay effects without references to HUD settings. Toggle the HUD's ordinary insanity number off: the event type still appears while an event is active.

Confirming logs: `Mental break started: VERTIGO; 30 gameplay seconds; insanity frozen at 100; ...`, corresponding `NO MORE DEATH` and `Random mental break started: ...` messages, `Insanity sound bank ready: N creature clips.`, `Mental break resolved: <perk>; insanity reset to 0.`, `Mental break resolved: Persevere; insanity reset to 35.`, and `Random mental break ended: no perk awarded.`

No game execution, harness, simulation or performance measurement was performed. Build/deployment result follows after the required Windows build. No commit/push/release requested.

## Build and deployment

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` succeeded with **0 warnings and 0 errors**. The game was closed and the Apocasetter disabled-WW manifest was absent before deployment. Built/installed MD5 both equal **AA43F46D436727A0E63EA61C72424D2C**. Named build copy: `bin/Release/WildWasteland_2.0.0_insanity_events_20261023_170941.dll`. No further tests were run; gameplay acceptance remains with Denis. No new config keys or manual config edits; the existing chance-per-perk description now reflects 30 seconds.
