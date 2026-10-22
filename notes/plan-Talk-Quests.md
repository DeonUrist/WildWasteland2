# Plan: Talk + Quests modules for Wild Wasteland (target 0.1.5 RC3 / 0.1.6)

2026-10-18 preparation update: full Quests 1.8.2 source is now available and Denis has requested native integration with quest/hiring UI independent of Talk; installed Talk must add its existing content. See [prepare-native-quests.md](prepare-native-quests.md) and its pinned source manifest for the current contract. The older binary-only inventory and primary-save-store proposal below are historical.

Implementation update (2026-10-15): Denis chose an optional compatibility bridge for the installed standalone mods instead of this full port. See [compat-Talk-Quests.md](compat-Talk-Quests.md) for the six accepted decisions, implemented scope and 0.1.7 RC4 test steps. Work packages and recommendations below remain the original research plan, not the release's implementation contract.

Written 2026-10-15 from a review of `E:\SteamLibrary\steamapps\common\Apocalypter\ModDev\ModCheck`. Both mods ship as **DLLs only** (no source anywhere under `V:\Repos\Apocalypter`), so the review is from the disassembled IL (ikdasm + Mono.Cecil call graphs). Everything below names the real classes and methods in those binaries so the porting session can find them again after decompiling.

| Mod | DLL | GUID / version | Role |
|---|---|---|---|
| Apocatalker | `Apocatalker\ApocalypterTalk.dll` (73,728 B, 2026-10-10) | `com.example.apocalypter.talk` 2.0.0 | **Dependency**: dialogue engine. Text-file prompts/intros/pools, flags, TMP uGUI window, `TalkApi` for other mods. |
| Apocalypter Quests | `ApocalypterQuests\ApocalypterQuests.dll` (244,224 B, 2026-10-13) | `local.apocalypterquests` 1.8.0 (BepInDependency on Talk) | **Quest mod**: 7 quest types, factions/reputation, camp takeover walkers + garrisons, hireable followers (`Hire`), voice lines, IMGUI quest log/tracker/toasts, map markers via *Immersive Map* reflection. |

Data shipped next to them: `Apocatalker\dialogue.txt`, `theme\{window,button,button_hover,button_active}.png`, `examples\*.talk.txt.example`; `ApocalypterQuests\quests.txt`, `factions.txt`, `Voice Actors\<actor>\<quest_id>\{offer,accept,remind,done}.wav` (Soldier96 x13 quests, LostSilver666 x2).

---

## 1. What the binaries actually do (facts that shape the port)

### 1.1 Apocatalker (namespace `ApocalypterTalk`)
- `Plugin : BaseUnityPlugin` — `Awake` binds 33 config entries (sections General, Dialogue, Dialogue Editor, Detection, Camera, Theme, Debug; full list in section 6) and calls `Harmony.CreateAndPatchAll`. `Update` every frame: editor-action switches, F-key hotkeys (`Input.GetKeyDown`), a **0.25 s** NPC scan (`FindNearbyNpc`: `Physics.OverlapSphereNonAlloc` around the camera, name keyword match `NpcNameKeywords` / `HostileNameKeywords`, facing check), prompt "Press F to talk", `Open()` / `Close()`. `LateUpdate` forces the cursor free while talking.
- `Open()`: sets `Time.timeScale = 0` when `PauseWhileTalking`, disables camera/player `Behaviour`s whose type name contains `look`/`mouse` (`FreezeLook`), picks an `[intro]`, raises `TalkApi.ConversationStarted`. `Close()` restores.
- `InputPatches`: **four global Harmony prefixes on `UnityEngine.Input.GetKeyDown / GetButtonDown / GetAxis / GetAxisRaw`** that swallow Escape/"Cancel"/"Pause" and mouse-look axes while talking. In WW these must go: WW already has `Core.UI.EditorSession` (pause + cursor + Apocasetter input block) and CleanHUD's `CancelHook` on the `GetButtonDown.OnUpdate` dispatcher.
- `DialogueDb` / `PromptDef` / `IntroDef` / `Rules`: parser for `dialogue.txt`, `dialogue\*.txt`, `**\*.talk.txt` under `BepInEx\plugins`, `BepInEx\config\Apocatalker\*.txt` (last wins). Pure C#, no Unity dependency except logging. Port verbatim.
- `DialogueEditor`: the Apocasetter "switch ON to save" editor writing `my_dialogue.txt` and `.bak` files. Pure file code; port verbatim, expose on an F7 page instead of toggle entries.
- `DialogueUI`: uGUI canvas (`ScreenSpaceOverlay`), TMP text, 9-sliced sprites from `theme\*.png`, two `ScrollRect`s (chat log, prompt list), own `EventSystem` if none. 1200x900 design units.
- `TalkApi`: static events `ActionTriggered(action, npc)`, `ConversationStarted(npc)`, `ConversationEnded`; `IsTalking`, `CurrentNpcName`, `CurrentNpc`, `GetFlag/SetFlag/AllFlags/ClearAllFlags`, `LoadFile`, `Reload`. Flags persist in `BepInEx\config\ApocalypterTalk.story.txt` (or `<StoryProfile>.story.txt`) — **one file for all save slots** (by design, profile is manual).

