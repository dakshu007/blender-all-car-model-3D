# Car asset tracker

A car is complete only after its Unity prefab is configured **and play-tested**.
The *Unity prefab* column means the car is set up at runtime by `CarFactory` in `UnityProject/`
(physics, colliders and wheels from the exported vehicle data). The play test has to be done in Unity
(see `CAR_ASSET_PIPELINE.md` §13). An FBX export alone is not completion.

| Vehicle ID | Working name | Source acquired | License checked | Blender cleanup | Wheels/pivots | FBX export | Unity prefab | Play test |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CAR-001 | Pocket Cooper | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| **CAR-002** | **Sorrel A1** (60s-70s rear-engine fastback) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| CAR-003 | Veloce FWD Classic | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| **CAR-004** | **Falcon Mk1 Rally** (early-70s hip-kick saloon) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| CAR-005 | Mid-engine wedge coupe | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| **CAR-006** | **Torino 131** (late-70s 4-door Group 4) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| CAR-007 | Ascona-inspired coupe | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| **CAR-008** | **Kestrel 9R** (70s rear-engine coupe) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| **CAR-009** | **Brenner S1** (Group B AWD coupe) | [x] | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| CAR-010 | 205-inspired mid-engine hatch | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-011 | Delta S-inspired AWD | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-012 | Metro-inspired 6R | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-013 | Kumo WR | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-014 | Shogun Evo | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-015 | Falcon Cosworth | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| **CAR-016** | **Elster 2T** (late-60s/70s tall-glass 2-door saloon) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| **CAR-017** | **Falcon Mk2 RS** (late-70s square-cut saloon) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| **CAR-018** | **Isar 3R** (mid-80s wedge saloon, box arches) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |
| **CAR-019** | **Mistral W1** (late-70s mid-engine wedge) | n/a (original build) | [x] | [x] | [x] | [x] | [x] runtime | [ ] |

CAR-016 to CAR-019 were added for the four reference-image classes that the original
15-car roster did not cover. For the original builds, "Source acquired" and "License
checked" don't apply: there is no third-party mesh, and the column is ticked only for
the licence record of fonts and tools in `LICENSES.md`. See
`Documentation/COLLECTION_REFERENCE_CARS.md`.

## CAR-009 notes

* **Source:** Grab3D "Rally Car" (CC0 1.0), kept in `ArtPipeline/Incoming/CAR-009/`.
  It is used as a dimension and parts reference. The game model is a new, original
  build (`cars/build_car009.py`).
* **Working file:** `VehicleLibrary/CAR-009/Working/CAR-009_Working_v001.blend`
* **Export:** `VehicleLibrary/CAR-009/Export/CAR-009_Export.fbx` (26 objects,
  22,702 tris, 1 × 2K texture, 17 materials)
* **Validation:** `Blender/Reports/CAR-009_validation.json`: **PASS** (46/46)
* **Preview:** `VehicleLibrary/CAR-009/Previews/CAR-009_Preview.png`
* **Open:** Unity import, prefab and Play Mode test (Sprint 1 exit criterion), then
  your art-direction approval before CAR-001 / CAR-005 / CAR-013.
* **Performance budget:** 22,702 tris (target 15k–35k), one 2048² texture. Profile
  on Mac hardware.
