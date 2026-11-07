# CAR-009: inspection report, design and execution plan

All numbers below come from scripts that were actually run in **Blender 5.0.1**
(the `bpy` module, Cycles CPU renders). Raw data:
`ArtPipeline/Blender/Reports/CAR-009_source_inspection.json`,
`CAR-009_prepare_source.json`, `CAR-009_build.json`, `CAR-009_validation.json`.

---

## 1. Inspection report: supplied `rally-car.fbx`

| Item | Finding |
| --- | --- |
| Origin | Grab3D **"Rally Car"**. The FBX metadata says "Created by grab3d" and it was exported from Blender 5.2 FBX IO. The live page (checked 2026-11-07) lists it as **CC0 1.0**: no attribution needed, commercial use allowed, 19,726 tris. |
| File | FBX 7400, 2.33 MB, SHA-256 `702270c1e7b8…fc9353` (unchanged after every step) |
| Hierarchy | `rally-car` (empty) → `rally-car_body`, `rally-car_windows`, `rally-car_wheel_fl/fr/rl/rr` |
| Size | 1.90 W × 4.39 L × 1.69 H m (body 1.62 m tall, plus the roof wing) |
| Orientation | **Forward = −Y.** Proven three ways: the wheels named `fl` sit at y = −1.28, x = +0.76 (driver-left when facing −Y), the mud flaps sit behind each wheel on the −Y side, and the renders show the front. The pipeline needs **+Y**, so the car must turn 180°. |
| Wheels | **4 separate objects** with origins exactly at the axle centre (offset 0.0 mm). Wheelbase 2.52 m, track 1.52 m, wheel Ø 0.68 m, width 0.25 m. Tyre, rim, hubcap and trim are all one mesh (41 loose parts) with 4 materials. |
| Body topology | **Fully triangulated "polygon soup"**: 13,784 tris in **571 disconnected shells**, 8,320 open boundary edges, 4,413 duplicate vertices and 5 zero-area faces. It has no quads or edge loops to edit, and no consistent panel structure. |
| Materials | 17 materials, 12 of which are per-wheel duplicates (`.001`–`.003`). Several flat-colour materials have an unused Normal Map node. |
| Textures | 6 images packed in the FBX: a 2K body base colour, normal and metal/roughness set, plus a 64×256 tyre set. The **livery is baked into the body UV atlas**. No textures are missing. |
| UVs | One `UVMap` per mesh |
| Interior | Steering wheel and an orange roll bar, visible through the glass |
| Look | Soft, "inflated" toy proportions with large rounded bevels everywhere, a tall wagon greenhouse, a narrow tucked-in track, a roof wing on stalks, and a pale-blue/coral palette. See `CAR-009_SourceModel_Preview.png`. |

### What to keep, improve, replace

| Keep (as reference / data) | Replace / rebuild |
| --- | --- |
| Wheel package: wheelbase ≈ 2.4–2.5 m, track ≈ 1.5 m, Ø ≈ 0.66 m | **Body shell.** The triangulated soup cannot be reshaped into crisp, chamfered planar panels, and its livery is locked to its UV layout. |
| Separate-wheel structure and axle-centre pivot convention | **Wheels.** Split into separate tyre and rim meshes with a chunky gravel tread, in a consistent style for all 15 cars. |
| Parts checklist: aux lamps, flares, flaps, number panels, cage, mirrors, roof vent | **Proportions:** lower roof, wider track, real box flares, a coupe greenhouse |
| The original file, untouched in `Incoming/` | **Materials:** one shared, consistent library instead of baked PBR textures |

**Decision.** The source is preserved and also normalised into
`Source/CAR-009_Source.blend` by `prepare_vehicle.py`. That copy is rotated to +Y,
renamed, has duplicate materials merged and a collision box added, and serves as a
dimension reference. The game car is a **new, original model** built by
`cars/build_car009.py`. The original file is never modified.

---

## 2. Art direction (from the reference images)

See `Documentation/ART_DIRECTION.md`. In short: matte, low-gloss paint;
chunky chamfered panels with a crisp shoulder crease; a separate, slightly tapered
glasshouse with dark glass; round auxiliary lamp pods; mud flaps; simple dish or
spoke wheels in gold, white or silver; bold geometric stripe liveries on a warm
off-white or primary-colour base.

