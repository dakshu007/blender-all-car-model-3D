# Rally Game --- Stylized Car Asset Collection & Blender Pipeline

**Project:** Original macOS-first Unity 3D rally game\
**Art direction:** Stylized, readable, detailed low-poly\
**Workflow:** Download legally usable base models → inspect and modify
in Blender → export FBX → integrate in Unity\
**Purpose:** Reusable production instructions for Claude Code and
Blender Python automation.

------------------------------------------------------------------------

## 1. Goal

Build a consistent collection of 15 original rally vehicles for the
first content target. Use real-world rally cars as historical and visual
inspiration, but use only models whose licenses permit the intended use.
Give the in-game vehicles original names, liveries, badges, and visual
details.

Do not extract, rip, decompile, or reuse vehicle meshes, textures,
sounds, logos, or other proprietary assets from *Art of Rally* or
another commercial game. Do not assume that a free download is
commercially usable. Check the exact model's license and keep its
license record.

### Target vehicle art style

-   Stylized proportions, not toy-like unless that is an intentional
    choice.
-   Clear body silhouette and recognizable era-specific design cues.
-   Simplified but readable headlights, windows, bumpers, arches,
    spoilers, grille, mirrors and lights.
-   Controlled bevels and clean shading.
-   Consistent wheel scale and ground clearance.
-   Restrained palette, with original rally liveries.
-   No copied manufacturer badges, racing-team marks, sponsor logos, or
    game branding unless separately licensed.

The cars should feel like they belong to the same game, even when their
real-world inspirations are from different decades.

------------------------------------------------------------------------

## 2. Initial 15-car roster

These are **real-world design references**, not permission to use
protected names, badges, or branded content in the shipped game. Use
working IDs and original fictional in-game names in the project.

  -------------------------------------------------------------------------
  ID                Real-world visual Suggested          Character
                    reference         fictional working  
                                      name               
  ----------------- ----------------- ------------------ ------------------
  CAR-001           Mini Cooper S     Pocket Cooper      Small, light
                    rally                                classic

  CAR-002           Alpine A110       Alpine-inspired    Rear-engine
                                      lightweight coupe  classic

  CAR-003           Lancia Fulvia HF  Veloce FWD Classic Compact
                                                         front-wheel drive

  CAR-004           Ford Escort Mk1   Falcon Mk1 Rally   Rear-wheel-drive
                                                         classic

  CAR-005           Lancia Stratos    Mid-engine wedge   Short, wide, agile
                                      coupe              

  CAR-006           Fiat 131 Abarth   Torino 131         Boxy Group 4 sedan

  CAR-007           Opel Ascona 400   Ascona-inspired    Long-wheelbase
                                      coupe              classic

  CAR-008           Porsche 911 SC    Rear-engine        Distinctive
                    rally             911-inspired coupe rear-engine shape

  CAR-009           Audi Sport        Quattro-inspired   Wide arches, short
                    Quattro S1        AWD                wheelbase

  CAR-010           Peugeot 205 T16   205-inspired       Compact Group B
                                      mid-engine hatch   

  CAR-011           Lancia Delta S4   Delta S-inspired   Wide rally hatch
                                      AWD                

  CAR-012           MG Metro 6R4      Metro-inspired 6R  Short, wide hatch

  CAR-013           Subaru Impreza    Kumo WR            Modern-classic AWD
                    22B / WRC-era                        sedan
                    Impreza                              

  CAR-014           Mitsubishi Lancer Shogun Evo         AWD performance
                    Evolution VI                         sedan

  CAR-015           Ford Escort RS    Falcon Cosworth    Large rear wing,
                    Cosworth                             AWD coupe
  -------------------------------------------------------------------------

**Important:** If a downloaded model is a branded real vehicle, review
the model creator's license and any embedded logos/trademarks
separately. A CC0 mesh license does not necessarily grant rights to use
a third-party trademark or logo as game branding. For a commercial
release, fictionalize badges, names, liveries, and sponsor marks unless
you have permission.

