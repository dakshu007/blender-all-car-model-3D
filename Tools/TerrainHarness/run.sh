#!/bin/bash
# Offline terrain harness: compiles the game's pure world code with a UnityEngine math stub and renders it.
# Usage: ./run.sh [--only City] [--region City|Coast|Mountains|Snow|Canyon|Valley] [--mpp 4] [--profile x0 z0 x1 z1]
#   --only Name : use only that region's real folder; the other four regions use their placeholders
#                 (so a half-finished neighbour can never break your build). Default: all real folders.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
SP=/tmp/claude-0/-home-user-blender-all-car-model-3D/73034ce3-669a-515e-8cac-ef9b7ec61b95/scratchpad
export DOTNET_ROOT=$SP/dn/root/lib/dotnet DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
S="$HERE/../../UnityProject/Assets/BlossomRally/Scripts"
ONLY=""
ARGS=()
while [ $# -gt 0 ]; do
  if [ "$1" = "--only" ]; then ONLY="$2"; shift 2; else ARGS+=("$1"); shift; fi
done
FILES="$S/WorldField.cs $S/MapData.cs $S/Spline.cs $S/Events/StageDef.cs"
for f in "$S"/World/Regions/*.cs; do case "$f" in *.Build.cs|*RegionRuntime.cs) ;; *) FILES="$FILES $f";; esac; done
for R in City Coast Mountains Snow Canyon; do
  if [ -z "$ONLY" ] || [ "$ONLY" = "$R" ]; then
    for f in $(find "$S/World/Regions/$R" -name "*.cs" ! -name "*.Build.cs"); do FILES="$FILES $f"; done
  else
    FILES="$FILES $HERE/placeholders/${R}Region.cs"
  fi
done
REFS=""
for f in $DOTNET_ROOT/packs/Microsoft.NETCore.App.Ref/*/ref/net10.0/*.dll; do REFS="$REFS -r:$f"; done
BIN=$(mktemp -d)
mkdir -p "$HERE/out"
$DOTNET_ROOT/dotnet $DOTNET_ROOT/sdk/*/Roslyn/bincore/csc.dll -nologo -noconfig -nostdlib -langversion:9.0 -optimize+ -target:exe \
  -nowarn:1701,1702,0649,0414,0162,0219 -out:"$BIN/harness.dll" $REFS "$HERE/UnityStub.cs" "$HERE/Harness.cs" $FILES
VER=$(ls $DOTNET_ROOT/shared/Microsoft.NETCore.App/ | head -1)
cat > "$BIN/harness.runtimeconfig.json" <<JSON
{ "runtimeOptions": { "tfm": "net10.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$VER" } } }
JSON
OUTD=$(mktemp -d)
$DOTNET_ROOT/dotnet "$BIN/harness.dll" --out "$OUTD" "${ARGS[@]}"
for p in "$OUTD"/*.ppm; do [ -e "$p" ] || continue; b=$(basename "${p%.ppm}"); python3 -c "
from PIL import Image
Image.open('$p').save('$HERE/out/$b.png')" && echo "image: $HERE/out/$b.png"; done
rm -rf "$BIN" "$OUTD"
