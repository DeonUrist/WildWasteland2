#!/bin/sh
# Builds WildWasteland.dll with mono mcs against the game's own libraries.
# Usage: MANAGED=<Apocalypter_Data/Managed> BEPCORE=<BepInEx/core> sh build.sh [outdir]
# The assembly is named after the -out file, so the file is always <outdir>/WildWasteland.dll.
# Sources are every .cs under src/ (same set as the csproj glob), so no file list to keep in sync.
# Runtime assets retain their authoring source folders but ship separately in RuntimeAssets, with a startup index.
set -e
cd "$(dirname "$0")"
M=${MANAGED:-/e/SteamLibrary/steamapps/common/Apocalypter/Apocalypter_Data/Managed}
B=${BEPCORE:-/e/SteamLibrary/steamapps/common/Apocalypter/BepInEx/core}
OUTDIR=${1:-bin}
mkdir -p "$OUTDIR"
SRC=$(find src -name '*.cs' | sort)
[ -n "$SRC" ] || { echo "build.sh: no .cs files under src/" >&2; exit 1; }
mkdir -p "$OUTDIR/RuntimeAssets"
: > "$OUTDIR/RuntimeAssets/asset-index.tsv"
find src/Modules -path '*/Embedded/*' ! -name '*.md' -type f | sort | while IFS= read -r f; do
  mod=$(printf '%s' "$f" | cut -d/ -f3)
  rel=${f#src/Modules/$mod/Embedded/}
  key=$(printf '%s' "$mod.$rel" | tr / .)
  target=$(printf '%s' "$mod/$rel" | sed -e 's#^NPC/WomenOfWasteland/#NPC/#' -e 's#^CleanHUD/LoadingScreens/#LoadingScreens/#' -e 's#^PlayerBody/Portraits/#PlayerBody/LegacyPortraits/#' -e 's#^PlayerBody/CharacterPortraits/#PlayerBody/Portraits/#' -e 's#^PlayerBody/Textures/female_kick.png$#PlayerBody/Models/Player2_female_kick.png#')
  mkdir -p "$OUTDIR/RuntimeAssets/$(dirname "$target")"
  cp "$f" "$OUTDIR/RuntimeAssets/$target"
  printf '%s\t%s\n' "$key" "$target" >> "$OUTDIR/RuntimeAssets/asset-index.tsv"
done
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
mcs -nostdlib -noconfig -target:library -langversion:latest -optimize+ -nowarn:649,414,169,219,162,618,108,114,67,168,1717,429,665 \
  -out:"$OUTDIR/WildWasteland.dll" $REFS -r:$B/BepInEx.dll -r:$B/0Harmony.dll -r:$B/Mono.Cecil.dll -r:$B/MonoMod.Utils.dll $SRC
echo "built $OUTDIR/WildWasteland.dll"