### Collection priority

Do not source all 15 before validating the pipeline. Work in this order:

1.  CAR-009: a wide-body AWD rally car as the primary test vehicle.
2.  CAR-001: a small classic car to test proportions and scale
    variation.
3.  CAR-005: a distinctive mid-engine silhouette.
4.  CAR-013: a modern-classic sedan.
5.  Remaining cars after the first four pass the full Blender-to-Unity
    process.

------------------------------------------------------------------------

## 3. Free model sources and licensing

Start with these sources. Availability, download formats, and terms can
change; re-check each individual asset before using it.

### Best starting sources

1.  **Grab3D --- Rally Car**
    -   A stylized rally-car model is listed as CC0, with GLB, FBX,
        BLEND, USDZ and OBJ downloads.
    -   The listed model includes rally-oriented details such as extra
        lamps, flares, mud flaps and gravel tires.
    -   Good first candidate for testing the pipeline.
    -   https://grab3d.com/models/vehicles/rally-car/
2.  **Quaternius --- Cars Pack**
    -   Eight car models.
    -   FBX, OBJ and BLEND formats.
    -   Pack page states CC0 and free personal/commercial use.
    -   Useful as stylized base models or proportions/geometry practice,
        not necessarily as exact historic rally cars.
    -   https://quaternius.com/packs/cars.html
    -   FAQ / license guidance: https://quaternius.com/faq.html
3.  **Sketchfab**
    -   Search for individual classic rally vehicle models.
    -   Filter to downloadable models, then inspect the license on the
        exact model page.
    -   Licenses vary. Some require attribution; some prohibit
        commercial use or derivative use.
    -   https://sketchfab.com/search?type=models&q=classic%20rally%20car
4.  **Sloyd free car models**
    -   Its listed free car models use CC BY 4.0, which permits
        commercial use with attribution.
    -   Check the exact model and creator attribution requirements.
    -   https://www.sloyd.ai/free-3d-models/vehicles/cars
5.  **Unity Asset Store**
    -   Useful for Unity-ready vehicle and environment packs.
    -   Check the asset's current Asset Store license and
        render-pipeline compatibility.
    -   https://assetstore.unity.com/

### Licensing checklist

Before a model enters the game project, record:

-   Exact model title and creator.
-   Original download URL.
-   Download date.
-   License name and link.
-   Commercial use allowed?
-   Modification allowed?
-   Attribution required?
-   Redistribution inside a compiled game allowed?
-   Any restrictions on trademarks, logos, or recognizable branded
    content?
-   A copy or screenshot/PDF of the license terms where practical.

Do not add a model to the release build if its license is unclear. Do
not redistribute the raw model as a separate downloadable asset pack.
Keep a `LICENSES.md` file with the project.

------------------------------------------------------------------------

## 4. Asset intake and folder structure

Create a separate source archive and never edit the original download.

``` text
RallyGame/
  ArtPipeline/
    Incoming/
      CAR-001/
        original_download.zip
        source_model.fbx
        textures/
        LICENSE.txt
        source_notes.md
    Blender/
      Working/
      Scripts/
      Exports/
      Reports/
    VehicleLibrary/
      CAR-001/
        Source/
        Working/
        Export/
        Textures/
        Previews/
        Documentation/
      CAR-002/
      ...
  UnityProject/
    Assets/
      _Game/
        Art/
          Vehicles/
            CAR-001/
              Models/
              Materials/
              Textures/
              Prefabs/
            CAR-002/
        Scripts/
```

Rules:

-   `Incoming` is immutable. Never overwrite it.
-   `Working` contains Blender edits.
-   `Export` contains approved FBX and texture files.
-   Keep the original model and textures with the license evidence.
-   Do not put unused source archives into Unity's `Assets` folder.
-   Use one folder per vehicle with a stable ID.

### Naming conventions

