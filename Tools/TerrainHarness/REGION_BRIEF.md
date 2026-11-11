# Region build-out brief (shared by all five region builders)

Blossom Rally is an original, stylised Chinese-themed rally game: Unity 6.6 (6000.6.8f1), built-in render pipeline,
linear colour, everything generated at runtime from C# (no scene content, no new asset files). The art style is
low-poly, clean, pastel, vertex-coloured, with soft light and a painterly mood. It is in the spirit of "Art of Rally" but
copies nothing from it. The original 1 km "Blossom Valley" sits at the world origin. The world has grown to a
**3 x 2 km map of six 1 km regions**, and you are building ONE of the five new regions. Make it look **incredible**:
super detailed, clean, cohesive, readable at rally speed, and full of life. The user asked for "insane graphics, each
and everything super detailed and clean".

## Read first (in this order)
- `UnityProject/Assets/BlossomRally/Scripts/World/Regions/WorldLayout.cs` contains the grid, blend weights, gateways,
  connector roads, border ridges and rim.
- `Scripts/World/Regions/Region.cs` is the pure API, and `Region.Build.cs` holds the content API plus `RegionContext`.
- `Scripts/WorldField.cs` holds the heights, roads, pads and ground colour, and `Scripts/MapData.cs` has `RoadDef`,
  the `Surface` enum and the valley data.
- `Scripts/World/Regions/ValleyRegion.cs` is a worked example of a region's pure half.
- `Scripts/World/Regions/WorldNoise.cs` is the noise library. Use it instead of `Mathf.PerlinNoise`.
- `Scripts/Events/StageDef.cs` describes rally events.
- The geometry toolkit is `Scripts/WorldProps/PropKit.cs`:
  - `PropModel` and the render kinds `PK` (`Props`, `Landmark`, `Strong`, `Glow`, `Water`, `Stream`, `Blades`,
    `Shafts`, `Karst`);
  - the `PG.*` helpers (boxes, beams, rods, columns, curved roofs, lanterns, boulders) and the `Pal` palette.
- More toolkit files:
  - `Scripts/World/ValleyLife.cs` from line ~737 has `LifeGeo` (triangles, frustums, ellipsoids, discs) and `LifeBatch`
    (CPU-posed animated parts).
  - `Scripts/WorldProps/NatureShapes.cs` has `Pine`, `Willow`, `BlossomTree`, `Karst` and `Lotus`.
  - `Scripts/WorldProps/PropShapes.cs` has houses, walls, roofs, towers, vehicles, carts and more.
  - `Scripts/WorldProps/WorldProps*.cs` is a big real example of placing thousands of props and merging them.
- `Scripts/MaterialLibrary.cs` provides `VertexColorWind`, `Terrain`, `Water`, `Glass`, `Glow`, `Standard(...)`,
  `Particle(...)` and `ProceduralTextures`.
- Shaders live in `Resources/Shaders/*.shader`. Use only the existing shaders and do not add new ones. Useful ones:
  - `BlossomRally/VertexColorWind` (vertex colour; alpha = wind weight; procedural detail patterns);
  - `BlossomRally/PropGlow` (lit at night) and `BlossomRally/WindowGlow`;
  - `BlossomRally/Water` and `BlossomRally/FlowFoam`;
  - `BlossomRally/PropAdditive`, `BlossomRally/HeatHaze`, `BlossomRally/Particle` and `BlossomRally/CloudPuffs`.
- `Scripts/Atmosphere.cs` exposes the statics `NightFactor`, `RainIntensity`, `Wetness`, `CloudCover`, `Gust` and
  `Sun`. `Scripts/Graphics/ColorGrading.cs` has the `Grade` struct.
- `Tools/TerrainHarness/README.md` covers the offline terrain harness. It is your eyes: use it constantly.

## Your files (strict)
- Write ONLY inside `UnityProject/Assets/BlossomRally/Scripts/World/Regions/<YourRegion>/`. Replace the placeholder
  `<YourRegion>Region.cs` and add as many files as you like.
- Files named `*.Build.cs` may use the engine (GameObject, Mesh, Material, ParticleSystem, Physics, AudioSource,
  Texture2D, ...). **Every other file must stay pure**, so the harness can compile it. Pure means only `Vector2/3/4`,
  `Color`, `Color32`, `Rect`, `Mathf`, `List` and similar; no `GameObject`, `Mesh` or `Material`.
- Use `public sealed partial class <YourRegion>Region : Region` across your files. Put any helper classes in the
  `BlossomRally` namespace with a unique prefix (for example `CityTower`, `SnowCableCar`) so they never collide with
  other regions.
- Do NOT edit any file outside your folder, and do not `git commit`; the integrator merges everything. If you need a
  framework change, finish without it and describe the request in your final report.
- Other builders are working on the other regions at the same time. Always compile and run the harness with your
  region isolated: `./compile_game.sh <YourRegion>` and `./run.sh --only <YourRegion> ...` (from `Tools/TerrainHarness`).

## Coordinates and terrain rules
- Units are metres; x = east, z = north, y = up. Your tile is `WorldLayout.RectOf(RegionId.<You>)`, 1000 x 1000 m.
- `Height(x, z)` is evaluated over your tile plus 110 m on every side (the blend band). On outer world edges it is also
  evaluated 240 m beyond the playable area, where the framework adds rim mountains.
  - It must be smooth and cheap: it is called about 1 million times, so stay at about 40 noise evaluations or fewer.
  - Use `WorldNoise.Fbm/Ridged/Cellular/Warp`.
