# Blender → Unity: exact steps and the FBX contract

> **Update:** the playable project in `UnityProject/` now does all of this automatically. It imports
> the FBX files from `Assets/BlossomRally/Resources`, and `CarFactory` builds each car (Rigidbody,
> collider, arcade drift physics, wheel visuals) from `CAR-xxx_vehicle_data.json` at runtime. The
> earlier WheelCollider wizard scripts were removed. The FBX axis contract below still applies.

## Conventions

| | Blender (source of truth) | Unity |
| --- | --- | --- |
| Forward | +Y | +Z |
| Up | +Z | +Y |
| Right (driver's right) | +X | +X |
| Units | metres | metres |
| Point mapping | (x, y, z) | (x, z, y) |

* Root `CAR-XXX_Root` sits at the ground-plane centre of the car's bounding box.
* Wheel pivots `CAR-XXX_Wheel_FL/FR/RL/RR` are empties at the axle centre. They
  have no rotation in Blender, and the axle runs along local X. Rotate the pivot,
  never the tyre or rim meshes.
* The steering wheel `CAR-XXX_SteeringWheel` has its column along local +Y in Blender.

## FBX export settings (`export_vehicle_fbx.py`)

`axis_forward='Z'`, `axis_up='Y'`, `apply_scale_options='FBX_SCALE_UNITS'`,
`bake_space_transform=False`, `use_mesh_modifiers=True`, `mesh_smooth_type='FACE'`,
`use_custom_props=True`, `path_mode='STRIP'`, `object_types={EMPTY, MESH}`.
Only the `CAR-XXX` collection is exported. Collision proxies are never included.

### Why not "Apply Transform" (`bake_space_transform=True`)?

This was tested on CAR-009 by parsing the exported FBX node graph:

* With Apply Transform, objects **two or more levels deep** came out with an
  un-baked `(90, 0, 180)` rotation and Blender-space translations. That includes the
  tyres and rims under their wheel pivot, and the steering wheel under the interior.
  Their placement in Unity would be wrong.
* With the setting off, the file is consistent. Only `CAR-XXX_Root` carries the axis
  conversion (`Lcl Rotation 90, 0, 180`), and every child keeps its clean Blender
  local transform.

`validate_vehicle.py` re-checks this on every export. It walks the raw FBX node
chain, converts to Unity space (FBX right-handed Y-up, with X negated for Unity's
left-handed space) and asserts that each wheel lands at its expected Unity position
with a lateral spin axis. CAR-009 result: FL (−0.78, 0.33, 1.13),
FR (0.78, 0.33, 1.13), RL (−0.78, 0.33, −1.23), RR (0.78, 0.33, −1.23). All PASS.

**What this means in Unity:** the imported `CAR-009_Root` GameObject shows a
rotation, which is the axis conversion, and its children have Blender-style local
axes. The car still appears the right way up and faces +Z.
`VehicleWheelVisuals` stores a rest offset per wheel, so it does not depend on this.
If you prefer an identity root, try the Model importer's **Bake Axis Conversion**
option and re-check the wheel spin direction.

## Unity steps (Sprint 1 test)

> These steps have **not yet been run**: no Unity installation was available in the
> authoring environment. Record the results in `CAR_ASSET_TRACKER.md`.

1. Copy `ArtPipeline/VehicleLibrary/CAR-009/Export/CAR-009_Export.fbx` to
   `Assets/_Game/Art/Vehicles/CAR-009/Models/`, and copy
   `CAR-009_BaseColor.png` to `.../CAR-009/Textures/`.
   Copy `CAR-009_vehicle_data.json` next to the FBX (Unity imports it as a TextAsset).
2. FBX **Model** tab: Scale Factor 1, *Convert Units* on. Check that the car is about
   4.26 m long and faces +Z. *Import Normals: Import*. Leave *Bake Axis Conversion*
   off unless you are testing it (see above).
3. **Materials** tab: *Extract Materials* to `.../CAR-009/Materials/`. Convert them
   to URP/Lit:
   * `M_CAR-009_Paint` and `M_CAR-009_Plate`: Base Map = `CAR-009_BaseColor`, Smoothness ≈ 0.55.
   * `M_Glass_Tint`: Surface Type *Transparent*, Base colour `#0B1013`, alpha ≈ 0.86.
   * `M_Light_*`: enable Emission with the lamp colour (low intensity).
   * `M_Chrome` / `M_<CAR>_Rim` / `M_Brake_Disc`: Metallic 1 / 0.85 / 0.8.
4. Create a `VehicleDefinition` asset (*Create → Rally → Vehicle Definition*) with
   class Group B and drivetrain AWD.
5. Open *Tools → Rally → Set Up Vehicle From Data*. Assign the FBX, the JSON and the
   definition, then click **Create in scene**. This adds a Rigidbody, BoxColliders from
   the data file, four WheelColliders at the visual pivots, `VehicleWheelVisuals` and
   `RallyTestDriveController`.
6. Add a ground plane and press Play. Use W/S, A/D and Space. Check:
   * the wheels spin forward when driving forward, and the left and right wheels
     spin the same way;
   * only the front wheels steer, and they steer to the correct side;
   * the colliders are where you expect (Gizmos), and the wheels don't sink or float;
   * the materials have no obvious import problems.
7. Save as a prefab in `.../CAR-009/Prefabs/`, then tick the tracker columns.

## Re-running the Blender side

```bash
cd ArtPipeline/Blender/Scripts
```

```bash
BLENDER=/Applications/Blender.app/Contents/MacOS/Blender ./run_car009_pipeline.sh
```

Each run writes a new `_vNNN` working file unless `OVERWRITE=1` is set. Every report
lands in `ArtPipeline/Blender/Reports/`.