### 1.2 Apocalypter Quests (namespace `ApocalypterQuests`)
- `Plugin.Awake`: ~70 config entries (section 6), `PatchAll()`:
  - Harmony **postfixes on `ApocalypterTalk.Plugin.Open/Close`** (`Hooks.OpenPostfix/ClosePostfix`) — found through `Chainloader.PluginInfos["com.example.apocalypter.talk"]`.
  - Harmony **postfix on `HutongGames.PlayMaker.Actions.CreateObject.OnEnter`** (`Hooks.CreateObjectPostfix`): filter `Fsm.Name == "Health"`, state `spawn`, owner not `Player*`, reads the action's `gameObject` field → `_Dead` corpse → `OnEnemyKilled`. WW already has the `Hooks.CreateObjectEnter` dispatcher (NPC corpses use it).
  - `TalkApiLink`: reflection on `ApocalypterTalk.TalkApi` (`GetFlag`, `SetFlag`, `ActionTriggered` event → `OnTalkAction`).
  - `TalkBridge`: **reflection into Talk's private fields** (`npcName`, `currentNpc`, `dialogueUI`, `promptContent`, `Talking`, `AddNpc`, `AppendChatLine`) and builds its own uGUI buttons inside Talk's prompt scroll content (`SyncButtons`, prefix `AQ|`). Fragile; in WW replaced by a service.