- The framework adds the border ridges (`WorldLayout.Ridges`), with notches where connectors cross, plus the outer rim.
  Then it calls your optional `Shape()`, then the valley river/lake carve and flat pads, and finally the road cut/fill.
- The terrain mesh is a 4 m grid. Vertical walls look stretched, so keep slopes on big areas under about 55 degrees.
  For dramatic cliffs and spires, place rock meshes (`PK.Karst` / `PK.Landmark`) on steep slopes.
- **Gateways.** The connector roads (framework-owned) end at your gateway points in `WorldLayout`:
  - Within about 120 m of each gateway, your natural height must be within ±4 m of the stated `H...` constant.
  - One of your roads must START exactly at each gateway (first control point = gateway), heading away from the
    border into your region. The connector and your road then meet end to end.
- Check the harness road table for your roads and the connectors touching your region:
  - maxCut and maxFill under about 6 m (deep cuts mean your height does not follow your road);
  - grade at most 11%;
  - radius at least 12 m (hairpins are eased automatically).

## Roads (`RoadDef`, see MapData.cs)
- Name: `Road_<Region>_<Name>`.
- Surfaces: `Asphalt`, `Gravel`, `Dirt`, `Paving`, `Snow`, `Ice` or `Sand` (the last three are new; grip and dust are
  already handled).
- Width is 7–12 m.
- Optional fields: `maxGrade` (default 0.10, at most 0.12), `cutWidth`, `fixedHeight` + `heightOffset` (perfectly flat
  streets), `texture` / `tint` (for example tinted asphalt), `lines`, and `bridgeSpans` (metres along the road where
  the deck is a bridge: the terrain is left alone and you build the deck sides and piers in Build).
- Roads may meet at grade (T or X junctions at the same height). There are no tunnels and no overpasses: never cross
  two roads at different heights. Keep roads at least 40 m inside the playable area, apart from the connectors.
- Aim for 4–8 km of road per region: a main road through the region plus side roads, loops and a few showpiece
  sections. Roads are what the player sees most, so line them with detail: verges, walls, posts, signs, lamps, trees,
  spectator spots and landmarks seen across corners.

## Content API (Build.cs)
- `IEnumerator Build(RegionContext ctx)`:
  - Stamp `PropModel`s with `ctx.Stamp(model, pos, yaw, scale)`; they are merged per material per 334 m chunk.
  - Tall landmarks seen from other regions use `ctx.StampSkyline(...)`; that root is never streamed out.
  - Other calls: `ctx.Box/Capsule/MeshColliderFor` (colliders), `ctx.AddMesh` (own material or animated),
    `ctx.Ground(x, z)` (terrain mesh height — always place on this), `ctx.EdgeDist(p)`, `ctx.Free/Reserve`
    (no overlaps), `ctx.Rng`, `ctx.Progress`.
  - Yield every few ms of work.
- `Tick(float dt, Vector3 eye, float presence)` runs while you are streamed in (camera within 720 m of your tile). Use it
  for animation, effects and audio. Do not allocate per frame.
- Also available: `Grade` (colour grade blended at the borders), `OnEnter()` (car entered), `SpawnPoint/SpawnYaw`
  (fast-travel point on one of your roads), `AddStages(list)` and `Accent`/`Name`/`Hanzi`/`Tagline`.
- Pure-file hooks: `GroundColor` (vertex colour of the terrain: make it rich and varied — patches, slope rock, wet
  edges, paths), `VergeColor`, `GroundSurface`, `GrassDensity` and `GrassColors` (grass blades, cheap: use them for
  verges and meadows), `Plan()` (call `Field.AddFlatRect` / `AddPad` for building plots, plazas and car parks) and
  `Shape()`.
- Colliders: everything the car can hit needs one, such as buildings, rocks, walls, trunks, poles and barriers. Small
  decor and grass do not.
- Layers: `PK.Props` (layer 9) is culled at about 260–520 m, so use it for small things. `PK.Landmark` (layer 0) has
  no distance culling, so use it for buildings, big rocks and forest canopies.
- Night: `PK.Glow` geometry lights up automatically at night. Windows, lamps and neon should use it. Use
  `Atmosphere.NightFactor` for anything else.
- Rain: `Atmosphere.RainIntensity` and `Wetness` are global. React where it makes sense.

## Budgets (the target is a Mac M1 at 60 fps, with the whole world loaded)
- At most ~900k triangles of merged static geometry per region, at most 150 standalone renderers, at most 2000
  colliders, at most 4 particle systems.
- Build time under about 3 s on M1, roughly under 15M simple operations. Cache models and stamp them many times; vary
  them by scale, yaw and per-instance colour tweaks.
- `Tick` work at most 0.3 ms with no GC allocations.
- Grass: average density in your tile at most about 0.15. Put it where it is seen (verges, meadows).

## Verify before you finish (mandatory)
1. `cd Tools/TerrainHarness && ./compile_game.sh <YourRegion>` must report `compile OK` and `API problems: 0`.
2. `./run.sh --only <YourRegion> --region <YourRegion>`: Read the PNG and look at it critically. Iterate on layout,
   heights and roads until it reads as a great rally region. Also run `./run.sh --only <YourRegion>` for the whole-world
   view, to see how you join your neighbours.
3. Road table: no "leaves the playable area" warnings, grades at most 12%, cut/fill at most about 6 m, and connector
   rows touching your region with small cut/fill.
4. Stages: the harness prints each stage, and each must say `OK`.

## Final report (keep it under about 400 words)
- What you built: landmarks, roads, stages and effects.
- File list with line counts.
- Estimated triangles, renderers, colliders, particle systems and grass.
- Harness numbers for your roads and connectors, and the compile result.
- Anything you could not do, and any framework requests.
