# Environment kit: Chinese spring valley (plan)

The goal is to design and approve every world component in Blender **before** building
the game level from the *Open World Rally – Map Blueprint v1* (a 1 km × 1 km valley).
Each component is built by script, animated where it should move, rendered for
review, and exported to FBX for Unity.

## Art direction (from the supplied references)

* **Mood:** bright spring valley, like the painted blossom-path reference. Fresh
  greens, soft pink canopies, falling petals, warm sun, blue sky.
* **Style:** the same stylised low-poly language as the cars. Chunky readable
  shapes, clumped canopies with light "dab" highlights (as in the blossom-tree
  reference), flat-shaded blades of grass (as in the grass reference).
* **Chinese theme:** vermilion walls and columns, sweeping hip roofs with
  upturned eaves and ridge ornaments, cream lattice windows, red lanterns with gold
  caps, stone arch bridges, a paifang gate, a multi-tier pagoda, a lake pavilion,
  bamboo, and tea terraces.
* **Palette:** vermilion `#C8372D`, deep red `#8E2A22`, roof tile `#B5342A` (village) /
  slate `#3E4148` (temple roofs), gold `#D9A441`, lattice cream `#F1E6CF`, blossom
  pinks `#F4A7B9` → `#E0607E`, highlight `#FFE6D2`, grass `#7CC04B` → `#4E9A2F`,
  stone `#B9B2A6`, asphalt `#4A4C52`.

## Components (from the map legend and your list)

| ID | Component | Map use | Animation |
| --- | --- | --- | --- |
| ENV_CherryKing | King cherry blossom tree (hero, ~9 m) | Lake / village landmarks | Branch sway, falling petals |
| ENV_CherryTree | Cherry blossom tree (~6 m) | Scattered over the valley | Branch sway, petals |
| ENV_CherryYoung | Young cherry tree (~4 m) | Roadsides, village | Sway |
| ENV_GrassClump | Stylised grass clump | Everywhere | Wind bend (blend shape) |
| ENV_GrassPatch | Grass + wildflower patch (4 × 4 m) | Verges | Wind bend, phase-shifted |
| ENV_Bamboo | Bamboo cluster | Bamboo Forest (W) | Stalk sway |
| ENV_HouseTwoStorey | Two-storey village house (as in the reference) | Blossom Village | Lanterns swing |
| ENV_HouseShop | One-storey village shop / house | Blossom Village | Lanterns swing |
| ENV_Garage | Start garage (Chinese-roofed workshop) | Garage · START | Banner sway |
| ENV_Pagoda | Five-tier mountain pagoda | Mountain Pagoda (N) | Wind chimes / lantern sway |
| ENV_LakePavilion | Hexagonal pavilion on stilts + zig-zag walkway | Lake Pavilion | Lantern swing |
| ENV_PaifangGate | Paifang gate with plaque 花村 ("Blossom Village") | Paifang Gate | Lantern swing |
| ENV_StoneBridge | Stone arch bridge with railings | Bridges 1 & 2 | none |
| ENV_LanternHanging / ENV_LanternPost | Hanging lantern / street lantern post | Lantern Street | Swing + glow |
| ENV_Road_* | Road kit: asphalt straight and curve, gravel, dirt, lantern-street paving | Routes 1–5 | none |
| ENV_Water | River / lake surface | River & lake | Ripple (Unity shader, preview animated) |
| ENV_TeaTerrace | Tea terrace module | Tea Terraces (E) | Bush sway |

## Technical contract (Unity)

* Same conventions as the cars: metres, Blender +Y forward / +Z up, origin on the
  ground at the base centre, FBX with `axis_forward='Z'`, `axis_up='Y'`, no Apply Transform.
* Animation is exported in the FBX:
  * sway: object transform keyframes on pivot objects, a 96-frame (4 s @ 24 fps) seamless loop;
  * grass bend: a `Wind` blend shape animated on a loop.
* For hundreds of copies in the level, use a Unity wind vertex shader instead. Each
  vegetation mesh stores a **`Col` vertex-colour layer** for its look, and its
  **alpha channel holds a wind weight** (0 at the base, 1 at the tips), ready for that shader.
* Petals: keyframed petal meshes for the Blender preview. In the game, use a Unity
  particle system that reuses the petal mesh.
* Vertex-coloured materials (no textures needed) for vegetation and buildings.
  Small generated tiling textures only for road surfaces.

## Deliverables for this review

1. One editable library file: `ArtPipeline/EnvironmentLibrary/Working/ENV_Kit_v001.blend`
   (each component in its own collection).
2. Per-component preview renders in a spring-light studio, plus a contact sheet.
3. A **diorama** in the spirit of the reference painting (road through blossom trees
   to the pagoda), as a still and as an animated loop (GIF), to show the motion.
4. An FBX per component, including animation.
5. After your approval: the level layout from the map blueprint.
