# Terrain harness (development tool, not shipped)

Evaluates the game's pure world code (WorldField, WorldLayout, every region's non-`.Build.cs` files) outside Unity
against a tiny `UnityEngine` math stub, prints road statistics (length, max grade, cut / fill depth, tightest radius,
height range) and renders a shaded top-down map with contours, tile borders and roads.

    ./run.sh                          # whole world -> out/world.png (4 m / px)
    ./run.sh --region Snow            # one region + 150 m margin -> out/Snow.png (2 m / px)
    ./run.sh --only Snow --region Snow   # same, other regions use placeholders/
    ./run.sh --profile 0 0 0 1500     # height profile along a line
    ./compile_game.sh [Region]        # full game + editor compile against Unity refs + Unity 6.6 API check
    ./run.sh --nostats                # skip the road geometry / junction checks (faster)
    JUNCTION_IMAGES=1 ./run.sh        # also write out/junction_<n>.ppm crops of every junction

Road checks (RoadStats.cs, JunctionCheck.cs) printed after the road table:
  * kink deg / dK: largest turn per 2 m step and largest curvature jump (1/km) - smoothness of bends;
    spacing min..max; folds (edge 1 m outside the road running backwards); "layout shift" if the final
    centreline moved > 1.5 m from the designer's Catmull-Rom line.
  * cutSlope deg: steepest ground within 26 m of the road; ">45 made": spots steeper than 45 deg where the
    natural ground is < 40 deg (cliffs made by the road cut / fill); bank %.
  * poke4 / poke8 / poke16: how far the terrain mesh (4 m grid and the 8 / 16 m LOD grids) rises above the road
    surface (must be negative); gap: drop from the road edge to the ground 0.4 m out (the skirt covers it).
  * junction contacts: mouth height mismatch; WorldField's junction list; then the real road meshes
    (World/RoadMesh.cs) per junction: overlap m2 (two surfaces = z-fighting), gap m2, seam cm.

Rule for region code: files named `*.Build.cs` may use the engine (GameObjects, meshes, materials); every other
file in `Scripts/World/Regions/<Region>/` must stay pure (Vector2/3, Color, Mathf, Rect, lists) so this harness
can compile it.