- `Update` every frame: deferred queue, `Hire.EarlyTeach`, `TrackSaveLoad` (polls `GameObject.Find("SaveLoadGame")` every 0.25 s and the FSM state every frame — WW's `SaveBus` does exactly this once), `Game.GameHours()` (cached `DayTime` FSM), delivery countdown, `Takeover.Update` (walkers), `Hire.Update` (follower AI, big), `SyncTalkUi`, hotkeys, then `Tick()` every **0.5 s**.
- `Tick` (0.5 s): `Takeover.NeutralizeTick`, `Takeover.GiverTick`, `SyncMapMarkers` (1 s, via `MapLink`), `ReputationTick` (3 s: `Game.FindPeople` = **full `PlayMakerFSM.FsmList` scan**), deadlines, goto/takeover progress, `RefreshLines` (1 s: `Game.FindItems` = **full FsmList scan per active fetch/deliver quest**, `Objective`, `DirectionsLine`, `ReputationText`).
- `OnGUI`: tracker (top right), toasts, quest log window (`J`), reputation menu; styles built once.
- `Game`: all world access. `FindPois` walks `MapMagic` tile children `Objects` (camps/caves/buildings/wrecks, faction from `EnemySpawn_<Faction>` children). `FindEnemies`, `LiveNames` (cached), `FindPeople`, `FindBodies`, `FindItems` iterate `PlayMakerFSM.FsmList` (Health / ID / ItemName FSMs). `VisitedIcons` = `FindObjectsOfType<TextMeshPro>` named `mapIcon` (colour = visited). `MoneyVar` = PlayMaker global int (`nuts` …). `SpawnItemsInFront` instantiates item prefabs found with `Resources.FindObjectsOfTypeAll`, registers them in `NewGO_ArrayList.ArrayList_Items` with a fresh `itemNameID` (same as the game's own spawn path). `SetPlayerEnemy` flips the `PlayerIsEnemy` FSM. `GroundAt` raycasts.
- `MapLink`: reflection on `ImmersiveMap.ImmersiveMapApi` (`AddQuestMarker`, `RemoveQuestMarker`, `AddTurnInMarker`, `RemoveTurnInMarker`, `ClearQuestMarkers`, `IsExplored`). **Without that mod there are no map icons at all** — this is why Denis sees none.
- Saves: `QuestBook.Serialize/Deserialize` (own text format, `Esc/Unesc`), written through **ES3 by reflection** as key `"ApocalypterQuests"` into the current slot (cache + straight-to-file, `SaveToCache/SaveToDisk`) plus a backup text file (`BackupPath`), on `SaveGame`; loaded on `isPlay` after `LoadGame`/`setSeed`. Also `Takeover.ActiveWalks/RestoreWalks` (walkers in the book) and `Takeover.Stamps` (garrison cooldowns).
- `Takeover`: spawns NPC groups from prefabs (`ScanForPrefab` = `Resources.FindObjectsOfTypeAll`, cached), drives them with the `Move` FSM and its own steering raycasts every frame per walker, garrisons captured camps, re-spawns dead givers (`GiverTick`), neutralises old spawners (`NeutralizeTick`).
- `Hire` (~130 statics, 80 methods): follower AI that **reaches into NPCAI and Apocapatrol by reflection** (`com.denis.apocalypter.npcai` `Inform` API, `Apocapatrol.Plugin`, `HumanArmPoses`, `RangedHumans`, `TeachApocapatrol`, `LendArmPose`, `AttachGuard`) — all of that now lives in WW modules under new names (known gap, `docs/modules.md` 1.4), so **Hire will silently lose aiming/arm poses/guard behaviour with WW** unless those calls are re-pointed to `Services.Npc` (`Inform` exists) and a new Patrols service member.
- `Voice`: own `AudioSource` on a DontDestroyOnLoad object, WAV reader (`ReadWav`) + `VoiceGain` filter; `Core.VoiceAudio.LoadWav` already does the same in WW.

### 1.3 Why the current `0.1.5 RC2` + these two DLLs misbehave together
- Talk's global `Input.*` prefixes run for every input call in the game, including WW's `Hotkeys`, CleanHUD menus and PlayerBody editors (all prefixes run; Apocasetter's InputBlocker is a separate prefix on PlayMaker actions).
- Both plugins own `Update/LateUpdate/OnGUI` MonoBehaviours outside WW's scheduler: no budget, no attribution in F7 → Performance; Quests' 1 s `FsmList` scans and per-frame `Hire`/`Takeover` steering are invisible to ModProfiler's per-module view.
- Quests' `TrackSaveLoad` duplicates `SaveBus` and writes an ES3 key during the native save without the Apocasaver transaction rules (notes/HostMods.md 2.6).
- Hire's reflection targets no longer exist (NPCAI / Apocapatrol assemblies are not loaded).
- No map icons (needs Immersive Map).

---

## 2. Target architecture in WW

Two new modules, both `DefaultEnabled = true`, registered after POI (`Plugin.RegisterModules`): **providers before consumers** — CleanHUD provides the windows/markers, Talk provides dialogue, Quests consumes both.

| Module Id | DisplayName | Source | Folder / namespace | ConflictsWith | Services provided |
|---|---|---|---|---|---|
| `Talk` | NPC dialogue | Apocatalker 2.0.0 (binary; no git — record DLL md5/date in `docs/modules.md`) | `src/Modules/Talk/` = `WildWasteland.Modules.Talk` (subfolders `Dialogue/` parser+rules+editor, `Ui/` fallback window, `Embedded/` default `dialogue.txt` + theme PNGs) | `com.example.apocalypter.talk` | `Services.Talk` (`ITalkApi`) |
| `Quests` | Quests & reputation | Apocalypter Quests 1.8.0 (binary) | `src/Modules/Quests/` = `WildWasteland.Modules.Quests` (subfolders `Book/` Quest, QuestBook, QuestFile, Phrases, QuestGen; `World/` Game, Factions, Takeover; `Hire/`; `Voice/`; `Bridges/` ImmersiveMapBridge; `Embedded/` default `quests.txt`, `factions.txt`) | `local.apocalypterquests` | none (raises nothing yet) |

Module-to-module contracts (all in `src/Core/Services.cs`, no type references across modules):

```csharp
// provided by CleanHUD (null when CleanHUD is off / conflicted)
public interface IDialogueWindow {
    bool Visible { get; }
    void Show(string npcName, string hint);          // opens the window, clears log + choices
    void Hide();
    void AppendLine(string speaker, string text, bool player);   // speaker "" = narration
    void SetChoices(IList<DialogueChoice> choices);  // replaces the choice list, keeps scroll
    event Action CloseRequested;                     // close button / Esc inside the window
}
public sealed class DialogueChoice { public string Key; public string Text; public int Order; public Action Click; }

// provided by CleanHUD: icons on the native M map (and optionally the compass)
public interface IMapMarkers {
    int Add(string owner, Func<Vector3> scenePosition, string icon, Color color, string label);
    bool Move(int id, Func<Vector3> scenePosition);
    bool Remove(int id);
    void RemoveAll(string owner);
    bool IsOpen { get; }                              // map currently shown (for Quests' "explored" logic it is not needed)
}

// provided by Talk
public interface ITalkApi {                            // = TalkApi + what Quests' TalkBridge reached by reflection
    bool IsTalking { get; } string CurrentNpcName { get; } Transform CurrentNpc { get; }
    event Action<string, string> ActionTriggered; event Action<string> ConversationStarted; event Action ConversationEnded;
    bool GetFlag(string name); void SetFlag(string name, bool value); IEnumerable<string> AllFlags(); void ClearAllFlags();
    void LoadFile(string path); void Reload();
    void Say(string text); void You(string text);                       // TalkBridge.Say / You
    void SetExtraChoices(string owner, IList<DialogueChoice> choices);  // TalkBridge.SyncButtons; Order -10 puts them first
}
```

`Services.Dialogue`, `Services.Map`, `Services.Talk` fields next to `Services.Compass`; providers set in `Init`, null in `Shutdown` with the `is` check; consumers null-check every call.

Fallbacks (same rule as every other module): Talk without CleanHUD uses its ported TMP window (`Talk/Ui/DialogueUI.cs`, verbatim, theme PNGs embedded); Quests without Talk logs "no dialogue" and keeps tracker/map/kill counting but offers no quests (as the standalone does); Quests without CleanHUD keeps the compass-less IMGUI tracker and gets no map icons (plus the Immersive Map bridge if that mod is present).

---

## 3. Work packages (in order; each is one PR-sized task)

### WP0 — Decompile on Denis's PC (prerequisite, 10 min)
Cloud sessions cannot reach NuGet (proxy 403), so decompile locally: `dotnet tool install -g ilspycmd` then
`ilspycmd -p -o V:\Repos\Apocalypter\_tools\decompiled\Apocatalker  "E:\...\ModCheck\Apocatalker\ApocalypterTalk.dll"` and the same for `ApocalypterQuests.dll` (`-r "E:\SteamLibrary\steamapps\common\Apocalypter\Apocalypter_Data\Managed"` so Unity/PlayMaker types resolve). Keep the output outside the WW repo (reference only, "port don't link"). If Denis has the real source repos, use them instead and record the commit.

### WP1 — Core (small, ~80 lines)
1. `src/Core/Services.cs`: add `IDialogueWindow`, `DialogueChoice`, `IMapMarkers`, `ITalkApi` and the three static fields (section 2).
2. `src/Core/GameMap.cs` (new, ~60 lines): the one shared lookup for the native map, so CleanHUD's markers and (later) Patrols' `TrafficMap` stop scanning separately: `Camera Camera` (`MapCheat_Camera`, from `Camera.allCameras` while missing, retry 5 s), `PlayMakerFSM Fsm` (`FsmUtil.Find(GameRefs.GameManager, "MapCheat")`), `RectTransform Image` (`RawImage_Map` found through the FSM's `ActivateGameObject` targets while inactive — AGENTS.md rule), `bool Open` (= Fsm enabled, state `on`, image `activeInHierarchy`, `GameRefs.Menu` state `play`), `Vector2 Viewport(Vector3 scene)` (= `Camera.WorldToViewportPoint`). Cleared on scene load. Patrols' `TrafficMap` is **not** changed in this round (separate follow-up issue: "Patrols: use Core.GameMap").
3. No new Hooks dispatchers: Quests uses `Hooks.CreateObjectEnter.Postfix` and Talk uses `Hooks.GetButtonDownUpdate.Prefix` (both exist).
4. `docs/harmony.md`: add the two subscribers.

### WP2 — CleanHUD: dialogue window + map markers (~450 lines, inside `src/Modules/CleanHUD/`)
1. `Widgets.cs` (new): move the private builders out of `Menu.cs` **unchanged** — `R`, `Img`, `T` (GlyphText), `Shade`, `PlateSprite`/`Plate`, `Frame`, `Row` (both overloads), palette constants `Gold/White/Dim/BarBorder/Red` — into `internal static class Widgets`; `Menu.cs` calls them (mechanical rename, zero behaviour change; verify by diffing the two files' method bodies).
2. `Dialogue/DialogueWindow.cs` (new, implements `IDialogueWindow`): own persistent canvas `CleanHUD.Dialogue` (same pattern as `LoadingScreen`'s `CleanHUD.Loading` canvas; sorting 31500 — above the HUD and Apocasaver's 31000 autosave clone, below the loading screen's 32000), built from `Widgets`: `panel_menu.png` plate 1200x900 (reuse Talk's `WindowWidth/Height` entries), title row with the NPC name (GlyphText, Gold), chat log = `ScrollRect` + vertical `LayoutGroup` of GlyphText lines (player lines Dim, NPC lines White), choice list = `ScrollRect` of `Widgets.Row` buttons (`MenuButton`, hover = gold bar like the menu rows), a hint line ("Esc closes") and a CLOSE `Row`. No TMP, no theme PNGs, no own `EventSystem` (the menu already ensures one; reuse `Menu`'s approach). `Show/Hide` toggle the canvas; `SetChoices` rebuilds rows (destroy + create is fine: it happens on clicks only, never per frame). Keyboard: Esc raises `CloseRequested`; Up/Down + Enter are optional (not in the original).
3. `Map/MapMarkers.cs` (new, implements `IMapMarkers`): entries `{Id, Owner, Func<Vector3>, icon key, color, label}`; one `RectTransform` child per entry under `GameMap.Image` holding a `RawImage` (glyph from `Assets.Tex("g_"+icon)` = the compass icons `flag`, `diamond`, `skull`, `camp`, `cross`, `dot`, `car`, `arrow`) and a `GlyphText` label; anchors set from `GameMap.Viewport(pos)` each tick. Job: `Scheduler.Interval(this, "Map markers", 0.1f, MapMarkers.Tick, Phase.LateUpdateLast)` — early-outs when `!GameMap.Open` (hides the children once), so the cost while the map is closed is one bool. Entries are removed on `RemoveAll`, scene load and Shutdown; children re-created when the map image changes (new scene).
4. `CleanHudModule`: `Services.Dialogue = _dialogue; Services.Map = _map;` in `Init`, null in `Shutdown`; `ToolPages`: nothing new (window size entries stay in Talk's config). F7 Game → HUD gets one toggle `Quest icons on map` (`[CleanHUD / HUD] ShowQuestMarkers`, default true) next to ShowConvoys/ShowLocations.
5. Optional (cheap, recommended): `IMessageLog` on `Services` so Quests' toasts ("Objective complete: …") go to CleanHUD's top-left `MessageLog` (`MessageHook.Push`) instead of IMGUI toasts.

### WP3 — Talk module (`src/Modules/Talk/`, ~1,100 lines ported + ~200 new)
Port verbatim (class names kept): `PromptDef`, `IntroDef`, `Rules`, `DialogueDb`, `DialogueEditor`, `TalkApi` (becomes the `ITalkApi` implementation, static surface kept for the port), `DialogueUI` (fallback only). Rewrite `Plugin` as `TalkModule : ModuleBase`:
- **Config** (`BindConfig`): settings.cfg `[Talk]` Enabled; `[Keys] Talk` (`Key.F`, was `TalkKey` KeyCode F=102), `[Keys] Reload dialogue` (`Key.None`, was F10 — F7 page gets a button), `[Talk] TalkRange` 10, `PauseWhileTalking` true, `LoadPackFiles` true, `SaveStoryProgress` true, `StoryProfile` ""; tools.cfg `[Talk / Detection]` NpcNameKeywords, HostileNameKeywords, NpcComponentType, `[Talk / Camera]` LookScriptKeywords, `[Talk / Theme]` WindowWidth 1200, WindowHeight 900, WindowBorder "16", WindowPadding "24,24,40,24", ButtonBorder "8", ChatTextOutline 3 (fallback window only), `[Talk / Debug]` DumpNearbyKey, LogDialogueUI. `cfg.Migrate` every entry from `com.example.apocalypter.talk.cfg` (`KeyMap` for KeyCode→Key). The Dialogue Editor's 12 "switch ON" entries are **not** bound: they become an F7 `ToolPage` (text fields + ADD / REPLACE / DELETE / RELOAD buttons calling `DialogueEditor.AddPrompt/AddPoolLine/EditLine`).
- **Data**: `Assets.ModuleDir("Talk")` = `plugins\WildWasteland\Talk\` holds `dialogue.txt` (copied from embedded on first run, like Patrols templates), `dialogue\`, `theme\`; `*.talk.txt` discovery under `BepInEx\plugins` kept; user files from `config\WildWasteland\Talk\` **and** the old `config\Apocatalker\` (still read for one release); flags file `config\WildWasteland\Talk\<profile>.story.txt`, copied once from `config\ApocalypterTalk.story.txt`.
- **Jobs**: `Scheduler.Interval(this, "Talk.Npc scan", 0.25f, Scan)` (the `OverlapSphereNonAlloc` scan; uses `GameRefs.MainCamera`, early-out `!GameRefs.InGame`), `Scheduler.EveryFrame(this, "Talk.Session", Tick)` only while a conversation is open (pending prompt/close, Esc, range check 4 m beyond `TalkRange`) — register once, early-out on `state == Idle` (one branch per frame).
- **Input**: `Hotkeys.Register(this, TalkKey, Open, when: () => state == Prompt)`. The talk prompt ("Press F to talk") is drawn by `Services.Dialogue` (`ShowHint`) or the fallback window. **Delete `InputPatches` entirely**: `Open()` acquires `Core.UI.EditorSession` (pauses when `PauseWhileTalking`, frees cursor, Apocasetter input block — exactly what `Open/FreezeLook/LateUpdate` did by hand) and subscribes `Hooks.GetButtonDownUpdate.Prefix` with a cheap handler that returns false for `Cancel`/`Pause` while talking (same pattern as CleanHUD `CancelHook`, `BlockCancelFrame`). `FreezeLook` is still needed when `PauseWhileTalking = false` (camera keeps running at timeScale 1); keep it, but resolve the `look/mouse` behaviours once per conversation from `GameRefs.PlayerCameraHolder` instead of `GetComponentsInChildren` on the camera root.
- **UI**: `Open()` → `Services.Dialogue != null ? Services.Dialogue.Show(npc, hint) : fallback.ShowDialogue(npc)`; choices built from `BuildMainList/BuildBranchList` + extra choices from `SetExtraChoices` (sorted by `Order`, quest buttons `-10`), mapped to `DialogueChoice.Click = () => ChoosePrompt(p)` (the original defers to the next frame through `pendingPrompt`; keep that).
- **F7 page** (`Core.UI.ToolGroups` "Weapons & HUD"? no — add group "Dialogue & quests" to `ToolGroups.Order`): status (files loaded, warnings, current NPC), RELOAD, DUMP NEARBY, the editor fields, flag list with CLEAR.
- `Shutdown`: close the session, destroy the fallback canvas, `Services.Talk = null`, restore look behaviours.

### WP4 — Quests module (`src/Modules/Quests/`, ~5,000 lines ported + ~400 changed)
Port verbatim: `Quest`, `QType`, `QuestBook`, `QuestFile`, `Phrases`, `QuestGen`, `Util`, `Faction`, `Factions`, `FactionDump`, `Poi`, `EnemyInfo`, `ItemHit`, `Walker`, `SavedWalk`, `Takeover`, `Game` (then optimise per WP5), `Hire`, `Voice`, `VoiceGain`. Replace: `TalkBridge` + `TalkApiLink` → `Services.Talk`; `MapLink` → `Services.Map` + `Services.Compass` (keep `MapLink` as `Bridges/ImmersiveMapBridge.cs`, `object`-typed, `NoInlining`, used only when `ImmersiveMap.ImmersiveMapApi` exists); `Hooks` → dispatcher subscriptions; `Plugin` → `QuestsModule : ModuleBase` + `QuestsPages.cs`.
- **Config**: settings.cfg `[Quests]` Enabled, MaxActiveQuests 5, RewardMultiplier 1, ShowTracker true, `[Quests / Generated]` Enabled true, Types "fetch,kill,goto,talk,clear,takeover,deliver"; `[Quests / Hire]` Enabled true, RideInCars true, ShootFromCar true; `[Quests / Voice]` Enabled true, Volume 10 (range 0–10), Spatial 0; `[Keys] Quest log` (`Key.J`, was 106), `[Keys] Dismiss follower` (`Key.Delete`, was 127). Everything else (Items, Quests tuning, Kills, Goto, Takeover, Deliver, Reputation, RepVendor, Factions, Hire tuning, Debug) → tools.cfg `[Quests / <Section>]` with the same keys and defaults (section 6). Migrate all from `local.apocalypterquests.cfg`.
- **Data**: `plugins\WildWasteland\Quests\{quests.txt, factions.txt, Voice Actors\}` (quests/factions embedded as defaults, written when missing; Voice Actors copied by hand / release zip like other assets; `Voice.Root` falls back to the old `plugins\ApocalypterQuests\Voice Actors`). `npc_names.txt`, `faction_dump.txt` → `config\WildWasteland\Quests\`.
- **Jobs** (replace `Update/Tick/OnGUI`):
  - `EveryFrame "Quests.Frame"`: deferred queue + delivery `TimeLeft -= Time.deltaTime` (only when a deliver quest is active) + `Takeover.Update` **only while `walkers.Count > 0`** + `Hire.Update` **only while a follower is active** (both already early-out; keep the per-walker steering as is — it is the original behaviour).
  - `Interval "Quests.Tick" 0.5 s`: `NeutralizeTick`, `GiverTick`, deadlines, goto/takeover checks, `SyncMarkers` (1 s gate kept), `ReputationTick` (3 s gate kept, reads the people cache from WP5), `RefreshLines` (1 s gate kept, reads the item cache from WP5).
  - `Gui "Quests.Hud"`: tracker, toasts, quest log, rep menu — verbatim IMGUI through `Scheduler.Gui` (no own `OnGUI`), styles from `Core.UI.Skin` only where the original used `GUI.skin` defaults. Toasts go to `Services.MessageLog` when present (WP2.5).
  - `Hotkeys.Register`: quest log key (`allowWhilePaused: false`), dismiss key (Hire).
- **Save/load** (decision needed, section 7): recommended = **per-slot sidecar** `config\WildWasteland\Quests\Saves\<slot>.quests.txt` written in `OnSaveEnd` through `SaveBus.AfterSave` (POI precedent), content = `QuestBook.Serialize()` verbatim; `OnLoadBegin` → `ResetSession`; `OnWorldReady` → `LoadBook`: sidecar, else the old ES3 key `ApocalypterQuests` from the slot file (read-only, through `ES3.Load<string>` directly — WW links ES3, no reflection), else the old backup file. This removes `ResolveEs3/SaveToCache/SaveToDisk/DiskSettings/CachedSlotFiles/SlotId` (slot comes from `SaveBus.Slot`). Delete `TrackSaveLoad`; the `IsLoadingState` logic is `SaveBus.StateChanged`.
- **Kills**: `Hooks.CreateObjectEnter.Postfix(this, OnCreateObject)` with the original cheap filter first (`__instance.Fsm.Name != "Health"` reference-free string compare after a length gate), then the `_Dead` logic. Note NPC's existing postfix on the same dispatcher runs first (init order) — no interaction.
- **Talk integration**: subscribe `Services.Talk.ConversationStarted/Ended/ActionTriggered` in `Init` (null-checked; re-probe every 5 s if Talk starts later, like NPC's ApBody probe); `BuildButtons` → `Services.Talk.SetExtraChoices("Quests", list)` on open and after every quest state change (not every frame: `SyncTalkUi` is replaced by a dirty flag set where `uiDirty` is set today).
- **Map icons** (the feature Denis asked for): in `SyncMapMarkers`, for every active quest with `HasPos` add/move one `Services.Map` marker (`flag`, quest title as label; deliver/takeover `camp`; kill area `skull`) and one `turn-in` marker (`diamond`, "Report in: <giver>") when the objective is done and `ReturnToGiver` (position = live giver via `Game.FindLive`, else `GiverPos`). Mirror the objective marker to the compass with `Services.Compass.Add` (gold flag, `ShowDistance`), removed when the quest completes/fails. `IsFound` (goto "already visited") keeps `Game.VisitedIcons` but reads the `mapIcon` roots from CleanHUD's `SceneCache` if a `Services` accessor is added later; for now keep `FindObjectsOfType` **behind the existing 30 s cache** (`visitedAt`).
- **Hire**: port verbatim but re-point the dead reflection: `Inform` → `Services.Npc.Inform(npc, target)`; `TeachApocapatrol`/`LendArmPose`/`AttachGuard`/`ForceRanged`/`PatrolShootDistance` reach `Apocapatrol.*` types — add `IPatrolsApi` to `Services` only if Denis wants followers to shoot from cars in this round; otherwise ship Hire with those paths logging once "not available with Wild Wasteland" (same behaviour it has today next to WW). **Recommendation: second round** (section 7).
- **Voice**: `Voice.ReadWav` → `Core.VoiceAudio.LoadWav`; keep `VoiceGain`; the AudioSource object is created in `Init` and destroyed in `Shutdown`.
- **F7 pages** (group "Dialogue & quests"): Quests overview (active quests, rep per faction, FORCE OFFER / COMPLETE / ABANDON buttons for testing, FACTION DUMP, RECORD NPC NAMES toggle), `ConfigPage`s for `Quests / *` tools.cfg sections, Hire page (status, DISMISS).
- `Shutdown`: `Takeover.ClearWalks` (destroy spawned walkers? no — they are saved NPCs; leave them, stop steering), `Hire.Dismiss`, remove markers (`Services.Map/Compass.RemoveAll("Quests")`), `MapLink.Reset`, stop voice.

### WP5 — Scheduler / performance work on the ported `Game` class (the "optimise to my scheduler" part)
Measured hazards in the binary and the exact fix for each:

| Hot path (original) | Cost | Fix in WW |
|---|---|---|
| `Game.FindItems` (every 1 s per active fetch/deliver quest) iterates all `PlayMakerFSM.FsmList` looking for `ItemName` FSMs, then `GetComponents` per item | O(all FSMs) per second | `Quests.Scan` **Sliced job (500 µs)** that walks `FsmList` incrementally and maintains three dictionaries keyed by instance id: items (`ItemName` FSM + cached `Quantity` FsmInt), people (`Health` FSM, name, tag), bodies (`ID` FSM with `_Dead`). Swept for destroyed objects each pass, cleared on scene load. `FindItems/FindEnemies/FindPeople/FindBodies/LiveNames` become distance filters over the caches (no allocation: reuse `List<ItemHit>` buffers). Same semantics, ≤ 0.5 ms/frame worst. |
| `ReputationTick` → `FindPeople` every 3 s | O(all FSMs) | reads the people cache |
| `FindPois` (on every offer / takeover pick) walks all MapMagic tiles' `Objects` | O(objects) per offer only — acceptable; add a per-tile cache refreshed on `TerrainTile.OnTileApplied` (LocationMarkers precedent) so repeated offers are O(1) |
| `VisitedIcons` `FindObjectsOfType<TextMeshPro>` | heavy, 30 s cache already | keep cache; later switch to `SceneCache` mapIcon roots |
| `TrackSaveLoad` `GameObject.Find` every 0.25 s | small | removed (SaveBus) |
| `Takeover.Update` per walker: raycast steering every frame (`Blocked` ×3 rays, `Steer`) while walkers exist | 5 walkers ≈ 15 raycasts/frame for ≤ 300 s after a takeover | keep behaviour; `NextSteer` already gates to ~0.2 s per walker; use `Physics.RaycastNonAlloc` with a reused buffer; `SolidMask` cached (it is) |
| `Hire.Update` per frame while a follower is active (threat scan `Threats` every `nextThreat`, `FindDeep`, `GetComponentsInChildren` in `ScanParts/LogBody`) | medium, only with a follower | keep; move `Threats` onto the people cache; nothing per frame allocates once `ClearShot`'s `RaycastAll` becomes `NonAlloc` |
| `OnGUI` style creation, string building in `RefreshLines` every 1 s | small | `RefreshLines` already caches strings; keep |
| Talk `Update`: `OverlapSphereNonAlloc` 0.25 s, `Camera.main` fallback scan | small | `GameRefs.MainCamera`; keep 0.25 s |
| Talk `InputPatches` 4 global Input prefixes | per input call, every frame | deleted (EditorSession + Cancel hook) |
| Quests `SyncTalkUi` per frame (`NeedsSync` string signature compare) | small | dirty flag |

Budget target per AGENTS 5.4: both modules together avg ≤ 0.1 ms/frame idle, worst ≤ 1 ms during a takeover; record in `docs/perf-log.md` (ModProfiler baseline of the two standalone DLLs first, scenarios: idle at a camp with 3 active quests incl. one fetch; takeover in progress; follower hired and driving).

### WP6 — Docs, wiki, verification
`docs/modules.md` rows (status `in (Testing)`, source = DLL md5 + date, decompiled), 1.3 services table (+Dialogue, Map, Talk), `docs/harmony.md`, `docs/decisions.md` (save format decision, Hire scope), `notes/port-Talk.md`, `notes/port-Quests.md`, wiki pages `Modules/Talk/*`, `Modules/Quests/*` (one per quest type + Factions + Hire + Voice), `tools/wiki-index.py`, board issues per feature (Testing). README: lists `ApocalypterTalk.dll` and `ApocalypterQuests.dll` among the DLLs to remove; config harness run for both modules.

---

## 4. Build / order of execution

1. WP0 (Denis or local agent) → 2. WP1 + WP2 in one session (CleanHUD provider first; Talk fallback path is what keeps it testable without WP3) → 3. WP3 (Talk) → build, deploy, Denis tests dialogue with the CleanHUD window and with CleanHUD off (fallback) → 4. WP4 + WP5 together (Quests cannot be verified without the scan caches) → build, harness, deploy → 5. WP6. Each step: `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` on Windows, zero errors; mono `tools/config-harness` by a cloud session for WP3/WP4 (new config entries). Version: `0.1.6` (`Plugin.VERSION`), tag `v0.1.6`, RC3 title; RC2 stays as is.

Estimated size: Core ~80, CleanHUD ~450 (+ ~150 moved), Talk ~1,300, Quests ~5,400 (of which ~400 new/changed), docs ~300 lines.

---

## 5. In-game test plan (Denis)

1. Startup summary shows `Talk on`, `Quests on`, no `PATCH CONFLICTS`, `core hooks` lists `CreateObject.OnEnter` subscribers NPC, Quests and `GetButtonDown.OnUpdate` with CleanHUD, Talk. With the two old DLLs still in `plugins\`, both modules must show `OffConflict` naming the DLL.
2. Walk up to a Bolter: "Press F to talk" hint in CleanHUD style; F opens the gold/white window (plate, title, log, choices), the game pauses, cursor free, mouse look frozen; Esc and CLOSE both close; F3 settings and Apocasetter F6 do not open while talking. Repeat with `PauseWhileTalking` off.
3. Choose "Got any work?" (quest button first in the list): offer text + voice line; ACCEPT → tracker line top right, compass flag with distance, **M map shows a flag with the quest title and, when done, a diamond "Report in: Bolter"**; the marker follows a moving giver; after completion both vanish.
4. Fetch quest: pick up 3 bandages → "Report in" appears; hand in → nuts increase (PlayMaker global).
5. Kill quest: kills counted (corpse creation), bodies bonus within 35 m.
6. Takeover: walkers arrive, camp flips, garrison spawns; F3 → Modules → Performance shows `Quests.Frame`, `Quests.Tick`, `Quests.Scan`, `CleanHUD.Map markers` with avg < 0.1 ms, worst < 1 ms.
7. Save, quit to menu, load: active quests, deadlines, rep and walkers restored from the sidecar; a save made with the standalone mod loads its quests once from the ES3 key. Save with WW, load without WW: no errors from the game.
8. Toggle Quests off/on and Talk off/on in F3 Modules: window closes, markers disappear, re-enable restores. CleanHUD off: Talk falls back to the TMP window; no map icons; compass gone.
9. ModProfiler: `WildWasteland.dll` row not slower than the two standalone DLLs together in the three scenarios above.

---

## 6. Config inventory (migration keys, defaults from the binaries)

Apocatalker `com.example.apocalypter.talk.cfg`: `[General] TalkKey=F, TalkRange=10, PauseWhileTalking=true`; `[Dialogue] ReloadKey=F10, LoadPackFiles=true, SaveStoryProgress=true, StoryProfile=""`; `[Dialogue Editor] LastResult, NewPromptText, NewPromptReply, NewPromptNpc, AddPromptNow, NewLinePool="rumors", NewLineText, AddLineNow, FindText, ReplaceWith, ReplaceLineNow, DeleteLineNow, ReloadNow` (not migrated: become F7 controls); `[Detection] NpcNameKeywords="bodyguard,pistoleer,pup,merchant,bolter,lugger,gunnar,mechanic,organic…", HostileNameKeywords="enemy,raider,hostile,zombie,mutant,warlord,bandit,dead", NpcComponentType=""`; `[Camera] LookScriptKeywords="look,mouse"`; `[Theme] WindowWidth=1200, WindowHeight=900, WindowBorder="16", WindowPadding="24,24,40,24", ButtonBorder="8", ChatTextOutline=3`; `[Debug] DumpNearbyKey=None, LogDialogueUI=false`.

Apocalypter Quests `local.apocalypterquests.cfg`: `[General] QuestLogKey=J, MaxActiveQuests=5, RewardMultiplier=1, ShowTracker=true`; `[Generated] Enabled=true, Types="fetch,kill,goto,talk,clear,takeover,deliver"`; `[Advanced] MoneyVariable=""`; `[Items] HandInRadius=10, MinLiquidFraction=0.5, CountCarriedItems=true`; `[Quests] GiverCooldownDays=1, ItemRange=4000, MinimumHours=24, ExtraHoursPerKm=4, MaximumHours=96, NoRepeatDistance=400, NoRepeatGameHours=12, ChainOfferGameHours=24, GiverRespawnDistance=500`; `[Kills] BodyRadius=35, BodyBonus=0.1, KillRadius=300, QuestRange=4000`; `[Goto] MinDistance=150, MaxDistance=1800`; `[Takeover] CampRadius=120, MinDistance=200, MaxDistance=4000, KillsNeeded=4, WalkerSpawnDistance=250, GroupSize=5, WalkSeconds=300, GarrisonSize=4, GarrisonRange=500, GarrisonCooldownGameHours=12, ScoutRange=250, PatrolAtCamp=true`; `[Deliver] MinDistance=400, MaxDistance=3000, GameHoursBase=24, GameHoursPerKm=4, CampRadius=120`; `[Reputation] StolenPenalty=3, FailPenaltyMultiplier=2, HostileAtRep=0, RecoverGameHours=24, PayPerRep=0.005, MinimumPayFraction=0.1, MaximumPayMultiplier=0`; `[RepVendor] Enabled=true, BuyPrice, SellPrice, MaxPerDeal, Npcs="merchant"`; `[Factions] HomeRadius=120, HumanFactions="Scrapyard,Coyotes", PeopleNames="bodyguard,pistoleer,pup,merchant,bolter,lugger,sco…"`; `[Hire] Enabled=true, Hirable="Bodyguard=5,Pup=1,Bolter=5,Pistoleer=5,Gunnar=15,Lugger=10", DurationGameHours=24, DeathPenalty=1, FollowDistance=6, CatchUpDistance=70, IgnoreCritters="Lizard", LeashDistance=28, RideInCars=true, ShootFromCar=true, RealShooting=true, ShotAnimation=false, RecoilDegrees=10, MagazineOverride, ReloadOverride, ReloadSoundFrom="Bolter=crossbow", ShotDamage=7, ShotInterval=0.9, RangedHires="Bolter", ArmPoseLike="Pistoleer=Scrud,Gunnar=Flexa,Lugger=Lugnut,Bolter=Boltjaw,Lugnut", RideRange=35, DismissKey=Delete`; `[Voice] Enabled=true, Volume=10, Spatial=0, Folder=""`; `[Debug] FactionDumpKey=None, RecordNpcNames=false`.

---

## 7. Decisions needed from Denis before WP3/WP4

1. **Save format**: sidecar `<slot>.quests.txt` (recommended; no ES3 write during the native save, Apocasaver-safe, old ES3 key read once) vs. keeping the ES3 key `ApocalypterQuests` verbatim (decisions 2026-10-13 pattern for *ported* mods; would need the HostMods 2.6 transaction rules and keeps the reflection-free `ES3.Save` call inside `SaveGame`).
2. **Hire followers**: port now but let the Apocapatrol/NPCAI-dependent parts (shooting from cars, arm poses, guard) stand down with a one-time log, or add `IPatrolsApi` (arm poses, ranged names, shoot distance) and `INpcApi.Inform` wiring in this round (+~150 lines in Patrols, +1 service). Recommended: stand down now, second round after the quests are verified.
3. **Immersive Map**: is it installed? If not, drop `MapLink` instead of keeping the bridge.
4. **Fallback TMP dialogue window** (Talk with CleanHUD off): keep (verbatim port, ~300 lines, theme PNGs embedded) or require CleanHUD for Talk (simpler, one window to maintain). Recommended: keep, it is free.
5. **Story flags per save slot**: the original uses one global flags file (+ manual `StoryProfile`). WW could key the file by `SaveBus.Slot` automatically (better) but that changes behaviour for existing `.story.txt` users; recommended: slot-keyed with a one-time copy of the global file into every existing slot's file on first run.
6. Version for the release that carries this: `0.1.6` RC3 (new modules are not a patch on RC2).
