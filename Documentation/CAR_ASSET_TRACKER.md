# Car asset tracker

A car is complete only after its Unity prefab is configured **and play-tested**
(see `CAR_ASSET_PIPELINE.md` §13). An FBX export alone is not completion.

| Vehicle ID | Working name | Source acquired | License checked | Blender cleanup | Wheels/pivots | FBX export | Unity prefab | Play test |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CAR-001 | Pocket Cooper | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-002 | Alpine-inspired lightweight coupe | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-003 | Veloce FWD Classic | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-004 | Falcon Mk1 Rally | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-005 | Mid-engine wedge coupe | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-006 | Torino 131 | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-007 | Ascona-inspired coupe | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-008 | Rear-engine 911-inspired coupe | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| **CAR-009** | **Brenner S1** (Group B AWD coupe) | [x] | [x] | [x] | [x] | [x] | [ ] | [ ] |
| CAR-010 | 205-inspired mid-engine hatch | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-011 | Delta S-inspired AWD | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-012 | Metro-inspired 6R | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-013 | Kumo WR | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-014 | Shogun Evo | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |
| CAR-015 | Falcon Cosworth | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] | [ ] |

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