-   `CAR-009_Source.blend`
-   `CAR-009_Working_v001.blend`
-   `CAR-009_Body`
-   `CAR-009_Wheel_FL`
-   `CAR-009_Wheel_FR`
-   `CAR-009_Wheel_RL`
-   `CAR-009_Wheel_RR`
-   `CAR-009_Export.fbx`
-   `CAR-009_BaseColor.png`
-   `CAR-009_Normal.png`
-   `CAR-009_Mask.png`
-   `CAR-009_Preview.png`

Use `FL`, `FR`, `RL`, `RR` consistently for front-left, front-right,
rear-left and rear-right.

------------------------------------------------------------------------

## 5. Blender preparation standard

Use Blender's native `.blend` file as the editable master. Export FBX
for Unity.

### A. Inspect before modifying

For each downloaded model, inspect:

-   Overall dimensions and orientation.
-   Object hierarchy and object names.
-   Whether the four wheels are separate objects.
-   Whether wheel origins/pivots are correctly positioned.
-   Whether the mesh has duplicate or hidden geometry.
-   Whether normals and face orientation are correct.
-   UV maps and texture assignments.
-   Material node setup.
-   Polygon count.
-   Whether the model includes an interior.
-   Whether there are missing textures or unsupported shaders.
-   Whether the source license permits the planned modifications and
    use.

Generate a report before making changes. Do not assume the model is
game-ready.

### B. Standard coordinate system

Adopt one convention across every car:

-   Blender units: meters.
-   Vehicle forward direction: local +Y in the Blender working scene,
    unless the chosen FBX export convention requires a documented
    conversion.
-   Vehicle up: +Z in Blender.
-   Vehicle origin: centered on the ground plane at the vehicle's
    longitudinal and lateral center.
-   Wheel pivots: at each wheel's axle center.
-   Apply scale and rotation to exported mesh objects where appropriate.
-   Preserve a consistent Unity import orientation and verify with a
    test cube/car before batch processing.

Do not blindly apply transforms to armatures or animated hierarchies.
Inspect the rig first.

### C. Separate components

Required separate objects where the source mesh allows:

-   Main body.
-   Front-left wheel.
-   Front-right wheel.
-   Rear-left wheel.
-   Rear-right wheel.
-   Optional steering wheel.
-   Optional brake calipers/discs.
-   Optional lights.
-   Optional interior.

Do not split a single continuous body into arbitrary fragments just to
increase object count.

### D. Wheel pivots

Each wheel must rotate around its axle center. The object's origin must
be at the center of the wheel, not at the world origin or the wheel's
mesh base.

Check:

-   Wheel axis is aligned correctly.
-   Left/right wheels rotate in the same expected direction in Unity.
-   Wheel mesh is centered on its origin.
-   Tire width and wheel diameter are plausible.
-   Wheels do not clip through the fenders at rest.
-   Front wheels have a clean steering pivot if steering animation is
    required.

If the source wheel geometry is joined to the body, separate it
carefully in Edit Mode or reconstruct the wheel only if needed. Do not
destroy the original source mesh.

### E. Geometry cleanup

-   Remove duplicate vertices where safe.
-   Recalculate normals.
-   Fix inverted faces.
-   Remove hidden interior geometry only when it is definitely
    unnecessary.
-   Keep silhouette-critical bevels.
-   Avoid destructive decimation that damages body shape.
-   Keep glass as a separate material/mesh where practical.
-   Use weighted normals or bevel modifiers only when they improve the
    final shading.
-   Check for non-manifold geometry and accidental holes.
-   Keep the source untouched.

### F. Stylized visual pass

Aim for consistency, not unnecessary detail:

-   Slightly exaggerated but believable wheel arches.
-   Clean body panel transitions.
-   Simplified grille and lamps.
-   Readable bumpers and spoilers.
-   Distinctive silhouette.
-   Limited, consistent material palette.
-   Avoid excessive tiny bolts and details that will never be visible at
    gameplay camera distance.

Do not automatically add a roof scoop, spoiler, light pod, mud flaps or
roll cage to every car. These should match the vehicle's design and
class.

------------------------------------------------------------------------

