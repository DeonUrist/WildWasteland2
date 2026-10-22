# Wild Wasteland 0.1.10 RC7

RC7 replaces the RC6 delivery destination restriction with the requested return/failure flow. Numeric plugin version 0.1.10; tag v0.1.10. Status: Testing.

## Behavior

- Original delivery selection is restored, including distant camps whose NPCs are not loaded yet. The four dogfood cans in the reported save still belong to its original quest.
- Patrols provides its existing saved cleared-camp flag and native spawner readiness through Services.Camps. A destination already known to be cleared fails the delivery even when distant. A missing or unloaded camp is never classified as empty.
- On arrival, wait for native spawners to finish. If there is no living, talkable, non-hostile recipient within the quest radius, remain within 30 m of the destination for 15 seconds of active play and allow a fresh sliced People scan. The quest then becomes Return N Items. Its message and journal explain: There was nobody to deliver to. Return the goods to the original giver.
- The map/compass turn-in marker follows the original giver, falling back to the last known position. Give back the goods uses the source mod's original quantity check, item removal and quest removal, with the player explaining why the goods came back. No delivery reward is paid.
- Returning to a ready but empty return point for the same observation period fails the quest. A known-dead return camp or the return giver's observed death also fails it. The source's automatic giver respawn is bypassed only for this return stage. A living giver who has moved updates the saved return position.
- The original delivery time limit continues during the return. Expiry while returning uses normal quest failure rather than an accusation of theft. Failures use the original FailQuest reputation rules and retain the goods; returning all goods uses the original penalty-free ReturnGoods action.

The return stage is stored in the original serialized quest fields (Place marker, title, flags and reward); no new save format or config entry. Save/load resumes the return. Disabling the camp provider defers empty-camp checks; it does not turn unloaded NPCs into failures. Optional Talk/Quests DLLs remain unchanged.

## Test in game

1. Restart, load the existing dogfood quest and approach its destination within 30 m. Wait for the native spawns, then 15 seconds of active play. If nobody can receive the goods and the camp is not already known dead, expect Return 4 Dogfood, the explanation, and a marker back to the Pup.
2. Save/reload during the return. Bring all four cans back and choose Give back the goods. Expect There was nobody to deliver to in the player dialogue, removal of the four cans and the quest, no delivery reward and no return penalty.
3. With a return quest active, visit its ready return point with the giver absent. After 15 active-play seconds and a fresh scan, expect Job failed (nobody is there to take the goods back). No giver should be conjured by the source's respawn loop for this return.
4. A delivery whose destination is recorded as cleared by Patrols should instead immediately report Job failed (destination camp is dead). Unknown, distant and unfinished-spawning camps must not fail for absence alone. Distant deliveries remain eligible for offers.
5. A normal delivery to a live recipient must still hand in, pay and complete through the original source logic. Check map/compass tracking across an origin shift and save/load.

Transition/failure messages use the original AddToast/FailQuest paths and appear in CleanHUD's message log and BepInEx log. No new verbose switch is needed. In-game verification remains Denis's task.

Validation: Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` passed with zero errors and seven existing warnings in an isolated checkout, excluding concurrent CleanHUD edits. Built/deployed DLL MD5: `59E49A1CC5F0CA3A9CA42494B5322D25`. Changes are approximately 210 lines across DeliveryRecovery, its QuestsCompat wiring and the approved Core/Patrols camp-status service, plus metadata/documentation. No config entries changed. Gameplay and performance remain Testing.

[Previous candidate: 0.1.9 RC6](https://github.com/DeonUrist/WildWasteland/releases/tag/v0.1.9).
