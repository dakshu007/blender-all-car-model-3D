#!/usr/bin/env bash
# Full pipeline for the motorbikes in bikes/bike_specs.py (same stages and outputs as run_car_pipeline.sh):
# livery -> build -> previews -> FBX export -> validation [-> copy into the Unity project].
#
#   BPY_PYTHON=/path/to/venv/bin/python ./bikes/run_bike_pipeline.sh all          (or BIKE-001 BIKE-004 ...)
#   BLENDER=/Applications/Blender.app/Contents/MacOS/Blender ./bikes/run_bike_pipeline.sh all
#
# Env: VERSION=v001  SAMPLES=32  OVERWRITE=1 (replace an existing working file)  UNITY=1 (copy the export to
#      UnityProject/Assets/BlossomRally/Resources/Cars/<BIKE>)  SHEET=1 (rebuild BIKES_ContactSheet.png)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS="$(cd "$HERE/.." && pwd)"
ROOT="$(cd "$SCRIPTS/../.." && pwd)"
VERSION="${VERSION:-v001}"
SAMPLES="${SAMPLES:-32}"
REPORTS="$ROOT/Blender/Reports"
UNITY_CARS="$ROOT/../UnityProject/Assets/BlossomRally/Resources/Cars"

run_blender() {
  local script="$1"; shift
  if [[ -n "${BPY_PYTHON:-}" ]]; then
    "$BPY_PYTHON" "$script" -- "$@"
  else
    "${BLENDER:-blender}" --factory-startup -b -P "$script" -- "$@"
  fi
}

BIKES=("$@")
if [[ "${#BIKES[@]}" -eq 0 || "${BIKES[0]}" == "all" ]]; then
  read -r -a BIKES <<< "$(cd "$HERE" && python3 -c 'from bike_specs import ORDER; print(" ".join(ORDER))')"
fi

for BIKE in "${BIKES[@]}"; do
  LIB="$ROOT/VehicleLibrary/$BIKE"
  NAME="$(cd "$HERE" && python3 -c "from bike_specs import BIKES; print(BIKES['$BIKE']['name'])")"
  echo "=================== $BIKE  $NAME"
  mkdir -p "$LIB"/{Working,Export,Textures,Previews,Documentation} "$REPORTS"
  python3 "$HERE/livery_bike.py" --bike "$BIKE" --out "$LIB/Textures/${BIKE}_BaseColor.png"
  BUILD_ARGS=(--bike "$BIKE" --texture "$LIB/Textures/${BIKE}_BaseColor.png"
              --out "$LIB/Working/${BIKE}_Working_${VERSION}.blend" --report "$REPORTS/${BIKE}_build.json")
  [[ "${OVERWRITE:-0}" == "1" ]] && BUILD_ARGS+=(--overwrite)
  run_blender "$HERE/build_bike.py" "${BUILD_ARGS[@]}"
  BLEND="$(python3 -c "import json;print(json.load(open('$REPORTS/${BIKE}_build.json'))['saved_file'])")"
  run_blender "$SCRIPTS/render_previews.py" --input "$BLEND" --out-dir "$LIB/Previews" --prefix "$BIKE" --samples "$SAMPLES"
  python3 "$SCRIPTS/make_contact_sheet.py" --dir "$LIB/Previews" --prefix "$BIKE" --out "$LIB/Previews/${BIKE}_Preview.png" \
    --title "$BIKE  $NAME  (working ${VERSION}) - neutral studio, Blender Cycles"
  run_blender "$SCRIPTS/export_vehicle_fbx.py" --blend "$BLEND" --car-id "$BIKE" --out-dir "$LIB/Export" \
    --report "$REPORTS/${BIKE}_export.json"
  run_blender "$SCRIPTS/validate_vehicle.py" --blend "$BLEND" --car-id "$BIKE" --fbx "$LIB/Export/${BIKE}_Export.fbx" \
    --report "$REPORTS/${BIKE}_validation.json"
  if [[ "${UNITY:-0}" == "1" ]]; then
    mkdir -p "$UNITY_CARS/$BIKE"
    cp "$LIB/Export/${BIKE}_Export.fbx" "$LIB/Export/${BIKE}_BaseColor.png" "$LIB/Export/${BIKE}_vehicle_data.json" "$UNITY_CARS/$BIKE/"
    echo "copied to $UNITY_CARS/$BIKE"
  fi
done
if [[ "${SHEET:-0}" == "1" ]]; then
  python3 "$HERE/make_bike_sheet.py" --lib "$ROOT/VehicleLibrary" --out "$ROOT/VehicleLibrary/BIKES_ContactSheet.png"
fi
echo "Bike pipeline finished: ${BIKES[*]}"
