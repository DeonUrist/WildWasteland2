#!/bin/sh
# Binds every module's config outside the game (plain mono, no Unity runtime) to catch what BepInEx would throw in Plugin.Awake:
# AcceptableValueRange/entry type mismatches, duplicate (section,key) pairs across modules (and statics of one module that silently
# share one entry), sections that do not start with the module id, exceptions anywhere in BindConfig, and the first-run migration
# from the standalone cfg files. Usage (after build.sh built <outdir>/WildWasteland.dll):
#   MANAGED=... BEPCORE=... sh tools/config-harness/run.sh <outdir> [BepInEx/config folder with the old standalone cfg files]
# Needs mono + mcs (apt-get install -y mono-mcs mono-runtime). Color entries log "could not be parsed" here (native ColorUtility): ignore.
set -e
cd "$(dirname "$0")"
OUT=${1:?outdir with WildWasteland.dll}; OLDCFG=$2
M=${MANAGED:?}; B=${BEPCORE:?}
W=$(mktemp -d)
REFS=""; for d in mscorlib System System.Core netstandard UnityEngine UnityEngine.CoreModule Unity.InputSystem; do REFS="$REFS -r:$M/$d.dll"; done
mcs -nostdlib -noconfig -target:exe -nowarn:618 -out:"$W/Harness.exe" $REFS -r:"$B/BepInEx.dll" -r:"$B/0Harmony.dll" -r:"$OUT/WildWasteland.dll" Harness.cs
cp "$OUT/WildWasteland.dll" "$B"/*.dll "$M"/UnityEngine*.dll "$M"/Unity.*.dll "$M"/PlayMaker.dll "$M"/Micosmo.SensorToolkit.dll "$M"/NWH.*.dll "$M"/MapMagic.dll "$M"/Den.Tools.dll "$M"/Assembly-CSharp*.dll "$W/" 2>/dev/null || true
mkdir -p "$W/root/BepInEx"
[ -n "$OLDCFG" ] && cp -r "$OLDCFG" "$W/root/BepInEx/config"
cd "$W" && MONO_PATH=/usr/lib/mono/4.5:/usr/lib/mono/4.5/Facades mono Harness.exe ./root
