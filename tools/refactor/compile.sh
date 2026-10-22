#!/bin/sh
# Compile-only check for refactor agents (Claude cloud): no asset copying. Usage: sh tools/refactor/compile.sh [outdir]
# Prints compiler errors; exit code != 0 on failure. Reference DLLs: $MANAGED / $BEPCORE (default /home/claude/refs).
cd "$(dirname "$0")/../.."
M=${MANAGED:-/home/claude/refs/Apocalypter_Data/Managed}
B=${BEPCORE:-/home/claude/refs/BepInEx/core}
OUT=${1:-/tmp/ww2-build-$$}
mkdir -p "$OUT"; rm -f "$OUT/WildWasteland.dll"
REFS=""
for d in mscorlib System System.Core netstandard \
  UnityEngine UnityEngine.CoreModule UnityEngine.PhysicsModule UnityEngine.IMGUIModule UnityEngine.TextRenderingModule \
  UnityEngine.InputLegacyModule UnityEngine.AnimationModule UnityEngine.AudioModule UnityEngine.ParticleSystemModule \
  UnityEngine.ImageConversionModule UnityEngine.AssetBundleModule UnityEngine.UI UnityEngine.UIModule \
  UnityEngine.TerrainModule UnityEngine.TerrainPhysicsModule UnityEngine.JSONSerializeModule UnityEngine.DirectorModule \
  UnityEngine.UnityWebRequestModule UnityEngine.UnityWebRequestAudioModule \
  Unity.TextMeshPro Unity.InputSystem PlayMaker Micosmo.SensorToolkit \
  NWH.Common NWH.VehiclePhysics2 NWH.WheelController MapMagic Den.Tools \
  Assembly-CSharp Assembly-CSharp-firstpass; do REFS="$REFS -r:$M/$d.dll"; done
SRC=$(find src -name '*.cs' | sort)
mcs -nostdlib -noconfig -target:library -langversion:latest -optimize+ -nowarn:649,414,169,219,162,618,108,114,67,168,1717,429,665 \
  -out:"$OUT/WildWasteland.dll" $REFS -r:$B/BepInEx.dll -r:$B/0Harmony.dll -r:$B/Mono.Cecil.dll -r:$B/MonoMod.Utils.dll $SRC 2>&1 | grep -E 'error|Compilation' 
test -f "$OUT/WildWasteland.dll" && echo "COMPILE OK $OUT/WildWasteland.dll"