## 6. Reusable Blender Python pipeline

Claude Code should create reusable Blender scripts, not manually repeat
the same operations for every model.

### Script 1 --- `inspect_vehicle.py`

Purpose: inspect and report without modifying the source.

Report:

-   Blender version.
-   Object names and types.
-   Mesh vertex, edge, polygon and triangle counts.
-   Dimensions and world transforms.
-   Material names and missing image textures.
-   UV map names.
-   Object origins.
-   Bounding box.
-   Candidate wheel objects based on names and geometry.
-   Whether the model appears to contain four separate wheels.

Output: `Reports/CAR-XXX_inspection.json`

Wheel detection must be marked as a suggestion, not a guaranteed result.
Never automatically rename or move objects based only on a guessed name.

### Script 2 --- `prepare_vehicle.py`

Purpose: prepare a copied working file.

Possible operations, each independently enabled:

-   Rename approved objects using the project naming convention.
-   Set the vehicle root/origin.
-   Separate approved wheel meshes.
-   Set wheel origins to axle centers.
-   Apply safe mesh transforms.
-   Recalculate normals.
-   Set material names.
-   Add a simple collision-proxy collection.
-   Generate a preview render.
-   Save a new versioned `.blend`.

The script must have a dry-run mode and produce a before/after report.
It must not overwrite the source file.

### Script 3 --- `export_vehicle_fbx.py`

Purpose: export only approved vehicle objects.

Export requirements:

-   Export selected vehicle collection only.
-   Include mesh objects and required hierarchy.
-   Apply documented forward/up axis settings.
-   Use consistent unit scale.
-   Export materials only when supported and useful.
-   Do not depend on absolute local file paths.
-   Export textures separately to the vehicle's `Textures` folder.
-   Use stable names.
-   Write an export report with object count, file size and warnings.

Test the chosen FBX axis settings in Unity before batch-exporting the
collection. Do not assume the first axis configuration is correct.

### Script 4 --- `validate_vehicle.py`

Check:

-   Required body object exists.
-   Four wheel objects exist or are explicitly marked as not ready.
-   Wheel origins are close to their axle centers.
-   Model dimensions are within a plausible vehicle range.
-   No zero-scale objects.
-   No missing material references.
-   No missing texture images.
-   No empty mesh objects.
-   No duplicate object names.
-   Export file exists.
-   Polygon/triangle count is recorded.
-   Root object and forward direction are documented.

A failed validation must block the asset from being marked approved.

### Automation safety

-   Scripts must be idempotent where possible.
-   Scripts must never overwrite `Incoming`.
-   Write to a new versioned working file.
-   Make a backup before changing a working file.
-   Log all actions and warnings.
-   Do not silently guess wheel objects.
-   Ask for user review when object identification is ambiguous.
-   Do not claim a Blender script was executed unless Blender actually
    ran it and returned a successful result.

------------------------------------------------------------------------

## 7. Blender-to-Unity FBX contract

Unity's primary model interchange format is FBX. Keep the `.blend` file
as the editable source and FBX as the game integration format.

### Export hierarchy target

``` text
CAR-009_Root
  CAR-009_Body
  CAR-009_Interior
  CAR-009_Wheel_FL
  CAR-009_Wheel_FR
  CAR-009_Wheel_RL
  CAR-009_Wheel_RR
  CAR-009_Details
```

Optional visual components can be children of the appropriate parent. Do
not include lights or collision proxies in the visual FBX unless there
is a specific reason.

### Unity setup

For each imported vehicle:

1.  Import FBX into its own `Models` folder.
2.  Check Model tab scale and axis orientation.
3.  Check material import settings.
4.  Extract or recreate materials using URP-compatible shaders.
5.  Assign textures explicitly.
6.  Create a prefab.
7.  Add the vehicle root and Rigidbody through the game setup workflow.
8.  Add collision proxies separately.
9.  Assign wheel visual transforms to the vehicle controller.
10. Test wheel rotation and steering in Play Mode.

### Collision

