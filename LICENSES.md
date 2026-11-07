# Third-party assets and licenses

Every external asset must have an entry here **before** it enters the game project.
Do not ship an asset whose license is unclear. Do not redistribute raw source
models as a separate asset pack.

## CAR-009 source model: Grab3D "Rally Car"

| Field | Value |
| --- | --- |
| Title / creator | "Rally Car" by Grab3D (file metadata: "Created by grab3d") |
| Download URL | https://grab3d.com/models/vehicles/rally-car/ |
| File in repo | `ArtPipeline/Incoming/CAR-009/rally-car.fbx` (SHA-256 `702270c1e7b890885bab12b2f5fa47dd4fc22d1cf3b0cbf5933d6f86f8fc9353`) |
| Supplied / license checked | File supplied by the project owner. License page checked 2026-11-07. |
| License | **CC0 1.0** (public domain dedication): https://grab3d.com/license/ |
| Page wording (2026-11-07) | "Free under CC0 for personal and commercial use, no attribution." |
| Commercial use allowed? | Yes |
| Modification allowed? | Yes |
| Attribution required? | No (credited here anyway) |
| Redistribution in a compiled game? | Yes |
| Trademark / logo concerns | None found. The model is a generic, unbadged rally hatch with blank number panels. |
| How it is used | As a dimension and parts reference only. The shipped CAR-009 mesh is a new, original model. |

## Fonts baked into livery textures

| Asset | License | Use |
| --- | --- | --- |
| DejaVu Sans / DejaVu Sans Bold | Bitstream Vera / DejaVu free license (permits embedding in rendered images) | Race numbers, fictional sponsor names and plates painted into `CAR-XXX_BaseColor.png` |

## Reference images (NOT redistributed)

`ArtPipeline/References/*.jpeg` are eight screenshots of *Art of Rally* (© Funselektor
Labs), supplied as visual inspiration. They are **gitignored** and not part of this
repository or the game. No assets, meshes, textures, logos or liveries were extracted
or recreated from them. See `ArtPipeline/References/REFERENCE_NOTES.md`.

## Tools

* Blender 5.0.1 (GPL). Output files (.blend/.fbx/.png) are not covered by the GPL.
* Pillow (HPND), used only to paint livery textures and contact sheets.

## Original content in this repository

The CAR-009 "Brenner S1" model, its livery, and the fictional names "Brenner S1",
"HALVIK", "ORSA TYRES", "K. VARGA / L. MOEN" and "BRN 27S" were created for this
project.
