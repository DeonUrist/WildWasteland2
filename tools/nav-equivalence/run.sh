#!/bin/sh
# Nav equivalence check + micro-benchmark (see NavCheck.cs). Compares the built WildWasteland.dll against a previous build on
# synthetic structure grids, by reflection, on plain mono (no game): full/lazy field distances and Nav.Next outputs old vs new.
# Usage (after build.sh built <outdir>/WildWasteland.dll):
#   MANAGED=... BEPCORE=... sh tools/nav-equivalence/run.sh <outdir> <before.dll> [seeds=6] [pairsPerSeed=400]
# Needs mono + mcs. Exit code 1 on any mismatch (the lines start with MISMATCH), 0 when every check passed.
set -e
cd "$(dirname "$0")"
OUT=${1:?outdir with WildWasteland.dll}; BEFORE=${2:?the previous WildWasteland.dll to compare against}
SEEDS=${3:-6}; PAIRS=${4:-400}
M=${MANAGED:?}; B=${BEPCORE:?}
W=$(mktemp -d)
REFS=""; for d in mscorlib System System.Core netstandard UnityEngine UnityEngine.CoreModule; do REFS="$REFS -r:$M/$d.dll"; done
mcs -nostdlib -noconfig -target:exe -nowarn:618 -out:"$W/NavCheck.exe" $REFS -r:"$B/BepInEx.dll" -r:"$B/Mono.Cecil.dll" NavCheck.cs
cp "$B"/*.dll "$M"/UnityEngine*.dll "$M"/Unity.*.dll "$M"/PlayMaker.dll "$M"/Micosmo.SensorToolkit.dll "$M"/NWH.*.dll "$M"/MapMagic.dll "$M"/Den.Tools.dll "$M"/Assembly-CSharp*.dll "$W/" 2>/dev/null || true
cp "$OUT/WildWasteland.dll" "$W/WildWasteland.dll"
cp "$BEFORE" "$W/WildWasteland_before.dll"
cd "$W"
export MONO_PATH=/usr/lib/mono/4.5:/usr/lib/mono/4.5/Facades
mono NavCheck.exe --patch "$W"
set +e
mono NavCheck.exe --run "$W" WildWasteland.dll WildWasteland_before.dll "$SEEDS" "$PAIRS" > "$W/out.txt" 2>&1; RC=$?
grep -v "mono runtime and class\|out of sync\|When you update\|the other too\|Do not report\|you probably have\|If you see other\|and you need to\|^$" "$W/out.txt"
exit $RC
