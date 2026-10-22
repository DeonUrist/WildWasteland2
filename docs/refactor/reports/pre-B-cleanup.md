# Pre-Wave B redundancy cleanup (2026-10-22)

Local coordinator checkpoint on `refactor/main`, based on `577046c`. Denis requested cleanup of the reported leftovers and preparation to follow the existing refactor plan. This is a bounded coordinator change across package ownership boundaries; no Wave B package is marked complete or started.

## Removed code and evidence

The source search found declarations only for the ten reported members; none was registered as a runtime callback or named in a reflection lookup. Removed:

| File | Removed | Evidence / retained behavior |
|---|---|---|
| NPCAI/Navigation/_fromNPC/PoiNav/PoiNav.cs | NearestNode, GlidePoints | No callers. Active graph lookup, route following, traversal and stuck-watchdog code retained. |
| Debug/Npc/_fromNPC/Nav.Debug.cs | CellText, WhyName | CellText had no callers; WhyName was only called by CellText. Active traces/overlays retained. |
| Patrols/Vehicles/Templates.cs | SmallBumpers, JunkerBumpers, TruckBumpers | No readers; RollBumper reads each template's Bumpers array, which is unchanged. |
| POI/_fromQuests/Game.Pois.cs | PoiKinds | No readers; FindPois still uses QuestWorld.Pois. |
| POI/_fromTowns/TownDressingPlan.cs | TownDressingKit.ShellMesh | Allocated but never populated/read; Make receives the actual shell bounds separately. |
| POI/_fromTowns/TownGasStation.cs | LaneCenter | No readers; LaneSpread and active placement/routing retained. |
| Survival/Weather/_fromWeather/WeatherModule.Regions.cs | Sequence | No callers; UpdateLayout's existing placement remains. |

NPC/NpcModule.Config.cs: removed oldSection/oldKey from both V overloads and the obsolete argument pairs at eleven binding call sites. They were forwarded but never consumed after D12. The first four arguments (section/key/default/description including ranges), binding order and Main.Bind call are unchanged. Updated summaries.

D12 helpers: removed Assets.LegacyPluginDir, ModuleConfig.Migrate, the inert Core.Legacy class (FirstRunMain/FirstRunTools/Get/Exists) and KeyMap.FromKeyCodeName. All had no remaining source callers after Wave A. Removed unused ModuleConfig imports and corrected KeyMap's summary. Keep live Services/Log and other forwarders for pending packages; keep frozen ModAPI contracts even when not yet consumed.

## Verification

- `dotnet build WildWasteland.csproj -c Release -p:Deploy=false -v:minimal`: 0 warnings, 0 errors. Full output retained locally at `$env:TEMP\ww2-pre-b-build.log`. Existing CS0618 suppression unchanged.
- `python -B tools/refactor/check_partials.py`: 215 declarations, 0 missing implementations.
- `python -B tools/refactor/filemap_coverage.py --wave A`: 0 open items.
- `python -B tools/refactor/check_comments.py src/Core/Modules/ModuleConfig.cs src/Core/Util/KeyMap.cs src/Modules/NPC/NpcModule.Config.cs --max 10`: 0 missing summaries.
- Final removed-symbol search: no remaining occurrences of the removed specific names in src. Reviewed the source diff and checked whitespace.

Whole-source baseline, deliberately not a Wave B/C pass:
- `check_module_refs.py --baseline`: 1,078 heuristic findings. Output retained locally at `$env:TEMP\ww2-pre-b-refs.txt`; this command returns 0 in baseline mode even when it reports findings. Package mode must pass before accepting each Wave B package.
- `check_comments.py --max 1`: 6,563 members without summaries across 723 source files (nonzero exit expected). Output retained locally at `$env:TEMP\ww2-pre-b-comments.txt`. Owners must resolve the findings in their packages.
- No ownership lint for this coordinator cleanup: it deliberately spans B01/B03/B07/B08/B11/B13/B14 under the user's cleanup instruction. Future package branches must use the ownership gate.

No config entries, save formats/keys, module contracts, assets or live game files changed. No game/harness run, deployment or performance claim. Existing gameplay verification remains Denis's Wave D work.

## Ready to resume

README status and work-packages local execution instructions now match the actual local continuation. STATUS.md names B01 as next and gives the branch/worktree and gate instructions. A3-7's obsolete helper-parameter request is recorded as resolved; all other package work remains pending. Read README section 0 before original briefs: D12/D18 supersede migration/old-save compatibility instructions, while D13 asset moves and current-format preservation still apply.
