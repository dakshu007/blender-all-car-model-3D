#!/usr/bin/env bash
# Full pipeline for any car defined in cars/collection_specs.py:
# livery -> build -> inspect -> previews -> FBX export -> validation.
#
#   BLENDER=/Applications/Blender.app/Contents/MacOS/Blender ./run_car_pipeline.sh CAR-002 [CAR-008 ...]
#   BPY_PYTHON=/path/to/venv/bin/python ./run_car_pipeline.sh all      (or: new = CAR-020..031)
#
# Env: VERSION=v001  SAMPLES=32  OVERWRITE=1 (replace an existing working file)
# (CAR-009 has its own dedicated script: run_car009_pipeline.sh)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
VERSION="${VERSION:-v001}"
SAMPLES="${SAMPLES:-32}"
REPORTS="$ROOT/Blender/Reports"

run_blender() {
  local script="$1"; shift
  if [[ -n "${BPY_PYTHON:-}" ]]; then
    "$BPY_PYTHON" "$script" -- "$@"
  else
    "${BLENDER:-blender}" --factory-startup -b -P "$script" -- "$@"
  fi
}

CARS=("$@")
if [[ "${#CARS[@]}" -eq 0 || "${CARS[0]}" == "all" ]]; then
  read -r -a CARS <<< "$(cd "$HERE/cars" && python3 -c 'from collection_specs import ORDER; print(" ".join(ORDER))')"
elif [[ "${CARS[0]}" == "new" ]]; then   # CAR-020..031
  read -r -a CARS <<< "$(cd "$HERE/cars" && python3 -c 'from collection_specs import NEW_ORDER; print(" ".join(NEW_ORDER))')"
fi

for CAR in "${CARS[@]}"; do
  LIB="$ROOT/VehicleLibrary/$CAR"
  NAME="$(cd "$HERE/cars" && python3 -c "from collection_specs import SPECS; print(SPECS['$CAR']['name'])")"
  echo "=================== $CAR  $NAME"
  mkdir -p "$LIB"/{Working,Export,Textures,Previews,Documentation} "$REPORTS"
  python3 "$HERE/cars/livery_generic.py" --car "$CAR" --out "$LIB/Textures/${CAR}_BaseColor.png"
  BUILD_ARGS=(--car "$CAR" --texture "$LIB/Textures/${CAR}_BaseColor.png"
              --out "$LIB/Working/${CAR}_Working_${VERSION}.blend" --report "$REPORTS/${CAR}_build.json")
  [[ "${OVERWRITE:-0}" == "1" ]] && BUILD_ARGS+=(--overwrite)
  run_blender "$HERE/cars/build_generic.py" "${BUILD_ARGS[@]}"
  BLEND="$(python3 -c "import json;print(json.load(open('$REPORTS/${CAR}_build.json'))['saved_file'])")"
  run_blender "$HERE/inspect_vehicle.py" --input "$BLEND" --car-id "$CAR" --out "$REPORTS/${CAR}_working_inspection.json"
  run_blender "$HERE/render_previews.py" --input "$BLEND" --out-dir "$LIB/Previews" --prefix "$CAR" --samples "$SAMPLES"
  python3 "$HERE/make_contact_sheet.py" --dir "$LIB/Previews" --prefix "$CAR" --out "$LIB/Previews/${CAR}_Preview.png" \
    --title "$CAR  $NAME  (working ${VERSION}) - neutral studio, Blender Cycles"
  run_blender "$HERE/export_vehicle_fbx.py" --blend "$BLEND" --car-id "$CAR" --out-dir "$LIB/Export" \
    --report "$REPORTS/${CAR}_export.json"
  run_blender "$HERE/validate_vehicle.py" --blend "$BLEND" --car-id "$CAR" --fbx "$LIB/Export/${CAR}_Export.fbx" \
    --report "$REPORTS/${CAR}_validation.json"
done
echo "Pipeline finished: ${CARS[*]}"
