#!/bin/sh
# Compile Core + ONE module (other modules replaced by empty stubs) to check a module in isolation.
# Usage: MANAGED=... BEPCORE=... sh tools/build-module.sh <ModuleFolder> [outdir]
#   ModuleFolder = Debug | Compatibility | Saves | NPCAI | Presentation | Gunplay | HUD | Vehicles | Loot | Patrols | POI | Quests | Survival | Tools | Developer
set -e
cd "$(dirname "$0")/.."
MOD=$1; OUT=${2:-/tmp/ww-$MOD}
[ -d "src/Modules/$MOD" ] || { echo "no src/Modules/$MOD" >&2; exit 1; }
TMP=$(mktemp -d)
mkdir -p "$TMP/src/Modules"
cp -r src/Core "$TMP/src/"
cp -r "src/Modules/$MOD" "$TMP/src/Modules/"
cp build.sh "$TMP/"
stub() { # folder ns class id
  [ "$1" = "$MOD" ] && return 0
  echo "namespace WildWasteland.Modules.$2 { public sealed class $3 : WildWasteland.Core.ModuleBase { public override string Id { get { return \"$4\"; } } public override string DisplayName { get { return \"$4\"; } } public override void BindConfig(WildWasteland.Core.ModuleConfig c) { } public override void Init() { } } }" > "$TMP/src/Modules/_Stub_$1.cs"
}
stub Debug Debug DebugModule Debug
stub Compatibility Compatibility CompatibilityModule Compatibility
stub Saves Saves SavesModule Saves
stub NPCAI NPCAI NPCAIModule NPCAI
stub Presentation Presentation PresentationModule Presentation
stub Gunplay Gunplay GunplayModule Gunplay
stub HUD HUD HudModule HUD
stub Vehicles Vehicles VehiclesModule Vehicles
stub Loot Loot LootModule Loot
stub Patrols Patrols PatrolsModule Patrols
stub POI POI PoiModule POI
stub Quests Quests QuestsModule Quests
stub Survival Survival SurvivalModule Survival
stub Tools Tools ToolsModule Tools
stub Developer Developer DeveloperModule Developer
sh "$TMP/build.sh" "$OUT"
rm -rf "$TMP"