## 3. First car design: **CAR-009 "Brenner S1"** (original, fictional)

An early-1980s, Group-B-style, short-wheelbase AWD coupe. It takes general era cues
only and copies no real car's body lines, badges or liveries.

* **Package:** 4.26 L × 1.92 W (1.87 m across the flares) × 1.37 H m.
  Wheelbase 2.36 m, track 1.56 m, wheels Ø 0.66 m × 0.235 m.
* **Body:** lofted lower body with a sill chamfer, a hard shoulder crease, slight
  tumblehome and a chamfered deck. The bonnet slopes down to a blunt nose, and the
  trunk deck is slightly raised in a wedge.
* **Glasshouse:** raked windscreen (~47°), long door, small rear quarter window,
  painted pillars with black window rubbers, and a roof vent.
* **Box flares:** squared bolt-on flares on all four arches with a flat shelf and a
  black inner return.
* **Front:** wraparound painted bumper with a black splitter and intakes,
  rectangular quad headlamps, a slatted grille, a **four-lamp pod**, plate and tow hook.
* **Rear:** full-width black tail panel with red/amber lamps, a black bumper,
  exhaust, and a trunk wing with end plates on black stands.
* **Details:** mirrors, door handles, side skirts, orange mud flaps, and an
  interior with tub, dash, LHD steering wheel, two bucket seats and a full roll cage.
* **Livery (original):** warm off-white base. A teal band between orange and
  mustard pinstripes runs around the car and sweeps up at the nose. Twin teal
  stripes run nose to tail, bonnet louvres are painted on, and #27 roundels sit on
  the doors and roof. Fictional names only: sponsor "HALVIK", "ORSA TYRES", crew
  "K. VARGA / L. MOEN", plate "BRN 27S".

## 4. Modelling changes vs. the source

1. Rotate to +Y forward. Root at the ground-plane centre of the bounding box.
2. Replace the triangulated body with a clean lofted shell: quads, closed and
   manifold before the boolean arch cut, and angle-limited chamfer bevels.
3. Cut real wheel arches with a boolean, so the wheel-well walls inherit black trim.
4. Add four box flares, side-projected so the livery runs across them.
5. Build a new glasshouse with window openings, a solidified shell and separate
   tinted glass.
6. Build new wheels with separate tyre (gravel blocks) and rim (6-spoke, lug nuts,
   brake disc) objects, under an empty pivot at the axle centre.
7. Use a single 2K livery atlas (`CAR-009_BaseColor.png`) projected from world space.
8. Use a shared material library of 17 materials, with no metallic/roughness/normal
   textures needed.
9. Add an interior, a roll cage, and a steering wheel as its own pivoted object.
10. Add collision boxes in a separate collection (not part of the visual FBX).

## 5. Execution plan (done = ✔)

1. ✔ Read the pipeline document. Extract the references into `ArtPipeline/References/` (local only).
2. ✔ `inspect_vehicle.py` on the source, plus source renders.
3. ✔ `prepare_vehicle.py`: dry run, then a real run to make `CAR-009_Source.blend`.
4. ✔ Design spec `cars/car009_spec.py` (one source of truth for geometry and livery).
5. ✔ `cars/livery_car009.py` paints the atlas, and `cars/build_car009.py` builds the car.
6. ✔ Iterated four times on studio renders: silhouette, flares, glass, decals, lighting.
7. ✔ `render_previews.py`: 8 views in the neutral studio, plus a contact sheet.
8. ✔ `export_vehicle_fbx.py`: FBX, texture and `CAR-009_vehicle_data.json`.
9. ✔ `validate_vehicle.py`: **PASS, 46/46** checks, including the FBX round trip and a raw
   FBX node-chain check.
10. ☐ **Unity:** import, wizard, Play Mode drive test. Not done yet: no Unity
    installation was available. Sprint 1 is complete only after this step.
11. ☐ Your review and approval before CAR-001, CAR-005 and CAR-013.

## 6. Known limitations / next steps

* The Unity C# scripts are written but **not compiled or tested**.
* Shading uses smooth-by-angle with angle-limited bevels. Check it under the game
  camera and URP lighting.
* The glass is a single-sided plane, so it is not visible from the cockpit
  (gameplay is third-person).
* The livery is a texture. Colour variants only need `livery_car009.py` to be re-run
  with a different palette.
