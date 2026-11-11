#!/bin/bash
# Compiles the whole game (runtime + editor) against the Unity reference assemblies and runs the Unity 6.6 API check.
# Usage: ./compile_game.sh [Name]   Name = City|Coast|Mountains|Snow|Canyon: only that region's real folder is
#        compiled, the other regions use placeholders (isolation from half-finished neighbours). No name = all real.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
SP=/tmp/claude-0/-home-user-blender-all-car-model-3D/73034ce3-669a-515e-8cac-ef9b7ec61b95/scratchpad
A="$HERE/../../UnityProject/Assets/BlossomRally"
ONLY="$1"
OUT=$(mktemp -d)
FILES=""
for f in $(find "$A/Scripts" -name "*.cs" -not -path "*/Scripts/Gamepad/*" -not -path "*/World/Regions/City/*" -not -path "*/World/Regions/Coast/*" \
           -not -path "*/World/Regions/Mountains/*" -not -path "*/World/Regions/Snow/*" -not -path "*/World/Regions/Canyon/*"); do FILES="$FILES $f"; done
for R in City Coast Mountains Snow Canyon; do
  if [ -z "$ONLY" ] || [ "$ONLY" = "$R" ]; then
    for f in $(find "$A/Scripts/World/Regions/$R" -name "*.cs"); do FILES="$FILES $f"; done
  else
    FILES="$FILES $HERE/placeholders/${R}Region.cs"
  fi
done
bash $SP/csc_unity.sh $OUT/Gamepad.dll 0 "$A/Scripts/Gamepad/GamepadBridge.cs" 2>&1 | grep -v "warning" || true
EXTRA_REFS=$OUT/Gamepad.dll bash $SP/csc_unity.sh $OUT/Game.dll 0 $FILES 2>&1 | grep -v "warning" || true
[ -f $OUT/Game.dll ] || { echo "GAME COMPILE FAILED"; rm -rf $OUT; exit 1; }
EXTRA_REFS="$OUT/Game.dll $OUT/Gamepad.dll" bash $SP/csc_unity.sh $OUT/GameEditor.dll 1 "$A"/Editor/*.cs 2>&1 | grep -v "warning" || true
[ -f $OUT/GameEditor.dll ] || { echo "EDITOR COMPILE FAILED"; rm -rf $OUT; exit 1; }
echo "compile OK"
for d in Game GameEditor; do $SP/bvenv/bin/python $SP/u66check.py $OUT/$d.dll 2>&1 | tail -3; done
rm -rf $OUT
