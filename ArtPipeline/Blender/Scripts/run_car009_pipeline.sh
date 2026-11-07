#!/usr/bin/env bash
# Full, reproducible CAR-009 pipeline: livery -> build -> previews -> FBX export -> validation.
#
# macOS (Blender app):
#   BLENDER=/Applications/Blender.app/Contents/MacOS/Blender ./run_car009_pipeline.sh
# Standalone bpy module (pip install bpy==5.0.1, Python 3.11):
#   BPY_PYTHON=/path/to/venv/bin/python ./run_car009_pipeline.sh
#
# Options (env): VERSION=v001  SAMPLES=48  OVERWRITE=1 (replace an existing working file)
# Requires python3 + Pillow for the livery and contact sheet.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"            # ArtPipeline/
CAR=CAR-009
VERSION="${VERSION:-v001}"
SAMPLES="${SAMPLES:-48}"
LIB="$ROOT/VehicleLibrary/$CAR"
REPORTS="$ROOT/Blender/Reports"

run_blender() {  # run_blender script.py args...
  local script="$1"; shift
  if [[ -n "${BPY_PYTHON:-}" ]]; then
    "$BPY_PYTHON" "$script" -- "$@"
  else
    "${BLENDER:-blender}" --factory-startup -b -P "$script" -- "$@"
  fi
}

mkdir -p "$LIB"/{Working,Export,Textures,Previews,Documentation} "$REPORTS"

echo "== 1/6 livery texture"
python3 "$HERE/cars/livery_car009.py" --out "$LIB/Textures/${CAR}_BaseColor.png"

echo "== 2/6 build working file"
BUILD_ARGS=(--texture "$LIB/Textures/${CAR}_BaseColor.png"
            --out "$LIB/Working/${CAR}_Working_${VERSION}.blend"
            --report "$REPORTS/${CAR}_build.json")
[[ "${OVERWRITE:-0}" == "1" ]] && BUILD_ARGS+=(--overwrite)
run_blender "$HERE/cars/build_car009.py" "${BUILD_ARGS[@]}"
BLEND="$(python3 -c "import json;print(json.load(open('$REPORTS/${CAR}_build.json'))['saved_file'])")"

echo "== 3/6 inspect the working file"
run_blender "$HERE/inspect_vehicle.py" --input "$BLEND" --car-id "$CAR" --out "$REPORTS/${CAR}_working_inspection.json"

echo "== 4/6 studio previews"
run_blender "$HERE/render_previews.py" --input "$BLEND" --out-dir "$LIB/Previews" --prefix "$CAR" --samples "$SAMPLES"
python3 "$HERE/make_contact_sheet.py" --dir "$LIB/Previews" --prefix "$CAR" --out "$LIB/Previews/${CAR}_Preview.png" \
  --title "$CAR  Brenner S1  (working ${VERSION}) - neutral studio, Blender Cycles"

echo "== 5/6 FBX export"
run_blender "$HERE/export_vehicle_fbx.py" --blend "$BLEND" --car-id "$CAR" --out-dir "$LIB/Export" \
  --report "$REPORTS/${CAR}_export.json"

echo "== 6/6 validation"
run_blender "$HERE/validate_vehicle.py" --blend "$BLEND" --car-id "$CAR" --fbx "$LIB/Export/${CAR}_Export.fbx" \
  --report "$REPORTS/${CAR}_validation.json"
echo "Pipeline finished for $CAR"