Do not use the detailed render mesh as the primary moving vehicle
collider.

Prefer simple colliders:

-   One or more BoxColliders for the body.
-   Optional simplified collider for front/rear bumpers.
-   Wheel physics/contact logic managed by the vehicle controller.

Avoid a MeshCollider on the moving car unless there is a measured,
justified reason.

### Wheel animation

The wheel visual objects should be separate from the physics system.

Unity vehicle controller should provide:

-   Wheel rotation from vehicle speed.
-   Front-wheel steering angle.
-   Optional suspension visual movement.
-   Optional brake disc/caliper visuals.

Do not let Blender animation and Unity physics both control the same
wheel transforms at runtime.

------------------------------------------------------------------------

## 8. Vehicle data in Unity

Use a `VehicleDefinition` ScriptableObject for each car.

Suggested data:

-   Stable vehicle ID.
-   Display name.
-   Description.
-   Vehicle prefab.
-   Vehicle class.
-   Mass.
-   Acceleration.
-   Maximum speed.
-   Braking.
-   Steering response.
-   Front/rear grip.
-   Handbrake behavior.
-   Suspension tuning.
-   Surface grip multipliers.
-   Preview image.
-   Unlock state (later).

Do not hard-code vehicle tuning into the visual model. Keep visual
assets and gameplay statistics separate.

### Initial vehicle classes

  Class         Handling identity
  ------------- ----------------------------------
  Classic FWD   Predictable, understeer-oriented
  Classic RWD   Playful, throttle-sensitive
  Group 4       More grip and power
  Group B       High power, demanding control
  Modern AWD    Stable, responsive and fast

These are gameplay starting points. Tune each car by testing, not by
assuming every car must behave exactly like its real-world counterpart.

------------------------------------------------------------------------

## 9. Stylized consistency checklist

Every car must pass:

-   [ ] Same approximate visual detail density.
-   [ ] Consistent material roughness and metallic response.
-   [ ] Consistent glass appearance.
-   [ ] Consistent tire and wheel material style.
-   [ ] Similar edge/bevel treatment.
-   [ ] Readable silhouette from gameplay camera distance.
-   [ ] No accidental realistic model mixed with toy-like models.
-   [ ] No low-resolution texture stretching.
-   [ ] No missing UVs.
-   [ ] No visible shading defects.
-   [ ] No unauthorized logos or liveries.
-   [ ] Performance budget recorded.

Create a neutral studio preview scene in Blender with the same camera,
lighting, ground plane and render settings for all 15 cars. This makes
style mismatches easy to spot.

### Suggested initial performance budget

These are project targets to validate, not universal requirements:

-   Start around 15k--35k triangles per vehicle for a detailed stylized
    player car.
-   Use fewer triangles for distant AI/traffic vehicles.
-   Use 1K or 2K textures depending on the vehicle's visual importance.
-   Reuse materials and texture atlases where sensible.
-   Avoid excessive separate materials.
-   Use LODs only when testing shows they are needed.
-   Profile actual Mac hardware before locking budgets.

------------------------------------------------------------------------

## 10. Car-by-car production tracker

Use this table in `Documentation/CAR_ASSET_TRACKER.md`.

  -----------------------------------------------------------------------------------
  Vehicle   Source     License   Blender   Wheels/pivots   FBX      Unity    Play
  ID        acquired   checked   cleanup                   export   prefab   test
  --------- ---------- --------- --------- --------------- -------- -------- --------
  CAR-001   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-002   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-003   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-004   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-005   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-006   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-007   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-008   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-009   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-010   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-011   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-012   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-013   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-014   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]

  CAR-015   \[ \]      \[ \]     \[ \]     \[ \]           \[ \]    \[ \]    \[ \]
  -----------------------------------------------------------------------------------

Do not mark a car complete just because its FBX exported. It is complete
only after the Unity prefab is configured and tested.

------------------------------------------------------------------------

## 11. Claude Code master prompt

Use this prompt in the repository after saving this document as
`Documentation/CAR_ASSET_PIPELINE.md`.

