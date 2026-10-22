#!/bin/sh
# Compile Core + ONE module (other modules replaced by empty stubs) to check a module in isolation.
# Usage: MANAGED=... BEPCORE=... sh tools/build-module.sh <ModuleFolder> [outdir]
#   ModuleFolder = PlayerBody | NPC | Guns | HUD | Patrols
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
stub PlayerBody PlayerBody PlayerBodyModule PlayerBody
stub NPC NPC NpcModule NPC
stub Guns Guns GunsModule Guns
stub HUD HUD HudModule HUD
stub Patrols Patrols PatrolsModule Patrols
sh "$TMP/build.sh" "$OUT"
rm -rf "$TMP"
