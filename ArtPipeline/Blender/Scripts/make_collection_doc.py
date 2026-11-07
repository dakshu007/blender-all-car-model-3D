"""make_collection_doc.py - write Documentation/COLLECTION_REFERENCE_CARS.md from the car
specs and the real build/validation reports (plain Python, no Blender needed).

    python3 make_collection_doc.py
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "cars"))

from collection_specs import ORDER, SPECS  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
REPORTS = os.path.join(ROOT, "ArtPipeline", "Blender", "Reports")


def load(name):
    p = os.path.join(REPORTS, name)
    return json.load(open(p)) if os.path.exists(p) else None


def main():
    out = ["# Reference-image collection: 8 original cars", "",
           "One original car per screenshot in the supplied `Archive.zip`. Each takes the",
           "reference's general body type, era and proportions. Names, liveries, sponsors and",
           "plates are new and fictional, and no game asset or livery is reproduced. All eight",
           "are built by `cars/build_generic.py` from `cars/collection_specs.py`, painted by",
           "`cars/livery_generic.py`, and checked by the same validation gate as CAR-009.", "",
           "| ID | Name | Reference class | Size L × W × H (m) | Wheelbase | Tris | Validation |",
           "| --- | --- | --- | --- | --- | --- | --- |"]
    for c in ORDER:
        s = SPECS[c]
        b = load(f"{c}_build.json")
        v = load(f"{c}_validation.json")
        size = b["bbox"]["size"] if b else None
        dims = f"{size[1]:.2f} × {size[0]:.2f} × {size[2]:.2f}" if size else "-"
        wb = s["wheels"]["axle_f"] - s["wheels"]["axle_r"]
        status = (f"**{v['status']}** ({sum(1 for i in v['checks'] if i['status'] == 'PASS')}/"
                  f"{len(v['checks'])})") if v else "not run"
        out.append(f"| {c} | {s['name']} | {s['reference'].split(': ', 1)[1]} | {dims} | {wb:.2f} m | "
                   f"{b['total_tris'] if b else '-'} | {status} |")
    out += ["", "Width includes mirrors. Each car has 26 objects in the standard hierarchy (`<CAR>_Root`,",
            "Body, Exterior group, Glass, Interior with steering wheel, Lights, and four wheel pivots",
            "with separate tyre and rim), one 2K livery texture, and collision boxes in their own",
            "collection.", ""]
    for c in ORDER:
        s = SPECS[c]
        w = s["wheels"]
        L = s["livery"]
        out += [f"## {c}: {s['name']}", "",
                f"![{c}](../ArtPipeline/VehicleLibrary/{c}/Previews/{c}_Preview.png)", "",
                f"* **Reference class:** {s['reference']}",
                f"* **Class / drivetrain:** {s['class']} / {s.get('drivetrain', 'AWD')}",
                f"* **Wheels:** Ø {2 * w['r']:.2f} m × {w['tire_w']:.3f} m, track {2 * w['x']:.2f} m, "
                f"{w['rim'][0]} rims ({w['rim'][1]}) in `{w['rim_color']}`",
                f"* **Flares:** {s['flares']['type'] if s.get('flares') else 'none (plain arches)'}"
                f" · **Spoiler:** {s['spoiler']['type'] if s.get('spoiler') else 'none'}"
                f"{' + roof spoiler' if s.get('roof_spoiler') else ''}",
                f"* **Livery (original):** base `{L['base']}`"
                + (f", roof `{L['roof']}`" if L.get("roof") else "")
                + f", race number {L.get('number', '-')}, plate \"{L.get('plate', '-')}\"",
                f"* **Files:** `ArtPipeline/VehicleLibrary/{c}/Working/{c}_Working_v001.blend`, "
                f"`Export/{c}_Export.fbx`, `Export/{c}_BaseColor.png`, `Export/{c}_vehicle_data.json`", ""]
    out += ["## Rebuild", "", "```bash", "cd ArtPipeline/Blender/Scripts", "```", "",
            "```bash", "BLENDER=/Applications/Blender.app/Contents/MacOS/Blender ./run_car_pipeline.sh all", "```",
            "", "Not done yet: Unity import and Play Mode test (see `BLENDER_TO_UNITY.md`), and your art review.", ""]
    path = os.path.join(ROOT, "Documentation", "COLLECTION_REFERENCE_CARS.md")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out))
    print(f"Wrote {path}")


if __name__ == "__main__":
    main()