``` text
You are the technical artist and tools engineer for my original
stylized 3D rally game.

Read Documentation/CAR_ASSET_PIPELINE.md before doing any work.

Environment:
- macOS
- Blender
- Unity 3D with URP
- C#
- Blender Python for repeatable asset preparation
- FBX as the Unity interchange format
- BLEND as the editable source format

Your job is to build a reliable, reusable vehicle asset pipeline.

FIRST:
1. Inspect the repository and existing Unity project.
2. Check whether Blender is installed and identify its version if
   the environment permits.
3. Inspect existing vehicle models and their license records.
4. Do not modify or overwrite original downloads.
5. Report what is present and what is missing.
6. Propose the smallest safe first implementation.

THEN, after approval:
1. Create the vehicle asset folder structure.
2. Create inspect_vehicle.py.
3. Create prepare_vehicle.py with dry-run support.
4. Create export_vehicle_fbx.py.
5. Create validate_vehicle.py.
6. Create a CAR_ASSET_TRACKER.md.
7. Create a LICENSES.md template.
8. Create a neutral Blender preview scene or a reproducible
   setup script.
9. Create Unity VehicleDefinition ScriptableObject code only
   after inspecting the existing game architecture.
10. Document exact Blender and Unity steps.

CRITICAL:
- Never download or extract proprietary game assets.
- Never assume a model is commercially licensed.
- Never overwrite source files.
- Never guess wheel object identities without inspection.
- Never silently change the Unity project's render pipeline,
  package versions, input system or physics architecture.
- Never claim a Blender script ran unless Blender executed it.
- Never claim a Unity prefab works without testing it.
- Keep every change small, reviewable and reversible.

For each model:
- Inspect geometry, dimensions, hierarchy, UVs, textures,
  materials, wheel separation and origins.
- Create a versioned working copy.
- Separate and position wheel pivots where possible.
- Preserve the model's silhouette.
- Apply the project's stylized art direction.
- Export a clean FBX and associated textures.
- Validate the exported asset.
- Integrate it into Unity and test it.

Do not batch-process the entire collection initially.
Prove the complete workflow on one car first.
Wait for my approval before processing the next cars.
```

------------------------------------------------------------------------

## 12. First implementation sprint

### Sprint 1: Prove the pipeline with one car

Use the Grab3D rally car as the first candidate because its listing
provides a stylized rally-specific model and a CC0 license. Confirm the
license at download time.

Tasks:

1.  Download the model and license information.
2.  Save the original archive in `ArtPipeline/Incoming/CAR-009/`.
3.  Open the BLEND or FBX in Blender.
4.  Run the inspection script.
5.  Check the body and all four wheels manually.
6.  Create a versioned working copy.
7.  Correct wheel origins and model orientation.
8.  Export the FBX.
9.  Import it into a temporary Unity test scene.
10. Test wheel steering, rotation, collider placement and scale.

**Sprint 1 is successful only when the car drives in Unity with
correctly rotating wheels and no obvious import or material problems.**

### Sprint 2: Prove style consistency

Repeat with one classic compact vehicle and one distinctive Group
B-style vehicle. Compare their studio previews. Fix the material, scale
and detail standards before expanding the collection.

### Sprint 3: Expand the roster

Process the remaining cars in small batches of two or three. Review each
asset before moving to the next batch.

------------------------------------------------------------------------

## 13. Definition of done for a car

A car is approved only when:

1.  Source and license are documented.
2.  Original download is preserved.
3.  Blender working file is saved.
4.  Body and wheel hierarchy is valid.
5.  Wheel pivots are checked.
6.  Materials and textures are present.
7.  FBX export passes validation.
8.  Unity prefab is configured.
9.  Vehicle controller can drive it.
10. Wheel visuals respond correctly.
11. It looks consistent with the game's art direction.
12. A preview image and tracker entry are saved.

**Final rule: one verified car pipeline first, then scale it to 15. Do
not create a large untested asset collection.**
