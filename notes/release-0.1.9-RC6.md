# Wild Wasteland 0.1.9 RC6

Release candidate 6 fixes newly offered deliveries to empty camps with Apocalypter Quests 1.8.0. Numeric plugin version **0.1.9**; tag **v0.1.9**. Gameplay and performance remain **Testing**.

## Change

The original destination picker classifies camps by faction names in their children, without checking for a recipient. The reported Deliver 4 Dogfood save points to Camp_8, which contains EnemySpawn_Coyotes_V1 despite being empty on arrival.

QuestsCompat now filters the original candidate list before the original faction, distance, duplicate and random-selection rules run. A destination needs an active, living Health NPC with an enabled collider, within the smaller of 120 metres and the configured delivery radius. Talk's original ResolveNpc/IsFriendly and Quests' IsPlayerEnemy must accept the recipient. The existing sliced People cache supplies candidates; health and positions are read live. No new world scan, scheduler job, config entry or save format is added. The optional mods' DLLs are unchanged.

Only loaded recipients qualify. If none qualify, the source mod can offer another job type. This does not reserve the NPC against later death or unloading. Already accepted deliveries keep their saved destination; return their goods to the original giver through the return-goods dialogue for a penalty-free cancellation.

## Install and test

Close the game and extract WildWasteland-0.1.9-RC6.zip into BepInEx/plugins. All RC5 assets are included. Keep Apocatalker 2.0.0 and Apocalypter Quests 1.8.0 installed separately with their data.

1. Restart and confirm `Loading [Wild Wasteland 0.1.9]` and `[QuestsCompat] started`, without a compatibility hook error.
2. Return the four cans for the existing dogfood job to its original Pup; its saved destination is deliberately unchanged.
3. Request new work. Empty destinations must be excluded; a delivery is offered only when a loaded, living, talkable, non-hostile recipient is near a qualifying friendly camp. If none exist, expect another quest type or no offer.
4. Carry all goods to the selected camp and talk to its recipient within the quest radius. Expect the original item hand-in, reward and quest completion.
5. With global VerboseLog enabled, rejected candidates log `[QuestsCompat] Delivery camp skipped: no living talkable recipient at ...`.

Validation: `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` passed with zero errors and seven existing warnings. Built/deployed DLL MD5: `4F72F070BE8650BE8C9747D2050C7E36`. The fix adds 43 lines in QuestsCompatModule.cs; Plugin.VERSION is the release metadata change. No config entries changed. In-game verification is Denis's task.

[Previous candidate: 0.1.8 RC5](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.8).
